using Jellyfin.Plugin.Actors.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Actors;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ActorLibraryIndex>();
        serviceCollection.AddSingleton<ILibraryPostScanTask, ActorsLibraryPostScanTask>();
        serviceCollection.AddHostedService<ActorsStartupHostedService>();
        serviceCollection.AddTransient<IStartupFilter, ActorsStartupFilter>();
    }
}
