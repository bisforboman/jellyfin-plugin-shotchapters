namespace Jellyfin.Plugin.ShotChapters.Tests;

/// <summary>
/// The guess on the last cuts of the films it was measured on (2026-11-08, issue #10): ShotDetector's adaptive cuts at
/// the plugin's settings, with the true credits from looking at the frames, and each tail stretch's share of steady or
/// still windows (<see cref="CreditsGuess.CreditsShare"/> on the scan's content_val).
/// </summary>
public class CreditsGuessTests
{
    /// <summary>The measured shares, by stretch start; a stretch not listed fails the test.</summary>
    static Func<double, double, double> Shares(params (double Start, double Share)[] shares) =>
        (start, _) => shares.Single(s => s.Start == start).Share;

    [Fact]
    public void BigBuckBunnysScrollingCreditsAreFound()
    {
        // Credits from 8:13 (after a fade from black) to a scene after them at 10:19; they scroll over moving pictures.
        var guess = CreditsGuess.Guess([477.233, 484.567, 486.033, 488.2, 490.667, 494.267, 618.7, 634.2], 634.533, Shares((494.267, 0.533)));
        Assert.Equal((494.267, 618.7), guess);
    }

    [Fact]
    public void SintelsCreditsAreFound()
    {
        // Credits from 12:24, to the end (a licence card): cards on black, then scrolling.
        var guess = CreditsGuess.Guess([687.958, 700.292, 710.042, 744.083], 888, Shares((744.083, 1.0)));
        Assert.Equal((744.083, 888.0), guess);
    }

    [Fact]
    public void ShorterScrollingCreditsAreFound()
    {
        // Tears of Steel: credits 588 to 706, cut by its title sequence; the scroll after it is 89 s.
        Assert.Equal((620.5, 709.5), CreditsGuess.Guess([556.833, 564.292, 588, 616.875, 620.5, 709.5, 730.333], 734.167, Shares((620.5, 0.773))));
        // Night of the Living Dead: credits over stills from 5652; 82 s without a cut.
        Assert.Equal((5665.263, 5746.789), CreditsGuess.Guess([5638.824, 5644.454, 5664.262, 5665.263, 5746.789], 5752.622, Shares((5665.263, 0.65))));
        // The Little Shop of Horrors: credits over a drawing from 4290 to the end.
        Assert.Equal((4280.4, 4350.5), CreditsGuess.Guess([4268.682, 4274.687, 4276.772, 4280.4], 4350.5, Shares((4280.4, 0.706))));
    }

    [Fact]
    public void AFinalLongTakeIsNotCredits()
    {
        // His Girl Friday: its last scene is one 104 s take (5366 to 5470), then The End and the cast.
        Assert.Null(CreditsGuess.Guess([5329.53, 5332.983, 5352.669, 5366.266, 5470.47, 5471.471], 5504.516, Shares((5366.266, 0))));
        // Detour: a 65 s last shot, then The End.
        Assert.Null(CreditsGuess.Guess([3890.791, 3973.507, 3984.852, 3992.659], 4058.321, Shares((3992.659, 0))));
    }

    [Fact]
    public void ALongTakeBeforeTheEndIsNotCredits()
    {
        // His Girl Friday's 130 s take at 5083 ends 290 s before the end: never measured.
        Assert.Null(CreditsGuess.Guess(
            [5081.0, 5083.0, 5213.413, 5229.229, 5229.83, 5329.53, 5332.983, 5352.669, 5366.266, 5470.47, 5471.471], 5504.516,
            Shares((5366.266, 0))));
    }

    [Fact]
    public void ShortCreditsAreMissed()
    {
        // Caminandes 2: 12 s of credits over the picture, between cuts at 114.667, 118.75 and 129.25.
        Assert.Null(CreditsGuess.Guess([76.875, 87.375, 92.208, 99.333, 110.542, 114.667, 118.75, 129.25, 130.25, 141.167], 146, Shares()));
    }

    [Fact]
    public void TheShareHasToReachTheMinimum()
    {
        Assert.Null(CreditsGuess.Guess([500, 620], 640, Shares((500, CreditsGuess.MinCreditsShare - 0.01))));
        Assert.Equal((500.0, 620.0), CreditsGuess.Guess([500, 620], 640, Shares((500, CreditsGuess.MinCreditsShare))));
    }

    [Fact]
    public void AVideoWithNoCutsHasNoGuessWhenShort() =>
        Assert.Null(CreditsGuess.Guess([], 50, Shares()));

    [Fact]
    public void CutsInAnyOrderGiveTheSameGuess() =>
        Assert.Equal(
            CreditsGuess.Guess([494.267, 618.7, 634.2, 490.667], 634.533, Shares((494.267, 0.5))),
            CreditsGuess.Guess([634.2, 490.667, 618.7, 494.267], 634.533, Shares((494.267, 0.5))));

    /// <summary>content_val for <paramref name="seconds"/> at 24 fps, from each frame's index; NaN for the first frame.</summary>
    static float[] Frames(double seconds, Func<int, float> value) =>
        Enumerable.Range(0, (int)(seconds * 24)).Select(i => i == 0 ? float.NaN : value(i)).ToArray();

    [Fact]
    public void ScrollingTextIsSteady()
    {
        // A steady scroll, with a repeated frame (content_val near 0) and its double after it every 5 frames.
        var frames = Frames(60, i => (i % 5) switch { 0 => 0.02f, 1 => 9.5f, _ => 4.6f + 0.1f * (i % 3) });
        Assert.Equal(1, CreditsGuess.CreditsShare(frames, 24, 0, 60));
    }

    [Fact]
    public void CardsAreStill()
    {
        // Text cards: still, with a fade between them every 8 s.
        var frames = Frames(64, i => i % 192 < 6 ? 12f : 0.04f);
        Assert.Equal(1, CreditsGuess.CreditsShare(frames, 24, 0, 64));
    }

    [Fact]
    public void ALongTakeIsNeither()
    {
        // Actors moving: content_val wanders from frame to frame, sometimes nearly still.
        var random = new Random(7);
        var frames = Frames(100, _ => (float)(0.2 + 3 * random.NextDouble() * random.NextDouble()));
        Assert.True(CreditsGuess.CreditsShare(frames, 24, 0, 100) < 0.1);
    }

    [Fact]
    public void OnlyTheStretchCounts()
    {
        // Steady from 40 s on, wandering before.
        var random = new Random(7);
        var frames = Frames(100, i => i >= 40 * 24 ? 5f : (float)(0.2 + 3 * random.NextDouble() * random.NextDouble()));
        Assert.Equal(1, CreditsGuess.CreditsShare(frames, 24, 40, 100));
        Assert.True(CreditsGuess.CreditsShare(frames, 24, 0, 40) < 0.2);
    }

    [Fact]
    public void NoWholeWindowGivesNothing()
    {
        var frames = Frames(10, _ => 5f);
        Assert.Equal(0, CreditsGuess.CreditsShare(frames, 24, 0, 4.5));
        Assert.Equal(0, CreditsGuess.CreditsShare(frames, 0, 0, 10));
        Assert.Equal(0, CreditsGuess.CreditsShare([], 24, 0, 60));
        // Past the frames there are: only the frames held count.
        Assert.Equal(1, CreditsGuess.CreditsShare(frames, 24, 0, 60));
    }
}
