namespace Blyant.Tests;

/// <summary>
/// The hints of the start page: the next one each time Blyant starts (saved with the settings), back to the first
/// after the last, and nothing changed or written while they are hidden
/// </summary>
[Collection("Static state")]
public class HintsTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), "blyant-hints-" + Guid.NewGuid().ToString("N"));
    private readonly bool _savedEnabled = Hints.Enabled;
    private readonly int _savedIndex = Hints.Index;

    public HintsTests()
    {
        FileIo.DataDirectoryOverride = _data;
    }

    public void Dispose()
    {
        FileIo.DataDirectoryOverride = null;
        Hints.Enabled = _savedEnabled;
        Hints.Index = _savedIndex;
        if (Directory.Exists(_data)) Directory.Delete(_data, true);
    }

    private string SettingsFile => Path.Combine(_data, FileIo.SettingsFileName);

    [Fact]
    public void ThereAreTwelveHints_AndTheFirstIsShownBeforeBlyantHasStarted()
    {
        Assert.Equal(12, Hints.All().Length);
        Assert.All(Hints.All(), hint => Assert.True(hint.Length > 40));
        Hints.Index = -1;
        Assert.Equal(1, Hints.Current().Number);
        Hints.Index = 4;
        Assert.Equal((Hints.All()[4], 5), Hints.Current());
    }

    [Fact]
    public void EachStart_ShowsTheNextHint_AndSavesWhichOne()
    {
        Hints.Enabled = true;
        Hints.Index = -1;
        Hints.Advance();
        Assert.Equal(0, Hints.Index);
        Assert.Equal("0", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["hintindex"]);
        Hints.Advance();
        Assert.Equal(2, Hints.Current().Number);
        // After the last, the first again
        Hints.Index = Hints.All().Length - 1;
        Hints.Advance();
        Assert.Equal(1, Hints.Current().Number);
    }

    [Fact]
    public void WhileHidden_TheHintDoesNotChange_AndNothingIsWritten()
    {
        Hints.Enabled = false;
        Hints.Index = 3;
        Hints.Advance();
        Assert.Equal(3, Hints.Index);
        Assert.False(File.Exists(SettingsFile));
    }
}
