namespace Blyant.Tests;

/// <summary>
/// Files that must not be written over or lost: a recovery copy put off for later, the list of added projects
/// that could not be read, Blyant's own files as the paste bin, and the data directory, which is chosen once
/// </summary>
[Collection("Static state")]
public class SafetyFixTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "blyant_safety_" + Guid.NewGuid().ToString("N"));
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public SafetyFixTests()
    {
        Directory.CreateDirectory(_root);
        Recovery.DirectoryOverride = Path.Combine(_root, "data", Recovery.DirectoryName);
        Projects.ListPathOverride = Path.Combine(_root, "data", Projects.ListFileName);
    }

    public void Dispose()
    {
        Recovery.DirectoryOverride = null;
        Projects.ListPathOverride = null;
        FileIo.DataDirectoryOverride = null;
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_root, true);
    }

    private string In(params string[] parts) => Path.Combine(new[] { _root }.Concat(parts).ToArray());

    [Fact]
    public void ACopyBesideOneThatIsKept_GetsANumber_AndTheNewestIsFound()
    {
        var file = In("chapter.md");
        var kept = Recovery.CopyPathOf(file);
        Directory.CreateDirectory(Recovery.Directory);
        File.WriteAllText(kept, Recovery.Serialize(file, "put off", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        // The copy that is there is never the one written: the next is beside it
        var beside = Recovery.FreeCopyPath(kept);
        Assert.NotEqual(kept, beside);
        Assert.EndsWith(" 2.txt", beside);
        File.WriteAllText(beside, Recovery.Serialize(file, "newer", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        Assert.EndsWith(" 3.txt", Recovery.FreeCopyPath(kept));
        // Both are kept; the newest is offered when the file is opened
        var found = Recovery.Find(file);
        Assert.NotNull(found);
        Assert.Equal("newer", found.Text);
        Assert.Equal(beside, found.CopyPath);
        Assert.Equal(2, Recovery.All().Count);
    }

    // (a file held open without sharing cannot be read on Windows; elsewhere the lock is only advisory)
    [WindowsFact]
    public void TheListOfProjects_IsNotWritten_WhenItCouldNotBeRead()
    {
        var first = Directory.CreateDirectory(In("Novel")).FullName;
        var second = Directory.CreateDirectory(In("Essay")).FullName;
        Assert.Equal(DialogText.Success, Projects.Add(first));
        var before = File.ReadAllText(Projects.ListPath);
        using (new FileStream(Projects.ListPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Held by another program (antivirus, sync): adding is refused, and the list is left as it is
            Assert.Equal(DialogText.ListNotRead, Projects.Add(second));
        }

        Assert.Equal(before, File.ReadAllText(Projects.ListPath));
        Assert.Equal(DialogText.Success, Projects.Add(second));
        Assert.Equal(2, Projects.AddedPaths().Count);
    }

    [Fact]
    public void BlyantsOwnFiles_AndDirectories_CannotBeThePasteBin()
    {
        var data = Directory.CreateDirectory(In("data")).FullName;
        FileIo.DataDirectoryOverride = data;
        File.WriteAllText(Path.Combine(data, FileIo.SettingsFileName), "[SETTINGS]");
        File.WriteAllText(Path.Combine(data, "bookmarks.txt"), "x");
        Assert.NotNull(SystemClipboard.PasteBinProblem(FileIo.SettingsFileName));
        Assert.NotNull(SystemClipboard.PasteBinProblem(Path.Combine(data, "bookmarks.txt")));
        Assert.NotNull(SystemClipboard.PasteBinProblem(data));
        Assert.NotNull(SystemClipboard.PasteBinProblem(_root));
        // The paste bin of Blyant, a new file in the data directory, and a file elsewhere can be used
        File.WriteAllText(Path.Combine(data, SystemClipboard.DefaultPasteBinFile), "pasted");
        Assert.Null(SystemClipboard.PasteBinProblem(SystemClipboard.DefaultPasteBinFile));
        Assert.Null(SystemClipboard.PasteBinProblem("clips.txt"));
        Assert.Null(SystemClipboard.PasteBinProblem(In("notes", "clips.txt")));
    }

    [Fact]
    public void TheDataDirectory_IsChosenOnce_AndADataFolderMadeLaterDoesNotTakeItsPlace()
    {
        var work = Directory.CreateDirectory(In("work")).FullName;
        Directory.SetCurrentDirectory(work);
        var chosen = FileIo.GetDataDirectoryName();
        Assert.NotEqual("data", chosen);
        // A folder "data" made in the file browser, in the directory Blyant was started in
        Directory.CreateDirectory(Path.Combine(work, "data"));
        Assert.Equal(chosen, FileIo.GetDataDirectoryName());
    }
}
