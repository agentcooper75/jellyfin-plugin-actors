using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Actors;

/// <summary>
/// Injects a "Schauspieler" menuLinks entry into GET /web/config.json
/// so the page appears in Jellyfin Web navigation without writing to disk.
/// </summary>
internal sealed class ActorsConfigJsonMiddleware
{
    private const string ConfigJsonPath = "/web/config.json";
    private const string LinkName = "Schauspieler";
    private const string LinkIcon = "person";
    private const string LinkUrl = "#/configurationpage?name=actors";

    private readonly RequestDelegate _next;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<ActorsConfigJsonMiddleware> _logger;

    public ActorsConfigJsonMiddleware(
        RequestDelegate next,
        IApplicationPaths applicationPaths,
        ILogger<ActorsConfigJsonMiddleware> logger)
    {
        _next = next;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) ||
            !string.Equals(context.Request.Path.Value, ConfigJsonPath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || !configuration.EnableMainMenuEntry)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var configFilePath = Path.Combine(_applicationPaths.WebPath, "config.json");
        if (!File.Exists(configFilePath))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        try
        {
            var configText = await File.ReadAllTextAsync(configFilePath).ConfigureAwait(false);
            var root = JsonNode.Parse(configText) as JsonObject;
            if (root is null)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            var menuLinks = root["menuLinks"] as JsonArray;
            if (menuLinks is null)
            {
                menuLinks = new JsonArray();
                root["menuLinks"] = menuLinks;
            }

            var hasLink = menuLinks.Any(node =>
                node is JsonObject link &&
                (string.Equals(link["name"]?.GetValue<string>(), LinkName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(link["url"]?.GetValue<string>(), LinkUrl, StringComparison.OrdinalIgnoreCase)));

            if (!hasLink)
            {
                menuLinks.Add(new JsonObject
                {
                    ["name"] = LinkName,
                    ["icon"] = LinkIcon,
                    ["url"] = LinkUrl
                });
                _logger.LogDebug("[Actors] Injected Schauspieler menu link into config.json response.");
            }

            var modifiedJson = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            context.Response.ContentType = "application/json";
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(modifiedJson, Encoding.UTF8).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Actors] Failed to inject menu link into config.json; falling through.");
            await _next(context).ConfigureAwait(false);
        }
    }
}

internal sealed class ActorsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<ActorsConfigJsonMiddleware>();
            next(app);
        };
    }
}
