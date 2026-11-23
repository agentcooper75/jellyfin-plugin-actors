using System.Security.Claims;
using Jellyfin.Plugin.Actors.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Actors.Api;

[ApiController]
[Authorize]
[Route("Actors")]
public sealed class ActorsController : ControllerBase
{
    private readonly ActorLibraryIndex _index;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ActorsController> _logger;

    public ActorsController(
        ActorLibraryIndex index,
        ILibraryManager libraryManager,
        ILogger<ActorsController> logger)
    {
        _index = index;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ActorsListResponse> GetActors(
        [FromQuery] int startIndex = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string sortBy = "Name")
    {
        var configured = _index.GetConfiguredLibraryIds();
        var pageSize = Math.Clamp(limit <= 0 ? (Plugin.Instance?.Configuration.DefaultPageSize ?? 50) : limit, 1, 200);
        var sort = string.Equals(sortBy, "ItemCount", StringComparison.OrdinalIgnoreCase) ? "ItemCount" : "Name";

        var items = _index.Query(searchTerm, sort, Math.Max(0, startIndex), pageSize, out var total);

        return Ok(new ActorsListResponse
        {
            Items = items.Select(a => new ActorDto
            {
                Id = a.PersonId,
                Name = a.Name,
                PrimaryImageTag = a.PrimaryImageTag,
                ItemCount = a.ItemCount
            }).ToList(),
            TotalRecordCount = total,
            StartIndex = Math.Max(0, startIndex),
            PageSize = pageSize,
            SortBy = sort,
            LibraryMappingLastSyncedUtc = _index.LastBuildTimeUtc,
            ConfiguredLibraryIds = configured,
            HasConfiguredLibraries = configured.Count > 0
        });
    }

    [HttpGet("{personId:guid}/Items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ActorItemsResponse> GetActorItems([FromRoute] Guid personId)
    {
        var actor = _index.GetActor(personId);
        if (actor is null)
        {
            return NotFound();
        }

        var filmography = new List<FilmographyItemDto>();
        foreach (var itemId in actor.ItemIds)
        {
            if (_libraryManager.GetItemById(itemId) is not BaseItem item)
            {
                continue;
            }

            filmography.Add(new FilmographyItemDto
            {
                Id = item.Id,
                Name = item.Name ?? string.Empty,
                Type = item.GetType().Name,
                ProductionYear = item.ProductionYear,
                PrimaryImageTag = item.PrimaryImageTag(),
                Overview = item.Overview
            });
        }

        filmography = filmography
            .OrderByDescending(i => i.ProductionYear ?? 0)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new ActorItemsResponse
        {
            PersonId = actor.PersonId,
            Name = actor.Name,
            Items = filmography,
            TotalRecordCount = filmography.Count
        });
    }

    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryDto>> GetLibraries()
    {
        var configured = _index.GetConfiguredLibraryIds().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var libraries = _index.GetAvailableLibraries()
            .Select(l => new LibraryDto
            {
                Id = l.Id,
                Name = l.Name,
                CollectionType = l.CollectionType,
                Included = configured.Contains(l.Id)
            })
            .ToList();

        return Ok(libraries);
    }

    [HttpPost("Libraries/RefreshMapping")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult RefreshMapping([FromQuery] string? reason = null)
    {
        var label = string.IsNullOrWhiteSpace(reason) ? "manual/api" : reason.Trim();
        _logger.LogInformation("[Actors] Refresh mapping requested ({Reason}).", label);
        _index.Rebuild();
        return NoContent();
    }

    // Keep claims helper available for future per-user filtering.
    private static Guid? GetUserIdFromClaims(ClaimsPrincipal principal)
    {
        string[] claimTypes = { "Jellyfin-UserId", "UserId", ClaimTypes.NameIdentifier };
        foreach (var claimType in claimTypes)
        {
            var claim = principal.Claims.FirstOrDefault(c =>
                c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));
            if (claim is not null && Guid.TryParse(claim.Value, out var userId))
            {
                return userId;
            }
        }

        return null;
    }
}
