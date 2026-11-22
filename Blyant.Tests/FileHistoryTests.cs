namespace Blyant.Tests;

[Collection("Static state")]
public class FileHistoryTests : IDisposable
{
    private readonly string _root;
    private readonly string? _savedPath = FileHistory.FilePathOverride;

    public FileHistoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "blyant_history_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        FileHistory.FilePathOverride = Path.Combine(_root, FileHistory.FileName);
        FileHistory.Clear();
    }

    public void Dispose()
    {
        FileHistory.FilePathOverride = _savedPath;
        FileHistory.Clear();
        Directory.Delete(_root, true);
    }

    private string In(string name) => Path.Combine(_root, name);

    private static string Names() =>
        string.Join(" ", FileHistory.Items.Select(entry => Path.GetFileName(entry.Path)));

    [Fact]
    public void SeveralFiles_AreTakenOutAtOnce()
    {
        foreach (var name in new[] { "a", "b", "c", "d", "e" }) FileHistory.Add(In(name + ".md"), DateTime.UtcNow);
        Assert.Equal("e.md d.md c.md b.md a.md", Names());
        // (the places in any order, once each, and those that are not in the list left out)
        Assert.Equal(3, FileHistory.RemoveMany([3, 0, 1, 3, 7]));
        Assert.Equal("c.md a.md", Names());
    }

    [Fact]
    public void ThePlaceOfTheCursor_IsALineAndColumn()
    {
        const string text = "one\ntwo three\n\nlast";
        Assert.Equal((1, 1), CaretPosition.Of(text, 0));
        Assert.Equal((2, 5), CaretPosition.Of(text, 8));
        Assert.Equal((3, 1), CaretPosition.Of(text, 14));
        Assert.Equal((4, 5), CaretPosition.Of(text, text.Length));
        foreach (var index in new[] { 0, 3, 4, 8, 13, 14, 15, text.Length })
        {
            var (line, column) = CaretPosition.Of(text, index);
            Assert.Equal(index, CaretPosition.IndexOf(text, line, column));
        }

        // A text that has changed since: the end of the line, or of the text
        Assert.Equal(3, CaretPosition.IndexOf(text, 1, 40));
        Assert.Equal(text.Length, CaretPosition.IndexOf(text, 99, 1));
        Assert.Equal(0, CaretPosition.IndexOf(string.Empty, 5, 5));

        // As written in the lists
        Assert.Equal("12:5\t", CaretPosition.Write(12, 5));
        Assert.Equal(string.Empty, CaretPosition.Write(0, 0));
        var written = "12:5\tC:\\notes\\a.md";
        Assert.Equal((12, 5, ""), CaretPosition.Take(ref written));
        Assert.Equal("C:\\notes\\a.md", written);
        var path = "/home/notes/1:2 a.md";
        Assert.Equal((0, 0, ""), CaretPosition.Take(ref path));
        Assert.Equal("/home/notes/1:2 a.md", path);
    }

    [Fact]
    public void ASplit_IsKeptAfterTheLineAndColumn_AndALineWithoutItIsReadAsBefore()
    {
        // The other pane after "|" ("<": at the left)
        Assert.Equal("12:5|40:1\t", CaretPosition.Write(12, 5, CaretPosition.SplitText(40, 1, false)));
        Assert.Equal("|<40:1", CaretPosition.SplitText(40, 1, true));
        var written = "12:5|<40:1\tC:\\notes\\a.md";
        Assert.Equal((12, 5, "|<40:1"), CaretPosition.Take(ref written));
        Assert.Equal("C:\\notes\\a.md", written);
        written = "12:5\tC:\\notes\\a.md";
        Assert.Equal((12, 5, ""), CaretPosition.Take(ref written));
        // Something else after the line and column: not a caret (the line is a path, as before)
        var path = "12:5|x\ta.md";
        Assert.Equal((0, 0, ""), CaretPosition.Take(ref path));

        const string text = "one\ntwo\nthree";
        Assert.Equal((8, true), CaretPosition.SplitOf("|<3:1", text));
        Assert.Equal((5, false), CaretPosition.SplitOf("|2:2", text));
        // A line past the end: the end of the text (it may have changed since)
        Assert.Equal((text.Length, false), CaretPosition.SplitOf("|99:1", text));
        Assert.Null(CaretPosition.SplitOf("", text));
        Assert.Null(CaretPosition.SplitOf("|x", text));

        var time = new DateTime(2026, 10, 18, 9, 30, 0);
        FileHistory.Add(In("a.txt"), time);
        Assert.True(FileHistory.SetCaret(In("a.txt"), 12, 5, "|<40:1"));
        FileHistory.Parse(FileHistory.Serialize());
        Assert.Equal((12, 5, "|<40:1"), FileHistory.CaretOf(In("a.txt")));
        // Closed with one pane: the split is forgotten
        Assert.True(FileHistory.SetCaret(In("a.txt"), 3, 1));
        Assert.Equal((3, 1, ""), FileHistory.CaretOf(In("a.txt")));
    }

    [Fact]
    public void WhereTheCursorWas_IsKeptInTheHistory_AndReadFromAnOlderList()
    {
        var time = new DateTime(2026, 10, 14, 9, 30, 0);
        FileHistory.Add(In("a.txt"), time);
        FileHistory.Add(In("b.txt"), time);
        Assert.Null(FileHistory.CaretOf(In("a.txt")));
        Assert.True(FileHistory.SetCaret(In("a.txt"), 12, 5));
        Assert.False(FileHistory.SetCaret(In("other.txt"), 1, 1));
        Assert.Equal((12, 5, ""), FileHistory.CaretOf(In("a.txt")));
        // Opened again: moved to the top, with the place kept
        FileHistory.Add(In("a.txt"), time.AddMinutes(1));
        Assert.Equal((12, 5, ""), FileHistory.CaretOf(In("a.txt")));

        var text = FileHistory.Serialize();
        Assert.Contains(Stored(time.AddMinutes(1)) + "\t12:5\t" + In("a.txt") + "\n", text);
        Assert.Contains(Stored(time) + "\t" + In("b.txt") + "\n", text);
        FileHistory.Parse(text);
        Assert.Equal((12, 5, ""), FileHistory.CaretOf(In("a.txt")));
        Assert.Null(FileHistory.CaretOf(In("b.txt")));
        Assert.Equal(new[] { "a.txt", "b.txt" }, FileHistory.Items.Select(entry => Path.GetFileName(entry.Path)));
    }

    [Fact]
    public void Add_PutsTheFileFirst_AndMovesAFileOpenedAgain()
    {
        var time = new DateTime(2026, 10, 10, 20, 45, 0);
        FileHistory.Add(In("a.txt"), time);
        FileHistory.Add(In("b.txt"), time.AddMinutes(1));
        FileHistory.Add(In("c.txt"), time.AddMinutes(2));
        Assert.Equal("c.txt b.txt a.txt", Names());
        FileHistory.Add(In("a.txt"), time.AddMinutes(3));
        Assert.Equal("a.txt c.txt b.txt", Names());
        Assert.Equal(time.AddMinutes(3), FileHistory.Items[0].Opened);
    }

    [Fact]
    public void Add_DropsTheOldest_WhenTheListIsFull()
    {
        for (var i = 0; i < FileHistory.MaxCount + 5; i++)
        {
            FileHistory.Add(In(i + ".txt"), DateTime.UtcNow);
        }

        Assert.Equal(FileHistory.MaxCount, FileHistory.Items.Count);
        Assert.Equal((FileHistory.MaxCount + 4) + ".txt", Path.GetFileName(FileHistory.Items[0].Path));
        Assert.Equal("5.txt", Path.GetFileName(FileHistory.Items[^1].Path));
    }

    // A time as the history keeps it: in UTC, with Z after it
    private static string Stored(DateTime time) => IsoDateTime.Stored(time, "yyyy-MM-dd HH:mm");

    [Fact]
    public void TheList_IsSavedAndReadAgain()
    {
        var time = new DateTime(2026, 10, 10, 20, 45, 0);
        FileHistory.Add(In("a.txt"), time);
        FileHistory.Add(In("my notes.md"), time.AddDays(1));
        Assert.Equal(DialogText.Success, FileHistory.Save());
        FileHistory.Clear();
        FileHistory.Load();
        Assert.Equal("my notes.md a.txt", Names());
        // (kept in UTC, read back as UTC: the same time in local time)
        Assert.Equal(time, IsoDateTime.Local(FileHistory.Items[1].Opened));
        Assert.Equal(DateTimeKind.Utc, FileHistory.Items[1].Opened.Kind);
        Assert.Equal(Stored(time.AddDays(1)) + "\t" + In("my notes.md") + "\n" + Stored(time) + "\t" + In("a.txt") + "\n",
            FileHistory.Serialize());
    }

    [Fact]
    public void Parse_SkipsLinesThatAreNotFiles_AndKeepsAFileWithoutATime()
    {
        FileHistory.Parse("no tab\n\t\nnot a time\t" + In("a.txt") + "\r\n2026-10-10 20:45\t" + In("a.txt") + "\n");
        Assert.Equal("a.txt", Names());
        Assert.Equal(DateTime.MinValue, FileHistory.Items[0].Opened);
    }

    [Fact]
    public void RemoveAndClear()
    {
        FileHistory.Add(In("a.txt"), DateTime.UtcNow);
        FileHistory.Add(In("b.txt"), DateTime.UtcNow);
        FileHistory.RemoveAt(0);
        FileHistory.RemoveAt(5);
        Assert.Equal("a.txt", Names());
        FileHistory.Clear();
        Assert.Empty(FileHistory.Items);
    }

    [Fact]
    public void Times_AreKeptInUtc_AndShownInLocalTime_AndOldLocalTimesAreRead()
    {
        var utc = new DateTime(2026, 11, 22, 9, 5, 0, DateTimeKind.Utc);
        // Shown: a UTC time in local time; a local time, or one of no kind, as it is
        Assert.Equal(utc.ToLocalTime(), IsoDateTime.Local(utc));
        Assert.Equal(DateTimeKind.Local, IsoDateTime.Local(utc).Kind);
        var plain = new DateTime(2026, 11, 22, 10, 0, 0);
        Assert.Equal(plain, IsoDateTime.Local(plain));
        Assert.Equal(utc.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), IsoDateTime.Local(utc, "HH:mm"));
        // Kept: in UTC with Z, and read back as UTC
        Assert.Equal("2026-11-22 09:05Z", IsoDateTime.Stored(utc, "yyyy-MM-dd HH:mm"));
        Assert.True(IsoDateTime.TryParseStored("2026-11-22 09:05Z", "yyyy-MM-dd HH:mm", out var read));
        Assert.Equal(utc, read);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
        // A time written before 1.6.2, in local time without Z: read as that local time, kept as UTC
        Assert.True(IsoDateTime.TryParseStored("2026-11-22 10:00", "yyyy-MM-dd HH:mm", out var old));
        Assert.Equal(DateTimeKind.Utc, old.Kind);
        Assert.Equal(plain, IsoDateTime.Local(old));
        Assert.False(IsoDateTime.TryParseStored("not a time", "yyyy-MM-dd HH:mm", out _));
        Assert.False(IsoDateTime.TryParseStored("Z", "yyyy-MM-dd HH:mm", out _));
        // The time now for :ts and $now: local time, as a person reads it
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", IsoDateTime.GetDateTimeNow());
    }
}
