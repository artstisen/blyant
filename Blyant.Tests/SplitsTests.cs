namespace Blyant.Tests;

[Collection("Static state")]
public class SplitsTests : IDisposable
{
    private readonly string _root;
    private readonly string? _savedPath = Splits.FilePathOverride;

    public SplitsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "blyant_splits_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Splits.FilePathOverride = Path.Combine(_root, Splits.FileName);
        Splits.Parse(string.Empty);
    }

    public void Dispose()
    {
        Splits.FilePathOverride = _savedPath;
        Splits.Parse(string.Empty);
        Directory.Delete(_root, true);
    }

    private string In(string name) => Path.Combine(_root, name);

    [Fact]
    public void AFileClosedWithAnotherBesideIt_IsKept_AndFoundAgain()
    {
        Assert.True(Splits.Set(In("chapter.md"), In("outline.md"), 14, 3));
        Assert.Equal(new Splits.Entry(In("chapter.md"), In("outline.md"), 14, 3), Splits.Get(In("chapter.md")));
        Assert.Null(Splits.Get(In("outline.md")));
        // Kept as it was: nothing changed
        Assert.False(Splits.Set(In("chapter.md"), In("outline.md"), 14, 3));
        // Another file beside it: one for each file, the newest first
        Splits.Set(In("notes.md"), In("ideas.md"), 1, 1);
        Assert.True(Splits.Set(In("chapter.md"), In("research.md"), 2, 1));
        Assert.Equal(new[] { "chapter.md", "notes.md" }, Splits.Items.Select(entry => Path.GetFileName(entry.Left)));
        Assert.Equal(In("research.md"), Splits.Get(In("chapter.md"))!.Right);
        // Closed without one: taken out
        Assert.True(Splits.Remove(In("chapter.md")));
        Assert.False(Splits.Remove(In("chapter.md")));
        Assert.Single(Splits.Items);
    }

    [Fact]
    public void TheSplits_AreWrittenAndReadAgain()
    {
        Splits.Set(In("a.md"), In("b.md"), 12, 5);
        Splits.Set(In("c.md"), In("d.md"), 1, 1);
        Assert.Equal(DialogText.Success, Splits.Save());
        var text = File.ReadAllText(Splits.FilePath);
        Assert.Equal(In("c.md") + "\t" + In("d.md") + "\t1:1\n" + In("a.md") + "\t" + In("b.md") + "\t12:5\n", text);
        Splits.Parse(string.Empty);
        Splits.Load();
        Assert.Equal(2, Splits.Items.Count);
        Assert.Equal(new Splits.Entry(In("a.md"), In("b.md"), 12, 5), Splits.Get(In("a.md")));
        // A line without a place: at the start; a line without a second file: left out
        Splits.Parse(In("x.md") + "\t" + In("y.md") + "\n" + In("z.md") + "\n");
        Assert.Equal(new Splits.Entry(In("x.md"), In("y.md"), 1, 1), Assert.Single(Splits.Items));
    }

    [Fact]
    public void AtMostMaxCount_AreKept()
    {
        for (var i = 0; i < Splits.MaxCount + 5; i++) Splits.Set(In(i + ".md"), In("o.md"), 1, 1);
        Assert.Equal(Splits.MaxCount, Splits.Items.Count);
        Assert.Equal(In(Splits.MaxCount + 4 + ".md"), Splits.Items[0].Left);
    }

    [Fact]
    public void TheSplits_FollowFilesThatAreRenamedOrMoved()
    {
        Splits.Set(In(Path.Combine("book", "chapter.md")), In(Path.Combine("book", "outline.md")), 3, 1);
        Assert.True(Splits.PathChanged(In(Path.Combine("book", "outline.md")), In("plan.md")));
        Assert.Equal(In("plan.md"), Splits.Items[0].Right);
        // A directory: what is in it follows
        Assert.True(Splits.PathChanged(In("book"), In("novel")));
        Assert.Equal(In(Path.Combine("novel", "chapter.md")), Splits.Items[0].Left);
        Assert.False(Splits.PathChanged(In("other.md"), In("else.md")));
    }

    [Fact]
    public void SplitsInTheEditor_AsksForTheList()
    {
        var editor = new EditorEngine("text", 40, 10, false);
        Assert.Equal(EditorRequest.Splits, editor.Type(":splits<CR>"));
    }
}
