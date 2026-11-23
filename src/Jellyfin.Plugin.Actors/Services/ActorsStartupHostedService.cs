using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Actors.Services;

public sealed class ActorsStartupHostedService : IHostedService
{
    private readonly ActorLibraryIndex _index;
    private readonly ILogger<ActorsStartupHostedService> _logger;

    public ActorsStartupHostedService(ActorLibraryIndex index, ILogger<ActorsStartupHostedService> logger)
    {
        _index = index;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Actors] Startup — scheduling initial actor index build.");
        _index.QueueRebuild("startup", TimeSpan.FromSeconds(8));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
