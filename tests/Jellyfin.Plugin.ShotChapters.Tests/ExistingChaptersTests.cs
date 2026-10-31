namespace Jellyfin.Plugin.ShotChapters.Tests;

public class ExistingChaptersTests
{
    static readonly long Minute = TimeSpan.TicksPerMinute;

    [Theory]
    [InlineData(new long[0])]
    [InlineData(new long[] { 0 })]
    public void NoneOrOneIsNone(long[] ticks) =>
        Assert.Equal(ChapterKind.None, ExistingChapters.Classify(ticks, 300, null));

    [Fact]
    public void JellyfinsEvenSpacingIsDummy() =>
        Assert.Equal(ChapterKind.Dummy, ExistingChapters.Classify([0, 5 * Minute, 10 * Minute], 300, null));

    [Fact]
    public void EvenSpacingIsOtherWhenDummyChaptersAreOff() =>
        Assert.Equal(ChapterKind.Other, ExistingChapters.Classify([0, 5 * Minute, 10 * Minute], 0, null));

    [Fact]
    public void AnotherSpacingIsTheFilesOwn() =>
        Assert.Equal(ChapterKind.Other, ExistingChapters.Classify([0, 4 * Minute, 10 * Minute], 300, null));

    [Fact]
    public void TheRecordedOnesAreOurs() =>
        Assert.Equal(ChapterKind.Ours, ExistingChapters.Classify([0, 4 * Minute, 10 * Minute], 300, [0, 4 * Minute, 10 * Minute]));

    [Fact]
    public void AnythingElseIsNotOurs() =>
        Assert.Equal(ChapterKind.Other, ExistingChapters.Classify([0, 4 * Minute, 9 * Minute], 300, [0, 4 * Minute, 10 * Minute]));

    static ChapterRecord Record(params long[] chapters) => new() { FileModifiedTicks = 7, Settings = "s", Chapters = chapters };

    [Theory]
    [InlineData((int)ChapterKind.None)]
    [InlineData((int)ChapterKind.Dummy)]
    [InlineData((int)ChapterKind.Ours)]
    public void NeverScannedOrChangedIsScanned(int value)
    {
        var kind = (ChapterKind)value;
        Assert.Equal(ChapterAction.Scan, ChapterDecision.Decide(kind, null, 7, "s", false));
        Assert.Equal(ChapterAction.Scan, ChapterDecision.Decide(kind, Record(0, 99), 8, "s", false));
        Assert.Equal(ChapterAction.Scan, ChapterDecision.Decide(kind, Record(0, 99), 7, "t", false));
    }

    [Fact]
    public void TheFilesOwnChaptersAreNeverTouched()
    {
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.Other, null, 7, "s", false));
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.Other, Record(0, 99), 8, "s", false));
    }

    [Fact]
    public void OursInPlaceOrNothingFoundIsSkipped()
    {
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.Ours, Record(0, 99), 7, "s", false));
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.None, Record(), 7, "s", false));
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.Dummy, Record(), 7, "s", false));
    }

    [Theory]
    [InlineData((int)ChapterKind.None)]
    [InlineData((int)ChapterKind.Dummy)]
    public void OursReplacedByARefreshAreRestored(int kind) =>
        Assert.Equal(ChapterAction.Restore, ChapterDecision.Decide((ChapterKind)kind, Record(0, 99), 7, "s", false));

    [Fact]
    public void WithSegmentsOnAVideoWithItsOwnChaptersIsScannedOnce()
    {
        Assert.Equal(ChapterAction.Scan, ChapterDecision.Decide(ChapterKind.Other, null, 7, "s", true));
        Assert.Equal(ChapterAction.Scan, ChapterDecision.Decide(ChapterKind.Other, Record(), 8, "s", true));
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.Other, Record(), 7, "s", true));
    }

    [Fact]
    public void WithSegmentsOnTheChapterDecisionsStayTheSame()
    {
        Assert.Equal(ChapterAction.Skip, ChapterDecision.Decide(ChapterKind.Ours, Record(0, 99), 7, "s", true));
        Assert.Equal(ChapterAction.Restore, ChapterDecision.Decide(ChapterKind.Dummy, Record(0, 99), 7, "s", true));
        Assert.Equal(ChapterAction.Scan, ChapterDecision.Decide(ChapterKind.None, null, 7, "s", true));
    }

    [Fact]
    public void TheStateSurvivesAReload()
    {
        string path = Path.Combine(Path.GetTempPath(), $"shotchapters-{Guid.NewGuid():N}", "chapters.json");
        var id = Guid.NewGuid();
        var state = ChapterState.Load(path);
        state.Set(id, Record(0, 99) with { BlackSpans = [new BlackSpan(5, 10)] });
        state.Save();
        var loaded = ChapterState.Load(path).Get(id);
        Assert.NotNull(loaded);
        Assert.Equal([0L, 99], loaded.Chapters);
        Assert.Equal("s", loaded.Settings);
        Assert.Equal([new BlackSpan(5, 10)], loaded.BlackSpans);
        var reloaded = ChapterState.Load(path);
        Assert.True(reloaded.Remove(id));
        reloaded.Save();
        Assert.Null(ChapterState.Load(path).Get(id));
        File.WriteAllText(path, "{ not json");
        Assert.Null(ChapterState.Load(path).Get(id));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }
}
