namespace Jellyfin.Plugin.ShotChapters;

/// <summary>Finds stretches of black frames from each frame's mean brightness.</summary>
internal static class BlackSpans
{
    /// <summary>
    /// The runs of consecutive frames darker than <paramref name="threshold"/> that last at least
    /// <paramref name="minSeconds"/>: from the first dark frame's time to the time of the first frame after the run (the
    /// video's end for a run that reaches it). A frame without a value counts as not dark.
    /// </summary>
    /// <param name="brightness">Each frame's mean of its B, G and R values (0-255; ShotDetector's <c>average_rgb</c>), or null.</param>
    /// <param name="frameSeconds">The time of a frame, by index.</param>
    /// <param name="endSeconds">The video's end.</param>
    /// <param name="threshold">A frame is dark below this (12: scenedetect's detect-threshold default).</param>
    /// <param name="minSeconds">Shorter runs are left out.</param>
    /// <returns>The runs, in seconds, in order.</returns>
    public static IReadOnlyList<(double Start, double End)> Find(
        IReadOnlyList<double?> brightness, Func<int, double> frameSeconds, double endSeconds, double threshold, double minSeconds)
    {
        ArgumentNullException.ThrowIfNull(brightness);
        ArgumentNullException.ThrowIfNull(frameSeconds);
        var spans = new List<(double, double)>();
        int? first = null;
        for (int i = 0; i <= brightness.Count; i++)
        {
            bool dark = i < brightness.Count && brightness[i] is { } v && v < threshold;
            if (dark)
            {
                first ??= i;
                continue;
            }

            if (first is { } f)
            {
                double start = frameSeconds(f), end = i < brightness.Count ? frameSeconds(i) : endSeconds;
                if (end - start >= minSeconds)
                    spans.Add((start, end));
                first = null;
            }
        }

        return spans;
    }
}
