using ShotDetector;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>What a scan found: the cuts, the video's length and its black stretches (seconds).</summary>
/// <param name="Cuts">The cuts with their strengths.</param>
/// <param name="DurationSeconds">The video's length, from the shots.</param>
/// <param name="Black">The black stretches; empty when not asked for.</param>
internal sealed record ScanResult(IReadOnlyList<Cut> Cuts, double DurationSeconds, IReadOnlyList<(double Start, double End)> Black);

/// <summary>Runs ShotDetector on a file and gives its cuts with their strengths, and its black stretches.</summary>
internal static class ShotScanner
{
    /// <summary>The per-frame metric used as a cut's strength: the HSV difference to the frame before.</summary>
    internal const string StrengthKey = "content_val";

    /// <summary>The per-frame metric the black stretches come from: the frame's mean B, G and R (ThresholdDetector's).</summary>
    internal const string BrightnessKey = "average_rgb";

    /// <summary>
    /// Finds a video's cuts with ShotDetector's adaptive detector (scenedetect's default) and, when
    /// <paramref name="black"/> is given, its black stretches, with the threshold detector in the same pass (its fade
    /// cuts then count as cuts too).
    /// </summary>
    /// <param name="path">The video file.</param>
    /// <param name="ffmpegDirectory">The folder with ffmpeg and ffprobe (jellyfin-ffmpeg 8 works; 6.1 lacks options ShotDetector passes).</param>
    /// <param name="downscale">ShotDetector's downscale factor; null: its automatic one.</param>
    /// <param name="parallel">Score the file in chunks on several cores (ShotDetector's default), else in one pass.</param>
    /// <param name="black">The brightness a black frame is under and the shortest stretch kept, or null for none.</param>
    /// <param name="cancellationToken">Stops the scan (and its ffmpeg processes).</param>
    /// <returns>What it found.</returns>
    public static async Task<ScanResult> ScanAsync(
        string path, string? ffmpegDirectory, int? downscale, bool parallel, (double Threshold, double MinSeconds)? black,
        CancellationToken cancellationToken)
    {
        var options = new DetectionOptions
        {
            Detectors = black is { } b
                ? [new DetectorSettings(DetectorKind.Adaptive), new DetectorSettings(DetectorKind.Threshold) { Threshold = b.Threshold }]
                : [new DetectorSettings(DetectorKind.Adaptive)],
            CollectStats = true,
            Downscale = downscale,
            FfmpegDirectory = ffmpegDirectory,
            // The executable: jellyfin-ffmpeg's libraries are a patched build in another folder, untested in-process.
            Decoder = VideoDecoder.FfmpegProcess,
            Mode = parallel ? DetectionMode.Auto : DetectionMode.Sequential,
        };
        var result = await ShotDetection.DetectAsync(path, options, cancellationToken).ConfigureAwait(false);
        var stats = result.Stats;
        var cuts = result.Cuts
            .Select(c => new Cut(c.Seconds, stats?.Get(checked((int)c.FrameNum), StrengthKey) ?? 0))
            .ToList();
        double duration = result.Shots.Count > 0 ? result.Shots[^1].End.Seconds : 0;
        IReadOnlyList<(double, double)> spans = [];
        if (black is { } bl && stats is not null)
        {
            var fps = result.Video.FrameRate;
            var brightness = Enumerable.Range(0, result.FrameCount).Select(i => stats.Get(i, BrightnessKey)).ToList();
            spans = BlackSpans.Find(brightness, i => FrameTime.Frame(i, fps).Seconds, duration, bl.Threshold, bl.MinSeconds);
        }

        return new ScanResult(cuts, duration, spans);
    }
}
