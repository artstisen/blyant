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
        ShellRegisters.Clip = null;
        ShellRegisters.Peek = null;
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
        ShellRegisters.Clip = null;
        ShellRegisters.Peek = null;
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
        // (the favorites and the history by name; the hotlist and the projects with their paths)
        Assert.Contains("  1  plot.md\n", output);
        Assert.Contains("  1  Ending.md  2026-11-13 09:30\n", output);
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
        // (a command with words after it is logged with "...": what follows is never written)
        Assert.Contains(EventLog.All, line => line.EndsWith(DialogText.LogShell + ": :ls ..."));
        Assert.DoesNotContain(EventLog.All, line => line.Contains("secret-folder"));
    }

    [Fact]
    public void FromAFile_AQuestionIsNotAsked_AndNothingIsDone_UnlessYes()
    {
        // "delete plot.md": Blyant asks first (ChoiceBox, asked on lines while the shell runs). Not asked: told on the
        // error output, nothing done, and the command failed
        var (output, errors, code, _) = Run("delete plot.md\n");
        Assert.True(File.Exists(In("work", "plot.md")));
        Assert.Contains(Lang.T("(not asked: the commands are not typed, so nothing is done)"), errors);
        Assert.DoesNotContain(Lang.T("(not asked: the commands are not typed, so nothing is done)"), output);
        Assert.Equal(1, code);
        // With --yes it is done
        (_, _, code, _) = Run("delete plot.md\n", yes: true);
        Assert.False(File.Exists(In("work", "plot.md")));
        Assert.Equal(0, code);
        // "open new.md" without a terminal: not made, as the editor could not open it (also with --yes)
        (_, errors, _, _) = Run("open new.md\n", yes: true);
        Assert.False(File.Exists(In("work", "new.md")));
        Assert.Contains(Lang.F("{0} is not opened: the editor needs a terminal", In("work", "new.md")), errors);
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
        var (_, errors, _, _) = Run("delete plot.md\ndelete Draft\n");
        Assert.Contains(Lang.T("It can be found again in the trash (:trash in the file browser)"), errors);
        Assert.DoesNotContain(DialogText.TrashFileNote + "\n", errors);
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
    [InlineData("+++")]
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
        // (kept for $clip: a script can put it in)
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.T("Kept for $clip (the clipboard needs a terminal)"), output);
        Assert.Equal(In("work", "plot.md"), ShellRegisters.Clip);
        Assert.Equal(0, code);

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

    // ---- shift: lines moved in a file

    private const string Todo = "# Week\n\n## Todo\n\n- [ ] Milk\n- [ ] Ink\n- [ ] Paper\n\n## Done\n\n- [x] Pens\n\n## Notes\nA note\n";

    [Theory]
    [InlineData("4", new[] { 4 })]
    [InlineData("+4", new[] { 4 })]
    [InlineData("4-6", new[] { 4, 5, 6 })]
    [InlineData("9-10,4,7,5", new[] { 4, 5, 7, 9, 10 })]
    public void LineSet_TakesNumbersRangesAndLists(string text, int[] lines)
    {
        Assert.True(ShellSession.LineSet(text, 100, out var found));
        Assert.Equal(lines, found);
    }

    [Theory]
    [InlineData("Milk")]
    [InlineData("4-")]
    [InlineData("0")]
    [InlineData("6-4")]
    public void LineSet_IsFalseForAText(string text)
    {
        Assert.False(ShellSession.LineSet(text, 100, out _));
    }

    [Theory]
    [InlineData("- [ ] Milk", "- [x] Milk")]
    [InlineData("  * [x] Milk", "  * [ ] Milk")]
    [InlineData("1. [X] Milk", "1. [ ] Milk")]
    [InlineData("+ [ ] Milk", "+ [x] Milk")]
    [InlineData("- Milk", "- Milk")]
    [InlineData("[ ] Milk", "[ ] Milk")]
    [InlineData("- [-] Milk", "- [-] Milk")]
    public void Ticked_TicksOrUnticksATaskBox(string line, string expected)
    {
        Assert.Equal(expected, ShellSession.Ticked(line));
    }

    [Fact]
    public void Headings_AreThoseOfMarkdown_NotInCode_OrOfTypst()
    {
        var text = "# One\n```\n# Not\n```\n    # Code\n## Two ##\n#NoSpace\n";
        Assert.Equal(new[] { (1, 1, "One"), (6, 2, "Two") }, ShellSession.Headings(text, false));
        Assert.Equal(new[] { (2, 2, "Done") }, ShellSession.Headings("Text\n== Done\n# not\n", true));
    }

    [Fact]
    public void Shifted_MovesTheLinesBeforeAPlace_UpOrDown()
    {
        // Down: lines 1 and 3 before line 5 (from 0: 4)
        Assert.Equal(("b\nd\na\nc\ne\n", 3, 0), ShellSession.Shifted("a\nb\nc\nd\ne\n", new[] { 1, 3 }, 4, false));
        // Up, and to the end (no line break at the end kept so)
        Assert.Equal(("d\na\nb\nc", 1, 0), ShellSession.Shifted("a\nb\nc\nd", new[] { 4 }, 0, false));
        Assert.Equal(("b\na", 2, 0), ShellSession.Shifted("a\nb", new[] { 1 }, 2, false));
    }

    [Fact]
    public void Shift_MovesLinesUnderAHeading_AndKeepsTheLineEndings()
    {
        File.WriteAllText(In("work", "todo.md"), Todo.Replace("\n", "\r\n"));
        var (output, errors, _, session) = Run("shift todo.md 5-6 done\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Week\n\n## Todo\n\n- [ ] Paper\n\n## Done\n\n- [ ] Milk\n- [ ] Ink\n- [x] Pens\n\n## Notes\nA note\n"
            .Replace("\n", "\r\n"), File.ReadAllText(In("work", "todo.md")));
        Assert.Contains("9  - [ ] Milk\n", output);
        // The file is the last list, at the lines moved
        Assert.Equal((9, 1), session.LastList[0].At);
    }

    [Fact]
    public void ShiftBang_TicksTheBoxes_AndPlusPutsThemAtTheEndOfTheSection()
    {
        File.WriteAllText(In("work", "todo.md"), Todo);
        var (_, errors, _, _) = Run("shift! todo.md Ink \"## Done+\"\nshift! todo.md Pens Todo\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Week\n\n## Todo\n\n- [ ] Pens\n- [ ] Milk\n- [ ] Paper\n\n## Done\n\n- [x] Ink\n\n## Notes\nA note\n",
            File.ReadAllText(In("work", "todo.md")));
    }

    [Fact]
    public void Shift_TakesTopBottomAndALine()
    {
        File.WriteAllText(In("work", "list.md"), "a\nb\nc\nd\n");
        var (_, errors, _, _) = Run("shift list.md 4 top\nshift list.md 1,2 bottom\nshift list.md 3 +1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("d\nb\nc\na\n", File.ReadAllText(In("work", "list.md")));
    }

    [Fact]
    public void Shift_TellsWhatItCannotDo_AndChangesNothing()
    {
        File.WriteAllText(In("work", "todo.md"), Todo);
        var (output, errors, _, _) = Run("shift todo.md 5\nshift todo.md 5 Later\nshift todo.md \"[ ]\" Done\n" +
                                         "shift todo.md Nothing Done\nshift todo.md 40 Done\nshift todo.md 2-3 Todo\n" +
                                         "shift todo.md 4-6 +5\nshift todo.md 5 Todo\n");
        Assert.Equal(7, errors.TrimEnd('\n').Split('\n').Length);
        // Line 5 is under Todo already
        Assert.Contains(Lang.T("Nothing moved: the lines are there already"), output);
        Assert.Equal(Todo, File.ReadAllText(In("work", "todo.md")));
        Assert.DoesNotContain(EventLog.All, line => line.Contains("Milk"));
    }

    [Fact]
    public void Tab_CompletesTheHeadingsOfTheFile_AfterTheLines()
    {
        File.WriteAllText(In("work", "todo.md"), Todo + "## Done today\n");
        Assert.Equal("shift todo.md 5 Notes", Tab(Completion(), "shift todo.md 5 No"));
        Assert.Equal("shift todo.md 5 bottom", Tab(Completion(), "shift todo.md 5 bo"));
        var completion = Completion();
        Assert.Equal("shift todo.md 5 Done", Tab(completion, "shift todo.md 5 Do"));
        Assert.Equal("shift todo.md 5 \"Done today\"", completion.Next("shift todo.md 5 Done", true));
    }

    // ---- Review of 1.5.0: the shell made solid

    [Fact]
    public void ACommandThatFails_UnexpectedlyIsTold_AndTheShellGoesOn()
    {
        // (a range as large as this was made into a list of lines, and Blyant ran out of memory)
        File.WriteAllText(In("work", "list.md"), "a\nb\n");
        var (output, errors, code, _) = Run("shift list.md 1-2147483647 top\nshift list.md 2147483647 top\npwd\n");
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "list.md"), 2), errors);
        Assert.Contains(In("work") + "\n", output);
        Assert.Equal(1, code);
    }

    [Fact]
    public void AFileOfCommands_WithAByteOrderMark_Runs()
    {
        var (output, errors, _, _) = Run("\uFEFFpwd\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work") + "\n", output);
    }

    [Fact]
    public void Peek_CountsTheLines_AsChangeShiftAndClipDo()
    {
        File.WriteAllText(In("work", "two.md"), "a\nb\n");
        var (output, errors, _, _) = Run("peek two.md\nchange two.md +3 X\n");
        Assert.DoesNotContain("  3  ", output);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "two.md"), 2), errors);
    }

    [Fact]
    public void RenameMoveAndDeleteOfTheDirectoryItIsIn_TheShellFollows()
    {
        Directory.CreateDirectory(In("work", "d1", "sub"));
        var (_, errors, _, session) = Run("cd d1/sub\nrename .. d2\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "d2", "sub"), session.Directory);
        (_, errors, _, session) = Run("cd d2\nmove . ../notes\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "notes", "d2"), session.Directory);
        // (the question not asked is told on the error output)
        (_, _, var code, session) = Run("cd notes/d2/sub\ndelete ..\n", yes: true);
        Assert.Equal(0, code);
        Assert.False(Directory.Exists(In("work", "notes", "d2")));
        Assert.Equal(In("work", "notes"), session.Directory);
    }

    [Fact]
    public void AFileWithMixedLineEndings_IsNotChanged()
    {
        File.WriteAllText(In("work", "mixed.md"), "one\r\ntwo\nthree\r\n");
        var (_, errors, _, _) = Run("insert mixed.md bottom new\nchange mixed.md +1 x\nshift mixed.md 1 bottom\ntrim mixed.md\n");
        Assert.Equal(4, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal("one\r\ntwo\nthree\r\n", File.ReadAllText(In("work", "mixed.md")));
    }

    [Fact]
    public void FormatAndTrim_LeaveTheFilesOfBlyant()
    {
        var settings = Path.Combine(_data, FileIo.SettingsFileName);
        File.WriteAllText(settings, "a=1   \n");
        var (_, errors, _, _) = Run("trim \"" + settings + "\"\n");
        Assert.NotEqual(string.Empty, errors);
        Assert.Equal("a=1   \n", File.ReadAllText(settings));
    }

    [Fact]
    public void Reg_ATextInQuotes_IsKeptWithoutThem()
    {
        Directory.CreateDirectory(In("work", "My Docs"));
        var (_, errors, _, _) = Run("reg docs \"" + In("work", "My Docs") + "\"\ncopy plot.md $docs\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "My Docs"), ShellRegisters.Get("docs"));
        Assert.True(File.Exists(In("work", "My Docs", "plot.md")));
    }

    [Fact]
    public void FavAndHotRemove_TakeOffAnEntryThatIsGone()
    {
        Directory.CreateDirectory(In("work", "gone"));
        File.WriteAllText(In("work", "gone.md"), "x");
        Run("fav add gone.md\nhot add gone\n");
        File.Delete(In("work", "gone.md"));
        Directory.Delete(In("work", "gone"));
        var (_, errors, _, _) = Run("fav remove gone.md\nhot remove gone\n");
        Assert.Equal(string.Empty, errors);
        Assert.False(FileFavorites.Contains(In("work", "gone.md")));
        Assert.DoesNotContain(DirectoryHotlist.Items, entry => FileOperations.SamePath(entry.Path, In("work", "gone")));
    }

    [Fact]
    public void Shift_AnEmptyText_IsNotTakenForEveryLine()
    {
        File.WriteAllText(In("work", "list.md"), "a\nb\n");
        var (_, errors, _, _) = Run("shift list.md \"\" top\n");
        Assert.Contains(Lang.F("Give a file, the lines and the place: {0} todo.md 4-6 Done", "shift"), errors);
        Assert.Equal("a\nb\n", File.ReadAllText(In("work", "list.md")));
    }

    [Fact]
    public void Open_ANameInQuotes_KeepsItsBlanks()
    {
        File.WriteAllText(In("work", "a  b.md"), "x");
        var (_, errors, _, _) = Run("open \"a  b.md\"\n");
        // (found: only the terminal is missing)
        Assert.Contains(Lang.F("{0} is not opened: the editor needs a terminal", In("work", "a  b.md")), errors);
    }

    [Fact]
    public void Output_ControlCharactersOfAFile_AreNotWritten()
    {
        File.WriteAllText(In("work", "esc.md"), "Title\u001b]52;c;aGk=\u0007 end\n");
        var (output, _, _, _) = Run("peek esc.md\n");
        // (compared by the characters: a comparison of the culture passes over control characters)
        Assert.DoesNotContain('\u001b', output);
        Assert.DoesNotContain('\u0007', output);
        Assert.Contains("Title?]52;c;aGk=? end", output);
    }

    [Fact]
    public void Set_AValueInQuotes_AndOneWrittenAsBlyantWritesIt()
    {
        FileIo.SaveSettings();
        var (output, errors, _, _) = Run("set shelldir ~/x\nset shelldir \"\"\nset indent 02\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(string.Empty, Shell.StartSetting);
        Assert.Contains("  indent = 2", output);
    }

    [Fact]
    public void AQuestionAfterQAtAPause_IsShown()
    {
        var output = new StringWriter();
        var keys = new Queue<ConsoleKeyInfo>(new[] { new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false) });
        var session = new ShellSession(new StringReader("y\n"), output, new StringWriter(), In("work"), true)
        {
            WindowSize = () => (80, 5),
            ReadKey = keys.Dequeue
        };
        for (var i = 0; i < 10; i++) File.WriteAllText(In("work", "f" + i + ".md"), "x");
        Assert.True(session.Execute("ls"));
        Assert.Equal(0, session.Pick("", "Go on?", "", [DialogText.ChoiceYes, DialogText.ChoiceNo], ["Y", "N"], 1, false));
        Assert.Contains("Go on?", output.ToString());
    }

    [Theory]
    [InlineData("--Shell")]
    [InlineData("--SHELL")]
    public void ShellOptions_AreTakenInAnyLetterCase(string option)
    {
        var arguments = new List<string> { option, "--Yes" };
        Assert.True(StartArgument.TakeShellOptions(arguments, In("work"), out var yes, out _, out _));
        Assert.True(yes);
        Assert.Empty(arguments);
    }

    [Fact]
    public void Run_WithoutACommandAfterIt_IsTold()
    {
        var output = new StringWriter();
        Assert.Equal(1, StartArgument.AnswerOption("--run", output));
        Assert.Contains("--run needs a command", output.ToString());
    }

    [Fact]
    public void Printable_KeepsLineBreaksAndTabs_AndHidesTheOtherControlCharacters()
    {
        Assert.Equal("a\tb\nc?d?e", ShellSession.Printable("a\tb\nc\u001bd\re"));
    }

    [Fact]
    public void LineReader_InANarrowWindow_KeepsToOneRow()
    {
        var keys = new Queue<ConsoleKeyInfo>("hello world".Select(c => new ConsoleKeyInfo(c, ConsoleKey.A, false, false, false))
            .Append(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        var output = new StringWriter();
        var reader = new ShellLineReader(keys.Dequeue, output, () => 12, (text, _) => text);
        Assert.Equal("hello world", reader.Read("blyant ~/notes> "));
        // Every row written (between carriage returns) fits in the 11 columns used
        Assert.All(output.ToString().Split('\r', '\n'), row => Assert.True(TextMetrics.WidthOf(row) <= 11, row));
    }

    [Fact]
    public void LineReader_CtrlZ_EndsTheInput_OnlyOnWindows()
    {
        var keys = new Queue<ConsoleKeyInfo>(new[]
        {
            new ConsoleKeyInfo('\u001a', ConsoleKey.Z, false, false, true),
            new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)
        });
        var reader = new ShellLineReader(keys.Dequeue, new StringWriter(), () => 80, (text, _) => text);
        var line = reader.Read("> ");
        if (OperatingSystem.IsWindows()) Assert.Null(line);
        else Assert.Equal(string.Empty, line);
    }

    // ---- Files edited by hand: the registers read again, and "reload"

    [Fact]
    public void Registers_EditedByHand_AreReadAgainBeforeTheNextCommand()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        var output = new StringWriter();
        var input = new StringReader(string.Empty);
        var session = new ShellSession(input, output, new StringWriter(), In("work"), false);
        session.Execute("reg note Buy ink");
        // Edited in another program: a value changed, one added, and a line that is not a register
        File.WriteAllText(file, "note\tBuy paper\ndocs\t" + In("work", "Draft") + "\nnot a register\n");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(1));
        session.Execute("reg");
        Assert.Contains("  $note Buy paper", output.ToString());
        Assert.Contains(Lang.F("(1 line of {0} is not a register: a name, a tab, and the value)",
            ShellRegisters.FileName), output.ToString());
        session.Execute("cd $docs");
        Assert.Equal(In("work", "Draft"), session.Directory);
    }

    [Fact]
    public void Registers_WrittenByTheShell_AreNotReadAgain_AndNothingIsLost()
    {
        var session = new ShellSession(new StringReader(string.Empty), new StringWriter(), new StringWriter(),
            In("work"), false);
        session.Execute("reg a one");
        session.Execute("reg b two");
        Assert.False(ShellRegisters.Refresh());
        Assert.Equal("one", ShellRegisters.Get("a"));
        Assert.Equal("two", ShellRegisters.Get("b"));
    }

    [Fact]
    public void Reload_ReadsTheSettingsFileAgain_AndTellsAValueNotTaken()
    {
        FileIo.SaveSettings();
        Assert.Equal(EditorEngine.DefaultIndent, EditorEngine.IndentSetting);
        // Edited by hand: indent 2, and a value shellpaging cannot have
        var text = FileIo.ReadSettingsFile().Replace("indent=" + EditorEngine.DefaultIndent, "indent=2")
            .Replace("shellpaging=true", "shellpaging=often");
        FileIo.WriteSettingsFile(text);
        File.WriteAllText(Path.Combine(_data, ShellRegisters.FileName), "kept\tyes\n");
        var (output, errors, _, _) = Run("reload\n");
        Assert.Equal(2, EditorEngine.IndentSetting);
        Assert.Equal("yes", ShellRegisters.Get("kept"));
        Assert.Contains(Lang.T("Settings and registers read again"), output);
        Assert.Contains("shellpaging", errors);
        Assert.Contains("often", errors);
        Assert.DoesNotContain("indent", errors);
    }

    [Fact]
    public void IsSettingsFile_IsTrueForTheSettingsFileOfBlyant_Only()
    {
        Assert.True(FileIo.IsSettingsFile(Path.Combine(_data, FileIo.SettingsFileName)));
        Assert.True(FileIo.IsSettingsFile(Path.Combine(_data, ".", FileIo.SettingsFileName)));
        Assert.False(FileIo.IsSettingsFile(In("work", FileIo.SettingsFileName)));
        Assert.False(FileIo.IsSettingsFile(string.Empty));
    }

    [Fact]
    public void Reload_TakesNothingAfterIt()
    {
        var (_, errors, _, _) = Run("reload now\n");
        Assert.Contains(Lang.T("reload takes nothing after it"), errors);
    }

    [Fact]
    public void About_WritesTheVersionAndTheCommandsUsedTheMost_AndTheLicense()
    {
        var (output, errors, _, _) = Run("about\n");
        Assert.Equal(string.Empty, errors);
        Assert.StartsWith(DialogText.ApplicationName + " " + DialogText.ApplicationVersion + " " + DialogText.ApplicationBy,
            output);
        Assert.Contains("  help [command]  ", output);
        Assert.Contains("  set language de  ", output);
        // (the about page of the full screen tells of its keys: not shown here)
        Assert.DoesNotContain(TextBuffer.Normalize(DialogText.About).Split('\n')[0], output);
        // The screen is 79 columns
        Assert.All(output.TrimEnd('\n').Split('\n'), line => Assert.True(TextMetrics.WidthOf(line) <= 79, line));
        (output, _, _, _) = Run("about license\n");
        Assert.Contains("MIT", output);
        (_, errors, _, _) = Run("about more\n");
        Assert.Contains(Lang.T("Give nothing, or license: about license"), errors);
        Assert.Equal("about license", Tab(Completion(), "about l"));
    }

    [Theory]
    [InlineData("set indent=2")]
    [InlineData("set indent = 2")]
    [InlineData("set indent 2")]
    public void Set_TakesNameEqualsValue_AsInTheSettingsFile(string line)
    {
        FileIo.SaveSettings();
        var (output, errors, _, _) = Run(line + "\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  indent = 2", output);
    }

    // ---- Review: registers read again by their text, a file that cannot be read, lines kept

    // A shell to run commands in one by one, and what it writes
    private (ShellSession Session, StringWriter Output, StringWriter Errors) Session()
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        return (new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false), output, errors);
    }

    [WindowsFact]
    public void Registers_AFileThatCannotBeReadForAMoment_IsNotWrittenOver_Windows()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "a\tone\nb\ttwo\n");
        var (session, _, errors) = Session();
        ShellRegisters.Load();
        File.WriteAllText(file, "a\tone\nb\ttwo\nc\tthree\n");
        // Another program has the file open, as while it saves it
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(ShellRegisters.Refresh());
            Assert.Equal("one", ShellRegisters.Get("a"));
        }

        // Read again now: nothing lost
        session.Execute("reg d four");
        Assert.Equal(string.Empty, errors.ToString());
        Assert.Equal("a\tone\nb\ttwo\nc\tthree\nd\tfour\n", File.ReadAllText(file));
    }

    [WindowsFact]
    public void Registers_NeverReadAtTheStart_AreNotWrittenOverTheFile_Windows()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "a\tone\n");
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            ShellRegisters.Load();
            ShellRegisters.Set("b", "two");
            Assert.NotEqual(DialogText.Success, ShellRegisters.Save());
        }

        Assert.Equal("a\tone\n", File.ReadAllText(file));
    }

    [UnixFact]
    public void Registers_AFileThatCannotBeRead_IsNotWrittenOver_Unix()
    {
        if (!OperatingSystem.IsWindows())
        {
            var file = Path.Combine(_data, ShellRegisters.FileName);
            File.WriteAllText(file, "a\tone\n");
            File.SetUnixFileMode(file, UnixFileMode.None);
            try
            {
                // (as root every file can be read: nothing to try then)
                if (Environment.UserName == "root") return;
                ShellRegisters.Load();
                ShellRegisters.Set("b", "two");
                Assert.NotEqual(DialogText.Success, ShellRegisters.Save());
            }
            finally
            {
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            Assert.Equal("a\tone\n", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Registers_LinesThatAreNotRegisters_AreKeptInTheFile()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "a\tone\nmistyped line\n2b\tbad name\n");
        var (session, output, _) = Session();
        ShellRegisters.Load();
        session.Execute("reg c three");
        Assert.Equal("mistyped line\n2b\tbad name\na\tone\nc\tthree\n", File.ReadAllText(file));
        session.Execute("reg");
        Assert.Contains(Lang.F("({0} lines of {1} are not registers: a name, a tab, and the value)", 2,
            ShellRegisters.FileName), output.ToString());
    }

    [Fact]
    public void Registers_AChangeOfTheSameSizeAndTime_IsSeen()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "a\tone\n");
        ShellRegisters.Load();
        var time = File.GetLastWriteTimeUtc(file);
        File.WriteAllText(file, "a\ttwo\n");
        File.SetLastWriteTimeUtc(file, time);
        Assert.True(ShellRegisters.Refresh());
        Assert.Equal("two", ShellRegisters.Get("a"));
    }

    [Fact]
    public void Tab_AfterADollar_UsesTheFileEditedByHand()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "a\tone\n");
        ShellRegisters.Load();
        File.WriteAllText(file, "a\tone\nnotes\t" + In("work") + "\n");
        Assert.Equal("cd $notes", Tab(Completion(), "cd $no"));
    }

    [Fact]
    public void Reload_ASettingsFileWithNoSettings_ChangesNothing()
    {
        FileIo.SaveSettings();
        var before = FileIo.SettingsText().Split('\n').Skip(1).ToList();
        FileIo.WriteSettingsFile("; nothing here\n");
        var (output, errors, _, _) = Run("reload\n");
        Assert.Contains(Lang.F("{0} has no settings: the settings stay as they were", FileIo.SettingsFileName), errors);
        Assert.DoesNotContain(Lang.T("Settings and registers read again"), output);
        Assert.Equal(before, FileIo.SettingsText().Split('\n').Skip(1).ToList());
    }

    [Fact]
    public void Ls_LeavesOutHiddenFiles_AndLsDashALists_Them()
    {
        var hidden = In("work", ".board.txt");
        File.WriteAllText(hidden, "x");
        // (on Windows a file is hidden by its attribute; on Linux and macOS by the "." of its name)
        if (OperatingSystem.IsWindows()) File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
        var (output, errors, _, _) = Run("ls\n");
        Assert.Equal(string.Empty, errors);
        Assert.DoesNotContain(".board.txt", output);
        (output, _, _, _) = Run("ls -a\nls -a Draft\n");
        Assert.Contains(".board.txt", output);
        Assert.Contains(In("work", "Draft") + "\n", output);
    }

    // ---- $clip: the text clip copied last, as a register

    [Fact]
    public void ClipVariable_PutsInTheLinesCopied_WithInsertAndChange()
    {
        File.WriteAllText(In("work", "lines.md"), "one\ntwo\nthree\nfour\n");
        File.WriteAllText(In("work", "plan.md"), "a\nb\n");
        var (output, errors, _, _) = Run("clip lines lines.md 2-3\ninsert plan.md bottom $clip\n" +
                                         "change plan.md +1 $clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("two\nthree\nb\ntwo\nthree\n", File.ReadAllText(In("work", "plan.md")));
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "plan.md"), 3, 4), output);
    }

    [Fact]
    public void ClipVariable_TheTextOfAWholeFile_PutsInItsLines()
    {
        File.WriteAllText(In("work", "plan.md"), "a\n");
        var (_, errors, _, _) = Run("clip text Ending.md\ninsert plan.md top $clip\nclip name plot.md\n" +
                                    "insert plan.md bottom See $clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("End\na\nSee plot.md\n", File.ReadAllText(In("work", "plan.md")));
    }

    [Fact]
    public void ClipVariable_AsAPath_AndInClipAgain()
    {
        var sent = Clipboard();
        var (output, errors, _, _) = Run("clip Ending.md\npeek $clip\nclip $clip\n", clipboard: true);
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  End", output);
        Assert.Equal(new[] { In("work", "Ending.md"), In("work", "Ending.md") }, sent);
    }

    [Fact]
    public void ClipVariable_NothingCopied_OrLinesWhereOneLineGoes_IsTold()
    {
        var (_, errors, code, _) = Run("insert plot.md bottom $clip\n");
        Assert.Contains(Lang.T("Nothing copied yet: clip copies a text for $clip"), errors);
        Assert.Equal(1, code);
        Assert.Equal("Plot\n", File.ReadAllText(In("work", "plot.md")));

        File.WriteAllText(In("work", "lines.md"), "one\ntwo\n");
        (_, errors, _, _) = Run("clip text lines.md\ncd $clip\nreg note $clip\n");
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg and run take it", "clip"), errors);
        // (a register keeps lines)
        Assert.Equal("one\ntwo", ShellRegisters.Get("note"));
        // ($$clip is the text "$clip")
        (_, errors, _, _) = Run("insert plot.md bottom $$clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Plot\n$clip\n", File.ReadAllText(In("work", "plot.md")));
    }

    [Fact]
    public void ClipVariable_IsNoRegister_ToSetOrTakeAway_AndIsNotWritten()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "clip\tfrom the file\n");
        ShellRegisters.Load();
        Assert.Null(ShellRegisters.Get("clip"));
        var (output, errors, _, _) = Run("reg clip something\nreg -d clip\nclip name plot.md\nreg clip\nreg\nreg a one\n");
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.T("$clip is the text clip copied last: it is not a register to set or take away"), errors);
        Assert.Contains("plot.md\n", output);
        Assert.Contains("  $clip " + Lang.F("{0}  (the last clip)", "plot.md"), output);
        // (the line of the file is kept as it was, $clip is not written)
        Assert.Equal("clip\tfrom the file\na\tone\n", File.ReadAllText(file));
    }

    [Fact]
    public void ClipVariable_InTheList_ShowsTheFirstLineAndHowMany()
    {
        File.WriteAllText(In("work", "lines.md"), new string('x', 60) + "\ntwo\n");
        var (output, _, _, _) = Run("clip text lines.md\nreg\n");
        Assert.Contains("  $clip " + Lang.F("{0}  ({1} lines, the last clip)", new string('x', 40) + "...", 2), output);
        // ($clip is under the names set by Blyant, after the registers)
        Assert.Contains(Lang.T("Set by Blyant:"), output);
        File.WriteAllText(In("work", "lines.md"), "one.\ntwo\n");
        (output, _, _, _) = Run("clip text lines.md\nreg\nhelp $clip\n");
        Assert.Contains("  $clip " + Lang.F("{0}  ({1} lines, the last clip)", "one. ...", 2), output);
        Assert.Contains("clip ... $clip", output);
    }

    [Fact]
    public void Tab_AfterADollar_OffersClip_OnceSomethingIsCopied()
    {
        Assert.Equal("insert plot.md top $cl", Tab(Completion(), "insert plot.md top $cl"));
        ShellRegisters.Clip = "x";
        Assert.Equal("insert plot.md top $clip", Tab(Completion(), "insert plot.md top $cl"));
    }

    [Theory]
    [InlineData("a $clip b", true)]
    [InlineData("a $CLIP", true)]
    [InlineData("a \"$clip/x\"", true)]
    [InlineData("a $$clip", false)]
    [InlineData("a $clips", false)]
    [InlineData("a clip", false)]
    public void Mentions_FindsDollarClip_AsExpandDoes(string line, bool expected)
    {
        Assert.Equal(expected, ShellRegisters.Mentions(line, ShellRegisters.ClipName));
    }

    [Theory]
    [InlineData("a\nb\n", "x\ny", 2, "a\nx\ny\nb\n", 2)]
    // (each line break starts a line: a blank line at the end of $clip is put in)
    [InlineData("a\nb\n", "x\ny\n", 9, "a\nb\nx\ny\n\n", 3)]
    [InlineData("", "x\r\ny", 1, "x\ny\n", 1)]
    public void WithLine_ATextOfLines_PutsInEachLine(string text, string line, int at, string expected, int number)
    {
        Assert.Equal((expected, number), ShellSession.WithLine(text, line, at));
    }

    [Fact]
    public void ARowNotInTheList_OfOneRow_IsToldAsOneRow()
    {
        var (_, errors, _, _) = Run("peek plot.md\nopen 3\nreg x #2\nls\nopen 9\n");
        Assert.Contains(Lang.F("No row {0}: the last list has 1 row", 3), errors);
        Assert.Contains(Lang.F("No row {0}: the last list has 1 row", 2), errors);
        // (ls: the rows of the folder of the tests, more than one)
        var rows = new DirectoryInfo(In("work")).EnumerateFileSystemInfos()
            .Count(entry => (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0);
        Assert.Contains(Lang.F("No row {0}: the last list has {1} rows", 9, rows), errors);
    }

    // ---- Tab: the files opened last first, and patterns

    // A folder "tabs" with a.md, b.md, c.md, Blyant-v151.md, notes-151.md, .h151, and the folders sub1 and sub2
    private void TabFolder()
    {
        Directory.CreateDirectory(In("work", "tabs", "sub1"));
        Directory.CreateDirectory(In("work", "tabs", "sub2"));
        foreach (var name in new[] { "a.md", "b.md", "c.md", "Blyant-v151.md", "notes-151.md", ".h151" })
            File.WriteAllText(In("work", "tabs", name), "x\n");
        File.WriteAllText(In("work", "tabs", "sub2", "x.md"), "x\n");
    }

    [Fact]
    public void Tab_WithNothingTyped_OffersTheFilesOpenedLastFirst()
    {
        TabFolder();
        FileHistory.Add(In("work", "tabs", "a.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Add(In("work", "tabs", "c.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Add(In("work", "plot.md"), new DateTime(2026, 11, 13, 11, 0, 0));
        FileHistory.Save();
        var completion = Completion();
        // c.md opened last, then a.md; then the others by name (plot.md is in another directory)
        Assert.Equal("open tabs/c.md", Tab(completion, "open tabs/"));
        Assert.Equal("open tabs/a.md", Tab(completion, "open tabs/", true, true));
        Assert.Equal("open tabs/b.md", Tab(completion, "open tabs/", true, true, true));
        // A letter typed: by name, as before
        Assert.Equal("open tabs/a.md", Tab(completion, "open tabs/", true, true));
        Assert.Equal("peek tabs/b.md", Tab(Completion(), "peek tabs/b"));
        // In the directory the shell is in
        Assert.Equal("open plot.md", Tab(Completion(), "open "));
    }

    [Fact]
    public void Tab_ADirectoryWithNothingTyped_OffersThoseOfTheFilesOpenedLastFirst()
    {
        TabFolder();
        FileHistory.Add(In("work", "tabs", "sub2", "x.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Save();
        Assert.Equal("cd tabs/sub2/", Tab(Completion(), "cd tabs/"));
        Assert.Equal("cd tabs/", Tab(Completion(), "cd "));
    }

    [Fact]
    public void Tab_TheFilesOpenedLastFirst_CanBeTurnedOff()
    {
        TabFolder();
        FileHistory.Add(In("work", "tabs", "c.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Save();
        FileIo.SaveSettings();
        Run("set shellrecentfirst false\n");
        Assert.False(Shell.RecentFirst);
        Assert.Equal("open tabs/a.md", Tab(Completion(), "open tabs/"));
    }

    [Fact]
    public void Tab_APatternWithStarOrQuestionMark_CompletesTheNamesItFits()
    {
        TabFolder();
        var completion = Completion();
        Assert.Equal("open tabs/Blyant-v151.md", Tab(completion, "open tabs/*151*"));
        Assert.Equal("open tabs/notes-151.md", Tab(completion, "open tabs/*151*", true, true));
        // After the last: the pattern as typed (a hidden name only for a pattern that starts with ".")
        Assert.Equal("open tabs/*151*", Tab(completion, "open tabs/*151*", true, true, true));
        Assert.Equal("open tabs/a.md", Tab(Completion(), "open tabs/?.MD"));
        Assert.Equal("open tabs/.h151", Tab(Completion(), "open tabs/.*1"));
        Assert.Equal("cd tabs/sub1/", Tab(Completion(), "cd tabs/*1"));
        Assert.Equal("open *151*", Tab(Completion(), "open *151*"));
    }

    // ---- lines: lines copied or cut to another file

    private string Text(string name) => File.ReadAllText(In("work", name));

    [Fact]
    public void Lines_CopiesLinesToAnotherFile_AtThePlace()
    {
        File.WriteAllText(In("work", "notes.md"), "one\ntwo\nthree\nfour\n");
        File.WriteAllText(In("work", "plan.md"), "# Plan\n\n- a\n\n## Done\n\n- b\n");
        var (output, errors, _, _) = Run("lines notes.md 2-3 > plan.md bottom\nlines notes.md 1 > plan.md top copy\n" +
                                         "lines notes.md +3 9 > plan.md Done\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\nthree\nfour\n", Text("notes.md"));
        Assert.Equal("one\n# Plan\n\n- a\n\n## Done\n\nthree\nfour\n- b\ntwo\nthree\n", Text("plan.md"));
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "plan.md"), 8, 9), output);
    }

    [Fact]
    public void Lines_Cut_MovesTheLines_ByNumberOrText()
    {
        File.WriteAllText(In("work", "todo.md"), "- [ ] Buy milk\n- [ ] Call Anna\n- [ ] Write\n");
        File.WriteAllText(In("work", "done.md"), "# Done\n\n- x\n");
        var (output, errors, _, _) = Run("lines todo.md \"Call Anna\" > done.md Done+ cut\nlines todo.md 1,2 > done.md +1 CUT\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("", Text("todo.md"));
        Assert.Equal("- [ ] Buy milk\n- [ ] Write\n# Done\n\n- x\n- [ ] Call Anna\n", Text("done.md"));
        Assert.Contains(Lang.F("{0}: line {1} taken out", In("work", "todo.md"), 2), output);
        Assert.Contains(Lang.F("{0}: {1} lines taken out", In("work", "todo.md"), 2), output);
        // The other file is the last list, at the lines put in
        var copied = Run("lines done.md 1-2 > todo.md top\n").Session;
        Assert.Equal(In("work", "todo.md"), copied.LastList.Single().Path);
        Assert.Equal((1, 1), copied.LastList.Single().At);
    }

    [Fact]
    public void Lines_AHeadingNamedCut_InQuotes_IsThePlace()
    {
        File.WriteAllText(In("work", "notes.md"), "one\ntwo\n");
        File.WriteAllText(In("work", "plan.md"), "# Cut\n\n- a\n");
        var (_, errors, _, _) = Run("lines notes.md 1 > plan.md \"Cut\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\n", Text("notes.md"));
        Assert.Equal("# Cut\n\none\n- a\n", Text("plan.md"));
    }

    [Fact]
    public void Lines_TellsWhatItCannotDo_AndChangesNothing()
    {
        File.WriteAllText(In("work", "notes.md"), "one\ntwo\n");
        File.WriteAllText(In("work", "plan.md"), "# Plan\n");
        var (_, errors, code, _) = Run("lines notes.md 1 plan.md bottom\nlines notes.md 1 > notes.md top\n" +
                                       "lines notes.md 9 > plan.md top\nlines notes.md 1 > plan.md Nowhere cut\n" +
                                       "lines notes.md 1 > nothing.md top\nlines notes.md 1 3 > plan.md top\n" +
                                       "lines notes.md 1 > plan.md\n");
        Assert.Equal(7, errors.TrimEnd('\n').Split('\n').Count(line => !line.StartsWith("  ")));
        Assert.Contains(Lang.T("The lines are in that file already: shift moves lines in a file"), errors);
        Assert.Equal(1, code);
        Assert.Equal("one\ntwo\n", Text("notes.md"));
        Assert.Equal("# Plan\n", Text("plan.md"));
    }

    [Fact]
    public void Lines_KeepsTheLineEndingsOfEachFile_AndNeverLogsTheText()
    {
        File.WriteAllText(In("work", "notes.md"), "zebrawords one\r\nzebrawords two\r\n");
        File.WriteAllText(In("work", "plan.md"), "a\nb");
        var (_, errors, _, _) = Run("lines notes.md 1 > plan.md bottom cut\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("zebrawords two\r\n", Text("notes.md"));
        Assert.Equal("a\nb\nzebrawords one", Text("plan.md"));
        Assert.DoesNotContain(EventLog.All, line => line.Contains("zebrawords"));
    }

    [Fact]
    public void Tab_AfterLines_CompletesTheOtherFile_ThePlace_AndCut()
    {
        File.WriteAllText(In("work", "plan.md"), "# Plan\n\n## Done later\n");
        var completion = Completion();
        Assert.Equal("lines plot.md", Tab(completion, "lines plo"));
        Assert.Equal("lines plot.md 1 > plan.md", Tab(Completion(), "lines plot.md 1 > pla"));
        Assert.Equal("lines plot.md 1 > plan.md bottom", Tab(Completion(), "lines plot.md 1 > plan.md bo"));
        Assert.Equal("lines plot.md 1 > plan.md \"Done later\"", Tab(Completion(), "lines plot.md 1 > plan.md Do"));
        Assert.Equal("lines plot.md 1 > plan.md top cut", Tab(Completion(), "lines plot.md 1 > plan.md top cu"));
    }

    [Theory]
    [InlineData("a\nb\n", 1, "a\nx\ny\nb\n", 2)]
    [InlineData("a\nb", 2, "a\nb\nx\ny", 3)]
    [InlineData("", 0, "x\ny\n", 1)]
    public void WithLinesAt_PutsInTheLines(string text, int position, string expected, int line)
    {
        Assert.Equal((expected, line), ShellSession.WithLinesAt(text, new[] { "x", "y" }, position));
    }

    [Theory]
    [InlineData("a\nb\nc\n", new[] { 2 }, "a\nc\n")]
    [InlineData("a\nb\nc", new[] { 1, 3 }, "b")]
    [InlineData("a\n", new[] { 1 }, "")]
    public void WithoutLines_TakesOutTheLines(string text, int[] taken, string expected)
    {
        Assert.Equal(expected, ShellSession.WithoutLines(text, taken));
    }

    // ---- Lists by name ("-l": full paths), and info keeps the last list

    [Fact]
    public void TheLists_ShowNames_TheFolderOfANameTwice_AndFullPathsWithDashL()
    {
        Directory.CreateDirectory(In("work", "novel"));
        File.WriteAllText(In("work", "novel", "plot.md"), "x\n");
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Toggle(In("work", "novel", "plot.md"));
        FileFavorites.Toggle(In("work", "Draft"));
        FileFavorites.Toggle(In("work", "gone.md"));
        FileFavorites.Save();
        FileHistory.Add(In("work", "Ending.md"), new DateTime(2026, 11, 13, 9, 30, 0));
        FileHistory.Add(In("work", "novel", "plot.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Save();

        var (output, errors, _, _) = Run("fav\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  plot.md (work)\n", output);
        Assert.Contains("  plot.md (novel)\n", output);
        Assert.Contains("  Draft/\n", output);
        Assert.Contains("  gone.md " + DialogText.FavoriteMissing + "\n", output);
        (output, _, _, _) = Run("fav -l\nrecent\nhist -l\n");
        Assert.Contains("  " + In("work", "novel", "plot.md") + "\n", output);
        // The dates lined up after the names (plot.md of novel is a favorite: marked)
        Assert.Contains("  1  plot.md *  2026-11-13 10:00\n", output);
        Assert.Contains("  2  Ending.md  2026-11-13 09:30\n", output);
        // (-l: the full paths, the date right after each: not lined up, the rows would be too wide)
        Assert.Contains("  2  " + In("work", "Ending.md") + "  2026-11-13 09:30\n", output);
    }

    [Fact]
    public void Info_KeepsTheLastList()
    {
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Toggle(In("work", "Ending.md"));
        FileFavorites.Save();
        var (output, errors, _, session) = Run("fav\ninfo 2\ninfo 1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(In("work", "Ending.md"), output);
        Assert.Equal(2, session.LastList.Count);
    }

    [Fact]
    public void Tab_AfterTheLists_CompletesDashL()
    {
        Assert.Equal("hist -l", Tab(Completion(), "hist -"));
        Assert.Equal("fav -l", Tab(Completion(), "fav -"));
    }

    // ---- The lists of favorites: fav lists, new, rename, delete, use, main

    [Fact]
    public void FavoriteLists_AreMadeOpenedRenamedAndDeleted_AndTheOpenOneIsKept()
    {
        try
        {
            FileIo.SaveSettings();
            var (output, errors, _, _) = Run("fav new reading\nfav add plot.md\nfav lists\n", yes: true);
            Assert.Equal(string.Empty, errors);
            Assert.Contains(Lang.F("List made and opened: {0}", "reading"), output);
            Assert.Contains("  * reading\n", output);
            Assert.Contains("    main\n", output);
            Assert.Equal("reading", FileFavorites.CurrentList);
            Assert.Equal("reading", FileIo.ParseSettings(FileIo.ReadSettingsFile())["favoritelist"]);

            (output, errors, _, _) = Run("fav main\nfav rename reading writing\nfav use writing\n", yes: true);
            Assert.Equal(string.Empty, errors);
            Assert.Contains(Lang.F("List: {0}", "main"), output);
            Assert.Contains(Lang.F("List {0} renamed to {1}", "reading", "writing"), output);
            Assert.Contains("  1  plot.md\n", output);
            Assert.Equal("writing", FileIo.ParseSettings(FileIo.ReadSettingsFile())["favoritelist"]);

            // The open list deleted: the main list is open
            (output, errors, _, _) = Run("fav delete writing\nfav lists\n", yes: true);
            // (the question, from a file: on the error output)
            Assert.StartsWith(Lang.T("Delete list"), errors);
            Assert.Contains(Lang.F("List deleted: {0}", "writing"), output);
            Assert.DoesNotContain("writing\n", output.Split(Lang.F("List deleted: {0}", "writing"))[1]);
            Assert.Equal(string.Empty, FileFavorites.CurrentList);
            Assert.Equal(string.Empty, FileIo.ParseSettings(FileIo.ReadSettingsFile())["favoritelist"]);
        }
        finally
        {
            FileFavorites.UseList(string.Empty);
        }
    }

    [Fact]
    public void FavoriteLists_WhatCannotBeDone_IsTold()
    {
        try
        {
            FileFavorites.NewList("work");
            var (_, errors, _, _) = Run("fav new\nfav new main\nfav new work\nfav rename main x\nfav delete main\n" +
                                        "fav delete nothing\nfav rename work\nfav nothing\n", yes: true);
            Assert.Equal(8, errors.TrimEnd('\n').Split('\n').Length);
            Assert.Contains(Lang.T("The main list keeps its name"), errors);
            Assert.Contains(Lang.T("The main list cannot be deleted"), errors);
            // Not asked from a file without --yes: nothing deleted
            var (output, _, _, _) = Run("fav delete work\n");
            Assert.Contains(Lang.T("Nothing was deleted"), output);
            Assert.Contains("work", FileFavorites.ListNames());
        }
        finally
        {
            FileFavorites.UseList(string.Empty);
        }
    }

    [Fact]
    public void FavoriteLists_ANameThatIsAWord_IsOpenedWithUse()
    {
        try
        {
            FileFavorites.NewList("new");
            var (output, errors, _, _) = Run("fav use new\n");
            Assert.Equal(string.Empty, errors);
            Assert.Contains(Lang.F("List: {0}", "new"), output);
            Assert.Equal("new", FileFavorites.CurrentList);
        }
        finally
        {
            FileFavorites.UseList(string.Empty);
        }
    }

    [Fact]
    public void Tab_AfterFav_CompletesTheWordsAndTheLists()
    {
        FileFavorites.NewList("reading");
        Assert.Equal("fav lists", Tab(Completion(), "fav li"));
        Assert.Equal("fav use main", Tab(Completion(), "fav use ma"));
        Assert.Equal("fav delete reading", Tab(Completion(), "fav delete re"));
        Assert.Equal("fav main", Tab(Completion(), "fav ma"));
    }

    // ---- Review of 1.5.1

    [Fact]
    public void ClipVariable_KeepsBlankLinesAndBlanksAtItsEnd()
    {
        File.WriteAllText(In("work", "src.md"), "a\nb\n\n\nc\nRoses are red,  \nViolets  \n");
        File.WriteAllText(In("work", "dst.md"), "x\n");
        var (_, errors, _, _) = Run("clip lines src.md 2-4\ninsert dst.md bottom $clip\nclip lines src.md 6-7\n" +
                                    "insert dst.md bottom $clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("x\nb\n\n\nRoses are red,  \nViolets  \n", Text("dst.md"));
    }

    [Fact]
    public void ClipVariable_ABlankLineOrEmpty_NeverMakesChangeDelete()
    {
        File.WriteAllText(In("work", "src.md"), "a\n\n\nb\n");
        File.WriteAllText(In("work", "blank.md"), "\n");
        File.WriteAllText(In("work", "dst.md"), "x\ny\nz\n");
        // Two blank lines: line 2 becomes two blank lines
        var (_, errors, _, _) = Run("clip lines src.md 2-3\nchange dst.md 2 $clip\n", yes: true);
        Assert.Equal(string.Empty, errors);
        Assert.Equal("x\n\n\nz\n", Text("dst.md"));
        // The text of a file of one blank line is empty: refused, nothing deleted
        (_, errors, _, _) = Run("clip text blank.md\nchange dst.md 1 $clip\n", yes: true);
        Assert.Contains(Lang.T("$clip is empty: change would delete the lines"), errors);
        Assert.Equal("x\n\n\nz\n", Text("dst.md"));
    }

    [Fact]
    public void ClipVariable_TheTextOfAFileOfOneLine_IsOneLine()
    {
        File.WriteAllText(In("work", "one.txt"), "Draft\n");
        var (output, errors, _, _) = Run("clip text one.txt\ncd $clip\nreg\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  $clip " + Lang.F("{0}  (the last clip)", "Draft"), output);
    }

    [Fact]
    public void Help_FitsTheScreen_TheTextsBrokenUnderTheirColumn()
    {
        var (output, _, _, _) = Run("help\nhelp fav\nhelp lines\n");
        Assert.All(output.TrimEnd('\n').Split('\n'), line => Assert.True(TextMetrics.WidthOf(line) <= 79, line));
        Assert.Contains("lines file|n lines > file|n place", output);
    }

    [Fact]
    public void FavoriteLists_AListNamedDashL_IsOpenedWithUse_AndOtherWordsAfterTheListsAreTold()
    {
        try
        {
            FileFavorites.NewList("-l");
            var (output, errors, _, _) = Run("fav use -l\n");
            Assert.Equal(string.Empty, errors);
            Assert.Equal("-l", FileFavorites.CurrentList);
            Assert.Contains(Lang.F("List: {0}", "-l"), output);
        }
        finally
        {
            FileFavorites.UseList(string.Empty);
        }

        var (_, others, code, _) = Run("hist 5\nrecent x\nbook work\nhist -l\n");
        Assert.Equal(3, others.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("Not taken: {0} (only -l, for the full paths)", "5"), others);
        Assert.Equal(1, code);
    }

    // Two files whose names differ only in letter case, where the file system tells them apart (Linux); false
    // where it does not (Windows, macOS)
    private bool TwoNamesByCase(string folder)
    {
        File.WriteAllText(In("work", folder, "Notes.md"), "upper\n");
        File.WriteAllText(In("work", folder, "notes.md"), "lower\n");
        return Directory.GetFiles(In("work", folder)).Length == 2;
    }

    [Fact]
    public void Tab_TheFilesOpenedLast_ByTheLetterCaseOfTheFileSystem()
    {
        Directory.CreateDirectory(In("work", "case"));
        if (!TwoNamesByCase("case")) return;
        FileHistory.Add(In("work", "case", "notes.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Save();
        Assert.Equal("open case/notes.md", Tab(Completion(), "open case/"));
        Assert.Equal("open case/Notes.md", Tab(Completion(), "open case/", true, true));
    }

    [Fact]
    public void Lines_TwoNamesByLetterCase_AreTwoFilesOnlyWhereTheFileSystemSaysSo()
    {
        Directory.CreateDirectory(In("work", "case"));
        var two = TwoNamesByCase("case");
        var (_, errors, _, _) = Run("lines case/Notes.md 1 > case/notes.md bottom\n");
        if (two)
        {
            Assert.Equal(string.Empty, errors);
            Assert.Equal("lower\nupper\n", Text(Path.Combine("case", "notes.md")));
        }
        else
        {
            Assert.Contains(Lang.T("The lines are in that file already: shift moves lines in a file"), errors);
        }
    }

    [UnixFact]
    public void OnLinuxAndMacOS_APatternThatFitsNothing_CompletesAName()
    {
        Directory.CreateDirectory(In("work", "marks"));
        File.WriteAllText(In("work", "marks", "notes?.md"), "x\n");
        Assert.Equal("open marks/notes?.md", Tab(Completion(), "open marks/notes?"));
    }

    [UnixFact]
    public void OnLinuxAndMacOS_ALinkToAFileThatIsGone_IsMissing_AndLinesFollowsALink()
    {
        File.WriteAllText(In("work", "gone.md"), "x\n");
        File.CreateSymbolicLink(In("work", "link.md"), In("work", "gone.md"));
        File.Delete(In("work", "gone.md"));
        FileFavorites.Toggle(In("work", "link.md"));
        FileFavorites.Save();
        var (output, _, _, _) = Run("fav\n");
        Assert.Contains("link.md " + DialogText.FavoriteMissing, output);

        File.WriteAllText(In("work", "real.md"), "a\nb\n");
        File.CreateSymbolicLink(In("work", "alias.md"), In("work", "real.md"));
        var (_, errors, _, _) = Run("lines real.md 1 > alias.md bottom cut\n");
        Assert.Contains(Lang.T("The lines are in that file already: shift moves lines in a file"), errors);
        Assert.Equal("a\nb\n", Text("real.md"));
    }

    // ---- lines: start, end, unstart, unend, replace

    [Theory]
    [InlineData("This is some task", "start", "Remember this:", false, "", "Remember this: This is some task")]
    [InlineData("Buy milk", "start", "- [ ] ", true, "", "- [ ] Buy milk")]
    [InlineData("x = 1", "end", ";", true, "", "x = 1;")]
    [InlineData("A task", "end", "(by Friday)", false, "", "A task (by Friday)")]
    [InlineData("   ", "start", "-", false, "", "   ")]
    [InlineData("Remember this: A task", "unstart", "Remember this:", false, "", "A task")]
    [InlineData("- [ ] Buy milk", "unstart", "- [ ] ", true, "", "Buy milk")]
    [InlineData("Other line", "unstart", "Remember this:", false, "", "Other line")]
    [InlineData("A task (by Friday)", "unend", "(by Friday)", false, "", "A task")]
    [InlineData("a draft, a Draft", "replace", "draft", false, "final", "a final, a Draft")]
    [InlineData("one two", "replace", " two", false, "", "one")]
    public void EditedLine_PutsInTakesOffOrReplaces(string line, string kind, string text, bool exact, string with,
        string expected)
    {
        Assert.Equal(expected, ShellSession.EditedLine(line, kind, text, exact, with));
    }

    [Fact]
    public void Lines_StartEndAndBack_WorkOnTheLinesPicked_AndKeepTheRest()
    {
        File.WriteAllText(In("work", "todo.md"), "This is some task, I need to complete\r\n\r\nBuy milk\r\nCall Anna\r\n");
        var (output, errors, _, session) = Run("lines todo.md 1 start Remember this:\nlines todo.md 1-4 end \"!\"\n" +
                                               "lines todo.md \"Buy milk\" start \"- [ ] \"\n");
        Assert.Equal(string.Empty, errors);
        // (the blank line left as it is; the line endings of the file kept)
        Assert.Equal("Remember this: This is some task, I need to complete!\r\n\r\n- [ ] Buy milk!\r\nCall Anna!\r\n",
            Text("todo.md"));
        Assert.Contains(Lang.F("{0}: {1} lines changed", In("work", "todo.md"), 3), output);
        Assert.Contains(Lang.F("{0}: line {1} changed", In("work", "todo.md"), 3), output);
        Assert.Equal(3, session.LastList.Single().At?.Line);

        (output, errors, _, _) = Run("lines todo.md 1 unstart Remember this:\nlines todo.md 1-4 unend \"!\"\n" +
                                     "lines todo.md 1-4 unend \"!\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("This is some task, I need to complete\r\n\r\n- [ ] Buy milk\r\nCall Anna\r\n", Text("todo.md"));
        Assert.Contains(Lang.F("Nothing changed: no line ends with {0}", "!"), output);
    }

    [Fact]
    public void Lines_Replace_ChangesATextInTheLinesOnly()
    {
        File.WriteAllText(In("work", "notes.md"), "a draft\nthe draft\nmy draft\n");
        var (output, errors, _, _) = Run("lines notes.md 1-2 replace draft \"final text\"\nlines notes.md +3 1 replace my \"\"\n" +
                                         "lines notes.md 1-3 replace nothing x\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("a final text\nthe final text\n draft\n", Text("notes.md"));
        Assert.Contains(Lang.F("Nothing changed: no line has {0} in it", "nothing"), output);
    }

    [Fact]
    public void Lines_ATextWithAnArrowOrAWordOfTheCommands_IsText_AndRegistersAreTheirValue()
    {
        File.WriteAllText(In("work", "notes.md"), "quote\nline\n");
        var (_, errors, _, _) = Run("lines notes.md 1 start >\nreg note Buy more ink\nlines notes.md 2 end $note\n" +
                                    "lines notes.md 2 end start\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("> quote\nline Buy more ink start\n", Text("notes.md"));
    }

    [Fact]
    public void Lines_WhatCannotBeDone_IsTold_AndChangesNothing()
    {
        File.WriteAllText(In("work", "notes.md"), "one\ntwo\n");
        var (_, errors, code, _) = Run("lines notes.md 1 start\nlines notes.md 1 replace x\nlines notes.md 9 end x\n" +
                                       "lines notes.md 1 middle x\nlines notes.md 1 start \"\"\n");
        Assert.Equal(5, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Equal(1, code);
        Assert.Equal("one\ntwo\n", Text("notes.md"));
    }

    [Theory]
    [InlineData("todo.md 4 > plan.md top", 2, -1)]
    [InlineData("todo.md +4 3 > plan.md top", 3, -1)]
    [InlineData("todo.md 4 start x", -1, 2)]
    [InlineData("todo.md +4 3 END x", -1, 3)]
    [InlineData("todo.md 4 end >", -1, 2)]
    [InlineData("todo.md 4 middle x", -1, -1)]
    public void LinesForm_TellsACopyFromAnEdit(string argument, int arrow, int edit)
    {
        Assert.Equal((arrow, edit), ShellSession.LinesForm(ShellSession.Words(argument)));
    }

    [Fact]
    public void Tab_AfterTheLinesOfLines_CompletesWhatIsDone()
    {
        Assert.Equal("lines plot.md 1 start", Tab(Completion(), "lines plot.md 1 st"));
        Assert.Equal("lines plot.md +1 2 unend", Tab(Completion(), "lines plot.md +1 2 une"));
        Assert.Equal("lines plot.md 1 replace", Tab(Completion(), "lines plot.md 1 rep"));
    }

    // ---- Line numbers: clip lines takes the lines as lines and shift do; peek alone takes "40" as how many

    [Fact]
    public void ClipLines_TakesTheLinesAsLinesDoes_AndPeekTheFirstOnes()
    {
        File.WriteAllText(In("work", "five.md"), "one\ntwo\nthree\nfour\nfive\n");
        var sent = Clipboard();
        var (output, errors, _, _) = Run("clip lines five.md 2\nclip lines five.md 1,3-4\nclip lines five.md four\n" +
                                         "clip lines five.md +2 2\npeek five.md 2\n", clipboard: true);
        Assert.Equal(string.Empty, errors);
        Assert.Equal(new[] { "two", "one\nthree\nfour", "four", "two\nthree" }, sent);
        Assert.Contains(Lang.F("Line {0} of {1}", 2, In("work", "five.md")), output);
        Assert.Contains(Lang.F("Lines {0} of {1}", "1,3,4", In("work", "five.md")), output);
        // peek: the first 2 lines
        Assert.Contains("five.md\n  1  one\n  2  two\n", output);
    }

    // ---- Folders of favorites: fav folder, subfolder, rename, remove, move, undo

    // The favorites as a line: "." for each level, a folder in brackets, a favorite by its name
    private static string FavoritesShape()
    {
        FileFavorites.Load();
        return string.Join("|", FileFavorites.Items.Select(item => new string('.', item.Level) +
            (item.IsFolder ? "[" + item.Name + "]" : Path.GetFileName(item.Path))));
    }

    // Four favorites: a.md, b.md, c.md, d.md
    private void FourFavorites()
    {
        foreach (var name in new[] { "a.md", "b.md", "c.md", "d.md" }) File.WriteAllText(In("work", name), "x\n");
        Run("fav add a.md\nfav add b.md\nfav add c.md\nfav add d.md\n");
    }

    [Fact]
    public void FavoriteFolders_AreMadeFilledRenamedAndShownAgain()
    {
        FourFavorites();
        var (output, errors, _, session) = Run("fav folder Drafts\nfav move 2,4 [Drafts]\nfav subfolder [Drafts] Old\n" +
                                               "fav move 1 \"[Drafts/Old]\"\nfav rename [Drafts] My writing\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("c.md|[My writing]|.[Old]|..a.md|.b.md|.d.md", FavoritesShape());
        Assert.Contains(Lang.F("Folder made: [{0}]", "Drafts"), output);
        Assert.Contains(Lang.F("Folder renamed: [{0}]", "My writing"), output);
        Assert.Contains(Lang.F("Moved: {0}", "b.md, d.md"), output);
        // The list shown again is the last list: its rows are those of fav
        Assert.Equal(In("work", "c.md"), session.LastList[0].Path);
        Assert.Contains("\n[My writing]\n  [Old]\n", output);
    }

    [Fact]
    public void FavoriteRows_MoveTopBottomUpDownToARowAndOut()
    {
        FourFavorites();
        Run("fav move 4 top\n");
        Assert.Equal("d.md|a.md|b.md|c.md", FavoritesShape());
        Run("fav move 1 bottom\nfav move 1 down\n");
        Assert.Equal("b.md|a.md|c.md|d.md", FavoritesShape());
        Run("fav move 4 2\n");
        Assert.Equal("b.md|d.md|a.md|c.md", FavoritesShape());
        Run("fav folder F\nfav move 1-2 [F]\nfav move 4 out\n");
        Assert.Equal("a.md|c.md|[F]|.b.md|d.md", FavoritesShape());
        // To the place of a row in a folder: into the folder
        Run("fav move 1 3\n");
        Assert.Equal("c.md|[F]|.a.md|.b.md|d.md", FavoritesShape());
    }

    [Fact]
    public void FavoriteFolders_MoveWithWhatIsInThem()
    {
        FourFavorites();
        Run("fav folder A\nfav folder B\nfav move 1 [A]\nfav move 1 [B]\nfav move [B] top\n");
        Assert.Equal("[B]|.b.md|c.md|d.md|[A]|.a.md", FavoritesShape());
        Run("fav move [A] [B]\n");
        Assert.Equal("[B]|.b.md|.[A]|..a.md|c.md|d.md", FavoritesShape());
        Run("fav move [B/A] out\nfav move [A] up\n");
        Assert.Equal("[A]|.a.md|[B]|.b.md|c.md|d.md", FavoritesShape());
        // A folder with subfolders does not go into another
        Run("fav subfolder [B] S\n");
        var (_, errors, _, _) = Run("fav move [B] [A]\nfav move [A] [A]\nfav move [A] out\nfav move [A] 3\n");
        Assert.Equal(4, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("{0} has subfolders: it cannot go into another folder", "B"), errors);
        Assert.Equal("[A]|.a.md|[B]|.[S]|.b.md|c.md|d.md", FavoritesShape());
    }

    [Fact]
    public void FavoriteFolders_RemovedAfterAsking_AndUndoPutsThemBack()
    {
        FourFavorites();
        Run("fav folder F\nfav move 1-2 [F]\n");
        var shape = FavoritesShape();
        // Not asked from a file without --yes: nothing removed
        var (output, _, _, _) = Run("fav remove [F]\n");
        Assert.Contains(Lang.T("Nothing was removed"), output);
        Assert.Equal(shape, FavoritesShape());

        // (asked on lines while the shell runs: --yes answers)
        Run("fav remove [F]\nfav undo\n", yes: true);
        Assert.Equal(shape, FavoritesShape());
        Run("fav remove [F]\n", yes: true);
        Assert.Equal("c.md|d.md", FavoritesShape());
        // Another shell has removed nothing
        var errors = Run("fav undo\n").Errors;
        Assert.Contains(Lang.T("Nothing to put back: fav remove has removed nothing here"), errors);
        // fav remove 3 too; and only while the list is as the removal left it
        Run("fav add a.md\nfav\nfav remove 2\nfav undo\n", yes: true);
        Assert.Equal("c.md|d.md|a.md", FavoritesShape());
        errors = Run("fav\nfav remove 1\nfav add c.md\nfav undo\n", yes: true).Errors;
        Assert.Contains(Lang.T("The favorites have changed since: nothing is put back"), errors);
        Assert.Equal("d.md|a.md|c.md", FavoritesShape());
    }

    [Fact]
    public void FavoriteFolders_WhatCannotBeDone_IsTold()
    {
        FourFavorites();
        Run("fav folder F\nfav folder G\nfav subfolder [F] X\nfav subfolder [G] X\n");
        var shape = FavoritesShape();
        var (_, errors, code, _) = Run("fav move 9 top\nfav move 1,2 up\nfav move 1 nowhere\nfav move [Nothing] top\n" +
                                       "fav move [X] top\nfav folder [bad]\nfav subfolder [F/X] Y\nfav rename [F]\n" +
                                       "fav move 1 1\nfav move 1 out\n");
        Assert.Equal(10, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("{0} folders are named [{1}]: give the folder it is in too, [Writing/{1}]", 2, "X"), errors);
        Assert.Equal(1, code);
        Assert.Equal(shape, FavoritesShape());
    }

    [Fact]
    public void Tab_AfterFavMove_CompletesTheFoldersAndThePlaces()
    {
        FourFavorites();
        Run("fav folder Drafts\nfav folder \"My notes\"\n");
        Assert.Equal("fav move 1 [Drafts]", Tab(Completion(), "fav move 1 [Dr"));
        Assert.Equal("fav move 1 bottom", Tab(Completion(), "fav move 1 bo"));
        Assert.Equal("fav move \"[My notes]\"", Tab(Completion(), "fav move [My"));
        Assert.Equal("fav remove [Drafts]", Tab(Completion(), "fav remove [D"));
    }

    // ---- A favorite marked " *" in the lists by name (not with -l, not in fav)

    [Fact]
    public void TheListsByName_MarkTheFavorites_AndDashLDoesNot()
    {
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Toggle(In("work", "Draft"));
        FileFavorites.Save();
        FileHistory.Add(In("work", "plot.md"), new DateTime(2026, 11, 13, 9, 30, 0));
        FileHistory.Save();
        var (output, errors, _, _) = Run("ls\nhist\nfav\nfind plot\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  Draft/ *\n", output);
        Assert.Contains("  plot.md *\n", output);
        Assert.Contains("  Ending.md\n", output);
        Assert.Contains("  plot.md *  2026-11-13 09:30\n", output);
        // fav: all of them favorites, no mark
        Assert.DoesNotContain("  1  Draft/ *", output.Split("List: main")[1]);

        (output, errors, _, _) = Run("ls -l\nls -a -l Draft\nhist -l\nfind -l plot\n");
        Assert.Equal(string.Empty, errors);
        Assert.DoesNotContain(" *", output);
        Assert.Contains("  " + In("work", "plot.md") + "\n", output);
        Assert.Contains("  " + Path.TrimEndingDirectorySeparator(In("work", "Draft")) + "/\n", output);
    }

    [Fact]
    public void Find_ALineFound_IsNotMarked_TheFileIs()
    {
        FileFavorites.Toggle(In("work", "plot.md"));
        FileFavorites.Save();
        var (output, _, _, _) = Run("find -t Plot\nfind plot\n");
        Assert.Contains("plot.md:1  Plot\n", output);
        Assert.Contains("  plot.md *\n", output);
    }

    [Fact]
    public void Tab_AfterLsDashAAndDashL_CompletesTheDirectory()
    {
        Assert.Equal("ls -a -l Draft/", Tab(Completion(), "ls -a -l Dr"));
    }

    [Fact]
    public void CopyRenameAndMove_PutTheFileInTheLatestFiles_ButNotADirectory()
    {
        Directory.CreateDirectory(In("work", "drafts"));
        // A file duplicated, and the copy renamed: the copy by its new name is the newest
        var (output, errors, _, _) = Run("copy plot.md .\nrename plot-copy.md plan.md\nrecent\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "plan.md"), FileHistory.Items[0].Path);
        Assert.DoesNotContain(FileHistory.Items, entry => entry.Path == In("work", "plot-copy.md"));
        Assert.Contains("  1  plan.md  ", output);
        (_, errors, _, _) = Run("copy plot.md drafts\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "drafts", "plot.md"), FileHistory.Items[0].Path);
        (_, errors, _, _) = Run("move plan.md drafts\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "drafts", "plan.md"), FileHistory.Items[0].Path);
        Assert.DoesNotContain(FileHistory.Items, entry => entry.Path == In("work", "plan.md"));
        // A directory copied or renamed is not a latest file
        var count = FileHistory.Items.Count;
        (_, errors, _, _) = Run("copy drafts .\nrename drafts old\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(count, FileHistory.Items.Count);
        Assert.Equal(In("work", "old", "plan.md"), FileHistory.Items[0].Path);
        // Not copied (a file of that name is there): not put in the list
        (_, errors, _, _) = Run("copy plot.md old\ncopy plot.md old\n");
        Assert.NotEmpty(errors);
        Assert.Single(FileHistory.Items, entry => entry.Path == In("work", "old", "plot.md"));
    }

    [Fact]
    public void New_PutsTheFileInTheLatestFiles()
    {
        var (output, errors, _, _) = Run("new fresh\nrecent\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "fresh.md"), FileHistory.Items[0].Path);
        Assert.Contains("  1  fresh.md  ", output);
        // A name that is taken: not made, and not put in the list
        (_, errors, _, _) = Run("new plot.md\n");
        Assert.NotEmpty(errors);
        Assert.DoesNotContain(FileHistory.Items, entry => entry.Path == In("work", "plot.md"));
    }

    // ---- again, !3, !!: the commands typed, listed and run again

    [Theory]
    [InlineData("!!", 1)]
    [InlineData("!3", 3)]
    [InlineData("again 12", 12)]
    [InlineData("AGAIN 2", 2)]
    [InlineData("again x", 0)]
    [InlineData("again", null)]
    [InlineData("!x", null)]
    [InlineData("ls", null)]
    public void AgainNumber_TellsTheCommandALineRunsAgain(string line, int? expected)
    {
        Assert.Equal(expected, ShellSession.AgainNumber(line));
    }

    [Fact]
    public void Again_ListsTheCommands_AndRunsOneByItsNumber()
    {
        CommandHistory.Clear();
        CommandHistory.Add(CommandContext.Shell, "peek plot.md");
        CommandHistory.Add(CommandContext.Shell, "pwd");
        try
        {
            var (output, errors, _, session) = Run("ls\nagain\nagain 2\n!!\nagain x\n!9\n");
            Assert.Contains("  1  pwd\n  2  peek plot.md\n", output);
            // Written out, then run: peek shows plot.md, and pwd the directory
            Assert.Contains("peek plot.md\n  1  Plot\n", output);
            Assert.Contains("pwd\n" + In("work") + "\n", output);
            Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
            Assert.Contains(Lang.F("No command {0}: {1} are kept (again lists them)", 9, 2), errors);
            // From a file nothing is kept: the numbers stay
            Assert.Equal(new[] { "pwd", "peek plot.md" }, CommandHistory.Of(CommandContext.Shell));
            // "again" does not take the place of the last list (peek did)
            Assert.Equal(In("work", "plot.md"), session.LastList.Single().Path);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Again_TheCommandsTypedAtThePrompt_AreKept_AndAgainItselfIsNot()
    {
        CommandHistory.Clear();
        try
        {
            var (output, _, _, _) = Run("pwd\npeek plot.md\n!2\nagain\n", terminal: true);
            Assert.Equal(new[] { "pwd", "peek plot.md" }, CommandHistory.Of(CommandContext.Shell));
            Assert.Contains("  1  pwd\n  2  peek plot.md\n", output);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Again_TheShellKeepsFiftyCommands()
    {
        CommandHistory.Clear();
        try
        {
            for (var i = 0; i < 60; i++) CommandHistory.Add(CommandContext.Shell, "cmd " + i);
            for (var i = 0; i < 30; i++) CommandHistory.Add(CommandContext.Files, "cmd " + i);
            Assert.Equal(CommandHistory.ShellMaxCount, CommandHistory.Of(CommandContext.Shell).Count);
            Assert.Equal("cmd 59", CommandHistory.Of(CommandContext.Shell)[0]);
            Assert.Equal(CommandHistory.MaxCount, CommandHistory.Of(CommandContext.Files).Count);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Tab_AfterABangOrAgain_PutsTheCommandOnTheLine()
    {
        CommandHistory.Clear();
        CommandHistory.Add(CommandContext.Shell, "lines todo.md 4 start Note:");
        CommandHistory.Add(CommandContext.Shell, "pwd");
        try
        {
            Assert.Equal("lines todo.md 4 start Note:", Tab(Completion(), "!2"));
            Assert.Equal("pwd", Tab(Completion(), "!!"));
            Assert.Equal("pwd", Tab(Completion(), "again 1"));
            Assert.Equal("!7", Tab(Completion(), "!7"));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    // ---- The names set by Blyant: $pwd, $file, $date, $time, $now

    [Fact]
    public void Pwd_IsTheDirectoryOfTheShell_InPathsAndInText()
    {
        var (_, errors, _, _) = Run("cd Draft\ninsert ../plot.md bottom Here: $pwd\ncopy ../Ending.md $pwd\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Plot\nHere: " + In("work", "Draft") + "\n", Text("plot.md"));
        Assert.True(File.Exists(In("work", "Draft", "Ending.md")));
    }

    [Fact]
    public void File_IsTheFileTheLastCommandWorkedOn_NotARowOfAList()
    {
        var (_, errors, _, _) = Run("insert plot.md bottom $file\n");
        Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"),
            errors);
        Assert.Equal("Plot\n", Text("plot.md"));
        (_, errors, _, _) = Run("peek plot.md\ninsert $file bottom one\nls\nfav\ninsert $file bottom two\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Plot\none\ntwo\n", Text("plot.md"));
    }

    [Fact]
    public void DateTimeAndNow_AreTheTimeNow_InIso()
    {
        ShellRegisters.Clock = () => new DateTime(2026, 11, 15, 14, 5, 33);
        try
        {
            var (_, errors, _, _) = Run("insert plot.md bottom $date | $time | $now | $$date\n");
            Assert.Equal(string.Empty, errors);
            Assert.Equal("Plot\n2026-11-15 | 14:05 | 2026-11-15 14:05:33 | $date\n", Text("plot.md"));
        }
        finally
        {
            ShellRegisters.Clock = () => DateTime.Now;
        }
    }

    [Fact]
    public void TheNamesSetByBlyant_AreNoRegisters_AndRegListsThem()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "date\tfrom the file\nnote\tBuy ink\n");
        ShellRegisters.Load();
        var (output, errors, _, _) = Run("reg pwd x\nreg -d date\nreg -d now\nreg\nreg pwd\n");
        Assert.Equal(3, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("${0} is set by Blyant: it is not a register to set or take away", "pwd"), errors);
        Assert.Contains("  $note Buy ink\n", output);
        Assert.Contains(Lang.T("Set by Blyant:") + "\n", output);
        Assert.Contains("  $pwd  " + In("work") + "\n", output);
        Assert.DoesNotContain("  $file ", output);
        Assert.DoesNotContain("from the file\n" + Lang.T("Set by Blyant:"), output);
        Assert.EndsWith(In("work") + "\n", output);
        // (the line of the file is kept as it was, and not used)
        Assert.Equal("date\tfrom the file\nnote\tBuy ink\n", File.ReadAllText(file));
    }

    [Fact]
    public void Tab_AfterADollar_OffersTheNamesSetByBlyant_ThatHaveAValue()
    {
        // (a shell: $pwd is its directory)
        Run("pwd\n");
        Assert.Equal("cd $pwd", Tab(Completion(), "cd $pw"));
        Assert.Equal("insert plot.md top $now", Tab(Completion(), "insert plot.md top $no"));
        Assert.Equal("open $fi", Tab(Completion(), "open $fi"));
    }

    [Fact]
    public void Find_TakesStarAndQuestionMark_InTheNameAndInTheText()
    {
        File.WriteAllText(In("work", "Blyant-v151.md"), "The storm came at night.\nA quiet day.\n");
        File.WriteAllText(In("work", "notes-151.txt"), "Storms and nights\n");
        var (output, errors, _, _) = Run("find *151*\nfind -t storm*night\nfind *.md -t st?rm came\nfind -t qu?et\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  Blyant-v151.md\n", output);
        Assert.Contains("  notes-151.txt\n", output);
        Assert.Contains("Blyant-v151.md:1  The storm came at night.\n", output);
        Assert.Contains("notes-151.txt:1  Storms and nights\n", output);
        Assert.Contains("Blyant-v151.md:2  A quiet day.\n", output);
        // (found once, by "storm*night": "*.md -t" does not search the text file)
        Assert.Equal(2, output.Split("notes-151.txt:1").Length);
    }

    // ---- peek +, ++, -, --: the window of the last peek moved

    [Fact]
    public void Peek_PlusAndMinus_MoveTheWindowOfTheLastPeek()
    {
        // (steps of 5 and 10, set)
        Shell.PeekStep = 5;
        Shell.PeekBigStep = 10;
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        var (output, errors, _, session) = Run("peek long.md\npeek 1 +\npeek +\npeek ++\npeek -\npeek --\n");
        Assert.Equal(string.Empty, errors);
        // 1-20, then 6-25, 11-30, 21-40, 16-35, 6-25
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        Assert.Equal(new[] { (1, 20), (6, 25), (11, 30), (21, 40), (16, 35), (6, 25) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 60)), windows);
        // "open 1" opens the file at the top of the window
        Assert.Equal((6, 1), session.LastList.Single().At);
    }

    [Fact]
    public void Peek_Plus_KeepsTheSizeOfTheWindow_AndStopsAtTheEnds()
    {
        Shell.PeekStep = 5;
        Shell.PeekBigStep = 10;
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        var (output, _, _, _) = Run("peek long.md +40 10\npeek ++\npeek ++\npeek ++\npeek -- \npeek long.md --\n");
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        Assert.Equal(new[]
        {
            Lang.F("(lines {0}-{1} of {2})", 40, 49, 60), Lang.F("(lines {0}-{1} of {2})", 50, 59, 60),
            Lang.F("(lines {0}-{1} of {2})", 51, 60, 60), Lang.F("(lines {0}-{1} of {2})", 51, 60, 60),
            Lang.F("(lines {0}-{1} of {2})", 41, 50, 60), Lang.F("(lines {0}-{1} of {2})", 31, 40, 60)
        }, windows);
    }

    [Fact]
    public void Peek_Plus_WithNoFilePeekedYet_IsTold_AndAnotherFileStartsAtTheTop()
    {
        Shell.PeekStep = 5;
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        var (output, errors, _, _) = Run("peek +\npeek plot.md\npeek long.md +\n");
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 6, 25, 60), output);
    }

    [Fact]
    public void Peek_Steps_AreOneAndFive_AndCanBeSet_AndReset()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        FileIo.SaveSettings();
        var (output, errors, _, _) = Run("peek long.md +1 5\npeek +\npeek ++\npeek -\nhelp peek\n");
        Assert.Equal(string.Empty, errors);
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        // 1-5, then 1 down, 5 down, 1 up
        Assert.Equal(new[] { (1, 5), (2, 6), (7, 11), (6, 10) }.Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 60)),
            windows);
        Assert.Contains(Lang.F("Show 1 line further down (++: {0}), or up (-, --)", 5), output);

        (output, errors, _, _) = Run("set shellpeekstep 3\nset shellpeekbigstep 0\npeek long.md +1 5\npeek +\nhelp peek\n");
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 4, 8, 60), output);
        Assert.Contains(Lang.F("Show {0} lines further down (++: {1}), or up (-, --)", 3, 5), output);
        Assert.NotEqual(string.Empty, errors);
        Assert.Equal(3, Shell.PeekStep);
        Assert.Equal(5, Shell.PeekBigStep);
        Assert.Equal("3", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shellpeekstep"]);

        Run("reset\n", yes: true);
        Assert.Equal(1, Shell.PeekStep);
        Assert.Equal(5, Shell.PeekBigStep);
    }

    [Fact]
    public void Peek_PlusAndMinusAlone_AreStepsOfTheLastPeek()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        var (output, errors, _, _) = Run("+\npeek long.md +40 10\n+\n++\ninsert long.md +50 new\n+\n-\n--\nhelp +\n");
        // "+" before a peek: as "peek +"
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        Assert.Equal(new[] { (40, 49, 60), (41, 50, 60), (46, 55, 60), (47, 56, 61), (46, 55, 61), (41, 50, 61) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, w.Item3)), windows);
        // help + tells of peek
        Assert.Contains("peek [file|n] +|++|-|--", output);
    }

    // ---- $peek: the lines the last peek showed

    [Fact]
    public void PeekVariable_PutsTheLinesShownInAnotherFile()
    {
        File.WriteAllText(In("work", "notes.md"), "one\ntwo\nthree\nfour\n");
        File.WriteAllText(In("work", "plan.md"), "# Plan\n");
        var (output, errors, _, _) = Run("insert plan.md bottom $peek\npeek notes.md 2-3\ninsert plan.md bottom $peek\n" +
                                         "+\nchange plan.md +1 $peek\ncd $peek\nreg\nreg peek x\n");
        Assert.Contains(Lang.T("Nothing peeked yet: peek shows lines for $peek"), errors);
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg and run take it", "peek"), errors);
        Assert.Contains(Lang.F("${0} is set by Blyant: it is not a register to set or take away", "peek"), errors);
        Assert.Equal(3, errors.TrimEnd('\n').Split('\n').Length);
        // Lines 2-3, then (+) lines 3-4 in place of the heading
        Assert.Equal("three\nfour\ntwo\nthree\n", Text("plan.md"));
        Assert.Contains("  $peek " + Lang.F("{0}  ({1} lines, the last peek)", "three ...", 2), output);
    }

    [Fact]
    public void PeekVariable_OfOneLine_GoesWhereALineGoes_AndTabOffersIt()
    {
        File.WriteAllText(In("work", "notes.md"), "Draft\nother\n");
        var (_, errors, _, _) = Run("peek notes.md 1\ncd $peek\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("insert plot.md top $peek", Tab(Completion(), "insert plot.md top $pe"));
    }

    // ---- Registers of several lines: reg name = / +=, lines ... > $name, >> $name

    [Fact]
    public void Registers_OfSeveralLines_AreKeptInTheFile_AsLinesStartingWithATab()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "docs\tC:\\Writing\\docs\n\tnot of a register\n");
        ShellRegisters.Load();
        Assert.Equal("C:\\Writing\\docs\nnot of a register", ShellRegisters.Get("docs"));
        ShellRegisters.Set("notes", "one\n\n\ttabbed\nlast");
        ShellRegisters.Set("docs", "C:\\Writing\\docs");
        Assert.Equal(DialogText.Success, ShellRegisters.Save());
        Assert.Equal("docs\tC:\\Writing\\docs\nnotes\tone\n\t\n\t\ttabbed\n\tlast\n", File.ReadAllText(file));
        ShellRegisters.Load();
        Assert.Equal("one\n\n\ttabbed\nlast", ShellRegisters.Get("notes"));
        // A line with a tab first and no register before it: kept as it is
        File.WriteAllText(file, "\tlost line\na\tone\n");
        ShellRegisters.Load();
        Assert.Equal(1, ShellRegisters.Skipped);
        Assert.Equal("one", ShellRegisters.Get("a"));
    }

    [Fact]
    public void Reg_EqualsAndPlusEquals_CollectLines()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\nfour\n");
        File.WriteAllText(In("work", "plan.md"), "# Plan\n");
        var (output, errors, _, _) = Run("reg notes = First idea\npeek a.md 2-3\nreg notes += $peek\nreg notes += Last\n" +
                                         "insert plan.md bottom $notes\ncd $notes\nreg\nreg x =\n");
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg and run take it", "notes"), errors);
        Assert.Equal("First idea\ntwo\nthree\nLast", ShellRegisters.Get("notes"));
        Assert.Equal("# Plan\nFirst idea\ntwo\nthree\nLast\n", Text("plan.md"));
        Assert.Contains("  $notes " + Lang.F("{0}  ({1} lines)", "First idea ...", 4), output);
    }

    [Fact]
    public void Lines_IntoARegister_SetAddAndCut()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\nfour\n");
        File.WriteAllText(In("work", "b.md"), "b1\nb2\n");
        var (output, errors, _, _) = Run("lines a.md 2-3 > $notes\nlines b.md 1 >> $notes\nlines a.md 4 >> $notes cut\n" +
                                         "lines a.md 1 >> b.md bottom\n");
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        Assert.Contains(Lang.T(">> adds lines to a register: lines notes.md 4-6 >> $notes"), errors);
        Assert.Equal("two\nthree\nb1\nfour", ShellRegisters.Get("notes"));
        Assert.Equal("one\ntwo\nthree\n", Text("a.md"));
        Assert.Contains(Lang.F("{0} lines kept in ${1}", 2, "notes"), output);
        Assert.Contains(Lang.F("Line {0} added to ${1}", 1, "notes"), output);
        // > sets it again; a register holding a path, with a place after it, is still the file
        Run("reg plan " + In("work", "b.md") + "\nlines a.md 1 > $notes\nlines a.md 3 > $plan top\n");
        Assert.Equal("one", ShellRegisters.Get("notes"));
        Assert.Equal("three\nb1\nb2\n", Text("b.md"));
    }

    // ---- run $name, run file: the commands of a register or a file

    [Fact]
    public void Run_TheCommandsOfARegister_WithANameKeptForWhenTheyRun()
    {
        File.WriteAllText(In("work", "log.md"), "# Log\n");
        ShellRegisters.Clock = () => new DateTime(2026, 11, 15, 14, 5, 33);
        try
        {
            var (output, errors, _, _) = Run("reg tidy = insert log.md bottom $$date Tidied\nreg tidy += peek log.md\n" +
                                             "run $tidy\n");
            Assert.Equal(string.Empty, errors);
            Assert.Equal("insert log.md bottom $date Tidied\npeek log.md", ShellRegisters.Get("tidy"));
            Assert.Equal("# Log\n2026-11-15 Tidied\n", Text("log.md"));
            // Each command written out, then run
            Assert.Contains("insert log.md bottom $date Tidied\n", output);
            Assert.Contains("peek log.md\n  1  # Log\n  2  2026-11-15 Tidied\n", output);
        }
        finally
        {
            ShellRegisters.Clock = () => DateTime.Now;
        }
    }

    [Fact]
    public void Run_StopsAtTheFirstCommandThatFails()
    {
        File.WriteAllText(In("work", "log.md"), "# Log\n");
        var (_, errors, code, _) = Run("reg bad = peek nothing.md\nreg bad += insert log.md bottom never\nrun $bad\n");
        Assert.Contains(Lang.F("run stopped at line {0} of {1}: {2}", 1, "$bad", "peek nothing.md"), errors);
        Assert.Equal("# Log\n", Text("log.md"));
        Assert.Equal(1, code);
    }

    [Fact]
    public void Run_AFile_WithComments_AndARegisterInIt()
    {
        File.WriteAllText(In("work", "log.md"), "# Log\n");
        File.WriteAllText(In("work", "weekly.txt"), "# The weekly tidy\n\ninsert log.md bottom from the file\nrun $more\n");
        var (_, errors, _, _) = Run("reg more = insert log.md bottom from the register\nrun weekly.txt\n# a comment typed\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Log\nfrom the file\nfrom the register\n", Text("log.md"));
    }

    [Fact]
    public void Run_ARegisterThatRunsItself_IsStopped_AndWhatCannotBeRunIsTold()
    {
        var (_, errors, _, _) = Run("reg loop = run $$loop\nrun $loop\nrun\nrun $nothing\nrun nothing.txt\nrun Draft\n");
        Assert.Contains(Lang.F("run is {0} deep: a register or file that runs itself is stopped", 10), errors);
        Assert.Contains(Lang.T("Give a register or a file: run $tidy, run tidy.txt"), errors);
        Assert.Contains(Lang.F("No register named {0} (reg lists them)", "nothing"), errors);
        Assert.Contains(Lang.F("Not a file: {0}", In("work", "Draft")), errors);
    }

    [Fact]
    public void Run_IsKeptForAgain_NotTheCommandsItRuns()
    {
        CommandHistory.Clear();
        try
        {
            Run("reg two = pwd\nreg two += ls\nrun $two\n", terminal: true);
            Assert.Equal(new[] { "run $two", "reg two += ls", "reg two = pwd" }, CommandHistory.Of(CommandContext.Shell));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    // ---- Review of 1.5.2

    [Fact]
    public void Registers_ALineThatIsNoRegister_StartingWithATab_StaysOutOfTheRegisters()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "\tstray line\nb\tvalue\n");
        ShellRegisters.Load();
        Run("reg c = hello\n");
        ShellRegisters.Load();
        Assert.Equal("hello", ShellRegisters.Get("c"));
        Assert.Equal("value", ShellRegisters.Get("b"));
        Assert.Equal(1, ShellRegisters.Skipped);
        Assert.Equal("\tstray line\nb\tvalue\nc\thello\n", File.ReadAllText(file));
    }

    [WindowsFact]
    public void Registers_UnreadableAtTheStart_AChangeIsWrittenWithThoseOfTheFile_Windows()
    {
        var file = Path.Combine(_data, ShellRegisters.FileName);
        File.WriteAllText(file, "a\tone\n");
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) ShellRegisters.Load();
        ShellRegisters.Set("b", "two");
        Assert.Equal(DialogText.Success, ShellRegisters.Save());
        Assert.Equal("a\tone\nb\ttwo\n", File.ReadAllText(file));
    }

    [Fact]
    public void Reg_PlusEquals_OnAnEmptyRegister_AndAnEmptyPeek()
    {
        File.WriteAllText(In("work", "a.md"), "one\n\nthree\n");
        var (_, errors, _, _) = Run("reg x = \"\"\nreg x += first\npeek a.md +2 1\nreg notes = top\nreg notes += $peek\n" +
                                    "reg notes += end\nreg y +=\n");
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        Assert.Equal("first", ShellRegisters.Get("x"));
        Assert.Equal("top\n\nend", ShellRegisters.Get("notes"));
    }

    [Fact]
    public void Run_AgainAndScreenInARun_AreNotRun()
    {
        File.WriteAllText(In("work", "a.md"), "one\n");
        CommandHistory.Clear();
        try
        {
            var (_, errors, _, _) = Run("reg r = lines a.md 1 start X\nreg r += !!\nrun $r\nreg s = screen\nrun $s\n",
                terminal: true);
            Assert.Equal("X one\n", Text("a.md"));
            Assert.Contains(Lang.T("again, !n and !! are not run in a run: it would run itself again"), errors);
            Assert.Contains(Lang.T("screen is not run in a run: run it after"), errors);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Run_AFileByAName_PutsTheNameIn()
    {
        Directory.CreateDirectory(In("work", "sub dir"));
        File.WriteAllText(In("work", "sub dir", "t.txt"), "insert ../log.md bottom ran\n");
        File.WriteAllText(In("work", "log.md"), "# Log\n");
        var (_, errors, _, _) = Run("reg d = sub dir\ncd $d\nrun $pwd/t.txt\npeek t.txt\nrun $file\nrun \"" +
                                    In("work", "sub dir", "t.txt") + "\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Log\nran\nran\nran\n", Text("log.md"));
    }

    [Fact]
    public void Run_AFileWithCrlfAndAByteOrderMark()
    {
        File.WriteAllText(In("work", "log.md"), "# Log\n");
        File.WriteAllBytes(In("work", "w.txt"), System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes("# weekly\r\ninsert log.md bottom one\r\n\r\ninsert log.md bottom two\r\n"))
            .ToArray());
        var (_, errors, _, _) = Run("run w.txt\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Log\none\ntwo\n", Text("log.md"));
    }

    [Fact]
    public void Lines_IntoARegisterThatHoldsAPath_IsRefused_AndAMultiLineRegisterChangesLines()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\n");
        var (_, errors, _, _) = Run("reg plan " + In("work", "plot.md") + "\nlines a.md 1 > $plan\nlines a.md 1 >> $plan\n" +
                                    "lines a.md 2-3 > $more\nchange a.md +1 $more\n");
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.F("${0} holds a path: give the place in that file (> ${0} bottom), or reg -d {0} first", "plan"),
            errors);
        Assert.Equal(In("work", "plot.md"), ShellRegisters.Get("plan"));
        Assert.Equal("two\nthree\ntwo\nthree\n", Text("a.md"));
    }

    [Fact]
    public void Peek_StepsAlone_TakeNothingAfterThem_AndStopAtTheEnds()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\n");
        var (output, errors, _, _) = Run("+ 7\npeek a.md 1\n-\n--\npeek a.md +2147483647\n+\npeek plot.md -\n");
        Assert.Contains(Lang.F("{0} takes nothing after it: {0} alone, or peek file {0}", "+"), errors);
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        // - and -- at the top: line 1; a peek past the end shows nothing, and + steps from the last window shown;
        // another file: its top
        Assert.Equal(new[] { (1, 1, 3), (1, 1, 3), (1, 1, 3), (2, 2, 3), (1, 1, 1) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, w.Item3)), windows);
    }

    [Fact]
    public void Peek_Steps_FromTheSettingsFile_AFaultyValueIsTheDefault()
    {
        Assert.Equal(1, Shell.ParsePeekStep("0", 1));
        Assert.Equal(5, Shell.ParsePeekStep("1001", 5));
        Assert.Equal(5, Shell.ParsePeekStep("x", 5));
        Assert.Equal(5, Shell.ParsePeekStep(null, 5));
        Assert.Equal(1000, Shell.ParsePeekStep(" 1000 ", 5));
    }

    [Fact]
    public void Fav_ARowInAFolder_IsShownIn_AndAFileNamedWithABracket_IsRemovedByName()
    {
        File.WriteAllText(In("work", "[2024].md"), "x\n");
        FourFavorites();
        var (output, errors, _, _) = Run("fav folder A\nfav folder B\nfav move 1-2 [A]\nfav move [B] [A]\nfav move 2 bottom\n" +
                                         "fav add [2024].md\nfav remove [2024].md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("c.md|[A]|.a.md|.b.md|.[B]|d.md", FavoritesShape());
        // The rows of A are shown in under it, its subfolder B as far in as its rows; d.md, moved to the bottom, is
        // after the folders (in none)
        Assert.Contains("[A]\n    2  a.md\n    3  b.md\n  [B]\n  4  d.md\n", output);
        Assert.Contains(Lang.F("Taken off the favorites: {0}", In("work", "[2024].md")), output);
    }

    [Fact]
    public void Fav_MoveAFolderDown_KeepsItOpenOrClosed_AndTheEndsAreTold()
    {
        FourFavorites();
        Run("fav folder A\nfav folder B\n");
        FileFavorites.Load();
        FileFavorites.Items.First(item => item.Name == "A").Collapsed = true;
        FileFavorites.Save();
        var (_, errors, _, _) = Run("fav move [A] down\nfav move [A] down\nfav move 1 up\n");
        Assert.Equal("a.md|b.md|c.md|d.md|[B]|[A]", FavoritesShape());
        Assert.True(FileFavorites.Items.First(item => item.Name == "A").Collapsed);
        Assert.Contains(Lang.F("[{0}] is at the end", "A"), errors);
        Assert.Contains(Lang.T("The row is at the top"), errors);
    }

    [Fact]
    public void Fav_Undo_AfterAnotherListWasOpened_TellsWhichList()
    {
        try
        {
            FourFavorites();
            var (_, errors, _, _) = Run("fav\nfav remove 1\nfav new other\nfav undo\nfav main\nfav undo\n", yes: true);
            Assert.Contains(Lang.F("fav undo puts back in the list {0}: open it first (fav {0})", "main"), errors);
            Assert.Equal("a.md|b.md|c.md|d.md", FavoritesShape());
        }
        finally
        {
            FileFavorites.UseList(string.Empty);
        }
    }

    [Fact]
    public void Again_TheMessagesOfNumbersNotKept()
    {
        CommandHistory.Clear();
        CommandHistory.Add(CommandContext.Shell, "pwd");
        try
        {
            var (_, errors, _, _) = Run("!0\n!5\n");
            Assert.Contains(Lang.F("No command {0}: 1 is kept (again lists it)", 0), errors);
            Assert.Contains(Lang.F("No command {0}: 1 is kept (again lists it)", 5), errors);
            CommandHistory.Clear();
            (_, errors, _, _) = Run("!!\n");
            Assert.Contains(Lang.T("No commands typed: from a file or a pipe they are not kept"), errors);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Help_OfPeekVariableAndBang_AndListsAndFindWithDashL()
    {
        File.WriteAllText(In("work", ".hidden.md"), "plot here\n");
        if (OperatingSystem.IsWindows()) File.SetAttributes(In("work", ".hidden.md"), FileAttributes.Hidden);
        var (output, errors, _, _) = Run("help $peek\nhelp !!\nhelp !3\nls -l -a\nfind -l -t Plot\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("peek [file|n] +|++|-|--", output);
        Assert.Contains("again [n]", output);
        Assert.Contains("  " + In("work", ".hidden.md") + "\n", output);
        Assert.Contains(In("work", "plot.md") + ":1  Plot", output);
    }

    [Fact]
    public void Format_OfAFileThatNeedsNothing_IsStillTheFileWorkedOn()
    {
        File.WriteAllText(In("work", "page.css"), "a {\n    color: red;\n}\n");
        var (_, errors, _, session) = Run("format page.css\npeek $file\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "page.css"), session.LastList.Single().Path);
    }

    [Fact]
    public void Tab_AfterRun_AndRegName_AndFavFolders()
    {
        Run("fav folder Drafts\n");
        Assert.Equal("run plot.md", Tab(Completion(), "run plo"));
        Assert.Equal("reg notes +=", Tab(Completion(), "reg notes +"));
        Assert.Equal("fav rename [Drafts]", Tab(Completion(), "fav rename [D"));
        Assert.Equal("fav subfolder [Drafts]", Tab(Completion(), "fav subfolder [D"));
    }

    // ---- Favorites: a place to add at, names to move, a filter (1.5.3)

    [Fact]
    public void FavAdd_WithAPlace_GoesThere_AndWithoutOne_AtTheBottom()
    {
        FourFavorites();
        foreach (var name in new[] { "e.md", "f.md", "g.md", "h.md" }) File.WriteAllText(In("work", name), "x\n");
        var (output, errors, _, _) = Run("fav folder Drafts\nfav add e.md top\nfav add f.md [Drafts]\nfav add g.md 2\n" +
                                         "fav add h.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("e.md|g.md|a.md|b.md|c.md|d.md|[Drafts]|.f.md|h.md", FavoritesShape());
        Assert.Contains(Lang.F("Added to the favorites: {0}", In("work", "f.md")) + "\n" + Lang.F("List: {0}", "main"),
            output);
    }

    [Fact]
    public void FavAdd_APlaceThatIsNotThere_AddsNothing()
    {
        FourFavorites();
        File.WriteAllText(In("work", "e.md"), "x\n");
        var (output, errors, _, _) = Run("fav add e.md [Nowhere]\nfav add e.md 9\nfav add e.md up\nfav add a.md top\n");
        Assert.Contains(Lang.F("No folder [{0}] in the favorites (fav shows them)", "Nowhere"), errors);
        Assert.Contains(Lang.F("No row {0}: the favorites have {1} rows", 9, 5), errors);
        Assert.Contains(Lang.T("A new favorite goes to top, bottom, a row (5) or a [folder]: fav add notes.md [Drafts]"),
            errors);
        Assert.Contains(Lang.F("Already a favorite: {0} (fav move puts it elsewhere)", In("work", "a.md")), output);
        Assert.Equal("a.md|b.md|c.md|d.md", FavoritesShape());
    }

    [Fact]
    public void FavMove_ByNameAndByPattern()
    {
        foreach (var name in new[] { "chapter-1.md", "chapter-2.md", "notes.md" }) File.WriteAllText(In("work", name), "x\n");
        Directory.CreateDirectory(In("work", "research"));
        var (_, errors, _, _) = Run("fav add notes.md\nfav add chapter-1.md\nfav add research\nfav add chapter-2.md\n" +
                                    "fav folder Book\nfav move chapter-*.md [Book]\nfav move NOTES.md bottom\n" +
                                    "fav move research/ top\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("research|[Book]|.chapter-1.md|.chapter-2.md|notes.md", FavoritesShape());
        // By a path, and a name that is not there
        (_, errors, _, _) = Run("fav move ./notes.md top\nfav move nothing.md top\n");
        Assert.Equal("notes.md|research|[Book]|.chapter-1.md|.chapter-2.md", FavoritesShape());
        Assert.Contains(Lang.F("No favorite named {0} (fav shows them)", "nothing.md"), errors);
    }

    [Fact]
    public void FavMove_ANameTwoFavoritesHave_TellsTheirRows()
    {
        Directory.CreateDirectory(In("work", "one"));
        Directory.CreateDirectory(In("work", "two"));
        File.WriteAllText(In("work", "one", "README.md"), "x\n");
        File.WriteAllText(In("work", "two", "README.md"), "x\n");
        File.WriteAllText(In("work", "a.md"), "x\n");
        var (_, errors, _, _) = Run("fav add a.md\nfav add one/README.md\nfav add two/README.md\nfav move readme.md top\n" +
                                    "fav move two/README.md top\n");
        Assert.Contains(Lang.F("{0} favorites are named {1}: give the row ({2})", 2, "readme.md", "2, 3"), errors);
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        FileFavorites.Load();
        Assert.Equal(In("work", "two", "README.md"), FileFavorites.Items[0].Path);
    }

    [Fact]
    public void FavFilter_ShowsTheRowsWithTheText_NumberedAsTheWholeList()
    {
        foreach (var name in new[] { "plot.md", "draft-1.md", "notes.md", "draft-2.typ" })
            File.WriteAllText(In("work", name), "x\n");
        Run("fav add plot.md\nfav add draft-1.md\nfav add notes.md\nfav add draft-2.typ\nfav folder Drafts\n" +
            "fav subfolder [Drafts] Old\nfav move 4 [Drafts/Old]\n");
        var (output, errors, _, session) = Run("fav filter DRAFT\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(Lang.F("List: {0}", "main") + "\n  2  draft-1.md\n[Drafts]\n  [Old]\n      4  draft-2.typ\n", output);
        // The last list is the whole list: row 3 is notes.md
        Assert.Equal(4, session.LastList.Count);
        Assert.Equal(In("work", "notes.md"), session.LastList[2].Path);
        (output, _, _, _) = Run("fav filter *.typ\nfav -l filter plot\nfav filter nothing\n");
        Assert.Contains("      4  draft-2.typ\n", output);
        Assert.DoesNotContain("draft-1.md", output);
        Assert.Contains("  1  " + In("work", "plot.md") + "\n", output);
        Assert.Contains(Lang.F("No favorites with {0} in them", "nothing"), output);
        (_, errors, _, _) = Run("fav filter\n");
        Assert.Contains(Lang.T("Give a text: fav filter draft (or *.md)"), errors);
    }

    [Fact]
    public void Tab_AfterFavMove_TheNamesOfTheFavorites_AlsoFromAPattern_AndAfterFavAdd_ThePlaces()
    {
        foreach (var name in new[] { "chapter-1.md", "chapter-2.md", "my notes.md" }) File.WriteAllText(In("work", name), "x\n");
        Run("fav add chapter-1.md\nfav add chapter-2.md\nfav add \"my notes.md\"\nfav folder Drafts\n");
        Assert.Equal("fav move chapter-1.md", Tab(Completion(), "fav move ch"));
        Assert.Equal("fav move chapter-2.md", Tab(Completion(), "fav move *2*"));
        Assert.Equal("fav move \"my notes.md\"", Tab(Completion(), "fav move my"));
        Assert.Equal("fav move [Drafts]", Tab(Completion(), "fav move [D"));
        Assert.Equal("fav add plot.md top", Tab(Completion(), "fav add plot.md t"));
        Assert.Equal("fav add plot.md [Drafts]", Tab(Completion(), "fav add plot.md [D"));
        Assert.Equal("fav filter", Tab(Completion(), "fav fil"));
    }

    [Fact]
    public void HistFilter_ShowsTheFilesWithTheText_NumberedAsTheWholeHistory()
    {
        Run("new chapter-1\nnew notes\nnew chapter-2.typ\n");
        var (output, errors, _, session) = Run("hist filter CHAPTER\n");
        Assert.Equal(string.Empty, errors);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("  1  chapter-2.typ", lines[0]);
        Assert.StartsWith("  3  chapter-1.md", lines[1]);
        // The last list is the whole history: row 2 is notes.md
        Assert.Equal(3, session.LastList.Count);
        Assert.Equal(In("work", "notes.md"), session.LastList[1].Path);
        (output, _, _, _) = Run("hist filter *.typ\nhist -l filter notes\nhist filter nothing\n");
        Assert.Contains("  1  chapter-2.typ", output);
        Assert.Contains("  2  " + In("work", "notes.md") + "  ", output);
        Assert.DoesNotContain("chapter-1.md", output);
        Assert.Contains(Lang.F("No files with {0} in them", "nothing"), output);
        (_, errors, _, _) = Run("hist filter\nrecent filter x\n");
        Assert.Contains(Lang.T("Give a text: hist filter draft (or *.md)"), errors);
        Assert.Contains(Lang.F("Not taken: {0} (only -l, for the full paths)", "filter x"), errors);
    }

    [Fact]
    public void HotFilter_ShowsTheDirectoriesWithTheText()
    {
        foreach (var name in new[] { "novel", "notes", "site" }) Directory.CreateDirectory(In("work", name));
        var (output, errors, _, session) = Run("hot add novel\nhot add notes\nhot add site\nhot filter no*\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  " + In("work", "novel") + "/\n  2  " + In("work", "notes") + "/\n", output);
        Assert.DoesNotContain(In("work", "site") + "/", output);
        Assert.Equal(3, session.LastList.Count);
        (output, _, _, _) = Run("hot filter SITE\nhot filter nothing\n");
        Assert.Contains("  3  " + In("work", "site") + "/\n", output);
        Assert.Contains(Lang.F("No directories with {0} in them", "nothing"), output);
        (_, errors, _, _) = Run("hot filter\n");
        Assert.Contains(Lang.T("Give a text: hot filter notes"), errors);
        Assert.Equal("hot filter", Tab(Completion(), "hot fi"));
        Assert.Equal("hist filter", Tab(Completion(), "hist fi"));
        Assert.Equal("hist -l filter", Tab(Completion(), "hist -l fi"));
    }
}
