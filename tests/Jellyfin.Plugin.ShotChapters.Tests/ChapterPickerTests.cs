namespace Jellyfin.Plugin.ShotChapters.Tests;

public class ChapterPickerTests
{
    static IReadOnlyList<double> Pick(double duration, double interval, double edge, params (double S, double V)[] cuts) =>
        ChapterPicker.Pick(cuts.Select(c => new Cut(c.S, c.V)), duration, interval, edge);

    [Fact]
    public void NoCutsGiveOnlyTheStart() =>
        Assert.Equal([0.0], Pick(3600, 300, 60));

    [Fact]
    public void TheStrongestCutNearEachTargetWins() =>
        Assert.Equal(
            [0.0, 310, 600],
            Pick(1000, 300, 60, (200, 50), (310, 90), (390, 10), (600, 40)));

    [Fact]
    public void ATieGoesToTheEarliest() =>
        Assert.Equal([0.0, 280], Pick(600, 300, 60, (280, 30), (320, 30)));

    [Fact]
    public void ChaptersKeepHalfAnIntervalApart() =>
        // 440 wins 300's window; 460, the strongest in 600's, is only 20 s after it, so 700 starts the next chapter.
        Assert.Equal([0.0, 440, 700], Pick(2000, 300, 60, (440, 99), (460, 50), (700, 5)));

    [Fact]
    public void NothingWithinTheEdgeOfTheEnd() =>
        Assert.Equal([0.0], Pick(330, 300, 60, (290, 99)));

    [Fact]
    public void AMultipleWithoutCutsGetsNoChapter() =>
        // Nothing in [150, 450); 600's window [450, 750) holds 700.
        Assert.Equal([0.0, 700], Pick(2000, 300, 60, (100, 99), (700, 5)));

    [Fact]
    public void OrderAndNonFiniteValuesDoNotMatter() =>
        Assert.Equal(
            [0.0, 300],
            Pick(1000, 300, 60, (310, double.NaN), (300, 10), (double.PositiveInfinity, 99), (250, 5)));

    [Fact]
    public void AFilmGetsAboutOneChapterAnInterval()
    {
        // A cut every 7 s for 2 hours, strengths cycling: about 24 chapters at 5 minutes, at most one per half interval.
        var cuts = Enumerable.Range(1, 7200 / 7).Select(i => new Cut(i * 7, i * 37 % 101)).ToList();
        var starts = ChapterPicker.Pick(cuts, 7200, 300, 60);
        Assert.InRange(starts.Count, 20, 28);
        Assert.All(starts.Zip(starts.Skip(1)), p => Assert.True(p.Second - p.First >= 150));
        Assert.True(starts[^1] <= 7200 - 60);
        Assert.Equal(starts, ChapterPicker.Pick(cuts, 7200, 300, 60));
    }
}
