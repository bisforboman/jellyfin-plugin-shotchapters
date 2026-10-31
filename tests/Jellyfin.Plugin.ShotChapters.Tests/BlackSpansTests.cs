namespace Jellyfin.Plugin.ShotChapters.Tests;

public class BlackSpansTests
{
    // 10 frames a second.
    static IReadOnlyList<(double Start, double End)> Find(double minSeconds, params double?[] brightness) =>
        BlackSpans.Find(brightness, i => i / 10.0, brightness.Length / 10.0, 12, minSeconds);

    [Fact]
    public void NoDarkFramesNoSpans() =>
        Assert.Empty(Find(0, 50, 60, 12, 70));

    [Fact]
    public void ASpanRunsToTheFirstFrameAfterIt() =>
        Assert.Equal([(0.2, 0.5)], Find(0, 50, 60, 3, 0, 11.9, 40));

    [Fact]
    public void TheThresholdIsExclusive() =>
        Assert.Equal([(0.1, 0.2)], Find(0, 50, 11.99, 12, 50));

    [Fact]
    public void ASpanAtTheEndEndsWithTheVideo() =>
        Assert.Equal([(0.1, 0.3)], Find(0, 50, 1, 2));

    [Fact]
    public void ShortSpansAreLeftOut() =>
        Assert.Equal([(0.3, 0.6)], Find(0.3, 50, 1, 50, 1, 1, 1, 50));

    [Fact]
    public void AFrameWithoutAValueBreaksASpan() =>
        Assert.Equal([(0.0, 0.1), (0.2, 0.3)], Find(0, 1, null, 1, 50));
}
