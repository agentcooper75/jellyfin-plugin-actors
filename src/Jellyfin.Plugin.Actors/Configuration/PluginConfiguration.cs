using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Actors.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Library item IDs that feed the actor index.
    /// Empty means no libraries — the overview stays empty on purpose.
    /// </summary>
    public string[] IncludedLibraryIds { get; set; } = Array.Empty<string>();

    public int DefaultPageSize { get; set; } = 50;

    public bool EnableMainMenuEntry { get; set; } = true;
}
