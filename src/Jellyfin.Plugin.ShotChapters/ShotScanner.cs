using ShotDetector;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>What a scan found: the cuts, the video's length and its black stretches (seconds).</summary>
/// <param name="Cuts">The cuts with their strengths.</param>
/// <param name="DurationSeconds">The video's length, from the shots.</param>
/// <param name="Black">The black stretches (blackdetect's black_start and black_end); empty when not asked for.</param>
internal sealed record ScanResult(IReadOnlyList<Cut> Cuts, double DurationSeconds, IReadOnlyList<(double Start, double End)> Black);

/// <summary>Runs ShotDetector on a file and gives its cuts with their strengths, and its black stretches.</summary>
internal static class ShotScanner
{
    /// <summary>The per-frame metric used as a cut's strength: the HSV difference to the frame before.</summary>
    internal const string StrengthKey = "content_val";

    /// <summary>
    /// Finds a video's cuts with ShotDetector's adaptive detector (scenedetect's default) and, when
    /// <paramref name="black"/> is given, its black stretches: ffmpeg blackdetect's spans, on the same pass.
    /// </summary>
    /// <param name="path">The video file.</param>
    /// <param name="ffmpegDirectory">The folder with ffmpeg and ffprobe (jellyfin-ffmpeg 8 works; 6.1 lacks options ShotDetector passes).</param>
    /// <param name="downscale">ShotDetector's downscale factor; null: its automatic one.</param>
    /// <param name="parallel">Score the file in chunks on several cores (ShotDetector's default), else in one pass.</param>
    /// <param name="black">blackdetect's options (its d, pic_th and pix_th), or null for no black stretches.</param>
    /// <param name="cancellationToken">Stops the scan (and its ffmpeg processes).</param>
    /// <returns>What it found.</returns>
    public static async Task<ScanResult> ScanAsync(
        string path, string? ffmpegDirectory, int? downscale, bool parallel, BlackDetectOptions? black,
        CancellationToken cancellationToken)
    {
        var options = new DetectionOptions
        {
            Detector = DetectorKind.Adaptive,
            BlackDetect = black,
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
        IReadOnlyList<(double, double)> spans = result.BlackSpans?.Select(b => (b.Start, b.End)).ToList() ?? [];
        return new ScanResult(cuts, duration, spans);
    }
}
