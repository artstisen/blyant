namespace Blyant.Tests;

/// <summary>
/// Notes on the bookmarks of the Jump menu (bookmarks.txt), the notes copied with the favorites, and new files
/// bookmarked below the selected bookmark
/// </summary>
[Collection("Static state")]
public class BookmarkNotesTests : IDisposable
{
    private readonly string _directory;

    public BookmarkNotesTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blyant-booknotes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Bookmarks.FilePathOverride = Path.Combine(_directory, Bookmarks.FileName);
        FileFavorites.FilePathOverride = Path.Combine(_directory, FileFavorites.FileName);
        Bookmarks.Clear();
        FileFavorites.Parse(string.Empty);
    }

    public void Dispose()
    {
        Bookmarks.FilePathOverride = null;
        FileFavorites.FilePathOverride = null;
        Bookmarks.Clear();
        FileFavorites.Parse(string.Empty);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.GetFullPath(Path.Combine(_directory, name));

    private static string Show() => string.Join(" ", Bookmarks.Items.Select(entry =>
        new string('.', entry.Level) + (entry.IsFolder ? "[" + entry.Name + "]" : entry.Target.Name)));

    [Fact]
    public void Notes_AreKeptOnTheLineAfterTheirBookmark_AndOlderListsReadAsBefore()
    {
        const string list = "[Work]\tCyan\n#About work\\nsecond line\n file\t/x/a\n #Waiting\\tfor review\npage\tstart\n";
        Bookmarks.Parse(list);
        Assert.Equal("[Work] .a start", Show());
        Assert.Equal("About work\nsecond line", Bookmarks.Items[0].Note);
        Assert.Equal("Waiting\tfor review", Bookmarks.Items[1].Note);
        Assert.Equal(string.Empty, Bookmarks.Items[2].Note);
        Assert.Equal(list, Bookmarks.Serialize());
        // A note before any bookmark is left out
        Bookmarks.Parse("#lost\nfile\t/x/a\n");
        Assert.Equal(string.Empty, Bookmarks.Items[0].Note);
        // A blank note is none
        Bookmarks.SetNote(Bookmarks.Items[0], " \n\n");
        Assert.Equal("file\t/x/a\n", Bookmarks.Serialize());
    }

    [Fact]
    public void TheNotesOfTheFavorites_AreCopied_AndANoteABookmarkHasIsKept()
    {
        FileFavorites.Parse("\t[Work]\n#Folder note\n \t" + In("a.md") + "\n #Note of a\n\t" + In("b.md") +
                            "\n#Note of b\n\t" + In("c.md") + "\n#Note of c\n");
        Bookmarks.Add(new JumpTarget(JumpKind.File, In("b.md")));
        Bookmarks.Add(new JumpTarget(JumpKind.File, In("c.md")));
        Bookmarks.SetNote(Bookmarks.Items[1], "My own note");
        var plan = Bookmarks.ImportFavorites(FileFavorites.Items, false);
        // a is added with its note, the folder is new with its note, b gets the note it did not have, c keeps its own
        Assert.Equal(new Bookmarks.ImportResult(1, 2, 1, 0, 3), plan);
        Assert.Equal(string.Empty, Bookmarks.Items[0].Note);
        Assert.Equal(plan, Bookmarks.ImportFavorites(FileFavorites.Items, true));
        Assert.Equal("Note of b", Bookmarks.Items[0].Note);
        Assert.Equal("My own note", Bookmarks.Items[1].Note);
        var work = Bookmarks.Items.Single(entry => entry.IsFolder);
        Assert.Equal("Folder note", work.Note);
        Assert.Equal("Note of a", Bookmarks.Items.Single(entry => entry.Target.Target == In("a.md")).Note);
        // Again: nothing to do
        Assert.Equal(new Bookmarks.ImportResult(0, 3, 0, 1, 0), Bookmarks.ImportFavorites(FileFavorites.Items, false));
    }

    [Fact]
    public void ANewFile_IsBookmarkedBelowTheSelectedBookmark()
    {
        Bookmarks.Parse("[A]\n file\t/x/a\nfile\t/x/c\n");
        Assert.Equal(1, Bookmarks.AddBelow(new JumpTarget(JumpKind.File, "/x/n1"), 0));
        Assert.Equal("[A] .n1 .a c", Show());
        Bookmarks.AddBelow(new JumpTarget(JumpKind.File, "/x/n2"), 2);
        Assert.Equal("[A] .n1 .a .n2 c", Show());
        Bookmarks.AddBelow(new JumpTarget(JumpKind.File, "/x/n3"), -1);
        Assert.Equal("[A] .n1 .a .n2 c n3", Show());
        Assert.Equal(-1, Bookmarks.AddBelow(new JumpTarget(JumpKind.File, "/x/c"), 0));
    }

    [Fact]
    public void ANewFile_StartsInTheFolderOfTheSelectedBookmark()
    {
        Directory.CreateDirectory(In("docs"));
        File.WriteAllText(In("docs/a.md"), "x");
        Bookmarks.Parse("[Docs]\n page\tstart\n file\t" + In("docs/a.md") + "\ndir\t" + In("docs") + "\npage\tstart2\n");
        Assert.Equal(In("docs"), JumpMenu.FolderForNewFile(Bookmarks.Items[0]));
        Assert.Equal(In("docs"), JumpMenu.FolderForNewFile(Bookmarks.Items[2]));
        Assert.Equal(In("docs"), JumpMenu.FolderForNewFile(Bookmarks.Items[3]));
        Assert.Null(JumpMenu.FolderForNewFile(Bookmarks.Items[1]));
        Assert.Null(JumpMenu.FolderForNewFile(null));
    }

    [Fact]
    public void ABookmarkFoundAgain_AfterTheListIsReadAgain_IsTheSameOne()
    {
        const string list = "[Work]\n [Drafts]\n[Home]\n [Drafts]\nfile\t/x/a\n";
        Bookmarks.Parse(list);
        var home = Bookmarks.Items[3];
        var a = Bookmarks.Items[4];
        Bookmarks.Parse(list);
        Assert.Same(Bookmarks.Items[3], JumpMenu.FindSameBookmark(home, 3, "Home"));
        Assert.Same(Bookmarks.Items[4], JumpMenu.FindSameBookmark(a, 4, ""));
        Assert.Null(JumpMenu.FindSameBookmark(new Bookmarks.Entry(new JumpTarget(JumpKind.File, "/x/gone")), 0, ""));
    }
}
