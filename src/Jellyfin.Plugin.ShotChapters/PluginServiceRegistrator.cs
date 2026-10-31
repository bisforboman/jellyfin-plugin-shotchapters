using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>Registers the plugin's services: Jellyfin finds media segment providers only in its service collection.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost) =>
        serviceCollection.AddSingleton<IMediaSegmentProvider, BlackSegmentProvider>();
}
