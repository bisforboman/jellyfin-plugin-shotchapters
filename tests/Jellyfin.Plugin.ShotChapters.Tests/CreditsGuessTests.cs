namespace Jellyfin.Plugin.ShotChapters.Tests;

/// <summary>
/// The guess on the last cuts of the films it was measured on (2026-11-08, issue #10): ShotDetector's adaptive cuts at
/// the plugin's settings, with the true credits from looking at the frames.
/// </summary>
public class CreditsGuessTests
{
    [Fact]
    public void BigBuckBunnysScrollingCreditsAreFound()
    {
        // Credits from 8:13 (after a fade from black) to a scene after them at 10:19.
        var guess = CreditsGuess.Guess([477.233, 484.567, 486.033, 488.2, 490.667, 494.267, 618.7, 634.2], 634.533);
        Assert.Equal((494.267, 618.7), guess);
    }

    [Fact]
    public void SintelsCreditsAreFound()
    {
        // Credits from 12:24, to the end (a licence card).
        var guess = CreditsGuess.Guess([687.958, 700.292, 710.042, 744.083], 888);
        Assert.Equal((744.083, 888.0), guess);
    }

    [Fact]
    public void ShorterCreditsAreMissed()
    {
        // Tears of Steel: credits 588 to 706, cut by its title sequence; the longest stretch is 89 s.
        Assert.Null(CreditsGuess.Guess([556.833, 564.292, 588, 616.875, 620.5, 709.5, 730.333], 734.167));
        // Night of the Living Dead: credits over stills from 5652; 82 s without a cut.
        Assert.Null(CreditsGuess.Guess([5638.824, 5644.454, 5664.262, 5665.263, 5746.789], 5752.622));
    }

    [Fact]
    public void AFinalLongTakeIsNotCredits()
    {
        // His Girl Friday: its last scene is one 104 s take (5366 to 5470), then The End and the cast.
        Assert.Null(CreditsGuess.Guess([5329.53, 5332.983, 5352.669, 5366.266, 5470.47, 5471.471], 5504.516));
        // Detour: a 65 s last shot, then The End.
        Assert.Null(CreditsGuess.Guess([3890.791, 3973.507, 3984.852, 3992.659], 4058.321));
    }

    [Fact]
    public void ALongTakeBeforeTheEndIsNotCredits()
    {
        // His Girl Friday's 130 s take at 5083 ends 290 s before the end.
        Assert.Null(CreditsGuess.Guess(
            [5081.0, 5083.0, 5213.413, 5229.229, 5229.83, 5329.53, 5332.983, 5352.669, 5366.266, 5470.47, 5471.471], 5504.516));
    }

    [Fact]
    public void AVideoWithNoCutsHasNoGuessWhenShort() =>
        Assert.Null(CreditsGuess.Guess([], 100));

    [Fact]
    public void CutsInAnyOrderGiveTheSameGuess() =>
        Assert.Equal(
            CreditsGuess.Guess([494.267, 618.7, 634.2, 490.667], 634.533),
            CreditsGuess.Guess([634.2, 490.667, 618.7, 494.267], 634.533));
}
