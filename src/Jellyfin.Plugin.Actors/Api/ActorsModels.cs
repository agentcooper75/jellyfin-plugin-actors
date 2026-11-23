namespace Jellyfin.Plugin.Actors.Api;

public sealed class ActorsListResponse
{
    public IReadOnlyList<ActorDto> Items { get; init; } = Array.Empty<ActorDto>();

    public int TotalRecordCount { get; init; }

    public int StartIndex { get; init; }

    public int PageSize { get; init; }

    public string SortBy { get; init; } = "Name";

    public DateTime? LibraryMappingLastSyncedUtc { get; init; }

    public IReadOnlyList<string> ConfiguredLibraryIds { get; init; } = Array.Empty<string>();

    public bool HasConfiguredLibraries { get; init; }
}

public sealed class ActorDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? PrimaryImageTag { get; init; }

    public int ItemCount { get; init; }
}

public sealed class ActorItemsResponse
{
    public Guid PersonId { get; init; }

    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<FilmographyItemDto> Items { get; init; } = Array.Empty<FilmographyItemDto>();

    public int TotalRecordCount { get; init; }
}

public sealed class FilmographyItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public int? ProductionYear { get; init; }

    public string? PrimaryImageTag { get; init; }

    public string? Overview { get; init; }
}

public sealed class LibraryDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string CollectionType { get; init; } = string.Empty;

    public bool Included { get; init; }
}
