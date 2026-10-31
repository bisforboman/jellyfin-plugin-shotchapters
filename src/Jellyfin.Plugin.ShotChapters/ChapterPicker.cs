namespace Jellyfin.Plugin.ShotChapters;

/// <summary>A cut between two shots: where it is and how strong a change of picture it is.</summary>
/// <param name="Seconds">The time of the first frame of the new shot.</param>
/// <param name="Strength">How different the frame is from the one before (ShotDetector's <c>content_val</c>).</param>
internal readonly record struct Cut(double Seconds, double Strength);

/// <summary>Chooses chapter starts from a video's cuts.</summary>
internal static class ChapterPicker
{
    /// <summary>
    /// Chapter starts, in seconds: 0, then one for each multiple of <paramref name="intervalSeconds"/>, at the strongest
    /// cut less than half an interval from it (the earliest on a tie), so a video gets about one chapter per interval.
    /// A chapter starts at least half an interval after the previous one and at least <paramref name="edgeSeconds"/>
    /// before the end; a multiple with no such cut gets no chapter. Deterministic: the same cuts give the same chapters.
    /// </summary>
    /// <param name="cuts">The cuts, in any order.</param>
    /// <param name="durationSeconds">The video's length.</param>
    /// <param name="intervalSeconds">The spacing aimed for.</param>
    /// <param name="edgeSeconds">No chapter starts this close to the end.</param>
    /// <returns>The chapter starts, ascending, starting with 0.</returns>
    public static IReadOnlyList<double> Pick(IEnumerable<Cut> cuts, double durationSeconds, double intervalSeconds, double edgeSeconds)
    {
        ArgumentNullException.ThrowIfNull(cuts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(edgeSeconds);

        var sorted = cuts.Where(c => double.IsFinite(c.Seconds) && double.IsFinite(c.Strength))
            .OrderBy(c => c.Seconds)
            .ToList();
        var starts = new List<double> { 0 };
        double half = intervalSeconds / 2, last = 0, end = durationSeconds - edgeSeconds;
        for (int k = 1; k * intervalSeconds - half < end; k++)
        {
            double from = Math.Max(k * intervalSeconds - half, last + half), to = k * intervalSeconds + half;
            Cut? best = null;
            foreach (var c in sorted)
            {
                if (c.Seconds < from)
                    continue;
                if (c.Seconds >= to || c.Seconds > end)
                    break;
                if (best is null || c.Strength > best.Value.Strength)
                    best = c;
            }

            if (best is { } chosen)
            {
                starts.Add(chosen.Seconds);
                last = chosen.Seconds;
            }
        }

        return starts;
    }
}
