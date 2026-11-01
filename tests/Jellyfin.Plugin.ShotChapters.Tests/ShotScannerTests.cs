using System.Diagnostics;
using ShotDetector;

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
    static string MakeClip(string ffmpegDirectory) => MakeClip(
        ffmpegDirectory,
        "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=8",
        "-f", "lavfi", "-i", "smptebars=size=320x240:rate=25:duration=8",
        "-f", "lavfi", "-i", "mandelbrot=size=320x240:rate=25",
        "-filter_complex", "[2]trim=duration=8,setpts=PTS-STARTPTS[m];[0][1][m]concat=n=3:v=1[v]");

    /// <summary>8 s of a test pattern, 3 s of black, 8 s of colour bars.</summary>
    static string MakeBlackClip(string ffmpegDirectory) => MakeClip(
        ffmpegDirectory,
        "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=8",
        "-f", "lavfi", "-i", "color=c=black:size=320x240:rate=25:duration=3",
        "-f", "lavfi", "-i", "smptebars=size=320x240:rate=25:duration=8",
        "-filter_complex", "[0][1][2]concat=n=3:v=1[v]");

    /// <summary>ffmpeg's own <c>-vf blackdetect=d=..</c> spans of a file, from its log.</summary>
    static List<(double, double)> Blackdetect(string ffmpegDirectory, string path, double minSeconds)
    {
        var start = new ProcessStartInfo(Path.Combine(ffmpegDirectory, "ffmpeg")) { RedirectStandardError = true };
        foreach (string arg in (string[])["-nostdin", "-hide_banner", "-i", path, "-map", "0:v:0", "-vf",
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"blackdetect=d={minSeconds}"), "-an", "-f", "null", "-"])
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        string log = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return [.. System.Text.RegularExpressions.Regex.Matches(log, @"black_start:([0-9.]+) black_end:([0-9.]+)")
            .Select(m => (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)))];
    }

    static string MakeClip(string ffmpegDirectory, params string[] inputs)
    {
        string path = Path.Combine(Path.GetTempPath(), $"shotchapters-{Guid.NewGuid():N}.mp4");
        var start = new ProcessStartInfo(Path.Combine(ffmpegDirectory, "ffmpeg"))
        {
            RedirectStandardError = true,
        };
        foreach (string arg in (string[])["-v", "error", "-y", .. inputs, "-map", "[v]", "-c:v", "mpeg4", "-q:v", "3", "-pix_fmt", "yuv420p", path])
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
            var (cuts, duration, black) = await ShotScanner.ScanAsync(clip, ffmpeg, null, parallel, null, TestContext.Current.CancellationToken);
            Assert.Empty(black);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindsTheBlackStretchAsBlackdetectDoes(bool parallel)
    {
        string ffmpeg = FfmpegDirectory();
        string clip = MakeBlackClip(ffmpeg);
        try
        {
            var (cuts, duration, black) = await ShotScanner.ScanAsync(clip, ffmpeg, null, parallel, new BlackDetectOptions { MinDuration = TimeSpan.FromSeconds(1) }, TestContext.Current.CancellationToken);
            Assert.Equal([(8.0, 11.0)], black);
            Assert.Equal(Blackdetect(ffmpeg, clip, 1), black);
            Assert.Equal(19.0, duration, 3);
            Assert.Contains(cuts, c => c.Seconds == 8.0);
            Assert.Contains(cuts, c => c.Seconds == 11.0);

            // Longer than the stretch: none.
            var (_, _, none) = await ShotScanner.ScanAsync(clip, ffmpeg, null, false, new BlackDetectOptions { MinDuration = TimeSpan.FromSeconds(3.5) }, TestContext.Current.CancellationToken);
            Assert.Empty(none);
        }
        finally
        {
            File.Delete(clip);
        }
    }
}
