namespace Jellyfin.Plugin.ShotChapters;

/// <summary>Where a video's end credits are, as a guess from its cuts. A test: the task only logs and records it.</summary>
/// <param name="StartTicks">The cut where the credits start.</param>
/// <param name="EndTicks">The next cut (a scene after the credits), or the end of the video.</param>
internal sealed record CreditsSpan(long StartTicks, long EndTicks);

/// <summary>
/// Guesses end credits from the cuts alone: a long stretch with no cut that ends at, or shortly before, the end. Scrolling
/// credits are one long "shot". Measured on 2026-11-08 (issue #10) on 4 Blender films and 3 public-domain features: the
/// strict thresholds here found Big Buck Bunny's and Sintel's credits to within 1 s, missed 3 films' shorter credits, and
/// fired on nothing else. Looser ones took the final long take of His Girl Friday and Detour for credits, and that's
/// the worst mistake (a client would skip the ending), so a miss is preferred.
/// </summary>
internal static class CreditsGuess
{
    /// <summary>The shortest stretch without a cut taken as credits, in seconds.</summary>
    internal const double MinSeconds = 120;

    /// <summary>The longest time left after it (a scene after the credits, a logo), in seconds.</summary>
    internal const double MaxTailSeconds = 60;

    /// <summary>Guesses where the end credits are.</summary>
    /// <param name="cutSeconds">The cuts' times, in any order.</param>
    /// <param name="durationSeconds">The video's length.</param>
    /// <returns>Their start and end, in seconds; null when there's no such stretch.</returns>
    public static (double Start, double End)? Guess(IEnumerable<double> cutSeconds, double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(cutSeconds);
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
            if (end - start >= MinSeconds)
                return (start, end);
        }

        return null;
    }
}
