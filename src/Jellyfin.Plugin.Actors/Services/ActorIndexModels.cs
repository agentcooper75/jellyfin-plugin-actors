namespace Jellyfin.Plugin.Actors.Services;

public sealed class ActorIndexEntry
{
    public Guid PersonId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? PrimaryImageTag { get; init; }

    public HashSet<Guid> ItemIds { get; init; } = new();

    public int ItemCount => ItemIds.Count;
}

public sealed class LibraryOption
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string CollectionType { get; init; } = string.Empty;
}
