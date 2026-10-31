using System.Globalization;
using Jellyfin.Plugin.ShotChapters.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>Shot Chapters: chapters from shot cuts, for videos without chapters of their own.</summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    /// <param name="applicationPaths">The server's paths.</param>
    /// <param name="xmlSerializer">The server's XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>Gets the loaded plugin.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Shot Chapters";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("a021d41c-4b7d-41e1-a024-faac0116f5b6");

    /// <inheritdoc />
    public override string Description => "Chapters from shot cuts (ShotDetector) for videos without chapters of their own.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace),
        },
    ];
}
