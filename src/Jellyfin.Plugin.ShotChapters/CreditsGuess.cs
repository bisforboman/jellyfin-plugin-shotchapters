namespace Jellyfin.Plugin.ShotChapters;

/// <summary>Where a video's end credits are, as a guess from its cuts. A test: the task only logs and records it.</summary>
/// <param name="StartTicks">The cut where the credits start.</param>
/// <param name="EndTicks">The next cut (a scene after the credits), or the end of the video.</param>
internal sealed record CreditsSpan(long StartTicks, long EndTicks);

/// <summary>
/// Guesses end credits from the cuts and the frames' content_val: a long stretch with no cut that ends at, or shortly
/// before, the end, and that moves the way credits do. Scrolling credits are one long "shot" whose content_val stays
/// in a narrow band (the text moves the same amount each frame); text cards are still. Long takes are neither.
/// Measured on 2026-11-08 (issue #10) on 4 Blender films and 5 public-domain features: credits had 53 to 100% of their
/// windows steady or still, every other stretch of 40 s or more at most 25%. With cuts alone (rule 1), the final long
/// takes of His Girl Friday and Detour were mistaken for credits unless stretches were at least 2 minutes long; with
/// the windows, 60 s is enough, and 5 of the films' credits are found instead of 2, with no false guess.
/// </summary>
internal static class CreditsGuess
{
    /// <summary>The rule's version, recorded with each guess; a record with another one is guessed again.</summary>
    internal const int Rule = 2;

    /// <summary>The shortest stretch without a cut taken as credits, in seconds.</summary>
    internal const double MinSeconds = 60;

    /// <summary>The longest time left after it (a scene after the credits, a logo), in seconds.</summary>
    internal const double MaxTailSeconds = 60;

    /// <summary>The share of its windows that have to be steady or still.</summary>
    internal const double MinCreditsShare = 0.4;

    /// <summary>The length of a window, in seconds.</summary>
    internal const double WindowSeconds = 4;

    /// <summary>A steady window's spread: its content_val's interquartile range, as a share of its median.</summary>
    /// <remarks>The quartiles, not the extremes: low-bitrate encodes repeat frames, which spike content_val.</remarks>
    internal const double SteadyBand = 0.3;

    /// <summary>The median content_val above which a window is moving (below it, it's still or nearly so).</summary>
    internal const double MovingMedian = 0.5;

    /// <summary>The 90th percentile of content_val below which a window is still.</summary>
    internal const double StillLevel = 0.3;

    /// <summary>Guesses where the end credits are.</summary>
    /// <param name="cutSeconds">The cuts' times, in any order.</param>
    /// <param name="durationSeconds">The video's length.</param>
    /// <param name="creditsShare">
    /// The share of a stretch's windows (from, to, in seconds) that are steady or still: <see cref="CreditsShare"/>.
    /// </param>
    /// <returns>Their start and end, in seconds; null when there's no such stretch.</returns>
    public static (double Start, double End)? Guess(
        IEnumerable<double> cutSeconds, double durationSeconds, Func<double, double, double> creditsShare)
    {
        ArgumentNullException.ThrowIfNull(cutSeconds);
        ArgumentNullException.ThrowIfNull(creditsShare);
        if (!(durationSeconds > MinSeconds))
            return null;

        var bounds = cutSeconds.Where(t => double.IsFinite(t) && t > 0 && t < durationSeconds)
            .Order()
            .Prepend(0)
            .Append(durationSeconds)
            .ToList();
        // The latest qualifying stretch: the one nearest the end.
        for (int i = bounds.Count - 1; i > 0; i--)
        {
            double start = bounds[i - 1], end = bounds[i];
            if (end < durationSeconds - MaxTailSeconds)
                break;
            if (end - start >= MinSeconds && creditsShare(start, end) >= MinCreditsShare)
                return (start, end);
        }

        return null;
    }

    /// <summary>
    /// The share of a stretch's <see cref="WindowSeconds"/> windows that are steady (moving, with content_val in a
    /// narrow band) or still. Half a second at each end is left out: the frames around a cut.
    /// </summary>
    /// <param name="contentVal">Each frame's content_val; NaN where there's none (the first frame).</param>
    /// <param name="fps">The frame rate: frame i is at i / fps seconds.</param>
    /// <param name="start">The stretch's start, in seconds.</param>
    /// <param name="end">Its end.</param>
    /// <returns>The share, from 0 to 1; 0 when it holds no whole window.</returns>
    public static double CreditsShare(IReadOnlyList<float> contentVal, double fps, double start, double end)
    {
        ArgumentNullException.ThrowIfNull(contentVal);
        if (!(fps > 0))
            return 0;
        int first = Math.Max(0, (int)((start + 0.5) * fps));
        int last = Math.Min(contentVal.Count, (int)((end - 0.5) * fps));
        int size = Math.Max(1, (int)(WindowSeconds * fps));
        int windows = 0, credits = 0;
        List<float> values = new(size);
        for (int from = first; from + size <= last; from += size)
        {
            values.Clear();
            for (int i = from; i < from + size; i++)
            {
                if (!float.IsNaN(contentVal[i]))
                    values.Add(contentVal[i]);
            }

            if (values.Count == 0)
                continue;
            values.Sort();
            windows++;
            double median = Quantile(values, 0.5);
            bool still = Quantile(values, 0.9) < StillLevel;
            bool steady = median > MovingMedian && (Quantile(values, 0.75) - Quantile(values, 0.25)) / median < SteadyBand;
            if (still || steady)
                credits++;
        }

        return windows == 0 ? 0 : (double)credits / windows;
    }

    static double Quantile(List<float> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
}
