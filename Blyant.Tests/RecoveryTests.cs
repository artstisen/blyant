namespace Blyant.Tests;

[Collection("Static state")]
public class RecoveryTests : IDisposable
{
    private readonly string _root;

    public RecoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "blyant_recovery_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Recovery.DirectoryOverride = Path.Combine(_root, "data", Recovery.DirectoryName);
    }

    public void Dispose()
    {
        Recovery.DirectoryOverride = null;
        Directory.Delete(_root, true);
    }

    private string In(params string[] parts) => Path.Combine(new[] { _root }.Concat(parts).ToArray());

    [Fact]
    public void TheCopy_IsNamedAfterTheFile_WithACodeThatIsTheSameEveryTime()
    {
        var path = In("projects", "chapter 1.md");
        var copy = Recovery.CopyPathOf(path);
        Assert.Equal(Recovery.Directory, Path.GetDirectoryName(copy));
        Assert.Matches(@"^chapter 1\.md\.[0-9a-f]{8}\.txt$", Path.GetFileName(copy));
        Assert.Equal(copy, Recovery.CopyPathOf(path));
        // Another file of the same name elsewhere: another code
        Assert.NotEqual(copy, Recovery.CopyPathOf(In("other", "chapter 1.md")));
        // Where names are the same in either case, so is the copy
        Assert.Equal(FileOperations.NamesIgnoreCase,
            copy.Equals(Recovery.CopyPathOf(In("PROJECTS", "chapter 1.md")), StringComparison.Ordinal));
    }

    [Fact]
    public void TheCopy_IsWrittenFoundAndDeleted()
    {
        var path = In("projects", "chapter 1.md");
        Assert.Null(Recovery.Find(path));
        Assert.Equal(DialogText.Success, Recovery.Write(path, "First line\r\nsecond --- line\n---\nlast"));
        var copy = Recovery.Find(path);
        Assert.NotNull(copy);
        Assert.Equal(Path.GetFullPath(path), copy!.FilePath);
        // The text with LF line endings, also with lines of "---" in it
        Assert.Equal("First line\nsecond --- line\n---\nlast", copy.Text);
        Assert.Equal(Environment.ProcessId, copy.ProcessId);
        Assert.True((DateTime.UtcNow - copy.Written).TotalMinutes < 1);
        // Written again: replaced
        Assert.Equal(DialogText.Success, Recovery.Write(path, "new"));
        Assert.Equal("new", Recovery.Find(path)!.Text);
        Assert.Single(Directory.GetFiles(Recovery.Directory));
        Recovery.Delete(path);
        Assert.Null(Recovery.Find(path));
        Assert.Empty(Directory.GetFiles(Recovery.Directory));
    }

    [Fact]
    public void ACopy_OfAnotherFile_OrNotACopy_IsNotFound()
    {
        var path = In("projects", "a.md");
        Directory.CreateDirectory(Recovery.Directory);
        File.WriteAllText(Recovery.CopyPathOf(path), "just some text");
        Assert.Null(Recovery.Find(path));
        File.WriteAllText(Recovery.CopyPathOf(path), Recovery.Serialize(In("projects", "b.md"), "text", DateTime.UtcNow));
        Assert.Null(Recovery.Find(path));
    }

    [Fact]
    public void Copies_AreKnownByTheirDirectory()
    {
        Assert.True(Recovery.IsCopy(Recovery.CopyPathOf(In("a.md"))));
        Assert.True(Recovery.IsCopy(Recovery.Directory));
        Assert.False(Recovery.IsCopy(In("a.md")));
    }

    [Fact]
    public void ACopy_OfThisBlyant_OrOfOneThatIsNotRunning_IsNotInUse()
    {
        var path = In("a.md");
        Recovery.Write(path, "text");
        var copy = Recovery.Find(path)!;
        Assert.False(Recovery.WrittenByOtherRunning(copy));
        Assert.False(Recovery.WrittenByOtherRunning(copy with { ProcessId = int.MaxValue }));
        // A process that is running, but started at another time than the one that wrote it
        var other = System.Diagnostics.Process.GetProcesses()
            .FirstOrDefault(process => process.Id != Environment.ProcessId && process.Id > 4);
        if (other != null)
        {
            Assert.False(Recovery.WrittenByOtherRunning(copy with
            {
                ProcessId = other.Id, ProcessStarted = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }));
        }
    }

    [Fact]
    public void ABlankPage_HasACopyOfItsOwn_ListedWithTheCopiesOfFiles()
    {
        var untitled = Recovery.NewUntitledCopyPath();
        Assert.Equal(Recovery.Directory, Path.GetDirectoryName(untitled));
        Assert.Matches(@"^untitled-\d{4}-\d\d-\d\d \d\d\.\d\d\.\d\d\.txt$", Path.GetFileName(untitled));
        Assert.Equal(DialogText.Success, Recovery.WriteCopy(untitled, string.Empty, "an idea"));
        // Another blank page opened in the same second: a name of its own
        Assert.NotEqual(untitled, Recovery.NewUntitledCopyPath());
        Assert.Equal(DialogText.Success, Recovery.Write(In("a.md"), "file text"));
        var all = Recovery.All();
        Assert.Equal(2, all.Count);
        var page = all.Single(copy => copy.IsUntitled);
        Assert.Equal("an idea", page.Text);
        Assert.Equal(untitled, page.CopyPath);
        Assert.False(all.Single(copy => copy.FilePath.Length > 0).IsUntitled);
        // Not a copy: left out
        File.WriteAllText(Path.Combine(Recovery.Directory, "notes.txt"), "just text");
        Assert.Equal(2, Recovery.All().Count);
        Recovery.DeleteCopy(untitled);
        Assert.Single(Recovery.All());
    }

    [Fact]
    public void TheCopy_HasLinesThatTellWhatItIs()
    {
        var text = Recovery.Serialize(In("a.md"), "text", new DateTime(2026, 10, 22, 12, 3, 11, DateTimeKind.Utc));
        Assert.StartsWith("# Blyant: a recovery copy", text);
        Assert.Contains("\nfile=" + In("a.md") + "\n", text);
        Assert.Contains("\nwritten=2026-10-22T12:03:11", text);
        Assert.EndsWith("\n---\ntext", text);
        var copy = Recovery.Parse(text, "x")!;
        Assert.Equal(new DateTime(2026, 10, 22, 12, 3, 11, DateTimeKind.Utc), copy.Written);
    }
}
