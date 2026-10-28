namespace Blyant.Tests;

/// <summary>
/// Subfolders and notes in the favorites (favorites.txt) and subfolders in the bookmarks, the panes below the
/// favorites (the note and the peek), and their settings
/// </summary>
[Collection("Static state")]
public class FavoriteSubfoldersTests : IDisposable
{
    private readonly string _directory;

    public FavoriteSubfoldersTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blyant-subfolders-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        FileFavorites.FilePathOverride = Path.Combine(_directory, FileFavorites.FileName);
        Bookmarks.FilePathOverride = Path.Combine(_directory, Bookmarks.FileName);
        FileFavorites.Parse(string.Empty);
        Bookmarks.Clear();
    }

    public void Dispose()
    {
        FileFavorites.FilePathOverride = null;
        Bookmarks.FilePathOverride = null;
        FileFavorites.Parse(string.Empty);
        Bookmarks.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.GetFullPath(Path.Combine(_directory, name));

    // The list as text: folders in brackets (with "+" when closed), a "." in front for each folder an item is in
    private static string Show() => string.Join(" ", FileFavorites.Items.Select(item =>
        new string('.', item.Level) +
        (item.IsFolder ? "[" + item.Name + (item.Collapsed ? "+" : "") + "]" : Path.GetFileName(item.Path))));

    private static int Find(string name)
    {
        for (var i = 0; i < FileFavorites.Items.Count; i++)
        {
            var item = FileFavorites.Items[i];
            if (item.IsFolder ? item.Name == name : Path.GetFileName(item.Path) == name) return i;
        }

        return -1;
    }

    private static void Move(string name, bool up, string expected)
    {
        Assert.True(FileFavorites.Move(Find(name), up));
        Assert.Equal(expected, Show());
    }

    private const string Article = "\t/x/a\nYellow\t[Article]\n \t/x/b\n Green\t[Done]\n  \t/x/c\n \t[Pending]\tcollapsed\n" +
                                   "  \t/x/d\n \t/x/e\n\t/x/f\n";

    [Fact]
    public void Subfolders_FileFormat_AndTheLinesOfOlderLists()
    {
        FileFavorites.Parse(Article);
        Assert.Equal("a [Article] .b .[Done] ..c .[Pending+] ..d .e f", Show());
        Assert.Equal(ConsoleColor.Green, FileFavorites.Items[Find("Done")].Color);
        Assert.Equal(Article, FileFavorites.Serialize());
        // Counted with what is in the subfolders
        Assert.Equal(4, FileFavorites.FolderCount(Find("Article")));
        Assert.Equal(2, FileFavorites.SubfolderCount(Find("Article")));
        Assert.Equal(1, FileFavorites.FolderCount(Find("Done")));
        Assert.Equal(Find("Pending"), FileFavorites.Parent(Find("d")));
        Assert.Equal(Find("Article"), FileFavorites.Parent(Find("Pending")));
        Assert.Equal(Find("Article"), FileFavorites.FolderOf(Find("d")));
        // In a closed subfolder: not shown
        Assert.False(FileFavorites.IsShown(Find("d")));
        Assert.True(FileFavorites.IsShown(Find("e")));

        // Deeper than the folders above it: in the ones there are. A folder line with a blank space outside a folder
        // is a folder
        FileFavorites.Parse("  \t/x/a\n \t[One]\n  \t/x/b\n   \t/x/c\n \t[Sub]\n   \t/x/d\n");
        Assert.Equal("a [One] .b .c .[Sub] ..d", Show());
    }

    [Fact]
    public void Subfolders_AreAdded_InTheFolderOfTheSelectedItem()
    {
        FileFavorites.Parse("\t/x/a\n\t[One]\n \t/x/b\n");
        // Not in a folder: no subfolder
        Assert.Equal(-2, FileFavorites.AddSubfolder("Sub", Find("a")));
        Assert.Equal(-1, FileFavorites.AddSubfolder("a]", Find("One")));
        // The folder selected: first in it
        Assert.Equal(2, FileFavorites.AddSubfolder("First", Find("One")));
        Assert.Equal("a [One] .[First] .b", Show());
        // An item in a folder: in front of it, and in a subfolder: in front of the subfolder
        FileFavorites.MoveToFolder(new[] { FileFavorites.Items[Find("b")] }, FileFavorites.Items[Find("First")]);
        Assert.Equal("a [One] .[First] ..b", Show());
        Assert.Equal(2, FileFavorites.AddSubfolder("Second", Find("b")));
        Assert.Equal("a [One] .[Second] .[First] ..b", Show());
        // A closed folder is opened
        FileFavorites.SetAllCollapsed(true);
        FileFavorites.AddSubfolder("Third", Find("One"));
        Assert.False(FileFavorites.Items[Find("One")].Collapsed);
    }

    [Fact]
    public void Favorites_MoveIntoAndOutOfSubfolders_OneStepAtATime()
    {
        FileFavorites.Parse("\t[A]\n \t[S]\n  \t/x/s\n \t/x/b\n\t/x/c\n");
        Assert.Equal("[A] .[S] ..s .b c", Show());
        // Up: into the folder above, then into its last subfolder, then up in it
        Move("c", true, "[A] .[S] ..s .b .c");
        Move("c", true, "[A] .[S] ..s .c .b");
        Move("c", true, "[A] .[S] ..s ..c .b");
        Move("c", true, "[A] .[S] ..c ..s .b");
        // The first of the subfolder: out of it, above it, and then out of the folder
        Move("c", true, "[A] .c .[S] ..s .b");
        Move("c", true, "c [A] .[S] ..s .b");
        Assert.False(FileFavorites.Move(Find("c"), true));
        // Down again: into the folder, into the subfolder, out at its end and out at the end of the folder
        Move("c", false, "[A] .c .[S] ..s .b");
        Move("c", false, "[A] .[S] ..c ..s .b");
        Move("c", false, "[A] .[S] ..s ..c .b");
        Move("c", false, "[A] .[S] ..s .c .b");
        Move("c", false, "[A] .[S] ..s .b .c");
        Move("c", false, "[A] .[S] ..s .b c");
        Assert.False(FileFavorites.Move(Find("c"), false));
        // A closed subfolder is opened when a favorite goes into it
        FileFavorites.Items[Find("S")].Collapsed = true;
        Move("b", true, "[A] .[S] ..s ..b c");
        Assert.False(FileFavorites.Items[Find("S")].Collapsed);
    }

    [Fact]
    public void Subfolders_MoveInTheirFolder_AndIntoTheFolderNextToIt()
    {
        FileFavorites.Parse("\t[A]\n \t/x/a\n \t[S]\n  \t/x/s\n\t[B]\n \t/x/b\n\t/x/c\n");
        Move("S", true, "[A] .[S+] ..s .a [B] .b c");
        // The first of its folder: last in the folder above (there is none), or first in the folder below
        Assert.False(FileFavorites.Move(Find("S"), true));
        Move("S", false, "[A] .a .[S+] ..s [B] .b c");
        Move("S", false, "[A] .a [B] .[S+] ..s .b c");
        Move("S", false, "[A] .a [B] .b .[S+] ..s c");
        // Not out of the folders: there is no folder below
        Assert.False(FileFavorites.Move(Find("S"), false));
        Move("S", true, "[A] .a [B] .[S+] ..s .b c");
        Move("S", true, "[A] .a .[S+] ..s [B] .b c");
        // A folder moves past the folder next to it, with its subfolders
        Move("A", false, "[B] .b [A+] .a .[S+] ..s c");
    }

    [Fact]
    public void Removed_FoldersWithSubfolders_ArePutBack_AndAFavoriteWhoseSubfolderIsGoneIsOutside()
    {
        FileFavorites.Parse(Article);
        var shown = Show();
        FileFavorites.RemoveAt(Find("Article"));
        Assert.Equal("a f", Show());
        Assert.Equal(1, FileFavorites.UndoRemove());
        Assert.Equal(shown, Show());

        // The favorite goes back into its subfolder
        FileFavorites.RemoveAt(Find("c"));
        FileFavorites.UndoRemove();
        Assert.Equal(shown, Show());
        // Its subfolder is no longer where it was: outside the folders, in front of the folder it would be in
        FileFavorites.RemoveAt(Find("c"));
        Move("Done", true, "a [Article] .[Done+] .b .[Pending+] ..d .e f");
        Assert.Equal(1, FileFavorites.UndoRemove());
        Assert.Equal("a c [Article] .[Done+] .b .[Pending+] ..d .e f", Show());
    }

    [Fact]
    public void Favorites_MoveToASubfolder_AndToTheTopOfIt()
    {
        FileFavorites.Parse(Article);
        var pending = FileFavorites.Items[Find("Pending")];
        FileFavorites.MoveToFolder(new[] { FileFavorites.Items[Find("a")], FileFavorites.Items[Find("f")] }, pending);
        Assert.Equal("[Article] .b .[Done] ..c .[Pending] ..d ..a ..f .e", Show());
        FileFavorites.MoveToEnd(new[] { FileFavorites.Items[Find("f")] }, true);
        Assert.Equal("[Article] .b .[Done] ..c .[Pending] ..f ..d ..a .e", Show());
        FileFavorites.MoveToEnd(new[] { FileFavorites.Items[Find("Done")] }, false);
        Assert.Equal("[Article] .b .[Pending] ..f ..d ..a .e .[Done] ..c", Show());
    }

    [Fact]
    public void Notes_AreKeptInTheList_OnTheLineAfterTheirItem()
    {
        FileFavorites.Parse(Article);
        FileFavorites.SetNote(FileFavorites.Items[Find("Pending")], "Waiting for review\nSee \\\\server\\share\tnow\r\n\n");
        FileFavorites.SetNote(FileFavorites.Items[Find("a")], "Link: https://example.com/a#b");
        FileFavorites.SetNote(FileFavorites.Items[Find("f")], " \n ");
        Assert.Equal(string.Empty, FileFavorites.Items[Find("f")].Note);
        var text = FileFavorites.Serialize();
        Assert.Contains("\t/x/a\n#Link: https://example.com/a#b\n", text);
        Assert.Contains(" \t[Pending]\tcollapsed\n #Waiting for review\\nSee \\\\\\\\server\\\\share\\tnow\n", text);

        FileFavorites.Parse(text);
        Assert.Equal("Waiting for review\nSee \\\\server\\share\tnow", FileFavorites.Items[Find("Pending")].Note);
        Assert.Equal("Link: https://example.com/a#b", FileFavorites.Items[Find("a")].Note);
        // The note does not change where the items are
        Assert.Equal("a [Article] .b .[Done] ..c .[Pending+] ..d .e f", Show());
        Assert.Equal(text, FileFavorites.Serialize());

        // A note goes with its favorite when it is moved, removed and put back, and when its file is renamed
        FileFavorites.RemoveAt(Find("a"));
        FileFavorites.UndoRemove();
        Assert.Equal("Link: https://example.com/a#b", FileFavorites.Items[Find("a")].Note);
        FileFavorites.PathChanged("/x/a", "/x/renamed");
        Assert.Equal("Link: https://example.com/a#b", FileFavorites.Items[Find("renamed")].Note);
        // A note before any item is left out
        FileFavorites.Parse("#lost\n\t/x/a\n");
        Assert.Equal(string.Empty, FileFavorites.Items[0].Note);
    }

    [Fact]
    public void ANoteFoundAgain_AfterTheListIsReadAgain_IsOfTheSameFolder_NotOneOfTheSameNameElsewhere()
    {
        const string list = "\t[Work]\n \t[Drafts]\n\t[Home]\n \t[Drafts]\n\t/x/a\n";
        FileFavorites.Parse(list);
        var home = FileFavorites.Items[3];
        // Read again: new items. The subfolder Drafts of Home, not of Work
        FileFavorites.Parse(list);
        Assert.Same(FileFavorites.Items[3], FileExplorer.FindSameFavorite(home, 3, "Home"));
        Assert.Same(FileFavorites.Items[4], FileExplorer.FindSameFavorite(FileFavorites.Items[4], 4, ""));
        // Two of the same name in folders of the same name: only the one at its place
        FileFavorites.Parse("\t[A]\n \t[S]\n\t[A]\n \t[S]\n");
        var sub = new FileFavorites.Favorite { IsFolder = true, Name = "S", Level = 1 };
        Assert.Same(FileFavorites.Items[3], FileExplorer.FindSameFavorite(sub, 3, "A"));
        Assert.Null(FileExplorer.FindSameFavorite(sub, 0, "A"));
        Assert.Null(FileExplorer.FindSameFavorite(new FileFavorites.Favorite { Path = "/x/gone" }, 0, ""));
    }

    [Fact]
    public void EachNote_IsWrittenInAFileOfItsOwn()
    {
        FileFavorites.Parse("\t[Work]\n \t[Drafts]\n\t[Home]\n \t[Drafts]\n\t/x/a\n\t/x/b\n");
        var names = Enumerable.Range(0, FileFavorites.Items.Count).Select(i =>
        {
            var parent = FileFavorites.Parent(i);
            return FileExplorer.FavoriteNoteFileName(FileFavorites.Items[i],
                parent < 0 ? "" : FileFavorites.Items[parent].Name);
        }).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.Matches("^favorite-note-[0-9a-f]{12}\\.md$", name));
        // The same each time
        Assert.Equal(names[4], FileExplorer.FavoriteNoteFileName(FileFavorites.Items[4], ""));
    }

    [Fact]
    public void AFolderThatCannotMove_StaysOpen_AndWhatIsPutBack_IsShown()
    {
        FileFavorites.Parse("\t[A]\n \t/x/a\n");
        Assert.False(FileFavorites.Move(0, true));
        Assert.False(FileFavorites.Items[0].Collapsed);
        FileFavorites.RemoveAt(1);
        FileFavorites.Items[0].Collapsed = true;
        Assert.Equal(1, FileFavorites.UndoRemove());
        FileFavorites.Reveal(1);
        Assert.False(FileFavorites.Items[0].Collapsed);
        Assert.True(FileFavorites.IsShown(1));
    }

    [Fact]
    public void ABookmarkFolderWithANameThatCannotBeUsed_DoesNotTakeWhatFollowsIntoTheFolderAbove()
    {
        Bookmarks.Parse("[One]\n [Sub]\n  file\t/x/a\n [a[b]\n  file\t/x/b\n");
        Assert.Equal(new[] { 0, 1, 2, 0 }, Bookmarks.Items.Select(entry => entry.Level));
    }

    [Fact]
    public void ANewFile_BecomesAFavoriteBelowTheSelectedItem()
    {
        FileFavorites.Parse("\t[A]\tcollapsed\n \t[S]\tcollapsed\n  \t/x/s\n \t/x/b\n\t/x/c\n");
        // A folder selected: first in it (opened)
        Assert.Equal(2, FileFavorites.AddBelow("/x/n1", Find("S")));
        Assert.Equal("[A] .[S] ..n1 ..s .b c", Show());
        // A favorite selected: after it, in its folder
        Assert.Equal(Find("s") + 1, FileFavorites.AddBelow("/x/n2", Find("s")));
        Assert.Equal("[A] .[S] ..n1 ..s ..n2 .b c", Show());
        FileFavorites.AddBelow("/x/n3", Find("c"));
        Assert.Equal("[A] .[S] ..n1 ..s ..n2 .b c n3", Show());
        // Nothing selected: at the end. A favorite already: not again
        FileFavorites.AddBelow("/x/n4", -1);
        Assert.EndsWith(" n4", Show());
        Assert.Equal(-1, FileFavorites.AddBelow("/x/c", 0));
    }

    [Fact]
    public void ANewFile_StartsInTheFolderOfTheSelectedFavorite()
    {
        Directory.CreateDirectory(In("docs"));
        File.WriteAllText(In("docs/a.md"), "x");
        FileFavorites.Parse("\t[Empty]\n\t[Docs]\n \t" + In("docs/a.md") + "\n\t" + In("docs") + "\n\t" + In("gone.md") + "\n");
        Assert.Null(FileExplorer.FavoriteFolderForNewFile(0));
        Assert.Equal(In("docs"), FileExplorer.FavoriteFolderForNewFile(1));
        Assert.Equal(In("docs"), FileExplorer.FavoriteFolderForNewFile(2));
        Assert.Equal(In("docs"), FileExplorer.FavoriteFolderForNewFile(3));
        Assert.Null(FileExplorer.FavoriteFolderForNewFile(4));
        Assert.Null(FileExplorer.FavoriteFolderForNewFile(-1));
        // It is the first of the places Tab shows
        Assert.Equal(In("docs"), StartMenu.NewFilePlaces(In("docs"))[0].Directory);
    }

    [Fact]
    public void TheNoteInAPane_IsWrapped_AndCut()
    {
        Assert.Equal(new[] { "(no note)" }, NotePanes.NoteLines(string.Empty, 20, 5));
        Assert.Equal(new[] { "one two", "three", "", "four" }, NotePanes.NoteLines("one two three\n\nfour", 8, 5));
        Assert.Equal(new[] { "a", "..." }, NotePanes.NoteLines("a\nb\nc", 10, 2));
    }

    [Fact]
    public void ThePeek_ShowsTheTopOfAFile_AndNeverWritesIt()
    {
        var path = In("long.md");
        File.WriteAllText(path, "﻿# Title\r\n\tindented\n" + string.Join("\n", Enumerable.Range(0, 100000)));
        var written = File.GetLastWriteTimeUtc(path);
        var lines = NotePanes.PeekLines(path, 3);
        Assert.Equal(new[] { "# Title", "    indented", "0" }, lines);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        File.WriteAllBytes(In("binary.bin"), new byte[] { 1, 0, 2 });
        Assert.Equal(new[] { "(not a text file)" }, NotePanes.PeekLines(In("binary.bin"), 3));
        File.WriteAllText(In("empty.md"), string.Empty);
        Assert.Equal(new[] { "(empty)" }, NotePanes.PeekLines(In("empty.md"), 3));
    }

    [Fact]
    public void Subfolders_OfTheBookmarks_FileFormat_AndTheFavoritesCopiedWithThem()
    {
        Bookmarks.Parse("[Work]\tCyan\n file\t/x/a\n [Sub]\t\tcollapsed\n  file\t/x/b\n file\t/x/c\n");
        Assert.Equal(new[] { 0, 1, 1, 2, 1 }, Bookmarks.Items.Select(entry => entry.Level));
        Assert.Equal("[Work]\tCyan\n file\t/x/a\n [Sub]\t\tcollapsed\n  file\t/x/b\n file\t/x/c\n", Bookmarks.Serialize());
        Assert.Equal(3, Bookmarks.FolderCount(0));
        Assert.Equal(1, Bookmarks.SubfolderCount(0));
        Assert.False(Bookmarks.IsShown(3));
        Assert.Equal(-2, Bookmarks.AddSubfolder("New", -1));

        // A subfolder of the favorites: a subfolder of the bookmark folder (merged with one of the same name)
        FileFavorites.Parse("\t[work]\n \t[sub]\n  \t/x/d\n \t[Other]\n  \t/x/e\n \t/x/f\n\t/x/g\n");
        var result = Bookmarks.ImportFavorites(FileFavorites.Items, true);
        Assert.Equal(new ImportResult(4, 0, 1, 2, 0), result);
        Assert.Equal("[Work] .a .[Sub] ..b ..d .c .[Other] ..e .f g", string.Join(" ", Bookmarks.Items.Select(entry =>
            new string('.', entry.Level) + (entry.IsFolder ? "[" + entry.Name + "]" : entry.Target.Name))));
    }

    [Fact]
    public void AFolder_MovesIntoAnother_AndASubfolderOutOfItsFolder()
    {
        FileFavorites.Parse(Article);
        Assert.Equal("a [Article] .b .[Done] ..c .[Pending+] ..d .e f", Show());
        // A subfolder out of its folder: a folder, right after it
        var done = FileFavorites.Items[Find("Done")];
        Assert.True(FileFavorites.MoveFolderTo(done, null) >= 0);
        Assert.Equal("a [Article] .b .[Pending+] ..d .e [Done] .c f", Show());
        // A folder into another: last in it, with what is in it
        Assert.True(FileFavorites.MoveFolderTo(done, FileFavorites.Items[Find("Article")]) >= 0);
        Assert.Equal("a [Article] .b .[Pending+] ..d .e .[Done] ..c f", Show());
        // A folder with subfolders does not go into another (two levels at most), nor a folder into itself
        FileFavorites.AddFolder("Other", Find("f"));
        var article = FileFavorites.Items[Find("Article")];
        var other = FileFavorites.Items[Find("Other")];
        Assert.Equal(-1, FileFavorites.MoveFolderTo(article, other));
        Assert.Equal(-1, FileFavorites.MoveFolderTo(other, other));
        // A folder at the top is not out of a folder
        Assert.Equal(-1, FileFavorites.MoveFolderTo(other, null));
        // A subfolder into another folder at the top, closed as it was; the folder is opened
        other.Collapsed = true;
        var pending = FileFavorites.Items[Find("Pending")];
        Assert.True(FileFavorites.MoveFolderTo(pending, other) >= 0);
        Assert.Equal("a [Article] .b .e .[Done] ..c [Other] .[Pending+] ..d f", Show());
        // ... and not into the folder it is in
        Assert.Equal(-1, FileFavorites.MoveFolderTo(pending, other));
        // The bookmarks have the same folders
        Bookmarks.Parse("[Work]\n file\t/x/a\n [Old]\n  file\t/x/b\n[Archive]\n");
        var old = Bookmarks.Items.First(entry => entry.Name == "Old");
        Assert.True(Bookmarks.MoveFolderTo(old, null) >= 0);
        Assert.True(Bookmarks.MoveFolderTo(old, Bookmarks.Items.First(entry => entry.Name == "Archive")) >= 0);
        Assert.Equal("[Work] .a [Archive] .[Old] ..b", string.Join(" ", Bookmarks.Items.Select(entry =>
            new string('.', entry.Level) + (entry.IsFolder ? "[" + entry.Name + "]" : entry.Target.Name))));
    }
}
