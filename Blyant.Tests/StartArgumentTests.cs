namespace Blyant.Tests;

/// <summary>
/// Starting Blyant with a file or directory ("blyant notes.md"), and the data directory it finds wherever it is
/// started from
/// </summary>
[Collection("Static state")]
public class StartArgumentTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _root;

    public StartArgumentTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "blyant-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        File.WriteAllText(Path.Combine(_root, "notes", "a.md"), "x");
        // As the current directory tells it (on macOS the temporary directory is reached by a link)
        _root = FileOperations.RealPath(_root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private StartArgument.Result Resolve(string argument) => StartArgument.Resolve(argument, _root);

    [Fact]
    public void AFile_IsOpened_AndARelativePathIsFromTheStartDirectory()
    {
        var result = Resolve(Path.Combine("notes", "a.md"));
        Assert.Equal(new JumpTarget(JumpKind.File, Path.Combine(_root, "notes", "a.md")), result.Target);
        Assert.False(result.AskToCreate);
        Assert.Equal(string.Empty, result.Message);
        // A full path, also in quotes
        Assert.Equal(JumpKind.File, Resolve("\"" + Path.Combine(_root, "notes", "a.md") + "\"").Target!.Kind);
    }

    [Fact]
    public void ADirectory_IsShownInTheBrowser()
    {
        Assert.Equal(new JumpTarget(JumpKind.Directory, Path.Combine(_root, "notes")), Resolve("notes").Target);
        Assert.Equal(JumpKind.Directory, Resolve(".").Target!.Kind);
    }

    [Fact]
    public void AFileThatIsNotThere_CanBeMade_InADirectoryThatIs()
    {
        var result = Resolve(Path.Combine("notes", "new.md"));
        Assert.True(result.AskToCreate);
        Assert.Equal(Path.Combine(_root, "notes", "new.md"), result.Path);
        // In a directory that is not there: not found
        var missing = Resolve(Path.Combine("nowhere", "new.md"));
        Assert.Null(missing.Target);
        Assert.Contains("nowhere", missing.Message);
    }

    [Fact]
    public void StartedInADirectoryWithADataDirectory_ThatOneIsUsed_WhenItIsBlyants()
    {
        // A folder "data" of something else (a web site) is not used
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        Directory.SetCurrentDirectory(_root);
        Assert.NotEqual(Path.Combine(_root, "data"), Path.GetFullPath(FileIo.GetDataDirectoryName()));
        // One with Blyant's settings is (chosen again in another directory, as when Blyant is started there)
        Directory.CreateDirectory(Path.Combine(_root, "notes", "data"));
        File.WriteAllText(Path.Combine(_root, "notes", "data", FileIo.SettingsFileName), "[SETTINGS]");
        Directory.SetCurrentDirectory(Path.Combine(_root, "notes"));
        Assert.Equal(Path.Combine(_root, "notes", "data"), Path.GetFullPath(FileIo.GetDataDirectoryName()));
    }

    [Fact]
    public void WithoutADataDirectoryWhereItIsStarted_TheOneNextToTheProgramIsUsed()
    {
        Directory.SetCurrentDirectory(Path.Combine(_root, "notes"));
        var data = Path.GetFullPath(FileIo.GetDataDirectoryName());
        Assert.NotEqual(Path.Combine(_root, "notes", "data"), data);
        Assert.Equal("data", Path.GetFileName(data));
        // The spell files are next to it
        Assert.Equal(Path.Combine(Path.GetDirectoryName(data)!, "spell"), SpellChecker.DirectoryName);
    }
}
