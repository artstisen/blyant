namespace Blyant.Tests;

/// <summary>
/// The shell ("blyant --shell"): commands read from lines, and what they show written as lines. The tests run a
/// script of commands and look at what was written, as a script run with "blyant --shell &lt; commands.txt" does
/// </summary>
[Collection("Static state")]
public class ShellTests : IDisposable
{
    private readonly string _directory;
    private readonly string _data;

    public ShellTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blyant-shell-" + Guid.NewGuid().ToString("N"));
        _data = Path.Combine(_directory, "data");
        Directory.CreateDirectory(Path.Combine(_directory, "work", "Draft"));
        Directory.CreateDirectory(Path.Combine(_directory, "work", "notes"));
        Directory.CreateDirectory(_data);
        File.WriteAllText(In("work", "plot.md"), "Plot\n");
        File.WriteAllText(In("work", "Ending.md"), "End\n");
        FileFavorites.FilePathOverride = Path.Combine(_data, FileFavorites.FileName);
        FileHistory.FilePathOverride = Path.Combine(_data, "history.txt");
        DirectoryHotlist.FilePathOverride = Path.Combine(_data, "hotlist.txt");
        Bookmarks.FilePathOverride = Path.Combine(_data, Bookmarks.FileName);
        Projects.ListPathOverride = Path.Combine(_data, Projects.ListFileName);
        FileIo.DataDirectoryOverride = _data;
        FileOperations.TrashOverride = Path.Combine(_data, "trash");
        FileFavorites.Parse(string.Empty);
        FileHistory.Parse(string.Empty);
        DirectoryHotlist.Parse(string.Empty);
        Bookmarks.Clear();
    }

    public void Dispose()
    {
        FileFavorites.FilePathOverride = null;
        FileHistory.FilePathOverride = null;
        DirectoryHotlist.FilePathOverride = null;
        Bookmarks.FilePathOverride = null;
        Projects.ListPathOverride = null;
        // (set changes the settings: back to those of the start)
        Actions.ApplyDefaultSettings();
        FileIo.DataDirectoryOverride = null;
        FileOperations.TrashOverride = null;
        FileFavorites.Parse(string.Empty);
        FileHistory.Parse(string.Empty);
        DirectoryHotlist.Parse(string.Empty);
        Bookmarks.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(params string[] parts) => Path.GetFullPath(Path.Combine(new[] { _directory }.Concat(parts).ToArray()));

    // Run a script of commands (one a line) in a shell started in "work": what it wrote, what went wrong, and the
    // exit code. Not a terminal unless told (no prompt, questions not asked)
    private (string Output, string Errors, int Code, ShellSession Session) Run(string script, bool terminal = false,
        bool yes = false)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(script), output, errors, In("work"), terminal, yes);
        var code = Shell.Run(session);
        return (output.ToString().Replace("\r\n", "\n"), errors.ToString().Replace("\r\n", "\n"), code, session);
    }

    [Fact]
    public void Ls_ListsTheFoldersThenTheFiles_Numbered_AndCdTakesANumber()
    {
        var (output, errors, code, session) = Run("ls\ncd 2\npwd\nup\npwd\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(0, code);
        var lines = output.Split('\n');
        Assert.Equal(In("work"), lines[0]);
        // The folders first, then the files, each by name (letter case alike)
        Assert.Equal("  1  Draft/", lines[1]);
        Assert.Equal("  2  notes/", lines[2]);
        Assert.Equal("  3  Ending.md", lines[3]);
        Assert.Equal("  4  plot.md", lines[4]);
        Assert.Equal(In("work", "notes"), lines[5]);
        Assert.Equal(In("work"), lines[6]);
        Assert.Equal(In("work"), session.Directory);
    }

    [Fact]
    public void Cd_AloneGoesHome_ToAFile_OrNowhere_IsTold_AndTheExitCodeTellsIt()
    {
        var (_, errors, code, session) = Run("cd plot.md\ncd missing\ncd 7\ncd\n");
        Assert.Equal(1, code);
        var lines = errors.TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("plot.md", lines[0]);
        Assert.Contains("missing", lines[1]);
        Assert.Equal(Lang.F("No row {0}: no list has been shown", "7"), lines[2]);
        Assert.Equal(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            Path.GetFullPath(session.Directory));
    }

    [Fact]
    public void ANumberTooLarge_TellsHowManyRowsTheLastListHas()
    {
        var (_, errors, _, _) = Run("ls\ncd 9\n");
        Assert.Equal(Lang.F("No row {0}: the last list has {1} rows", "9", 4) + "\n", errors);
    }

    [Fact]
    public void APathFromTheRoot_IsOnTheDriveOfTheDirectory()
    {
        // ("/" on Windows: the root of the drive the shell is in, not of the drive Blyant was started on)
        var (_, errors, _, session) = Run("cd /\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(Path.GetPathRoot(In("work")), Path.GetPathRoot(session.Directory));
        Assert.Equal(Path.GetFullPath(Path.GetPathRoot(In("work"))!), Path.GetFullPath(session.Directory + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void TheLists_AreNumbered_AndOpenAndCdTakeTheirRows()
    {
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Save();
        FileHistory.Add(In("work", "Ending.md"), new DateTime(2026, 11, 13, 9, 30, 0));
        FileHistory.Save();
        DirectoryHotlist.Add(In("work", "Draft"));
        DirectoryHotlist.Save();
        Projects.Add(In("work", "notes"));
        Bookmarks.Parse("file\t" + In("work", "plot.md") + "\n");
        Bookmarks.Save();

        var (output, errors, _, session) = Run("fav\nhist\nhot\ncd 1\nproj\nproject notes\nbook\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  " + In("work", "plot.md") + "\n", output);
        Assert.Contains("  1  " + In("work", "Ending.md") + "  2026-11-13 09:30\n", output);
        Assert.Contains("  1  " + Path.TrimEndingDirectorySeparator(In("work", "Draft")) + "/\n", output);
        Assert.Contains("  1  notes  " + Path.TrimEndingDirectorySeparator(In("work", "notes")) + "/\n", output);
        // "project notes" went to the project; the bookmark is the last list
        Assert.Equal(In("work", "notes"), session.Directory);
        Assert.Equal(In("work", "plot.md"), session.LastList.Single().Path);
    }

    [Fact]
    public void Help_ListsEveryCommandOfTheTable()
    {
        var (output, _, _, _) = Run("help\n");
        foreach (var name in Commands.Names(CommandContext.Shell))
        {
            Assert.True(output.Contains("  " + name) || output.Contains("also " + name) || output.Contains(", " + name) ||
                        output.Contains("(" + name + ")"),
                "not in the help: " + name);
        }
    }

    [Fact]
    public void AnUnknownCommand_IsTold_AndTheShellGoesOn_UntilExit()
    {
        var (output, errors, code, _) = Run("frobnicate\nversion\nexit\nversion\n");
        Assert.Equal(Lang.F("Unknown command: {0} (help lists the commands)", "frobnicate") + "\n", errors);
        // After exit nothing more is run
        Assert.Equal(DialogText.ApplicationName + " " + DialogText.ApplicationVersion + "\n", output);
        Assert.Equal(1, code);
    }

    [Fact]
    public void Commands_AreLoggedByName_NeverWhatFollowsThem()
    {
        Run("ls secret-folder\n");
        Assert.Contains(EventLog.All, line => line.EndsWith(DialogText.LogShell + ": :ls"));
        Assert.DoesNotContain(EventLog.All, line => line.Contains("secret-folder"));
    }

    [Fact]
    public void FromAFile_AQuestionIsNotAsked_AndNothingIsDone_UnlessYes()
    {
        // "open new.md": Blyant asks to make it (ChoiceBox, asked on lines while the shell runs)
        var (output, _, _, _) = Run("open new.md\n");
        Assert.False(File.Exists(In("work", "new.md")));
        Assert.Contains(Lang.T("(not asked: the commands are not typed, so nothing is done)"), output);
        // With --yes it is made (and not opened: the editor needs a terminal)
        var (_, errors, code, _) = Run("open new.md\n", yes: true);
        Assert.True(File.Exists(In("work", "new.md")));
        Assert.Contains(Lang.F("{0} is not opened: the editor needs a terminal", In("work", "new.md")), errors);
        Assert.Equal(1, code);
    }

    [Fact]
    public void InATerminal_TheQuestionsAreAnsweredOnALine()
    {
        var session = new ShellSession(new StringReader("y\n2\n\nYes\nyes\nno\n"), new StringWriter(), new StringWriter(),
            In("work"), true);
        string[] choices = [DialogText.ChoiceYes, DialogText.ChoiceNo];
        Assert.Equal(0, session.Pick("", "Go?", "", choices, ["Y", "N"], 1, false));
        Assert.Equal(1, session.Pick("", "Go?", "", choices, ["Y", "N"], 0, false));
        // Enter alone: the choice selected from the start
        Assert.Equal(1, session.Pick("", "Go?", "", choices, ["Y", "N"], 1, false));
        Assert.Equal(0, session.Pick("", "Go?", "", choices, ["Y", "N"], 1, false));
        // Guarded (deleting): only the first choice typed in full is yes
        Assert.Equal(0, session.Pick("", "Delete?", "", choices, ["", ""], 1, true));
        Assert.Equal(-1, session.Pick("", "Delete?", "", choices, ["", ""], 1, true));
        // The end of the input: no
        Assert.Equal(-1, session.Pick("", "Go?", "", choices, ["Y", "N"], 0, false));
    }

    [Fact]
    public void InATerminal_ThePromptShowsTheDirectory()
    {
        var (output, _, _, _) = Run("exit\n", terminal: true);
        Assert.Contains("> ", output);
        Assert.Contains("work> ", output);
    }

    [UnixFact]
    public void OnLinuxAndMacOS_TildeIsTheHomeDirectory_AndSlashTheSeparator()
    {
        var (_, errors, _, session) = Run("cd ~/\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            Path.GetFullPath(session.Directory));
    }

    // ---- Phase 2: the files

    [Fact]
    public void Words_AreSeparatedByBlanks_OrKeptTogetherInQuotes()
    {
        Assert.Equal(new[] { "rename", "my notes.md", "new.md" }, ShellSession.Words("rename \"my notes.md\"  new.md"));
        Assert.Empty(ShellSession.Words("   "));
        Assert.Equal(new[] { "" }, ShellSession.Words("\"\""));
    }

    [Fact]
    public void NewMkdirRenameCopyMove_WorkOnTheFiles_AndTheNewPlaceIsTheLastList()
    {
        var (output, errors, _, session) = Run(
            "new ideas\nmkdir old\ncopy plot.md old\nrename Ending.md \"The end.md\"\nmove 1 old\nls old\n");
        Assert.Equal(string.Empty, errors);
        // (a name without an extension gets that of new files)
        Assert.True(File.Exists(In("work", "ideas.md")));
        Assert.True(File.Exists(In("work", "plot.md")));
        Assert.True(File.Exists(In("work", "old", "plot.md")));
        Assert.False(File.Exists(In("work", "Ending.md")));
        // "move 1": the row of the last list, which was the file renamed
        Assert.True(File.Exists(In("work", "old", "The end.md")));
        Assert.Contains(Lang.F("Moved to {0}", In("work", "old", "The end.md")), output);
        Assert.Equal(2, session.LastList.Count);
    }

    [Fact]
    public void CopyAndMove_NeverOverwrite_AndNamesTakenAreTold()
    {
        Directory.CreateDirectory(In("work", "old"));
        File.WriteAllText(In("work", "old", "plot.md"), "Older\n");
        var (_, errors, code, _) = Run("copy plot.md old\nmove plot.md old\nnew plot.md\nmkdir Draft\n");
        Assert.Equal("Older\n", File.ReadAllText(In("work", "old", "plot.md")));
        Assert.Equal("Plot\n", File.ReadAllText(In("work", "plot.md")));
        Assert.Equal(4, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(1, code);
    }

    [Fact]
    public void RenameAndMove_AreFollowedByTheFavoritesAndTheHistory()
    {
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Save();
        FileHistory.Add(In("work", "plot.md"), DateTime.Now);
        FileHistory.Save();
        Run("rename plot.md story.md\n");
        FileFavorites.Load();
        FileHistory.Load();
        Assert.True(FileFavorites.Contains(In("work", "story.md")));
        Assert.False(FileFavorites.Contains(In("work", "plot.md")));
        Assert.Equal(In("work", "story.md"), FileHistory.Items[0].Path);
    }

    [Fact]
    public void Delete_AsksFirst_AndGoesToTheTrash_AndLeavesTheFavorites()
    {
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Save();
        // From a file: not asked, so nothing is deleted
        var (output, _, _, _) = Run("delete plot.md\n");
        Assert.True(File.Exists(In("work", "plot.md")));
        Assert.Contains(Lang.T("Nothing was deleted"), output);
        // In a terminal: the answer typed in full; a letter is no
        Run("delete plot.md\ny\n", terminal: true);
        Assert.True(File.Exists(In("work", "plot.md")));
        Run("delete plot.md\n" + DialogText.ChoiceYes + "\n", terminal: true);
        Assert.False(File.Exists(In("work", "plot.md")));
        Assert.Single(Directory.GetFiles(Path.Combine(_data, "trash")), path => path.EndsWith("plot.md"));
        FileFavorites.Load();
        Assert.False(FileFavorites.Contains(In("work", "plot.md")));
        // With --yes, from a file: a directory with what is in it
        Run("delete Draft\n", yes: true);
        Assert.False(Directory.Exists(In("work", "Draft")));
    }

    [Fact]
    public void TheFilesOfBlyant_CannotBeRenamedMovedOrDeleted_EvenWithYes()
    {
        File.WriteAllText(Path.Combine(_data, FileIo.SettingsFileName), "x\n");
        var settings = Path.Combine(_data, FileIo.SettingsFileName);
        var (_, errors, _, _) = Run("rename \"" + settings + "\" y.ini\ndelete \"" + _data + "\"\nmove \"" + settings +
                                     "\" Draft\n", yes: true);
        Assert.True(File.Exists(settings));
        Assert.True(Directory.Exists(_data));
        Assert.Equal(3, errors.TrimEnd('\n').Split('\n').Length);
    }

    [Fact]
    public void Peek_ShowsTheLinesNumbered_AndNeverWritesTheFile()
    {
        File.WriteAllText(In("work", "long.md"), string.Join("\n", Enumerable.Range(1, 50).Select(n => "Line " + n)));
        var before = File.GetLastWriteTimeUtc(In("work", "long.md"));
        var (output, errors, _, _) = Run("peek long.md\npeek long.md 10-12\npeek long.md 3\npeek Draft\npeek long.md x\n");
        Assert.Contains("   1  Line 1\n", output);
        Assert.Contains("  20  Line 20\n" + Lang.F("(lines {0}-{1} of {2})", 1, 20, 50), output);
        Assert.Contains("  10  Line 10\n  11  Line 11\n  12  Line 12\n", output);
        Assert.Contains("  3  Line 3\n", output);
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(before, File.GetLastWriteTimeUtc(In("work", "long.md")));
    }

    [Fact]
    public void Find_FindsByNameAndByText_AndOpenGoesToTheLineFound()
    {
        File.WriteAllText(In("work", "Draft", "storm.md"), "Calm\nThe storm came\n");
        var (output, errors, _, session) = Run("find *.md\nfind -t storm\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("Ending.md", output);
        Assert.Contains("Draft" + Path.DirectorySeparatorChar + "storm.md:2  The storm came", output);
        // The last list: the file found by its text, at the line
        Assert.Equal((2, 1), session.LastList.Single().At);
        var (_, nothing, _, _) = Run("find\n");
        Assert.NotEqual(string.Empty, nothing);
    }

    [Fact]
    public void FavAndHot_AddAndRemove_AndTheListsShowIt()
    {
        var (output, errors, _, _) = Run("fav add plot.md\nfav add plot.md\nfav\nfav remove 1\nhot add\nhot add Draft\nhot\nhot remove 2\nhot remove plot.md\n");
        Assert.Contains(Lang.F("Already a favorite: {0}", In("work", "plot.md")), output);
        FileFavorites.Load();
        Assert.False(FileFavorites.Contains(In("work", "plot.md")));
        DirectoryHotlist.Load();
        Assert.Single(DirectoryHotlist.Items);
        Assert.Equal(Lang.F("Not a directory: {0}", In("work", "plot.md")) + "\n", errors);
    }

    // ---- Phase 3: the text of files, info, the settings, --run

    [Fact]
    public void FormatAndTrim_ChangeTheFile_KeepingItsLineEndingsAndByteOrderMark()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        File.WriteAllBytes(In("work", "page.html"),
            bom.Concat(System.Text.Encoding.UTF8.GetBytes("<div><p>One</p></div>\r\n")).ToArray());
        File.WriteAllText(In("work", "poem.md"), "Roses  \r\nViolets  \r\n\r\nEnd  \r\n");
        var (output, errors, _, _) = Run("format page.html\nformat page.html\ntrim poem.md\n");
        Assert.Equal(string.Empty, errors);
        var bytes = File.ReadAllBytes(In("work", "page.html"));
        Assert.Equal(bom, bytes.Take(3));
        Assert.Equal("<div>\r\n  <p>One</p>\r\n</div>\r\n", System.Text.Encoding.UTF8.GetString(bytes.Skip(3).ToArray()));
        Assert.Contains(Lang.T("Already formatted"), output);
        // A line break of Markdown (two spaces before a line of text) is kept, as by :trim
        Assert.Equal("Roses  \r\nViolets\r\n\r\nEnd\r\n", File.ReadAllText(In("work", "poem.md")));
        Run("trim! poem.md\n");
        Assert.Equal("Roses\r\nViolets\r\n\r\nEnd\r\n", File.ReadAllText(In("work", "poem.md")));
    }

    [Fact]
    public void FormatAndTrim_LeaveFilesTheyCannotChange_AndTellWhy()
    {
        // Not UTF-8 (Windows-1252), not of a kind that is formatted, broken, and a directory
        File.WriteAllBytes(In("work", "old.md"), new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x20, 0x20, 0x0A });
        File.WriteAllText(In("work", "broken.css"), "a{color:red\n");
        var before = File.ReadAllBytes(In("work", "old.md"));
        var (_, errors, code, _) = Run("trim old.md\nformat plot.md\nformat broken.css\nformat Draft\n");
        Assert.Equal(before, File.ReadAllBytes(In("work", "old.md")));
        Assert.Equal("a{color:red\n", File.ReadAllText(In("work", "broken.css")));
        var lines = errors.TrimEnd('\n').Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Contains(Lang.T("Only HTML, CSS and XML files can be formatted"), lines[1]);
        Assert.Contains(Lang.F("Not formatted: {0}", string.Empty), lines[2]);
        Assert.Equal(1, code);
    }

    [Fact]
    public void Info_TellsAboutAFile_WithItsLinesAndEncoding_OrADirectory()
    {
        var (output, errors, _, _) = Run("info plot.md\ninfo Draft\ninfo\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.T("Lines:"), output);
        Assert.Contains("UTF-8", output);
        Assert.Contains(DialogText.InfoContent, output);
        Assert.Contains(In("work", "Draft"), output);
    }

    [Fact]
    public void Set_ShowsAndChangesTheSettings_AndAValueThatCannotBe_ChangesNothing()
    {
        FileIo.SaveSettings();
        var (output, errors, _, _) = Run("set\nset indent 8\nset indent 3\nset INDENT\nset nosuch 1\n");
        Assert.Contains("indent", output);
        Assert.Contains("  indent = 8\n", output);
        Assert.Equal(8, EditorEngine.IndentSetting);
        Assert.Equal("8", FileIo.ParseSettings(FileIo.ReadSettingsFile())["indent"]);
        var lines = errors.TrimEnd('\n').Split('\n');
        Assert.Equal(Lang.F("{0} cannot be {1}: it stays {2}", "indent", "3", "8"), lines[0]);
        Assert.Equal(Lang.F("No setting named {0} (set lists them)", "nosuch"), lines[1]);
        // Logged by name, never with the value
        Assert.Contains(EventLog.All, line => line.EndsWith(DialogText.LogShell + ": setting indent"));
    }

    [Fact]
    public void Run_TakesTheCommandsGiven_AndTheShellOptions()
    {
        var arguments = new List<string> { "--run", "format a.html", "notes", "--yes", "--run", "ls" };
        Assert.True(StartArgument.TakeShellOptions(arguments, In("work"), out var yes, out var run));
        Assert.True(yes);
        Assert.Equal(new[] { "format a.html", "ls" }, run);
        Assert.Equal(new[] { "notes" }, arguments);
        // "--run" without a command after it: not taken (an option Blyant does not know, as told)
        var alone = new List<string> { "--run" };
        Assert.False(StartArgument.TakeShellOptions(alone, In("work"), out _, out var none));
        Assert.Empty(none);
        Assert.Equal(new[] { "--run" }, alone);
        // "--yes" without the shell is not taken either
        var noShell = new List<string> { "--yes" };
        Assert.False(StartArgument.TakeShellOptions(noShell, In("work"), out var notYes, out _));
        Assert.False(notYes);
        Assert.Equal(new[] { "--yes" }, noShell);
    }

    [Fact]
    public void Clear_ClearsTheScreenOfTheTerminal_AndFromAFileDoesNothing()
    {
        var cleared = 0;
        var session = new ShellSession(new StringReader("clear\ncls\n"), new StringWriter(), new StringWriter(),
            In("work"), true) { ClearScreen = () => cleared++ };
        Shell.Run(session);
        Assert.Equal(2, cleared);
        // Not a terminal: no screen to clear, and nothing goes wrong
        var (output, errors, code, _) = Run("clear\n");
        Assert.Equal(string.Empty, output + errors);
        Assert.Equal(0, code);
    }

    // ---- Pages: a long output in a terminal waits for a key when the window is full

    // A shell in a terminal of this size, with these keys pressed at the pauses: what it wrote, and how many keys
    // were read
    private (string Output, int KeysRead) Paged(string script, (int Width, int Height)? size, params ConsoleKey[] keys)
    {
        var output = new StringWriter();
        var queue = new Queue<ConsoleKeyInfo>(keys.Select(key => new ConsoleKeyInfo(key == ConsoleKey.C ? '\u0003' : ' ',
            key, false, false, key == ConsoleKey.C)));
        var read = 0;
        var session = new ShellSession(new StringReader(script), output, new StringWriter(), In("work"), true)
        {
            WindowSize = () => size,
            ReadKey = () =>
            {
                read++;
                return queue.Count > 0 ? queue.Dequeue() : new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
            }
        };
        Shell.Run(session);
        return (output.ToString().Replace("\r\n", "\n"), read);
    }

    // The lines of the file peek shows ("12  Line 12")
    private static int PeekLines(string output) =>
        // (anywhere: in a test nothing echoes the command typed, so the first line follows the prompt, and a line after
        // a pause follows the return that wrote over it)
        System.Text.RegularExpressions.Regex.Matches(output, @"(?<!\d)\d+  Line \d+").Count;

    private void LongFile() =>
        File.WriteAllText(In("work", "long.md"), string.Join("\n", Enumerable.Range(1, 60).Select(n => "Line " + n)));

    [Fact]
    public void ALongOutput_ComesInPages_AndQStopsIt()
    {
        LongFile();
        // A window of 10 rows: 9 for the output (the last for the pause), the first of them the command typed
        var (output, read) = Paged("peek long.md 1-60\n", (80, 10), ConsoleKey.Enter, ConsoleKey.Q);
        Assert.Equal(2, read);
        Assert.Equal(8 + 9, PeekLines(output));
        Assert.Equal(2, output.Split(Lang.T("-- More: Enter or Space next page, q stop --")).Length - 1);
        // The next command is shown whole again from its first page
        var (again, _) = Paged("peek long.md 1-60\npeek long.md 1-5\n", (80, 10), ConsoleKey.Q);
        Assert.Equal(8 + 5, PeekLines(again));
    }

    [Fact]
    public void Space_GoesOn_AndCtrlC_StopsAsQ()
    {
        LongFile();
        var (output, _) = Paged("peek long.md 1-30\n", (80, 10), ConsoleKey.Spacebar, ConsoleKey.Spacebar,
            ConsoleKey.Spacebar);
        Assert.Equal(30, PeekLines(output));
        var (stopped, read) = Paged("peek long.md 1-30\n", (80, 10), ConsoleKey.C);
        Assert.Equal(1, read);
        Assert.Equal(8, PeekLines(stopped));
    }

    [Fact]
    public void ALineWiderThanTheWindow_TakesMoreRows()
    {
        // 25 columns in a window of 10: three rows each
        File.WriteAllText(In("work", "wide.md"), string.Join("\n", Enumerable.Range(1, 9).Select(n => "Line " + n + new string('x', 17))));
        var (output, _) = Paged("peek wide.md 1-9\n", (10, 10), ConsoleKey.Q);
        // (8 rows after the command: two lines of three rows)
        Assert.Equal(2, PeekLines(output));
    }

    [Fact]
    public void NothingIsPaged_WithoutAWindowSize_WithPagingOff_OrFromAFile()
    {
        LongFile();
        // A terminal that does not tell its size (or tells 0 rows)
        Assert.Equal((60, 0), (PeekLines(Paged("peek long.md 1-60\n", null).Output), Paged("peek long.md 1-60\n", null).KeysRead));
        Assert.Equal(0, Paged("peek long.md 1-60\n", (80, 0)).KeysRead);
        // Paging turned off (setting "shellpaging")
        Shell.Paging = false;
        var (off, read) = Paged("peek long.md 1-60\n", (80, 10));
        Assert.Equal(60, PeekLines(off));
        Assert.Equal(0, read);
        Shell.Paging = true;
        // Not a terminal: the whole output, no key read (as for a pipe: blyant --run "ls" | less)
        var keys = 0;
        var output = new StringWriter();
        Shell.Run(new ShellSession(new StringReader("peek long.md 1-60\n"), output, new StringWriter(), In("work"), false)
        {
            WindowSize = () => (80, 10),
            ReadKey = () =>
            {
                keys++;
                return new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
            }
        });
        Assert.Equal(60, PeekLines(output.ToString()));
        Assert.Equal(0, keys);
    }

    [Fact]
    public void ShellPaging_IsASettingOfTheSettingsFile()
    {
        FileIo.SaveSettings();
        Assert.Equal("true", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shellpaging"]);
        Run("set shellpaging false\n");
        Assert.False(Shell.Paging);
        Assert.Equal("false", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shellpaging"]);
    }

    // ---- Between the full screen and the shell: ":shell" and "screen"

    [Fact]
    public void Screen_EndsTheShell_ToGoOnOnTheFullScreen_InItsDirectory()
    {
        var (_, errors, _, session) = Run("cd Draft\nscreen\nversion\n", terminal: true);
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "Draft"), session.ScreenDirectory);
        Assert.Equal(In("work", "Draft"), Shell.TakeScreenRequest());
        // Taken once
        Assert.Null(Shell.TakeScreenRequest());
        // "exit": no full screen
        Run("exit\n", terminal: true);
        Assert.Null(Shell.TakeScreenRequest());
    }

    [Fact]
    public void Screen_NeedsATerminal()
    {
        var (_, errors, code, session) = Run("screen\nversion\n");
        Assert.Equal(Lang.T("The full screen needs a terminal") + "\n", errors);
        Assert.Null(session.ScreenDirectory);
        Assert.Null(Shell.TakeScreenRequest());
        Assert.Equal(1, code);
    }

    [Fact]
    public void FromTheEditor_TheFileIsTheLastList()
    {
        var session = new ShellSession(new StringReader("exit\n"), new StringWriter(), new StringWriter(), In("work"), false);
        session.ShowFirst(In("work", "plot.md"));
        Assert.Equal(In("work", "plot.md"), session.LastList.Single().Path);
    }

    [Fact]
    public void ToShell_LeavesThePage_AsAJump_UnlessThePageIsNotLeft()
    {
        Shell.Requested = null;
        // The page does not let go (Cancel at the question about changes): nothing happens
        Jump.ToShell(() => false, In("work"));
        Assert.Null(Shell.Requested);
        // Let go: the page is left (as for a jump), and the main loop is asked for the shell, with the file
        Assert.Throws<NavigationException>(() => Jump.ToShell(() => true, In("work"), In("work", "plot.md")));
        Assert.Equal(new Shell.Switch(In("work"), In("work", "plot.md")), Shell.Requested);
        Assert.Null(Jump.Pending);
        Shell.Requested = null;
    }
}
