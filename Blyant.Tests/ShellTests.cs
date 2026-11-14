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
        ShellRegisters.FilePathOverride = Path.Combine(_data, ShellRegisters.FileName);
        ShellRegisters.Clear();
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
        ShellRegisters.FilePathOverride = null;
        ShellRegisters.Clear();
        SystemClipboard.WriteOverride = null;
        Tools.ClearLineMemory();
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
        bool yes = false, bool clipboard = false)
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(script), output, errors, In("work"), terminal, yes)
        {
            Clipboard = clipboard
        };
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
    public void Cd_AloneGoesHome_ToNowhere_IsTold_AndTheExitCodeTellsIt()
    {
        var (_, errors, code, session) = Run("cd missing\ncd 7\ncd\n");
        Assert.Equal(1, code);
        var lines = errors.TrimEnd('\n').Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("missing", lines[0]);
        Assert.Equal(Lang.F("No row {0}: no list has been shown", "7"), lines[1]);
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
    public void Delete_TellsWhereTheTrashIsOpened_InTheShell()
    {
        // (":trash" is a command of the file browser, not of the shell)
        var (output, _, _, _) = Run("delete plot.md\ndelete Draft\n");
        Assert.Contains(Lang.T("It can be found again in the trash (:trash in the file browser)"), output);
        Assert.DoesNotContain(DialogText.TrashFileNote + "\n", output);
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
        Assert.True(StartArgument.TakeShellOptions(arguments, In("work"), out var yes, out var run, out _));
        Assert.True(yes);
        Assert.Equal(new[] { "format a.html", "ls" }, run);
        Assert.Equal(new[] { "notes" }, arguments);
        // "--run" without a command after it: not taken (an option Blyant does not know, as told)
        var alone = new List<string> { "--run" };
        Assert.False(StartArgument.TakeShellOptions(alone, In("work"), out _, out var none, out _));
        Assert.Empty(none);
        Assert.Equal(new[] { "--run" }, alone);
        // "--yes" without the shell is not taken either
        var noShell = new List<string> { "--yes" };
        Assert.False(StartArgument.TakeShellOptions(noShell, In("work"), out var notYes, out _, out _));
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

    [Fact]
    public void Cd_OnAFile_GoesToItsDirectory_AndTheFileIsTheLastList()
    {
        File.WriteAllText(In("work", "Draft", "storm.md"), "Calm\nThe storm came\n");
        FileFavorites.Toggle(In("work", "Draft", "storm.md"));
        FileFavorites.Save();
        // A row of the favorites (from another directory), and then "open 1" is the file
        var (output, errors, _, session) = Run("cd ..\nfav\ncd 1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "Draft"), session.Directory);
        Assert.Contains(Lang.F("In {0} (the directory of {1})", In("work", "Draft"), "storm.md"), output);
        Assert.Equal(In("work", "Draft", "storm.md"), session.LastList.Single().Path);
        // A path of a file; and a file found by its text keeps its line for "open 1"
        var (_, _, _, byPath) = Run("cd Draft/storm.md\n");
        Assert.Equal(In("work", "Draft"), byPath.Directory);
        var (_, _, _, found) = Run("find -t storm\ncd 1\n");
        Assert.Equal(In("work", "Draft"), found.Directory);
        Assert.Equal((2, 1), found.LastList.Single().At);
    }

    [Fact]
    public void Peek_FromALine_ShowsAsManyLinesAsGiven_AndOpenGoesThere()
    {
        File.WriteAllText(In("work", "long.md"), string.Join("\n", Enumerable.Range(1, 50).Select(n => "Line " + n)));
        var (output, errors, _, session) = Run("peek long.md +45\n");
        Assert.Equal(string.Empty, errors);
        // 20 lines from line 45: as many as there are
        Assert.Contains("  45  Line 45\n", output);
        Assert.Contains("  50  Line 50\n", output);
        Assert.Equal((45, 1), session.LastList.Single().At);
        var (some, _, _, _) = Run("peek long.md +10 3\n");
        Assert.Contains("  10  Line 10\n  11  Line 11\n  12  Line 12\n" + Lang.F("(lines {0}-{1} of {2})", 10, 12, 50), some);
        // A start past the end: how many lines the file has
        var (past, _, _, _) = Run("peek long.md +60\n");
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "long.md"), 50), past);
        // From the first line, the place of the file is as it was (open goes where it was left)
        var (_, _, _, top) = Run("peek long.md\n");
        Assert.Null(top.LastList.Single().At);
    }

    [Theory]
    [InlineData("+0")]
    [InlineData("+10 0")]
    [InlineData("+10 x")]
    [InlineData("40 10")]
    [InlineData("+")]
    [InlineData("-5")]
    public void Peek_TellsLinesItCannotShow(string lines)
    {
        File.WriteAllText(In("work", "long.md"), "a\nb\n");
        var (_, errors, _, _) = Run("peek long.md " + lines + "\n");
        Assert.NotEqual(string.Empty, errors);
    }

    [Fact]
    public void LinesOf_ReadsTheFormsOfPeek()
    {
        Assert.True(ShellSession.LinesOf("40", null, out var first, out var last));
        Assert.Equal((1, 40), (first, last));
        Assert.True(ShellSession.LinesOf("100-120", null, out first, out last));
        Assert.Equal((100, 120), (first, last));
        Assert.True(ShellSession.LinesOf("+100", null, out first, out last));
        Assert.Equal((100, 119), (first, last));
        Assert.True(ShellSession.LinesOf("+100", "30", out first, out last));
        Assert.Equal((100, 129), (first, last));
        // Very large: not past the largest line number
        Assert.True(ShellSession.LinesOf("+2000000000", "2000000000", out _, out last));
        Assert.Equal(int.MaxValue, last);
    }

    [Theory]
    [InlineData("a\nb\n", 1, "new\na\nb\n", 1)]
    [InlineData("a\nb\n", int.MaxValue, "a\nb\nnew\n", 3)]
    [InlineData("a\nb\n", 2, "a\nnew\nb\n", 2)]
    [InlineData("a\nb\n", 3, "a\nb\nnew\n", 3)]
    [InlineData("a\nb\n", 99, "a\nb\nnew\n", 3)]
    // Without a line break at the end: none after a line put in at the end
    [InlineData("a\nb", int.MaxValue, "a\nb\nnew", 3)]
    [InlineData("a\nb", 1, "new\na\nb", 1)]
    [InlineData("", int.MaxValue, "new\n", 1)]
    [InlineData("", 5, "new\n", 1)]
    public void WithLine_PutsTheLineInItsPlace(string text, int at, string expected, int line)
    {
        Assert.Equal((expected, line), ShellSession.WithLine(text, "new", at));
    }

    [Fact]
    public void Insert_PutsALineInAFile_KeepingItsLineEndings_AndOpenGoesThere()
    {
        File.WriteAllText(In("work", "list.md"), "One\r\nTwo\r\n");
        var (output, errors, _, session) = Run(
            "insert list.md top Zero\ninsert list.md bottom   Three, with \"quotes\"\ninsert list.md +3 Two and a half\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Zero\r\nOne\r\nTwo and a half\r\nTwo\r\n  Three, with \"quotes\"\r\n",
            File.ReadAllText(In("work", "list.md")));
        Assert.Contains(Lang.F("{0}: line {1} put in", In("work", "list.md"), 3), output);
        Assert.Equal((3, 1), session.LastList.Single().At);
        // A file named with blanks, in quotes
        File.WriteAllText(In("work", "my notes.md"), "x\n");
        Run("insert \"my notes.md\" bottom y\n");
        Assert.Equal("x\ny\n", File.ReadAllText(In("work", "my notes.md")));
    }

    [Fact]
    public void Insert_TellsWhatItCannotDo()
    {
        File.WriteAllBytes(In("work", "old.md"), new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x0A });
        File.WriteAllText(Path.Combine(_data, FileIo.SettingsFileName), "x\n");
        var (_, errors, code, _) = Run("insert plot.md\ninsert plot.md middle x\ninsert plot.md +0 x\ninsert old.md top x\n" +
                                       "insert missing.md top x\ninsert Draft top x\ninsert \"" +
                                       Path.Combine(_data, FileIo.SettingsFileName) + "\" top x\n");
        Assert.Equal(7, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal("Plot\n", File.ReadAllText(In("work", "plot.md")));
        Assert.Equal("x\n", File.ReadAllText(Path.Combine(_data, FileIo.SettingsFileName)));
        Assert.Equal(1, code);
    }

    [Fact]
    public void Insert_NeverLogsTheText()
    {
        Run("insert plot.md bottom secret words\n");
        Assert.DoesNotContain(EventLog.All, line => line.Contains("secret words"));
        Assert.Contains(EventLog.All, line => line.Contains("Insert: " + In("work", "plot.md") + ":2"));
    }

    // ---- The start directory (shelldir), and home and data

    [Fact]
    public void StartIn_IsTheDirectorySet_OrTheLastOne_OrHere_AndAGoneOneIsTold()
    {
        var here = In("work");
        // Nothing set: here
        Shell.StartSetting = string.Empty;
        Assert.Equal(here, Shell.StartIn(here, out var note));
        Assert.Null(note);
        // A path
        Shell.StartSetting = In("work", "Draft");
        Assert.Equal(In("work", "Draft"), Shell.StartIn(here, out note));
        Assert.Null(note);
        // "~": the home directory
        Shell.StartSetting = "~";
        Assert.Equal(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            Path.GetFullPath(Shell.StartIn(here, out _)));
        // Gone: here, and told
        Shell.StartSetting = In("work", "gone");
        Assert.Equal(here, Shell.StartIn(here, out note));
        Assert.Equal(Lang.F("The start directory {0} is not there: the shell starts here", In("work", "gone")), note);
        // "last": the one the shell was left in (nothing yet: here)
        Shell.StartSetting = "LAST";
        Shell.LastDirectory = string.Empty;
        Assert.Equal(here, Shell.StartIn(here, out note));
        Assert.Null(note);
        Shell.LastDirectory = In("work", "notes");
        Assert.Equal(In("work", "notes"), Shell.StartIn(here, out _));
    }

    [Fact]
    public void Last_KeepsTheDirectoryTheShellIsLeftIn_InTheSettings()
    {
        Shell.StartSetting = "last";
        Shell.RememberLast(In("work", "Draft"));
        Assert.Equal(In("work", "Draft"), Shell.LastDirectory);
        Assert.Equal(In("work", "Draft"), FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelllastdir"]);
        Assert.Equal("last", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelldir"]);
        // Not "last": nothing is kept
        Shell.StartSetting = In("work");
        Shell.RememberLast(In("work", "notes"));
        Assert.Equal(In("work", "Draft"), Shell.LastDirectory);
    }

    [Fact]
    public void Set_KnowsTheSettingsOfThisVersion_InAFileOfAnEarlierOne()
    {
        // A settings file without shelldir (written before it was there)
        var text = string.Join("\n", FileIo.SettingsText().Split('\n').Where(line => !line.StartsWith("shelldir=")));
        FileIo.WriteSettingsFile(text);
        Assert.False(FileIo.ParseSettings(FileIo.ReadSettingsFile()).ContainsKey("shelldir"));
        var (output, errors, _, _) = Run("set shelldir last\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("shelldir = last", output);
        Assert.Equal("last", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelldir"]);
    }

    [Fact]
    public void ShellDir_IsSetWithSet_AndReadBack()
    {
        FileIo.SaveSettings();
        Run("set shelldir ~/projects\n");
        Assert.Equal("~/projects", Shell.StartSetting);
        Run("set shelldir last\n");
        Assert.Equal("last", Shell.StartSetting);
    }

    [Fact]
    public void HomeAndData_GoToTheHomeAndTheDataDirectory_AndAFolderNamedSoIsStillACd()
    {
        Directory.CreateDirectory(In("work", "data"));
        Directory.CreateDirectory(In("work", "home"));
        var (_, errors, _, session) = Run("data\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(Path.GetFullPath(_data), Path.GetFullPath(session.Directory));
        var (_, _, _, home) = Run("home\n");
        Assert.Equal(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            Path.GetFullPath(home.Directory));
        // "cd data" and "cd home": the folders of those names
        var (_, _, _, folder) = Run("cd data\n");
        Assert.Equal(In("work", "data"), folder.Directory);
        var (_, _, _, other) = Run("cd home\n");
        Assert.Equal(In("work", "home"), other.Directory);
    }

    [UnixFact]
    public void OnLinuxAndMacOS_TheStartDirectoryTakesTildeAndSlash()
    {
        Shell.StartSetting = "~/";
        Assert.Equal(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            Path.GetFullPath(Shell.StartIn(In("work"), out var note)));
        Assert.Null(note);
        // A path of Windows: not there, told, and here
        Shell.StartSetting = @"C:\Users\nobody\projects";
        Assert.Equal(In("work"), Shell.StartIn(In("work"), out note));
        Assert.NotNull(note);
    }

    [Fact]
    public void Reset_PutsTheSettingsBack_OnlyWhenTheAnswerIsYes()
    {
        FileIo.SaveSettings();
        Run("set indent 8\nset shelldir last\n");
        Assert.Equal(8, EditorEngine.IndentSetting);
        // From a file: not asked, and nothing is reset
        var (kept, _, _, _) = Run("reset\n");
        Assert.Contains(Lang.T("The settings are as they were"), kept);
        Assert.Equal(8, EditorEngine.IndentSetting);
        // In a terminal: No is selected (Enter alone), and "n"
        Run("reset\n\nreset\nn\n", terminal: true);
        Assert.Equal(8, EditorEngine.IndentSetting);
        // Yes: all the settings back, and saved
        var (output, errors, _, _) = Run("reset\ny\n", terminal: true);
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.T("Settings reset to the defaults"), output);
        Assert.Equal(EditorEngine.DefaultIndent, EditorEngine.IndentSetting);
        Assert.Equal(string.Empty, Shell.StartSetting);
        Assert.Equal("4", FileIo.ParseSettings(FileIo.ReadSettingsFile())["indent"]);
        // With --yes, from a file
        Run("set indent 2\n");
        Run("reset\n", yes: true);
        Assert.Equal(EditorEngine.DefaultIndent, EditorEngine.IndentSetting);
    }

    [Fact]
    public void Reset_KeepsTheLists()
    {
        FileIo.SaveSettings();
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Save();
        Run("reset\n", yes: true);
        FileFavorites.Load();
        Assert.True(FileFavorites.Contains(In("work", "plot.md")));
    }

    // ---- change: lines changed into a text, or deleted

    [Theory]
    [InlineData("a\nb\nc\nd\n", 2, 2, "x", "a\nx\nc\nd\n", 2)]
    [InlineData("a\nb\nc\nd\n", 2, 3, "x", "a\nx\nd\n", 2)]
    [InlineData("a\nb\nc\nd\n", 2, 3, null, "a\nd\n", 2)]
    [InlineData("a\nb\nc\nd\n", 3, 4, null, "a\nb\n", 2)]
    [InlineData("a\nb\nc\nd", 4, 4, "x", "a\nb\nc\nx", 4)]
    [InlineData("a\nb\n", 1, 2, null, "", 1)]
    public void WithLinesChanged_ChangesOrTakesOutTheLines(string text, int first, int last, string? line,
        string expected, int at)
    {
        Assert.Equal((expected, at), ShellSession.WithLinesChanged(text, first, last, line));
    }

    [Theory]
    [InlineData("+12", true, 12, 12)]
    [InlineData("12-15", true, 12, 15)]
    [InlineData("+12-15", true, 12, 15)]
    [InlineData("12", true, 12, 12)]
    [InlineData("15-12", false, 0, 0)]
    [InlineData("+0", false, 0, 0)]
    [InlineData("top", false, 0, 0)]
    public void LinesToChange_ReadsOneLineOrARange(string text, bool ok, int first, int last)
    {
        Assert.Equal(ok, ShellSession.LinesToChange(text, out var from, out var to));
        if (ok) Assert.Equal((first, last), (from, to));
    }

    [Fact]
    public void Change_ChangesLinesIntoAText_KeepingTheLineEndings_AndOpenGoesThere()
    {
        File.WriteAllText(In("work", "list.md"), "One\r\nTwo\r\nThree\r\nFour\r\n");
        var (output, errors, _, session) = Run("change list.md +1 Zero\nchange list.md 2-3 Two and three\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Zero\r\nTwo and three\r\nFour\r\n", File.ReadAllText(In("work", "list.md")));
        Assert.Contains(Lang.F("{0}: lines {1}-{2} changed", In("work", "list.md"), 2, 3), output);
        Assert.Equal((2, 1), session.LastList.Single().At);
        // A range past the end stops at the last line
        Run("change list.md 3-99 End\n");
        Assert.Equal("Zero\r\nTwo and three\r\nEnd\r\n", File.ReadAllText(In("work", "list.md")));
    }

    [Fact]
    public void Change_WithoutAText_DeletesTheLines_OnlyWhenTheAnswerIsYes()
    {
        File.WriteAllText(In("work", "list.md"), "One\nTwo\nThree\nFour\n");
        // From a file: shown, not asked, and nothing is deleted
        var (shown, _, _, _) = Run("change list.md 2-3\n");
        Assert.Contains("  2  Two\n  3  Three\n", shown);
        Assert.Contains(Lang.T("Nothing was deleted"), shown);
        Assert.Equal("One\nTwo\nThree\nFour\n", File.ReadAllText(In("work", "list.md")));
        // In a terminal: Enter alone is no, y is yes
        Run("change list.md 2-3\n\n", terminal: true);
        Assert.Equal("One\nTwo\nThree\nFour\n", File.ReadAllText(In("work", "list.md")));
        var (output, _, _, _) = Run("change list.md 2-3\ny\n", terminal: true);
        Assert.Equal("One\nFour\n", File.ReadAllText(In("work", "list.md")));
        Assert.Contains(Lang.F("{0}: lines {1}-{2} deleted", In("work", "list.md"), 2, 3), output);
        // With --yes, from a file
        Run("change list.md +1\n", yes: true);
        Assert.Equal("Four\n", File.ReadAllText(In("work", "list.md")));
    }

    [Fact]
    public void Change_TellsWhatItCannotDo_AndNeverLogsTheText()
    {
        File.WriteAllText(In("work", "list.md"), "One\nTwo\n");
        var (_, errors, code, _) = Run("change list.md\nchange list.md middle x\nchange list.md 5 x\nchange Draft +1 x\n" +
                                       "change missing.md +1 x\nchange list.md +1 secret words\n");
        Assert.Equal(5, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "list.md"), 2), errors);
        Assert.Equal(1, code);
        Assert.DoesNotContain(EventLog.All, line => line.Contains("secret words"));
        Assert.Contains(EventLog.All, line => line.Contains("Change lines: " + In("work", "list.md") + ":1-1"));
    }

    // ---- Tab, Up and the keys of the line (setting "shellcompletion")

    private ShellCompletion Completion() => new(() => In("work"), typed => Path.GetFullPath(typed, In("work")));

    // Tab pressed a number of times on a line (false: Shift+Tab)
    private static string Tab(ShellCompletion completion, string text, params bool[] keys)
    {
        foreach (var forward in keys.Length == 0 ? new[] { true } : keys) text = completion.Next(text, forward);
        return text;
    }

    [Fact]
    public void Tab_CompletesTheNamesOfTheCommands_AndGoesThroughThem()
    {
        var completion = Completion();
        Assert.Equal("favorites", Tab(completion, "fa"));
        var names = Commands.Names(CommandContext.Shell).Where(name => name.StartsWith('c')).ToList();
        Assert.Equal(names[0], Tab(completion, "c"));
        Assert.Equal(names[1], Tab(completion, "c", true, true));
        // After the last: what was typed; Shift+Tab goes back
        Assert.Equal("c", Tab(completion, "c", Enumerable.Repeat(true, names.Count + 1).ToArray()));
        Assert.Equal(names[^1], Tab(completion, "c", false));
        Assert.Equal("xyz", Tab(completion, "xyz"));
    }

    [Fact]
    public void Tab_CompletesTheWordsAfterSomeCommands()
    {
        var completion = Completion();
        FileIo.SaveSettings();
        Assert.Equal("set shellpaging", Tab(completion, "set shellp"));
        Assert.Equal("fav add", Tab(completion, "fav a"));
        Assert.Equal("hot remove", Tab(completion, "hot r"));
        Assert.Equal("insert plot.md bottom", Tab(completion, "insert plot.md b"));
        Assert.Equal("help peek", Tab(completion, "help pe"));
        // Nothing follows: as it was
        Assert.Equal("version x", Tab(completion, "version x"));
    }

    [Fact]
    public void Tab_CompletesPaths_AndDirectoriesOnlyWhereADirectoryIsWanted()
    {
        var completion = Completion();
        File.WriteAllText(In("work", "Draft", "storm.md"), "x");
        Assert.Equal("open plot.md", Tab(completion, "open pl"));
        Assert.Equal("peek Ending.md", Tab(completion, "peek e"));
        // cd: the directories only, by name, then what was typed
        Assert.Equal("cd Draft/", Tab(completion, "cd "));
        Assert.Equal("cd notes/", Tab(completion, "cd ", true, true));
        Assert.Equal("cd ", Tab(completion, "cd ", true, true, true));
        Assert.Equal("cd pl", Tab(completion, "cd pl"));
        // The only one that fits is taken: Tab goes on into the directory
        var into = Completion();
        var line = Tab(into, "open Dr");
        Assert.Equal("open Draft/", line);
        Assert.Equal("open Draft/storm.md", Tab(into, line));
        // The directory of copy and move, and the file of fav add
        Assert.Equal("copy plot.md Draft/", Tab(completion, "copy plot.md Dr"));
        Assert.Equal("fav add plot.md", Tab(completion, "fav add pl"));
    }

    [Fact]
    public void Tab_PutsANameWithBlanksInQuotes_AndLeavesHiddenNamesOut()
    {
        File.WriteAllText(In("work", "my notes.md"), "x");
        Directory.CreateDirectory(In("work", ".hidden"));
        var completion = Completion();
        Assert.Equal("open \"my notes.md\"", Tab(completion, "open my"));
        Assert.Equal("open \"my notes.md\"", Tab(completion, "open \"my n"));
        Assert.DoesNotContain(".hidden", Tab(completion, "cd ", true, true, true));
        Assert.Equal("cd .hidden/", Tab(completion, "cd ."));
    }

    [Theory]
    [InlineData("open pl", "open ", "pl")]
    [InlineData("open ", "open ", "")]
    [InlineData("fa", "", "fa")]
    [InlineData("open \"my no", "open ", "\"my no")]
    [InlineData("rename \"my notes.md\" ne", "rename \"my notes.md\" ", "ne")]
    public void LastWord_IsAfterTheLastBlank_OrFromAnOpenQuote(string text, string before, string word)
    {
        Assert.Equal((before, word), ShellCompletion.LastWord(text));
    }

    // A reader of the keys given (a text: each char a key; named keys between < and >), in a window this wide
    private static (string? Line, string Output, ShellLineReader Reader) ReadLine(string keys, int width = 80,
        Func<string, bool, string>? complete = null)
    {
        var queue = new Queue<ConsoleKeyInfo>(LineKeys(keys));
        var output = new StringWriter();
        var reader = new ShellLineReader(() => queue.Dequeue(), output, () => width,
            complete ?? ((text, _) => text));
        var line = reader.Read("> ");
        return (line, output.ToString(), reader);
    }

    private static IEnumerable<ConsoleKeyInfo> LineKeys(string keys)
    {
        var named = new Dictionary<string, ConsoleKeyInfo>
        {
            ["CR"] = new('\r', ConsoleKey.Enter, false, false, false),
            ["BS"] = new('\b', ConsoleKey.Backspace, false, false, false),
            ["Del"] = new('\0', ConsoleKey.Delete, false, false, false),
            ["Left"] = new('\0', ConsoleKey.LeftArrow, false, false, false),
            ["Right"] = new('\0', ConsoleKey.RightArrow, false, false, false),
            ["Home"] = new('\0', ConsoleKey.Home, false, false, false),
            ["End"] = new('\0', ConsoleKey.End, false, false, false),
            ["Up"] = new('\0', ConsoleKey.UpArrow, false, false, false),
            ["Down"] = new('\0', ConsoleKey.DownArrow, false, false, false),
            ["Tab"] = new('\t', ConsoleKey.Tab, false, false, false),
            ["Esc"] = new('\u001b', ConsoleKey.Escape, false, false, false),
            ["C-c"] = new('\u0003', ConsoleKey.C, false, false, true),
            ["C-d"] = new('\u0004', ConsoleKey.D, false, false, true),
            ["C-u"] = new('\u0015', ConsoleKey.U, false, false, true),
            ["C-w"] = new('\u0017', ConsoleKey.W, false, false, true)
        };
        for (var i = 0; i < keys.Length; i++)
        {
            var close = keys[i] == '<' ? keys.IndexOf('>', i) : -1;
            if (close > i && named.TryGetValue(keys[(i + 1)..close], out var key))
            {
                yield return key;
                i = close;
                continue;
            }

            yield return new ConsoleKeyInfo(keys[i], keys[i] == ' ' ? ConsoleKey.Spacebar : ConsoleKey.A, false, false, false);
        }
    }

    [Theory]
    [InlineData("ls<CR>", "ls")]
    [InlineData("lx<BS>s<CR>", "ls")]
    [InlineData("s<Home>l<CR>", "ls")]
    [InlineData("lxs<Left><Left><Del><End> 2<CR>", "ls 2")]
    [InlineData("peek plot.md<C-w>notes.md<CR>", "peek notes.md")]
    [InlineData("rm plot.md<Home><Right><Right><C-u>ls<CR>", "ls plot.md")]
    [InlineData("something<Esc>pwd<CR>", "pwd")]
    [InlineData("half<C-c>", "")]
    [InlineData("<C-d>", null)]
    [InlineData("ls<C-d><CR>", "ls")]
    public void TheKeysOfTheLine_EditIt(string keys, string? line)
    {
        Assert.Equal(line, ReadLine(keys).Line);
    }

    [Fact]
    public void AltGr_TypesItsCharacters_AsCtrlAltDoes()
    {
        // ($ @ { [ \ with AltGr on many keyboards: Windows tells them as Ctrl+Alt)
        var keys = new Queue<ConsoleKeyInfo>(new[]
        {
            new ConsoleKeyInfo('$', ConsoleKey.D4, false, true, true),
            new ConsoleKeyInfo('@', ConsoleKey.D2, false, true, true),
            new ConsoleKeyInfo('{', ConsoleKey.D7, false, true, true),
            // Ctrl+Alt+C is not Ctrl+C: the line is kept
            new ConsoleKeyInfo('c', ConsoleKey.C, false, true, true),
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)
        });
        var reader = new ShellLineReader(() => keys.Dequeue(), new StringWriter(), () => 80, (text, _) => text);
        Assert.Equal("$@{c", reader.Read("> "));
    }

    [Fact]
    public void Tab_AndUp_OnTheLine()
    {
        CommandHistory.Clear();
        Assert.Equal("favorites", ReadLine("fa<Tab><CR>", complete: Completion().Next).Line);
        CommandHistory.Add(CommandContext.Shell, "peek plot.md");
        CommandHistory.Add(CommandContext.Shell, "ls");
        Assert.Equal("ls", ReadLine("<Up><CR>").Line);
        Assert.Equal("peek plot.md", ReadLine("<Up><Up><CR>").Line);
        // What is typed is what the commands brought back start with
        Assert.Equal("peek plot.md", ReadLine("pe<Up><CR>").Line);
        Assert.Equal("ls", ReadLine("<Up><Up><Down><CR>").Line);
        CommandHistory.Clear();
    }

    [Fact]
    public void ALineWiderThanTheWindow_IsScrolledSideways_OnOneRow()
    {
        var text = string.Concat(Enumerable.Range(0, 6).Select(n => "word" + n + "-------"));
        var (line, output, _) = ReadLine(text + "<CR>", 30);
        Assert.Equal(text, line);
        // Never more than the window on the row, and the end of the line is shown at the end
        Assert.All(output.Split('\r'), row => Assert.True(row.TrimEnd('\n').Length <= 29, row));
        var last = output.Split('\r').Where(row => row.Length > 0).TakeLast(2).First();
        Assert.EndsWith("word5-------", last.TrimEnd());
    }

    [Fact]
    public void ShellCompletion_IsASettingOfTheSettingsFile()
    {
        FileIo.SaveSettings();
        Assert.Equal("true", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shellcompletion"]);
        Run("set shellcompletion false\n");
        Assert.False(Shell.Completion);
    }

    // ---- Starting as the shell by the setting (startshell), and --screen

    [Fact]
    public void StartsAsShell_ByTheSetting_UnlessAFileOrScreenIsGiven()
    {
        // Asked for: always
        Assert.True(StartArgument.StartsAsShell(true, false, new[] { "notes" }, () => false));
        // Set, and nothing given: the shell
        Assert.True(StartArgument.StartsAsShell(false, false, Array.Empty<string>(), () => true));
        // Set, and a file given (as Blyant Validator opens one at a line): the full screen
        Assert.False(StartArgument.StartsAsShell(false, false, new[] { "+12", "page.html" }, () => true));
        // --screen: the full screen, and the setting is not read
        Assert.False(StartArgument.StartsAsShell(false, true, Array.Empty<string>(), () => throw new InvalidOperationException()));
        // Not set
        Assert.False(StartArgument.StartsAsShell(false, false, Array.Empty<string>(), () => false));
    }

    [Fact]
    public void Screen_IsTakenAsAnOption()
    {
        var arguments = new List<string> { "--screen", "notes" };
        Assert.False(StartArgument.TakeShellOptions(arguments, In("work"), out _, out _, out var screen));
        Assert.True(screen);
        Assert.Equal(new[] { "notes" }, arguments);
    }

    [Fact]
    public void StartsInShell_IsReadFromTheSettingsFile_AndAFileThatIsNotThereIsNo()
    {
        Assert.False(FileIo.StartsInShell());
        Shell.StartInShell = true;
        FileIo.SaveSettings();
        Assert.True(FileIo.StartsInShell());
        Assert.Equal("true", FileIo.ParseSettings(FileIo.ReadSettingsFile())["startshell"]);
        Run("set startshell false\n");
        Assert.False(FileIo.StartsInShell());
    }

    // ---- Registers: $name in a command

    [Theory]
    // A register put in; one that is not, and "$5", as typed; $$ is $
    [InlineData("cd $docs", "cd C:/notes")]
    [InlineData("peek $docs/plot.md", "peek C:/notes/plot.md")]
    [InlineData("insert x.md bottom Costs $5 and $nothing", "insert x.md bottom Costs $5 and $nothing")]
    [InlineData("insert x.md bottom $$docs", "insert x.md bottom $docs")]
    // A value with blanks in a path: the word in quotes; in the text after the words, as it is
    [InlineData("copy plot.md $space", "copy plot.md \"C:/my notes\"")]
    [InlineData("copy plot.md $space/old", "copy plot.md \"C:/my notes/old\"")]
    [InlineData("copy plot.md \"$space/old\"", "copy plot.md \"C:/my notes/old\"")]
    [InlineData("insert $space bottom $note", "insert \"C:/my notes\" bottom Buy more ink")]
    [InlineData("reg other $note", "reg other Buy more ink")]
    public void Expand_PutsTheValuesInTheLine(string line, string expected)
    {
        ShellRegisters.Set("docs", "C:/notes");
        ShellRegisters.Set("space", "C:/my notes");
        ShellRegisters.Set("Note", "Buy more ink");
        var words = line.StartsWith("insert") ? 3 : line.StartsWith("reg") ? 2 : int.MaxValue;
        Assert.Equal(expected, ShellRegisters.Expand(line, words));
    }

    [Theory]
    [InlineData("docs", true)]
    [InlineData("Draft_2", true)]
    [InlineData("2nd", false)]
    [InlineData("my docs", false)]
    [InlineData("a-b", false)]
    public void IsName_IsALetterThenLettersDigitsOrUnderscore(string name, bool ok)
    {
        Assert.Equal(ok, ShellRegisters.IsName(name));
    }

    [Fact]
    public void Reg_KeepsATextTheDirectoryOrARow_AndShowsIt()
    {
        var (output, errors, _, _) = Run("reg note Buy more ink\nreg here .\nls\nreg plot #4\nreg note\nreg\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Buy more ink", ShellRegisters.Get("note"));
        Assert.Equal(In("work"), ShellRegisters.Get("here"));
        // Row 4 of ls: plot.md (the folders first)
        Assert.Equal(In("work", "plot.md"), ShellRegisters.Get("plot"));
        // The value alone on its line, as a script reads it; and the list
        Assert.Contains("\nBuy more ink\n", output);
        Assert.Contains("  $here ", output);
        // Kept in the data directory, and read again next time
        ShellRegisters.Clear();
        ShellRegisters.Load();
        Assert.Equal("Buy more ink", ShellRegisters.Get("NOTE"));
    }

    [Fact]
    public void Registers_AreUsedByTheCommands_AsPathsAndAsText()
    {
        Directory.CreateDirectory(In("work", "my old"));
        File.WriteAllText(In("work", "todo.md"), "One\n");
        var (_, errors, _, session) = Run("reg old " + In("work", "my old") + "\nreg note Buy more ink\n" +
                                          "copy plot.md $old\ninsert todo.md bottom $note\ncd $old\n");
        Assert.Equal(string.Empty, errors);
        Assert.True(File.Exists(In("work", "my old", "plot.md")));
        Assert.Equal("One\nBuy more ink\n", File.ReadAllText(In("work", "todo.md")));
        Assert.Equal(In("work", "my old"), session.Directory);
    }

    [Fact]
    public void Reg_TakesRegistersAway_AndTellsWhatItCannotDo()
    {
        var (_, errors, code, _) = Run("reg a one\nreg b two\nreg -d a\nreg -d a\nreg 2nd x\nreg nothing\nreg c #9\nreg -d\n");
        Assert.Null(ShellRegisters.Get("a"));
        Assert.Equal("two", ShellRegisters.Get("b"));
        Assert.Equal(5, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(1, code);
        Run("reg -d all\n");
        Assert.Empty(ShellRegisters.All);
        // Empty: told how to keep one
        var (empty, _, _, _) = Run("reg\n");
        Assert.Contains(Lang.T("No registers (reg name text keeps one)"), empty);
    }

    [Fact]
    public void Registers_AreNeverLogged_OnlyTheirNames()
    {
        Run("reg secret top secret words\ninsert plot.md bottom $secret\n");
        Assert.DoesNotContain(EventLog.All, line => line.Contains("top secret"));
        Assert.Contains(EventLog.All, line => line.EndsWith(DialogText.LogShell + ": register set: secret"));
    }

    [Fact]
    public void Tab_CompletesTheNamesOfTheRegisters()
    {
        ShellRegisters.Set("docs", "x");
        ShellRegisters.Set("draft", "y");
        var completion = Completion();
        Assert.Equal("cd $docs", Tab(completion, "cd $do"));
        Assert.Equal("copy plot.md $draft", Tab(completion, "copy plot.md $dr"));
        Assert.Equal("reg -d", Tab(Completion(), "reg -"));
        Assert.Equal("reg -d all", Tab(Completion(), "reg -d a"));
        Assert.Equal("reg docs", Tab(Completion(), "reg do"));
    }

    // ---- clip: to the clipboard (OSC 52) and the string memory

    // What "clip" sent to the clipboard of the terminal, by the text in it (the sequence decoded)
    private static List<string> Clipboard()
    {
        var sent = new List<string>();
        SystemClipboard.WriteOverride = sequence =>
        {
            var base64 = sequence["\u001b]52;c;".Length..^1];
            sent.Add(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
        };
        return sent;
    }

    [Fact]
    public void Clip_CopiesThePathTheNameTheTextAndLines()
    {
        File.WriteAllText(In("work", "lines.md"), "one\ntwo\nthree\nfour\nfive\n");
        var sent = Clipboard();
        var (output, errors, _, _) = Run("ls\nclip 3\nclip name 3\nclip text plot.md\nclip lines lines.md +2 3\n" +
                                         "clip lines lines.md 4-9\nclip .\n", clipboard: true);
        Assert.Equal(string.Empty, errors);
        // Row 3 of ls: Ending.md (the folders first, then the files by name)
        Assert.Equal(new[]
        {
            In("work", "Ending.md"), "Ending.md", "Plot\n", "two\nthree\nfour", "four\nfive", In("work")
        }, sent);
        Assert.Contains(DialogText.CopiedToClipboardAndMemory, output);
        // And in the string memory: "p" in the editor puts it in
        Assert.Equal(In("work"), Tools.GetCurrentLineFromLineMemory());
    }

    [Fact]
    public void Clip_CopiesTheValueOfARegister_AsItIs()
    {
        var sent = Clipboard();
        var (_, errors, _, _) = Run("reg note Buy more ink\nreg plot #9\nclip $note\nclip name $docs\n", clipboard: true);
        Assert.Equal(new[] { "Buy more ink" }, sent);
        // ($docs is no register: a path that is not there)
        Assert.Contains(In("work", "$docs"), errors);
    }

    [Fact]
    public void Clip_WithoutATerminal_UsesThePasteBinOnly_NeverTheOutput()
    {
        var sent = Clipboard();
        var (output, errors, code, _) = Run("clip plot.md\n");
        Assert.Empty(sent);
        Assert.DoesNotContain("\u001b]52", output);
        Assert.Contains(Lang.T("The clipboard needs a terminal (the output goes to a file or a program): turn on the paste bin"), errors);
        Assert.Equal(1, code);

        SystemClipboard.PasteBin = true;
        SystemClipboard.PasteBinFile = Path.Combine(_data, "bin.txt");
        (_, errors, _, _) = Run("clip name plot.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Empty(sent);
        Assert.Equal("plot.md", File.ReadAllText(Path.Combine(_data, "bin.txt")).TrimEnd());
    }

    [Fact]
    public void Clip_TellsWhatItCannotDo_AndNeverLogsTheText()
    {
        File.WriteAllText(In("work", "secret.md"), "top secret words\n");
        var sent = Clipboard();
        var (_, errors, _, _) = Run("clip\nclip text notes\nclip lines secret.md\nclip lines secret.md 9-12\n" +
                                    "clip lines secret.md x\nclip text nothing.md\nclip text secret.md\n", clipboard: true);
        Assert.Equal(6, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(new[] { "top secret words\n" }, sent);
        Assert.DoesNotContain(EventLog.All, line => line.Contains("top secret"));
        Assert.Contains(EventLog.All, line => line.EndsWith(DialogText.LogShell + ": clip: text"));
    }

    [Fact]
    public void Tab_CompletesWhatClipCopies_AndPaths()
    {
        Assert.Equal("clip text", Tab(Completion(), "clip te"));
        Assert.Equal("clip plot.md", Tab(Completion(), "clip pl"));
        Assert.Equal("clip lines plot.md", Tab(Completion(), "clip lines pl"));
    }
}
