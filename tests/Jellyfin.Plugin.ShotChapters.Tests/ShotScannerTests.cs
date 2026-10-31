using System.Diagnostics;

namespace Jellyfin.Plugin.ShotChapters.Tests;

/// <summary>
/// A real scan through ffmpeg. Needs SHOTCHAPTERS_FFMPEG: a folder with an FFmpeg 8 ffmpeg and ffprobe (jellyfin-ffmpeg's
/// version; CI downloads one). Skipped without it.
/// </summary>
public class ShotScannerTests
{
    static string FfmpegDirectory()
    {
        string? dir = Environment.GetEnvironmentVariable("SHOTCHAPTERS_FFMPEG");
        if (string.IsNullOrEmpty(dir))
            Assert.Skip("SHOTCHAPTERS_FFMPEG isn't set.");
        return dir;
    }

    /// <summary>Three 8 s shots of different test patterns: cuts at 8 s and 16 s.</summary>
    static string MakeClip(string ffmpegDirectory)
    {
        string path = Path.Combine(Path.GetTempPath(), $"shotchapters-{Guid.NewGuid():N}.mp4");
        var start = new ProcessStartInfo(Path.Combine(ffmpegDirectory, "ffmpeg"))
        {
            RedirectStandardError = true,
        };
        foreach (string arg in new[]
        {
            "-v", "error", "-y",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=8",
            "-f", "lavfi", "-i", "smptebars=size=320x240:rate=25:duration=8",
            "-f", "lavfi", "-i", "mandelbrot=size=320x240:rate=25",
            "-filter_complex", "[2]trim=duration=8,setpts=PTS-STARTPTS[m];[0][1][m]concat=n=3:v=1[v]",
            "-map", "[v]", "-c:v", "mpeg4", "-q:v", "3", "-pix_fmt", "yuv420p", path,
        })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        string errors = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, errors);
        return path;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindsTheCutsWithTheirStrengths(bool parallel)
    {
        string ffmpeg = FfmpegDirectory();
        string clip = MakeClip(ffmpeg);
        try
        {
            var (cuts, duration) = await ShotScanner.ScanAsync(clip, ffmpeg, null, parallel, TestContext.Current.CancellationToken);
            Assert.Equal([8.0, 16.0], cuts.Select(c => c.Seconds));
            Assert.All(cuts, c => Assert.True(c.Strength > 30, $"strength {c.Strength}"));
            Assert.Equal(24.0, duration, 3);

            // At a 10 s interval (5 s edge): one chapter at each cut.
            Assert.Equal([0.0, 8, 16], ChapterPicker.Pick(cuts, duration, 10, 5));
        }
        finally
        {
            File.Delete(clip);
        }
    }
}
