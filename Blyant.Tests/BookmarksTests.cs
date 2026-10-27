namespace Blyant.Tests;

/// <summary>
/// The bookmarks of the Jump menu (bookmarks.txt), and the tabs of the menu: tabs at the top, the lists of the file
/// browser as sub-tabs, Tab between the tabs and h l through all the lists
/// </summary>
[Collection("Static state")]
public class BookmarksTests : IDisposable
{
    private readonly string _directory;

    public BookmarksTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blyant-bookmarks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Bookmarks.FilePathOverride = Path.Combine(_directory, Bookmarks.FileName);
        Bookmarks.Clear();
    }

    public void Dispose()
    {
        Bookmarks.FilePathOverride = null;
        Bookmarks.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.GetFullPath(Path.Combine(_directory, name));
    private JumpTarget FileAt(string name) => new(JumpKind.File, In(name));

    [Fact]
    public void TheFavorites_AreCopiedIntoTheBookmarks_WithTheirFolders_AndWhatIsThereIsLeftOut()
    {
        Directory.CreateDirectory(In("Novel"));
        File.WriteAllText(In("notes.md"), "x");
        var favorites = new List<FileFavorites.Favorite>
        {
            new() { Path = In("notes.md"), Color = ConsoleColor.Green },
            new() { Path = In("Novel") },
            new() { IsFolder = true, Name = "Work", Color = ConsoleColor.Cyan, Collapsed = true },
            new() { Path = In("gone.md"), InFolder = true }, // Not there: copied too
            new() { Path = In("plan.md"), InFolder = true },
            new() { IsFolder = true, Name = "Ideas" },
            new() { Path = In("idea.md"), InFolder = true },
            new() { Path = In("last.md") } // After a folder, out of it
        };
        Bookmarks.Add(FileAt("plan.md")); // A bookmark already
        Bookmarks.AddFolder("ideas", 1); // A folder of the same name (in other case)
        var before = Bookmarks.Items.Count;

        // Counted first, without changing the list
        var plan = Bookmarks.ImportFavorites(favorites, false);
        Assert.Equal(new ImportResult(5, 1, 1, 1, 0), plan);
        Assert.Equal(before, Bookmarks.Items.Count);

        Assert.Equal(plan, Bookmarks.ImportFavorites(favorites, true));
        var items = Bookmarks.Items.ToList();
        var notes = items.Single(entry => entry.Target.SameAs(FileAt("notes.md")));
        Assert.Equal(ConsoleColor.Green, notes.Color);
        Assert.Equal(JumpKind.Directory, items.Single(entry => entry.Target.Target == In("Novel")).Target.Kind);
        var work = items.Single(entry => entry.IsFolder && entry.Name == "Work");
        Assert.True(work.Collapsed);
        Assert.Equal(ConsoleColor.Cyan, work.Color);
        // In the folder, in their order
        var at = items.IndexOf(work);
        Assert.Equal(In("gone.md"), items[at + 1].Target.Target);
        Assert.True(items[at + 1].InFolder);
        // Merged into the folder there was
        var ideas = items.Single(entry => entry.IsFolder && entry.Name == "ideas");
        Assert.Equal(In("idea.md"), items[items.IndexOf(ideas) + 1].Target.Target);
        Assert.True(items[items.IndexOf(ideas) + 1].InFolder);
        // Not in a folder
        Assert.False(items.Single(entry => entry.Target.Target == In("last.md")).InFolder);
        // Again: all are there
        Assert.Equal(0, Bookmarks.ImportFavorites(favorites, true).Added);
    }

    [Fact]
    public void TheBookmarks_AreCopiedIntoTheFavorites_WithoutThePagesAndTheNames()
    {
        Directory.CreateDirectory(In("Novel"));
        // A favorite already, and a folder of the same name (in other case)
        FileFavorites.Parse("\t" + In("plan.md") + "\n\t[ideas]\n");
        Bookmarks.Parse("file\t" + In("notes.md") + "\tMy notes\tGreen\n" +
                        "dir\t" + In("Novel") + "\n" +
                        "page\tclipboard\n" +
                        "file\t" + In("plan.md") + "\n" +
                        "[Ideas]\tCyan\tcollapsed\n" +
                        "#Folder note\n" +
                        " file\t" + In("idea.md") + "\n" +
                        " page\tlog\n");
        try
        {
            // Counted first, without changing the favorites
            var plan = FileFavorites.ImportBookmarks(Bookmarks.Items, false);
            Assert.Equal(new ImportResult(3, 1, 0, 1, 1, 2), plan);
            Assert.Equal(2, FileFavorites.Items.Count);
            Assert.Equal(string.Empty, FileFavorites.Items[1].Note);

            Assert.Equal(plan, FileFavorites.ImportBookmarks(Bookmarks.Items, true));
            var items = FileFavorites.Items.ToList();
            // The name given to the bookmark is not copied, its color is
            var notes = items.Single(item => item.Path == In("notes.md"));
            Assert.Equal(string.Empty, notes.Name);
            Assert.Equal(ConsoleColor.Green, notes.Color);
            Assert.Contains(items, item => item.Path == In("Novel"));
            // Merged into the folder there was, with the note it did not have
            var ideas = items.Single(item => item.IsFolder);
            Assert.Equal("Folder note", ideas.Note);
            Assert.Equal(In("idea.md"), items[items.IndexOf(ideas) + 1].Path);
            Assert.True(items[items.IndexOf(ideas) + 1].InFolder);
            // No page became a favorite, and the bookmarks stay
            Assert.Equal(5, items.Count);
            Assert.Equal(7, Bookmarks.Items.Count);
            // Again: all are there
            Assert.Equal(new ImportResult(0, 4, 0, 1, 0, 2), FileFavorites.ImportBookmarks(Bookmarks.Items, false));
        }
        finally
        {
            FileFavorites.Parse(string.Empty);
        }
    }

    [Fact]
    public void Places_AreAddedAtTheEnd_Once_WithoutALimit()
    {
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(FileAt("a.md")));
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(new JumpTarget(JumpKind.Page, "schemes")));
        Assert.Equal(Bookmarks.AddResult.AlreadyThere, Bookmarks.Add(FileAt("a.md")));
        Assert.Equal(2, Bookmarks.Items.Count);
        for (var i = 0; i < 250; i++) Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(FileAt("f" + i + ".md")));
        Assert.Equal(252, Bookmarks.Items.Count);
    }

    [Fact]
    public void TheList_IsSavedAndReadAgain_WithTheKindsAndNames()
    {
        Bookmarks.Add(FileAt("a.md"));
        Bookmarks.Add(new JumpTarget(JumpKind.Directory, In("docs")));
        Bookmarks.Add(new JumpTarget(JumpKind.Page, "config/editor"));
        Bookmarks.Rename(0, "Budget");
        Assert.Equal("file\t" + In("a.md") + "\tBudget\ndir\t" + In("docs") + "\npage\tconfig/editor\n",
            Bookmarks.Serialize());
        Assert.Equal(DialogText.Success, Bookmarks.Save());
        Bookmarks.Clear();
        Bookmarks.Load();
        Assert.Equal(3, Bookmarks.Items.Count);
        Assert.Equal("Budget", Bookmarks.Items[0].Shown);
        Assert.Equal(new JumpTarget(JumpKind.Page, "config/editor"), Bookmarks.Items[2].Target);
        // Edited by hand: an unknown kind (a data file of Marbles), a blank line and a place there twice are not used
        Bookmarks.Parse("nonsense\tx\n\ndata\tnotes/work.mbls\nfile\t" + In("a.md") + "\nfile\t" + In("a.md") +
                        "\r\n");
        Assert.Single(Bookmarks.Items);
    }

    [Fact]
    public void Bookmarks_AreMoved_Renamed_RemovedAndPutBack()
    {
        foreach (var name in new[] { "a", "b", "c" }) Bookmarks.Add(FileAt(name));
        Bookmarks.MoveToEnd(new[] { Bookmarks.Items[0] }, false);
        Assert.Equal(new[] { "b", "c", "a" }, Bookmarks.Items.Select(entry => entry.Shown));
        Bookmarks.MoveToEnd(new[] { Bookmarks.Items[2] }, true);
        Assert.True(Bookmarks.Move(2, true));
        Assert.True(Bookmarks.Move(1, false));
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
        Bookmarks.Rename(1, "  Mine\there ");
        Assert.Equal("Mine here", Bookmarks.Items[1].Shown);
        // An empty name, or the name of the place, shows its own name again
        Bookmarks.Rename(1, string.Empty);
        Assert.Equal("b", Bookmarks.Items[1].Shown);
        Bookmarks.RemoveAt(1);
        Assert.Equal(new[] { "a", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
        Assert.Equal(1, Bookmarks.UndoRemove());
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
    }

    [Fact]
    public void Folders_AreSavedWithColors_AndTheBookmarksAreNumberedWithoutThem()
    {
        foreach (var name in new[] { "a", "b", "c" }) Bookmarks.Add(FileAt(name));
        // A folder in front of b, and b moved down into it, then the folder closed
        Assert.Equal(1, Bookmarks.AddFolder("Work", 1));
        Bookmarks.MoveToFolder(Bookmarks.Places.Where(entry => entry.Shown != "a").ToList(), Bookmarks.Items[1]);
        Bookmarks.SetColor(0, ConsoleColor.Red);
        Bookmarks.SetColor(1, ConsoleColor.Green);
        Bookmarks.Items[1].Collapsed = true;

        // The bookmarks are numbered 1, 2, 3 in the order of the list: the folder is not counted, and the bookmarks
        // in the closed folder are
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Places.Select(entry => entry.Shown).OrderBy(name => name));
        Assert.Equal(1, Bookmarks.NumberOf(Bookmarks.Places[0]));
        Assert.Equal(3, Bookmarks.Places.Count);
        Assert.Equal(0, Bookmarks.NumberOf(Bookmarks.Items[1]));

        // Saved and read again as it was
        var text = Bookmarks.Serialize();
        Assert.Contains("[Work]\tGreen\tcollapsed\n", text);
        Assert.Contains("\tRed\n", text);
        Assert.Equal(DialogText.Success, Bookmarks.Save());
        var before = Bookmarks.Items.Select(entry => (entry.Shown, entry.IsFolder, entry.InFolder, entry.Color)).ToList();
        Bookmarks.Clear();
        Bookmarks.Load();
        Assert.Equal(before, Bookmarks.Items.Select(entry => (entry.Shown, entry.IsFolder, entry.InFolder, entry.Color)));
        Assert.True(Bookmarks.Items[1].Collapsed);
        Assert.Equal(2, Bookmarks.FolderCount(1));

        // A folder is removed with its bookmarks, and put back with them
        Bookmarks.RemoveAt(1);
        Assert.Single(Bookmarks.Places);
        Bookmarks.UndoRemove();
        Assert.Equal(3, Bookmarks.Places.Count);
        Assert.True(Bookmarks.Items[2].InFolder);
    }

    [Fact]
    public void AFolderName_CannotHaveBrackets_AndBookmarksFromBefore_AreRead()
    {
        Assert.Equal(-1, Bookmarks.AddFolder("[x]", 0));
        Assert.Equal(-1, Bookmarks.AddFolder("  ", 0));
        // A file written before there were colors and folders
        Bookmarks.Parse("file\t" + In("a.md") + "\npage\thelp\tHelp me\n");
        Assert.Equal(new[] { "a.md", "Help me" }, Bookmarks.Items.Select(entry => entry.Shown));
        Assert.All(Bookmarks.Items, entry => Assert.Null(entry.Color));
        // More than there could be before (100) are kept, also when read from the file
        Bookmarks.Clear();
        for (var i = 0; i < 150; i++) Bookmarks.Add(FileAt(i + ".md"));
        Bookmarks.Save();
        Bookmarks.Load();
        Assert.Equal(150, Bookmarks.Items.Count);
    }

    [Fact]
    public void ARenamedOrMovedFileOrDirectory_IsFollowed()
    {
        Bookmarks.Add(new JumpTarget(JumpKind.Directory, In("work")));
        Bookmarks.Add(FileAt(Path.Combine("work", "notes.md")));
        Bookmarks.Add(new JumpTarget(JumpKind.Page, "help"));
        Assert.True(Bookmarks.PathChanged(In("work"), In("job")));
        Assert.Equal(In("job"), Bookmarks.Items[0].Target.Target);
        Assert.Equal(In(Path.Combine("job", "notes.md")), Bookmarks.Items[1].Target.Target);
        Assert.Equal("help", Bookmarks.Items[2].Target.Target);
    }

    [Fact]
    public void AddWithMessage_SavesAndTells()
    {
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "a.md"), Bookmarks.AddWithMessage(FileAt("a.md")));
        Assert.Equal(Lang.F("Already bookmarked ({0}): {1}", 1, "a.md"), Bookmarks.AddWithMessage(FileAt("a.md")));
        Assert.Contains("a.md", File.ReadAllText(Bookmarks.FilePath));
        // The number is the one ":book" takes
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 2, "b.md"), Bookmarks.AddWithMessage(FileAt("b.md")));
    }

    [Fact]
    public void ABookmarkMadeInTheEditor_KeepsItsLineAndColumn()
    {
        var chapter = FileAt("chapter.md");
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "chapter.md:120"),
            Bookmarks.AddWithMessage(chapter with { At = (120, 5) }));
        // Another line of the same file, and the file itself, are other bookmarks; the same line is not
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 2, "chapter.md:3"),
            Bookmarks.AddWithMessage(chapter with { At = (3, 1) }));
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 3, "chapter.md"), Bookmarks.AddWithMessage(chapter));
        Assert.Equal(Lang.F("Already bookmarked ({0}): {1}", 1, "chapter.md:120"),
            Bookmarks.AddWithMessage(chapter with { At = (120, 9) }));

        // Kept in the file after the name and color, and read again
        var text = File.ReadAllText(Bookmarks.FilePath);
        Assert.Contains("file\t" + In("chapter.md") + "\t\t\t120:5\n", text);
        Bookmarks.Load();
        Assert.Equal(3, Bookmarks.Places.Count);
        Assert.Equal((120, 5), Bookmarks.Places[0].Target.At);
        Assert.Null(Bookmarks.Places[2].Target.At);
        // With a name and a color
        Assert.True(Bookmarks.Rename(0, "Chapter 3"));
        Bookmarks.Parse(Bookmarks.Serialize());
        Assert.Equal("Chapter 3", Bookmarks.Places[0].Shown);
        Assert.Equal((120, 5), Bookmarks.Places[0].Target.At);
        // A place that cannot be read is a bookmark of the file
        Bookmarks.Parse("file\t" + In("x.md") + "\t\t\tnone\n");
        Assert.Null(Bookmarks.Places[0].Target.At);
    }

    [Fact]
    public void ThePeekOfABookmark_StartsAtItsLine()
    {
        var path = In("long.md");
        File.WriteAllText(path, string.Join("\n", Enumerable.Range(1, 50).Select(i => "line " + i)));
        Assert.Equal(new[] { "line 20", "line 21", "line 22" }, NotePanes.PeekPlace(path, 3, 20));
        Assert.Equal(new[] { "line 1", "line 2" }, NotePanes.PeekPlace(path, 2));
        // Past the end: the last lines
        Assert.Equal(new[] { "line 49", "line 50" }, NotePanes.PeekPlace(path, 2, 80));
    }

    [Fact]
    public void BookInTheEditor_AsksForTheJump_WithTheNumber()
    {
        var editor = new EditorEngine("text", 60, 10, false);
        Assert.Equal(EditorRequest.JumpToBookmark, editor.Type(":book 3<CR>"));
        Assert.Equal("3", editor.BookArgument);
        // Alone: the Bookmarks tab of the Jump menu
        Assert.Equal(EditorRequest.JumpToBookmark, editor.Type(":book<CR>"));
        Assert.Equal(string.Empty, editor.BookArgument);
        // ":bookmark" is another command
        Assert.Equal(EditorRequest.Bookmark, editor.Type(":bookmark<CR>"));
    }

    [Fact]
    public void TheJumpMenu_HasTabsWithTheListsOfTheFileBrowserAsSubTabs()
    {
        var tabs = JumpMenu.Tabs();
        Assert.Equal(new[]
        {
            JumpMenu.Tab.Bookmarks, JumpMenu.Tab.Jumps, JumpMenu.Tab.Projects, JumpMenu.Tab.History,
            JumpMenu.Tab.Favorites, JumpMenu.Tab.Hotlist, JumpMenu.Tab.Configuration
        }, tabs);
        Assert.Equal(new[] { JumpMenu.Group.FileBrowser },
            new[] { JumpMenu.Tab.History, JumpMenu.Tab.Favorites, JumpMenu.Tab.Hotlist }.Select(JumpMenu.GroupOf).Distinct());
        // Tab: from a list of the file browser to the next tab (Blyant), Shift+Tab: to the first list of the tab
        // before (Projects); from the last tab round to the first. Projects is after Recent
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.Configuration), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Favorites), false));
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.Projects), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Hotlist), true));
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.Projects), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Jumps), false));
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.History), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Projects), false));
        Assert.Equal(0, JumpMenu.NextGroup(tabs, tabs.Count - 1, false));
    }
}
