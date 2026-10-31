using System.Globalization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ShotChapters.Configuration;

/// <summary>The plugin's settings (the dashboard's plugin page).</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the spacing aimed for between chapters, in minutes.</summary>
    public int ChapterIntervalMinutes { get; set; } = 5;

    /// <summary>Gets or sets ShotDetector's downscale factor; 0: its automatic one (scenedetect's).</summary>
    public int Downscale { get; set; }

    /// <summary>Gets or sets a value indicating whether a file is scanned on several cores at once.</summary>
    public bool UseSeveralCores { get; set; }

    /// <summary>Gets the settings that change the chapters, as a string kept with each video's record.</summary>
    internal string Key => string.Create(CultureInfo.InvariantCulture, $"v1;interval={IntervalMinutes};downscale={Downscale}");

    /// <summary>Gets the interval, at least one minute.</summary>
    internal int IntervalMinutes => Math.Max(1, ChapterIntervalMinutes);
}
