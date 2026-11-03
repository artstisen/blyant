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
        Corkboard.UseBoard(string.Empty);
    }

    public void Dispose()
    {
        Corkboard.UseBoard(string.Empty);
        Corkboard.FilePathOverride = null;
        Corkboard.Parse(string.Empty);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.Combine(_directory, name);

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
        Assert.True(Corkboard.ChangeCard(string.Empty, note.Number, card =>
        {
            card.Text = "Second";
            return true;
        }));
        Assert.Empty(Corkboard.Cards);
        Corkboard.UseBoard(string.Empty);
        Assert.Equal("Second", Corkboard.Numbered(note.Number)!.Text);
        // The open board, read again since (a card object of before is not the one changed)
        Corkboard.Load();
        Assert.True(Corkboard.ChangeCard(string.Empty, note.Number, card =>
        {
            card.Text = "Third";
            return true;
        }));
        Corkboard.Load();
        Assert.Equal("Third", Corkboard.Numbered(note.Number)!.Text);
        // A card that is not there, or not changed
        Assert.False(Corkboard.ChangeCard(string.Empty, 9, _ => true));
        Assert.False(Corkboard.ChangeCard(string.Empty, note.Number, _ => false));
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
        // (made beside it, as by another program: Blyant takes it as the same name)
        File.WriteAllText(Corkboard.Boards.PathOf("plot"), string.Empty);
        Assert.Equal("plot", Corkboard.Boards.Find("plot"));
        Assert.Equal("Plot", Corkboard.Boards.Find("Plot"));
        Assert.Contains(Corkboard.Boards.Find("PLOT"), new[] { "Plot", "plot" });
    }
}
