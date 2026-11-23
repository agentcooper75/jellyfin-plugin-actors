using System.Collections;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Actors.Services;

/// <summary>
/// Builds personId → filmography mappings for the configured libraries only.
/// Empty IncludedLibraryIds yields an empty index on purpose.
/// </summary>
public sealed class ActorLibraryIndex
{
    private static readonly TimeSpan DefaultRebuildDebounce = TimeSpan.FromSeconds(3);

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<ActorLibraryIndex> _logger;
    private readonly object _dataLock = new();
    private readonly object _rebuildLock = new();
    private readonly object _scheduleLock = new();

    private Dictionary<Guid, ActorIndexEntry> _actors = new();
    private DateTime _lastBuildTime = DateTime.MinValue;
    private CancellationTokenSource? _scheduledRebuild;

    public ActorLibraryIndex(
        ILibraryManager libraryManager,
        IUserManager userManager,
        ILogger<ActorLibraryIndex> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    public DateTime? LastBuildTimeUtc
    {
        get
        {
            lock (_dataLock)
            {
                return _lastBuildTime == DateTime.MinValue
                    ? null
                    : DateTime.SpecifyKind(_lastBuildTime, DateTimeKind.Utc);
            }
        }
    }

    public void EnsureBuilt()
    {
        if (LastBuildTimeUtc.HasValue)
        {
            return;
        }

        Rebuild();
    }

    public void QueueRebuild(string reason, TimeSpan? debounce = null)
    {
        var delay = debounce ?? DefaultRebuildDebounce;
        CancellationTokenSource cts;
        lock (_scheduleLock)
        {
            _scheduledRebuild?.Cancel();
            _scheduledRebuild?.Dispose();
            _scheduledRebuild = new CancellationTokenSource();
            cts = _scheduledRebuild;
        }

        _logger.LogInformation("[Actors] Scheduled index rebuild in {DelayMs}ms (reason: {Reason})", delay.TotalMilliseconds, reason);

        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                }

                Rebuild();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Actors] Scheduled rebuild failed (reason: {Reason})", reason);
            }
            finally
            {
                lock (_scheduleLock)
                {
                    if (ReferenceEquals(_scheduledRebuild, cts))
                    {
                        _scheduledRebuild.Dispose();
                        _scheduledRebuild = null;
                    }
                }
            }
        });
    }

    public void Rebuild()
    {
        lock (_rebuildLock)
        {
            var included = GetConfiguredLibraryIds();
            if (included.Count == 0)
            {
                _logger.LogInformation("[Actors] No libraries configured — clearing actor index.");
                lock (_dataLock)
                {
                    _actors = new Dictionary<Guid, ActorIndexEntry>();
                    _lastBuildTime = DateTime.UtcNow;
                }

                return;
            }

            _logger.LogInformation("[Actors] Building actor index for {Count} libraries…", included.Count);

            var queryUser = GetAnyQueryUser();
            if (queryUser is null)
            {
                _logger.LogWarning("[Actors] No users available yet — cannot build index.");
                return;
            }

            var next = new Dictionary<Guid, ActorIndexEntry>();
            var items = GetLibraryItems(queryUser, included);
            _logger.LogInformation("[Actors] Scanning {Count} movies/series for actors…", items.Count);

            foreach (var item in items)
            {
                IReadOnlyList<PersonInfo> people;
                try
                {
                    people = _libraryManager.GetPeople(item);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[Actors] Failed to read people for item {ItemId}", item.Id);
                    continue;
                }

                if (people is null || people.Count == 0)
                {
                    continue;
                }

                foreach (var personInfo in people)
                {
                    if (!IsActor(personInfo) || string.IsNullOrWhiteSpace(personInfo.Name))
                    {
                        continue;
                    }

                    var person = ResolvePerson(personInfo);
                    if (person is null)
                    {
                        continue;
                    }

                    if (!next.TryGetValue(person.Id, out var entry))
                    {
                        entry = new ActorIndexEntry
                        {
                            PersonId = person.Id,
                            Name = person.Name ?? personInfo.Name.Trim(),
                            PrimaryImageTag = ResolvePrimaryImageTag(person),
                            ItemIds = new HashSet<Guid>()
                        };
                        next[person.Id] = entry;
                    }

                    entry.ItemIds.Add(item.Id);
                }
            }

            lock (_dataLock)
            {
                _actors = next;
                _lastBuildTime = DateTime.UtcNow;
            }

            _logger.LogInformation("[Actors] Index ready: {Count} actors.", next.Count);
        }
    }

    public IReadOnlyList<ActorIndexEntry> Query(
        string? searchTerm,
        string sortBy,
        int startIndex,
        int limit,
        out int totalCount)
    {
        EnsureBuilt();

        List<ActorIndexEntry> snapshot;
        lock (_dataLock)
        {
            snapshot = _actors.Values.ToList();
        }

        IEnumerable<ActorIndexEntry> filtered = snapshot;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            filtered = filtered.Where(a =>
                a.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        IOrderedEnumerable<ActorIndexEntry> ordered = string.Equals(sortBy, "ItemCount", StringComparison.OrdinalIgnoreCase)
            ? filtered.OrderByDescending(a => a.ItemCount).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            : filtered.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase);

        var list = ordered.ToList();
        totalCount = list.Count;
        return list.Skip(Math.Max(0, startIndex)).Take(Math.Clamp(limit, 1, 200)).ToList();
    }

    public ActorIndexEntry? GetActor(Guid personId)
    {
        EnsureBuilt();
        lock (_dataLock)
        {
            return _actors.TryGetValue(personId, out var entry) ? entry : null;
        }
    }

    public IReadOnlyList<LibraryOption> GetAvailableLibraries()
    {
        try
        {
            var folders = _libraryManager.GetVirtualFolders();
            return folders
                .Where(f => !string.IsNullOrWhiteSpace(f.ItemId))
                .Select(f => new LibraryOption
                {
                    Id = NormalizeLibraryId(f.ItemId),
                    Name = string.IsNullOrWhiteSpace(f.Name) ? f.ItemId : f.Name,
                    CollectionType = f.CollectionType?.ToString() ?? string.Empty
                })
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Actors] Failed to list libraries.");
            return Array.Empty<LibraryOption>();
        }
    }

    public IReadOnlyList<string> GetConfiguredLibraryIds()
    {
        var raw = Plugin.Instance?.Configuration.IncludedLibraryIds ?? Array.Empty<string>();
        return raw
            .Select(NormalizeLibraryId)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<BaseItem> GetLibraryItems(object queryUser, IReadOnlyList<string> includedLibraryIds)
    {
        var included = includedLibraryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<BaseItem>();
        var seen = new HashSet<Guid>();

        foreach (var libraryIdText in included)
        {
            if (!Guid.TryParse(libraryIdText, out var libraryId))
            {
                continue;
            }

            try
            {
                var query = new InternalItemsQuery
                {
                    Recursive = true,
                    IsVirtualItem = false,
                    ParentId = libraryId,
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series }
                };

                TrySetUser(query, queryUser);

                var items = _libraryManager.GetItemList(query);
                foreach (var item in items)
                {
                    if (item is not (Movie or Series) || !seen.Add(item.Id))
                    {
                        continue;
                    }

                    var folders = _libraryManager.GetCollectionFolders(item);
                    var matches = folders.Any(folder =>
                        included.Contains(NormalizeLibraryId(folder.Id.ToString("N"))) ||
                        included.Contains(NormalizeLibraryId(folder.Id.ToString("D"))));
                    if (matches)
                    {
                        results.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Actors] Failed querying library {LibraryId}", libraryIdText);
            }
        }

        return results;
    }

    private Person? ResolvePerson(PersonInfo personInfo)
    {
        try
        {
            if (personInfo.Id != Guid.Empty &&
                _libraryManager.GetItemById(personInfo.Id) is Person byId)
            {
                return byId;
            }

            return _libraryManager.GetPerson(personInfo.Name);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Actors] Failed resolving person '{Name}'", personInfo.Name);
            return null;
        }
    }

    private static string? ResolvePrimaryImageTag(BaseItem person)
    {
        if (!person.HasImage(ImageType.Primary, 0))
        {
            return null;
        }

        var info = person.GetImageInfo(ImageType.Primary, 0);
        return info?.DateModified.Ticks.ToString("x");
    }

    private static bool IsActor(PersonInfo personInfo)
    {
        return personInfo.Type is PersonKind.Actor or PersonKind.GuestStar;
    }

    private object? GetAnyQueryUser()
    {
        try
        {
            var getFirstUser = _userManager.GetType().GetMethod("GetFirstUser", Type.EmptyTypes);
            if (getFirstUser?.Invoke(_userManager, null) is { } first)
            {
                return first;
            }

            var usersProperty = _userManager.GetType().GetProperty("Users");
            if (usersProperty?.GetValue(_userManager) is IEnumerable users)
            {
                foreach (var user in users)
                {
                    if (user is not null)
                    {
                        return user;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Actors] Failed enumerating users.");
        }

        return null;
    }

    private static void TrySetUser(InternalItemsQuery query, object queryUser)
    {
        var userProperty = typeof(InternalItemsQuery).GetProperty("User");
        if (userProperty is null)
        {
            return;
        }

        try
        {
            userProperty.SetValue(query, queryUser);
        }
        catch
        {
            // User type differs across Jellyfin versions; query still works without it.
        }
    }

    public static string NormalizeLibraryId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return Guid.TryParse(trimmed, out var guid) ? guid.ToString("N") : trimmed;
    }
}

internal static class PersonImageExtensions
{
    public static string? PrimaryImageTag(this BaseItem item)
    {
        if (!item.HasImage(ImageType.Primary, 0))
        {
            return null;
        }

        var info = item.GetImageInfo(ImageType.Primary, 0);
        return info?.DateModified.Ticks.ToString("x");
    }
}
