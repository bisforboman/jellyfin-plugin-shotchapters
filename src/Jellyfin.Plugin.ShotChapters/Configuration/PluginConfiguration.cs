using System.Globalization;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ShotChapters.Configuration;

/// <summary>The plugin's settings (the dashboard's plugin page).</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>A frame is black below this mean brightness (0-255): scenedetect's detect-threshold default.</summary>
    internal const double BlackThreshold = 12;

    /// <summary>Gets or sets the spacing aimed for between chapters, in minutes.</summary>
    public int ChapterIntervalMinutes { get; set; } = 5;

    /// <summary>Gets or sets ShotDetector's downscale factor; 0: its automatic one (scenedetect's).</summary>
    public int Downscale { get; set; }

    /// <summary>Gets or sets a value indicating whether a file is scanned on several cores at once.</summary>
    public bool UseSeveralCores { get; set; }

    /// <summary>Gets or sets a value indicating whether black stretches become media segments.</summary>
    public bool MarkBlackSegments { get; set; } = true;

    /// <summary>Gets or sets the shortest black stretch that becomes a segment, in seconds.</summary>
    public double MinBlackSeconds { get; set; } = 1;

    /// <summary>Gets or sets the segment type black stretches get (Unknown: clients take no action on it by default).</summary>
    public MediaSegmentType BlackSegmentType { get; set; } = MediaSegmentType.Unknown;

    /// <summary>
    /// Gets the settings that change what a scan finds, as a string kept with each video's record (the segment type
    /// isn't one: the provider applies it when it reports).
    /// </summary>
    internal string Key => string.Create(
        CultureInfo.InvariantCulture,
        $"v2;interval={IntervalMinutes};downscale={Downscale};black={(MarkBlackSegments ? $"{BlackThreshold}:{BlackMinSeconds}" : "off")}");

    /// <summary>Gets the interval, at least one minute.</summary>
    internal int IntervalMinutes => Math.Max(1, ChapterIntervalMinutes);

    /// <summary>Gets the shortest black stretch kept, at least one frame's worth (0.04 s).</summary>
    internal double BlackMinSeconds => Math.Max(0.04, MinBlackSeconds);
}
