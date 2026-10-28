namespace Blyant.Tests;

/// <summary>
/// Limits from a code review: the layout follows an edit without moving all the lines after it, ":dup" puts in no
/// more than "p" does, a ":s" that takes too long changes nothing, a very long yank is not sent to the terminal,
/// and a file that may not be written is not replaced (Linux, macOS)
/// </summary>
[Collection("Static state")]
public class LimitTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-limits-" + Guid.NewGuid().ToString("N"));
    private readonly string _savedFile = SystemClipboard.PasteBinFile;
    private readonly bool _savedOsc52 = SystemClipboard.Osc52;
    private readonly bool _savedPasteBin = SystemClipboard.PasteBin;
    private readonly bool _savedAppend = SystemClipboard.PasteBinAppend;

    public LimitTests()
    {
        Directory.CreateDirectory(_directory);
        SystemClipboard.PasteBinFile = Path.Combine(_directory, "paste-bin.txt");
    }

    public void Dispose()
    {
        SystemClipboard.PasteBinFile = _savedFile;
        SystemClipboard.Osc52 = _savedOsc52;
        SystemClipboard.PasteBin = _savedPasteBin;
        SystemClipboard.PasteBinAppend = _savedAppend;
        if (Directory.Exists(_directory))
        {
            foreach (var file in Directory.EnumerateFiles(_directory)) File.SetAttributes(file, FileAttributes.Normal);
            if (!OperatingSystem.IsWindows())
            {
                foreach (var file in Directory.EnumerateFiles(_directory))
                {
                    File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }

            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public void TheLayout_FollowsEditsInsideLines_AndLinesAddedOrTakenAway()
    {
        // Rows of 10 cells: "one two three four" takes two. After each change the rows are those of the text laid
        // out from the start
        var editor = new EditorEngine("one two three four\nfive", 10, 10, false) { Wrap = true };
        Assert.Equal(3, editor.Layout.TotalRows());
        foreach (var keys in new[] { "dd", "u", "ggd$", "u", "yy3p", "ggAmore text here<Esc>", "jJ", ":%j<CR>", "u",
                     "ggVGd", "u", "ix<CR>y<CR><Esc>" })
        {
            editor.Type(keys);
            var fresh = new EditorEngine(editor.Buffer.GetText(), 10, 10, false) { Wrap = true };
            Assert.Equal(fresh.Layout.TotalRows(), editor.Layout.TotalRows());
            for (var line = 0; line < editor.Buffer.LineCount; line++)
            {
                Assert.Equal(fresh.Layout.Segments(line), editor.Layout.Segments(line));
            }
        }
    }

    [Fact]
    public void Dup_PutsInNoMoreThanPutDoes()
    {
        var line = new string('a', 100_000);
        var editor = new EditorEngine(line, 40, 10, false);
        editor.Type(":dup 200<CR>");
        Assert.Equal(1, editor.Buffer.LineCount);
        Assert.True(editor.MessageIsError);
        editor.Type(":dup 3<CR>");
        Assert.Equal(4, editor.Buffer.LineCount);
    }

    [Fact]
    public void AReplacementThatTakesTooLong_ChangesNothing()
    {
        // The last line is found and replaced first (from the bottom); the first takes too long, so it is put back
        var text = new string('x', 30) + "\nmid\nxy";
        var editor = new EditorEngine(text, 40, 10, false);
        editor.Type(":%s/(x+x+)+y/z/<CR>");
        Assert.Equal(text, editor.Buffer.GetText());
        Assert.True(editor.MessageIsError);
    }

    [Fact]
    public void AVeryLongYank_IsNotSentToTheTerminal_ButToThePasteBin()
    {
        var text = new string('a', SystemClipboard.MaxOsc52Bytes + 1);
        SystemClipboard.Osc52 = true;
        SystemClipboard.PasteBin = false;
        var (message, isError) = SystemClipboard.Copy(text, true);
        Assert.True(isError);
        Assert.Contains("KB", message);
        SystemClipboard.PasteBin = true;
        SystemClipboard.PasteBinAppend = false;
        (_, isError) = SystemClipboard.Copy(text, true);
        Assert.False(isError);
        Assert.Equal(text, File.ReadAllText(SystemClipboard.PasteBinPath).TrimEnd('\r', '\n'));
    }

    // (UnixFact skips it on Windows; the attribute tells the analyzer, as File.SetUnixFileMode is not on Windows)
    [UnixFact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void AFileThatMayNotBeWritten_IsNotReplaced()
    {
        var path = Path.Combine(_directory, "locked.md");
        File.WriteAllText(path, "kept");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        Assert.Throws<UnauthorizedAccessException>(() => FileIo.WriteAllBytesSafe(path, "new"u8.ToArray()));
        Assert.Equal("kept", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(_directory));
    }
}
