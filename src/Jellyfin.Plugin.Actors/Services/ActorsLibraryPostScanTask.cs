using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Actors.Services;

public sealed class ActorsLibraryPostScanTask : ILibraryPostScanTask
{
    private readonly ActorLibraryIndex _index;
    private readonly ILogger<ActorsLibraryPostScanTask> _logger;

    public ActorsLibraryPostScanTask(ActorLibraryIndex index, ILogger<ActorsLibraryPostScanTask> logger)
    {
        _index = index;
        _logger = logger;
    }

    public Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Actors] Library scan finished — queueing actor index rebuild.");
        _index.QueueRebuild("post-scan");
        progress.Report(100);
        return Task.CompletedTask;
    }
}
