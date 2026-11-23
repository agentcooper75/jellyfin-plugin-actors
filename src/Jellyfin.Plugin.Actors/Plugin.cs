using Jellyfin.Plugin.Actors.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Actors;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override Guid Id => Guid.Parse("7c2e9a41-5f8b-4d3e-9a1c-0b6e8f2a4c10");

    public override string Name => "Actors";

    public override string Description =>
        "Schauspieler-Übersicht, gefiltert auf fest konfigurierte Libraries.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var prefix = GetType().Namespace;

        yield return new PluginPageInfo
        {
            Name = "actors-config",
            DisplayName = "Actors",
            EmbeddedResourcePath = $"{prefix}.Configuration.config.html",
            EnableInMainMenu = false
        };

        yield return new PluginPageInfo
        {
            Name = "actors-config.js",
            EmbeddedResourcePath = $"{prefix}.Configuration.actors-config.js"
        };

        yield return new PluginPageInfo
        {
            Name = "actors",
            DisplayName = "Schauspieler",
            EmbeddedResourcePath = $"{prefix}.Web.actors.html",
            EnableInMainMenu = true,
            MenuSection = "server",
            MenuIcon = "person"
        };

        yield return new PluginPageInfo
        {
            Name = "actors.js",
            EmbeddedResourcePath = $"{prefix}.Web.actors.js"
        };
    }
}
