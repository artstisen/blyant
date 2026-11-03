namespace Blyant.Tests;

/// <summary>
/// "gf": the name of a file written in the text, and the files it can be
/// </summary>
public class FileReferenceTests
{
    [Theory]
    [InlineData("See chapter-2.md for more", 6, "chapter-2.md")]
    [InlineData("See chapter-2.md for more", 15, "chapter-2.md")]
    [InlineData("notes/plan.md", 3, "notes/plan.md")]
    [InlineData("The end is in end.md.", 17, "end.md")]
    [InlineData("- `api.md`: the API", 4, "api.md")]
    [InlineData("(see draft.typ)", 7, "draft.typ")]
    [InlineData("**intro.md**", 4, "intro.md")]
    [InlineData("_index.md", 2, "_index.md")]
    [InlineData("See _draft.md.", 6, "_draft.md")]
    [InlineData("*notes.md*", 3, "notes.md")]
    [InlineData("__notes.md__", 3, "notes.md")]
    [InlineData("See plan.md#intro", 6, "plan.md")]
    [InlineData("at notes.md:12", 5, "notes.md")]
    [InlineData("at notes.md:12:5", 5, "notes.md")]
    [InlineData("issue#12", 3, "issue#12")]
    [InlineData("two words", 3, "")]
    [InlineData("two words", 1, "two")]
    [InlineData("", 0, "")]
    public void TheNameAtTheCursor_IsTheWordOfAFileName(string line, int column, string name)
    {
        Assert.Equal(name, FileReference.At(line, column));
    }

    [Theory]
    [InlineData("- [The plan](notes/plan.md) for now", 4, "notes/plan.md")]
    [InlineData("- [The plan](notes/plan.md) for now", 15, "notes/plan.md")]
    [InlineData("[Intro](intro.md#start)", 2, "intro.md")]
    [InlineData("[Old](old%20notes.md)", 1, "old notes.md")]
    [InlineData("[Old](<old notes.md>)", 1, "old notes.md")]
    [InlineData("[Plan](plan.md \"The plan\")", 1, "plan.md")]
    [InlineData("[Side](_sidebar.md)", 1, "_sidebar.md")]
    public void InALink_TheNameIsItsTarget(string line, int column, string name)
    {
        Assert.Equal(name, FileReference.At(line, column));
    }

    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "blyant-gf"));

    private static string In(params string[] parts) => Path.Combine(new[] { Root }.Concat(parts).ToArray());

    [Fact]
    public void AFileNextToTheFileEdited_IsTheOne()
    {
        var there = new HashSet<string> { In("book", "ch1.md"), In("book", "notes", "plan.md"), In("other", "ch1.md") };
        var current = In("book", "contents.md");
        var known = new[] { In("other", "ch1.md") };
        Assert.Equal(new[] { In("book", "ch1.md") }, FileReference.Find("ch1.md", current, known, there.Contains));
        Assert.Equal(new[] { In("book", "notes", "plan.md") },
            FileReference.Find("notes/plan.md", current, known, there.Contains));
        // Without its extension
        Assert.Equal(new[] { In("book", "ch1.md") }, FileReference.Find("ch1", current, known, there.Contains));
    }

    [Fact]
    public void ElseTheFilesKnown_ByTheEndOfTheirPath()
    {
        var there = new HashSet<string> { In("a", "plan.md"), In("b", "plan.md"), In("b", "myplan.md"), In("c", "x", "plan.md") };
        var known = new[] { In("a", "plan.md"), In("b", "plan.md"), In("b", "myplan.md"), In("gone", "plan.md"), In("c", "x", "plan.md") };
        var current = In("toc", "contents.md");
        // All of that name (not "myplan.md", nor a file that is gone)
        Assert.Equal(new[] { In("a", "plan.md"), In("b", "plan.md"), In("c", "x", "plan.md") },
            FileReference.Find("plan.md", current, known, there.Contains));
        // Upper and lower case alike, and a folder in the name narrows it
        Assert.Equal(new[] { In("c", "x", "plan.md") }, FileReference.Find("X/PLAN.md", current, known, there.Contains));
        Assert.Equal(new[] { In("c", "x", "plan.md") }, FileReference.Find("../x/plan.md", current, known, there.Contains));
        Assert.Empty(FileReference.Find("nothing.md", current, known, there.Contains));
    }

    [Fact]
    public void TheFileEdited_IsLeftOut_AndEachFileIsOnce()
    {
        var there = new HashSet<string> { In("a", "plan.md"), In("b", "plan.md") };
        var known = new[] { In("a", "plan.md"), In("a", "plan.md"), In("b", "plan.md") };
        Assert.Equal(new[] { In("b", "plan.md") }, FileReference.Find("plan.md", In("a", "plan.md"), known, there.Contains));
        // Nothing edited (a new text): the files known only
        Assert.Equal(2, FileReference.Find("plan.md", null, known, there.Contains).Count);
    }

    [Fact]
    public void AFullPath_IsTheFile()
    {
        var there = new HashSet<string> { In("a", "plan.md") };
        Assert.Equal(new[] { In("a", "plan.md") }, FileReference.Find(In("a", "plan.md"), null, Array.Empty<string>(), there.Contains));
    }

    [Fact]
    public void ABackslash_IsASeparator_OnAllSystems()
    {
        var there = new HashSet<string> { In("book", "notes", "plan.md") };
        var current = In("book", "contents.md");
        Assert.Equal(new[] { In("book", "notes", "plan.md") },
            FileReference.Find("notes\\plan.md", current, Array.Empty<string>(), there.Contains));
        // and in the files known
        Assert.Equal(new[] { In("book", "notes", "plan.md") },
            FileReference.Find("notes\\plan.md", null, there, there.Contains));
    }

    [Fact]
    public void APathFromTheTop_IsLookedForAboveTheFile()
    {
        var there = new HashSet<string> { In("site", "docs", "plan.md") };
        var current = In("site", "blog", "2026", "post.md");
        Assert.Equal(new[] { In("site", "docs", "plan.md") },
            FileReference.Find("/docs/plan.md", current, Array.Empty<string>(), there.Contains));
        // Nothing edited: the files known, by the end of their path
        Assert.Equal(new[] { In("site", "docs", "plan.md") },
            FileReference.Find("/docs/plan.md", null, there, there.Contains));
    }

    [Fact]
    public void AFile_IsThere_AndOneThatIsGone_IsNot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "blyant-gf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "plan.md");
            File.WriteAllText(file, "plan");
            Assert.True(FileOperations.IsFileThere(file));
            Assert.False(FileOperations.IsFileThere(Path.Combine(directory, "gone.md")));
            Assert.False(FileOperations.IsFileThere(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [UnixFact]
    public void ALinkToAFileThatIsGone_IsNotThere()
    {
        var directory = Path.Combine(Path.GetTempPath(), "blyant-gf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var link = Path.Combine(directory, "notes.md");
            File.CreateSymbolicLink(link, Path.Combine(directory, "gone.md"));
            // (File.Exists is true here on Linux and macOS)
            Assert.True(File.Exists(link));
            Assert.False(FileOperations.IsFileThere(link));
            Assert.Empty(FileReference.Find("notes.md", Path.Combine(directory, "toc.md"), Array.Empty<string>(),
                FileOperations.IsFileThere));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("notes.md:12", "notes.md")]
    [InlineData("plan.md#intro", "plan.md")]
    [InlineData("notes.md", "notes.md")]
    [InlineData("issue#12", "issue#12")]
    public void APlaceAfterTheName_IsLeftOut_AlsoFromASelection(string name, string without)
    {
        Assert.Equal(without, FileReference.WithoutPlace(name));
    }

    [Fact]
    public void APathFromTheTop_IsLookedForInTheTopFolder_First()
    {
        var there = new HashSet<string> { In("site", "index.md"), In("site", "docs", "guide", "index.md") };
        var current = In("site", "docs", "guide", "page.md");
        Assert.Equal(new[] { In("site", "index.md") },
            FileReference.Find("/index.md", current, Array.Empty<string>(), there.Contains, folder => folder == In("site")));
        // Without a top: the nearest first
        Assert.Equal(new[] { In("site", "docs", "guide", "index.md"), In("site", "index.md") },
            FileReference.Find("/index.md", current, Array.Empty<string>(), there.Contains));
    }

    [Fact]
    public void AtTheRootOfTheDrive_APathFromTheTop_IsLookedForLast()
    {
        var atRoot = Path.Combine(Path.GetPathRoot(Root)!, "README.md");
        var there = new HashSet<string> { Path.GetFullPath(atRoot), In("site", "README.md") };
        var current = In("site", "docs", "page.md");
        Assert.Equal(new[] { In("site", "README.md") },
            FileReference.Find("/README.md", current, Array.Empty<string>(), there.Contains));
        there.Remove(In("site", "README.md"));
        Assert.Equal(new[] { Path.GetFullPath(atRoot) },
            FileReference.Find("/README.md", current, Array.Empty<string>(), there.Contains));
    }
}
