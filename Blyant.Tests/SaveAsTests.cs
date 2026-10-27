namespace Blyant.Tests;

/// <summary>
/// ":w name" and ":saveas name" in the editor: where the new file goes, and that it keeps the encoding and line
/// endings of the file it is made from
/// </summary>
public class SaveAsTests
{
    private static readonly string Directory = Path.Combine(Path.GetTempPath(), "blyant-saveas");

    [Fact]
    public void TheNewFile_IsNextToTheFile_WithItsExtension_WhenTheNameHasNone()
    {
        var file = Path.Combine(Directory, "notes.txt");
        Assert.Equal(Path.Combine(Directory, "draft.txt"), FileExplorer.PathNextToCurrent(file, "draft"));
        Assert.Equal(Path.Combine(Directory, "draft.md"), FileExplorer.PathNextToCurrent(file, " draft.md "));
        Assert.Equal(Path.Combine(Directory, "v2.tar.gz"), FileExplorer.PathNextToCurrent(file, "v2.tar.gz"));
    }

    [Fact]
    public void ANameWithADirectoryInIt_IsNotAName()
    {
        Assert.NotNull(Tools.FileNameProblem("sub/draft.md"));
        Assert.NotNull(Tools.FileNameProblem("..\\draft.md"));
        Assert.Null(Tools.FileNameProblem("draft.md"));
    }

    [Fact]
    public void TheCopy_HasTheLineEndingsAndByteOrderMarkOfTheFile()
    {
        var file = TextFile.Read(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', 13, 10, (byte)'b' });
        file.Path = "/x/notes.md";
        var copy = file.WithPath("/x/notes-v2.md");
        Assert.Equal("/x/notes-v2.md", copy.Path);
        Assert.Equal("/x/notes.md", file.Path);
        Assert.Equal(file.LineEndingLabel, copy.LineEndingLabel);
        Assert.Equal(file.EncodingLabel, copy.EncodingLabel);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', 13, 10, (byte)'c' }, copy.GetBytes("a\nc"));
    }
}
