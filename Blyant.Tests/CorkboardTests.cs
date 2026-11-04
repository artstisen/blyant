namespace Blyant.Tests;

/// <summary>
/// The corkboard: files pinned as cards with numbers that stay, placed freely, drawn in their order (the last on top)
/// </summary>
[Collection("Static state")]
public class CorkboardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-board-" + Guid.NewGuid().ToString("N"));
    private const int Width = 78;
    private const int Height = 19;

    public CorkboardTests()
    {
        Directory.CreateDirectory(_directory);
        Corkboard.FilePathOverride = Path.Combine(_directory, Corkboard.FileName);
        Projects.ListPathOverride = Path.Combine(_directory, Projects.ListFileName);
        Corkboard.UseBoard(string.Empty);
    }

    public void Dispose()
    {
        Corkboard.UseBoard(string.Empty);
        Corkboard.FilePathOverride = null;
        Projects.ListPathOverride = null;
        Corkboard.Parse(string.Empty);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(params string[] parts) => Path.Combine(new[] { _directory }.Concat(parts).ToArray());

    private static string Order() => string.Join(" ", Corkboard.Cards.Select(card => card.Number));

    [Fact]
    public void NewCards_GetTheLowestFreeNumber_AndAFreePlace()
    {
        var one = Corkboard.Add(In("one.md"), Width, Height);
        var two = Corkboard.Add(In("two.md"), Width, Height);
        var three = Corkboard.Add(In("three.md"), Width, Height);
        Assert.Equal((1, 0, 0), (one.Number, one.X, one.Y));
        // Side by side, with a column between them
        Assert.Equal((2, Corkboard.CardWidth + 1, 0), (two.Number, two.X, two.Y));
        Assert.Equal(2 * (Corkboard.CardWidth + 1), three.X);
        // The row is full: the next row
        var four = Corkboard.Add(In("four.md"), Width, Height);
        Assert.Equal((0, Corkboard.CardHeight), (four.X, four.Y));
        // A number that is free again is used
        Corkboard.Remove(two);
        Assert.Equal(2, Corkboard.Add(In("five.md"), Width, Height).Number);
        Assert.Equal("one", one.Name);
    }

    [Fact]
    public void ABoardThatIsFull_GetsTheCardOnTop_AStepAside()
    {
        for (var i = 0; i < 18; i++) Corkboard.Add(In(i + ".md"), Width, Height);
        var last = Corkboard.Cards[^1];
        var extra = Corkboard.Add(In("extra.md"), Width, Height);
        Assert.Equal((last.X + 2, last.Y + 1), (extra.X, extra.Y));
    }

    [Fact]
    public void TheOrder_IsChangedByTopAndBottom_TheNumbersStay()
    {
        var one = Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Add(In("two.md"), Width, Height);
        var three = Corkboard.Add(In("three.md"), Width, Height);
        Corkboard.ToFront(one, true);
        Assert.Equal("2 3 1", Order());
        Corkboard.ToFront(three, false);
        Assert.Equal("3 2 1", Order());
        Assert.Equal(new[] { 1, 2, 3 }, Corkboard.Cards.Select(card => card.Number).Order());
    }

    [Fact]
    public void ACard_IsMovedInsideTheBoard()
    {
        var card = Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Move(card, 5, 2, Width, Height);
        Assert.Equal((5, 2), (card.X, card.Y));
        Corkboard.Move(card, -100, 100, Width, Height);
        Assert.Equal((0, Height - Corkboard.CardHeight), (card.X, card.Y));
        // A card placed on a larger window is shown at the edge of a smaller one, and keeps its place
        card.X = 200;
        Assert.Equal(Width - Corkboard.CardWidth, Corkboard.Shown(card, Width, Height).X);
        Assert.Equal(200, card.X);
    }

    [Fact]
    public void ARemovedCard_IsPutBackWhereItWas()
    {
        Corkboard.Add(In("one.md"), Width, Height);
        var two = Corkboard.Add(In("two.md"), Width, Height);
        Corkboard.Add(In("three.md"), Width, Height);
        Corkboard.Remove(two);
        Assert.Equal("1 3", Order());
        Assert.Equal(Corkboard.Undone.Card, Corkboard.Undo());
        Assert.Same(two, Corkboard.UndoneCard);
        Assert.Equal("1 2 3", Order());
        Assert.Equal(Corkboard.Undone.Nothing, Corkboard.Undo());
    }

    [Fact]
    public void TheCards_AreKeptInTheFile()
    {
        var one = Corkboard.Add(In("one.md"), Width, Height);
        one.Color = ConsoleColor.Yellow;
        Corkboard.Rename(one, "Chapter\tone");
        Corkboard.Add(In("two.md"), Width, Height);
        Corkboard.ToFront(one, true);
        Assert.Equal(DialogText.Success, Corkboard.Save());
        Corkboard.Load();
        Assert.Equal("2 1", Order());
        var read = Corkboard.Numbered(1)!;
        Assert.Equal(("Chapter one", ConsoleColor.Yellow, In("one.md")), (read.Name, read.Color!.Value, read.Path));
        // Lines that cannot be read, and a number twice, are left out
        Corkboard.Parse("x\n3\t1,1\t\t\ta.md\n3\t2,2\t\t\tb.md\n4\tno\t\t\tc.md\n");
        Assert.Equal("3", Order());
    }

    [Fact]
    public void ThePinnedFiles_FollowARenameOrAMove()
    {
        var card = Corkboard.Add(In(Path.Combine("book", "ch1.md")), Width, Height);
        Corkboard.Add(In("other.md"), Width, Height);
        Corkboard.Save();
        Corkboard.Follow(In("book"), In("novel"));
        Assert.Equal(In(Path.Combine("novel", "ch1.md")), Corkboard.Numbered(card.Number)!.Path);
        Corkboard.Follow(In("other.md"), In("renamed.md"));
        Assert.Equal(In("renamed.md"), Corkboard.Numbered(2)!.Path);
    }

    [Fact]
    public void PinningAFileTwice_SaysWhereItIs()
    {
        Lang.Set(Lang.English);
        Assert.Equal("Pinned to the corkboard (1): notes", Corkboard.PinWithMessage(In("notes.md"), Height));
        Assert.Equal("Already on the corkboard (1): notes", Corkboard.PinWithMessage(In("notes.md"), Height));
        Corkboard.Load();
        Assert.Single(Corkboard.Cards);
    }

    [Fact]
    public void TheNearestCard_IsFoundThatWay()
    {
        var a = Corkboard.Add(In("a.md"), Width, Height);
        var b = Corkboard.Add(In("b.md"), Width, Height);
        var c = Corkboard.Add(In("c.md"), Width, Height);
        var d = Corkboard.Add(In("d.md"), Width, Height);
        // a b c in a row, d below a
        Assert.Same(b, Corkboard.Nearest(a, 1, 0, Width, Height));
        Assert.Same(c, Corkboard.Nearest(b, 1, 0, Width, Height));
        Assert.Same(d, Corkboard.Nearest(a, 0, 1, Width, Height));
        Assert.Same(a, Corkboard.Nearest(d, 0, -1, Width, Height));
        Assert.Null(Corkboard.Nearest(a, -1, 0, Width, Height));
        Assert.Null(Corkboard.Nearest(c, 1, 0, Width, Height));
    }

    [Theory]
    [InlineData('l', ConsoleKey.L, false, 1, 0)]
    [InlineData('h', ConsoleKey.H, false, -1, 0)]
    [InlineData('j', ConsoleKey.J, false, 0, 1)]
    [InlineData('k', ConsoleKey.K, false, 0, -1)]
    // H J K L, with Caps Lock (no Shift) or with Shift: five steps
    [InlineData('L', ConsoleKey.L, false, 5, 0)]
    [InlineData('H', ConsoleKey.H, true, -5, 0)]
    [InlineData('J', ConsoleKey.J, false, 0, 5)]
    [InlineData('K', ConsoleKey.K, true, 0, -5)]
    [InlineData('\0', ConsoleKey.RightArrow, false, 1, 0)]
    [InlineData('\0', ConsoleKey.UpArrow, true, 0, -5)]
    public void TheKeysOfMoving_AreOneStep_OrFiveWithCapitals(char c, ConsoleKey key, bool shift, int columns, int rows)
    {
        Assert.Equal((columns, rows), CorkboardPage.MoveOf(new ConsoleKeyInfo(c, key, shift, false, false)));
    }

    [Fact]
    public void OtherKeys_DoNotMoveACard()
    {
        Assert.Null(CorkboardPage.MoveOf(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false)));
        Assert.Null(CorkboardPage.MoveOf(new ConsoleKeyInfo('T', ConsoleKey.T, true, false, false)));
    }

    [Fact]
    public void ANoteCard_KeepsItsText_InTheFile()
    {
        var note = Corkboard.AddNote("Ask about the ending\r\nand the title\n\n", Width, Height);
        Corkboard.Add(In("one.md"), Width, Height);
        Assert.True(note.IsNote);
        Assert.Equal("Ask about the ending", note.Name);
        Assert.Equal(DialogText.Success, Corkboard.Save());
        Corkboard.Load();
        var read = Corkboard.Numbered(1)!;
        Assert.Equal((true, "Ask about the ending\nand the title"), (read.IsNote, read.Text));
        Assert.False(Corkboard.Numbered(2)!.IsNote);
        // A note is not the card of a file
        Assert.Null(Corkboard.CardOf(string.Empty));
        // A note without text has a name all the same
        var empty = Corkboard.AddNote("  ", Width, Height);
        Assert.Equal(Lang.T("Note"), empty.Name);
        // A card discarded is not put back
        Assert.True(Corkboard.Discard(empty));
        Assert.Equal(Corkboard.Undone.Nothing, Corkboard.Undo());
        Assert.False(Corkboard.Discard(empty));
    }

    [Fact]
    public void TheBoards_AreFilesOfTheirOwn()
    {
        Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Save();
        Assert.Equal(DialogText.Success, Corkboard.NewBoard("Plot"));
        Assert.NotEqual(DialogText.Success, Corkboard.NewBoard("plot"));
        Assert.NotEqual(DialogText.Success, Corkboard.NewBoard("main"));
        Assert.Equal("Plot", Corkboard.Boards.Find("PLOT"));
        Corkboard.UseBoard("Plot");
        Assert.Empty(Corkboard.Cards);
        Corkboard.Add(In("two.md"), Width, Height);
        Corkboard.AddNote("A note", Width, Height);
        Corkboard.Save();
        Assert.Equal("1  two\n2  A note", Corkboard.BoardPreview("Plot"));
        Corkboard.UseBoard(string.Empty);
        Assert.Equal("1", Order());
        Assert.Equal(In("one.md"), Corkboard.Cards[0].Path);
        Assert.Equal(new[] { "main", "Plot" }, Corkboard.Boards.Words());
        // The open board stays open when it is renamed, and the main board is opened when it is deleted
        Corkboard.UseBoard("Plot");
        Assert.Equal(DialogText.Success, Corkboard.RenameBoard("Plot", "Story"));
        Assert.Equal("Story", Corkboard.CurrentBoard);
        Assert.Equal("1 2", Order());
        var toTrash = FileOperations.DeleteToTrash;
        try
        {
            FileOperations.DeleteToTrash = false;
            Assert.Equal(DialogText.Success, Corkboard.DeleteBoard("Story", out _));
        }
        finally
        {
            FileOperations.DeleteToTrash = toTrash;
        }

        Assert.Equal((string.Empty, "1"), (Corkboard.CurrentBoard, Order()));
        Assert.Null(Corkboard.Boards.Find("Story"));
    }

    [Fact]
    public void ABoardThatIsGone_OpensTheMainBoard()
    {
        Corkboard.NewBoard("Plot");
        Corkboard.UseBoard("Plot");
        File.Delete(Corkboard.Boards.PathOf("Plot"));
        Corkboard.Load();
        Assert.Equal(string.Empty, Corkboard.CurrentBoard);
    }

    [Fact]
    public void TheCards_AreArrangedByNumberNameOrColor_AndPutBack()
    {
        var ten = Corkboard.Add(In("Chapter 10.md"), Width, Height);
        var two = Corkboard.Add(In("chapter 2.md"), Width, Height);
        var appendix = Corkboard.Add(In("Appendix.md"), Width, Height);
        Corkboard.Move(ten, 10, 10, Width, Height);
        var step = Corkboard.CardWidth + 1;
        Assert.True(Corkboard.Arrange("name", Width, Height));
        Assert.Equal((0, step, 2 * step), (appendix.X, two.X, ten.X));
        Assert.Equal(0, ten.Y);
        // The order they are drawn in stays
        Assert.Equal("1 2 3", Order());
        Assert.Equal(Corkboard.Undone.Places, Corkboard.Undo());
        Assert.Equal((10, 10), (ten.X, ten.Y));
        Assert.Equal(Corkboard.Undone.Nothing, Corkboard.Undo());
        // By color: in the order of the colors, the cards without one last
        ten.Color = ConsoleColor.Red;
        appendix.Color = ConsoleColor.Blue;
        Assert.True(Corkboard.Arrange("color", Width, Height));
        Assert.Equal((0, step, 2 * step), (appendix.X, ten.X, two.X));
        Assert.True(Corkboard.Arrange("number", Width, Height));
        Assert.Equal((0, step, 2 * step), (ten.X, two.X, appendix.X));
        Assert.False(Corkboard.Arrange("size", Width, Height));
    }

    [Fact]
    public void MoreCardsThanTheBoardHolds_AreArrangedAStepAside()
    {
        for (var i = 0; i < 19; i++) Corkboard.Add(In(i + ".md"), Width, Height);
        Corkboard.Arrange("number", Width, Height);
        // 3 a row, 6 rows: the 19th starts again at the top left, a step aside
        var last = Corkboard.Numbered(19)!;
        Assert.Equal((2, 1), (last.X, last.Y));
    }

    [Theory]
    [InlineData("Chapter 2", "chapter 10")]
    [InlineData("a", "B")]
    [InlineData("Part 1 scene 9", "Part 1 scene 10")]
    [InlineData("Draft", "Draft 1")]
    public void TheNaturalOrder_HasNumbersByTheirValue(string first, string second)
    {
        Assert.True(Corkboard.NaturalOrder.Instance.Compare(first, second) < 0);
        Assert.True(Corkboard.NaturalOrder.Instance.Compare(second, first) > 0);
    }

    [Fact]
    public void ThePinnedFiles_FollowARenameOnAllTheBoards()
    {
        Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Save();
        Corkboard.NewBoard("Plot");
        Corkboard.UseBoard("Plot");
        Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Save();
        Corkboard.UseBoard(string.Empty);
        Corkboard.Follow(In("one.md"), In("first.md"));
        Assert.Equal(In("first.md"), Corkboard.Cards[0].Path);
        Corkboard.UseBoard("Plot");
        Assert.Equal(In("first.md"), Corkboard.Cards[0].Path);
    }

    [Fact]
    public void ACard_IsChangedOnItsBoard_AlsoWhenAnotherIsOpen()
    {
        var note = Corkboard.AddNote("First", Width, Height);
        Corkboard.Save();
        Corkboard.NewBoard("Plot");
        Corkboard.UseBoard("Plot");
        // The main board, while Plot is open
        Assert.Equal(DialogText.Success, Corkboard.ChangeCard(string.Empty, note.Number, card =>
        {
            card.Text = "Second";
            return true;
        }));
        Assert.Empty(Corkboard.Cards);
        Corkboard.UseBoard(string.Empty);
        Assert.Equal("Second", Corkboard.Numbered(note.Number)!.Text);
        // The open board, read again since (a card object of before is not the one changed)
        Corkboard.Load();
        Assert.Equal(DialogText.Success, Corkboard.ChangeCard(string.Empty, note.Number, card =>
        {
            card.Text = "Third";
            return true;
        }));
        Corkboard.Load();
        Assert.Equal("Third", Corkboard.Numbered(note.Number)!.Text);
        // A card that is not there, or not changed
        Assert.Null(Corkboard.ChangeCard(string.Empty, 9, _ => true));
        Assert.Equal(string.Empty, Corkboard.ChangeCard(string.Empty, note.Number, _ => false));
        Assert.Null(Corkboard.ChangeCard("Plot", 9, _ => true));
    }

    [Fact]
    public void ReadingAgain_KeepsWhatUPutsBack_IfTheFileIsAsItWas()
    {
        Corkboard.Add(In("one.md"), Width, Height);
        var two = Corkboard.Add(In("two.md"), Width, Height);
        Corkboard.Remove(two);
        Corkboard.Save();
        Corkboard.Reload();
        Assert.Equal(Corkboard.Undone.Card, Corkboard.Undo());
        Corkboard.Save();
        // Changed by another page: read again
        File.WriteAllText(Corkboard.FilePath, "1\t0,0\t\t\t" + In("one.md") + "\n");
        Corkboard.Reload();
        Assert.Equal("1", Order());
        Assert.Equal(Corkboard.Undone.Nothing, Corkboard.Undo());
    }

    [Fact]
    public void ATitleAsTheOwnName_IsNoTitle()
    {
        var card = Corkboard.Add(In("chapter.md"), Width, Height);
        Corkboard.Rename(card, "Opening");
        Assert.Equal("Opening", card.Title);
        // Enter on the name as it was in the box: the card goes on following its file
        Corkboard.Rename(card, "chapter");
        Assert.Equal((string.Empty, "chapter"), (card.Title, card.Name));
        Corkboard.Rename(card, "Opening");
        Corkboard.Rename(card, "  ");
        Assert.Equal(string.Empty, card.Title);
        // A tab in a note's first line is blank space on the card
        Assert.Equal("To do", Corkboard.AddNote("To\tdo", Width, Height).Name);
    }

    [Fact]
    public void APathWithATabOrALineBreak_IsNotPinned()
    {
        Assert.True(Corkboard.CanKeep(In("one.md")));
        Assert.False(Corkboard.CanKeep(In("a\tb.md")));
        Assert.Equal(Lang.T("A path with a tab or a line break is not kept"),
            Corkboard.PinWithMessage(In("a\nb.md"), Height));
        Assert.Empty(Corkboard.Cards);
        // A file renamed to such a name: the card stays as it was
        var card = Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Save();
        Corkboard.Follow(In("one.md"), In("a\tb.md"));
        Assert.Equal(In("one.md"), Corkboard.Numbered(card.Number)!.Path);
    }

    [UnixFact]
    public void APathEndingWithBlankSpace_IsKept()
    {
        var path = In("Chapter ");
        File.WriteAllText(path, "text");
        Corkboard.Add(path, Width, Height);
        Corkboard.Save();
        Corkboard.Load();
        Assert.Equal(path, Corkboard.Cards[0].Path);
    }

    [UnixFact]
    public void BoardsWhoseNamesDifferInCase_AreFoundAsWritten()
    {
        Assert.Equal(DialogText.Success, Corkboard.NewBoard("Plot"));
        // (on a file system where upper and lower case are alike, as most on macOS, there is only one)
        if (File.Exists(Corkboard.Boards.PathOf("plot"))) return;
        // (made beside it, as by another program: Blyant takes it as the same name)
        File.WriteAllText(Corkboard.Boards.PathOf("plot"), string.Empty);
        Assert.Equal("plot", Corkboard.Boards.Find("plot"));
        Assert.Equal("Plot", Corkboard.Boards.Find("Plot"));
        Assert.Contains(Corkboard.Boards.Find("PLOT"), new[] { "Plot", "plot" });
    }

    [Fact]
    public void AFolder_IsACard_KeptWithASeparatorAfterIt()
    {
        var folder = In("research");
        Directory.CreateDirectory(folder);
        Assert.StartsWith("Pinned", Corkboard.PinWithMessage(folder, Height));
        var card = Corkboard.CardOf(folder)!;
        Assert.True(card.IsFolder);
        Assert.Equal("research/", card.Name);
        Corkboard.Add(In("notes.md"), Width, Height);
        Corkboard.Save();
        Assert.Contains(folder + Path.DirectorySeparatorChar + "\n", File.ReadAllText(Corkboard.FilePath));
        Corkboard.Load();
        Assert.Equal((true, folder), (Corkboard.Numbered(1)!.IsFolder, Corkboard.Numbered(1)!.Path));
        Assert.False(Corkboard.Numbered(2)!.IsFolder);
        // A folder that is renamed: its card follows, and stays a folder
        Corkboard.Follow(folder, In("sources"));
        Assert.Equal((true, In("sources")), (Corkboard.Numbered(1)!.IsFolder, Corkboard.Numbered(1)!.Path));
    }

    [Fact]
    public void TheTopEdge_TellsANoteAndAFolder()
    {
        var file = Corkboard.Add(In("one.md"), Width, Height);
        var folder = Corkboard.Add(In("research"), Width, Height, true);
        var note = Corkboard.AddNote("Note", Width, Height);
        Assert.Equal(new string('-', 22), CorkboardPage.TopEdge(file, 22));
        Assert.Equal("-///" + new string('-', 18), CorkboardPage.TopEdge(folder, 22));
        Assert.Equal(new string('~', 22), CorkboardPage.TopEdge(note, 22));
    }

    [Fact]
    public void ClearingTheBoard_TakesAllOff_AndUPutsThemBack()
    {
        Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Add(In("two.md"), Width, Height);
        Corkboard.AddNote("Three", Width, Height);
        Assert.Equal(3, Corkboard.Clear());
        Assert.Empty(Corkboard.Cards);
        Assert.Equal(0, Corkboard.Clear());
        Assert.Equal(Corkboard.Undone.Cards, Corkboard.Undo());
        Assert.Equal((3, "1 2 3"), (Corkboard.UndoneCount, Order()));
        // A card added since keeps its number: the one put back that had it gets a new one
        Corkboard.Clear();
        var added = Corkboard.Add(In("new.md"), Width, Height);
        Assert.Equal(1, added.Number);
        Corkboard.Undo();
        Assert.Equal((3, "4 2 3 1"), (Corkboard.UndoneCount, Order()));
        Assert.Same(added, Corkboard.Numbered(1));
        Assert.Equal(In("one.md"), Corkboard.Numbered(4)!.Path);
    }

    [Fact]
    public void TheBoardOfAProject_IsInItsFolder_WithPathsFromIt()
    {
        var project = In("Novel");
        Directory.CreateDirectory(Path.Combine(project, "chapters"));
        Directory.CreateDirectory(Path.Combine(project, "research"));
        File.WriteAllText(Projects.ListPathOverride!, project + "\n");
        Assert.True(Corkboard.IsProjectBoard(project));
        Assert.False(Corkboard.IsProjectBoard("Plot"));
        Assert.Null(Corkboard.Snapshot(project));
        Assert.Empty(Corkboard.ProjectBoards());
        Corkboard.UseBoard(project);
        Corkboard.Add(Path.Combine(project, "chapters", "one.md"), Width, Height);
        Corkboard.Add(Path.Combine(project, "research"), Width, Height, true);
        Corkboard.Add(In("outside.md"), Width, Height);
        Assert.Equal(DialogText.Success, Corkboard.Save());
        var file = Path.Combine(project, Corkboard.ProjectFileName);
        var text = File.ReadAllText(file);
        // What is in the project is kept from its folder, with "/" on all systems, the rest with its full path
        Assert.Contains("\tchapters/one.md\n", text);
        Assert.Contains("\tresearch/\n", text);
        Assert.Contains("\t" + In("outside.md") + "\n", text);
        Assert.Equal(new[] { Path.GetFullPath(project) }, Corkboard.ProjectBoards());
        Assert.Equal("Novel", Corkboard.NameOf(project));
        Assert.Equal(Path.GetFullPath(project), Corkboard.FindByName("novel"));
        Assert.Contains("Novel", Corkboard.BoardWords());
        Assert.Equal(new[] { 1, 2, 3 }, Corkboard.Snapshot(project)!.Select(card => card.Number));
        // The project moved (outside Blyant): its board goes with it
        Corkboard.UseBoard(string.Empty);
        var moved = In("Book");
        Directory.Move(project, moved);
        Corkboard.UseBoard(moved);
        Assert.Equal(Path.Combine(moved, "chapters", "one.md"), Corkboard.Numbered(1)!.Path);
        Assert.Equal((true, Path.Combine(moved, "research")),
            (Corkboard.Numbered(2)!.IsFolder, Corkboard.Numbered(2)!.Path));
        Assert.Equal(In("outside.md"), Corkboard.Numbered(3)!.Path);
    }

    [Fact]
    public void TheOpenBoardOfAProject_FollowsItsFolder()
    {
        var project = In("Novel");
        Directory.CreateDirectory(project);
        File.WriteAllText(Projects.ListPathOverride!, project + "\n");
        Corkboard.UseBoard(project);
        Corkboard.Add(Path.Combine(project, "one.md"), Width, Height);
        Corkboard.Save();
        Directory.Move(project, In("Book"));
        Corkboard.Follow(project, In("Book"));
        Assert.Equal(Path.GetFullPath(In("Book")), Corkboard.CurrentBoard);
        Assert.Equal(Path.Combine(In("Book"), "one.md"), Corkboard.Numbered(1)!.Path);
        Assert.Equal(Path.GetFullPath(In("Book")), Corkboard.FindBoard(In("Book")));
        Assert.Null(Corkboard.FindBoard(project));
    }

    [Fact]
    public void TheBoardOfAProjectThatIsGone_IsNotMadeAgain()
    {
        var project = In("Novel");
        Directory.CreateDirectory(project);
        Corkboard.UseBoard(project);
        Corkboard.Add(In("one.md"), Width, Height);
        Directory.Delete(project);
        Assert.Equal(DialogText.ProjectMissing, Corkboard.Save());
        Assert.False(Directory.Exists(project));
        // Read again: the main board
        Corkboard.Load();
        Assert.Equal(string.Empty, Corkboard.CurrentBoard);
    }

    [Fact]
    public void TheBoardOfAProject_IsDeletedAsAFile()
    {
        var project = In("Novel");
        Directory.CreateDirectory(project);
        Corkboard.UseBoard(project);
        Corkboard.Add(In("one.md"), Width, Height);
        Corkboard.Save();
        var toTrash = FileOperations.DeleteToTrash;
        try
        {
            FileOperations.DeleteToTrash = false;
            Assert.Equal(DialogText.Success, Corkboard.DeleteBoard(Path.GetFullPath(project), out _));
        }
        finally
        {
            FileOperations.DeleteToTrash = toTrash;
        }

        Assert.False(File.Exists(Path.Combine(project, Corkboard.ProjectFileName)));
        Assert.True(Directory.Exists(project));
        Assert.Equal(string.Empty, Corkboard.CurrentBoard);
    }

    [Fact]
    public void AFolderCardOfARoot_IsKeptAsItIs()
    {
        var driveRoot = Path.GetPathRoot(Path.GetFullPath(_directory))!;
        Corkboard.Add(driveRoot, Width, Height, true);
        Corkboard.Add(In("research"), Width, Height, true);
        Corkboard.Save();
        Corkboard.Load();
        Assert.Equal((true, driveRoot, driveRoot), (Corkboard.Numbered(1)!.IsFolder, Corkboard.Numbered(1)!.Path,
            Corkboard.Numbered(1)!.Name));
        Assert.Equal((true, In("research")), (Corkboard.Numbered(2)!.IsFolder, Corkboard.Numbered(2)!.Path));
        Assert.False(Corkboard.Numbered(1)!.IsNote);
        // A project at the root of a drive is named by the root
        Assert.Equal(driveRoot, Corkboard.NameOf(driveRoot));
    }

    [Fact]
    public void AProjectBoard_IsAFullPath_NotAName()
    {
        Assert.True(Corkboard.IsProjectBoard(Path.GetFullPath(In("Novel"))));
        Assert.False(Corkboard.IsProjectBoard("Plot"));
        Assert.False(Corkboard.IsProjectBoard("a" + "\\" + "b"));
        Assert.False(Corkboard.IsProjectBoard(string.Empty));
    }

    [Fact]
    public void ABoardAndAProject_AreFoundByTheirNamesAsWritten_First()
    {
        var project = In("Plot");
        Directory.CreateDirectory(project);
        File.WriteAllText(Projects.ListPathOverride!, project + "\n");
        File.WriteAllText(Path.Combine(project, Corkboard.ProjectFileName), string.Empty);
        Assert.Equal(DialogText.Success, Corkboard.NewBoard("plot"));
        Assert.Equal(Path.GetFullPath(project), Corkboard.FindByName("Plot"));
        Assert.Equal("plot", Corkboard.FindByName("plot"));
        Assert.Equal("plot", Corkboard.FindByName("PLOT"));
        Assert.Equal(string.Empty, Corkboard.FindByName("main"));
        Assert.Null(Corkboard.FindByName("nothing"));
    }

    [Fact]
    public void AFolderRenamed_IsFollowed_OnTheBoardsOfTheProjectsInIt()
    {
        // The project W/P, with a card of W/S/notes.md (outside the project: a full path)
        var project = In("W", "P");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(In("W", "S"));
        File.WriteAllText(Projects.ListPathOverride!, project + "\n");
        Corkboard.UseBoard(project);
        Corkboard.Add(In("W", "S", "notes.md"), Width, Height);
        Corkboard.Add(Path.Combine(project, "one.md"), Width, Height);
        Corkboard.Save();
        Corkboard.UseBoard(string.Empty);
        // W renamed: the list of projects has not followed yet
        Directory.Move(In("W"), In("W2"));
        Corkboard.Follow(In("W"), In("W2"));
        Corkboard.UseBoard(In("W2", "P"));
        Assert.Equal(In("W2", "S", "notes.md"), Corkboard.Numbered(1)!.Path);
        Assert.Equal(In("W2", "P", "one.md"), Corkboard.Numbered(2)!.Path);
    }

    [Fact]
    public void TheBoardOfAProjectWithoutAFile_IsDeletedAsNothing()
    {
        var project = Path.GetFullPath(In("Novel"));
        Directory.CreateDirectory(project);
        Corkboard.UseBoard(project);
        Assert.Equal(DialogText.Success, Corkboard.DeleteBoard(project, out var trashed));
        Assert.Equal((string.Empty, string.Empty), (trashed, Corkboard.CurrentBoard));
    }

    [UnixFact]
    public void ANameWithABackslash_InAProject_IsKept()
    {
        var project = In("Novel");
        Directory.CreateDirectory(project);
        var file = Path.Combine(project, "a" + "\\" + "b.md");
        File.WriteAllText(file, "text");
        Corkboard.UseBoard(project);
        Corkboard.Add(file, Width, Height);
        Corkboard.Save();
        Corkboard.Load();
        Assert.Equal(file, Corkboard.Numbered(1)!.Path);
        // Read again unchanged: what u puts back stays
        Corkboard.Remove(Corkboard.Numbered(1)!);
        Corkboard.Save();
        Corkboard.Reload();
        Assert.Equal(Corkboard.Undone.Card, Corkboard.Undo());
    }

    [Fact]
    public void TheKeysGoAlongARow_BeforeAnotherRow()
    {
        // Two rows laid a little apart: the second starts two columns in and a row lower than the first would
        var a = Corkboard.Add(In("a.md"), Width, Height);
        var below = Corkboard.Add(In("below.md"), Width, Height);
        var right = Corkboard.Add(In("right.md"), Width, Height);
        var last = Corkboard.Add(In("last.md"), Width, Height);
        Corkboard.Move(a, 0, 0, Width, Height);
        below.X = 2;
        below.Y = 3;
        right.X = 25;
        right.Y = 1;
        last.X = 30;
        last.Y = 4;
        // l goes along the row (right, a row lower but beside a), not to the card below
        Assert.Same(right, Corkboard.Nearest(a, 1, 0, Width, Height));
        Assert.Same(a, Corkboard.Nearest(right, -1, 0, Width, Height));
        // At the end of a row: the nearest that way in another
        Assert.Same(last, Corkboard.Nearest(right, 1, 0, Width, Height));
        Assert.Same(below, Corkboard.Nearest(last, -1, 0, Width, Height));
        // j goes down the column (below, beside a), not to a card to the side
        Assert.Same(below, Corkboard.Nearest(a, 0, 1, Width, Height));
        Assert.Same(last, Corkboard.Nearest(right, 0, 1, Width, Height));
        Assert.Same(a, Corkboard.Nearest(below, 0, -1, Width, Height));
    }

    [Fact]
    public void OfTwoInTheRowAsNear_TheOneLessToTheSide_IsTaken()
    {
        var a = Corkboard.Add(In("a.md"), Width, Height);
        var lower = Corkboard.Add(In("lower.md"), Width, Height);
        var level = Corkboard.Add(In("level.md"), Width, Height);
        a.X = 0;
        a.Y = 2;
        lower.X = 25;
        lower.Y = 4;
        level.X = 25;
        level.Y = 2;
        Assert.Same(level, Corkboard.Nearest(a, 1, 0, Width, Height));
    }

    [Theory]
    [InlineData(12, "....:....1..")]
    [InlineData(0, "")]
    public void TheRuler_HasADotForEachColumn_AndTheTens(int width, string ruler)
    {
        Assert.Equal(ruler, Tools.Ruler(width));
        // After 90, the tens go on from 0
        Assert.Equal('0', Tools.Ruler(100)[99]);
        Assert.Equal('9', Tools.Ruler(100)[89]);
    }

    [Fact]
    public void ABadge_IsANote_KeptWithItsWord_WithoutATitle()
    {
        var badge = Corkboard.AddBadge("Chapters to rewrite\nfirst", Width, Height);
        var empty = Corkboard.AddBadge(string.Empty, Width, Height);
        Assert.True(badge.IsBadge && badge.IsNote);
        Assert.Equal(Corkboard.BadgeWidth, badge.Width);
        Assert.Equal(Lang.T("Badge"), empty.Name);
        // A badge has no title
        Corkboard.Rename(badge, "Rewrite");
        Assert.Equal(string.Empty, badge.Title);
        badge.Color = ConsoleColor.Red;
        Assert.Equal(DialogText.Success, Corkboard.Save());
        var text = File.ReadAllText(Corkboard.FilePath);
        Assert.StartsWith("1\t0,0\tRed\t\t\tbadge\n", text);
        Corkboard.Load();
        var read = Corkboard.Numbered(1)!;
        Assert.Equal((true, "Chapters to rewrite\nfirst", ConsoleColor.Red), (read.IsBadge, read.Text, read.Color));
        // An empty badge is kept
        Assert.True(Corkboard.Numbered(2)!.IsBadge);
        // A title written in the file by hand is left out, and the word is only of a note
        Corkboard.Parse("1\t0,0\t\tHand\t\tBADGE\n2\t0,3\t\t\t" + In("one.md") + "\tbadge\n3\t0,6\t\t\t\n");
        Assert.Equal((true, string.Empty), (Corkboard.Numbered(1)!.IsBadge, Corkboard.Numbered(1)!.Title));
        Assert.False(Corkboard.Numbered(2)!.IsBadge);
        Assert.False(Corkboard.Numbered(3)!.IsBadge);
    }

    [Fact]
    public void ABadge_IsPlacedBySizeOfItsOwn()
    {
        var card = Corkboard.Add(In("one.md"), Width, Height);
        var badge = Corkboard.AddBadge(string.Empty, Width, Height);
        // Beside the card, with a column between
        Assert.Equal((Corkboard.CardWidth + 1, 0), (badge.X, badge.Y));
        // It goes nearer the right side than a card can
        Corkboard.Move(badge, Width, 0, Width, Height);
        Assert.Equal(Width - Corkboard.BadgeWidth, badge.X);
        Corkboard.Move(card, Width, 0, Width, Height);
        Assert.Equal(Width - Corkboard.CardWidth, card.X);
        badge.X = Width + 10;
        Assert.Equal(Width - Corkboard.BadgeWidth, Corkboard.Shown(badge, Width, Height).X);
    }

    [Fact]
    public void Arranging_LeavesTheBadges_AndTheSnapshotHasOnlyCards()
    {
        var badge = Corkboard.AddBadge(string.Empty, Width, Height);
        var one = Corkboard.Add(In("one.md"), Width, Height);
        var two = Corkboard.Add(In("two.md"), Width, Height);
        Corkboard.Move(badge, 40, 10, Width, Height);
        var place = (badge.X, badge.Y);
        Assert.True(Corkboard.Arrange("number", Width, Height));
        Assert.Equal(place, (badge.X, badge.Y));
        Assert.Equal((0, 0, Corkboard.CardWidth + 1), (one.X, one.Y, two.X));
        Assert.Equal(Corkboard.Undone.Places, Corkboard.Undo());
        Assert.Equal(place, (badge.X, badge.Y));
        Assert.Equal(DialogText.Success, Corkboard.Save());
        Assert.Equal(new[] { 2, 3 }, Corkboard.Snapshot(string.Empty)!.Select(card => card.Number));
    }

    [Fact]
    public void ABadge_IsInTheRowAndColumn_OfTheCards()
    {
        var a = Corkboard.Add(In("a.md"), Width, Height);
        var badge = Corkboard.AddBadge(string.Empty, Width, Height);
        var b = Corkboard.Add(In("b.md"), Width, Height);
        var under = Corkboard.AddBadge(string.Empty, Width, Height);
        a.X = 0;
        a.Y = 0;
        badge.X = 26;
        badge.Y = 0;
        b.X = 35;
        b.Y = 0;
        // Under the middle of a: in its column (half the badge's width is enough)
        under.X = 10;
        under.Y = 4;
        Assert.Same(badge, Corkboard.Nearest(a, 1, 0, Width, Height));
        Assert.Same(b, Corkboard.Nearest(badge, 1, 0, Width, Height));
        Assert.Same(under, Corkboard.Nearest(a, 0, 1, Width, Height));
        Assert.Same(a, Corkboard.Nearest(under, 0, -1, Width, Height));
    }

    [Theory]
    [InlineData(3, "-3-")]
    [InlineData(12, "12-")]
    [InlineData(123, "123")]
    [InlineData(1234, "234")]
    public void TheNumberOfABadge_IsOnItsTopEdge(int number, string edge)
    {
        Assert.Equal(edge, CorkboardPage.BadgeEdge(number));
    }

    [Fact]
    public void CardsMovedTogether_KeepTheirPlaces_AndStopTogetherAtTheEdge()
    {
        var a = Corkboard.Add(In("a.md"), Width, Height);
        var b = Corkboard.Add(In("b.md"), Width, Height);
        var badge = Corkboard.AddBadge(string.Empty, Width, Height);
        Corkboard.Add(In("other.md"), Width, Height);
        (a.X, a.Y, b.X, b.Y, badge.X, badge.Y) = (10, 2, 40, 5, 30, 2);
        var group = new[] { a, b, badge };
        Corkboard.Move(group, 5, 1, Width, Height);
        Assert.Equal((15, 3, 45, 6, 35, 3), (a.X, a.Y, b.X, b.Y, badge.X, badge.Y));
        // b reaches the right side first: all stop there, as far apart as they were
        Corkboard.Move(group, 50, 0, Width, Height);
        Assert.Equal(Width - Corkboard.CardWidth, b.X);
        Assert.Equal((b.X - 30, b.X - 10), (a.X, badge.X));
        // a reaches the top first
        Corkboard.Move(group, 0, -10, Width, Height);
        Assert.Equal((0, 3, 0), (a.Y, b.Y, badge.Y));
    }

    [Fact]
    public void CardsPutOnTopTogether_KeepTheirOrder()
    {
        for (var i = 1; i <= 4; i++) Corkboard.Add(In(i + ".md"), Width, Height);
        var three = Corkboard.Numbered(3)!;
        var one = Corkboard.Numbered(1)!;
        Corkboard.ToFront(new[] { three, one }, true);
        Assert.Equal("2 4 1 3", Order());
        Corkboard.ToFront(new[] { Corkboard.Numbered(4)!, three }, false);
        Assert.Equal("4 3 2 1", Order());
    }

    [Fact]
    public void CardsRemovedTogether_ArePutBackTogether_WhereTheyWere()
    {
        for (var i = 1; i <= 4; i++) Corkboard.Add(In(i + ".md"), Width, Height);
        Assert.Equal(2, Corkboard.Remove(new[] { Corkboard.Numbered(2)!, Corkboard.Numbered(4)! }));
        Assert.Equal("1 3", Order());
        Assert.Equal(Corkboard.Undone.Cards, Corkboard.Undo());
        Assert.Equal(2, Corkboard.UndoneCount);
        Assert.Equal("1 2 3 4", Order());
        // A card added since keeps its number: the one put back gets a new one
        Corkboard.Remove(new[] { Corkboard.Numbered(2)!, Corkboard.Numbered(3)! });
        var added = Corkboard.Add(In("new.md"), Width, Height);
        Assert.Equal(2, added.Number);
        Assert.Equal(Corkboard.Undone.Cards, Corkboard.Undo());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Corkboard.Cards.Select(card => card.Number).Order());
        Assert.Same(added, Corkboard.Numbered(2));
    }
}
