using ShotDetector;

namespace Jellyfin.Plugin.ShotChapters;

/// <summary>Runs ShotDetector on a file and gives its cuts with their strengths.</summary>
internal static class ShotScanner
{
    /// <summary>The per-frame metric used as a cut's strength: the HSV difference to the frame before.</summary>
    internal const string StrengthKey = "content_val";

    /// <summary>Finds a video's cuts with ShotDetector's adaptive detector (scenedetect's default).</summary>
    /// <param name="path">The video file.</param>
    /// <param name="ffmpegDirectory">The folder with ffmpeg and ffprobe (jellyfin-ffmpeg 8 works; 6.1 lacks options ShotDetector passes).</param>
    /// <param name="downscale">ShotDetector's downscale factor; null: its automatic one.</param>
    /// <param name="parallel">Score the file in chunks on several cores (ShotDetector's default), else in one pass.</param>
    /// <param name="cancellationToken">Stops the scan (and its ffmpeg processes).</param>
    /// <returns>The cuts, and the video's length in seconds from the shots.</returns>
    public static async Task<(IReadOnlyList<Cut> Cuts, double DurationSeconds)> ScanAsync(
        string path, string? ffmpegDirectory, int? downscale, bool parallel, CancellationToken cancellationToken)
    {
        var options = new DetectionOptions
        {
            Detector = DetectorKind.Adaptive,
            CollectStats = true,
            Downscale = downscale,
            FfmpegDirectory = ffmpegDirectory,
            // The executable: jellyfin-ffmpeg's libraries are a patched build in another folder, untested in-process.
            Decoder = VideoDecoder.FfmpegProcess,
            Mode = parallel ? DetectionMode.Auto : DetectionMode.Sequential,
        };
        var result = await ShotDetection.DetectAsync(path, options, cancellationToken).ConfigureAwait(false);
        var cuts = result.Cuts
            .Select(c => new Cut(c.Seconds, result.Stats?.Get(checked((int)c.FrameNum), StrengthKey) ?? 0))
            .ToList();
        double duration = result.Shots.Count > 0 ? result.Shots[^1].End.Seconds : 0;
        return (cuts, duration);
    }
}
