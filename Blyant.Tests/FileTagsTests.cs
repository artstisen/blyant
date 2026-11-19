namespace Blyant.Tests;

/// <summary>
/// The tags of files (tags.txt): given, taken off, renamed, and following files that are renamed, moved, copied and
/// deleted. Only the list: the commands of the shell are in ShellTests
/// </summary>
[Collection("Static state")]
public class FileTagsTests : IDisposable
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "blyant-tags"));

    private static string At(params string[] parts) => Path.GetFullPath(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    public FileTagsTests() => FileTags.Clear();

    public void Dispose() => FileTags.Clear();

    [Fact]
    public void Tags_AreNamesOfLettersDigitsDashAndUnderscore()
    {
        Assert.True(FileTags.IsName("draft"));
        Assert.True(FileTags.IsName("chapter-3_v2"));
        Assert.True(FileTags.IsName("kapitel-ü"));
        Assert.False(FileTags.IsName(""));
        Assert.False(FileTags.IsName("two words"));
        Assert.False(FileTags.IsName("a/b"));
        Assert.False(FileTags.IsName("$tag"));
        Assert.False(FileTags.IsName(new string('a', FileTags.MaxNameLength + 1)));
    }

    [Fact]
    public void AFile_HasSeveralTags_TheOneGivenLastAtTheEnd_InSmallLetters()
    {
        Assert.True(FileTags.Add(At("a.md"), "Draft"));
        Assert.True(FileTags.Add(At("a.md"), "ferry"));
        Assert.Equal(new[] { "draft", "ferry" }, FileTags.TagsOf(At("a.md")));
        // Given again: the last
        Assert.True(FileTags.Add(At("a.md"), "draft"));
        Assert.False(FileTags.Add(At("a.md"), "DRAFT"));
        Assert.Equal(new[] { "ferry", "draft" }, FileTags.TagsOf(At("a.md")));
        FileTags.Add(At("b.md"), "draft");
        Assert.Equal(new[] { At("a.md"), At("b.md") }, FileTags.FilesWith("Draft"));
        Assert.Equal(new[] { ("draft", 2), ("ferry", 1) }, FileTags.All());
        Assert.Empty(FileTags.TagsOf(At("c.md")));
    }

    [Fact]
    public void Remove_TakesOneTagOff_OrAll_AndRenameAndClear_WorkOnAllFiles()
    {
        FileTags.Add(At("a.md"), "draft");
        FileTags.Add(At("a.md"), "ferry");
        FileTags.Add(At("b.md"), "draft");
        FileTags.Add(At("b.md"), "final");
        Assert.True(FileTags.Remove(At("a.md"), "ferry"));
        Assert.False(FileTags.Remove(At("a.md"), "ferry"));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(At("a.md")));
        // Renamed to a tag a file has already: it keeps one
        Assert.Equal(2, FileTags.Rename("draft", "final"));
        Assert.Equal(new[] { "final" }, FileTags.TagsOf(At("a.md")));
        Assert.Equal(new[] { "final" }, FileTags.TagsOf(At("b.md")));
        Assert.True(FileTags.Remove(At("b.md"), null));
        Assert.Empty(FileTags.TagsOf(At("b.md")));
        Assert.Equal(1, FileTags.ClearTag("final"));
        Assert.Empty(FileTags.Tagged());
        FileTags.Add(At("a.md"), "x");
        FileTags.Add(At("b.md"), "y");
        Assert.Equal(2, FileTags.ClearTag(null));
        Assert.Empty(FileTags.All());
    }

    [Fact]
    public void TheFile_HasALineForEachFile_AndLinesThatAreNotTagsAreLeftOut()
    {
        FileTags.Add(At("a.md"), "draft");
        FileTags.Add(At("a.md"), "ferry");
        var text = FileTags.Serialize();
        Assert.Equal(At("a.md") + "\tdraft ferry\n", text);
        FileTags.Parse(text + "no tab here\nrelative.md\tdraft\n" + At("b.md") + "\tok bad/name\n\n");
        Assert.Equal(new[] { "draft", "ferry" }, FileTags.TagsOf(At("a.md")));
        Assert.Equal(new[] { "ok" }, FileTags.TagsOf(At("b.md")));
        Assert.Equal(2, FileTags.Tagged().Count);
    }

    [Fact]
    public void Tags_FollowAFileOrADirectory_Renamed_Copied_AndDeleted()
    {
        FileTags.Add(At("a.md"), "draft");
        FileTags.Add(At("book", "ch1.md"), "draft");
        FileTags.Add(At("book", "part", "ch2.md"), "ferry");
        // Renamed: the file, and the files in a directory
        Assert.True(FileTags.PathChanged(At("a.md"), At("b.md")));
        Assert.True(FileTags.PathChanged(At("book"), At("novel")));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(At("b.md")));
        Assert.Equal(new[] { "ferry" }, FileTags.TagsOf(At("novel", "part", "ch2.md")));
        Assert.Empty(FileTags.TagsOf(At("book", "ch1.md")));
        Assert.False(FileTags.PathChanged(At("other.md"), At("x.md")));
        // A name that only starts as the directory's is not in it
        FileTags.Add(At("novel-notes.md"), "x");
        FileTags.PathChanged(At("novel"), At("book"));
        Assert.Equal(new[] { "x" }, FileTags.TagsOf(At("novel-notes.md")));
        // Copied: the copy has the tags too
        Assert.True(FileTags.Copied(At("b.md"), At("copy", "b.md")));
        Assert.True(FileTags.Copied(At("book"), At("backup", "book")));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(At("copy", "b.md")));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(At("b.md")));
        Assert.Equal(new[] { "ferry" }, FileTags.TagsOf(At("backup", "book", "part", "ch2.md")));
        Assert.False(FileTags.Copied(At("none.md"), At("x.md")));
        // Deleted: the file, and the files in a directory
        Assert.True(FileTags.PathsDeleted(new[] { At("b.md"), At("backup") }));
        Assert.Empty(FileTags.TagsOf(At("b.md")));
        Assert.Empty(FileTags.TagsOf(At("backup", "book", "part", "ch2.md")));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(At("copy", "b.md")));
        Assert.False(FileTags.PathsDeleted(new[] { At("gone.md") }));
    }
}
