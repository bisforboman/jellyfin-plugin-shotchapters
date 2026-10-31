namespace Jellyfin.Plugin.ShotChapters;

/// <summary>What a video's current chapters are, as far as this plugin is concerned.</summary>
internal enum ChapterKind
{
    /// <summary>None, or a single one: Jellyfin itself treats a file with one chapter as having none.</summary>
    None,

    /// <summary>Jellyfin's dummy chapters: one every <c>DummyChapterDuration</c> seconds from 0 (FFProbeVideoInfo.CreateDummyChapters).</summary>
    Dummy,

    /// <summary>The chapters this plugin saved last time.</summary>
    Ours,

    /// <summary>Anything else: the file's own chapters, or ones another plugin wrote. Never replaced.</summary>
    Other,
}

/// <summary>Classifies a video's chapters.</summary>
internal static class ExistingChapters
{
    /// <summary>Classifies chapters by their start positions.</summary>
    /// <param name="startTicks">The chapters' start positions, in ticks, in order.</param>
    /// <param name="dummyChapterSeconds">The server's <c>DummyChapterDuration</c> (0 or less: off).</param>
    /// <param name="ours">The start positions this plugin saved for the video last time, or null.</param>
    /// <returns>Their kind.</returns>
    public static ChapterKind Classify(IReadOnlyList<long> startTicks, int dummyChapterSeconds, IReadOnlyList<long>? ours)
    {
        ArgumentNullException.ThrowIfNull(startTicks);
        if (ours is { Count: > 1 } && startTicks.SequenceEqual(ours))
            return ChapterKind.Ours;
        if (startTicks.Count <= 1)
            return ChapterKind.None;
        if (dummyChapterSeconds > 0)
        {
            long step = TimeSpan.FromSeconds(dummyChapterSeconds).Ticks;
            bool dummy = true;
            for (int i = 0; i < startTicks.Count && dummy; i++)
                dummy = startTicks[i] == i * step;
            if (dummy)
                return ChapterKind.Dummy;
        }

        return ChapterKind.Other;
    }
}

/// <summary>What the task does with a video.</summary>
internal enum ChapterAction
{
    /// <summary>Nothing: it has chapters of its own, or ours, or was scanned already with no chapters to show.</summary>
    Skip,

    /// <summary>Save the recorded chapters again: a metadata refresh replaced them with Jellyfin's.</summary>
    Restore,

    /// <summary>Scan it: never scanned, or the file or the settings changed since.</summary>
    Scan,
}

/// <summary>Decides what the task does with a video.</summary>
internal static class ChapterDecision
{
    /// <summary>Decides from its current chapters and the plugin's record of it.</summary>
    /// <param name="kind">Its current chapters' kind (<see cref="ExistingChapters.Classify"/>).</param>
    /// <param name="record">The plugin's record of it, or null.</param>
    /// <param name="fileModifiedTicks">The file's modification time now.</param>
    /// <param name="settings">The settings now.</param>
    /// <returns>The action.</returns>
    public static ChapterAction Decide(ChapterKind kind, ChapterRecord? record, long fileModifiedTicks, string settings)
    {
        if (kind == ChapterKind.Other)
            return ChapterAction.Skip;
        if (record is null || record.FileModifiedTicks != fileModifiedTicks || record.Settings != settings)
            return ChapterAction.Scan;
        if (kind == ChapterKind.Ours || record.Chapters.Count == 0)
            return ChapterAction.Skip;
        return ChapterAction.Restore;
    }
}
