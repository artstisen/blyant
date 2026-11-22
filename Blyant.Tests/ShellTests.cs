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
        ShellRegisters.Calc = null;
        ShellSession.ForgetFind();
        FileTags.FilePathOverride = Path.Combine(_data, FileTags.FileName);
        FileTags.Clear();
        ShellHistory.FilePathOverride = Path.Combine(_data, ShellHistory.FileName);
        ShellHistory.Reset();
        PeekMarks.FilePathOverride = Path.Combine(_data, PeekMarks.FileName);
        PeekMarks.Clear();
        ShellSession.ForgetTag();
        ShellSession.ForgetPreviousDirectory();
        // (the tests written before the markers and the setting shellpeeklines see the output as it was then: the
        // tests of them turn them on)
        Shell.Markers = false;
        Shell.PeekLines = 20;
        // (and the logo left out: the tests of it turn it on)
        Shell.Logo = false;
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
        ShellRegisters.Calc = null;
        ShellSession.ForgetFind();
        FileTags.FilePathOverride = null;
        FileTags.Clear();
        ShellHistory.FilePathOverride = null;
        ShellHistory.Reset();
        PeekMarks.FilePathOverride = null;
        PeekMarks.Clear();
        ShellSession.ForgetTag();
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
        FileHistory.Add(In("work", "plot.md"), DateTime.UtcNow);
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
        // (after a search, "find" alone shows it again: without one it asks for a name)
        ShellSession.ForgetFind();
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
    public void File_IsKeptWhenTheShellIsLeft_AndIsFileAgainInTheNextShellAtATerminal()
    {
        try
        {
            Shell.StartSetting = string.Empty;
            File.WriteAllText(In("work", "kept.md"), "x\n");
            var (_, _, _, session) = Run("peek kept.md\n", terminal: true);
            Shell.RememberLast(session.Directory, session.WorkedFile);
            Assert.Equal(In("work", "kept.md"), Shell.LastFile);
            Assert.Equal(In("work", "kept.md"), FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelllastfile"]);
            // (shelldir is not "last": the directory is not kept)
            Assert.Equal(string.Empty, FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelllastdir"]);
            // The next shell at a terminal has it as $file; commands from a file do not
            var (output, errors, _, _) = Run("insert $file bottom more\n", terminal: true);
            Assert.Equal(string.Empty, errors);
            Assert.Equal("x\nmore\n", Text("kept.md"));
            (_, errors, _, _) = Run("insert $file bottom again\n");
            Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"), errors);
            // A file that is gone is not $file
            File.Delete(In("work", "kept.md"));
            (_, errors, _, _) = Run("peek $file\n", terminal: true);
            Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"), errors);
        }
        finally
        {
            Shell.LastFile = string.Empty;
        }
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
        Assert.Contains(Lang.T("Settings unchanged"), kept);
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
            ["C-w"] = new('\u0017', ConsoleKey.W, false, false, true),
            ["C-a"] = new('\u0001', ConsoleKey.A, false, false, true),
            ["C-e"] = new('\u0005', ConsoleKey.E, false, false, true),
            ["C-Left"] = new('\0', ConsoleKey.LeftArrow, false, false, true),
            ["C-Right"] = new('\0', ConsoleKey.RightArrow, false, false, true),
            ["M-b"] = new('b', ConsoleKey.B, false, true, false),
            ["M-f"] = new('f', ConsoleKey.F, false, true, false)
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
        Assert.Contains("  input  ", output);
        Assert.DoesNotContain("  resume  ", output);
        Assert.Contains("  /text  ", output);
        Assert.Contains("  /, ?  ", output);
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
        Assert.Contains(Lang.F("{0} has no settings: settings unchanged", FileIo.SettingsFileName), errors);
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
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg, run and echo take it", "clip"), errors);
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
        Assert.Contains(Lang.T("$clip is the text clip copied last: it cannot be set or taken away"), errors);
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
        var (_, errors, code, _) = Run("lines notes.md 1 plan.md bottom\nlines notes.md 1 > notes.md top cut\n" +
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
            Assert.Contains(Lang.T("Main list cannot be renamed"), errors);
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
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -s sizes, -f folder/name, -n by name)", "5"), others);
        Assert.Contains(Lang.F("Not taken: {0} (only -l, for the full paths)", "work"), others);
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
        // (cut: within one file it is refused, so the test tells whether the two names are one file)
        var (_, errors, _, _) = Run("lines case/Notes.md 1 > case/notes.md bottom cut\n");
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
    [InlineData("one two", "remove", " two", false, "", "one")]
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
        Run("fav add a.md bottom\nfav add b.md bottom\nfav add c.md bottom\nfav add d.md bottom\n");
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
        // (a.md added at the top)
        Assert.Equal("a.md|c.md|d.md", FavoritesShape());
        // (a.md removed, then b.md added: the list is not as the removal left it)
        errors = Run("fav\nfav remove 1\nfav add b.md\nfav undo\n", yes: true).Errors;
        Assert.Contains(Lang.T("The favorites have changed since: nothing is put back"), errors);
        Assert.Equal("b.md|c.md|d.md", FavoritesShape());
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
        Assert.Contains(Lang.F("{0} folders are named [{1}]: give the parent folder too, [Writing/{1}]", 2, "X"), errors);
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
    [InlineData("again filter lines", null)]
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
            ShellRegisters.Clock = () => DateTime.UtcNow;
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
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg, run and echo take it", "peek"), errors);
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
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg, run and echo take it", "notes"), errors);
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
            ShellRegisters.Clock = () => DateTime.UtcNow;
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
            Assert.Contains(Lang.T("again, !n and !! are refused in a run: it would repeat itself"), errors);
            Assert.Contains(Lang.T("screen is refused in a run: run it after"), errors);
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
    public void Peek_StepsAlone_TakeANumberOrNothing_AndStopAtTheEnds()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\n");
        var (output, errors, _, _) = Run("+ x\npeek a.md 1\n-\n--\npeek a.md +2147483647\n+\npeek plot.md -\n");
        Assert.Contains(Lang.F("{0} takes a number of steps, or nothing: {0} 20", "+"), errors);
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        // - and -- at the top: line 1; a peek past the end shows nothing, but is the window: + from it is the last
        // line; another file: its top
        Assert.Equal(new[] { (1, 1, 3), (1, 1, 3), (1, 1, 3), (3, 3, 3), (1, 1, 1) }
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
    public void FavAdd_WithAPlace_GoesThere_AndWithoutOne_AtTheTop()
    {
        FourFavorites();
        foreach (var name in new[] { "e.md", "f.md", "g.md", "h.md" }) File.WriteAllText(In("work", name), "x\n");
        File.WriteAllText(In("work", "i.md"), "x\n");
        var (output, errors, _, _) = Run("fav folder Drafts\nfav add e.md top\nfav add f.md [Drafts]\nfav add g.md 2\n" +
                                         "fav add h.md\nfav add i.md bottom\n");
        Assert.Equal(string.Empty, errors);
        // h.md without a place: at the top; i.md at the bottom, after the folder
        Assert.Equal("h.md|e.md|g.md|a.md|b.md|c.md|d.md|[Drafts]|.f.md|i.md", FavoritesShape());
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
        // (the rows as they are before the favorite is added)
        Assert.Contains(Lang.F("No row {0}: the favorites have {1} rows", 9, 4), errors);
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
        var (_, errors, _, _) = Run("fav add notes.md bottom\nfav add chapter-1.md bottom\nfav add research bottom\nfav add chapter-2.md bottom\n" +
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
        var (_, errors, _, _) = Run("fav add a.md bottom\nfav add one/README.md bottom\nfav add two/README.md bottom\nfav move readme.md top\n" +
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
        Run("fav add plot.md bottom\nfav add draft-1.md bottom\nfav add notes.md bottom\nfav add draft-2.typ bottom\nfav folder Drafts\n" +
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
        Run("fav add chapter-1.md bottom\nfav add chapter-2.md bottom\nfav add \"my notes.md\" bottom\nfav folder Drafts\n");
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
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -s sizes, -f folder/name, -n by name)", "filter x"), errors);
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

    [Fact]
    public void DotAndPeekAlone_ShowTheLastPeekAgain_AsTheFileIsNow()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\nfour\nfive\n");
        var (output, errors, _, session) = Run(".\npeek a.md +2 2\nchange a.md +2 TWO\n.\ninsert a.md top zero\npeek\n. 3\n");
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        Assert.Contains(Lang.T(". takes nothing after it: . alone shows the last peek again"), errors);
        // The same window (lines 2-3), read again: after the change, and after a line put in above it
        Assert.Contains("  2  TWO\n  3  three\n(lines 2-3 of 5)", output);
        Assert.Contains("  2  one\n  3  TWO\n(lines 2-3 of 6)", output);
        Assert.Equal("one\nTWO", ShellRegisters.Peek);
        Assert.Equal((2, 1), session.LastList.Single().At);
        Assert.Contains("peek (or .)", Run("help .\n").Output);
    }

    [Fact]
    public void AutoPeek_ShowsTheWindowAgain_AfterACommandChangedItsFile()
    {
        try
        {
            Shell.AutoPeek = true;
            File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\n");
            File.WriteAllText(In("work", "b.md"), "x\n");
            var (output, errors, _, _) = Run("peek a.md 1-2\ninsert b.md bottom y\nchange a.md +1 ONE\nls\n");
            Assert.Equal(string.Empty, errors);
            var again = Lang.F("(peek again: {0})", "a.md");
            // Once: after the change of a.md, not after b.md was changed, nor after ls
            Assert.Single(output.Split('\n'), line => line == again);
            Assert.Contains(again + "\n  1  ONE\n  2  two\n", output);
            // Off: not shown again
            Shell.AutoPeek = false;
            (output, _, _, _) = Run("peek a.md 1-2\nchange a.md +1 one\n");
            Assert.DoesNotContain(again, output);
        }
        finally
        {
            Shell.AutoPeek = false;
        }
    }

    [Fact]
    public void Peek_TopMiddleBottomPercentAndALine_KeepTheSizeOfTheWindow()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 100).Select(i => "line " + i + "\n")));
        var (output, errors, _, session) = Run("peek long.md 10-12\npeek bottom\npeek top\npeek middle\npeek 50%\n" +
                                               "peek +40\npeek long.md 90%\npeek 100%\npeek 0%\n");
        Assert.Equal(string.Empty, errors);
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        Assert.Equal(new[] { (10, 12), (98, 100), (1, 3), (49, 51), (50, 52), (40, 42), (90, 92), (98, 100), (1, 3) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 100)), windows);
        Assert.Equal((98, 1), Run("peek long.md 10-12\npeek bottom\n").Session.LastList.Single().At);
        // Another file: the window of 20 lines
        File.WriteAllText(In("work", "b.md"), string.Concat(Enumerable.Range(1, 50).Select(i => "b " + i + "\n")));
        (output, _, _, _) = Run("peek b.md bottom\n");
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 31, 50, 50), output);
        // Not a place: told
        (_, errors, _, _) = Run("peek long.md 101%\n");
        Assert.Contains(Lang.F("Not lines: {0} (as 40, 100-120, or +100 30)", "101%"), errors);
    }

    [Fact]
    public void PeekAll_ShowsTheWholeFile_AndKeepsTheWindowAsItWas()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 100).Select(i => "line " + i + "\n")));
        // A window of 3 lines at 40: all shows the whole file, then "." and "+" go on from the window at 40
        var (output, errors, _, session) = Run("peek long.md 40-42\npeek all\n.\n+\npeek long.md all\njj\n");
        Assert.Equal(string.Empty, errors);
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        Assert.Equal(new[] { (40, 42), (1, 100), (40, 42), (41, 43), (1, 100), (46, 48) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 100)), windows);
        Assert.Contains("    1  line 1\n", output);
        Assert.Contains("  100  line 100\n", output);
        // $peek: the whole file; the file at its top ("open 1")
        (output, _, _, session) = Run("peek long.md 40-42\npeek all\necho $peek\n");
        Assert.Contains("line 1\nline 2\n", output);
        Assert.Null(session.LastList.Single().At);
        // A file not peeked before: the window at its top, of shellpeeklines lines (+ goes on from line 1)
        File.WriteAllText(In("work", "b.md"), string.Concat(Enumerable.Range(1, 50).Select(i => "b " + i + "\n")));
        (output, _, _, _) = Run("peek long.md 10-14\npeek b.md all\n+\n");
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 2, 21, 50), output);
        // With the markers: the top and the end of the file
        Shell.Markers = true;
        try
        {
            (output, _, _, _) = Run("peek b.md all\n");
            Assert.Contains(ShellSession.TopMarker + "b.md\n", output);
            Assert.Contains(ShellSession.EndMarker + Lang.F("lines {0}-{1} of {2}", 1, 50, 50), output);
        }
        finally
        {
            Shell.Markers = false;
        }

        // Alone before any peek: told; a file named "all" alone is that file
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), Run("peek all\n").Errors);
        File.WriteAllText(In("work", "all"), "the file\n");
        Assert.Contains("  1  the file\n", Run("peek b.md\npeek all\n").Output);
        Assert.Equal("peek plot.md all", Tab(Completion(), "peek plot.md al"));
        Assert.Contains("peek [file|n] all", Run("help peek\n").Output);
    }

    [Fact]
    public void Peek_StepsWithANumber_MoveAsManySteps()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 100).Select(i => "line " + i + "\n")));
        var (output, errors, _, _) = Run("peek long.md 10-12\n+ 20\n- 5\n++ 3\n-- 2\n+ 1000\n");
        Assert.Equal(string.Empty, errors);
        var windows = output.Split('\n').Where(l => l.StartsWith('(')).ToList();
        Assert.Equal(new[] { (10, 12), (30, 32), (25, 27), (40, 42), (30, 32), (98, 100) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 100)), windows);
        (_, errors, _, _) = Run("++ 0\n-- -2\n");
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
    }

    [Fact]
    public void Peek_APlaceAlone_BeforeAnyPeek_IsTold_AndAFileNamedPlusANumberIsPeeked()
    {
        var (_, errors, _, _) = Run("peek bottom\npeek +40\n");
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        File.WriteAllText(In("work", "+40"), "the file\n");
        File.WriteAllText(In("work", "a.md"), "a\n");
        var (output, _, _, _) = Run("peek a.md\npeek +40\n");
        Assert.Contains("  1  the file\n", output);
        Assert.Contains("+ n, - n, ++ n, -- n", Run("help +\n").Output);
        Assert.Equal("peek bottom", Tab(Completion(), "peek bo"));
        Assert.Equal("peek plot.md middle", Tab(Completion(), "peek plot.md mi"));
    }

    [Fact]
    public void AgainFilter_ShowsTheCommandsWithTheText_WithTheirNumbers()
    {
        CommandHistory.Clear();
        foreach (var command in new[] { "lines a.md 1 start X", "pwd", "peek plot.md", "lines b.md 2 end Y" })
            CommandHistory.Add(CommandContext.Shell, command);
        try
        {
            var (output, errors, _, _) = Run("again filter LINES\nagain filter peek*\nagain filter nothing\nagain filter\n");
            Assert.Contains("  1  lines b.md 2 end Y\n  4  lines a.md 1 start X\n", output);
            Assert.Contains("  2  peek plot.md\n", output);
            Assert.DoesNotContain("  3  pwd", output);
            Assert.Contains(Lang.F("No commands with {0} in them", "nothing"), output);
            Assert.Contains(Lang.T("Give a text: again filter lines (or lines*)"), errors);
            Assert.Equal("again filter", Tab(Completion(), "again fi"));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    // ---- Review of 1.5.3

    [Fact]
    public void AutoPeek_LeavesTheLastListFileAndPeekOfTheCommand()
    {
        try
        {
            Shell.AutoPeek = true;
            File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\n");
            File.WriteAllText(In("work", "b.md"), "x\n");
            var (output, errors, _, session) = Run("peek a.md 1-2\nlines a.md 1 > b.md bottom cut\n");
            Assert.Equal(string.Empty, errors);
            Assert.Contains(Lang.F("(peek again: {0})", "a.md"), output);
            // The last list and $file are those of lines: b.md, at the line put in; $peek is still the peek
            Assert.Equal(In("work", "b.md"), session.LastList.Single().Path);
            Assert.Equal(In("work", "b.md"), session.WorkedFile);
            Assert.Equal("one\ntwo", ShellRegisters.Peek);
        }
        finally
        {
            Shell.AutoPeek = false;
        }
    }

    [Fact]
    public void AutoPeek_AFileShorterThanTheWindow_ShowsItsLastLines_Once()
    {
        try
        {
            Shell.AutoPeek = true;
            File.WriteAllText(In("work", "a.md"), string.Concat(Enumerable.Range(1, 10).Select(i => "line " + i + "\n")));
            var (output, errors, _, session) = Run("peek a.md +8 3\nchange a.md 3-10\nls\npwd\nopen 2\n", yes: true);
            var again = Lang.F("(peek again: {0})", "a.md");
            Assert.Single(output.Split('\n'), line => line == again);
            // The window as large as before, at the end of the shorter file
            Assert.Contains(again + "\n  1  line 1\n  2  line 2\n(lines 1-2 of 2)", output);
            Assert.DoesNotContain(Lang.F("No row {0}: the last list has 1 row", 2), errors);
        }
        finally
        {
            Shell.AutoPeek = false;
        }
    }

    [Fact]
    public void Peek_APlace_KeepsTheSizeOfTheWindow_OnAShortFile()
    {
        // The window of 20 lines put at the top of a 5-line file stays 20 lines: with the file grown, "." shows 20
        File.WriteAllText(In("work", "f.md"), "1\n2\n3\n4\n5\n");
        File.WriteAllText(In("work", "g.md"), string.Concat(Enumerable.Range(6, 25).Select(i => i + "\n")));
        var (output, errors, _, _) = Run("peek f.md\npeek top\nlines g.md 1-25 > f.md bottom\n.\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 1, 5, 5), output);
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 1, 20, 30), output);
        // A 2-line window at the bottom of a 3-line file
        File.WriteAllText(In("work", "s.md"), "a\nb\nc\n");
        (output, _, _, _) = Run("peek s.md +2 2\npeek bottom\n");
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 2, 3, 3), output);
    }

    [Fact]
    public void Peek_FollowsItsFile_WhenItIsRenamedOrMovedInTheShell()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\n");
        Directory.CreateDirectory(In("work", "d"));
        var (output, errors, _, _) = Run("peek a.md 2\nrename a.md b.md\n.\nmove b.md d\n+\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  one\n  2  two\n", output);
    }

    [Fact]
    public void Peek_AWordAlone_ThatIsAFileHere_IsThatFile_AndAnEmptyFileIsAWindow()
    {
        File.WriteAllText(In("work", "top"), "the file named top\n");
        File.WriteAllText(In("work", "empty.md"), "");
        var (output, errors, _, _) = Run("peek top\npeek empty.md\npeek bottom\n");
        Assert.Contains("  1  the file named top\n", output);
        Assert.Equal(string.Empty, errors);
        Assert.Equal(2, output.Split('\n').Count(line => line == Lang.F("{0} has {1} lines", In("work", "empty.md"), 0)));
    }

    [UnixFact]
    public void OnLinuxAndMacOS_DotPeeksAgain_APathWithAQuote()
    {
        Directory.CreateDirectory(In("work", "q"));
        File.WriteAllText(In("work", "q", "my \"draft\".md"), "one\n");
        var (output, errors, _, _) = Run("cd q\nls\npeek 1\n.\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(2, output.Split('\n').Count(line => line == "  1  one"));
    }

    [Fact]
    public void FavAdd_TheRowAfterTheLast_IsTheBottom_AndAnUnknownPlaceIsToldForAdd()
    {
        FourFavorites();
        File.WriteAllText(In("work", "e.md"), "x\n");
        var (_, errors, _, _) = Run("fav add e.md sideways\nfav add e.md 5\n");
        Assert.Contains(Lang.T("A new favorite goes to top, bottom, a row (5) or a [folder]: fav add notes.md [Drafts]"),
            errors);
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        Assert.Equal("a.md|b.md|c.md|d.md|e.md", FavoritesShape());
        // An empty list: the rows it has are none
        FileFavorites.Load();
        while (FileFavorites.Items.Count > 0) FileFavorites.RemoveAt(0);
        FileFavorites.Save();
        (_, errors, _, _) = Run("fav add e.md 2\n");
        Assert.Contains(Lang.F("No row {0}: the favorites have {1} rows", 2, 0), errors);
    }

    [Fact]
    public void FavFilter_KeepsThePlaceOfAFavorite()
    {
        FourFavorites();
        FileFavorites.Load();
        FileFavorites.Items.First(item => item.Path == In("work", "c.md")).Line = 7;
        FileFavorites.Save();
        var session = Run("fav filter c.md\n").Session;
        Assert.Equal((7, 1), session.LastList[2].At);
    }

    [Fact]
    public void RememberLast_ChangesOnlyItsLines_AndKeepsWhatWasEditedByHand()
    {
        try
        {
            Shell.StartSetting = "last";
            FileIo.SaveSettings();
            var file = Path.Combine(_data, "settings.ini");
            File.WriteAllText(file, File.ReadAllText(file).Replace("indent=4", "indent=2") + "; a note of my own\n");
            Shell.RememberLast(In("work"), In("work", "plot.md"));
            var text = File.ReadAllText(file);
            Assert.Contains("indent=2\n", text);
            Assert.Contains("; a note of my own\n", text);
            Assert.Equal(In("work", "plot.md"), FileIo.ParseSettings(text)["shelllastfile"]);
            Assert.Equal(In("work"), FileIo.ParseSettings(text)["shelllastdir"]);
            // A path with a line break is not kept
            Shell.RememberLast(In("work"), In("work", "a\nb.md"));
            Assert.Equal(In("work", "plot.md"), Shell.LastFile);
        }
        finally
        {
            Shell.StartSetting = string.Empty;
            Shell.LastFile = string.Empty;
            Shell.LastDirectory = string.Empty;
        }
    }

    [Fact]
    public void Patterns_TakeABackslash_AsItself()
    {
        Assert.True(ShellCompletion.PatternFits(@"*C:\Users*", @"open C:\Users\x.md"));
        Assert.True(ShellCompletion.PatternFits(@"a\b*", @"a\b.md"));
        Assert.False(ShellCompletion.PatternFits(@"a\b*", "ab.md"));
        CommandHistory.Clear();
        CommandHistory.Add(CommandContext.Shell, @"open C:\Users\x.md");
        CommandHistory.Add(CommandContext.Shell, "pwd");
        try
        {
            Assert.Contains(@"  2  open C:\Users\x.md", Run("again filter *C:\\Users*\n").Output);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Log_StepsWithANumber_AreLoggedAsTheStep()
    {
        Assert.Equal("+", EventLog.CommandName("+ 20"));
        Assert.Equal("--", EventLog.CommandName("-- 3"));
        Assert.Equal(".", EventLog.CommandName("."));
    }

    [UnixFact]
    public void OnLinuxAndMacOS_FavMove_NamesThatDifferOnlyInCase_AreToldWithTheirRows()
    {
        File.WriteAllText(In("work", "Notes.md"), "x\n");
        File.WriteAllText(In("work", "notes.md"), "y\n");
        // (a file system that takes them as one name, as macOS usually does: nothing to tell apart)
        if (Directory.GetFiles(In("work"), "*otes.md").Length < 2) return;
        var (_, errors, _, _) = Run("fav add Notes.md bottom\nfav add notes.md bottom\nfav move notes.md top\nfav move ./notes.md top\n");
        Assert.Contains(Lang.F("{0} favorites are named {1}: give the row ({2})", 2, "notes.md", "1, 2"), errors);
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        FileFavorites.Load();
        Assert.Equal(In("work", "notes.md"), FileFavorites.Items[0].Path);
    }

    // ---- Headings by their start or a pattern; Tab with a row or $file

    [Fact]
    public void Shift_AHeading_ByItsStart_OrAPattern_TheWholeNameFirst()
    {
        const string todo = "item\n##### Done herunder\n## Done\n## What?\n";
        foreach (var (place, expected) in new[]
                 {
                     // The whole name first: "Done" is a heading, not the start of "Done herunder"
                     ("Done", "##### Done herunder\n## Done\nitem\n## What?\n"),
                     ("\"Done her\"", "##### Done herunder\nitem\n## Done\n## What?\n"),
                     ("\"*herunder\"", "##### Done herunder\nitem\n## Done\n## What?\n"),
                     // A heading with "?" in it: by its whole name
                     ("\"What?\"", "##### Done herunder\n## Done\n## What?\nitem\n"),
                 })
        {
            File.WriteAllText(In("work", "t.md"), todo);
            var (_, errors, _, _) = Run("shift t.md 1 " + place + "\n");
            Assert.Equal(string.Empty, errors);
            Assert.Equal(expected, Text("t.md"));
        }

        // The start of one heading only, in the command of the report: "shift! $file +35 Done"
        File.WriteAllText(In("work", "u.md"), "- [ ] Buy pencils\n##### Done herunder\n");
        var (_, more, _, _) = Run("peek u.md\nshift! $file +1 Done\n");
        Assert.Equal(string.Empty, more);
        Assert.Equal("##### Done herunder\n- [x] Buy pencils\n", Text("u.md"));
    }

    [Fact]
    public void Shift_AStartOrPatternMoreHeadingsFit_IsTold_AndNothingMoves()
    {
        File.WriteAllText(In("work", "t.md"), "item\n## Chapter 1\n## Chapter 2\n## Notes\n");
        var (output, errors, _, _) = Run("shift t.md 1 Chap\nshift t.md 1 Ch*\nshift t.md 1 Nothing\n");
        Assert.Contains(Lang.F("{0} headings fit {1}: give more of the name, or the line (+12)", 2, "Chap"), errors);
        Assert.Contains(Lang.F("{0} headings fit {1}: give more of the name, or the line (+12)", 2, "Ch*"), errors);
        Assert.Contains(Lang.F("No heading {0} in {1} (or +12, top, bottom)", "Nothing", "t.md"), errors);
        Assert.Contains("  2  ## Chapter 1\n  3  ## Chapter 2\n", output);
        Assert.Equal("item\n## Chapter 1\n## Chapter 2\n## Notes\n", Text("t.md"));
        // The end of a section, by a pattern
        (_, errors, _, _) = Run("shift t.md 1 \"*2+\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("## Chapter 1\n## Chapter 2\nitem\n## Notes\n", Text("t.md"));
    }

    [Fact]
    public void Lines_ToAHeading_ByItsStart()
    {
        File.WriteAllText(In("work", "a.md"), "one\n");
        File.WriteAllText(In("work", "b.md"), "# B\n\n## Done herunder\n");
        var (_, errors, _, _) = Run("lines a.md 1 > b.md Done\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# B\n\n## Done herunder\none\n", Text("b.md"));
    }

    [Fact]
    public void Tab_TheHeadingsOfARowOrOfFile_AlsoFromAPattern()
    {
        File.WriteAllText(In("work", "t.md"), "x\n## Done herunder\n## Chapter 1\n");
        var session = Run("peek t.md\n").Session;
        var completion = new ShellCompletion(() => In("work"), typed => Path.GetFullPath(typed, In("work")))
        {
            FileOf = session.CompletionFile
        };
        Assert.Equal("shift 1 1 \"Done herunder\"", Tab(completion, "shift 1 1 Do"));
        completion = new ShellCompletion(() => In("work"), typed => Path.GetFullPath(typed, In("work")))
        {
            FileOf = session.CompletionFile
        };
        Assert.Equal("shift $file 1 \"Done herunder\"", Tab(completion, "shift $file 1 *herunder"));
        completion = new ShellCompletion(() => In("work"), typed => Path.GetFullPath(typed, In("work")))
        {
            FileOf = session.CompletionFile
        };
        Assert.Equal("lines plot.md 1 > $file \"Chapter 1\"", Tab(completion, "lines plot.md 1 > $file Ch"));
        Assert.Equal(In("work", "t.md"), session.CompletionFile("1"));
        Assert.Equal(In("work", "t.md"), session.CompletionFile("$file"));
    }

    // ---- undo, redo, changes

    [Fact]
    public void Undo_PutsBackTheLastChange_AndRedoDoesItAgain()
    {
        File.WriteAllText(In("work", "t.md"), "one\ntwo\n");
        var (output, errors, _, session) = Run("insert t.md bottom three\nchange t.md +1 ONE\nchanges\nundo\nredo\nundo 2\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\n", Text("t.md"));
        Assert.Contains("  1  change t.md +1 ONE\n  2  insert t.md bottom three\n", output);
        Assert.Contains(Lang.F("Undone: {0}", "change t.md +1 ONE"), output);
        Assert.Contains(Lang.F("Redone: {0}", "change t.md +1 ONE"), output);
        // The file written is the last list
        Assert.Equal(In("work", "t.md"), session.LastList.Single().Path);
        (output, _, _, _) = Run("undo\nredo\n");
        Assert.Contains(Lang.T("Nothing to undo: no file has been changed in this shell"), output);
        Assert.Contains(Lang.T("Nothing to redo"), output);
    }

    [Fact]
    public void Undo_LinesCut_PutsBackBothFiles_InOneStep()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\n");
        File.WriteAllText(In("work", "b.md"), "x\n");
        var (_, errors, _, session) = Run("lines a.md 1 > b.md bottom cut\nundo\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\n", Text("a.md"));
        Assert.Equal("x\n", Text("b.md"));
        Assert.Equal(2, session.LastList.Count);
    }

    [Fact]
    public void Undo_AFileChangedSince_IsNotWrittenOver()
    {
        File.WriteAllText(In("work", "t.md"), "one\n");
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("insert t.md bottom two");
        File.WriteAllText(In("work", "t.md"), "changed elsewhere\n");
        session.Execute("undo");
        Assert.Contains(Lang.F("{0} has changed since: {1} is not undone", In("work", "t.md"), "insert t.md bottom two"),
            errors.ToString());
        Assert.Equal("changed elsewhere\n", Text("t.md"));
    }

    [Fact]
    public void Undo_KeepsTheBytes_LineEndingsAndByteOrderMark()
    {
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes("one\r\ntwo\r\n")).ToArray();
        File.WriteAllBytes(In("work", "w.md"), bytes);
        var (_, errors, _, _) = Run("insert w.md top zero  \ntrim w.md\nundo 2\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(bytes, File.ReadAllBytes(In("work", "w.md")));
    }

    [Fact]
    public void Undo_KeepsAsManyStepsAsSet_AndANewChangeEndsRedo()
    {
        try
        {
            Shell.UndoSteps = 2;
            File.WriteAllText(In("work", "t.md"), "a\n");
            var (output, errors, _, _) = Run("insert t.md bottom b\ninsert t.md bottom c\ninsert t.md bottom d\nchanges\n" +
                                             "undo 5\nredo\ninsert t.md bottom e\nredo\nundo x\n");
            Assert.Contains("  1  insert t.md bottom d\n  2  insert t.md bottom c\n", output);
            Assert.DoesNotContain("  3  insert", output);
            // Two undone (b stays), one done again (c), then a new change: nothing to redo
            Assert.Equal("a\nb\nc\ne\n", Text("t.md"));
            Assert.Contains(Lang.T("Nothing to redo"), output);
            Assert.Contains(Lang.T("Give how many: undo, undo 3 (changes lists them)"), errors);
        }
        finally
        {
            Shell.UndoSteps = Shell.DefaultUndoSteps;
        }
    }

    [Fact]
    public void UndoSteps_FromTheSettingsFile_10To200_Else50()
    {
        Assert.Equal(50, Shell.DefaultUndoSteps);
        Assert.Equal(10, Shell.ParseUndoSteps("10"));
        Assert.Equal(20, Shell.ParseUndoSteps("20"));
        Assert.Equal(200, Shell.ParseUndoSteps(" 200 "));
        Assert.Equal(50, Shell.ParseUndoSteps("201"));
        // 1 to 9, which 1.5.7 took: the fewest there are now
        Assert.Equal(10, Shell.ParseUndoSteps("9"));
        Assert.Equal(10, Shell.ParseUndoSteps("1"));
        Assert.Equal(50, Shell.ParseUndoSteps("0"));
        Assert.Equal(50, Shell.ParseUndoSteps("-20"));
        Assert.Equal(50, Shell.ParseUndoSteps("x"));
        Assert.Equal(50, Shell.ParseUndoSteps(null));
    }

    [Fact]
    public void Undo_KeepsFiftyStepsByDefault()
    {
        Shell.UndoSteps = Shell.DefaultUndoSteps;
        File.WriteAllText(In("work", "f.md"), "a\n");
        var commands = string.Concat(Enumerable.Range(1, 55).Select(i => "insert f.md bottom " + i + "\n"));
        var (output, errors, _, _) = Run(commands + "changes\nundo 60\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(" 50  insert f.md bottom 6\n", output);
        Assert.DoesNotContain("insert f.md bottom 5\n", output);
        // The 50 newest undone: the first 5 lines added stay
        Assert.Equal("a\n1\n2\n3\n4\n5\n", Text("f.md"));
    }

    [Fact]
    public void Undo_InARun_EachCommandIsAStep()
    {
        File.WriteAllText(In("work", "t.md"), "a\n");
        var (output, errors, _, _) = Run("reg r = insert t.md bottom b\nreg r += insert t.md bottom c\nrun $r\nundo\nchanges\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("a\nb\n", Text("t.md"));
        Assert.Contains("  1  insert t.md bottom b\n", output);
        Assert.Contains("insert t.md bottom c", output.Split(Lang.T("Undone (redo does them again):"))[1]);
    }

    // ---- peek /text, / and ?; peek of a row of find -t

    private void StormFile() =>
        File.WriteAllText(In("work", "s.md"), string.Concat(Enumerable.Range(1, 40).Select(i =>
            i is 10 or 25 ? "line " + i + " the Storm came\n" : i == 33 ? "line 33 storm at night\n" : "line " + i + "\n")));

    [Fact]
    public void PeekSearch_TheWindowAtTheFirstLineWithTheText_MarkedAndCounted()
    {
        StormFile();
        var (output, errors, _, session) = Run("peek s.md 1-5\npeek s.md /storm\n");
        Assert.Equal(string.Empty, errors);
        // Two lines above it, as large as the last window (5 lines), letter case alike
        Assert.Contains("   8  line 8\n   9  line 9\n> 10  line 10 the Storm came\n  11  line 11\n  12  line 12\n", output);
        Assert.Contains(Lang.F("(match {0} of {1}, lines {2}-{3} of {4})", 1, 3, 8, 12, 40), output);
        Assert.Equal((10, 1), session.LastList.Single().At);
    }

    [Fact]
    public void PeekSearch_SlashAndQuestionMark_GoOn_AndGoRound()
    {
        StormFile();
        var (output, errors, _, _) = Run("peek s.md /storm\n/\n/\n/\n?\n");
        Assert.Equal(string.Empty, errors);
        var marked = output.Split('\n').Where(line => line.StartsWith('>')).ToList();
        Assert.Equal(new[] { "> 10", "> 25", "> 33", "> 10", "> 33" }, marked.Select(line => line[..4]));
        Assert.Contains(Lang.T("(from the top again)"), output);
        Assert.Contains(Lang.T("(from the end again)"), output);
    }

    [Fact]
    public void PeekSearch_InTheFilePeekedLast_AWholeText_AndAPattern()
    {
        StormFile();
        var (output, errors, _, _) = Run("peek s.md\npeek /storm at night\npeek s.md /storm*night\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(2, output.Split('\n').Count(line => line.StartsWith("> 33")));
    }

    [Fact]
    public void PeekSearch_NothingFound_OrNothingSearched_IsTold()
    {
        StormFile();
        var (_, errors, _, _) = Run("/\npeek /storm\npeek s.md /nothing\n?\n/ x\npeek s.md /\n");
        Assert.Contains(Lang.T("Nothing searched yet: peek notes.md /storm"), errors);
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        Assert.Contains(Lang.F("No line with {0} in {1}", "nothing", "s.md"), errors);
        Assert.Contains(Lang.F("{0} takes nothing after it: peek notes.md /storm, then {0}", "/"), errors);
        Assert.Contains(Lang.T("Give a text: peek notes.md /storm"), errors);
    }

    [Fact]
    public void Peek_ARowOfFindByText_ShowsTheLineFound_Marked()
    {
        StormFile();
        var (output, errors, _, session) = Run("find -t night\npeek 1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("> 33  line 33 storm at night\n", output);
        Assert.Contains("  31  line 31\n", output);
        // The file at that line is the last list: open 1 opens it there; peek 1 again is the file as peeked
        Assert.Equal((33, 1), session.LastList.Single().At);
        Assert.Null(session.LastList.Single().Found);
    }

    [UnixFact]
    public void OnLinuxAndMacOS_PeekOfAFullPath_IsNotASearch()
    {
        StormFile();
        var (output, errors, _, _) = Run("peek " + In("work", "s.md") + " 1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  line 1\n", output);
    }

    // ---- Review of the heading search, undo and the peek search

    // A file of a size, of lines "line n"
    private void LinesFile(string name, long atLeast)
    {
        var text = new System.Text.StringBuilder();
        for (var i = 1; text.Length < atLeast; i++) text.Append("line ").Append(i).Append('\n');
        File.WriteAllText(In("work", name), text.ToString());
    }

    [Fact]
    public void Undo_ACommandWithAFileTooLarge_IsNoStep_AndEndsRedo()
    {
        LinesFile("big.md", ShellSession.UndoMaxSize + 1000);
        File.WriteAllText(In("work", "small.md"), "x\n");
        File.WriteAllText(In("work", "t.md"), "a\n");
        var (output, errors, _, _) = Run("insert t.md bottom b\nundo\nlines big.md 1 > small.md bottom cut\nundo\nredo\n");
        Assert.Contains(Lang.F("(this cannot be undone: {0} is larger than 2 MB)", "big.md"), output);
        // Nothing to undo: the lines stay in small.md (they are not in big.md any more)
        Assert.Contains(Lang.T("Nothing to undo: the changes made here cannot be undone"), output);
        Assert.Equal("x\nline 1\n", Text("small.md"));
        // The undone insert can no longer be done again
        Assert.Contains(Lang.T("Nothing to redo"), output);
        Assert.Equal("a\n", Text("t.md"));
        Assert.Equal(string.Empty, errors);
    }

    [Fact]
    public void Undo_AChangeThatChangesNothing_IsNoStep_AndTheLastUndoIsToldSo()
    {
        File.WriteAllText(In("work", "t.md"), "a\nb\n");
        var (output, _, _, _) = Run("change t.md +1 a\nchanges\ninsert t.md top z\nundo\nundo\n");
        Assert.Contains(Lang.T("No changes to undo: no file has been changed in this shell"), output);
        Assert.Contains(Lang.T("Nothing more to undo (redo does it again)"), output);
    }

    [Fact]
    public void Redo_AFileChangedSinceItWasUndone_IsNotWritten_AndAFileGoneIsTold()
    {
        File.WriteAllText(In("work", "t.md"), "a\n");
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("insert t.md bottom b");
        session.Execute("undo");
        File.WriteAllText(In("work", "t.md"), "edited\n");
        session.Execute("redo");
        Assert.Contains(Lang.F("{0} has changed since it was undone: {1} is not redone", In("work", "t.md"),
            "insert t.md bottom b"), errors.ToString());
        Assert.Equal("edited\n", Text("t.md"));
        session.Execute("insert t.md bottom c");
        File.Delete(In("work", "t.md"));
        session.Execute("undo");
        Assert.Contains(Lang.F("{0} is no longer there: {1} is not undone", In("work", "t.md"), "insert t.md bottom c"),
            errors.ToString());
        Assert.False(File.Exists(In("work", "t.md")));
    }

    [Fact]
    public void Undo_TheCommandsThatChangeText_AllOfThem()
    {
        // (each command changes the file: shift, lines start, lines replace, trim, change that deletes, format)
        File.WriteAllText(In("work", "t.md"), "a\n## Done\nb \nc\n");
        File.WriteAllText(In("work", "p.html"), "<div>\n<p>x</p>\n</div>\n");
        var (output, _, _, _) = Run("shift t.md 1 Done\nlines t.md 3 start -\nlines t.md 4 replace c C\ntrim t.md\n" +
                                    "change t.md 1-2\nformat p.html\nchanges\nundo 6\n", yes: true);
        Assert.Contains("  6  shift t.md 1 Done\n", output);
        Assert.Equal(6, output.Split('\n').Count(line => line.StartsWith(Lang.F("Undone: {0}", string.Empty))));
        Assert.Equal("a\n## Done\nb \nc\n", Text("t.md"));
        Assert.Equal("<div>\n<p>x</p>\n</div>\n", Text("p.html"));
    }

    [Fact]
    public void Undo_TheWrongWords_AreTold()
    {
        var (_, errors, _, _) = Run("redo x\nchanges x\nundo 0\n");
        Assert.Contains(Lang.T("Give how many: redo, redo 2"), errors);
        Assert.Contains(Lang.T("changes takes nothing after it"), errors);
        Assert.Contains(Lang.T("Give how many: undo, undo 3 (changes lists them)"), errors);
    }

    [Fact]
    public void Undo_TheSettingMadeSmaller_LetsTheOldestGoAtOnce()
    {
        File.WriteAllText(In("work", "t.md"), "a\n");
        var output = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, new StringWriter(), In("work"), false);
        try
        {
            session.Execute("insert t.md bottom b");
            session.Execute("insert t.md bottom c");
            session.Execute("insert t.md bottom d");
            Shell.UndoSteps = 1;
            session.Execute("changes");
            Assert.Contains("  1  insert t.md bottom d", output.ToString());
            Assert.DoesNotContain("insert t.md bottom c", output.ToString());
        }
        finally
        {
            Shell.UndoSteps = Shell.DefaultUndoSteps;
        }
    }

    [Fact]
    public void Undo_AllTheStepsKeepAtMost32MB()
    {
        LinesFile("big.md", ShellSession.UndoMaxSize - 200_000);
        var commands = string.Concat(Enumerable.Range(1, 10).Select(i => "insert big.md top x" + i + "\n"));
        var (output, _, _, _) = Run(commands + "changes\n");
        var rows = output.Split('\n').Count(line => line.Contains("  insert big.md top x"));
        // Each step keeps the file twice, about 3.6 MB: 8 of them stay under 32 MB, the newest first
        Assert.InRange(rows, 1, 9);
        Assert.Contains("insert big.md top x10", output.Split(Lang.T("Undone (redo does them again):"))[0]);
    }

    [WindowsFact]
    public void Undo_AFileThatCannotBeWritten_PutsBackTheOtherFile_AndTheStepStays_Windows()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\n");
        File.WriteAllText(In("work", "b.md"), "x\n");
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("lines a.md 1 > b.md bottom cut");
        File.SetAttributes(In("work", "a.md"), FileAttributes.ReadOnly);
        try
        {
            session.Execute("undo");
            Assert.NotEmpty(errors.ToString());
            // b.md was put back first, then put back as the step left it: both as after the command
            Assert.Equal("x\none\n", Text("b.md"));
            Assert.Equal("two\n", Text("a.md"));
        }
        finally
        {
            File.SetAttributes(In("work", "a.md"), FileAttributes.Normal);
        }

        session.Execute("undo");
        Assert.Equal("one\ntwo\n", Text("a.md"));
        Assert.Equal("x\n", Text("b.md"));
    }

    [Fact]
    public void AutoPeek_AfterUndo_ShowsTheWindowAgain()
    {
        try
        {
            Shell.AutoPeek = true;
            File.WriteAllText(In("work", "t.md"), "a\nb\n");
            var (output, _, _, _) = Run("peek t.md\ninsert t.md top z\nundo\n");
            Assert.Equal(2, output.Split('\n').Count(line => line == Lang.F("(peek again: {0})", "t.md")));
            // (the window of 3 lines at the end of the shorter file, where it was moved)
            Assert.EndsWith("  1  a\n  2  b\n" + Lang.F("(lines {0}-{1} of {2})", 1, 2, 2) + "\n", output);
        }
        finally
        {
            Shell.AutoPeek = false;
        }
    }

    [Fact]
    public void PeekSearch_AnotherFile_IsSearchedFromItsTop()
    {
        File.WriteAllText(In("work", "a.md"), string.Concat(Enumerable.Range(1, 30).Select(i => i == 25 ? "storm\n" : "x\n")));
        File.WriteAllText(In("work", "b.md"), "storm here\nx\nx\n");
        var (output, _, _, _) = Run("peek a.md /storm\npeek b.md\n/\n");
        Assert.Contains("> 1  storm here", output);
        Assert.DoesNotContain(Lang.T("(from the top again)"), output);
    }

    [Fact]
    public void PeekSearch_TheEdges_ASmallWindow_TheTop_ALargeWindow_AndTheWords()
    {
        StormFile();
        // A window of 1 line: the line found
        var (output, _, _, _) = Run("peek s.md +5 1\n/\npeek s.md /storm\n");
        Assert.Contains("> 10  line 10 the Storm came\n" + Lang.F("(match {0} of {1}, lines {2}-{3} of {4})", 1, 3, 10, 10, 40),
            output);
        // At the top: from line 1; a window larger than the file: the whole file
        File.WriteAllText(In("work", "top.md"), "storm\nx\n");
        (output, _, _, _) = Run("peek top.md /storm\n");
        Assert.Contains("> 1  storm\n  2  x\n" + Lang.F("(match {0} of {1}, lines {2}-{3} of {4})", 1, 1, 1, 2, 2), output);
        // A text that starts with "/", "?" as one character, an empty file, a directory
        File.WriteAllText(In("work", "slash.md"), "a/b\nstarm\n");
        File.WriteAllText(In("work", "empty.md"), string.Empty);
        Directory.CreateDirectory(In("work", "dir"));
        var errors = string.Empty;
        (output, errors, _, _) = Run("peek slash.md //b\npeek slash.md /st?rm\npeek empty.md /x\npeek dir /x\n");
        Assert.Contains("> 1  a/b", output);
        Assert.Contains("> 2  starm", output);
        Assert.Contains(Lang.F("No line with {0} in {1}", "x", "empty.md"), errors);
        Assert.Contains(Lang.F("Not a file: {0}", In("work", "dir")), errors);
    }

    [Fact]
    public void PeekSearch_SlashTextAlone_SearchesTheFilePeekedLast_AndIsLoggedAsSlash()
    {
        StormFile();
        var (output, errors, _, _) = Run("peek s.md\n/night\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("> 33  line 33 storm at night", output);
        Assert.Equal("/", EventLog.CommandName("/night"));
        Assert.Equal("/", EventLog.CommandName("/"));
        Assert.Equal("?", EventLog.CommandName("?"));
        Assert.Contains("/text", Run("help /\n").Output);
    }

    [Fact]
    public void Peek_AMistypedPathStartingWithSlash_IsNotFound_NotASearch()
    {
        StormFile();
        var (_, errors, _, _) = Run("peek s.md\npeek /nowhere/notes.md\n");
        Assert.Contains("Not found:", errors);
        Assert.DoesNotContain(Lang.F("No line with {0} in {1}", "nowhere/notes.md", "s.md"), errors);
    }

    [Fact]
    public void Peek_AFindRow_TheFileChangedOrGone_AndAfterACommandOnIt()
    {
        StormFile();
        var (_, _, _, session) = Run("find -t night\n");
        // The line moved down 2: the line with the text nearest to it is marked
        File.WriteAllText(In("work", "s.md"), "new\nnew\n" + File.ReadAllText(In("work", "s.md")));
        var output = new StringWriter();
        var errors = new StringWriter();
        var shell = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        shell.Execute("find -t night");
        File.WriteAllText(In("work", "s.md"), "new\n" + File.ReadAllText(In("work", "s.md")));
        shell.Execute("peek 1");
        Assert.Contains("> 36  line 33 storm at night", output.ToString());
        // A command on the row: a file again (peek shows its top)
        shell.Execute("find -t night");
        shell.Execute("trim 1");
        shell.Execute("peek 1");
        Assert.DoesNotContain("> 1 ", output.ToString().Split("trim")[^1]);
        // Gone
        shell.Execute("find -t night");
        File.Delete(In("work", "s.md"));
        shell.Execute("peek 1");
        Assert.NotEmpty(errors.ToString());
    }

    [Fact]
    public void Shift_AHeadingEndingInPlus_ATypstHeading_AndAStartWithAQuestionMark()
    {
        foreach (var (file, text, place, expected) in new[]
                 {
                     ("c.md", "item\n## C++\n## C\n", "C++", "## C++\nitem\n## C\n"),
                     ("w.md", "item\n## Why? Because\n", "\"Why?\"", "## Why? Because\nitem\n"),
                     ("t.typ", "item\n= Done herunder\n= Other\n", "Done", "= Done herunder\nitem\n= Other\n"),
                     ("u.typ", "item\n= Done herunder\nx\n= Other\n", "\"*herunder+\"", "= Done herunder\nx\nitem\n= Other\n"),
                     ("v.md", "item\n## Done herunder\nx\n## Other\n", "Done+", "## Done herunder\nx\nitem\n## Other\n"),
                 })
        {
            File.WriteAllText(In("work", file), text);
            var (_, errors, _, _) = Run("shift " + file + " 1 " + place + "\n");
            Assert.Equal(string.Empty, errors);
            Assert.Equal(expected, Text(file));
        }
    }

    [Fact]
    public void CompletionFile_ARegisterWithAPathWithBlanks_AndANumberThatIsNoRow()
    {
        Directory.CreateDirectory(In("work", "my notes"));
        File.WriteAllText(In("work", "my notes", "n.md"), "x\n");
        var session = Run("reg n = " + In("work", "my notes", "n.md") + "\n").Session;
        Assert.Equal(In("work", "my notes", "n.md"), session.CompletionFile("$n"));
        Assert.Equal(In("work", "7"), session.CompletionFile("7"));
    }

    // ---- echo

    [Fact]
    public void Echo_WritesTheText_WithTheNamesPutIn_AndChangesNothing()
    {
        File.WriteAllText(In("work", "a.md"), "one\ntwo\n");
        var (output, errors, _, session) = Run("peek a.md 1-2\nls\necho $file was peeked on $date\necho cost $$5\necho\n" +
                                               "echo $peek\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(In("work", "a.md") + " was peeked on " + IsoDateTime.Local(DateTime.UtcNow, "yyyy-MM-dd") + "\n", output);
        Assert.Contains("\ncost $5\n\n", output);
        // A value of several lines: all of them
        Assert.EndsWith("one\ntwo\n", output);
        // The last list is still that of ls, and $file the file peeked
        Assert.True(session.LastList.Count > 1);
        Assert.Equal(In("work", "a.md"), session.WorkedFile);
    }

    [Fact]
    public void Echo_ANameThatIsNoRegister_IsTold_AndNothingIsWritten()
    {
        var (output, errors, _, _) = Run("reg docs = " + In("work") + "\necho $docs and $nothing\necho $clip\n");
        Assert.Contains(Lang.F("No register named {0} (reg lists them)", "nothing"), errors);
        Assert.Contains(Lang.T("Nothing copied yet: clip copies a text for $clip"), errors);
        Assert.DoesNotContain(" and $nothing", output);
        // (by name only: never the text)
        Assert.Equal("echo ...", EventLog.CommandName("echo $docs secret text"));
    }

    [Fact]
    public void Echo_InARun_WritesItsLine()
    {
        File.WriteAllText(In("work", "t.txt"), "echo Tidying in $pwd\n");
        var (output, errors, _, _) = Run("run t.txt\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("Tidying in " + In("work") + "\n", output);
    }

    // ---- path and name (as :path and :name in the editor)

    [Fact]
    public void PathAndName_CopyThoseOfFile_OrOfAFileOrRowGiven()
    {
        File.WriteAllText(In("work", "a.md"), "x\n");
        var sent = Clipboard();
        var (output, errors, _, _) = Run("peek a.md\npath\nname\nfind Ending\npath 1\nname plot.md\n", clipboard: true);
        Assert.Equal(string.Empty, errors);
        // Row 1 of find: Ending.md
        Assert.Equal(new[] { In("work", "a.md"), "a.md", In("work", "Ending.md"), "plot.md" }, sent);
        Assert.Contains(DialogText.CopiedToClipboardAndMemory, output);
        Assert.Equal("plot.md", ShellRegisters.Clip);
        Assert.Equal("path", EventLog.CommandName("path"));
    }

    [Fact]
    public void PathAndName_BeforeAnyFile_AreTold_AndTabCompletesAFile()
    {
        var (_, errors, _, _) = Run("path\nname a.md b.md\n", clipboard: true);
        Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"), errors);
        Assert.Contains(Lang.F("Give one file or directory: {0} 3", "name"), errors);
        Assert.Equal("path plot.md", Tab(Completion(), "path plo"));
        Assert.Equal("name plot.md", Tab(Completion(), "name plo"));
        Assert.Contains("path [file|n], name [file|n]", Run("help name\n").Output);
    }

    // ---- file: a file of a list made $file

    [Fact]
    public void File_MakesARowOfTheList_File_AndTheListStays()
    {
        File.WriteAllText(In("work", "a.md"), "x\n");
        var (output, errors, _, session) = Run("file\nfind a.md\nfile 1\nfile\ninsert $file bottom y\n");
        Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"), errors);
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        Assert.Equal(2, output.Split('\n').Count(line => line == Lang.F("$file: {0}", In("work", "a.md"))));
        Assert.Equal("x\ny\n", Text("a.md"));
        // By its name; the list shown stays the last list
        (output, errors, _, session) = Run("ls\nfile plot.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "plot.md"), session.WorkedFile);
        Assert.True(session.LastList.Count > 1);
    }

    [Fact]
    public void File_ADirectory_TwoWords_AndAFileGone_AreTold()
    {
        Directory.CreateDirectory(In("work", "d"));
        var (_, errors, _, session) = Run("file d\nfile a b c\nfile gone.md\n");
        Assert.Contains(Lang.F("Not a file: {0} (cd goes to a directory)", In("work", "d")), errors);
        Assert.Contains(Lang.T("Give a file and any line: file 3, file plot.md 12"), errors);
        Assert.Contains(Lang.F("Not found: {0}", In("work", "gone.md")), errors);
        Assert.Null(session.WorkedFile);
        Assert.Equal("file plot.md", Tab(Completion(), "file plo"));
        Assert.Contains("file [file|n [line]]", Run("help file\n").Output);
    }

    // ---- Favorites added at the top

    [Fact]
    public void FavAdd_WithoutAPlace_GoesToTheTop_OutsideTheFolders()
    {
        FourFavorites();
        File.WriteAllText(In("work", "new.md"), "x\n");
        Run("fav folder F\nfav move 1 [F]\nfav add new.md\n");
        Assert.Equal("new.md|b.md|c.md|d.md|[F]|.a.md", FavoritesShape());
        // Row 1 in fav, to be moved where it belongs
        Assert.Contains(Lang.F("List: {0}", "main") + "\n  1  new.md\n", Run("fav\n").Output);
    }

    // ---- Review of 1.5.4

    [Fact]
    public void FavAdd_WithoutAPlace_ShowsTheListAgain_AsItsRowsHaveMoved()
    {
        FourFavorites();
        File.WriteAllText(In("work", "x.md"), "x\n");
        var (output, _, _, session) = Run("fav\nfav add x.md\nfav move 3 top\n");
        Assert.Contains(Lang.F("Added to the favorites: {0}", In("work", "x.md")) + "\n" + Lang.F("List: {0}", "main") +
                        "\n  1  x.md\n  2  a.md\n", output);
        // Row 3 is the one shown as row 3 after the add: b.md
        Assert.Equal("b.md|x.md|a.md|c.md|d.md", FavoritesShape());
    }

    [Fact]
    public void PathAndName_OfAFileGone_AreThePathAndNameKnown_AndADirectory()
    {
        File.WriteAllText(In("work", "a.md"), "x\n");
        Directory.CreateDirectory(In("work", "d"));
        var sent = Clipboard();
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false)
        {
            Clipboard = true
        };
        session.Execute("peek a.md");
        File.Delete(In("work", "a.md"));
        session.Execute("path");
        session.Execute("name");
        session.Execute("path d");
        Assert.Equal(string.Empty, errors.ToString());
        Assert.Equal(new[] { In("work", "a.md"), "a.md", In("work", "d") }, sent);
        // Without a terminal: kept for $clip
        var (text, _, _, _) = Run("peek plot.md\npath\n");
        Assert.Contains(Lang.T("Kept for $clip (the clipboard needs a terminal)"), text);
    }

    [Fact]
    public void File_OfAFindRow_AndOfAHistoryRowGone()
    {
        File.WriteAllText(In("work", "a.md"), "storm\n");
        var (output, errors, _, session) = Run("find -t storm\nfile 1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "a.md"), session.WorkedFile);
        Run("new gone\n");
        File.Delete(In("work", "gone.md"));
        (_, errors, _, _) = Run("hist\nfile 1\n");
        Assert.Contains(Lang.F("Not found: {0}", In("work", "gone.md")), errors);
    }

    [Fact]
    public void Echo_ANameInQuotes_TwoDollars_AnUnknownNameThroughAgain_AndHelp()
    {
        CommandHistory.Clear();
        try
        {
            var (output, errors, _, _) = Run("echo \"$pwd\" $$pwd\n", terminal: true);
            Assert.Contains("\"" + In("work") + "\" $pwd\n", output);
            CommandHistory.Add(CommandContext.Shell, "echo $nothing");
            (_, errors, _, _) = Run("!!\n");
            Assert.Contains(Lang.F("No register named {0} (reg lists them)", "nothing"), errors);
            Assert.Contains("echo text", Run("help echo\n").Output);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    // ---- recent and hist: -s, -f, -n

    // Three files opened: b.md (Drafts) first, then a.md (Notes), then c.md (Notes, gone since)
    private void ThreeOpened()
    {
        Directory.CreateDirectory(In("work", "Notes"));
        Directory.CreateDirectory(In("work", "Drafts"));
        File.WriteAllText(In("work", "Notes", "a.md"), new string('a', 2048));
        File.WriteAllText(In("work", "Drafts", "b.md"), "bb");
        FileHistory.Add(In("work", "Notes", "c.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Add(In("work", "Notes", "a.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Add(In("work", "Drafts", "b.md"), new DateTime(2026, 11, 13, 11, 0, 0));
        FileHistory.Save();
    }

    [Fact]
    public void Recent_Sizes_ShowTheSizeOfEachFile_AndADashForOneGone()
    {
        ThreeOpened();
        var (output, errors, _, _) = Run("recent -s\n");
        Assert.Equal(string.Empty, errors);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.StartsWith("  1  b.md", lines[0]);
        var width = new[] { FileSizeFormatter.FormatSize(2), FileSizeFormatter.FormatSize(2048), "-" }.Max(t => t.Length);
        Assert.EndsWith("2026-11-13 11:00  " + FileSizeFormatter.FormatSize(2).PadLeft(width), lines[0]);
        Assert.EndsWith("2026-11-13 10:00  " + FileSizeFormatter.FormatSize(2048).PadLeft(width), lines[1]);
        Assert.EndsWith("  " + "-".PadLeft(width), lines[2]);
    }

    [Fact]
    public void Recent_Folder_ShowsTheFolderAndTheName_AndFullPathsWin()
    {
        ThreeOpened();
        var (output, _, _, _) = Run("recent -f\n");
        Assert.Contains("  1  Drafts/b.md  ", output);
        Assert.Contains("  2  Notes/a.md   ", output);
        Assert.Contains("  3  Notes/c.md " + DialogText.FavoriteMissing, output);
        (output, _, _, _) = Run("recent -f -l\n");
        Assert.Contains("  1  " + In("work", "Drafts", "b.md") + "  ", output);
        // (not the folder and the name; on Linux and macOS the full path has "Drafts/b.md" at its end)
        Assert.DoesNotContain("  1  Drafts/b.md", output);
    }

    [Fact]
    public void Recent_ByName_SortsTheRows_AndOpenTakesTheRowAsShown()
    {
        ThreeOpened();
        var (output, errors, _, session) = Run("recent -n\n");
        Assert.Equal(string.Empty, errors);
        var lines = output.TrimEnd('\n').Split('\n');
        Assert.StartsWith("  1  a.md", lines[0]);
        Assert.StartsWith("  2  b.md", lines[1]);
        Assert.StartsWith("  3  c.md", lines[2]);
        Assert.Equal(In("work", "Notes", "a.md"), session.LastList[0].Path);
        // With the folder: by what is shown (Drafts/ before Notes/)
        (output, _, _, session) = Run("recent -f -n -s\n");
        lines = output.TrimEnd('\n').Split('\n');
        Assert.StartsWith("  1  Drafts/b.md", lines[0]);
        Assert.StartsWith("  2  Notes/a.md", lines[1]);
        Assert.Equal(In("work", "Drafts", "b.md"), session.LastList[0].Path);
    }

    [Fact]
    public void Hist_TakesTheOptions_AlsoBeforeFilter_AndTellsOneItDoesNotKnow()
    {
        ThreeOpened();
        var (output, errors, _, _) = Run("hist -n -f filter Notes\n");
        Assert.Equal(string.Empty, errors);
        // Numbered as in the whole sorted list: Drafts/b.md is row 1
        Assert.Contains("  2  Notes/a.md", output);
        Assert.Contains("  3  Notes/c.md", output);
        Assert.DoesNotContain("b.md", output);
        (_, errors, _, _) = Run("recent -s -x\nhist -q filter a\n");
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -s sizes, -f folder/name, -n by name)", "-x"), errors);
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -s sizes, -f folder/name, -n by name)", "-q"), errors);
    }

    [Fact]
    public void Tab_AfterRecentAndHist_OffersTheOptionsNotGivenYet()
    {
        Assert.Equal("recent -l", Tab(Completion(), "recent "));
        Assert.Equal("recent -s -l", Tab(Completion(), "recent -s "));
        Assert.Equal("recent -l -s -f -n", Tab(Completion(), "recent -l -s -f -"));
        Assert.Equal("hist -l -s -f -n filter", Tab(Completion(), "hist -l -s -f -n "));
        Assert.Equal("hist -n filter", Tab(Completion(), "hist -n f"));
        Assert.Equal("recent -f", Tab(Completion(), "recent -f"));
    }

    // ---- edit, lines sort/upper/lower, fav move into a folder, Tab after rename

    [Fact]
    public void Edit_IsOpen_ByAnotherName()
    {
        var (_, openErrors, _, _) = Run("open nothing.md\n");
        var (_, editErrors, _, _) = Run("edit nothing.md\n");
        Assert.NotEqual(string.Empty, editErrors);
        Assert.Equal(openErrors, editErrors);
        Assert.Contains("open file|n [+line] (edit)", Run("help edit\n").Output);
        Assert.Equal("edit plot.md", Tab(Completion(), "edit pl"));
    }

    [Fact]
    public void LinesSort_SortsAToZ_OrZToA_AsSortInTheEditor_AndUndoTakesItBack()
    {
        File.WriteAllText(In("work", "fruit.md"), "# List\ncherry\napple\nbanana\nEnd\n");
        var (output, errors, _, _) = Run("lines fruit.md 2-4 sort\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# List\napple\nbanana\ncherry\nEnd\n", File.ReadAllText(In("work", "fruit.md")));
        Assert.Contains("  2  apple\n  3  banana\n  4  cherry\n", output);
        Assert.Contains(Lang.F("{0}: {1} lines sorted", In("work", "fruit.md"), 3), output);
        Run("lines fruit.md 2-4 sort -d\n");
        Assert.Equal("# List\ncherry\nbanana\napple\nEnd\n", File.ReadAllText(In("work", "fruit.md")));
        Run("lines fruit.md 2-4 sort -a\n");
        Assert.Equal("# List\napple\nbanana\ncherry\nEnd\n", File.ReadAllText(In("work", "fruit.md")));
        // The order of :sort: by the value of the characters (upper case first)
        Assert.Equal(new[] { "Date", "apple" }, EditorEngine.SortedLines(new[] { "apple", "Date" }, false, false));
        (output, _, _, _) = Run("lines fruit.md 2-4 sort\n");
        Assert.Contains(Lang.T("Nothing changed: the lines are in that order already"), output);
        var (_, wrong, _, _) = Run("lines fruit.md 2-4 sort -x\n");
        Assert.Contains(Lang.T("After sort: -a (A to Z, the default) or -d (Z to A)"), wrong);
    }

    [Fact]
    public void LinesSort_OfLinesApart_PutsThemBackInTheirPlaces_AndUndoTakesItBack()
    {
        File.WriteAllText(In("work", "t.md"), "c\nx\nb\ny\na\n");
        var session = new ShellSession(new StringReader(string.Empty), new StringWriter(), new StringWriter(), In("work"), false);
        session.Execute("lines t.md 1,3,5 sort");
        Assert.Equal("a\nx\nb\ny\nc\n", File.ReadAllText(In("work", "t.md")));
        session.Execute("undo");
        Assert.Equal("c\nx\nb\ny\na\n", File.ReadAllText(In("work", "t.md")));
    }

    [Fact]
    public void LinesUpperAndLower_ChangeTheLetterCase_AndTakeNothingAfterThem()
    {
        File.WriteAllText(In("work", "t.md"), "Title one\nsecond Line\n\nÆble og Ørn\n");
        var (output, errors, _, _) = Run("lines t.md 1-2 upper\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("TITLE ONE\nSECOND LINE\n\nÆble og Ørn\n", File.ReadAllText(In("work", "t.md")));
        Assert.Contains(Lang.F("{0}: {1} lines changed", In("work", "t.md"), 2), output);
        Run("lines t.md 1-4 lower\n");
        Assert.Equal("title one\nsecond line\n\næble og ørn\n", File.ReadAllText(In("work", "t.md")));
        (output, _, _, _) = Run("lines t.md 1-4 lower\n");
        Assert.Contains(Lang.T("Nothing changed: no uppercase letters in the lines"), output);
        var (_, wrong, _, _) = Run("lines t.md 1 upper now\nlines t.md 1 sideways\n");
        Assert.Contains(Lang.F("Nothing goes after {0}: lines notes.md 3-20 {0}", "upper"), wrong);
        Assert.Contains(Lang.T("After the lines: > file place, > $name, start, end, unstart, unend, replace, remove, at, dup, sort, upper, lower or title"), wrong);
    }

    [Fact]
    public void Tab_AfterLines_OffersSortUpperLower_AndAfterSortTheOrder()
    {
        File.WriteAllText(In("work", "t.md"), "x\n");
        Assert.Equal("lines t.md 1-2 sort", Tab(Completion(), "lines t.md 1-2 so"));
        Assert.Equal("lines t.md 1-2 upper", Tab(Completion(), "lines t.md 1-2 up"));
        Assert.Equal("lines t.md 1-2 sort -a", Tab(Completion(), "lines t.md 1-2 sort "));
        Assert.Equal("lines t.md 1-2 sort -d", Tab(Completion(), "lines t.md 1-2 sort -", true, true));
    }

    [Fact]
    public void FavMove_IntoAFolder_PutsTheFavoritesAtTheTopOfIt()
    {
        FourFavorites();
        Run("fav folder F\nfav move 1 [F]\n");
        Assert.Equal("b.md|c.md|d.md|[F]|.a.md", FavoritesShape());
        // The next one goes above it, at the top of the folder
        Run("fav move c.md [F]\n");
        Assert.Equal("b.md|d.md|[F]|.c.md|.a.md", FavoritesShape());
        // fav add with a folder too
        File.WriteAllText(In("work", "e.md"), "x\n");
        Run("fav add e.md [F]\n");
        Assert.Equal("b.md|d.md|[F]|.e.md|.c.md|.a.md", FavoritesShape());
    }

    [Fact]
    public void Tab_AfterRenameAndAFile_OffersTheNameItHas()
    {
        File.WriteAllText(In("work", "chapter-1.md"), "x\n");
        File.WriteAllText(In("work", "my notes.md"), "x\n");
        Directory.CreateDirectory(In("work", "drafts"));
        Assert.Equal("rename chapter-1.md chapter-1.md", Tab(Completion(), "rename chapter-1.md "));
        Assert.Equal("rename chapter-1.md chapter-1.md", Tab(Completion(), "rename chapter-1.md ch"));
        Assert.Equal("rename \"my notes.md\" \"my notes.md\"", Tab(Completion(), "rename \"my notes.md\" "));
        Assert.Equal("rename drafts/ drafts", Tab(Completion(), "rename drafts/ "));
        // Nothing after the new name
        Assert.Equal("rename chapter-1.md x.md ", Tab(Completion(), "rename chapter-1.md x.md "));
    }

    // ---- calc and $calc

    [Fact]
    public void Calc_WritesTheResult_KeepsItInCalc_ForTheNextCommands()
    {
        File.WriteAllText(In("work", "budget.md"), "# Budget\n");
        var (output, errors, _, _) = Run("calc 1250 * 1.25 + 80\ninsert budget.md bottom Total: $calc\ncalc $calc / 2\n" +
                                         "echo $calc\ncalc\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("1642.5\n", output);
        Assert.Equal("# Budget\nTotal: 1642.5\n", File.ReadAllText(In("work", "budget.md")));
        Assert.Contains("821.25\n821.25\n", output);
        Assert.Contains(Lang.F("$calc: {0}", "821.25"), output);
        Assert.Equal("821.25", ShellRegisters.Calc);
    }

    [Fact]
    public void Calc_ThatFails_KeepsCalc_AndIsTold()
    {
        var (_, errors, _, _) = Run("calc 6 * 7\ncalc 1 / 0\ncalc 12x\n");
        Assert.Contains(Lang.T("Division by zero"), errors);
        Assert.Contains(Lang.F("Not understood: {0} (position {1})", "12x", 3), errors);
        Assert.Equal("42", ShellRegisters.Calc);
    }

    [Fact]
    public void Calc_BeforeAnyCalculation_IsTold_AndCannotBeSetOrTakenAway()
    {
        var (_, errors, _, _) = Run("calc\necho $calc\ninsert t.md bottom $calc\n");
        Assert.Contains(Lang.T("Give a calculation: calc 2 + 3 * 4"), errors);
        Assert.Contains(Lang.T("Nothing calculated yet: calc 2 + 3 gives $calc"), errors);
        Assert.False(File.Exists(In("work", "t.md")));
        (_, errors, _, _) = Run("reg calc 5\nreg -d calc\n");
        Assert.Contains(Lang.F("${0} is set by Blyant: it is not a register to set or take away", "calc"), errors);
        // Listed with the names set by Blyant
        var (output, _, _, _) = Run("calc 2 + 2\nreg\n");
        Assert.Contains("  $calc", output);
        Assert.Contains("calc expression", Run("help calc\n").Output);
    }

    // ---- resume

    private ShellSession Session(StringWriter errors) =>
        new(new StringReader(string.Empty), new StringWriter(), errors, In("work"), false);

    [Fact]
    public void Resume_OpensFileAtTheLineWorkedOnLast_OrAtTheLineGiven()
    {
        File.WriteAllText(In("work", "chapter-3.md"), "one\ntwo\nthree\n");
        var errors = new StringWriter();
        var session = Session(errors);
        session.Execute("insert chapter-3.md bottom four");
        var path = In("work", "chapter-3.md");
        // The line put in last
        Assert.Equal((path, (int?)4), session.ResumeTarget(string.Empty));
        Assert.Equal((path, (int?)2), session.ResumeTarget("+2"));
        // Another last list: where the cursor of the editor was
        session.Execute("ls");
        Assert.Equal((path, (int?)null), session.ResumeTarget(string.Empty));
        // The last list is another file at a line: not that line
        File.WriteAllText(In("work", "other.md"), "a\nb\n");
        session.Execute("peek other.md 2");
        session.Execute("file chapter-3.md");
        Assert.Equal((path, (int?)null), session.ResumeTarget(string.Empty));
        Assert.Equal(string.Empty, errors.ToString());
    }

    [Fact]
    public void Resume_WithoutFile_ForAFileGone_AndWithMoreThanALine_IsTold()
    {
        var errors = new StringWriter();
        var session = Session(errors);
        Assert.Null(session.ResumeTarget(string.Empty));
        Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"), errors.ToString());
        File.WriteAllText(In("work", "gone.md"), "x\n");
        session.Execute("peek gone.md");
        File.Delete(In("work", "gone.md"));
        Assert.Null(session.ResumeTarget(string.Empty));
        Assert.Contains(Lang.F("Not found: {0}", In("work", "gone.md")), errors.ToString());
        Assert.False(File.Exists(In("work", "gone.md")));
        File.WriteAllText(In("work", "here.md"), "x\n");
        session.Execute("peek here.md");
        Assert.Null(session.ResumeTarget("here.md"));
        Assert.Null(session.ResumeTarget("+2 +3"));
        Assert.Contains(Lang.T("Give only a line, or nothing: resume +120"), errors.ToString());
    }

    [Fact]
    public void Resume_WithoutATerminal_IsRefusedAsOpenIs_AndHelpTellsIt()
    {
        File.WriteAllText(In("work", "a.md"), "x\n");
        var (_, errors, _, _) = Run("peek a.md\nresume\n");
        Assert.Contains(Lang.F("{0} is not opened: the editor needs a terminal", In("work", "a.md")), errors);
        Assert.Contains("resume [+line]", Run("help resume\n").Output);
        Assert.Equal("resume", Tab(Completion(), "resu"));
    }

    // ---- Review of 1.5.5

    [Fact]
    public void Tab_AfterAFile_OfChangeDeleteAndTheOthers_OffersNoName_OnlyRenameDoes()
    {
        File.WriteAllText(In("work", "t.md"), "x\n");
        foreach (var command in new[] { "change", "delete", "info", "path", "name", "format", "trim", "trim!", "del" })
        {
            Assert.Equal(command + " t.md ", Tab(Completion(), command + " t.md "));
        }

        Assert.Equal("rename t.md t.md", Tab(Completion(), "rename t.md "));
    }

    [Fact]
    public void Tab_OffersTheOrderOfSort_OnlyAfterSortAsTheWordOfWhatIsDone()
    {
        File.WriteAllText(In("work", "t.md"), "x\n");
        Assert.Equal("lines t.md 1-2 sort -a", Tab(Completion(), "lines t.md 1-2 sort "));
        Assert.Equal("lines t.md 1-2 replace sort ", Tab(Completion(), "lines t.md 1-2 replace sort "));
        Assert.Equal("lines t.md 1-2 start sort ", Tab(Completion(), "lines t.md 1-2 start sort "));
    }

    [Fact]
    public void Calc_PutsANameInParentheses_SoANegativeValueOrAnExpressionIsOneNumber()
    {
        var (output, errors, _, _) = Run("calc 1 - 4\ncalc $calc ^ 2\nreg x 1 + 2\ncalc $x * 2\ncalc $$5 + 1\n");
        Assert.Contains("-3\n9\n", output);
        Assert.Contains("\n6\n", output);
        // ("$$" a "$", which is not a number: told, not put in)
        Assert.Contains(Lang.F("Not understood: {0} (position {1})", "$5", 1), errors);
        Assert.Equal("6", ShellRegisters.Calc);
        Assert.Contains("calc expression", Run("help $calc\n").Output);
    }

    [Fact]
    public void Reg_TellsARegisterNamedAsANameSetByBlyant()
    {
        File.WriteAllText(In("data", ShellRegisters.FileName), "calc\t12\nnotes\tx\n");
        ShellRegisters.Refresh();
        var (output, _, _, _) = Run("reg\n");
        Assert.Contains(Lang.F("(${0} in {1} is set by Blyant now: give that register another name)", "calc",
            ShellRegisters.FileName), output);
        Assert.Contains("$notes", output);
    }

    [Fact]
    public void Resume_AfterTheEditorWasClosed_OpensWhereTheCursorWas_NotAtAnOldLine()
    {
        File.WriteAllText(In("work", "notes.md"), "a\nb\n");
        var errors = new StringWriter();
        var session = Session(errors);
        session.Execute("insert notes.md bottom c");
        var path = In("work", "notes.md");
        Assert.Equal((path, (int?)3), session.ResumeTarget(string.Empty));
        // The editor was closed (open, resume): the line of the insert is from before it
        session.EditorClosed();
        Assert.Equal((path, (int?)null), session.ResumeTarget(string.Empty));
        // A change after it: its line again
        session.Execute("insert notes.md top z");
        Assert.Equal((path, (int?)1), session.ResumeTarget(string.Empty));
        Assert.Equal(string.Empty, errors.ToString());
    }

    [UnixFact]
    public void Resume_OfAFileWithAQuoteInItsName_GivesThePathAsItIs()
    {
        Directory.CreateDirectory(In("work", "q"));
        var path = In("work", "q", "say \"hi\".md");
        File.WriteAllText(path, "x\n");
        var errors = new StringWriter();
        var session = Session(errors);
        // (made $file from a row: the name is not typed)
        session.Execute("ls q");
        session.Execute("file 1");
        Assert.Equal(string.Empty, errors.ToString());
        Assert.Equal((path, (int?)null), session.ResumeTarget(string.Empty));
    }

    [Fact]
    public void LinesUpperAndLower_AreUndoneAndRedone()
    {
        File.WriteAllText(In("work", "t.md"), "Title\n");
        var session = Session(new StringWriter());
        session.Execute("lines t.md 1 upper");
        Assert.Equal("TITLE\n", File.ReadAllText(In("work", "t.md")));
        session.Execute("undo");
        Assert.Equal("Title\n", File.ReadAllText(In("work", "t.md")));
        session.Execute("redo");
        Assert.Equal("TITLE\n", File.ReadAllText(In("work", "t.md")));
        session.Execute("lines t.md 1 lower");
        session.Execute("undo");
        Assert.Equal("TITLE\n", File.ReadAllText(In("work", "t.md")));
        // A line without letters: nothing to change
        File.WriteAllText(In("work", "n.md"), "123\n");
        var (output, _, _, _) = Run("lines n.md 1 upper\n");
        Assert.Contains(Lang.T("Nothing changed: no lowercase letters in the lines"), output);
    }

    [Fact]
    public void Recent_SizesWithFullPaths_AndHistSizesWithAFilter()
    {
        ThreeOpened();
        var (output, errors, _, _) = Run("recent -s -l\nhist -s filter Drafts\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  " + In("work", "Drafts", "b.md") + "  2026-11-13 11:00  ", output);
        Assert.Contains(FileSizeFormatter.FormatSize(2048), output);
        // (hist filter: the row of b.md, numbered as in the whole history, with its size at the end)
        var row = output.Split('\n').Single(line => line.StartsWith("  1  b.md", StringComparison.Ordinal));
        Assert.EndsWith(FileSizeFormatter.FormatSize(2), row);
    }

    [Fact]
    public void Recent_Folder_TwoRowsAlike_GetTheFolderAboveThat()
    {
        Directory.CreateDirectory(In("work", "x", "Book"));
        Directory.CreateDirectory(In("work", "y", "Book"));
        File.WriteAllText(In("work", "x", "Book", "a.md"), "x\n");
        File.WriteAllText(In("work", "y", "Book", "a.md"), "y\n");
        FileHistory.Add(In("work", "x", "Book", "a.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Add(In("work", "y", "Book", "a.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Save();
        var (output, _, _, _) = Run("recent -f\n");
        Assert.Contains("  1  Book/a.md (y)", output);
        Assert.Contains("  2  Book/a.md (x)", output);
    }

    [Fact]
    public void Names_WithFolder_OfAFileAtTheRoot_IsItsPath()
    {
        // (the root of the drive the tests run on: "C:\" on Windows, "/" on Linux and macOS)
        var root = Path.GetPathRoot(In("work"))!;
        var path = Path.Combine(root, "notes-blyant-test.md");
        Assert.Equal(new[] { path + " " + DialogText.FavoriteMissing },
            ShellSession.Names(new[] { (path, false) }, false, false, withFolder: true));
        Assert.Equal(new[] { "work/" + "x.md " + DialogText.FavoriteMissing },
            ShellSession.Names(new[] { (In("work", "x.md"), false) }, false, false, withFolder: true));
    }

    [UnixFact]
    public void Recent_Sizes_OfALink_AreTheSizesOfTheFileItPointsTo()
    {
        File.WriteAllText(In("work", "target.md"), new string('t', 3000));
        File.CreateSymbolicLink(In("work", "link.md"), In("work", "target.md"));
        FileHistory.Add(In("work", "link.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Save();
        var (output, _, _, _) = Run("recent -s\n");
        Assert.Contains(FileSizeFormatter.FormatSize(3000), output);
    }

    // ---- file ... > $name

    [Fact]
    public void FileToARegister_KeepsTheFullPath_AndLeavesFileAndTheLastListAsTheyWere()
    {
        Directory.CreateDirectory(In("work", "book"));
        File.WriteAllText(In("work", "book", "chapter-3.md"), "x\n");
        File.WriteAllText(In("work", "notes.md"), "y\n");
        var errors = new StringWriter();
        var output = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("peek notes.md");
        // (a register of that name already: its value is not put in, the name is the target)
        session.Execute("reg other something");
        session.Execute("file book/chapter-3.md > $other");
        Assert.Equal(In("work", "book", "chapter-3.md"), ShellRegisters.Get("other"));
        Assert.Contains("  $other = " + In("work", "book", "chapter-3.md"), output.ToString());
        Assert.Equal(In("work", "notes.md"), session.WorkedFile);
        Assert.Equal(In("work", "notes.md"), session.LastList.Single().Path);
        // A row of the last list, and $file
        session.Execute("ls book");
        session.Execute("file 1 > $first");
        Assert.Equal(In("work", "book", "chapter-3.md"), ShellRegisters.Get("first"));
        session.Execute("file $file > $current");
        Assert.Equal(In("work", "notes.md"), ShellRegisters.Get("current"));
        // From another directory it is the same file
        session.Execute("cd book");
        session.Execute("peek $current");
        Assert.Equal(string.Empty, errors.ToString());
    }

    [Fact]
    public void FileToARegister_IsRefused_ForAFileNotThere_ADirectory_AndANameSetByBlyant()
    {
        Directory.CreateDirectory(In("work", "book"));
        File.WriteAllText(In("work", "a.md"), "x\n");
        var (_, errors, _, _) = Run("file nothing.md > $x\nfile book > $x\nfile a.md > $calc\nfile a.md > other\n" +
                                    "file a.md b.md > $x\nfile a.md > $1x\n");
        Assert.Contains(Lang.F("Not found: {0}", In("work", "nothing.md")), errors);
        Assert.Contains(Lang.F("Not a file: {0} (reg name . keeps the directory you are in)", In("work", "book")), errors);
        Assert.Contains(Lang.F("${0} is set by Blyant: it is not a register to set or take away", "calc"), errors);
        Assert.Equal(3, errors.Split(Lang.T("Give a file and a register: file plot.md > $other")).Length - 1);
        Assert.Null(ShellRegisters.Get("x"));
    }

    [Fact]
    public void Tab_AfterFileAndAFile_OffersTheArrow()
    {
        File.WriteAllText(In("work", "a.md"), "x\n");
        Assert.Equal("file a.md >", Tab(Completion(), "file a.md "));
        Assert.Equal("file a.md", Tab(Completion(), "file a."));
        Assert.Contains("file file|n > $name", Run("help file\n").Output);
    }

    // ---- find -f, -b

    // Two storm.md in folders named drafts, one notes.md
    private void StormTree()
    {
        Directory.CreateDirectory(In("work", "novel", "part-1", "drafts"));
        Directory.CreateDirectory(In("work", "novel", "part-2", "scenes", "drafts"));
        File.WriteAllText(In("work", "novel", "part-1", "drafts", "storm.md"), "A calm sea.\nstorm later\n");
        File.WriteAllText(In("work", "novel", "part-2", "scenes", "drafts", "storm.md"), "The storm broke.\n");
        File.WriteAllText(In("work", "novel", "notes.md"), "x\n");
    }

    [Fact]
    public void Find_FolderAndName_AndNamesOnly_ToldApart_AndTheRowsAreTheSame()
    {
        StormTree();
        var (output, errors, _, session) = Run("find -b storm\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  storm.md (part-1/drafts)\n  2  storm.md (scenes/drafts)\n", output);
        Assert.Equal(In("work", "novel", "part-1", "drafts", "storm.md"), session.LastList[0].Path);
        (output, _, _, _) = Run("find -f storm\n");
        Assert.Contains("  1  drafts/storm.md (part-1)\n  2  drafts/storm.md (scenes)\n", output);
        // The default: the path from here
        (output, _, _, _) = Run("find storm\n");
        Assert.Contains("  1  " + Path.Combine("novel", "part-1", "drafts", "storm.md") + "\n", output);
        // A name only one row has: no folder
        (output, _, _, _) = Run("find -b notes\n");
        Assert.EndsWith("  2  notes.md\n", output);
        // -l wins
        (output, _, _, _) = Run("find -b -l storm\n");
        Assert.Contains("  1  " + In("work", "novel", "part-1", "drafts", "storm.md") + "\n", output);
    }

    [Fact]
    public void Find_Options_WithText_InAnyOrder_AndOneNotKnown()
    {
        StormTree();
        var (output, errors, _, session) = Run("find -b -t storm\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  storm.md (part-1/drafts):2  storm later\n", output);
        Assert.Contains("  2  storm.md (scenes/drafts):1  The storm broke.\n", output);
        Assert.Equal((2, 1), session.LastList[0].At);
        (output, _, _, _) = Run("find -f -l -b *.md -t storm\n");
        Assert.Contains(In("work", "novel", "part-1", "drafts", "storm.md") + ":2", output);
        var (_, wrong, _, _) = Run("find -x storm\nfind -b -q\n");
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -f folder/name, -b names only, -d dates, -r again, -t text)", "-x"), wrong);
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -f folder/name, -b names only, -d dates, -r again, -t text)", "-q"), wrong);
        // A name that starts with "-", in quotes
        File.WriteAllText(In("work", "-draft.md"), "x\n");
        (output, errors, _, _) = Run("find \"-draft\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("-draft.md", output);
    }

    [Fact]
    public void Tab_AfterFind_OffersTheOptionsNotGivenYet()
    {
        Assert.Equal("find -l", Tab(Completion(), "find "));
        Assert.Equal("find -b", Tab(Completion(), "find -b"));
        Assert.Equal("find -f -l", Tab(Completion(), "find -f "));
        Assert.Equal("find storm ", Tab(Completion(), "find storm "));
        Assert.Equal("find -t ", Tab(Completion(), "find -t "));
    }

    [Fact]
    public void Recent_TwoNamesInFoldersAlike_GetAsManyFoldersAsTellThemApart()
    {
        StormTree();
        FileHistory.Add(In("work", "novel", "part-1", "drafts", "storm.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Add(In("work", "novel", "part-2", "scenes", "drafts", "storm.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Save();
        var (output, _, _, _) = Run("recent\n");
        Assert.Contains("  1  storm.md (scenes/drafts)", output);
        Assert.Contains("  2  storm.md (part-1/drafts)", output);
    }

    // ---- > $name after calc, again and the lists

    [Fact]
    public void Calc_ToARegister_KeepsTheResultThereAndInCalc_AndAddsWithTwoArrows()
    {
        var (output, errors, _, _) = Run("calc 22 * 3500 > $words\ncalc $words / 70 > $perday\ncalc 1 + 1 >> $words\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("77000\n2", ShellRegisters.Get("words"));
        Assert.Equal("1100", ShellRegisters.Get("perday"));
        Assert.Equal("2", ShellRegisters.Calc);
        Assert.Contains("  $words = 77000\n", output);
        // (>>: the result shown too, as the register shows only its first line)
        Assert.Contains("\n2\n", output);
        var (_, wrong, _, _) = Run("calc 1 > $file\ncalc > $x\ncalc 1 / 0 > $x\n");
        Assert.Contains(Lang.F("${0} is set by Blyant: it is not a register to set or take away", "file"), wrong);
        Assert.Contains(Lang.T("Give a calculation: calc 2 + 3 * 4"), wrong);
        Assert.Contains(Lang.T("Division by zero"), wrong);
        Assert.Null(ShellRegisters.Get("x"));
    }

    [Fact]
    public void Again_ToARegister_KeepsTheCommandsAsTyped_TheOldestFirst_ForRun()
    {
        CommandHistory.Clear();
        try
        {
            File.WriteAllText(In("work", "log.md"), "# Log\n");
            var errors = new StringWriter();
            var output = new StringWriter();
            var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
            // (as typed at the prompt: newest first in "again", 1 the last)
            foreach (var typed in new[] { "pwd", "insert log.md bottom First $date", "insert log.md bottom Second" })
            {
                CommandHistory.Add(CommandContext.Shell, typed);
            }

            session.Execute("again 1-2 > $tidy");
            Assert.Equal("insert log.md bottom First $date\ninsert log.md bottom Second", ShellRegisters.Get("tidy"));
            // The other way round is the same, and >> adds
            session.Execute("again 2-1 > $same");
            Assert.Equal(ShellRegisters.Get("tidy"), ShellRegisters.Get("same"));
            session.Execute("again 3 >> $tidy");
            Assert.EndsWith("\npwd", ShellRegisters.Get("tidy"));
            Assert.Equal(string.Empty, errors.ToString());
            // Run: the $date is put in when it runs
            ShellRegisters.Set("tidy", "insert log.md bottom First $date\ninsert log.md bottom Second");
            session.Execute("run $tidy");
            Assert.Equal("# Log\nFirst " + IsoDateTime.Local(DateTime.UtcNow, "yyyy-MM-dd") + "\nSecond\n", File.ReadAllText(In("work", "log.md")));
            session.Execute("again 9 > $x");
            session.Execute("again 2-x > $x");
            Assert.Contains(Lang.F("No command {0}: {1} are kept (again lists them)", 9, 3), errors.ToString());
            Assert.Contains(Lang.T("Give the numbers of the commands: again 3 > $tidy, again 3-5 > $tidy"), errors.ToString());
            Assert.Null(ShellRegisters.Get("x"));
            // Not run as "again 1"
            Assert.Null(ShellSession.AgainNumber("again 1 > $x"));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Lists_ToARegister_KeepTheFullPathsOfTheRowsShown()
    {
        StormTree();
        var (output, errors, _, _) = Run("find -b storm > $storms\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(In("work", "novel", "part-1", "drafts", "storm.md") + "\n" +
                     In("work", "novel", "part-2", "scenes", "drafts", "storm.md"), ShellRegisters.Get("storms"));
        // The list is shown too
        Assert.Contains("  1  storm.md (part-1/drafts)", output);
        // History: only the rows a filter shows, and >> adds
        FileHistory.Add(In("work", "novel", "notes.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Add(In("work", "novel", "part-1", "drafts", "storm.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Save();
        Run("hist filter notes > $h\nrecent >> $h\n");
        Assert.Equal(In("work", "novel", "notes.md") + "\n" + In("work", "novel", "part-1", "drafts", "storm.md") + "\n" +
                     In("work", "novel", "notes.md"), ShellRegisters.Get("h"));
        // Favorites, with folders
        Run("fav add novel/notes.md\nfav folder F\nfav move 1 [F]\nfav > $favs\n");
        Assert.Equal(In("work", "novel", "notes.md"), ShellRegisters.Get("favs"));
        // An empty list leaves the register
        (output, _, _, _) = Run("find nothing-at-all > $storms\n");
        Assert.Contains(Lang.F("${0} is not changed: the list is empty", "storms"), output);
        Assert.Contains("part-1", ShellRegisters.Get("storms"));
        var (_, wrong, _, _) = Run("fav add novel/notes.md > $y\nfind storm > $calc\nfile novel/notes.md >> $y\n");
        Assert.Contains(Lang.T("Only a list of favorites goes to a register: fav > $name, fav filter draft > $name"), wrong);
        Assert.Contains(Lang.F("${0} is set by Blyant: it is not a register to set or take away", "calc"), wrong);
        Assert.Contains(Lang.T("file keeps one path: file plot.md > $other (lines ... >> $name adds lines)"), wrong);
        Assert.Null(ShellRegisters.Get("y"));
    }

    [Fact]
    public void Target_IsOnlyARegisterAtTheEnd()
    {
        Assert.Equal(("2 * 3", "x", false), ShellSession.TargetAtEnd("2 * 3 > $x"));
        Assert.Equal(("3-5", "tidy", true), ShellSession.TargetAtEnd("3-5 >> $Tidy"));
        Assert.Null(ShellSession.TargetAtEnd("-t a > b"));
        Assert.Null(ShellSession.TargetAtEnd("-t > $x and more"));
        Assert.Equal("a >> $$x", ShellSession.ProtectTargetAtEnd("a >> $x"));
        Assert.Equal("a > $x b", ShellSession.ProtectTargetAtEnd("a > $x b"));
    }

    // ---- find alone, find -r

    [Fact]
    public void Find_Alone_ShowsTheLastSearchAgain_FromAnotherDirectory_AsItWasFound()
    {
        StormTree();
        var errors = new StringWriter();
        var output = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("find -t storm");
        session.Execute("cd novel/part-1");
        output.GetStringBuilder().Clear();
        session.Execute("find");
        var shown = output.ToString();
        Assert.StartsWith(Lang.F("Last search: {0} in {1} ({2})", "-t storm", In("work"), IsoDateTime.Local(DateTime.UtcNow, "HH:mm")),
            shown.Replace(IsoDateTime.Local(DateTime.UtcNow.AddMinutes(-1), "HH:mm"), IsoDateTime.Local(DateTime.UtcNow, "HH:mm")));
        // The rows as found: the path from the directory searched, and the line
        Assert.Contains("  1  " + Path.Combine("novel", "part-1", "drafts", "storm.md") + ":2  storm later", shown);
        Assert.Equal(In("work", "novel", "part-1", "drafts", "storm.md"), session.LastList[0].Path);
        Assert.Equal((2, 1), session.LastList[0].At);
        // Shown another way, without a new search
        output.GetStringBuilder().Clear();
        session.Execute("find -b");
        Assert.Contains("  1  storm.md (part-1/drafts):2  storm later", output.ToString());
        // A file gone since: marked
        File.Delete(In("work", "novel", "part-2", "scenes", "drafts", "storm.md"));
        output.GetStringBuilder().Clear();
        session.Execute("find");
        Assert.Contains("storm.md " + DialogText.FavoriteMissing + ":1  The storm broke.", output.ToString());
        Assert.Equal(string.Empty, errors.ToString());
    }

    [Fact]
    public void FindR_SearchesAgain_WhereTheLastSearchWasMade()
    {
        StormTree();
        var errors = new StringWriter();
        var output = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("find storm");
        session.Execute("cd novel/part-1");
        File.WriteAllText(In("work", "novel", "storm-notes.md"), "x\n");
        output.GetStringBuilder().Clear();
        session.Execute("find -r -b");
        var shown = output.ToString();
        Assert.StartsWith(Lang.F("Search again: {0} in {1}", "storm", In("work")), shown);
        Assert.Contains("storm-notes.md", shown);
        Assert.Equal(3, session.LastList.Count);
        // It is the last search now
        output.GetStringBuilder().Clear();
        session.Execute("find");
        Assert.Contains("storm-notes.md", output.ToString());
        Assert.Equal(string.Empty, errors.ToString());
    }

    [Fact]
    public void Find_WithoutASearchYet_AndFindRWithAName_AreTold()
    {
        var (_, errors, _, _) = Run("find\nfind -r\n");
        Assert.Contains(Lang.T("Write a name or a text to search for") + " (find plot, find *.md, find -t storm)", errors);
        Assert.Contains(Lang.T("No search yet: find plot, find -t storm"), errors);
        StormTree();
        (_, errors, _, _) = Run("find storm\nfind -r storm\n");
        Assert.Contains(Lang.T("find -r makes the last search again: give no name or text after it"), errors);
        Assert.Equal("find -r", Tab(Completion(), "find -r"));
        Assert.Contains("find -r", Run("help find\n").Output);
    }

    // ---- forget

    // Something kept while Blyant runs: commands typed, $clip, and (in the script) $peek, $calc, a search, an undo step
    private const string Memory = "peek t.md\ncalc 6 * 7\nfind t.md\ninsert t.md bottom two\nreg keep this\n";

    private void MemoryBefore()
    {
        CommandHistory.Clear();
        File.WriteAllText(In("work", "t.md"), "one\n");
        CommandHistory.Add(CommandContext.Shell, "pwd");
        CommandHistory.Add(CommandContext.Editor, "w");
        ShellRegisters.Clip = "copied";
    }

    [Fact]
    public void Forget_AfterYes_ForgetsWhatIsKeptWhileBlyantRuns_AndKeepsWhatIsSaved()
    {
        try
        {
            MemoryBefore();
            var (output, errors, _, session) = Run(Memory + "forget\nfind -r\nundo\n", yes: true);
            Assert.Contains(Lang.T("Forgotten: the commands typed, $clip, $peek, $calc, the last search and the undo steps"),
                output);
            Assert.Empty(CommandHistory.Of(CommandContext.Shell));
            Assert.Null(ShellRegisters.Clip);
            Assert.Null(ShellRegisters.Peek);
            Assert.Null(ShellRegisters.Calc);
            // The other places keep their commands; the registers, $file and the file stay
            Assert.Equal(new[] { "w" }, CommandHistory.Of(CommandContext.Editor));
            Assert.Equal("this", ShellRegisters.Get("keep"));
            Assert.Equal(In("work", "t.md"), session.WorkedFile);
            // No search to make again, no step to undo: the line put in stays
            Assert.Contains(Lang.T("No search yet: find plot, find -t storm"), errors);
            Assert.Equal("one\ntwo\n", File.ReadAllText(In("work", "t.md")));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Forget_IsAsked_AndWithoutYesNothingIsForgotten()
    {
        try
        {
            MemoryBefore();
            var (output, errors, _, _) = Run(Memory + "forget\nundo\nforget all\n");
            Assert.Contains(Lang.T("Nothing forgotten"), output);
            Assert.Equal("copied", ShellRegisters.Clip);
            Assert.Equal("42", ShellRegisters.Calc);
            Assert.NotEmpty(CommandHistory.Of(CommandContext.Shell));
            // The undo step was kept: undo took the line back
            Assert.Equal("one\n", File.ReadAllText(In("work", "t.md")));
            Assert.Contains(Lang.T("forget takes nothing after it"), errors);
            Assert.Contains("forget", Run("help forget\n").Output);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    // ---- -t text in recent, hist and fav

    // Three files opened, newest first: c.md (a heading, then storm), b.md (no storm), a.md (storm on line 1)
    private void ThreeWithText()
    {
        File.WriteAllText(In("work", "a.md"), "storm at sea\n");
        File.WriteAllText(In("work", "b.md"), "calm\n");
        File.WriteAllText(In("work", "c.md"), "# Chapter 3\n\nThe storm came.\n");
        FileHistory.Add(In("work", "a.md"), new DateTime(2026, 11, 13, 9, 0, 0));
        FileHistory.Add(In("work", "b.md"), new DateTime(2026, 11, 13, 10, 0, 0));
        FileHistory.Add(In("work", "c.md"), new DateTime(2026, 11, 13, 11, 0, 0));
        FileHistory.Save();
    }

    [Fact]
    public void RecentWithText_ShowsTheFilesWithIt_AndTheLine_NumberedAsTheWholeList()
    {
        ThreeWithText();
        var (output, errors, _, session) = Run("recent -t STORM\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  c.md:3  The storm came.\n", output);
        Assert.Contains("  3  a.md:1  storm at sea\n", output);
        Assert.DoesNotContain("b.md", output);
        // Row 1 opens at the line found
        Assert.Equal((3, 1), session.LastList[0].At);
        Assert.Equal(In("work", "b.md"), session.LastList[1].Path);
        // -t *: the first line of each; a pattern: the first heading
        (output, _, _, _) = Run("recent -t *\n");
        Assert.Contains("  2  b.md:1  calm\n", output);
        (output, _, _, _) = Run("recent -t \"# *\"\n");
        Assert.Contains("  1  c.md:1  # Chapter 3\n", output);
        Assert.DoesNotContain("a.md", output);
        // None
        (output, _, _, _) = Run("recent -t nothing-at-all\n");
        Assert.Contains(Lang.F("No file has {0} in a line", "nothing-at-all"), output);
    }

    [Fact]
    public void HistWithText_TakesTheOtherOptions_AFilter_AndARegister()
    {
        ThreeWithText();
        var (output, errors, _, _) = Run("hist -n -t storm\nhist filter c.md -t storm\nhist -t storm > $stormy\n");
        Assert.Equal(string.Empty, errors);
        // -n: sorted by name, a.md first
        Assert.Contains("  1  a.md:1  storm at sea\n  3  c.md:3  The storm came.\n", output);
        Assert.Equal(3, output.Split("c.md:3  The storm came.").Length - 1);
        Assert.Equal(In("work", "c.md") + "\n" + In("work", "a.md"), ShellRegisters.Get("stormy"));
        var (_, wrong, _, _) = Run("recent -t\n");
        Assert.Contains(Lang.T("Give a text after -t: recent -t storm, recent -t *"), wrong);
    }

    [Fact]
    public void FavWithText_LeavesOutDirectoriesAndFilesWithoutIt()
    {
        ThreeWithText();
        Directory.CreateDirectory(In("work", "dir"));
        var (output, errors, _, session) = Run("fav add a.md bottom\nfav add b.md bottom\nfav add dir bottom\n" +
                                                "fav -t *\n");
        Assert.Equal(string.Empty, errors);
        var shown = output[output.LastIndexOf(Lang.F("List: {0}", "main"), StringComparison.Ordinal)..];
        Assert.Contains("  1  a.md:1  storm at sea\n  2  b.md:1  calm\n", shown);
        Assert.DoesNotContain("dir", shown);
        Assert.Equal((1, 1), session.LastList[0].At);
        (output, _, _, _) = Run("fav -t storm\nfav filter b.md -t storm\n");
        Assert.Contains("  1  a.md:1  storm at sea\n", output);
        Assert.Contains(Lang.F("No favorite has {0} in a line", "storm"), output);
        var (_, wrong, _, _) = Run("fav -t\n");
        Assert.Contains(Lang.T("Give a text after -t: fav -t storm, fav -t *"), wrong);
    }

    [Fact]
    public void FindWithText_InQuotes_IsTheTextWithoutThem()
    {
        File.WriteAllText(In("work", "h.md"), "# Title\n");
        var (output, errors, _, _) = Run("find -t \"# T*\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("h.md:1  # Title", output);
    }

    [Fact]
    public void TextOption_IsTheLastPart_AfterMinusT()
    {
        Assert.Equal(("-n", "storm"), ShellSession.TextOption("-n -t storm"));
        Assert.Equal(("", "# *"), ShellSession.TextOption("-t \"# *\""));
        Assert.Equal(("filter draft", "a b"), ShellSession.TextOption("filter draft -t a b"));
        Assert.Equal(("-l", ""), ShellSession.TextOption("-l -t"));
        Assert.Equal(("-l filter x-t", (string?)null), ShellSession.TextOption("-l filter x-t"));
    }

    // ---- words, word, lines title

    private const string WordLine = "This is a line of text, that I want to change, with a few commands...";

    [Fact]
    public void Words_ShowsTheWordsOfALineNumbered_AndMakesTheFileTheFileWorkedOn()
    {
        File.WriteAllText(In("work", "n.md"), "# Notes\n" + WordLine + "\n");
        var (output, errors, _, session) = Run("words n.md 2\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  #1:1 This  #2:6 is  #3:9 a  #4:11 line  #5:16 of  #6:19 text  #7:25 that", output);
        Assert.Contains(Lang.F("{0}, line {1}: {2} words", In("work", "n.md"), 2, 15), output);
        Assert.Equal(In("work", "n.md"), session.WorkedFile);
        Assert.Equal((2, 1), session.LastList[0].At);
        (output, _, _, _) = Run("words n.md +1\n");
        Assert.Contains("  #1:3 Notes\n", output);
    }

    [Fact]
    public void Word_ChangesWordsByNumber_KeepsTheFile_AndUndoTakesItBack()
    {
        File.WriteAllBytes(In("work", "n.md"), System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes("# Notes\r\n" + WordLine + "\r\n")).ToArray());
        var output = new StringWriter();
        var errors = new StringWriter();
        var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
        session.Execute("word n.md 2 9 replace wish");
        session.Execute("word $file 2 14 before handful of");
        session.Execute("word $file +2 1-4 title");
        Assert.Equal(string.Empty, errors.ToString());
        var bytes = File.ReadAllBytes(In("work", "n.md"));
        // The byte order mark and the line endings kept
        Assert.True(bytes.Take(3).SequenceEqual(System.Text.Encoding.UTF8.GetPreamble()));
        Assert.Equal("# Notes\r\nThis Is A Line of text, that I wish to change, with a handful of few commands...\r\n",
            System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Contains(Lang.F("{0}: line {1} changed", In("work", "n.md"), 2), output.ToString());
        // The words numbered again after a change
        Assert.Contains("#14:55 handful  #15:63 of  #16:66 few", output.ToString());
        session.Execute("undo");
        session.Execute("undo");
        session.Execute("undo");
        Assert.Contains(WordLine, File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void Word_IsRefused_ForAWordNotThere_AnActionNotKnown_AndATextMissingOrTooMuch()
    {
        File.WriteAllText(In("work", "n.md"), "one two\n");
        var (_, errors, _, _) = Run("word n.md 1 3 delete\nword n.md 1 1 sideways\nword n.md 1 1 replace\n" +
                                    "word n.md 1 1 upper now\nword n.md 5 1 delete\nword n.md 1 x delete\nword n.md 1\n" +
                                    "words n.md\n");
        Assert.Contains(Lang.F("Line {0} has {1} words: {2} is not one", 1, 2, 3), errors);
        Assert.Contains(Lang.F("Not taken: {0} (replace, before, after, delete, move, upper, lower or title)", "sideways"), errors);
        Assert.Contains(Lang.F("Give the text: word notes.md 12 9 {0} wish", "replace"), errors);
        Assert.Contains(Lang.F("Nothing goes after {0}: word notes.md 12 9 {0}", "upper"), errors);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "n.md"), 1), errors);
        Assert.Contains(Lang.F("Not words: {0} (as 9, or 5-7)", "x"), errors);
        // (word with a file and a line only: words was meant)
        Assert.Contains(Lang.T("To see the words of a line: words notes.md 12 (word changes them)"), errors);
        Assert.Contains(Lang.T("Give a file and a line: words notes.md 12"), errors);
        Assert.Equal("one two\n", File.ReadAllText(In("work", "n.md")));
        var (output, _, _, _) = Run("word n.md 1 1 lower\n");
        Assert.Contains(Lang.T("Nothing changed: the words are so already"), output);
    }

    [Fact]
    public void LinesTitle_AndTab_AfterWordAndLines()
    {
        File.WriteAllText(In("work", "n.md"), "# my notes on the storm\nAlready Title\n");
        var (output, errors, _, _) = Run("lines n.md 1 title\nlines n.md 2 title\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# My Notes On The Storm\nAlready Title\n", File.ReadAllText(In("work", "n.md")));
        Assert.Contains(Lang.T("Nothing changed: the words start with capitals already"), output);
        Assert.Equal("word n.md 1 2 replace", Tab(Completion(), "word n.md 1 2 "));
        Assert.Equal("word n.md 1 2 title", Tab(Completion(), "word n.md 1 2 ti"));
        Assert.Equal("lines n.md 1 title", Tab(Completion(), "lines n.md 1 ti"));
        Assert.Contains("words [-p] file|n line", Run("help words\n").Output);
    }

    // ---- words -p, word -p

    [Fact]
    public void WordsAndWord_WithP_TakeAllBetweenBlanks()
    {
        File.WriteAllText(In("work", "n.md"), "# Notes\n" + WordLine + "\nKeep **bold** and a/b here.\n");
        var (output, errors, _, _) = Run("words -p n.md 2\nword -p n.md 2 6 replace note:\n" +
                                         "word -p n.md 2 15 replace commands.\nword -p n.md 3 2 title\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  #1:1 This  #2:6 is  #3:9 a  #4:11 line  #5:16 of  #6:19 text,  #7:25 that", output);
        Assert.Contains("#15:59 commands...", output);
        Assert.Equal("# Notes\nThis is a line of note: that I want to change, with a few commands.\n" +
                     "Keep **Bold** and a/b here.\n", File.ReadAllText(In("work", "n.md")));
        // Numbered again in the same way after the change
        Assert.Contains("  #1:1 Keep  #2:6 **Bold**  #3:15 and  #4:19 a/b  #5:23 here.", output);
        // Without -p, a/b is two words
        (output, _, _, _) = Run("words n.md 3\n");
        Assert.Contains("#4:19 a  #5:21 b  #6:23 here", output);
        var (_, wrong, _, _) = Run("words -x n.md 3\nword -q n.md 3 1 upper\n");
        Assert.Contains(Lang.F("Not taken: {0} (-p: words between blanks, with punctuation)", "-x"), wrong);
        Assert.Contains(Lang.F("Not taken: {0} (-p: words between blanks, with punctuation)", "-q"), wrong);
        Assert.Equal("word -p n.md 1 2 replace", Tab(Completion(), "word -p n.md 1 2 "));
        Assert.Equal("words -p", Tab(Completion(), "words -"));
    }

    // ---- run with words, a register as the start of a command

    [Fact]
    public void Run_WithWords_PutsThemInForDollarOneAndTwo_AndAddsThemToAScriptOfOneLine()
    {
        File.WriteAllText(In("work", "n.md"), "# Notes\nThe letter.\n");
        var (output, errors, _, _) = Run("peek n.md 1\nreg test words\nrun $test $file 2\n" +
                                         "reg check words $1 $2\nrun $check n.md +2\n" +
                                         "reg tidy = word $1 2 1 upper\nreg tidy += word $1 2 2 upper\nrun $tidy n.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("words " + In("work", "n.md") + " 2\n", output);
        Assert.Contains("words n.md +2\n", output);
        Assert.Equal("# Notes\nTHE LETTER.\n", File.ReadAllText(In("work", "n.md")));
        // $* all, a word with blanks as one; and the registers keep $1 as typed
        (output, _, _, _) = Run("reg every echo [$*]\nrun $every one \"two three\"\n");
        Assert.Contains("echo [one \"two three\"]\n", output);
        Assert.Equal("words $1 $2", ShellRegisters.Get("check"));
    }

    [Fact]
    public void Run_WithWordsThatDoNotFit_IsRefused_AndWithoutWordsAScriptIsAsBefore()
    {
        var (_, errors, _, _) = Run("reg check words $1 $2\nrun $check a.md\n");
        Assert.Contains(Lang.F("The script takes {0} words ($1 to ${0}): {1} given", 2, 1), errors);
        (_, errors, _, _) = Run("run $check a.md 2 3\n");
        Assert.Contains(Lang.F("The script takes {0} words ($1 to ${0}): {1} given", 2, 3), errors);
        // Without words: a script with $1 is refused ($* alone is nothing)
        (_, errors, _, _) = Run("reg some echo $1 $*\nrun $some\n");
        Assert.Contains(Lang.F("The script takes {0} words ($1 to ${0}): {1} given", 1, 0), errors);
        (_, errors, _, _) = Run("reg every echo [$*]\nrun $every\n");
        Assert.Equal(string.Empty, errors);
        (_, errors, _, _) = Run("reg two = pwd\nreg two += pwd\nrun $two x\n");
        Assert.Contains(Lang.T("The script has no $1: write $1, $2 ... where the words go (reg tidy = trim $1)"), errors);
        // A file whose name has a blank is still a file
        File.WriteAllText(In("work", "my script.txt"), "pwd\n");
        var (output, wrong, _, _) = Run("run my script.txt\n");
        Assert.Equal(string.Empty, wrong);
        Assert.Contains(In("work") + "\n", output);
        // A file with words
        File.WriteAllText(In("work", "w.txt"), "echo got $1\n");
        (output, _, _, _) = Run("run w.txt here\n");
        Assert.Contains("got here\n", output);
    }

    [Fact]
    public void ARegisterOfOneLine_IsTheStartOfACommand()
    {
        File.WriteAllText(In("work", "n.md"), "# Notes\nThe letter.\n");
        var (output, errors, _, _) = Run("reg w words\n$w n.md 2\nreg up word $$file 2 1 upper\npeek n.md\n$up\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  #1:1 The  #2:5 letter\n", output);
        Assert.Equal("# Notes\nTHE letter.\n", File.ReadAllText(In("work", "n.md")));
        var (_, wrong, _, _) = Run("reg two = pwd\nreg two += pwd\n$two\n$nothere x\n$file x\n");
        Assert.Contains(Lang.F("${0} has more than one line: run ${0} runs it", "two"), wrong);
        Assert.Contains(Lang.F("Unknown command: {0} (help lists the commands)", "$nothere"), wrong);
        Assert.Contains(Lang.F("Unknown command: {0} (help lists the commands)", "$file"), wrong);
    }

    // ---- reg -t

    [Fact]
    public void RegT_FindsRegistersByNameAndValue_AndShowsALaterLineFound()
    {
        var (output, errors, _, _) = Run("reg notes = First idea\nreg notes += A calm sea\nreg notes += The STORM came\n" +
                                         "reg chap3 C:\\Writing\\03-storm.md\nreg other plain\nreg -t storm\n");
        Assert.Equal(string.Empty, errors);
        // (the rows of reg -t after the lines reg wrote when the registers were set)
        var found = output[output.IndexOf("  $chap3 C:", output.IndexOf("$other = plain", StringComparison.Ordinal), StringComparison.Ordinal)..];
        Assert.Contains("$chap3", found);
        Assert.Contains("$notes", found);
        Assert.Contains(Lang.F("line {0}: {1}", 3, "The STORM came"), found);
        Assert.DoesNotContain("$other", found);
        // By name, with a pattern; the first line found: no line shown under it
        (output, _, _, _) = Run("reg -t chap*\nreg -t first\n");
        Assert.Contains("$chap3", output);
        Assert.DoesNotContain(Lang.F("line {0}: {1}", 1, "First idea"), output);
        Assert.DoesNotContain("$other", output);
    }

    [Fact]
    public void RegT_SearchesTheNamesSetByBlyantToo_AndTellsWhenNoneHasIt()
    {
        var (output, errors, _, _) = Run("calc 6 * 7\nreg -t 42\nreg -t nothing-here\n");
        Assert.Equal(string.Empty, errors);
        // ($pwd can have 42 in it too, as the temporary folder of a test machine: $calc is a row, not the first)
        Assert.Contains(Lang.T("Set by Blyant:") + "\n", output);
        Assert.Matches(@"\n  \$calc +42\n", output);
        Assert.Contains(Lang.F("No register with {0} in the name or the value", "nothing-here"), output);
        var (_, wrong, _, _) = Run("reg -t\n");
        Assert.Contains(Lang.T("Give a text: reg -t storm (or chap*)"), wrong);
        Assert.Equal("reg -t", Tab(Completion(), "reg -t"));
        Assert.Contains("reg -t text", Run("help reg\n").Output);
    }

    // ---- Review of 1.5.6

    [Fact]
    public void Run_AComment_WithDollarTwo_IsLeftAsItIs_AndAScriptThatSetsARegisterKeepsDollarOne()
    {
        File.WriteAllText(In("work", "tidy.txt"), "# $2 is the line\necho got $1\n");
        var (output, errors, _, _) = Run("run tidy.txt a.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("got a.md\n", output);
        // A reg line keeps $1 as text, without words
        File.WriteAllText(In("work", "make.txt"), "reg check words $1 $2\n");
        (_, errors, _, _) = Run("run make.txt\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("words $1 $2", ShellRegisters.Get("check"));
    }

    [Fact]
    public void Run_TheWords_AreNotPutInTwice_AndAWordWithBlanksAndQuotesIsRefused()
    {
        var (output, errors, _, _) = Run("reg s echo [$1]\nrun $s $$date\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("[$date]\n", output);
        File.WriteAllText(In("work", "notes$draft.md"), "x\n");
        (output, errors, _, _) = Run("reg t echo [$1]\nrun $t notes$draft.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("[notes$draft.md]\n", output);
        (_, errors, _, _) = Run("reg v a \"b\" c\nrun $t $v\n");
        Assert.Contains(Lang.F("A word with blanks and quotes cannot be given to a script: {0}", "a \"b\" c"), errors);
    }

    [Fact]
    public void ARegisterAsTheStartOfACommand_MustStartWithACommand_AndIsReadAgainFirst()
    {
        File.WriteAllText(In("work", "plot2.md"), "x\n");
        var (_, errors, _, _) = Run("file plot2.md > $r\n$r\nreg note Buy ink\n$note\n");
        Assert.Contains(Lang.F("${0} does not start with a command: put it in one (open ${0})", "r"), errors);
        Assert.Contains(Lang.F("${0} does not start with a command: put it in one (open ${0})", "note"), errors);
        // Edited by hand: the new value
        Run("reg w pwd\n");
        File.WriteAllText(In("data", ShellRegisters.FileName), "w\techo new\n");
        var (output, _, _, _) = Run("$w value\n");
        Assert.Contains("new value\n", output);
    }

    [Fact]
    public void ARegisterThatRunsAgain_IsAgain_AsTyped()
    {
        CommandHistory.Clear();
        try
        {
            var (output, errors) = (new StringWriter(), new StringWriter());
            var session = new ShellSession(new StringReader(string.Empty), output, errors, In("work"), false);
            CommandHistory.Add(CommandContext.Shell, "echo hello");
            session.Execute("reg a again 1");
            session.Execute("$a");
            Assert.Equal(string.Empty, errors.ToString());
            Assert.Contains("hello\n", output.ToString().Replace("\r\n", "\n"));
            // again with nothing typed: told so
            CommandHistory.Clear();
            session.Execute("again 1 > $x");
            Assert.Contains(Lang.T("No commands typed: from a file or a pipe they are not kept"), errors.ToString());
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void ATarget_IsOnlyOneTyped_NotOneInAValueOrWrittenWithTwoDollars()
    {
        File.WriteAllText(In("work", "a.md"), "a > $b\n");
        var (output, errors, _, _) = Run("reg q a > $$b\nfind -t $q\nfind -t a > $$y\n");
        Assert.Equal(string.Empty, errors);
        Assert.Null(ShellRegisters.Get("b"));
        Assert.Null(ShellRegisters.Get("y"));
        Assert.Contains("a.md:1", output);
    }

    [Fact]
    public void HistT_WithoutAText_NamesHist_AndFindL_HasNoMark()
    {
        var (_, errors, _, _) = Run("hist -t\n");
        Assert.Contains(Lang.T("Give a text after -t: hist -t storm, hist -t *"), errors);
        File.WriteAllText(In("work", "gone.md"), "x\n");
        var (output, _, _, session) = Run("fav add gone.md\nfind gone\n");
        File.Delete(In("work", "gone.md"));
        output = Run("find\nfind -l\n").Output;
        // The mark of a favorite before "(missing)", and none with -l
        Assert.Contains("gone.md * " + DialogText.FavoriteMissing, output);
        Assert.Contains("  1  " + In("work", "gone.md") + "\n", output);
    }

    [Fact]
    public void Names_TwoRowsAlikeUpToTheRoot_DoNotShowTheRootAsAFolder()
    {
        var root = Path.GetPathRoot(In("work"))!;
        var one = Path.Combine(root, "a", "x.md");
        var two = Path.Combine(root, "b", "a", "x.md");
        var names = ShellSession.Names(new[] { (one, false), (two, false) }, false, false);
        // ("a" and "b/a" tell them apart: the root is no folder name to show)
        Assert.Equal("x.md (a) " + DialogText.FavoriteMissing, names[0]);
        Assert.Equal("x.md (b/a) " + DialogText.FavoriteMissing, names[1]);
    }

    [Fact]
    public void Words_OneWord_AnEmptyLine_AndATextOfSeveralLines_AreToldPlainly()
    {
        File.WriteAllText(In("work", "w.md"), "Hello\n\n");
        var (output, errors, _, _) = Run("words w.md 1\nword w.md 2 1 upper\nreg two = a\nreg two += b\n" +
                                         "word w.md 1 1 replace $two\n");
        Assert.Contains(Lang.F("{0}, line {1}: 1 word", In("work", "w.md"), 1), output);
        Assert.Contains(Lang.F("Line {0} has no words", 2), errors);
        Assert.Contains(Lang.F("${0} has more than one line: only insert, change, reg, run and echo take it", "two"), errors);
        Assert.Equal("Hello\n\n", File.ReadAllText(In("work", "w.md")));
    }

    [UnixFact]
    public void Run_AFileWithQuotesAndBlanksInItsName_IsRefusedAsAWord_NotSplit()
    {
        File.WriteAllText(In("work", "my \"odd\" notes.md"), "x\n");
        var (_, errors, _, _) = Run("reg s echo [$1]\nfind odd\nfile 1\nrun $s $file\n");
        Assert.Contains(Lang.F("A word with blanks and quotes cannot be given to a script: {0}",
            In("work", "my \"odd\" notes.md")), errors);
    }

    [UnixFact]
    public void ARegisterHoldingAPath_IsNotTakenAsAPeekSearch_OnLinuxAndMacOs()
    {
        File.WriteAllText(In("work", "p.md"), "x\n");
        var (output, errors, _, _) = Run("file p.md > $r\n$r\n");
        Assert.Contains(Lang.F("${0} does not start with a command: put it in one (open ${0})", "r"), errors);
        Assert.DoesNotContain("match", output);
    }

    // ---- 1.5.7: shift up and down, sections, @ and $line, changed files in the history

    private const string Book = "# Book\n\n## A\n\naaa\n\n## B\n\nbbb\n\n## C\n\nccc\n";

    [Theory]
    [InlineData("up", true, 1)]
    [InlineData("Down", false, 1)]
    [InlineData("down 3", false, 3)]
    [InlineData("UP  2", true, 2)]
    [InlineData("up 99999999999", true, int.MaxValue)]
    public void Step_TakesUpAndDownWithACount(string place, bool up, int count)
    {
        Assert.Equal((up, count), ShellSession.Step(place));
    }

    [Theory]
    [InlineData("upper")]
    [InlineData("up 0")]
    [InlineData("up x")]
    [InlineData("down 2 3")]
    [InlineData("Done")]
    public void Step_IsNullForAnotherPlace(string place)
    {
        Assert.Null(ShellSession.Step(place));
    }

    [Fact]
    public void Shift_UpAndDown_MovesLinesByLines_AndStopsAtTheTopAndBottom()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\nthree\nfour\n");
        var (output, errors, _, _) = Run("shift l.md 3 up\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nthree\ntwo\nfour\n", File.ReadAllText(In("work", "l.md")));
        Assert.Contains(Lang.F("{0}: line moved to line {1}", In("work", "l.md"), 2), output);
        Run("shift l.md 1-2 down 9\n");
        Assert.Equal("two\nfour\none\nthree\n", File.ReadAllText(In("work", "l.md")));
        Run("shift l.md 4 up 2\n");
        Assert.Equal("two\nthree\nfour\none\n", File.ReadAllText(In("work", "l.md")));
        (output, _, _, _) = Run("shift l.md 1 up\n");
        Assert.Contains(Lang.T("Nothing moved: the lines are there already"), output);
        // ("Up" in quotes: a heading of that name)
        File.WriteAllText(In("work", "h.md"), "x\n## Up\ny\n");
        Run("shift h.md 3 \"Up\"\n");
        Assert.Equal("x\n## Up\ny\n", File.ReadAllText(In("work", "h.md")));
        Run("shift h.md 1 \"Up\"\n");
        Assert.Equal("## Up\nx\ny\n", File.ReadAllText(In("work", "h.md")));
    }

    [Fact]
    public void ShiftSection_UpAndDown_PassesTheSectionsOfItsLevel_AndKeepsTheBlankLines()
    {
        File.WriteAllText(In("work", "n.md"), Book);
        var (output, errors, _, _) = Run("shift -s n.md C up\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Book\n\n## A\n\naaa\n\n## C\n\nccc\n\n## B\n\nbbb\n", File.ReadAllText(In("work", "n.md")));
        Assert.Contains(Lang.F("{0}: section {1} ({2} lines) moved to line {3}", In("work", "n.md"), "C", 3, 7), output);
        Run("shift -s n.md A down 5\n");
        Assert.Equal("# Book\n\n## C\n\nccc\n\n## B\n\nbbb\n\n## A\n\naaa\n", File.ReadAllText(In("work", "n.md")));
        // The first of its level under # Book: nothing above it at its level
        (output, _, _, _) = Run("shift -s n.md C up\n");
        Assert.Contains(Lang.F("Nothing moved: no section above {0} at its level", "C"), output);
        (output, _, _, _) = Run("shift -s n.md A down\n");
        Assert.Contains(Lang.F("Nothing moved: no section below {0} at its level", "A"), output);
        Assert.Equal("# Book\n\n## C\n\nccc\n\n## B\n\nbbb\n\n## A\n\naaa\n", File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void ShiftSection_TakesItsSubsections_AFileWithoutBlankLines_AndAPlace()
    {
        File.WriteAllText(In("work", "s.md"), "## A\na\n### A1\na1\n## B\nb\n");
        Run("shift -s s.md A down\n");
        Assert.Equal("## B\nb\n## A\na\n### A1\na1\n", File.ReadAllText(In("work", "s.md")));
        // A subsection passes the subsections of its heading only
        var (output, _, _, _) = Run("shift -s s.md A1 up\n");
        Assert.Contains(Lang.F("Nothing moved: no section above {0} at its level", "A1"), output);
        // By the line of the heading ("+3", as @ gives it), and to a place
        Run("shift -s s.md +3 top\n");
        Assert.Equal("## A\na\n### A1\na1\n## B\nb\n", File.ReadAllText(In("work", "s.md")));
        Run("shift -s s.md B +1\n");
        Assert.Equal("## B\nb\n## A\na\n### A1\na1\n", File.ReadAllText(In("work", "s.md")));
    }

    [Fact]
    public void ShiftSection_TellsWhatItCannotDo_AndChangesNothing()
    {
        File.WriteAllText(In("work", "n.md"), Book);
        File.WriteAllText(In("work", "i.md"), "intro\n## A\na\n");
        var (_, errors, _, _) = Run("shift -s i.md 1 down\nshift -s n.md 99 up\nshift -s n.md Nothing up\n" +
                                    "shift -s n.md B\nshift -s n.md B bbb\nshift -s n.md B +9\n");
        Assert.Contains(Lang.F("Line {0} is above the first heading: give a heading (shift -s notes.md Ideas up)", 1),
            errors);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "n.md"), 13), errors);
        Assert.Contains(Lang.F("No heading {0} in {1}", "Nothing", "n.md"), errors);
        Assert.Contains(Lang.F("Give a file, a heading and the place: {0} -s notes.md Ideas up", "shift"), errors);
        Assert.Contains(Lang.F("No heading {0} in {1} (or +12, top, bottom)", "bbb", "n.md"), errors);
        // (into itself)
        Assert.Contains(Lang.F("Target {0} is among the lines moved", "+9"), errors);
        Assert.Equal(Book, File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void ShiftBang_WithASection_TicksItsBoxes()
    {
        File.WriteAllText(In("work", "t.md"), "## Now\n- [ ] a\n## Later\n- [ ] b\n");
        var (output, _, _, _) = Run("shift! -s t.md Later up\n");
        Assert.Equal("## Later\n- [x] b\n## Now\n- [ ] a\n", File.ReadAllText(In("work", "t.md")));
        Assert.Contains(Lang.F("Task boxes ticked or unticked: {0}", 1), output);
    }

    [Fact]
    public void At_IsTheFileAndTheLineWorkedOnLast_AndFollowsTheLine()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\nthree\n");
        var (output, errors, _, _) = Run("words l.md 2\nword @ 1 replace TWO\ninsert @ new line\nchange @ changed\n" +
                                         "echo $line\nshift @ up\nshift @ up\necho $line\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("changed\none\nTWO\nthree\n", File.ReadAllText(In("work", "l.md")));
        Assert.Contains("\n3\n", output);
        Assert.EndsWith("\n1\n", output);
        // A section by @: the heading moved, @ on it at its new place
        File.WriteAllText(In("work", "n.md"), Book);
        (_, errors, _, _) = Run("shift -s n.md C up\nshift -s @ up\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Book\n\n## C\n\nccc\n\n## A\n\naaa\n\n## B\n\nbbb\n", File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void At_InAText_AndInQuotes_IsAsItIs()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\n");
        File.WriteAllText(In("work", "@"), "at\n");
        var (output, errors, _, _) = Run("words l.md 1\ninsert @ Ask @anna\npeek \"@\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nAsk @anna\ntwo\n", File.ReadAllText(In("work", "l.md")));
        Assert.Contains("1  at", output);
    }

    [Fact]
    public void At_WithoutALine_IsRefused_AndTold()
    {
        File.WriteAllText(In("work", "l.md"), "one\n");
        var (_, errors, _, _) = Run("word @ 1 upper\n");
        Assert.Contains(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"), errors);
        (_, errors, _, _) = Run("file l.md\nwords @\nwords l.md 1\nfile l.md\nwords @\nfile plot.md\nwords @\n");
        // (file l.md: no line yet; words l.md 1, then file l.md again: the line kept; another file: none)
        Assert.Contains(Lang.F("@ has no line in {0} yet: peek it, or give the line (insert {0} +5 ...)", "l.md"), errors);
        Assert.Contains(Lang.F("@ has no line in {0} yet: peek it, or give the line (insert {0} +5 ...)", "plot.md"),
            errors);
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
    }

    [Fact]
    public void At_OfAFileWithoutALineWorkedOn_IsWhereTheCursorWasInTheEditor()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\nthree\n");
        FileHistory.Add(In("work", "l.md"), new DateTime(2026, 11, 18, 9, 26, 0));
        FileHistory.SetCaret(In("work", "l.md"), 2, 4);
        Assert.Equal(DialogText.Success, FileHistory.Save());
        var (output, errors, _, _) = Run("recent\nfile 1\necho $line\ninsert @ after two\necho $line\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\nafter two\nthree\n", File.ReadAllText(In("work", "l.md")));
        Assert.Contains("\n2\n", output);
        // (the line inserted: the line worked on now, not the cursor of the editor)
        Assert.EndsWith("\n3\n", output);
    }

    [Fact]
    public void LineCommand_SetsTheLineOfFile_ForAt()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\nthree\n");
        var (output, errors, _, _) = Run("file l.md\nline 2\necho $line\ninsert @ after two\nline\nline bottom\n" +
                                         "insert @ last\nline top\nline +2\necho $line\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\nafter two\nthree\nlast\n", File.ReadAllText(In("work", "l.md")));
        Assert.Contains(Lang.F("$line: {0} ({1})", 2, "l.md"), output);
        // (line alone: the line insert put in)
        Assert.Contains(Lang.F("$line: {0} ({1})", 3, "l.md"), output);
        Assert.Contains(Lang.F("$line: {0} ({1})", 4, "l.md"), output);
        Assert.Contains(Lang.F("$line: {0} ({1})", 1, "l.md"), output);
        Assert.EndsWith("\n2\n", output);
    }

    [Fact]
    public void LineCommand_TellsWhatItCannotDo_AndKeepsTheLine()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\n");
        var (_, errors, _, _) = Run("line 1\n");
        Assert.Contains(Lang.T("No file yet: open, peek, insert, new ... give $file"), errors);
        var (output, more, _, _) = Run("file l.md\nline\nline 2\nline 9\nline x\nline 0\nline 1 2\necho $line\n");
        Assert.Contains(Lang.F("{0} has no line yet: line 3 sets one", "l.md"), more);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "l.md"), 2), more);
        Assert.Contains(Lang.F("Not a line: {0} (line 3, line top, line bottom)", "x"), more);
        Assert.Contains(Lang.F("Not a line: {0} (line 3, line top, line bottom)", "0"), more);
        Assert.Contains(Lang.T("Give one line: line 3, line top, line bottom"), more);
        Assert.DoesNotContain(Lang.T("line only sets $line: to change lines, use lines (lines notes.md +4 replace old new)"), more);
        Assert.EndsWith("\n2\n", output);
        // An empty file: top and bottom are its top, for insert, as after new
        File.WriteAllText(In("work", "e.md"), string.Empty);
        (_, errors, _, _) = Run("file e.md bottom\ninsert @ first\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("first", File.ReadAllText(In("work", "e.md")).TrimEnd('\r', '\n'));
    }

    [Fact]
    public void FileWithALine_SetsBoth_AndALineNotThereChangesNothing()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\nthree\n");
        var (output, errors, _, _) = Run("ls\nfile l.md 3\nword @ 1 upper\nfile plot.md 9\necho $file $line\n");
        Assert.Contains(Lang.F("$line: {0}", 3), output);
        Assert.Equal("one\ntwo\nTHREE\n", File.ReadAllText(In("work", "l.md")));
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "plot.md"), 1), errors);
        Assert.Contains(In("work", "l.md") + " 3\n", output);
        Assert.Equal("file l.md top", Tab(Completion(), "file l.md t"));
        Assert.Equal("line bottom", Tab(Completion(), "line b"));
        Assert.Contains("line [n|top|bottom]", Run("help line\n").Output);
    }

    [Theory]
    [InlineData("word @ 2 re", "word @ 2 replace")]
    [InlineData("word @ 2-3 up", "word @ 2-3 upper")]
    [InlineData("word -p @ 2 de", "word -p @ 2 delete")]
    [InlineData("lines @ st", "lines @ start")]
    [InlineData("lines @ 3 so", "lines @ 3 sort")]
    [InlineData("lines @ sort -", "lines @ sort -a")]
    [InlineData("shift @ do", "shift @ down")]
    [InlineData("shift -s @ u", "shift -s @ up")]
    public void Tab_AtCountsAsTheFileAndTheLine(string typed, string completed)
    {
        Assert.Equal(completed, Tab(Completion(), typed));
    }

    [Theory]
    [InlineData("insert @ t")]
    [InlineData("peek @ t")]
    [InlineData("word @ 2 replace a")]
    public void Tab_AfterAt_OffersNothingWhereATextOrNothingGoes(string typed)
    {
        Assert.Equal(typed, Tab(Completion(), typed));
    }

    [Fact]
    public void Tab_TheFileAfterTheArrowOfLinesAt_AndTheWordsOfAFileWrittenOut_StayAsTheyWere()
    {
        Assert.Equal("lines @ > plot.md", Tab(Completion(), "lines @ > plo"));
        Assert.Equal("word plot.md 1 2 replace", Tab(Completion(), "word plot.md 1 2 rep"));
        Assert.Equal("insert plot.md top", Tab(Completion(), "insert plot.md t"));
    }

    [Fact]
    public void Line_WithoutALine_IsTold_NotWrittenAsItIs()
    {
        File.WriteAllText(In("work", "l.md"), "one\n");
        var (output, errors, _, _) = Run("echo $line\nfile l.md\necho [$line]\n");
        Assert.Contains(Lang.T("No line yet: words, peek, insert ... give $line"), errors);
        Assert.Equal(2, errors.TrimEnd('\n').Split('\n').Length);
        Assert.DoesNotContain("$line", output);
        // ($$line is the text)
        (output, _, _, _) = Run("echo $$line\n");
        Assert.Contains("$line", output);
    }

    [Fact]
    public void At_AfterNew_IsTheTopOfTheFile_ForInsert()
    {
        var (output, errors, _, _) = Run("peek plot.md\nnew fresh\necho $file $line\ninsert @ First line\n" +
                                         "insert @ Second line\nword @ 1 upper\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(In("work", "fresh.md") + " 0\n", output);
        // (an empty file gets the line endings of the system)
        Assert.Equal("First line\nSECOND line\n", File.ReadAllText(In("work", "fresh.md")).Replace("\r\n", "\n"));
        (_, errors, _, _) = Run("new other\nwords @\n");
        Assert.Contains(Lang.F("@ is at the top of {0}, before line 1: insert @ text puts a line there", "other.md"),
            errors);
    }

    [Fact]
    public void At_APeek_KeepsTheLine_ASearchSetsIt_AnotherFileStartsAtTheFirstLineShown()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\nstorm\nfour\n");
        var (output, errors, _, _) = Run("words l.md 2\npeek l.md\necho $line\npeek l.md /storm\necho $line\n" +
                                         "peek plot.md\necho $line\n");
        Assert.Equal(string.Empty, errors);
        var numbers = output.Split('\n').Where(line => line.Length == 1 && char.IsAsciiDigit(line[0])).ToList();
        Assert.Equal(new[] { "2", "3", "1" }, numbers);
    }

    [Fact]
    public void Line_IsSetByBlyant_WithFile()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\n");
        Assert.True(ShellRegisters.IsReserved("line"));
        var (output, _, _, _) = Run("words l.md 2\nreg\n");
        Assert.Matches(@"\n  \$line +2\n", output);
        (_, var errors, _, _) = Run("echo [$line]\n");
        Assert.Contains(Lang.T("No line yet: words, peek, insert ... give $line"), errors);
    }

    [Fact]
    public void ChangedFiles_AreTheNewestOfTheHistory_ALookIsNot()
    {
        File.WriteAllText(In("work", "l.md"), "one\ntwo\n");
        File.WriteAllText(In("work", "o.md"), "x\n");
        Run("insert l.md bottom three\n");
        FileHistory.Load();
        Assert.Equal(In("work", "l.md"), FileHistory.Items[0].Path);
        Run("peek plot.md\nwords Ending.md 1\n");
        FileHistory.Load();
        Assert.Equal(In("work", "l.md"), FileHistory.Items[0].Path);
        // Two files changed: $file (the other file) the newest
        Run("lines l.md 1 > o.md bottom cut\n");
        FileHistory.Load();
        Assert.Equal(In("work", "o.md"), FileHistory.Items[0].Path);
        Assert.Equal(In("work", "l.md"), FileHistory.Items[1].Path);
        // undo writes the files back: changed too
        var (_, errors, _, _) = Run("shift l.md 1 down\nword l.md 1 1 upper\nundo\n");
        Assert.Equal(string.Empty, errors);
        FileHistory.Load();
        Assert.Equal(In("work", "l.md"), FileHistory.Items[0].Path);
    }

    [Fact]
    public void Tab_ShiftTakesDashSUpDown_AndTheHeadingsOfASection()
    {
        File.WriteAllText(In("work", "n.md"), Book);
        Assert.Equal("shift -s", Tab(Completion(), "shift -"));
        Assert.Equal("shift n.md 2 up", Tab(Completion(), "shift n.md 2 u"));
        Assert.Equal("shift n.md 2 down", Tab(Completion(), "shift n.md 2 do"));
        Assert.Equal("shift -s n.md Book", Tab(Completion(), "shift -s n.md Bo"));
        Assert.Equal("shift -s n.md B down", Tab(Completion(), "shift -s n.md B do"));
        var session = Run("words n.md 7\n").Session;
        var completion = new ShellCompletion(() => In("work"), typed => Path.GetFullPath(typed, In("work")))
        {
            FileOf = session.CompletionFile
        };
        Assert.Equal("shift @ Book", Tab(completion, "shift @ Bo"));
        Assert.Contains("shift -s file|n heading place", Run("help shift\n").Output);
        Assert.Contains("word @ n action", Run("help word\n").Output);
    }

    // ---- Review of 1.5.7

    private const string Ten = "l1\nl2\nl3\nl4\nl5\nl6\nl7\nl8\nl9\nl10\n";

    [Fact]
    public void At_UndoAndRedo_PutTheLineBack()
    {
        File.WriteAllText(In("work", "f.md"), Ten);
        var (output, errors, _, _) = Run("words f.md 4\ninsert @ X\nundo\necho $line\nredo\necho $line\nundo\nchange @ Y\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("\n4\n", output);
        Assert.Contains("\n5\n", output);
        // (after the undo, @ is line 4 again: Y in place of l4, not of l5)
        Assert.Equal(Ten.Replace("l4\n", "Y\n"), File.ReadAllText(In("work", "f.md")));
    }

    [Fact]
    public void At_ALinePastTheEnd_IsRefused_NotTakenAsTheEnd()
    {
        File.WriteAllText(In("work", "s.md"), "a\nb\nc\n");
        // The cursor of the editor on line 9, and the file shorter since
        FileHistory.Add(In("work", "s.md"), new DateTime(2026, 11, 18, 9, 0, 0));
        FileHistory.SetCaret(In("work", "s.md"), 9, 1);
        Assert.Equal(DialogText.Success, FileHistory.Save());
        var (_, errors, _, _) = Run("file s.md\ninsert @ x\n");
        Assert.Contains(Lang.F("@ is line {0}, and {1} has {2} lines: line bottom sets the last", 9, "s.md", 3), errors);
        Assert.Equal("a\nb\nc\n", File.ReadAllText(In("work", "s.md")));
        // A peek past the end sets no line
        File.WriteAllText(In("work", "o.md"), "a\nb\n");
        (_, errors, _, _) = Run("peek o.md +50\ninsert @ x\n");
        Assert.Contains(Lang.F("@ has no line in {0} yet: peek it, or give the line (insert {0} +5 ...)", "o.md"), errors);
        Assert.Equal("a\nb\n", File.ReadAllText(In("work", "o.md")));
    }

    [Fact]
    public void At_AfterAnInsertOfSeveralLines_IsTheLastOfThem()
    {
        File.WriteAllText(In("work", "f.md"), "a\nb\n");
        var (_, errors, _, _) = Run("reg three 1\nreg three += 2\nreg three += 3\nwords f.md 1\ninsert @ $three\n" +
                                    "insert @ more\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("a\n1\n2\n3\nmore\nb\n", File.ReadAllText(In("work", "f.md")));
        // Lines copied to another file: @ is that file, at the last line put in
        File.WriteAllText(In("work", "p.md"), "# Plan\nend\n");
        (_, errors, _, _) = Run("lines f.md 2-4 > p.md +2\ninsert @ after\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Plan\n1\n2\n3\nafter\nend\n", File.ReadAllText(In("work", "p.md")));
    }

    [Fact]
    public void At_InAnEmptyFile_IsItsTop()
    {
        File.WriteAllText(In("work", "e.md"), string.Empty);
        var (_, errors, _, _) = Run("peek e.md\nword @ 1 upper\ninsert @ first\ninsert @ second\n");
        Assert.Contains(Lang.F("@ is at the top of {0}, before line 1: insert @ text puts a line there", "e.md"), errors);
        Assert.Single(errors.TrimEnd('\n').Split('\n'));
        Assert.Equal("first\nsecond", File.ReadAllText(In("work", "e.md")).Replace("\r\n", "\n").TrimEnd('\n'));
    }

    [Fact]
    public void At_OfAFileWithBlanksInTheName_IsOneWord()
    {
        File.WriteAllText(In("work", "my notes.md"), "one two\nthree four\n");
        var (_, errors, _, _) = Run("words \"my notes.md\" 2\nword @ 1 upper\nlines @ end !\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one two\nTHREE four !\n", File.ReadAllText(In("work", "my notes.md")));
    }

    [Fact]
    public void At_RunsInLinesPeekChangeAndWordP()
    {
        File.WriteAllText(In("work", "f.md"), "one\ntwo, three\nfour\n");
        File.WriteAllText(In("work", "o.md"), string.Empty);
        var (output, errors, _, _) = Run("words f.md 2\nword -p @ 1 replace 2:\nlines @ start - \npeek @ 1\n" +
                                         "change @ new two\nlines @ > o.md bottom\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nnew two\nfour\n", File.ReadAllText(In("work", "f.md")));
        Assert.Contains("2  - 2: three", output);
        Assert.Equal("new two\n", File.ReadAllText(In("work", "o.md")).Replace("\r\n", "\n"));
    }

    [Fact]
    public void At_FromARegisterAndInAScript_IsTheLineAtTheTime()
    {
        File.WriteAllText(In("work", "f.md"), "a\nb\nc\n");
        File.WriteAllText(In("work", "fix.txt"), "word @ 1 upper\nshift @ up\n");
        var (_, errors, _, _) = Run("reg up word @ 1 upper\nwords f.md 2\n$up\nwords f.md 3\nrun fix.txt\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("a\nC\nB\n", File.ReadAllText(In("work", "f.md")));
    }

    [Fact]
    public void At_AfterARename_HasNoLine_UntilACommandWorksOnOne()
    {
        File.WriteAllText(In("work", "f.md"), "a\nb\n");
        var (_, errors, _, _) = Run("words f.md 2\nrename f.md g.md\nword @ 1 upper\n");
        Assert.Contains(Lang.F("@ has no line in {0} yet: peek it, or give the line (insert {0} +5 ...)", "g.md"), errors);
    }

    [Fact]
    public void LineCommand_OnAFileThatCannotBeRead_IsTold()
    {
        File.WriteAllBytes(In("work", "b.md"), new byte[] { 0x61, 0, 0x62, 0, 0, 0x0a });
        var (_, errors, _, _) = Run("file b.md 1\n");
        Assert.Contains(Lang.F("{0} cannot be read: {1}", In("work", "b.md"), string.Empty).TrimEnd(), errors);
    }

    [UnixFact]
    public void At_APathWithAQuote_IsPutInWithTheQuoteDoubled_OnLinuxAndMacOs()
    {
        // (a name with a quote, as Linux and macOS allow: found by a search, so it is not typed; put in as
        // "say ""hi"".md", which Words takes back as it is)
        File.WriteAllText(In("work", "say \"hi\".md"), "a\n");
        var (_, errors, _, _) = Run("find say\nfile 1 1\nword @ 1 upper\nreg q $file\nlines $q 1 end x\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("A x\n", File.ReadAllText(In("work", "say \"hi\".md")));
    }

    [Fact]
    public void ShiftSection_ToTheEndOfASection_HasOneBlankLine_NotTwo()
    {
        File.WriteAllText(In("work", "n.md"), Book);
        Run("shift -s n.md A B+\n");
        Assert.Equal("# Book\n\n## B\n\nbbb\n\n## A\n\naaa\n\n## C\n\nccc\n", File.ReadAllText(In("work", "n.md")));
        // The last section, which has no blank line after it, to the end of the first section
        File.WriteAllText(In("work", "n.md"), Book);
        Run("shift -s n.md C A+\n");
        Assert.Equal("# Book\n\n## A\n\naaa\n\n## C\n\nccc\n\n## B\n\nbbb\n", File.ReadAllText(In("work", "n.md")));
        // Before a line: +7 is ## B
        File.WriteAllText(In("work", "n.md"), Book);
        Run("shift -s n.md C +7\n");
        Assert.Equal("# Book\n\n## A\n\naaa\n\n## C\n\nccc\n\n## B\n\nbbb\n", File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void ShiftSection_PutWhereItIs_IsNoMove_AlsoWhereTheSpacingDiffers()
    {
        const string text = "## A\na\n## B\nb\n\n## C\nc\n";
        File.WriteAllText(In("work", "f.md"), text);
        var (output, _, _, _) = Run("shift -s f.md B A+\n");
        Assert.Contains(Lang.T("Nothing moved: the lines are there already"), output);
        Assert.Equal(text, File.ReadAllText(In("work", "f.md")));
    }

    [Fact]
    public void Shift_AFileWithoutALineBreakAtTheEnd_KeepsItsLines()
    {
        File.WriteAllText(In("work", "f.md"), "# A\na\n\n# B\nb");
        Run("shift -s f.md B up\n");
        Assert.Equal("# B\nb\n\n# A\na", File.ReadAllText(In("work", "f.md")));
        // (a blank line moved last is kept: the file gets a line break at the end, not a line less)
        File.WriteAllText(In("work", "g.md"), "x\n\ny");
        Run("shift g.md 3 top\n");
        Assert.Equal("y\nx\n\n", File.ReadAllText(In("work", "g.md")));
        Assert.Equal(3, ShellSession.LinesOfText(File.ReadAllText(In("work", "g.md"))).Count);
    }

    [Fact]
    public void ShiftSection_AFileEndingWithBlankLines_KeepsThem()
    {
        File.WriteAllText(In("work", "f.md"), "# A\na\n\n# B\nb\n\n\n");
        Run("shift -s f.md B up\n");
        Assert.Equal("# B\nb\n\n# A\na\n\n\n", File.ReadAllText(In("work", "f.md")));
        Run("shift -s f.md A up\n");
        Assert.Equal("# A\na\n\n# B\nb\n\n\n", File.ReadAllText(In("work", "f.md")));
    }

    [Fact]
    public void ShiftSection_KeepsTheLineEndingsAndTheByteOrderMark()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        File.WriteAllBytes(In("work", "w.md"), bom.Concat(System.Text.Encoding.UTF8.GetBytes("# A\r\na\r\n\r\n# B\r\nb\r\n")).ToArray());
        var (_, errors, _, _) = Run("shift -s w.md B up\nshift w.md 2 down\n");
        Assert.Equal(string.Empty, errors);
        var bytes = File.ReadAllBytes(In("work", "w.md"));
        Assert.Equal(bom, bytes.Take(3).ToArray());
        Assert.Equal("# B\r\n\r\nb\r\n# A\r\na\r\n", System.Text.Encoding.UTF8.GetString(bytes.Skip(3).ToArray()));
    }

    [Fact]
    public void ShiftSection_TypstHeadings_ACodeBlock_AndAnEmptySection()
    {
        File.WriteAllText(In("work", "t.typ"), "= A\na\n== A1\nx\n= B\nb\n");
        Run("shift -s t.typ B up\n");
        Assert.Equal("= B\nb\n= A\na\n== A1\nx\n", File.ReadAllText(In("work", "t.typ")));
        // A "#" line in a block of code does not end the section
        File.WriteAllText(In("work", "c.md"), "## A\n```\n## not\n```\n## B\nb\n");
        Run("shift -s c.md A down\n");
        Assert.Equal("## B\nb\n## A\n```\n## not\n```\n", File.ReadAllText(In("work", "c.md")));
        File.WriteAllText(In("work", "e.md"), "## A\n## B\nb\n");
        Run("shift -s e.md A down\n");
        Assert.Equal("## B\nb\n## A\n", File.ReadAllText(In("work", "e.md")));
    }

    [Fact]
    public void ShiftSection_ByALineInIt_AHugeCount_AndAStartThatFitsTwo()
    {
        File.WriteAllText(In("work", "n.md"), Book);
        var (_, errors, _, _) = Run("shift -s n.md 9 up\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Book\n\n## B\n\nbbb\n\n## A\n\naaa\n\n## C\n\nccc\n", File.ReadAllText(In("work", "n.md")));
        // The section of the line @ is on
        File.WriteAllText(In("work", "n.md"), Book);
        (_, errors, _, _) = Run("words n.md 13\nshift -s @ up\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Book\n\n## A\n\naaa\n\n## C\n\nccc\n\n## B\n\nbbb\n", File.ReadAllText(In("work", "n.md")));
        File.WriteAllText(In("work", "n.md"), Book);
        Run("shift -s n.md A down 99999999999\n");
        Assert.Equal("# Book\n\n## B\n\nbbb\n\n## C\n\nccc\n\n## A\n\naaa\n", File.ReadAllText(In("work", "n.md")));
        File.WriteAllText(In("work", "h.md"), "## Ch 1\n## Ch 2\n");
        (_, errors, _, _) = Run("shift -s h.md Ch up\nshift -s h.md +0 up\n");
        Assert.Contains(Lang.F("{0} headings fit {1}: give more of the name, or the line (+12)", 2, "Ch"), errors);
        Assert.Contains(Lang.F("Line {0} is above the first heading: give a heading (shift -s notes.md Ideas up)", 0),
            errors);
    }

    [Fact]
    public void Shift_UpWithLinesApart_PutsThemTogether_AndShiftBangTicks()
    {
        File.WriteAllText(In("work", "f.md"), "a\nb\nc\nd\n");
        Run("shift f.md 2,4 up\n");
        Assert.Equal("b\nd\na\nc\n", File.ReadAllText(In("work", "f.md")));
        File.WriteAllText(In("work", "t.md"), "- [ ] x\n- [ ] y\n");
        Run("shift! t.md 2 up\n");
        Assert.Equal("- [x] y\n- [ ] x\n", File.ReadAllText(In("work", "t.md")));
    }

    [Fact]
    public void ChangedFiles_OfARun_AndOfARedo_AreInTheHistory_ARefusedCommandAddsNone()
    {
        File.WriteAllText(In("work", "a.md"), "a\n");
        File.WriteAllText(In("work", "b.md"), "b\n");
        File.WriteAllText(In("work", "two.txt"), "insert a.md bottom x\ninsert b.md bottom y\n");
        Run("run two.txt\n");
        FileHistory.Load();
        Assert.Equal(In("work", "b.md"), FileHistory.Items[0].Path);
        Assert.Equal(In("work", "a.md"), FileHistory.Items[1].Path);
        Run("insert a.md bottom z\nundo\nfile b.md\nredo\n");
        FileHistory.Load();
        Assert.Equal(In("work", "a.md"), FileHistory.Items[0].Path);
        var count = FileHistory.Items.Count;
        // (refused: the line is not there)
        Run("change b.md +9 nothing\n");
        FileHistory.Load();
        Assert.Equal(count, FileHistory.Items.Count);
        Assert.Equal(In("work", "a.md"), FileHistory.Items[0].Path);
    }

    [Fact]
    public void Tab_TheOptionsAreTakenAsTyped()
    {
        File.WriteAllText(In("work", "n.md"), Book);
        // ("-S" is no option: the command takes it as the file, and so does Tab)
        Assert.Equal("shift -S n.md bottom", Tab(Completion(), "shift -S n.md bo"));
        Assert.Equal("word -P", Tab(Completion(), "word -P"));
    }

    // ---- words -p and word: counted the same way

    [Theory]
    [InlineData("Well - she said: try a/b first.", 1, 1, true)]
    [InlineData("Well - she said: try a/b first.", 2, 2, false)]
    [InlineData("Well - she said: try a/b first.", 6, 6, false)]
    [InlineData("text, more text.", 1, 3, true)]
    [InlineData("**bold** and (this)", 1, 3, true)]
    [InlineData("one two", 3, 3, false)]
    public void SameWords_TellsWhetherANumberIsTheSameWordCountedBothWays(string line, int first, int last, bool same)
    {
        Assert.Equal(same, LineWords.SameWords(line, first, last));
    }

    [Fact]
    public void Word_CountedTheOtherWayThanWords_IsRefused_WhenTheNumbersDiffer()
    {
        File.WriteAllText(In("work", "n.md"), "Well - she said: try a/b first.\n");
        var (_, errors, _, _) = Run("words -p n.md 1\nword @ 6 upper\nwords n.md 1\nword -p @ 6 upper\n");
        Assert.Contains(Lang.F("words -p numbered line {0}, and without -p the numbers differ: give -p to word too", 1),
            errors);
        Assert.Contains(Lang.F("words numbered line {0} without -p, and with -p the numbers differ: leave out -p", 1),
            errors);
        Assert.Equal("Well - she said: try a/b first.\n", File.ReadAllText(In("work", "n.md")));
        // The same way: as asked
        (_, errors, _, _) = Run("words -p n.md 1\nword -p @ 6 upper\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Well - she said: try A/B first.\n", File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void Word_CountedTheOtherWay_IsTaken_WhereTheNumbersAreTheSameWords()
    {
        File.WriteAllText(In("work", "n.md"), "Well - she said: try a/b first.\nanother line\n");
        // (word 1 is "Well" both ways; another line has nothing shown to go by; words again counts it as word does)
        var (_, errors, _, _) = Run("words -p n.md 1\nword @ 1 upper\nword n.md 2 1 upper\nwords -p n.md 1\nwords n.md 1\n" +
                                    "word n.md 1 6 upper\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("WELL - she said: try a/B first.\nANOTHER line\n", File.ReadAllText(In("work", "n.md")));
    }

    // ---- Second review of 1.5.7

    [Fact]
    public void ShiftSection_DownAndBottom_KeepTheBlankLinesAtTheEnd()
    {
        const string text = "# A\na\n\n# B\nb\n\n\n";
        File.WriteAllText(In("work", "f.md"), text);
        Run("shift -s f.md A down\n");
        Assert.Equal("# B\nb\n\n# A\na\n\n\n", File.ReadAllText(In("work", "f.md")));
        File.WriteAllText(In("work", "f.md"), text);
        Run("shift -s f.md A bottom\n");
        Assert.Equal("# B\nb\n\n# A\na\n\n\n", File.ReadAllText(In("work", "f.md")));
        // (the last section to the bottom: there already)
        var (output, _, _, _) = Run("shift -s f.md A bottom\n");
        Assert.Contains(Lang.T("Nothing moved: the lines are there already"), output);
    }

    [Fact]
    public void Undo_WithNoFileBefore_LeavesNoLine_NotTheLineOfTheChange()
    {
        File.WriteAllText(In("work", "t.md"), Ten);
        var (_, errors, _, _) = Run("shift t.md 1 bottom\nundo\nword @ 1 upper\n");
        Assert.Contains(Lang.F("@ has no line in {0} yet: peek it, or give the line (insert {0} +5 ...)", "t.md"), errors);
        Assert.Equal(Ten, File.ReadAllText(In("work", "t.md")));
    }

    [Fact]
    public void WordGuard_ComesBeforeTheCount_AndEndsWhenTheFileChanges()
    {
        File.WriteAllText(In("work", "t.md"), "- [ ] Buy milk\n");
        // (word 5 is "milk" with -p; without -p the line has 2 words)
        var (_, errors, _, _) = Run("words -p t.md 1\nword t.md 1 5 upper\n");
        Assert.Contains(Lang.F("words -p numbered line {0}, and without -p the numbers differ: give -p to word too", 1),
            errors);
        // A number words did not show is told by the count
        (_, errors, _, _) = Run("words -p t.md 1\nword t.md 1 9 upper\n");
        Assert.Contains(Lang.F("Line {0} has {1} words: {2} is not one", 1, 2, 9), errors);
        // Another command changed the file: the line can have moved, and is not checked
        File.WriteAllText(In("work", "n.md"), "first\nWell - she said: try a/b first.\n");
        (_, errors, _, _) = Run("words -p n.md 2\ninsert n.md +1 Title\nword n.md 2 1 upper\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Title\nFIRST\nWell - she said: try a/b first.\n", File.ReadAllText(In("work", "n.md")));
    }

    [Fact]
    public void At_InAnEmptyFile_PeekTakesTheFile_WordHasNoLine()
    {
        File.WriteAllText(In("work", "e.md"), string.Empty);
        var (output, errors, _, _) = Run("peek e.md\npeek @\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "e.md"), 0), output);
        // (open, edit and peek take the file from line 1: "open @" is "open $file +1")
        Assert.Equal("$file +1", Run("peek e.md\n").Session.WithAt("peek", "@"));
        Assert.Equal("$file +1", Run("peek e.md\n").Session.WithAt("open", "@"));
        Assert.Null(Run("peek e.md\n").Session.WithAt("word", "@ 1 upper"));
    }

    [Fact]
    public void ShiftSection_AFileWithoutHeadings_IsTold()
    {
        File.WriteAllText(In("work", "p.md"), "a\nb\nc\n");
        var (_, errors, _, _) = Run("shift -s p.md 2 up\nshift -s p.md Ideas up\n");
        Assert.Equal(2, errors.Split(Lang.F("{0} has no headings: shift -s moves a section under a heading", "p.md")).Length - 1);
        Assert.Contains("give -p to both", Lang.T("A word is all between blanks: give -p to both"));
        Assert.Contains("words -p, word -p", Run("help words\n").Output);
    }

    // ---- 1.5.8: markers around peek and words, the setting shellpeeklines

    // The markers on and the window of 15 lines, as in a new Blyant (the constructor turns them off for the tests
    // written before them)
    private static void MarkersAsSet()
    {
        Shell.Markers = Shell.DefaultMarkers;
        Shell.PeekLines = Shell.DefaultPeekLines;
    }

    private void SixtyLines() =>
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(i => "Line " + i + "\n")));

    [Fact]
    public void Peek_ShowsFifteenLines_BetweenMarkers()
    {
        MarkersAsSet();
        SixtyLines();
        var (output, errors, _, _) = Run("peek long.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(15, PeekLines(output));
        Assert.Contains(ShellSession.TopMarker + "long.md\n   1  Line 1\n", output);
        // (lines below the window: -v)
        Assert.Contains("  15  Line 15\n" + ShellSession.BelowMarker + Lang.F("lines {0}-{1} of {2}", 1, 15, 60) + "\n", output);
        Assert.DoesNotContain(Lang.F("(lines {0}-{1} of {2})", 1, 15, 60), output);
        // $peek: the lines alone, without the markers
        Assert.Equal(string.Join("\n", Enumerable.Range(1, 15).Select(i => "Line " + i)), ShellRegisters.Peek);
    }

    [Fact]
    public void Peek_WithMarkers_TellsWhereTheLinesAre_AlsoForAWholeFile()
    {
        MarkersAsSet();
        SixtyLines();
        // A whole file: the end marker tells it too (without the markers nothing is told under it)
        var (output, _, _, _) = Run("peek plot.md\npeek long.md +\npeek long.md /Line 30\n");
        Assert.Contains(ShellSession.TopMarker + "plot.md\n  1  Plot\n" + ShellSession.EndMarker + Lang.F("lines {0}-{1} of {2}", 1, 1, 1) + "\n", output);
        // A window in the middle: lines above it (-^) and below it (-v)
        Assert.Contains(ShellSession.AboveMarker + "long.md\n   2  Line 2\n", output);
        Assert.Contains("  16  Line 16\n" + ShellSession.BelowMarker + Lang.F("lines {0}-{1} of {2}", 2, 16, 60) + "\n", output);
        Assert.Contains("> 30  Line 30\n", output);
        Assert.Contains(ShellSession.AboveMarker + "long.md\n  28  Line 28\n", output);
        Assert.Contains(ShellSession.BelowMarker + Lang.F("match {0} of {1}, lines {2}-{3} of {4}", 1, 1, 28, 42, 60) + "\n", output);
        // A window past the end shows no lines, and no markers
        (output, _, _, _) = Run("peek plot.md +5\n");
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "plot.md"), 1), output);
        Assert.DoesNotContain(ShellSession.TopMarker + "plot.md", output);
        // At the end: lines above it, and the end of the file
        (output, _, _, _) = Run("peek long.md bottom\n");
        Assert.Contains(ShellSession.AboveMarker + "long.md\n  46  Line 46\n", output);
        Assert.Contains("  60  Line 60\n" + ShellSession.EndMarker + Lang.F("lines {0}-{1} of {2}", 46, 60, 60) + "\n", output);
    }

    [Fact]
    public void AutoPeek_WithMarkers_NamesTheFileInTheFirstMarker()
    {
        try
        {
            MarkersAsSet();
            Shell.AutoPeek = true;
            File.WriteAllText(In("work", "a.md"), "one\ntwo\nthree\n");
            var (output, errors, _, _) = Run("peek a.md 1-2\nchange a.md +1 ONE\n");
            Assert.Equal(string.Empty, errors);
            Assert.Contains(ShellSession.TopMarker + Lang.F("{0} (peek again)", "a.md") + "\n  1  ONE\n  2  two\n" +
                            ShellSession.BelowMarker + Lang.F("lines {0}-{1} of {2}", 1, 2, 3) + "\n", output);
            Assert.DoesNotContain(Lang.F("(peek again: {0})", "a.md"), output);
        }
        finally
        {
            Shell.AutoPeek = false;
        }
    }

    [Fact]
    public void Words_WithMarkers_ShowTheFileTheLineAndTheCount()
    {
        MarkersAsSet();
        File.WriteAllText(In("work", "w.md"), "one two three\nfour\n");
        var (output, errors, _, _) = Run("words w.md 1\nwords w.md 2\nword w.md 1 2 delete\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(ShellSession.TopMarker + Lang.F("{0}, line {1}", "w.md", 1) + "\n  #1:1 one  #2:5 two  #3:9 three\n" +
                        ShellSession.EndMarker + Lang.F("{0} words", 3) + "\n", output);
        Assert.Contains(ShellSession.TopMarker + Lang.F("{0}, line {1}", "w.md", 2) + "\n  #1:1 four\n" + ShellSession.EndMarker + Lang.T("1 word") + "\n", output);
        // (the markers tell what the line under the words told)
        Assert.DoesNotContain(Lang.F("{0}, line {1}: {2} words", In("work", "w.md"), 1, 3), output);
        // A change: the words numbered again, between the markers
        Assert.Contains(Lang.F("{0}: line {1} changed", In("work", "w.md"), 1) + "\n" +
                        ShellSession.TopMarker + Lang.F("{0}, line {1}", "w.md", 1) + "\n  #1:1 one  #2:5 three\n" + ShellSession.EndMarker + Lang.F("{0} words", 2) + "\n",
            output);
    }

    [Fact]
    public void Markers_Off_ShowTheLinesAsBefore()
    {
        try
        {
            MarkersAsSet();
            SixtyLines();
            FileIo.SaveSettings();
            var (output, errors, _, _) = Run("set shellmarkers false\npeek long.md\nwords long.md 2\n");
            Assert.Equal(string.Empty, errors);
            Assert.False(Shell.Markers);
            // (no line starts with a marker)
            Assert.DoesNotContain(output.Split('\n'), line => line.StartsWith("->") || line.StartsWith("-^") ||
                                                              line.StartsWith("<-") || line.StartsWith("-v"));
            Assert.Contains("  15  Line 15\n" + Lang.F("(lines {0}-{1} of {2})", 1, 15, 60) + "\n", output);
            Assert.Contains(Lang.F("{0}, line {1}: {2} words", In("work", "long.md"), 2, 2), output);
            Run("set shellmarkers true\n");
            Assert.True(Shell.Markers);
        }
        finally
        {
            MarkersAsSet();
        }
    }

    [Fact]
    public void PeekLines_IsSet_From1To1000()
    {
        try
        {
            MarkersAsSet();
            SixtyLines();
            FileIo.SaveSettings();
            var (output, _, _, _) = Run("set shellpeeklines 30\npeek long.md\nhelp peek\npeek long.md +40\n");
            Assert.Equal(30, Shell.PeekLines);
            Assert.Contains(ShellSession.BelowMarker + Lang.F("lines {0}-{1} of {2}", 1, 30, 60), output);
            // (the help row can be wrapped)
            Assert.Contains("the first 30,", output);
            // "+40": as many lines as set, from line 40
            Assert.Contains(ShellSession.EndMarker + Lang.F("lines {0}-{1} of {2}", 40, 60, 60), output);
            (output, _, _, _) = Run("set shellpeeklines 0\n");
            Assert.Equal(30, Shell.PeekLines);
            Assert.Equal(15, Shell.ParsePeekStep("1001", Shell.DefaultPeekLines));
            Assert.Equal(1000, Shell.ParsePeekStep("1000", Shell.DefaultPeekLines));
            // reset: 15 lines, and the markers on again
            Run("set shellmarkers false\nreset\n", yes: true);
            Assert.Equal(15, Shell.PeekLines);
            Assert.True(Shell.Markers);
        }
        finally
        {
            MarkersAsSet();
        }
    }

    [Fact]
    public void Change_WithoutAText_ShowsTheLinesBetweenTheMarkers()
    {
        try
        {
            MarkersAsSet();
            File.WriteAllText(In("work", "list.md"), "One\nTwo\nThree\nFour\n");
            var (shown, _, _, _) = Run("change list.md 2-3\nchange list.md 1-4\n");
            // Lines above and below them, then the whole file
            Assert.Contains(ShellSession.AboveMarker + "list.md\n  2  Two\n  3  Three\n" + ShellSession.BelowMarker +
                            Lang.F("lines {0}-{1} of {2}", 2, 3, 4) + "\n", shown);
            Assert.Contains(ShellSession.TopMarker + "list.md\n  1  One\n", shown);
            Assert.Contains("  4  Four\n" + ShellSession.EndMarker + Lang.F("lines {0}-{1} of {2}", 1, 4, 4) + "\n", shown);
            // Off: the lines alone
            Shell.Markers = false;
            (shown, _, _, _) = Run("change list.md 2-3\n");
            Assert.Contains("  2  Two\n  3  Three\n" + Lang.T("Nothing was deleted"), shown);
        }
        finally
        {
            MarkersAsSet();
        }
    }

    [Fact]
    public void FindD_ShowsWhenEachFileWasChanged_LinedUpAfterTheNames()
    {
        File.SetLastWriteTime(In("work", "plot.md"), new DateTime(2026, 11, 18, 14, 2, 0));
        File.SetLastWriteTime(In("work", "Ending.md"), new DateTime(2026, 1, 5, 9, 40, 0));
        var (output, errors, _, _) = Run("find -d *.md\n");
        Assert.Equal(string.Empty, errors);
        // (the names lined up: "Ending.md" is the longest)
        Assert.Contains("Ending.md  2026-01-05 09:40\n", output);
        Assert.Contains("plot.md    2026-11-18 14:02\n", output);
        // A directory has no date; with -b and -f the names are as without -d
        (output, _, _, _) = Run("find -d Draft\nfind -b -d plot\n");
        Assert.Contains("  1  Draft/\n", output);
        Assert.Contains("  1  plot.md  2026-11-18 14:02\n", output);
        // With -t: the date between the line number and the line found
        (output, _, _, _) = Run("find -d -t Plot\n");
        Assert.Contains("plot.md:1  2026-11-18 14:02  Plot\n", output);
        // The last search again, with the dates as they are now
        (output, _, _, _) = Run("find plot\nfind -d\n");
        Assert.Single(output.Split('\n'), line => line.EndsWith("plot.md  2026-11-18 14:02"));
        File.SetLastWriteTime(In("work", "plot.md"), new DateTime(2026, 11, 19, 8, 0, 0));
        (output, _, _, _) = Run("find -d\n");
        Assert.Contains("plot.md  2026-11-19 08:00\n", output);
        // Without -d: no dates
        (output, _, _, _) = Run("find *.md\n");
        Assert.DoesNotContain("2026-", output);
        (_, errors, _, _) = Run("find -x plot\n");
        Assert.Contains(Lang.F("Not taken: {0} (-l full paths, -f folder/name, -b names only, -d dates, -r again, -t text)", "-x"),
            errors);
        Assert.Contains("find -d", Run("help find\n").Output);
    }

    [Fact]
    public void LsD_AndLsS_ShowTheDatesAndSizesOfTheFiles_LinedUp()
    {
        File.WriteAllText(In("work", "plot.md"), new string('x', 2048));
        File.SetLastWriteTime(In("work", "plot.md"), new DateTime(2026, 11, 18, 14, 2, 0));
        File.SetLastWriteTime(In("work", "Ending.md"), new DateTime(2026, 1, 5, 9, 40, 0));
        var size = FileSizeFormatter.FormatSize(2048);
        var small = FileSizeFormatter.FormatSize(new FileInfo(In("work", "Ending.md")).Length);
        var width = Math.Max(size.Length, small.Length);
        var (output, errors, _, _) = Run("ls -d -s\n");
        Assert.Equal(string.Empty, errors);
        // A folder has neither; the files lined up ("Ending.md" is the longest name)
        Assert.Contains("  1  Draft/\n", output);
        Assert.Contains("Ending.md  2026-01-05 09:40  " + small.PadLeft(width) + "\n", output);
        Assert.Contains("plot.md    2026-11-18 14:02  " + size.PadLeft(width) + "\n", output);
        // One of them, in any order with the others; a directory after them
        (output, _, _, _) = Run("ls -s\nls -a -d .\n");
        Assert.Contains("plot.md    " + size.PadLeft(width) + "\n", output);
        Assert.Contains("plot.md    2026-11-18 14:02\n", output);
        // Without them: the names alone
        (output, _, _, _) = Run("ls\n");
        Assert.DoesNotContain("2026-", output);
        // An option not known is told; a directory with such a name is listed
        (_, errors, _, _) = Run("ls -x\n");
        Assert.Contains(Lang.F("Not taken: {0} (-a hidden ones too, -l full paths, -d dates, -s sizes)", "-x"), errors);
        Directory.CreateDirectory(In("work", "-x"));
        (output, errors, _, _) = Run("ls -x\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(In("work", "-x"), output);
        Assert.Contains("ls -d, ls -s", Run("help ls\n").Output);
    }

    [Fact]
    public void Ls_TabCompletesTheOptions_AndTheDirectories()
    {
        Assert.Equal("ls -a", Tab(Completion(), "ls -"));
        // As typed: "-D" is not an option, so nothing more is offered after it
        Assert.Equal("ls -D ", Tab(Completion(), "ls -D "));
        // The options not given yet
        Assert.Equal("ls -d -a", Tab(Completion(), "ls -d -"));
        Assert.Equal("ls -d -s", Tab(Completion(), "ls -d -", true, true, true));
        // Directories as before, also after the options
        Assert.Equal("ls Draft/", Tab(Completion(), "ls Dr"));
        Assert.Equal("ls -s -d Draft/", Tab(Completion(), "ls -s -d Dr"));
    }

    // ---- Review of 1.5.8

    [Fact]
    public void Ls_OptionsNotKnown_AndOptionsAfterTheDirectory_AreTold()
    {
        foreach (var word in new[] { "-ab", "-", "--" })
        {
            var (_, errors, _, _) = Run("ls " + word + "\n");
            Assert.Contains(Lang.F("Not taken: {0} (-a hidden ones too, -l full paths, -d dates, -s sizes)", word), errors);
        }

        var (_, after, _, _) = Run("ls Draft -d\n");
        Assert.Contains(Lang.T("Give the options before the directory: ls -d notes"), after);
        // A directory named as an option: "ls ./-d" (ls -d is the option)
        Directory.CreateDirectory(In("work", "-d"));
        File.WriteAllText(In("work", "-d", "inside.md"), "x\n");
        var (output, errors2, _, _) = Run("ls -d\nls ./-d\n");
        Assert.Equal(string.Empty, errors2);
        Assert.Contains("inside.md", output);
        Assert.Contains("  1  -d/\n", output);
    }

    [Fact]
    public void Ls_TabOffersTheOptions_ThenADirectoryWhoseNameStartsWithADash()
    {
        Directory.CreateDirectory(In("work", "-x"));
        Assert.Equal("ls -x/", Tab(Completion(), "ls -", true, true, true, true, true));
    }

    [Fact]
    public void LsD_DoesNotReadTheSizes_AndFindD_LinesUpTheFilesOnly()
    {
        File.SetLastWriteTime(In("work", "plot.md"), new DateTime(2026, 11, 18, 14, 2, 0));
        File.WriteAllText(In("work", "notes", "plot-b.md"), "x\n");
        File.SetLastWriteTime(In("work", "notes", "plot-b.md"), new DateTime(2026, 11, 17, 8, 5, 0));
        Directory.CreateDirectory(In("work", "plots"));
        var (output, errors, _, _) = Run("find -d plot\nls -d\n");
        Assert.Equal(string.Empty, errors);
        // The folder without a date and not padded; the files lined up after the longest name of a file
        var folder = Path.Combine("plots") + "/";
        Assert.Contains("  " + folder + "\n", output);
        var longest = Path.Combine("notes", "plot-b.md");
        Assert.Contains(longest + "  2026-11-17 08:05\n", output);
        Assert.Contains("plot.md" + new string(' ', longest.Length - "plot.md".Length) + "  2026-11-18 14:02\n", output);
        // ls -d: the date, and no size after it
        Assert.Contains("plot.md    2026-11-18 14:02\n", output);
    }

    [Fact]
    public void Peek_OfAnEmptyFile_ShowsNoMarkers_AndTheHelpSaysTheFirstLine()
    {
        try
        {
            MarkersAsSet();
            File.WriteAllText(In("work", "empty.md"), string.Empty);
            var (output, _, _, _) = Run("peek empty.md\n");
            Assert.Contains(Lang.F("{0} has {1} lines", In("work", "empty.md"), 0), output);
            Assert.DoesNotContain(ShellSession.TopMarker, output);
            Assert.DoesNotContain(ShellSession.EndMarker, output);
            Shell.PeekLines = 1;
            Assert.Contains("the first line,", Run("help peek\n").Output);
        }
        finally
        {
            MarkersAsSet();
        }
    }

    [UnixFact]
    public void OnLinuxAndMacOS_LsDAndFindD_TakeTheDateOfTheFileALinkPointsTo()
    {
        File.SetLastWriteTime(In("work", "plot.md"), new DateTime(2026, 11, 18, 14, 2, 0));
        File.CreateSymbolicLink(In("work", "alias.md"), In("work", "plot.md"));
        File.WriteAllText(In("work", "gone.md"), "x\n");
        File.CreateSymbolicLink(In("work", "broken.md"), In("work", "gone.md"));
        File.Delete(In("work", "gone.md"));
        var (output, errors, _, _) = Run("ls -d -s\nfind -d alias\nfind -d broken\n");
        Assert.Equal(string.Empty, errors);
        var size = FileSizeFormatter.FormatSize(new FileInfo(In("work", "plot.md")).Length);
        // The link: the date and the size of plot.md; the broken link: neither
        Assert.Matches(@"alias\.md +2026-11-18 14:02 +" + System.Text.RegularExpressions.Regex.Escape(size) + "\n", output);
        Assert.Matches(@"broken\.md +- +-\n", output);
        Assert.Contains("alias.md  2026-11-18 14:02\n", output);
        Assert.DoesNotMatch(@"broken\.md[^\n]*\d{4}-\d\d-\d\d", output);
    }

    // ---- 1.5.9: "#" before the numbers of words

    [Fact]
    public void Words_AreNumberedWithAHash_AndWordTakesTheNumbersAsShown()
    {
        File.WriteAllText(In("work", "v.md"), "Blyant v1 5 8 Shell\n");
        var (output, errors, _, _) = Run("words v.md 1\n");
        Assert.Equal(string.Empty, errors);
        // A word that is a number is told apart from the number of the word
        Assert.Contains("  #1:1 Blyant  #2:8 v1  #3:11 5  #4:13 8  #5:15 Shell\n", output);
        // "#4", "#2-#3" and "#2-3", as typed without "#"
        (_, errors, _, _) = Run("word v.md 1 #4 replace 9\nword v.md 1 #1-2 upper\nword v.md 1 #2-#3 delete\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("BLYANT 9 Shell\n", Text("v.md"));
        (_, errors, _, _) = Run("word v.md 1 ## delete\nword v.md 1 #x delete\n");
        Assert.Contains(Lang.F("Not words: {0} (as 9, or 5-7)", "##"), errors);
        Assert.Contains(Lang.F("Not words: {0} (as 9, or 5-7)", "#x"), errors);
        Assert.True(ShellSession.WordNumbers("#5-#7", out var five, out var seven, out _) && (five, seven) == (5, 7));
        Assert.True(ShellSession.WordNumbers("#5-7", out five, out seven, out _) && (five, seven) == (5, 7));
        Assert.True(ShellSession.WordNumbers("9", out var nine, out _, out _) && nine == 9);
        // Tab goes on after a number with "#" as after one without
        Assert.Equal("word v.md 1 #2 replace", Tab(Completion(), "word v.md 1 #2 re"));
    }

    // ---- 1.5.9: the logo when the shell starts

    // The shell started at a terminal of a width (null: not known), with "exit": what it wrote
    private string StartedAt((int Width, int Height)? size)
    {
        var output = new StringWriter();
        var session = new ShellSession(new StringReader("exit\n"), output, new StringWriter(), In("work"), true)
        {
            WindowSize = () => size
        };
        Shell.Run(session);
        return output.ToString().Replace("\r\n", "\n");
    }

    [Fact]
    public void Shell_StartsWithTheLogo_AtATerminal_WithoutColorsAndBlanksBefore()
    {
        var logo = Logo.Current;
        try
        {
            Shell.Logo = true;
            Logo.Current = LogoArt.BuiltIn;
            var output = StartedAt((80, 24));
            // "- Shell mode -", the rows of the logo without the blanks they start with (a blank line above and below
            // them), then the first line
            var rows = LogoArt.BuiltIn.AllRows.Select(row => row.TrimEnd()[4..]).ToList();
            Assert.StartsWith(Lang.T("- Shell mode -") + "\n\n" + string.Join("\n", rows) + "\n\n" +
                              Lang.F("{0} {1}: help lists the commands, exit leaves", DialogText.ApplicationName,
                                  DialogText.ApplicationVersion) + "\n", output);
            Assert.StartsWith("\"\"\"\"\"MM_ MMM", rows[0]);
            // A window of an unknown width: shown too
            Assert.StartsWith(Lang.T("- Shell mode -"), StartedAt(null));
        }
        finally
        {
            Logo.Current = logo;
        }
    }

    [Fact]
    public void Shell_LeavesOutTheLogo_FromAFile_WhenTurnedOff_AndInANarrowWindow()
    {
        var logo = Logo.Current;
        try
        {
            Shell.Logo = true;
            Logo.Current = LogoArt.BuiltIn;
            // Commands from a file or a pipe (not a terminal)
            Assert.DoesNotContain(Lang.T("- Shell mode -"), Run("pwd\n").Output);
            // Narrower than the logo (54 columns, the last column of the window not used)
            Assert.DoesNotContain(Lang.T("- Shell mode -"), StartedAt((54, 24)));
            Assert.Contains(Lang.T("- Shell mode -"), StartedAt((55, 24)));
            // Turned off: the first line alone
            Shell.Logo = false;
            Assert.StartsWith(Lang.F("{0} {1}: help lists the commands, exit leaves", DialogText.ApplicationName,
                DialogText.ApplicationVersion), StartedAt((80, 24)));
            // A logo of its own: the blank rows around it and the blanks the rows start with left out
            Shell.Logo = true;
            Logo.Current = new LogoArt("Mine", new[] { "", "  AB", "   C", "" });
            Assert.StartsWith(Lang.T("- Shell mode -") + "\n\nAB\n C\n\n", StartedAt((80, 24)));
        }
        finally
        {
            Logo.Current = logo;
        }
    }

    // ---- 1.5.9: j, jj, k, kk, gg and G move a peek

    // The windows a run of commands showed, as "(lines a-b of n)" tells them (markers off, as in these tests)
    private static List<string> Windows(string output) =>
        output.Split('\n').Where(line => line.StartsWith("(lines ")).ToList();

    [Fact]
    public void VimKeys_MoveThePeek_AsPlusAndMinus_TopAndBottom()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        var (output, errors, _, _) = Run("peek long.md +10 5\nj\njj\nk\nkk\nj 3\njj 2\nG\ngg\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(new[] { (10, 14), (11, 15), (16, 20), (15, 19), (10, 14), (13, 17), (23, 27), (56, 60), (1, 5) }
            .Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 60)), Windows(output));
        // After a file, as + and bottom are
        (output, _, _, _) = Run("peek long.md +1 5\npeek long.md jj\npeek long.md G\n");
        Assert.Equal(new[] { (1, 5), (6, 10), (56, 60) }.Select(w => Lang.F("(lines {0}-{1} of {2})", w.Item1, w.Item2, 60)),
            Windows(output));
        // "G notes.md": that file, at the bottom
        (output, _, _, _) = Run("G long.md\n");
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 41, 60, 60), output);
    }

    [Fact]
    public void VimKeys_AreCaseExact_AndAFileNamedJIsNotTakenForJ()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        foreach (var key in new[] { "J", "JJ", "K", "GG", "Gg", "g" })
        {
            var (_, errors, _, _) = Run("peek long.md +10 5\n" + key + "\n");
            Assert.Contains(Lang.F("Unknown command: {0} (help lists the commands)", key), errors);
        }

        File.WriteAllText(In("work", "j"), "not this\n");
        var (output, _, _, _) = Run("peek long.md +10 5\nj\n");
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 11, 15, 60), output);
        Assert.DoesNotContain("not this", output);
        var (_, wrong, _, _) = Run("peek long.md\nj x\n");
        Assert.Contains(Lang.F("{0} takes a number of steps, or nothing: {0} 20", "j"), wrong);
        Assert.Equal("G", EventLog.CommandName("G"));
        Assert.Equal("jj ...", EventLog.CommandName("jj 3"));
        Assert.Contains("j, jj, k, kk, gg, G", Run("help peek\n").Output);
        Assert.Contains("j, jj, k, kk, gg, G", Run("help G\n").Output);
    }

    // ---- 1.5.9: tags

    [Fact]
    public void Tag_GivesFilesATag_AndTheTagListsThem_AsTheLastList()
    {
        var (output, errors, _, session) = Run("ls\ntag add 3,4 draft\ntag add plot.md ferry\ntag draft\necho $tag\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.F("Tagged {0}: {1} files", "draft", 2), output);
        Assert.Contains(Lang.F("Tagged {0}: {1}", "ferry", In("work", "plot.md")), output);
        // The rows of the tag, numbered: the last list
        Assert.Contains("  1  Ending.md\n  2  plot.md\n", output);
        Assert.Equal(new[] { In("work", "Ending.md"), In("work", "plot.md") }, session.LastList.Select(item => item.Path));
        Assert.EndsWith("draft\n", output);
        Assert.Equal(new[] { "draft", "ferry" }, FileTags.TagsOf(In("work", "plot.md")));
        // Kept in tags.txt
        Assert.Contains(In("work", "plot.md") + "\tdraft ferry", File.ReadAllText(FileTags.FilePath));
        // "tag" alone: those of $tag; "tag -l": the full paths
        (output, _, _, _) = Run("tag\ntag -l ferry\n");
        Assert.Contains("  2  plot.md\n", output);
        Assert.Contains("  1  " + In("work", "plot.md") + "\n", output);
    }

    [Fact]
    public void Tags_ListsTheTags_AndTheFilesWithTags()
    {
        var (output, errors, _, _) = Run("tags\ntag add plot.md draft\ntag add plot.md ferry\ntag add Ending.md draft\ntags\ntags -f\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.T("No tags yet: tag add notes.md draft"), output);
        Assert.Contains("  draft  " + Lang.F("{0} files", 2) + "\n  ferry  " + Lang.T("1 file") + "\n", output);
        Assert.Contains("  1  plot.md    draft, ferry\n  2  Ending.md  draft\n", output);
    }

    [Fact]
    public void TagRemove_Rename_AndClear_ChangeTheTags_ClearAsksFirst()
    {
        Run("tag add plot.md draft\ntag add plot.md ferry\ntag add Ending.md draft\n");
        var (output, errors, _, _) = Run("tag remove plot.md ferry\ntag remove plot.md ferry\ntag rename draft final\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.F("Tag {0} taken off: {1}", "ferry", In("work", "plot.md")), output);
        Assert.Contains(Lang.F("Nothing taken off: no tag {0} there", "ferry"), output);
        Assert.Contains(Lang.F("Tag {0} is now {1}", "draft", "final"), output);
        Assert.Equal(new[] { "final" }, FileTags.TagsOf(In("work", "Ending.md")));
        // From a file the question is not asked: nothing is changed; with --yes it is
        (output, _, _, _) = Run("tag clear final\n");
        Assert.Contains(Lang.T("Nothing was changed"), output);
        Assert.Equal(2, FileTags.FilesWith("final").Count);
        (output, _, _, _) = Run("tag clear final\n", yes: true);
        Assert.Contains(Lang.F("Tag {0} taken off all files", "final"), output);
        Assert.Empty(FileTags.All());
        Run("tag add plot.md a\ntag add Ending.md b\n");
        (output, _, _, _) = Run("tag remove plot.md\ntags clear\n", yes: true);
        Assert.Contains(Lang.F("Tags taken off: {0}", In("work", "plot.md")), output);
        Assert.Contains(Lang.T("All tags taken off"), output);
        Assert.Empty(FileTags.Tagged());
    }

    [Fact]
    public void Tag_TellsWhatItCannotTake()
    {
        var (_, errors, _, _) = Run("tag\ntag draft\ntag add plot.md two words\ntag add plot.md a/b\ntag add plot.md add\n" +
                                    "tag add Draft x\ntag add missing.md x\nls\ntag add 2-9 x\ntag add @ x\n");
        Assert.Contains(Lang.T("No tag yet: tag add notes.md draft, then tag draft"), errors);
        Assert.Contains(Lang.F("No file has the tag {0} (tags lists them)", "draft"), errors);
        Assert.Contains(Lang.T("Give the files and the tag: tag add notes.md draft (or rows: 2,4 or 2-5)"), errors);
        Assert.Contains(Lang.F("Not a name for a tag: {0} (letters, digits, - and _)", "a/b"), errors);
        Assert.Contains(Lang.F("{0} is a word of the tag command: give the tag another name", "add"), errors);
        Assert.Contains(Lang.F("Not a file: {0}", In("work", "Draft")), errors);
        Assert.Contains(Lang.F("Not rows: {0} (as 2,4,7 or 2-5)", "2-9"), errors);
        Assert.Contains(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"), errors);
        Assert.Empty(FileTags.All());
        // (tags with a tag: tag was meant)
        Assert.Contains(Lang.F("tag {0} lists the files with the tag (tags lists the tags)", "x"), Run("tags x\n").Errors);
    }

    [Fact]
    public void DollarTag_FollowsTheFileWorkedOn_WhenItHasTagsButNotDollarTag()
    {
        File.WriteAllText(In("work", "c.md"), "c\n");
        Run("tag add plot.md draft\ntag add plot.md ferry\ntag add Ending.md research\ntag add c.md ferry\n");
        // $tag is ferry (given last); plot.md has it: it stays
        var (output, _, _, _) = Run("tag ferry\npeek plot.md\necho $tag\n");
        Assert.DoesNotContain("$tag =", output);
        Assert.EndsWith("ferry\n", output);
        // Ending.md has research only: $tag follows, and says so
        (output, _, _, _) = Run("peek Ending.md\necho $tag\ntag\n");
        Assert.Contains("  $tag = research\n", output);
        Assert.Contains("research\n  1  Ending.md\n", output);
        // A file without tags leaves it
        File.WriteAllText(In("work", "none.md"), "n\n");
        (output, _, _, _) = Run("peek none.md\necho $tag\n");
        Assert.EndsWith("research\n", output);
        // tag add sets it
        (output, _, _, _) = Run("tag add none.md later\necho $tag\n");
        Assert.EndsWith("later\n", output);
    }

    [Fact]
    public void Tags_FollowAFileCopiedRenamedMovedAndDeleted_AndTagsTxtIsProtected()
    {
        Run("tag add plot.md draft\n");
        var (_, errors, _, _) = Run("copy plot.md Draft\nrename plot.md story.md\nmove Ending.md notes\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(In("work", "Draft", "plot.md")));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(In("work", "story.md")));
        Assert.Empty(FileTags.TagsOf(In("work", "plot.md")));
        Run("tag add notes/Ending.md x\nrename notes papers\n");
        Assert.Equal(new[] { "x" }, FileTags.TagsOf(In("work", "papers", "Ending.md")));
        Run("delete story.md\n", yes: true);
        Assert.Empty(FileTags.TagsOf(In("work", "story.md")));
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(In("work", "Draft", "plot.md")));
        // A file of Blyant: not deleted nor renamed from the shell
        (_, errors, _, _) = Run("delete " + FileTags.FilePath + "\n", yes: true);
        Assert.Contains(DialogText.FileUsedByBlyant, errors);
        Assert.True(File.Exists(FileTags.FilePath));
    }

    [Fact]
    public void Tag_TabCompletesWhatItDoes_TheFiles_AndTheTags()
    {
        Run("tag add plot.md draft\n");
        Assert.Equal("tag add", Tab(Completion(), "tag a"));
        Assert.Equal("tag draft", Tab(Completion(), "tag dr"));
        Assert.Equal("tag add plot.md", Tab(Completion(), "tag add pl"));
        Assert.Equal("tag add plot.md draft", Tab(Completion(), "tag add plot.md d"));
        Assert.Equal("tag rename draft", Tab(Completion(), "tag rename d"));
        Assert.Equal("tags -f", Tab(Completion(), "tags -"));
        Assert.Contains("tag add file|n|2,4|2-5 name", Run("help tag\n").Output);
        Assert.Contains("tags clear", Run("help tags\n").Output);
        Assert.Equal("tag ...", EventLog.CommandName("tag add plot.md draft"));
    }

    // ---- 1.5.9: replace

    [Fact]
    public void ReplacedIn_ReplacesFromTheLeft_AndCountsThem()
    {
        Assert.Equal(("a-b-c", 2), ShellSession.ReplacedIn("a b c", " ", "-", StringComparison.Ordinal));
        // (what replaced a text is not searched again)
        Assert.Equal(("aaaa", 2), ShellSession.ReplacedIn("aa", "a", "aa", StringComparison.Ordinal));
        Assert.Equal(("x", 0), ShellSession.ReplacedIn("x", "", "y", StringComparison.Ordinal));
        Assert.Equal(("Marit, Marit", 2), ShellSession.ReplacedIn("MAREN, maren", "Maren", "Marit", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(("MAREN, maren", 0), ShellSession.ReplacedIn("MAREN, maren", "Maren", "Marit", StringComparison.Ordinal));
    }

    [Fact]
    public void Replace_ReplacesInTheWholeFile_ShowsTheLines_AndUndoTakesItBack()
    {
        var text = "Maren stood at the rail.\r\nNobody spoke.\r\n\"MAREN!\" Anna called. Maren, Maren.\r\nThe end.\r\n";
        File.WriteAllText(In("work", "r.md"), text);
        var (output, errors, _, session) = Run("replace r.md Maren Marit\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Marit stood at the rail.\r\nNobody spoke.\r\n\"MAREN!\" Anna called. Marit, Marit.\r\nThe end.\r\n",
            File.ReadAllText(In("work", "r.md")));
        Assert.Contains("  1  Marit stood at the rail.\n  3  \"MAREN!\" Anna called. Marit, Marit.\n", output);
        Assert.Contains(Lang.F("{0}: {1} replaced, in {2} lines", In("work", "r.md"), 3, 2), output);
        // The file at the first line changed: open 1, peek @
        Assert.Equal((1, 1), session.LastList.Single().At);
        // undo, in the same shell: the file as it was (line endings too)
        File.WriteAllText(In("work", "r.md"), text);
        (output, _, _, _) = Run("replace r.md Maren Marit\nundo\n");
        Assert.Contains(Lang.F("Undone: {0}", "replace r.md Maren Marit"), output);
        Assert.Equal(text, File.ReadAllText(In("work", "r.md")));
        // Any letter case; one line
        (output, _, _, _) = Run("replace -i r.md maren Marit\n");
        Assert.Contains(Lang.F("{0}: {1} replaced, in {2} lines", In("work", "r.md"), 4, 2), output);
        Assert.DoesNotContain("MAREN", File.ReadAllText(In("work", "r.md")));
        (output, _, _, _) = Run("replace r.md Nobody \"No one\"\nreplace r.md \"stood at\" \"\"\n");
        Assert.Contains(Lang.F("{0}: 1 replaced, in line {1}", In("work", "r.md"), 2), output);
        Assert.StartsWith("Marit  the rail.\r\nNo one spoke.", File.ReadAllText(In("work", "r.md")));
    }

    [Fact]
    public void ReplaceN_OnlyShowsWhatWouldChange()
    {
        File.WriteAllText(In("work", "r.md"), "draft one\ndraft two\n");
        var (output, errors, _, _) = Run("replace -n r.md draft final\nundo\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("draft one\ndraft two\n", File.ReadAllText(In("work", "r.md")));
        Assert.Contains("  1  final one\n  2  final two\n", output);
        Assert.Contains(Lang.F("{0}: {1} would be replaced, in {2} lines (-n: nothing written)", In("work", "r.md"), 2, 2), output);
        // Nothing to undo in the same shell: nothing was written
        Assert.Contains(Lang.T("Nothing to undo: no file has been changed in this shell"), output);
        // "-i -n" in any order
        (output, _, _, _) = Run("replace -i -n r.md DRAFT final\n");
        Assert.Contains(Lang.F("{0}: {1} would be replaced, in {2} lines (-n: nothing written)", In("work", "r.md"), 2, 2), output);
    }

    [Fact]
    public void Replace_ShowsAtMostAPeekOfLines_BetweenTheMarkers()
    {
        try
        {
            MarkersAsSet();
            File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 40).Select(n => "line " + n + " x\n")));
            var (output, _, _, _) = Run("replace long.md x y\n");
            Assert.Contains(ShellSession.TopMarker + "long.md\n   1  line 1 y\n", output);
            Assert.Contains("  15  line 15 y\n  " + Lang.F("(and {0} more lines)", 25) + "\n" + ShellSession.EndMarker +
                            Lang.F("{0} lines of {1}", 40, 40) + "\n", output);
            Assert.DoesNotContain("  16  line 16 y", output);
            File.WriteAllText(In("work", "s.md"), "a\nb x\nc\n");
            (output, _, _, _) = Run("replace s.md x y\n");
            Assert.Contains(ShellSession.AboveMarker + "s.md\n  2  b y\n" + ShellSession.BelowMarker + Lang.F("1 line of {0}", 3), output);
        }
        finally
        {
            MarkersAsSet();
        }
    }

    [Fact]
    public void Replace_TellsWhatItCannotDo_AndNeverLogsTheTexts()
    {
        File.WriteAllText(In("work", "r.md"), "same\n");
        var (output, errors, _, _) = Run("replace r.md\nreplace r.md \"\" x\nreplace -x r.md a b\nreplace r.md secret other\n" +
                                         "replace r.md same same\nreplace @ a b\nreplace Draft a b\n");
        Assert.Contains(Lang.T("Give the file, the text and what replaces it: replace notes.md draft final"), errors);
        Assert.Contains(Lang.F("Not taken: {0} (-n: only show what would change, -i: any letter case)", "-x"), errors);
        Assert.Contains(Lang.F("No {0} in {1}", "secret", "r.md"), output);
        Assert.Contains(Lang.T("Nothing changed: the new text is the same as the old"), output);
        Assert.Contains(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"), errors);
        Assert.Contains(Lang.F("Not a file: {0}", In("work", "Draft")), errors);
        Run("replace r.md same Hidden\n");
        Assert.DoesNotContain("Hidden", string.Join("\n", EventLog.All));
        Assert.DoesNotContain("same", string.Join("\n", EventLog.All.Where(entry => entry.Contains("Replace"))));
        // @: the file worked on
        (_, errors, _, _) = Run("peek r.md\nreplace @ Hidden Shown\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Shown\n", File.ReadAllText(In("work", "r.md")));
    }

    [Fact]
    public void Replace_TabCompletesTheOptionsAndTheFile()
    {
        Assert.Equal("replace -n", Tab(Completion(), "replace -"));
        Assert.Equal("replace -n -i", Tab(Completion(), "replace -n -"));
        Assert.Equal("replace -i plot.md", Tab(Completion(), "replace -i pl"));
        Assert.Contains("replace [-n] [-i] file|n old new", Run("help replace\n").Output);
    }

    // ---- replace -n asks at a terminal

    // Lines read one at a time, and something done before a line is read (the file changed while a question waits)
    private sealed class ReaderWithStep(string text, int before, Action step) : StringReader(text)
    {
        private int _read;

        public override string? ReadLine()
        {
            if (++_read == before) step();
            return base.ReadLine();
        }
    }

    [Fact]
    public void ReplaceN_AtATerminal_AsksAndReplacesOnYes_NoFirst()
    {
        File.WriteAllText(In("work", "r.md"), "draft one\ndraft two\n");
        // Enter alone and n: nothing changed
        var (output, _, _, _) = Run("replace -n r.md draft final\n\nreplace -n r.md draft final\nn\n", terminal: true);
        Assert.Equal("draft one\ndraft two\n", File.ReadAllText(In("work", "r.md")));
        Assert.Contains(Lang.F("{0}: {1} would be replaced, in {2} lines", In("work", "r.md"), 2, 2), output);
        Assert.Contains(Lang.F("Replace them in {0}?", "r.md"), output);
        Assert.Equal(2, output.Split(Lang.T("Nothing was changed")).Length - 1);
        Assert.DoesNotContain("(-n:", output);
        // y: replaced as shown, and undo takes it back
        (output, _, _, _) = Run("replace -n r.md draft final\ny\n", terminal: true);
        Assert.Equal("final one\nfinal two\n", File.ReadAllText(In("work", "r.md")));
        Assert.Contains(Lang.F("{0}: {1} replaced, in {2} lines", In("work", "r.md"), 2, 2), output);
        (output, _, _, _) = Run("replace -n r.md final draft\ny\nundo\n", terminal: true);
        Assert.Equal("final one\nfinal two\n", File.ReadAllText(In("work", "r.md")));
        Assert.Contains(Lang.F("Undone: {0}", "replace -n r.md final draft"), output);
    }

    [Fact]
    public void ReplaceN_FromAFile_NeverAsksNorWrites_NotEvenWithYes()
    {
        File.WriteAllText(In("work", "r.md"), "draft\n");
        var (output, _, _, _) = Run("replace -n r.md draft final\ny\n", yes: true);
        Assert.Equal("draft\n", File.ReadAllText(In("work", "r.md")));
        Assert.Contains(Lang.F("{0}: 1 would be replaced, in line {1} (-n: nothing written)", In("work", "r.md"), 1), output);
        Assert.DoesNotContain(Lang.F("Replace them in {0}?", "r.md"), output);
    }

    [Fact]
    public void ReplaceN_WritesNothing_WhenTheFileWasChangedWhileItAsked()
    {
        File.WriteAllText(In("work", "r.md"), "draft\n");
        var output = new StringWriter();
        var errors = new StringWriter();
        // (the answer is the second line read: the file is changed just before it)
        var input = new ReaderWithStep("replace -n r.md draft final\ny\n", 2,
            () => File.WriteAllText(In("work", "r.md"), "draft, changed\n"));
        Shell.Run(new ShellSession(input, output, errors, In("work"), true));
        Assert.Equal("draft, changed\n", File.ReadAllText(In("work", "r.md")));
        Assert.Contains(Lang.F("{0} was changed meanwhile: nothing written (run replace again)", In("work", "r.md")),
            errors.ToString().Replace("\r\n", "\n"));
    }

    // ---- Review of 1.5.9

    [Fact]
    public void TagRename_ToTheSameName_IsRefused_AndTheFilesKeepTheTag()
    {
        Run("tag add plot.md draft\n");
        var (_, errors, _, _) = Run("tag rename draft Draft\n");
        Assert.Contains(Lang.F("The tag is called {0} already", "draft"), errors);
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(In("work", "plot.md")));
    }

    [Fact]
    public void Tag_TakesNoNameThatStartsWithADash_AndDashLOnlyFirst()
    {
        var (_, errors, _, _) = Run("tag add plot.md -x\n");
        Assert.Contains(Lang.F("Not a name for a tag: {0} (letters, digits, - and _)", "-x"), errors);
        // A file named -l can be given a tag; -l after the tag is not taken for the option
        File.WriteAllText(In("work", "-l"), "x\n");
        (_, errors, _, _) = Run("tag add -l draft\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(new[] { "draft" }, FileTags.TagsOf(In("work", "-l")));
        Assert.Contains(Lang.T("Give one tag: tag draft (tag add notes.md draft gives a file a tag)"), Run("tag draft -l\n").Errors);
        Assert.Equal("tag -l", Tab(Completion(), "tag -"));
        Assert.Equal("tag -l draft", Tab(Completion(), "tag -l d"));
        Assert.Contains("tag -l [name]", Run("help tag\n").Output);
    }

    [UnixFact]
    public void OnLinuxAndMacOS_AFileWithATabInItsName_CannotHaveTags()
    {
        var odd = In("work", "a\tb.md");
        File.WriteAllText(odd, "x\n");
        var (_, errors, _, _) = Run("ls\ntag add 3 draft\n");
        Assert.Contains(Lang.F("{0} has a tab or a line break in its name: it cannot have tags", odd), errors);
        Assert.Empty(FileTags.All());
    }

    [Fact]
    public void GAndGg_TakeTheFilePeekedLast_NotAFileNamedBottomOrTop()
    {
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 60).Select(n => "line " + n + "\n")));
        File.WriteAllText(In("work", "bottom"), "not this\n");
        File.WriteAllText(In("work", "top"), "nor this\n");
        var (output, errors, _, _) = Run("G\npeek long.md +10 5\nG\ngg\n");
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 56, 60, 60), output);
        Assert.Contains(Lang.F("(lines {0}-{1} of {2})", 1, 5, 60), output);
        Assert.DoesNotContain("not this", output);
        Assert.DoesNotContain("nor this", output);
    }

    [Fact]
    public void ReplaceI_CountsOnlyWhatChanges_AndTellsWhenNothingWould()
    {
        File.WriteAllText(In("work", "r.md"), "Draft draft\nDraft\n");
        var (output, _, _, _) = Run("replace -i r.md draft Draft\n");
        Assert.Contains(Lang.F("{0}: 1 replaced, in line {1}", In("work", "r.md"), 1), output);
        Assert.Equal("Draft Draft\nDraft\n", File.ReadAllText(In("work", "r.md")));
        (output, _, _, _) = Run("replace -i r.md draft Draft\n");
        Assert.Contains(Lang.T("Nothing changed: the new text is the same as the old"), output);
        Assert.Equal(("Draft x", 1), ShellSession.ReplacedIn("draft x", "DRAFT", "Draft", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(("Draft x", 0), ShellSession.ReplacedIn("Draft x", "DRAFT", "Draft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReplaceN_AnsweredNo_LeavesFileAndLine_AndALoneDashIsAFile()
    {
        File.WriteAllText(In("work", "r.md"), "draft\n");
        var (_, _, _, session) = Run("peek plot.md\nreplace -n r.md draft final\nn\necho $file\n", terminal: true);
        Assert.Equal(In("work", "plot.md"), session.WorkedFile);
        (_, _, _, session) = Run("peek plot.md\nreplace -n r.md draft final\n");
        Assert.Equal(In("work", "plot.md"), session.WorkedFile);
        // (the options and the file take paths; a lone "-" is the file)
        Assert.Equal(3, ShellSession.ReplaceWords("-n -i r.md a b"));
        Assert.Equal(1, ShellSession.ReplaceWords("- a b"));
    }

    [Fact]
    public void Forget_TellsThatDollarTagStays()
    {
        // (the question, and the note under it, at a terminal; Enter: No)
        var (output, _, _, _) = Run("forget\n\n", terminal: true);
        Assert.Contains("$file and $tag", output);
        Assert.Contains(Lang.T("Nothing forgotten"), output);
    }

    // ---- 1.6.0: short forms in help, rec and res

    [Fact]
    public void Help_EndsWithTheShortForms_AndRecAndResAreRecentAndResume()
    {
        var output = Run("help\n").Output;
        Assert.Contains(Lang.T("Short forms:"), output);
        foreach (var form in new[] { "fav, favs (favorites)", "hist (history)", "rec (recent)", "res (resume)",
                     "hot (hotlist)", "book (bookmarks)", "proj (projects)", "del (delete)", "cls (clear)", "h (help)",
                     "q, quit (exit)" })
        {
            Assert.Contains(form, output);
        }

        // (no line wider than the screen)
        Assert.All(output.TrimEnd('\n').Split('\n'), line => Assert.True(TextMetrics.WidthOf(line) <= 79, line));
        // rec lists what recent lists, res does what resume does
        FileHistory.Parse(string.Empty);
        FileExplorer.AddToHistory(In("work", "plot.md"));
        Assert.Equal(Run("recent\n").Output, Run("rec\n").Output);
        var (_, resume, _, _) = Run("resume\n");
        var (_, res, _, _) = Run("res\n");
        Assert.Equal(resume, res);
        Assert.Contains("recent [-l] [-s] [-f] [-n]", Run("help rec\n").Output);
        Assert.Contains("resume [+line]", Run("help res\n").Output);
        // Accepted, but not offered by Tab
        Assert.DoesNotContain("rec", Commands.Names(CommandContext.Shell));
        Assert.Equal("rec ...", EventLog.CommandName("rec -s"));
    }

    // ---- 1.6.0: searching the tagged files

    // Three tagged files: two with draft, one with ferry
    private void ThreeTaggedFiles()
    {
        File.WriteAllText(In("work", "chapter-a.md"), "The storm came.\nquiet\n");
        File.WriteAllText(In("work", "chapter-b.md"), "calm\n");
        File.WriteAllText(In("work", "notes-c.md"), "x\nA storm, later.\n");
        Run("tag add chapter-a.md draft\ntag add chapter-b.md draft\ntag add notes-c.md ferry\n");
    }

    [Fact]
    public void TagsFilterAndT_SearchTheNamesAndTheLinesOfAllTaggedFiles()
    {
        ThreeTaggedFiles();
        var (output, errors, _, session) = Run("tags filter chapter\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  chapter-a.md  draft\n  2  chapter-b.md  draft\n", output);
        Assert.DoesNotContain("notes-c.md", output);
        // -t: the line found, numbered as in the whole list, and open 3 opens the file at that line
        (output, errors, _, session) = Run("tags -t storm\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  1  chapter-a.md:1  The storm came.\n  3  notes-c.md:2    A storm, later.\n", output);
        Assert.DoesNotContain("chapter-b.md", output);
        Assert.Equal((2, 1), session.LastList[2].At);
        // Both: the name and the line
        (output, _, _, _) = Run("tags filter notes -t storm\n");
        Assert.Contains("  3  notes-c.md:2  A storm, later.\n", output);
        Assert.DoesNotContain("chapter-a.md", output);
        (output, _, _, _) = Run("tags -t nowhere\ntags filter nothing\n");
        Assert.Contains(Lang.F("No file has {0} in a line", "nowhere"), output);
        Assert.Contains(Lang.F("No files with {0} in them", "nothing"), output);
    }

    [Fact]
    public void TagFilterAndT_SearchTheFilesOfATag_AndTheTagIsThenDollarTag()
    {
        ThreeTaggedFiles();
        ShellSession.ForgetTag();
        var (output, errors, _, _) = Run("tag draft filter b\necho $tag\ntag draft -t storm\ntag -t calm\ntag filter a.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("  2  chapter-b.md\n", output);
        Assert.Contains("draft\n", output);
        Assert.Contains("  1  chapter-a.md:1  The storm came.\n", output);
        Assert.Contains("  2  chapter-b.md:1  calm\n", output);
        Assert.Contains("  1  chapter-a.md\n", output);
        // Not the files of another tag
        Assert.DoesNotContain("notes-c.md", output);
        // Full paths with -l
        (output, _, _, _) = Run("tag -l draft -t calm\n");
        Assert.Contains(In("work", "chapter-b.md") + ":1  calm", output);
    }

    [Fact]
    public void TagSearch_TellsWhatItNeeds()
    {
        ThreeTaggedFiles();
        var (_, errors, _, _) = Run("tags -t\ntag draft filter\ntag draft other\ntag bad/name -t x\n");
        Assert.Contains(Lang.T("Give a text after -t: tags -t storm, tag draft -t storm"), errors);
        Assert.Contains(Lang.T("Give a text: tags filter chapter, tag draft filter chapter (or *.md)"), errors);
        Assert.Contains(Lang.T("Give one tag: tag draft (tag add notes.md draft gives a file a tag)"), errors);
        Assert.Contains(Lang.F("Not a name for a tag: {0} (letters, digits, - and _)", "bad/name"), errors);
        ShellSession.ForgetTag();
        (_, errors, _, _) = Run("tag -t storm\n");
        Assert.Contains(Lang.T("No tag yet: tag add notes.md draft, then tag draft"), errors);
        Assert.Equal("tags filter", Tab(Completion(), "tags fi"));
        Assert.Equal("tag draft filter", Tab(Completion(), "tag draft fi"));
        Assert.Equal("tag draft -t", Tab(Completion(), "tag draft -"));
        Assert.Contains("tags filter|-t text", Run("help tags\n").Output);
        Assert.Contains("tag [name] filter|-t text", Run("help tag\n").Output);
    }

    // ---- 1.6.0: help -t

    [Fact]
    public void HelpT_ShowsTheRowsWithTheText_InTheCommandOrWhatItDoes()
    {
        var (output, errors, _, _) = Run("help -t start\n");
        Assert.Equal(string.Empty, errors);
        // In the command, and in what it does
        Assert.Contains("lines file|n lines start|end text", output);
        Assert.Contains("lines file lines unstart|unend text", output);
        Assert.DoesNotContain("  pwd  ", output);
        Assert.DoesNotContain(Lang.T("Short forms:"), output);
        // Letter case alike, and * as in the other searches; the short forms too
        (output, _, _, _) = Run("help -t START\nhelp -t peek*top\nhelp -t favs\n");
        Assert.Contains("lines file|n lines start|end text", output);
        Assert.Contains("peek [f] top|middle|bottom|n%", output);
        Assert.Contains(Lang.T("Short forms:") + "  fav, favs (favorites)", output);
        (output, errors, _, _) = Run("help -t xyzzy\nhelp -t\n");
        Assert.Contains(Lang.F("Nothing in help has {0}", "xyzzy"), output);
        Assert.Contains(Lang.T("Give a text after -t: help -t start"), errors);
        Assert.Contains("help -t text", Run("help help\n").Output);
        Assert.Equal("help -t", Tab(Completion(), "help -"));
    }

    // ---- 1.6.0: cd - and $prev

    [Fact]
    public void CdDash_GoesBackAndForth_AfterAnyCommandThatChangedTheDirectory()
    {
        var (output, errors, _, session) = Run("cd -\ncd Draft\ncd -\necho $prev\ncd -\nup\ncd -\ncd ../plot.md\ncd -\n");
        Assert.Contains(Lang.T("No directory before this one yet: cd - goes back after cd, up, home, hot ..."), errors);
        var lines = output.Split('\n');
        // cd Draft, cd -: back in work, and $prev is Draft; cd - again: Draft
        Assert.Equal(In("work"), lines[0]);
        Assert.Equal(In("work", "Draft"), lines[1]);
        Assert.Equal(In("work", "Draft"), lines[2]);
        // up (from Draft to work), cd -: Draft again
        Assert.Equal(In("work", "Draft"), lines[3]);
        // cd to a file: the directory of the file; cd -: back
        Assert.Equal(In("work", "Draft"), session.Directory);
    }

    [Fact]
    public void CdDash_TellsWhenThePreviousIsGone_AndFollowsARename()
    {
        Directory.CreateDirectory(In("work", "old"));
        var (output, errors, _, _) = Run("cd old\nup\nrename old new\ncd -\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(In("work", "new") + "\n", output);
        // The directory renamed while the shell was in it: the shell goes with it, which is not a move to go back from
        (_, _, _, var session) = Run("cd new\nrename . newer\ncd -\n");
        Assert.Equal(In("work"), session.Directory);
        // Gone
        (_, errors, _, session) = Run("cd newer\nup\ndelete newer\ncd -\n", yes: true);
        Assert.Contains(Lang.F("{0} is not there any more: the shell stays here", In("work", "newer")), errors);
        Assert.Equal(In("work"), session.Directory);
        Assert.Contains("cd -", Run("help cd\n").Output);
    }

    // ---- 1.6.0: the keys of the line, and again edit

    [Fact]
    public void TheLine_CtrlAAndCtrlE_GoToTheStartAndTheEnd_CtrlArrowsAWordAtATime()
    {
        // Ctrl+A, then a letter at the start; Ctrl+E, then one at the end
        Assert.Equal("Xinsert a b Y", ReadLine("insert a b<C-a>X<C-e> Y<CR>").Line);
        // Ctrl+Left twice: before "b"; Ctrl+Right: after "b"
        Assert.Equal("one two_ three", ReadLine("one two three<C-Left><C-Left><C-Right>_<CR>").Line);
        // Option+Left and Option+Right of macOS (Alt+B, Alt+F): the same, and no letter typed
        Assert.Equal("one two_ three", ReadLine("one two three<M-b><M-b><M-f>_<CR>").Line);
        // Blanks before a word are skipped; at the ends nothing moves
        Assert.Equal("[a  b", ReadLine("a  b<C-Left><C-Left><C-Left>[<CR>").Line);
        Assert.Equal("a  b]", ReadLine("a  b<C-Right><C-Right>]<CR>").Line);
    }

    [Fact]
    public void AgainEdit_PutsTheCommandInAFile_ToChangeAndRun_AndTheFileIsDeleted()
    {
        File.WriteAllText(In("work", "e.md"), "start\n");
        var (output, errors, _, _) = Run("insrt e.md bottom it alwyas did\nagain edit\nreplace $cmd insrt insert\n" +
                                         "replace $cmd alwyas always\nrun $cmd\nr\n", terminal: true);
        Assert.Contains("  1  insrt e.md bottom it alwyas did\n", output);
        Assert.Equal("start\nit always did\n", File.ReadAllText(In("work", "e.md")));
        // In the folder "temp" of the data directory, and deleted when the shell is left
        var temp = Path.Combine(Path.GetFullPath(FileIo.GetDataDirectoryName()), "temp");
        Assert.True(System.IO.Directory.Exists(temp));
        Assert.Contains(Path.Combine(temp, "blyant-commands-" + Environment.ProcessId + ".txt"), output);
        Assert.Empty(System.IO.Directory.GetFiles(temp));
        // (the command typed wrong was told, and kept for again)
        Assert.Contains(Lang.F("Unknown command: {0} (help lists the commands)", "insrt"), errors);
    }

    [Fact]
    public void AgainEdit_FilesOfAShellNotLeft_AreDeleted_WhenTheProcessIsNotRunning()
    {
        var temp = Path.Combine(Path.GetFullPath(FileIo.GetDataDirectoryName()), "temp");
        System.IO.Directory.CreateDirectory(temp);
        // A process that is not running (no process has the largest number), and files of other names
        var stopped = Path.Combine(temp, "blyant-commands-" + int.MaxValue + ".txt");
        File.WriteAllText(stopped, "pwd\n");
        File.WriteAllText(Path.Combine(temp, "notes.txt"), "x\n");
        File.WriteAllText(Path.Combine(temp, "blyant-commands-x.txt"), "x\n");
        Run("pwd\n");
        Assert.False(File.Exists(stopped));
        Assert.True(File.Exists(Path.Combine(temp, "notes.txt")));
        Assert.True(File.Exists(Path.Combine(temp, "blyant-commands-x.txt")));
    }

    [Fact]
    public void RunCmd_ShowsTheCommandsAndAsks_RunEditOrCancel_AndRunYRunsAtOnce()
    {
        File.WriteAllText(In("work", "e.md"), "start\n");
        const string typed = "insert e.md bottom one\nagain edit\nundo\n";
        // Enter: run (the first choice); the commands are shown before the question
        var (output, _, _, _) = Run(typed + "run $cmd\n\n", terminal: true);
        Assert.Contains(Lang.T("Run these commands?"), output);
        Assert.Contains("  1  insert e.md bottom one\n", output.Split(Lang.T("Run these commands?"))[^2]);
        Assert.Equal("start\none\n", File.ReadAllText(In("work", "e.md")));
        // Edit: nothing runs, and $cmd is kept to change and run again
        File.WriteAllText(In("work", "e.md"), "start\n");
        (output, _, _, _) = Run(typed + "run $cmd\ne\nreplace $cmd one two\nrun $cmd\nr\n", terminal: true);
        Assert.Contains(Lang.T("Not run: change $cmd, then run $cmd again"), output);
        Assert.Equal("start\ntwo\n", File.ReadAllText(In("work", "e.md")));
        // Cancel: nothing runs, and $cmd is deleted
        File.WriteAllText(In("work", "e.md"), "start\n");
        var errors = string.Empty;
        (output, errors, _, _) = Run(typed + "run $cmd\nc\necho $cmd\n", terminal: true);
        Assert.Contains(Lang.T("Not run: $cmd is deleted (again edit makes a new one)"), output);
        Assert.Contains(Lang.T("No $cmd yet: again edit puts the commands typed in it"), errors);
        Assert.Equal("start\n", File.ReadAllText(In("work", "e.md")));
        // No answer (the end of the input, as Esc): kept, nothing runs
        (output, _, _, _) = Run(typed + "run $cmd\n", terminal: true);
        Assert.Contains(Lang.T("Not run: change $cmd, then run $cmd again"), output);
        Assert.Equal("start\n", File.ReadAllText(In("work", "e.md")));
        // -y: run at once, without the question
        (output, _, _, _) = Run(typed + "run -y $cmd\n", terminal: true);
        Assert.DoesNotContain(Lang.T("Run these commands?"), output);
        Assert.Equal("start\none\n", File.ReadAllText(In("work", "e.md")));
        // Another file of commands is not asked about, also with -y
        File.WriteAllText(In("work", "c.txt"), "pwd\n");
        (output, errors, _, _) = Run("run c.txt\nrun -y c.txt\n", terminal: true);
        Assert.Equal(string.Empty, errors);
        Assert.DoesNotContain(Lang.T("Run these commands?"), output);
        Assert.Contains("run [-y] $cmd", Run("help run\n").Output);
        Assert.Equal("run -y", Tab(Completion(), "run -"));
    }

    // ---- 1.6.0: the findings of the review before the release

    // The folder of the file of "again edit"
    private static string CommandsTemp() => Path.Combine(Path.GetFullPath(FileIo.GetDataDirectoryName()), "temp");

    [Fact]
    public void Undo_GoesOnAfterCmd_WasCancelledOrWrittenAnew_AndCmdLeavesRedo()
    {
        File.WriteAllText(In("work", "e.md"), "start\n");
        // A change of $cmd, then Cancel: undo takes back the insert before it (the $cmd step is gone with the file)
        var (_, errors, _, _) = Run("insert e.md bottom one\nagain edit\nreplace $cmd one two\nrun $cmd\nc\nundo\n",
            terminal: true);
        Assert.Equal("start\n", File.ReadAllText(In("work", "e.md")));
        Assert.DoesNotContain("blyant-commands-", errors);
        // A second again edit writes it anew: the same
        File.WriteAllText(In("work", "e.md"), "start\n");
        (_, errors, _, _) = Run("insert e.md bottom one\nagain edit\nreplace $cmd one two\npwd\nagain edit\nundo\n",
            terminal: true);
        Assert.Equal("start\n", File.ReadAllText(In("work", "e.md")));
        Assert.DoesNotContain("blyant-commands-", errors);
        // Deleted with delete: for good (not in the trash), and the same
        File.WriteAllText(In("work", "e.md"), "start\n");
        (_, errors, _, _) = Run("insert e.md bottom one\nagain edit\nreplace $cmd one two\ndelete $cmd\n" +
                                DialogText.ChoiceYes + "\nundo\n",
            terminal: true);
        Assert.Equal("start\n", File.ReadAllText(In("work", "e.md")));
        Assert.DoesNotContain("blyant-commands-", errors);
        Assert.Empty(Directory.GetFiles(CommandsTemp()));
        Assert.False(Directory.Exists(FileOperations.TrashDirectory) &&
                     Directory.EnumerateFiles(FileOperations.TrashDirectory, "blyant-commands-*", SearchOption.AllDirectories).Any());
        // A change of $cmd leaves the redo of the files worked on
        File.WriteAllText(In("work", "e.md"), "start\n");
        Run("insert e.md bottom one\nundo\nagain edit\nreplace $cmd one two\nredo\n", terminal: true);
        Assert.Equal("start\none\n", File.ReadAllText(In("work", "e.md")));
    }

    [Fact]
    public void CommandsFiles_LeftBehind_AreDeleted_WhenNoBlyantRunsWithTheNumber_AndAreNotCmd()
    {
        var temp = CommandsTemp();
        Directory.CreateDirectory(temp);
        // A number of the system (4 on Windows, 1 on Linux and macOS): a process that is not a Blyant, which a user
        // may not open; and a temporary file of a save that was cut off
        var system = Path.Combine(temp, "blyant-commands-" + (OperatingSystem.IsWindows() ? 4 : 1) + ".txt");
        var cut = Path.Combine(temp, "blyant-commands-" + int.MaxValue + ".txt.1a2b3c4d.tmp");
        File.WriteAllText(system, "pwd\n");
        File.WriteAllText(cut, "pwd\n");
        // A file with the number of this process, left by a Blyant before: not $cmd
        File.WriteAllText(Path.Combine(temp, "blyant-commands-" + Environment.ProcessId + ".txt"), "pwd\n");
        var (_, errors, _, _) = Run("echo $cmd\n");
        Assert.Contains(Lang.T("No $cmd yet: again edit puts the commands typed in it"), errors);
        Assert.Empty(Directory.GetFiles(temp));
    }

    [Fact]
    public void OpenAndPeekCmd_LeaveFileThePeekAndTheLatestFiles()
    {
        File.WriteAllText(In("work", "e.md"), "one\ntwo\nthree\n");
        FileHistory.Parse(string.Empty);
        var (output, errors, _, session) = Run("peek e.md 2\nchnage e.md +2 TWO\nagain edit\nopen $cmd\npeek $cmd\n" +
                                               "echo $peek\n.\n", terminal: true);
        Assert.Contains(Lang.T("$cmd is changed in the shell: replace $cmd old new, word $cmd 1 3 replace new"), errors);
        Assert.Equal(In("work", "e.md"), session.WorkedFile);
        // $peek and "." are still those of e.md: its lines, and its window again (1-2, as peek e.md 2 showed it)
        Assert.Contains("> one\ntwo\n", output);
        Assert.Equal(2, output.Split("  1  one\n  2  two\n").Length - 1);
        FileHistory.Load();
        Assert.DoesNotContain(FileHistory.Items, item => item.Path.Contains("blyant-commands-"));
    }

    [Fact]
    public void Tag_FilterIsNotANewName_AndATagNamedFilterBeforeIsStillListed()
    {
        File.WriteAllText(In("work", "plot.md"), "x\n");
        var (_, errors, _, _) = Run("tag add plot.md filter\ntag add plot.md draft\ntag rename draft Filter\n");
        Assert.Equal(2, errors.Split(Lang.F("{0} is a word of the tag command: give the tag another name", "filter"))
            .Length - 1);
        Assert.DoesNotContain(FileTags.All(), pair => pair.Tag == "filter");
        // A tag named filter, given before: tag filter lists it, and it can be renamed
        FileTags.Parse(In("work", "plot.md") + "\tfilter\n");
        FileTags.Save();
        var (output, _, _, _) = Run("tag filter\ntag rename filter old\n");
        Assert.Contains("plot.md", output);
        Assert.Contains(FileTags.All(), pair => pair.Tag == "old");
        // tags filter alone is still a search without a text
        Assert.Contains(Lang.T("Give a text: tags filter chapter, tag draft filter chapter (or *.md)"),
            Run("tags filter\n").Errors);
    }

    [Fact]
    public void Rec_PutsItsListInARegister_AndTabCompletesItsOptions()
    {
        File.WriteAllText(In("work", "plot.md"), "x\n");
        FileHistory.Parse(string.Empty);
        FileHistory.Add(In("work", "plot.md"), DateTime.UtcNow);
        FileHistory.Save();
        var (_, errors, _, _) = Run("rec > $x\necho $x\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("plot.md", ShellRegisters.Get("x"));
        Assert.Equal("rec -l", Tab(Completion(), "rec -"));
    }

    [Fact]
    public void Prev_BeforeAnyCd_IsTold_AndCdDashAfterARunThatRenamedTheDirectory()
    {
        Assert.Contains(Lang.T("No directory before this one yet: cd, up, home ... give $prev"), Run("ls $prev\n").Errors);
        // In d: a run renames it and runs on; the directory before stays work, not the old name of d
        Directory.CreateDirectory(In("work", "d"));
        File.WriteAllText(In("work", "s.txt"), "rename . d2\npwd\n");
        var (output, errors, _, session) = Run("cd d\nrun ../s.txt\necho $prev\ncd -\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(In("work") + "\n", output.Split("echo $prev")[^1]);
        Assert.Equal(In("work"), session.Directory);
    }

    [Fact]
    public void PeekAll_OfAnEmptyFile_AndRunY_WithWordsAndInQuotes()
    {
        File.WriteAllText(In("work", "empty.md"), string.Empty);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "empty.md"), 0), Run("peek empty.md all\n").Output);
        // run -y $cmd with a word: a command of one line gets it at its end; "-y" in quotes is -y too
        var (output, errors, _, _) = Run("echo a\nagain edit\nrun -y $cmd b\nrun \"-y\" $cmd c\n", terminal: true);
        Assert.Equal(string.Empty, errors);
        Assert.Contains("a b\n", output);
        Assert.Contains("a c\n", output);
    }

    [Fact]
    public void ShortForms_AreInTheTableOfTheShell_AndHelpTellsTheirCommands()
    {
        var all = Commands.All(CommandContext.Shell);
        foreach (var (shortForm, command) in ShellSession.ShortFormsOf)
        {
            Assert.Contains(shortForm, all);
            Assert.Contains(command, Commands.Names(CommandContext.Shell));
            Assert.Equal(Run("help " + command + "\n").Output, Run("help " + shortForm + "\n").Output);
        }
    }

    [Fact]
    public void AgainEdit_TakesSeveralCommands_AndIsRefusedInAFile()
    {
        File.WriteAllText(In("work", "e.md"), "x\n");
        var (output, _, _, session) = Run("insert e.md bottom a\ninsert e.md bottom b\nagain edit 2-1\n", terminal: true);
        // The oldest first, a command on each line; $file stays the file worked on
        Assert.Contains("  1  insert e.md bottom a\n  2  insert e.md bottom b\n", output);
        Assert.Equal(In("work", "e.md"), session.WorkedFile);
        var (_, errors, _, _) = Run("again edit\n");
        Assert.Contains(Lang.T("again edit is for the commands typed at the prompt: not in a run, a file or --run"), errors);
        (_, errors, _, _) = Run("pwd\nagain edit x\nagain edit 9\n", terminal: true);
        Assert.Contains(Lang.T("Give the numbers of the commands: again edit, again edit 3, again edit 3-5"), errors);
        Assert.Contains("again edit [n|n-m]", Run("help again\n").Output);
        Assert.Equal("again edit", Tab(Completion(), "again e"));
    }

    [Fact]
    public void Cmd_IsTheFileOfAgainEdit_AndCommandsOnItLeaveFileLineAndTheLastList()
    {
        File.WriteAllText(In("work", "e.md"), "one\ntwo\nthree\n");
        FileHistory.Parse(string.Empty);
        // Working on e.md at line 2; then a command typed wrong, fixed in $cmd
        var (output, errors, _, session) = Run(
            "peek e.md 2\nchnage e.md +2 TWO\nagain edit\necho $cmd\nreplace $cmd chnage change\nwords $cmd 1\n" +
            "peek $cmd\necho $file $line\nundo\nreplace $cmd chnage change\nrun -y $cmd\necho $file $line\n", terminal: true);
        Assert.Contains("blyant-commands-", output);
        // While $cmd is changed: $file and $line are still e.md at line 2
        Assert.Contains(In("work", "e.md") + " 2\n", output);
        // run $cmd runs the command, and that command works on e.md as typed
        Assert.Equal("one\nTWO\nthree\n", File.ReadAllText(In("work", "e.md")));
        Assert.Equal(In("work", "e.md"), session.WorkedFile);
        // Not in the latest files
        FileHistory.Load();
        Assert.DoesNotContain(FileHistory.Items, item => item.Path.Contains("blyant-commands-"));
        // After the shell: no $cmd
        Assert.Contains(Lang.T("No $cmd yet: again edit puts the commands typed in it"), Run("echo $cmd\n").Errors);
    }

    // ---- 1.6.1: a quote in a text, and lines remove

    [Theory]
    [InlineData("replace \"\"\"\" \"\"", new[] { "replace", "\"", "" })]
    [InlineData("\"say \"\"hi\"\"\"", new[] { "say \"hi\"" })]
    [InlineData("\"a\"\"b\" c", new[] { "a\"b", "c" })]
    [InlineData("\"\"\"lines\" lines", new[] { "\"lines", "lines" })]
    [InlineData("rename \"my notes.md\" new.md", new[] { "rename", "my notes.md", "new.md" })]
    [InlineData("\"\" \"\"", new[] { "", "" })]
    public void Words_TakeTwoQuotesInQuotes_AsOneQuote(string text, string[] expected)
    {
        Assert.Equal(expected, ShellSession.Words(text));
    }

    [Theory]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"say \"\"hi\"\"\"", "say \"hi\"")]
    [InlineData("\"- [ ] \"", "- [ ] ")]
    [InlineData("\"\"", "")]
    [InlineData("\"a\" \"b\"", null)]
    [InlineData("\"a\"b\"", null)]
    [InlineData("\"a\"\"", null)]
    [InlineData("\"", null)]
    [InlineData("plain", null)]
    public void Unquoted_IsTheTextInOnePairOfQuotes_WithTwoQuotesAsOne(string text, string? expected)
    {
        Assert.Equal(expected, ShellSession.Unquoted(text));
    }

    [Theory]
    [InlineData("lines a.md 1 remove \"say \"\"hi", "lines a.md 1 remove ", "\"say \"\"hi")]
    [InlineData("lines a.md 1 remove \"\"\"\" x", "lines a.md 1 remove \"\"\"\" ", "x")]
    public void LastWord_GoesOnOverTwoQuotesInQuotes(string text, string before, string word)
    {
        Assert.Equal((before, word), ShellCompletion.LastWord(text));
    }

    [Theory]
    [InlineData("a draft, the draft", "draft", "a , the ")]
    [InlineData("\"quoted\" text", "\"", "quoted text")]
    [InlineData("nothing here", "x", "nothing here")]
    public void EditedLine_Remove_TakesTheTextOutWhereverItIs(string line, string text, string expected)
    {
        Assert.Equal(expected, ShellSession.EditedLine(line, "remove", text, false));
    }

    [Fact]
    public void Lines_ReplaceAndRemove_TakeAQuoteWrittenAsTwoInQuotes()
    {
        File.WriteAllText(In("work", "q.md"),
            " * \"lines er en meget powerfull command\"\n\"one\" two\n\"three\"\nsay hi\n");
        var (output, errors, _, _) = Run("lines q.md 1 replace \"\"\"\" \"\"\nlines q.md 2 remove \"\"\"\"\n" +
                                         "lines q.md 3 remove \"\nlines q.md 4 replace hi \"\"\"hi\"\"\"\n" +
                                         "lines q.md 4 end \"\"\"!\"\"\"\nlines q.md 1 remove x\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(" * lines er en meget powerfull command\none two\nthree\nsay \"hi\"\"!\"\n", Text("q.md"));
        Assert.Contains(Lang.F("Nothing changed: no line has {0} in it", "x"), output);
    }

    [Fact]
    public void Lines_Remove_WithBlanks_AndWithoutAText()
    {
        File.WriteAllText(In("work", "r.md"), "Write chapter 3 (draft)\nCall Anna (draft)\n");
        var (_, errors, _, _) = Run("lines r.md 1-2 remove \" (draft)\"\nlines r.md 1 remove\n");
        Assert.Contains(Lang.F("Give the text: lines todo.md 4 {0} Remember this:", "remove"), errors);
        Assert.Equal("Write chapter 3\nCall Anna\n", Text("r.md"));
        Assert.Equal("lines r.md 1 remove", Tab(Completion(), "lines r.md 1 rem"));
        Assert.Contains("lines file|n lines remove text", Run("help lines\n").Output);
    }

    [Fact]
    public void ReplaceAndWord_TakeAQuoteWrittenAsTwoInQuotes()
    {
        File.WriteAllText(In("work", "w.md"), "\"Hi\" she said\n");
        var (_, errors, _, _) = Run("replace w.md \"\"\"\" '\nword w.md 1 1 replace \"\"\"Hello\"\"\"\n");
        Assert.Equal(string.Empty, errors);
        // (word 1 is Hi: the ' made of the quotes stay around it)
        Assert.Equal("'\"Hello\"' she said\n", Text("w.md"));
    }

    [Fact]
    public void Line_WithAFileOrAWordOfLines_PointsToLines_AndDoesNothing()
    {
        File.WriteAllText(In("work", "t.md"), "one\ntwo\nthree\nfour text1\n");
        var hint = Lang.T("line only sets $line: to change lines, use lines (lines notes.md +4 replace old new)");
        var (output, errors, _, _) = Run("peek t.md 2\nline $file +4 replace \"text1\" \"text2\"\nline 2 end x\n" +
                                         "line t.md 4\nline 3 > other.md bottom\necho $line\n");
        // ("line t.md 4": a file and a line, as file takes them)
        Assert.Equal(3, errors.Split(hint).Length - 1);
        Assert.Contains(Lang.T("line sets only the line of $file: file notes.md 3 sets the file and the line"), errors);
        Assert.Equal("one\ntwo\nthree\nfour text1\n", Text("t.md"));
        Assert.False(File.Exists(In("work", "other.md")));
        Assert.EndsWith("\n1\n", output);
        // lines does it, and $line is the line changed
        (output, errors, _, _) = Run("lines t.md +4 replace \"text1\" \"text2\"\necho $line\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\ntwo\nthree\nfour text2\n", Text("t.md"));
        Assert.EndsWith("\n4\n", output);
    }

    // ---- 1.6.1: commands a letter or a word apart point to the one meant

    [Theory]
    [InlineData("replace three 3", true)]
    [InlineData("end (by Friday)", true)]
    [InlineData("  start - [ ] ", true)]
    [InlineData("End of chapter", false)]
    [InlineData("end", false)]
    [InlineData("the end of it", false)]
    [InlineData("sort -d", false)]
    public void LinesMeantByChange_IsAWordOfLinesWithATextAfterIt(string text, bool meant)
    {
        Assert.Equal(meant, ShellSession.LinesMeantByChange(text));
    }

    [Fact]
    public void Change_WithAWordOfLines_ChangesTheLine_AndTellsOfLines()
    {
        File.WriteAllText(In("work", "c.md"), "one\ntwo\nthree\n");
        var note = Lang.T("change puts the text in place of the whole line: lines changes a text in it (undo takes it back)");
        var (output, errors, _, _) = Run("change c.md 2 replace two 2\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(note, output);
        Assert.Equal("one\nreplace two 2\nthree\n", Text("c.md"));
        (output, _, _, _) = Run("change c.md 2 End of chapter\n");
        Assert.DoesNotContain(note, output);
    }

    [Fact]
    public void CommandsALetterApart_PointToTheOneMeant_AndDoNothing()
    {
        File.WriteAllText(In("work", "h.md"), "one two\nthree four\n");
        var (_, errors, _, _) = Run("words h.md 2 1 replace x\nword h.md 2\nreplace h.md 2 three 3\n" +
                                    "replace h.md 1-2,4 three 3\ntags draft\ntag h.md draft\n");
        Assert.Contains(Lang.T("To change words: word notes.md 12 9 replace wish (words only shows them)"), errors);
        Assert.Contains(Lang.T("To see the words of a line: words notes.md 12 (word changes them)"), errors);
        Assert.Equal(2, errors.Split(Lang.T("replace works on the whole file: lines notes.md 2-5 replace old new works on lines")).Length - 1);
        Assert.Contains(Lang.F("tag {0} lists the files with the tag (tags lists the tags)", "draft"), errors);
        Assert.Contains(Lang.F("To give a file a tag: tag add {0} {1}", "h.md", "draft"), errors);
        Assert.Equal("one two\nthree four\n", Text("h.md"));
        FileTags.Load();
        Assert.Empty(FileTags.Tagged());
        // The other wrong forms keep the messages they had
        (_, errors, _, _) = Run("words h.md\nword h.md 2 1\nreplace h.md x\ntags a b\n");
        Assert.Contains(Lang.T("Give a file and a line: words notes.md 12"), errors);
        Assert.Contains(Lang.T("Give a file, a line, the words and what to do: word notes.md 12 9 replace wish"), errors);
        Assert.Contains(Lang.T("Give the file, the text and what replaces it: replace notes.md draft final"), errors);
        Assert.Contains(Lang.T("tags lists the tags, tags -f the files with tags, and tags clear takes them all off"), errors);
        // A text that is a number is replaced as before (three words)
        (_, errors, _, _) = Run("replace h.md 2 x\n");
        Assert.Equal(string.Empty, errors);
    }

    // ---- 1.6.1: the commands typed kept in shellhistory.txt

    [Fact]
    public void ShellHistory_Newest_KeepsEachCommandOnce_WhereItWasLast_AndTheNewest()
    {
        Assert.Equal(new[] { "b", "a", "c" }, ShellHistory.Newest(new[] { "a", "b", "", "a", " c " }, 5));
        Assert.Equal(new[] { "a", "c" }, ShellHistory.Newest(new[] { "a", "b", "a", "c" }, 2));
        Assert.Empty(ShellHistory.Newest(Array.Empty<string>(), 3));
    }

    [Theory]
    [InlineData("50", 50)]
    [InlineData("0", 0)]
    [InlineData("200", 200)]
    [InlineData("201", 50)]
    [InlineData("-1", 50)]
    [InlineData("x", 50)]
    [InlineData("", 50)]
    public void ShellHistory_TheSettingIs0To200_Else50(string value, int expected)
    {
        Assert.Equal(expected, Shell.ParseHistoryCount(value));
    }

    [Fact]
    public void ShellHistory_CommandsTyped_AreKeptForTheNextShell_ButNotOnesWithABlankInFront()
    {
        CommandHistory.Clear();
        try
        {
            Run("pwd\n echo private\nls\n", terminal: true);
            Assert.Equal("pwd\nls\n", File.ReadAllText(ShellHistory.FilePath));
            Assert.DoesNotContain("echo private", CommandHistory.Of(CommandContext.Shell));
            // A new Blyant: the commands come from the file
            CommandHistory.Clear();
            ShellHistory.Reset();
            var (output, _, _, _) = Run("again\n", terminal: true);
            Assert.Contains("1  ls\n", output);
            Assert.Contains("2  pwd\n", output);
            // From a file or a pipe: neither read nor added to
            Run("version\n");
            Assert.Equal("pwd\nls\n", File.ReadAllText(ShellHistory.FilePath));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void ShellHistory_IsWrittenAgainWithTheNewest_WhenItHasTooMany()
    {
        CommandHistory.Clear();
        try
        {
            // Read with more than kept: the newest 50, each once
            File.WriteAllText(ShellHistory.FilePath,
                string.Concat(Enumerable.Range(1, 80).Select(i => "echo " + i + "\n")) + "echo 3\n");
            Run("pwd\n", terminal: true);
            var lines = File.ReadAllLines(ShellHistory.FilePath);
            Assert.Equal(Shell.DefaultHistoryCount + 1, lines.Length);
            Assert.Equal(new[] { "echo 80", "echo 3", "pwd" }, lines[^3..]);
            Assert.Equal(Shell.DefaultHistoryCount, CommandHistory.Of(CommandContext.Shell).Count);
            Assert.Equal("pwd", CommandHistory.Of(CommandContext.Shell)[0]);
            // Grown to twice as many lines as kept: written again with the newest
            Shell.HistoryCount = 3;
            Run("echo a\necho b\necho c\necho d\necho e\necho f\n", terminal: true);
            Assert.True(File.ReadAllLines(ShellHistory.FilePath).Length <= 5);
            Assert.Equal(new[] { "echo d", "echo e", "echo f" }, File.ReadAllLines(ShellHistory.FilePath)[^3..]);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void ShellHistory_Zero_KeepsNoFile_AndForgetDeletesIt_AndItIsProtected()
    {
        CommandHistory.Clear();
        try
        {
            Run("pwd\n", terminal: true);
            Assert.True(File.Exists(ShellHistory.FilePath));
            // A file of Blyant: not deleted from the shell
            var (_, errors, _, _) = Run("delete " + ShellHistory.FilePath + "\n", yes: true);
            Assert.Contains(DialogText.FileUsedByBlyant, errors);
            // forget deletes it
            var (output, _, _, _) = Run("forget\n", yes: true);
            Assert.Contains(Lang.T("Forgotten: the commands typed, $clip, $peek, $calc, the last search and the undo steps"), output);
            Assert.False(File.Exists(ShellHistory.FilePath));
            // 0: the commands are kept while Blyant runs, not in the file
            Shell.HistoryCount = 0;
            (output, _, _, _) = Run("again\nls\nagain\n", terminal: true);
            Assert.Contains(Lang.T("No commands typed yet (they are kept while Blyant runs)"), output);
            Assert.Contains("1  ls\n", output);
            Assert.False(File.Exists(ShellHistory.FilePath));
            Shell.HistoryCount = Shell.DefaultHistoryCount;
            CommandHistory.Clear();
            (output, _, _, _) = Run("again\n", terminal: true);
            Assert.Contains(Lang.T("No commands typed yet"), output);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    // ---- 1.6.1: lines at a column, and the columns of words

    [Theory]
    [InlineData("Blyant v1", 1, 0)]
    [InlineData("Blyant v1", 8, 7)]
    [InlineData("Blyant v1", 10, 9)]
    [InlineData("Blyant v1", 11, null)]
    [InlineData("", 1, 0)]
    [InlineData("", 2, null)]
    [InlineData("a", 0, null)]
    // (a wide character is two columns: column 3 is after it, column 2 in the middle of it)
    [InlineData("a\u4e2db", 2, 1)]
    [InlineData("a\u4e2db", 3, null)]
    [InlineData("a\u4e2db", 4, 2)]
    public void IndexOfColumn_IsThePlaceOfAColumnAsTheEditorCountsThem(string line, int column, int? index)
    {
        Assert.Equal(index, LineWords.IndexOfColumn(line, column));
        if (index is { } at) Assert.Equal(column, LineWords.ColumnOf(line, at));
    }

    [Theory]
    [InlineData("Some text", "at", "new ", 1, "new Some text")]
    [InlineData("Some text", "at", ",", 5, "Some, text")]
    [InlineData("Some text", "at", "!", 10, "Some text!")]
    [InlineData("Some text", "at", "!", 11, "Some text")]
    public void EditedLine_At_PutsTheTextInBeforeTheColumn(string line, string kind, string text, int column,
        string expected)
    {
        Assert.Equal(expected, ShellSession.EditedLine(line, kind, text, true, column: column));
    }

    [Fact]
    public void LinesAt_PutsATextInAtAColumn_AsWordsShowsIt_InEveryLineOrNone()
    {
        File.WriteAllText(In("work", "a.md"), "Blyant v1 5 8 Shell\nBlyant v2 6 9 Shell\nshort\n");
        var (output, errors, _, _) = Run("words a.md 1\nlines a.md 1-2 at 8 \"version \"\necho $line\n" +
                                         "lines a.md 1 at 1 \"# \"\nlines a.md +3 at 6 \"\"\"\"\n");
        Assert.Equal(string.Empty, errors);
        // words shows the column each word starts at
        Assert.Contains("#2:8 v1", output);
        Assert.Equal("# Blyant version v1 5 8 Shell\nBlyant version v2 6 9 Shell\nshort\"\n", Text("a.md"));
        Assert.Contains("\n1\n", output);
        // A line without the column: none is changed
        (_, errors, _, _) = Run("lines a.md 1-3 at 9 x\nlines a.md 1 at 0 x\nlines a.md 1 at x y\nlines a.md 1 at 3\n");
        Assert.Contains(Lang.F("Line {0} has no column {1}: nothing changed", 3, 9), errors);
        Assert.Contains(Lang.F("Not a column: {0} (as 10, the column words shows)", "0"), errors);
        Assert.Contains(Lang.F("Not a column: {0} (as 10, the column words shows)", "x"), errors);
        Assert.Contains(Lang.T("Give the column and the text: lines notes.md 34 at 10 text"), errors);
        Assert.Equal("# Blyant version v1 5 8 Shell\nBlyant version v2 6 9 Shell\nshort\"\n", Text("a.md"));
        Assert.Equal("lines a.md 1 at", Tab(Completion(), "lines a.md 1 a"));
        Assert.Contains("lines file|n lines at column text", Run("help lines\n").Output);
    }

    [Fact]
    public void Word_TakesTheNumbersWithTheColumns_AsWordsShowsThem()
    {
        Assert.True(ShellSession.WordNumbers("#2:8", out var two, out _, out var columns) && two == 2);
        Assert.Equal(new[] { (2, 8) }, columns);
        Assert.True(ShellSession.WordNumbers("#5:20-#7:31", out var five, out var seven, out columns) && (five, seven) == (5, 7));
        Assert.Equal(new[] { (5, 20), (7, 31) }, columns);
        File.WriteAllText(In("work", "v.md"), "Blyant v1 5 8 Shell\n");
        var (_, errors, _, _) = Run("word v.md 1 #2:8 replace v2\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("Blyant v2 5 8 Shell\n", Text("v.md"));
    }

    // ---- 1.6.1: clip words

    [Fact]
    public void ClipWords_CopiesWordsAsTheyAreInTheLine_AndChangesNothing()
    {
        File.WriteAllText(In("work", "c.md"), "# Notes\n" + WordLine + "\n");
        File.WriteAllText(In("work", "other.md"), "one\n");
        var (output, errors, _, session) = Run("peek other.md\nclip words c.md 2 6-9\necho $clip\nclip words c.md 2 #9:32\n" +
                                               "echo $clip\nclip words -p c.md 2 6\necho $clip\necho $file\n");
        Assert.Equal(string.Empty, errors);
        // From the start of the first word to the end of the last, what is between them too
        Assert.Contains("\ntext, that I want\n", output);
        Assert.Contains("\nwant\n", output);
        // -p: a word is all between blanks, the comma too
        Assert.Contains("\ntext,\n", output);
        Assert.Contains(Lang.F("Words {0}-{1} of line {2} of {3}", 6, 9, 2, In("work", "c.md")), output);
        // The file is only read: $file is still the file peeked
        Assert.EndsWith(In("work", "other.md") + "\n", output);
        Assert.Equal(In("work", "other.md"), session.WorkedFile);
        // Put in elsewhere, at a column and after a word
        (_, errors, _, _) = Run("clip words c.md 2 14-15\nlines other.md 1 at 4 \" $clip\"\nword other.md 1 1 before $clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("few commands one few commands\n", Text("other.md"));
    }

    [Fact]
    public void ClipWords_WithAt_TakesTheFileAndTheLineWorkedOn_AndTellsWhatItCannotDo()
    {
        File.WriteAllText(In("work", "c.md"), "# Notes\n" + WordLine + "\n\n");
        var (output, errors, _, _) = Run("words c.md 2\nclip words @ 1-2\necho $clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("\nThis is\n", output);
        (_, errors, _, _) = Run("clip words c.md 2\nclip words c.md 2 x\nclip words c.md 2 99\nclip words c.md 3 1\n" +
                                "clip words c.md 9 1\nclip words -q c.md 2 1\n");
        Assert.Contains(Lang.T("Give a file, a line and the words: clip words notes.md 12 6-9"), errors);
        Assert.Contains(Lang.F("Not words: {0} (as 9, or 5-7)", "x"), errors);
        Assert.Contains(Lang.F("Line {0} has {1} words: {2} is not one", 2, 15, 99), errors);
        Assert.Contains(Lang.F("Line {0} has no words", 3), errors);
        Assert.Contains(Lang.F("{0} has {1} lines", In("work", "c.md"), 3), errors);
        Assert.Contains(Lang.F("Not taken: {0} (-p: words between blanks, with punctuation)", "-q"), errors);
        Assert.Equal("clip words", Tab(Completion(), "clip wo"));
        Assert.Contains("clip words [-p] file|n line n|n-m", Run("help clip\n").Output);
    }

    // ---- 1.6.1: word ... move

    [Theory]
    [InlineData("This is a line of text, that I want to change", 9, 9, true, 6, false,
        "This is a line of want text, that I to change")]
    [InlineData("This is a line of text, that I want to change", 6, 7, false, 11, false,
        "This is a line of I want to change text, that")]
    [InlineData("Keep **bold** words here", 2, 2, false, 4, false, "Keep words here **bold**")]
    [InlineData("one two three", 3, 3, true, 1, false, "three one two")]
    [InlineData("one two three", 1, 1, false, 3, false, "two three one")]
    [InlineData("a text, here", 2, 2, true, 1, true, "text, a here")]
    [InlineData("one two three", 2, 3, true, 2, false, null)]
    public void LineWords_Moved_PutsTheWordsBeforeOrAfterAWord_NumberedAsBefore(string line, int first, int last,
        bool before, int target, bool blanks, string? expected)
    {
        Assert.Equal(expected, LineWords.Moved(line, first, last, before, target, blanks));
    }

    [Fact]
    public void WordMove_MovesWordsInTheLine_ByTheNumbersShown_AndUndoTakesItBack()
    {
        File.WriteAllText(In("work", "m.md"), "# Notes\n" + WordLine + "\n");
        var (output, errors, _, _) = Run("words m.md 2\nword @ 9 move before #6:19\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Notes\nThis is a line of want text, that I to change, with a few commands...\n", Text("m.md"));
        // The line shown with the words numbered again
        Assert.Contains("#6:19 want  #7:24 text", output);
        File.WriteAllText(In("work", "m.md"), "# Notes\n" + WordLine + "\n");
        (_, errors, _, _) = Run("word m.md 2 6-7 move after 11\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Notes\nThis is a line of I want to change text, that, with a few commands...\n", Text("m.md"));
        // undo in the same shell
        (_, errors, _, _) = Run("word m.md 2 1 move after 2\nundo\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Notes\nThis is a line of I want to change text, that, with a few commands...\n", Text("m.md"));
    }

    [Fact]
    public void WordMove_TellsWhatItCannotDo_AndChangesNothing()
    {
        File.WriteAllText(In("work", "m.md"), "one two three\n");
        var (output, errors, _, _) = Run("word m.md 1 2-3 move before 2\nword m.md 1 2 move before 9\n" +
                                         "word m.md 1 2 move\nword m.md 1 2 move under 1\nword m.md 1 2 move before x\n" +
                                         "word m.md 1 2 move after 1 2\nword m.md 1 2 move after 1\n");
        Assert.Contains(Lang.F("Word {0} is one of the words moved: give a word outside them", 2), errors);
        Assert.Contains(Lang.F("Line {0} has {1} words: {2} is not one", 1, 3, 9), errors);
        Assert.Equal(4, errors.Split(Lang.T("Give where to: word notes.md 12 9 move before 6 (or after 6)")).Length - 1);
        // Where they are already: nothing changed
        Assert.Contains(Lang.T("Nothing changed: the words are so already"), output);
        Assert.Equal("one two three\n", Text("m.md"));
        Assert.Equal("word m.md 1 2 move", Tab(Completion(), "word m.md 1 2 mo"));
        Assert.Equal("word m.md 1 2 move before", Tab(Completion(), "word m.md 1 2 move be"));
        Assert.Contains("word ... move before|after n", Run("help word\n").Output);
    }

    // ---- 1.6.1: saved peeks, and the last peek kept for the next shell

    private void FortyLines() =>
        File.WriteAllText(In("work", "long.md"), string.Concat(Enumerable.Range(1, 40).Select(i => "line " + i + "\n")));

    [Fact]
    public void PeekMarks_Parse_ReadsTheWindows_AndLeavesOutLinesItCannotRead()
    {
        var path = In("work", "long.md");
        PeekMarks.Parse("\t10\t5\t" + path + "\nintro\t1\t15\t" + path + "\n*\t20\t3\t" + path + "\nbad\tx\t5\t" + path +
                        "\n2nd\t1\t1\t" + path + "\nrel\t1\t1\tnotes.md\n");
        Assert.Equal(new[] { "", "intro" }, PeekMarks.Saved.Select(entry => entry.Name));
        Assert.Equal(new PeekMarks.Window(path, 10, 5), PeekMarks.Saved[0].Window);
        Assert.Equal(new PeekMarks.Window(path, 20, 3), PeekMarks.Last);
        Assert.True(PeekMarks.IsName("intro-2"));
        Assert.False(PeekMarks.IsName("2nd"));
        Assert.False(PeekMarks.IsName("a b"));
    }

    [Fact]
    public void PeekSaveAndBack_KeepAWindow_AndShowItAgainAsTheFileIsNow()
    {
        FortyLines();
        var (output, errors, _, _) = Run("peek long.md +10 5\npeek save\npeek long.md +30 3\npeek save end\n" +
                                         "peek long.md bottom\npeek back\n+\npeek back end\npeek back 1\npeek saved\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.F("Saved: lines {0} of {1} (peek back)", "10-14", In("work", "long.md")), output);
        Assert.Contains(Lang.F("Saved as {0}: lines {1} of {2} (peek back {0})", "end", "30-32", In("work", "long.md")), output);
        // peek back: the window kept; it is the last peek then, so + goes on from it
        Assert.Contains("  15  line 15\n", output);
        Assert.Contains("  32  line 32\n", output);
        // peek saved: numbered, the name ("-" without one), the lines and the file
        Assert.Contains("1  -    10-14  " + In("work", "long.md"), output);
        Assert.Contains("2  end  30-32  " + In("work", "long.md"), output);
        Assert.Equal("\t10\t5\t" + In("work", "long.md") + "\nend\t30\t3\t" + In("work", "long.md") + "\n",
            File.ReadAllText(PeekMarks.FilePath));
        // Saved again by the same name: in its place
        (_, errors, _, _) = Run("peek long.md +2 2\npeek save END\npeek save -d\npeek saved\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("end\t2\t2\t" + In("work", "long.md") + "\n", File.ReadAllText(PeekMarks.FilePath));
    }

    [Fact]
    public void PeekSaveAndBack_TellWhatTheyCannotDo()
    {
        FortyLines();
        var (output, errors, _, _) = Run("peek saved\npeek back\npeek save\npeek long.md 3\npeek save 2x\npeek save a b\n" +
                                         "peek back none\npeek back 9\npeek save -d none\npeek saved x\n");
        Assert.Contains(Lang.T("No peeks saved yet: peek save keeps the window of the last peek"), output);
        Assert.Contains(Lang.T("No peeks saved yet: peek save keeps the window of the last peek"), errors);
        Assert.Contains(Lang.T("No file peeked yet: peek a file first (peek plot.md)"), errors);
        Assert.Contains(Lang.F("Not a name for a saved peek: {0} (a letter first, then letters, digits, - and _)", "2x"), errors);
        Assert.Contains(Lang.T("Give one name, or none: peek save, peek save intro, peek save -d intro"), errors);
        Assert.Contains(Lang.F("No saved peek named {0} (peek saved lists them)", "none"), errors);
        Assert.Contains(Lang.T("No peeks saved yet: peek save keeps the window of the last peek"), errors);
        Assert.Contains(Lang.T("After peek saved: filter text, -t text, or both"), errors);
        // A file gone since: told
        (_, errors, _, _) = Run("peek long.md 3\npeek save gone\n");
        File.Delete(In("work", "long.md"));
        (output, errors, _, _) = Run("peek back gone\npeek saved\n");
        Assert.Contains(Lang.F("Not found: {0}", In("work", "long.md")), errors);
        Assert.Contains(Lang.T("(missing)"), output);
        Assert.Equal("peek saved", Tab(Completion(), "peek saved"));
        Assert.Equal("peek back gone", Tab(Completion(), "peek back g"));
        Assert.Contains("peek save [name]", Run("help peek\n").Output);
    }

    [Fact]
    public void PeekMarks_FollowAFileRenamedOrMoved_AndPeeksTxtIsProtected()
    {
        FortyLines();
        Run("peek long.md 3\npeek save intro\nrename long.md longer.md\nmove longer.md notes\n");
        Assert.Equal(In("work", "notes", "longer.md"), PeekMarks.Get("intro")!.Path);
        var (_, errors, _, _) = Run("delete " + PeekMarks.FilePath + "\n", yes: true);
        Assert.Contains(DialogText.FileUsedByBlyant, errors);
    }

    [Fact]
    public void TheLastPeek_IsKeptForTheNextShell_TypedInAtATerminal()
    {
        FortyLines();
        Run("peek long.md +20 4\n", terminal: true);
        Assert.Equal(new PeekMarks.Window(In("work", "long.md"), 20, 4), PeekMarks.Last);
        // The next shell goes on from it
        var (output, _, _, _) = Run(".\n+\n", terminal: true);
        Assert.Contains("  20  line 20\n", output);
        Assert.Contains("  24  line 24\n", output);
        // From a file or a pipe: neither taken nor kept
        (output, _, _, _) = Run("peek long.md +5 2\n.\n");
        Assert.Equal(new PeekMarks.Window(In("work", "long.md"), 21, 4), PeekMarks.Last);
        // forget: no last peek for the next shell
        Run("forget\n" + DialogText.ChoiceYes + "\n", terminal: true);
        Assert.Null(PeekMarks.Last);
    }

    [Fact]
    public void PeekSaved_FilterAndT_SearchTheNamesPathsAndLines_AndKeepTheNumbers()
    {
        File.WriteAllText(In("work", "ch1.md"), "The storm\nat sea\nthe harbour lights\nfar away\n");
        File.WriteAllText(In("work", "notes.md"), "harbour master\nlist\n");
        var (_, errors, _, _) = Run("peek ch1.md 1-2\npeek save storm\npeek ch1.md 3-4\npeek save lights\n" +
                                    "peek notes.md 2-2\npeek save list\n");
        Assert.Equal(string.Empty, errors);
        var (output, _, _, _) = Run("peek saved filter ch1.md\n");
        Assert.Contains("1  storm   1-2  " + In("work", "ch1.md"), output);
        Assert.Contains("2  lights  3-4  " + In("work", "ch1.md"), output);
        Assert.DoesNotContain("notes.md", output);
        // A pattern fits the name or the name of the file
        (output, _, _, _) = Run("peek saved filter li*\n");
        Assert.Contains("2  lights", output);
        Assert.Contains("3  list", output);
        Assert.DoesNotContain("storm", output);
        Assert.Contains("peek saved filter|-t text", Run("help peek\n").Output);
        // -t: only the lines of the window (notes.md has "harbour" in line 1, outside its window)
        (output, _, _, _) = Run("peek saved -t harbour\n");
        Assert.Contains("2  lights  ch1.md:3  the harbour lights", output);
        Assert.DoesNotContain("list", output);
        // Both, and nothing found
        (output, _, _, _) = Run("peek saved filter notes -t harbour\npeek saved filter none\npeek saved -t none\n");
        Assert.Contains(Lang.F("No saved peeks with {0} in the name or path and {1} in the lines", "notes", "harbour"), output);
        Assert.Contains(Lang.F("No saved peeks with {0} in the name or path", "none"), output);
        Assert.Contains(Lang.F("No saved peeks with {0} in the lines", "none"), output);
        var (_, wrong, _, _) = Run("peek saved filter\npeek saved -t\npeek saved x\n");
        Assert.Equal(2, wrong.Split(Lang.T("Give a text: peek saved filter storm, peek saved -t harbour")).Length - 1);
        Assert.Contains(Lang.T("After peek saved: filter text, -t text, or both"), wrong);
        Assert.Equal("peek saved filter", Tab(Completion(), "peek saved fi"));
    }

    // ---- 1.6.1: the findings of the review before the release

    [Theory]
    [InlineData("+9")]
    [InlineData("9:")]
    [InlineData("9:x")]
    [InlineData("#9:32:1")]
    [InlineData("5-+7")]
    [InlineData("#:5")]
    [InlineData("0")]
    [InlineData("7-5")]
    [InlineData("1-2-3")]
    [InlineData(" 9")]
    public void WordNumbers_TakeOnlyTheFormsWordsShows(string typed)
    {
        Assert.False(ShellSession.WordNumbers(typed, out _, out _, out _));
    }

    [Theory]
    [InlineData("**bold** x", 2, 2, true, 1, false, "x **bold**")]
    [InlineData("x (see)", 1, 1, false, 2, false, "(see) x")]
    [InlineData("(see chapter) end", 3, 3, true, 1, false, "end (see chapter)")]
    [InlineData("a, **b** c", 1, 1, false, 2, false, ", **b** a c")]
    public void LineWords_Moved_PutsTheWordsOutsideTheMarksOfTheTarget(string line, int first, int last, bool before,
        int target, bool blanks, string expected)
    {
        Assert.Equal(expected, LineWords.Moved(line, first, last, before, target, blanks));
    }

    [Fact]
    public void WordMove_TheTargetIsCheckedAgainstTheNumbersWordsShowed()
    {
        File.WriteAllText(In("work", "m.md"), "x a/b y\n");
        var (_, errors, _, _) = Run("words -p m.md 1\nword m.md 1 1 move after 2\n");
        Assert.Contains(Lang.F("words -p numbered line {0}, and without -p the numbers differ: give -p to word too", 1), errors);
        Assert.Equal("x a/b y\n", Text("m.md"));
        // clip words checks them the same way
        (_, errors, _, _) = Run("words -p m.md 1\nclip words m.md 1 2\n");
        Assert.Contains(Lang.F("words -p numbered line {0}, and without -p the numbers differ: give -p to word too", 1), errors);
    }

    [Fact]
    public void WordNumbers_WithAColumn_AreCheckedAgainstTheLineAsItIsNow()
    {
        File.WriteAllText(In("work", "c.md"), "one two three\n");
        var (_, errors, _, _) = Run("word c.md 1 #2:9 upper\nclip words c.md 1 #3:5\nword c.md 1 +2 upper\n" +
                                    "word c.md 1 1 move after #3:3\n");
        Assert.Contains(Lang.F("Word {0} starts at column {1} now, not {2}: words shows the line as it is", 2, 5, 9), errors);
        Assert.Contains(Lang.F("Word {0} starts at column {1} now, not {2}: words shows the line as it is", 3, 9, 5), errors);
        Assert.Contains(Lang.F("Not words: {0} (as 9, or 5-7)", "+2"), errors);
        Assert.Contains(Lang.F("Word {0} starts at column {1} now, not {2}: words shows the line as it is", 3, 9, 3), errors);
        Assert.Equal("one two three\n", Text("c.md"));
    }

    [Fact]
    public void ClipWords_ReadsAFileThatWordCannotChange()
    {
        // (not UTF-8: word refuses it, clip words only reads it)
        File.WriteAllBytes(In("work", "l.md"), new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x20, 0x6E, 0x6F, 0x69, 0x72, 0x0A });
        var (output, errors, _, _) = Run("clip words l.md 1 2\necho $clip\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("\nnoir\n", output);
    }

    [Fact]
    public void TheLastPeek_FollowsARename_AndIsKeptOnlyWhenTheShellChangedIt()
    {
        File.WriteAllText(In("work", "p.md"), "a\nb\nc\n");
        // The shell that runs follows a rename through the hook every rename and move calls (the shell's own, the file
        // browser, the editor opened from the shell)
        var (output, errors, _, _) = Run("peek p.md 2\nrename p.md q.md\n.\n", terminal: true);
        Assert.Equal(string.Empty, errors);
        Assert.Equal(2, output.Split("   1  a\n").Length - 1);
        Assert.Equal(new PeekMarks.Window(In("work", "q.md"), 1, 2), PeekMarks.Last);
        // Without a shell that runs, the hook does nothing
        ShellSession.LastPeekFollows(In("work", "q.md"), In("work", "r.md"));
        // A shell that did not change the last peek leaves the window in peeks.txt, also when its file is not there
        File.Move(In("work", "q.md"), In("work", "r.md"));
        Run("pwd\n", terminal: true);
        Assert.Equal(new PeekMarks.Window(In("work", "q.md"), 1, 2), PeekMarks.Last);
    }

    [Fact]
    public void Forget_ClearsTheLastPeekInPeeksTxt_AtOnce()
    {
        File.WriteAllText(In("work", "p.md"), "a\nb\n");
        Run("peek p.md 1\n", terminal: true);
        Assert.NotNull(PeekMarks.Last);
        // (peeks.txt read in the same shell, right after forget: a terminal closed then does not leave the shell)
        var (_, errors, _, _) = Run("forget\n" + DialogText.ChoiceYes + "\nclip text " + PeekMarks.FilePath + "\n", terminal: true);
        Assert.Contains(Lang.T("Nothing to copy: it is empty"), errors);
        PeekMarks.Load();
        Assert.Null(PeekMarks.Last);
    }

    [Fact]
    public void PeekMarks_NumbersPastIntAreLeftOut_AndNamesAreOneInAnyLetterCase()
    {
        var path = In("work", "long.md");
        PeekMarks.Parse("big\t2000000000\t500000000\t" + path + "\nStorm\t1\t2\t" + path + "\n");
        Assert.Equal(new[] { "storm" }, PeekMarks.Saved.Select(entry => entry.Name));
        File.WriteAllText(path, "one\ntwo\n");
        var (output, errors, _, _) = Run("peek long.md 1\npeek save STORM\npeek saved\npeek back sToRm\n" +
                                         "peek back 99999999999\npeek save -d Storm\n");
        Assert.Contains("1  storm", output);
        Assert.Contains(Lang.F("No saved peek {0}: {1} are saved (peek saved lists them)", "99999999999", 1), errors);
        Assert.Contains(Lang.F("Saved peek {0} deleted", "storm"), output);
    }

    [Fact]
    public void PeekSaved_T_SearchesTheLinesPeekBackShows_InAFileThatGotShorter()
    {
        File.WriteAllText(In("work", "s.md"), string.Concat(Enumerable.Range(1, 10).Select(i => "line " + i + "\n")));
        Run("peek s.md +8 3\npeek save end\n");
        File.WriteAllText(In("work", "s.md"), "one\nstorm here\nthree\n");
        // (a filter that is in no temporary path: on macOS the path of the tests has "x" in it, /var/folders/xx/...)
        var (output, _, _, _) = Run("peek saved -t storm\npeek saved filter qqz -t storm\npeek saved filter \"e\"\"\"\n");
        Assert.Contains("1  end  s.md:2  storm here", output);
        Assert.Contains(Lang.F("No saved peeks with {0} in the name or path and {1} in the lines", "qqz", "storm"), output);
        // A filter in quotes, with "" a quote
        Assert.Contains(Lang.F("No saved peeks with {0} in the name or path", "e\""), output);
        Assert.Equal("peek save -d end", Tab(Completion(), "peek save -d e"));
        Assert.Equal("peek saved filter x -t", Tab(Completion(), "peek saved filter x -"));
    }

    [UnixFact]
    public void PeekSave_APathWithATab_IsTold_OnLinuxAndMacOs()
    {
        File.WriteAllText(In("work", "a\tb.md"), "x\n");
        var (_, errors, _, _) = Run("find a*\npeek 1\npeek save\n");
        Assert.Contains(Lang.F("A path with a tab or a line break cannot be saved: {0}", In("work", "a\tb.md")), errors);
    }

    [Fact]
    public void ShellHistory_SetTo0_DeletesTheFile_AndAFailedReadIsTriedAgain()
    {
        CommandHistory.Clear();
        try
        {
            FileIo.SaveSettings();
            Run("pwd\nls\n", terminal: true);
            Assert.True(File.Exists(ShellHistory.FilePath));
            Run("set shellhistory 0\npwd\n", terminal: true);
            Assert.False(File.Exists(ShellHistory.FilePath));
            // A file with CRLF line endings, as written on Windows, read on another system
            Shell.HistoryCount = Shell.DefaultHistoryCount;
            CommandHistory.Clear();
            ShellHistory.Reset();
            File.WriteAllText(ShellHistory.FilePath, "pwd\r\nls\r\n");
            var (output, _, _, _) = Run("again\n", terminal: true);
            Assert.Contains("1  ls\n", output);
            // A lower setting: no more commands than it keeps, also before the next one
            Shell.HistoryCount = 1;
            Assert.Single(CommandHistory.Of(CommandContext.Shell));
            Shell.HistoryCount = Shell.DefaultHistoryCount;
            // forget empties the list, and the next file is written again when it grows
            CommandHistory.Clear();
            Run("forget\n" + DialogText.ChoiceYes + "\npwd\n", terminal: true);
            Assert.Equal("pwd\n", File.ReadAllText(ShellHistory.FilePath));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void ShellHistory_KeepsALineAsTyped_NotAsItRan()
    {
        CommandHistory.Clear();
        try
        {
            Run("reg w pwd\n$w\npwd\n!!\n", terminal: true);
            var lines = File.ReadAllLines(ShellHistory.FilePath);
            Assert.Contains("$w", lines);
            Assert.DoesNotContain("!!", lines);
            // A line typed with a blank in front does not move the numbers of again
            var (output, _, _, _) = Run(" echo secret\nagain\n", terminal: true);
            Assert.Contains("1  pwd\n", output);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Theory]
    [InlineData("at last, peace", false)]
    [InlineData("at 10 text", true)]
    [InlineData("replace the old window", false)]
    [InlineData("replace old new", true)]
    [InlineData("end of part one", true)]
    public void LinesMeantByChange_TakesAtAndReplaceInTheFormLinesTakes(string text, bool meant)
    {
        Assert.Equal(meant, ShellSession.LinesMeantByChange(text));
    }

    [Fact]
    public void Line_WithAFileAndALine_PointsToFile()
    {
        File.WriteAllText(In("work", "f.md"), "a\nb\nc\n");
        var (_, errors, _, _) = Run("line f.md 3\nline f.md bottom\n");
        Assert.Equal(2, errors.Split(Lang.T("line sets only the line of $file: file notes.md 3 sets the file and the line")).Length - 1);
    }

    [Fact]
    public void LinesAt_LeavesBlankLinesAsTheyAre_AndTellsAShortLineInTheMiddle()
    {
        File.WriteAllText(In("work", "b.md"), "first line\n\nthird line\n");
        var (_, errors, _, _) = Run("lines b.md 1-3 at 7 \"big \"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("first big line\n\nthird big line\n", Text("b.md"));
        File.WriteAllText(In("work", "b.md"), "first line\nab\nthird line\n");
        (_, errors, _, _) = Run("lines b.md 1-3 at 7 x\n");
        Assert.Contains(Lang.F("Line {0} has no column {1}: nothing changed", 2, 7), errors);
        Assert.Equal("first line\nab\nthird line\n", Text("b.md"));
    }

    [Theory]
    [InlineData("a\u200Bb", 2, 2)]
    [InlineData("\ud83d\ude00x", 3, 2)]
    [InlineData("e\u0301x", 2, 2)]
    [InlineData("a\tb", 3, 2)]
    public void IndexOfColumn_GoesPastCharactersOfNoWidth_AsTheEditorShowsThem(string line, int column, int index)
    {
        Assert.Equal(index, LineWords.IndexOfColumn(line, column));
    }

    [Fact]
    public void Quoted_DoublesTheQuotes_AndWordsTakeThemBack()
    {
        Assert.Equal("\"say \"\"hi\"\".md\"", ShellSession.Quoted("say \"hi\".md"));
        Assert.Equal(new[] { "say \"hi\".md" }, ShellSession.Words(ShellSession.Quoted("say \"hi\".md")));
        Assert.Equal(("remove ", "\"\""), ShellCompletion.LastWord("remove \"\""));
        // A register with quotes in its value, put in a text in quotes
        File.WriteAllText(In("work", "q.md"), "line\n");
        var (_, errors, _, _) = Run("reg q a\"\"b\nlines q.md 1 end \"$q\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("linea\"b\n", Text("q.md"));
    }

    [Fact]
    public void Filters_AndThePeekSearch_TakeAQuoteWrittenAsTwo()
    {
        File.WriteAllText(In("work", "q.md"), "one\nsay \"hi\"\n");
        var (output, errors, _, _) = Run("peek q.md \"/say \"\"hi\"\"\"\nagain filter \"\"\"\"\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("2  say \"hi\"", output);
        Assert.Contains("(match 1 of 1", output);
    }

    // ---- 1.6.2: input, lines typed into a file as in ed

    [Fact]
    public void Input_PutsTheLinesInAfterLine_AsTyped_UntilALineWithADot()
    {
        File.WriteAllText(In("work", "in.md"), "one\ntwo\nthree\n");
        var (output, errors, _, session) = Run("peek in.md 1\ninput\nThe ferry was late.\n\n  Indented $5 \"quoted\"\n.\n" +
                                               "echo $line\ninput in.md bottom\nlast\n .\n. \n");
        Assert.Equal(string.Empty, errors);
        // After line 1, each line after the one before; as typed: blanks, $5 and quotes kept, a blank line too, and
        // " ." (only a line that starts with the "." ends input)
        Assert.Equal("one\nThe ferry was late.\n\n  Indented $5 \"quoted\"\ntwo\nthree\nlast\n .\n", Text("in.md"));
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "in.md"), 2, 4), output);
        Assert.Contains("\n4\n", output);
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "in.md"), 7, 8), output);
        // The file is the last list at the first line put in, and $line the last
        Assert.Equal(7, session.LastList.Single().At?.Line);
    }

    [Fact]
    public void Input_IsOneStepOfUndo_AndTellsWhatItCannotDo()
    {
        File.WriteAllText(In("work", "in.md"), "one\ntwo\n");
        var (output, errors, _, _) = Run("input in.md top\na\nb\nc\n.\nundo\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains(Lang.F("Undone: {0}", "input in.md top"), output);
        Assert.Equal("one\ntwo\n", Text("in.md"));
        (output, errors, _, _) = Run("input\ninput in.md\ninput in.md middle\ninput in.md +2\n.\n");
        Assert.Contains(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"), errors);
        Assert.Equal(2, errors.Split(Lang.T("Give a file and where, or nothing: input, input notes.md bottom, input notes.md +12")).Length - 1);
        Assert.Contains(Lang.T("Nothing put in"), output);
        // The end of the input ends it too, with the lines kept
        (output, errors, _, _) = Run("input in.md bottom\nend\n");
        Assert.Equal("one\ntwo\nend\n", Text("in.md"));
        // A new file: at the top
        (_, errors, _, _) = Run("new fresh\ninput\nFirst line\n.\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("First line\n", File.ReadAllText(In("work", "fresh.md")).Replace("\r\n", "\n"));
        Assert.Equal("input in.md top", Tab(Completion(), "input in.md t"));
        Assert.Contains("input [+line|file|n top|bottom|+line]", Run("help input\n").Output);
    }

    [Fact]
    public void Input_ShowsTheFileAndTheLine_AndKeepsTheLinesOutOfTheCommandsTyped()
    {
        CommandHistory.Clear();
        try
        {
            File.WriteAllText(In("work", "in.md"), "one\ntwo\n");
            var (output, _, _, _) = Run("peek in.md 1\ninput\nsecret text\nmore\n.\n", terminal: true);
            Assert.Contains("$file:2> ", output);
            Assert.Contains("$file:3> ", output);
            Assert.Contains("$file:4> ", output);
            Assert.Contains("input", CommandHistory.Of(CommandContext.Shell));
            Assert.DoesNotContain(CommandHistory.Of(CommandContext.Shell), command => command.Contains("secret"));
            Assert.DoesNotContain("secret", File.ReadAllText(ShellHistory.FilePath));
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Input_InARun_TakesTheLinesAfterIt_UpToTheDot()
    {
        File.WriteAllText(In("work", "in.md"), "one\n");
        File.WriteAllText(In("work", "script.txt"), "input in.md bottom\nTwo\n  Three\n.\necho done\n");
        var (output, errors, _, _) = Run("run script.txt\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nTwo\n  Three\n", Text("in.md"));
        Assert.Contains("\ndone\n", output);
        // (the lines of the text are not run as commands)
        Assert.DoesNotContain(Lang.F("Unknown command: {0} (help lists the commands)", "two"), output);
    }

    [Fact]
    public void LineReader_ForText_HasNoTabNorUp_AndTellsCtrlCFromABlankLine()
    {
        CommandHistory.Clear();
        try
        {
            CommandHistory.Add(CommandContext.Shell, "peek notes.md");
            var queue = new Queue<ConsoleKeyInfo>(LineKeys("<Up>ab<Tab><CR>"));
            var reader = new ShellLineReader(() => queue.Dequeue(), new StringWriter(), () => 80, (text, _) => text + "XX");
            Assert.Equal("ab", reader.Read("> ", true));
            Assert.False(reader.Dropped);
            queue = new Queue<ConsoleKeyInfo>(LineKeys("abc<C-c>"));
            reader = new ShellLineReader(() => queue.Dequeue(), new StringWriter(), () => 80, (text, _) => text);
            Assert.Equal(string.Empty, reader.Read("> ", true));
            Assert.True(reader.Dropped);
            queue = new Queue<ConsoleKeyInfo>(LineKeys("<CR>"));
            reader = new ShellLineReader(() => queue.Dequeue(), new StringWriter(), () => 80, (text, _) => text);
            Assert.Equal(string.Empty, reader.Read("> ", true));
            Assert.False(reader.Dropped);
            // A command: Tab and Up as before
            Assert.Equal("peek notes.md", ReadLine("<Up><CR>").Line);
        }
        finally
        {
            CommandHistory.Clear();
        }
    }

    [Fact]
    public void Input_MakesTheFileGivenFileDollar_FromTheStart()
    {
        File.WriteAllText(In("work", "a.md"), "a\n");
        File.WriteAllText(In("work", "b.md"), "b\n");
        // (the prompt names $file: a file given is $file at once, also when nothing is typed in)
        var (output, _, _, _) = Run("peek a.md 1\ninput b.md bottom\n.\necho $file $line\n");
        Assert.Contains(In("work", "b.md") + " 1\n", output);
    }

    // ---- 1.6.2: lines dup, and a copy within the same file

    [Fact]
    public void LinesDup_CopiesTheLinesRightBelowThem_AsManyTimesAsAsked()
    {
        File.WriteAllText(In("work", "d.md"), "# T\r\none\r\ntwo\r\nlast\r\n");
        var (output, errors, _, session) = Run("lines d.md 2 dup\nlines d.md 3-4 dup 2\necho $line\n");
        Assert.Equal(string.Empty, errors);
        // (the line endings of the file kept)
        Assert.Equal("# T\r\none\r\none\r\ntwo\r\none\r\ntwo\r\none\r\ntwo\r\nlast\r\n", Text("d.md"));
        Assert.Contains(Lang.F("{0}: line {1} put in", In("work", "d.md"), 3), output);
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "d.md"), 5, 8), output);
        // $line is the last copy; the file is the last list at the first copy
        Assert.Contains("\n8\n", output);
        Assert.Equal(5, session.LastList.Single().At?.Line);
        // Lines given apart: one block below the last, in their order; @ the line worked on
        File.WriteAllText(In("work", "d.md"), "a\nb\nc\n");
        (_, errors, _, _) = Run("lines d.md 1,3 dup\nlines @ dup\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("a\nb\nc\na\nc\nc\n", Text("d.md"));
    }

    [Fact]
    public void LinesDup_IsOneStepOfUndo_AndTellsACountItCannotTake()
    {
        File.WriteAllText(In("work", "d.md"), "a\nb\n");
        var (output, errors, _, _) = Run("lines d.md 1-2 dup 3\nundo\nlines d.md 1 dup 0\nlines d.md 1 dup 501\nlines d.md 1 dup x\n");
        Assert.Contains(Lang.F("Undone: {0}", "lines d.md 1-2 dup 3"), output);
        Assert.Equal("a\nb\n", Text("d.md"));
        Assert.Equal(3, errors.Split(Lang.F("After dup: how many copies, 1 to {0} (lines notes.md 4-6 dup 3)", 500)).Length - 1);
        Assert.Equal("lines d.md 1 dup", Tab(Completion(), "lines d.md 1 du"));
        Assert.Contains("lines file|n lines dup [n]", Run("help lines\n").Output);
    }

    [Fact]
    public void LinesCopy_GoesToAnotherPlaceOfTheSameFile_ButCutIsShift()
    {
        File.WriteAllText(In("work", "c.md"), "# Notes\none\ntwo\n\n## Ideas\n\nlast\n");
        var (_, errors, _, _) = Run("lines c.md 2 > c.md bottom\nlines c.md 3 > c.md top\n");
        Assert.Equal(string.Empty, errors);
        // (line 3 is "two" after the first copy)
        Assert.Equal("two\n# Notes\none\ntwo\n\n## Ideas\n\nlast\none\n", Text("c.md"));
        (_, errors, _, _) = Run("lines c.md 2 > c.md bottom cut\n");
        Assert.Contains(Lang.T("The lines are in that file already: shift moves lines in a file"), errors);
        Assert.Equal("two\n# Notes\none\ntwo\n\n## Ideas\n\nlast\none\n", Text("c.md"));
    }

    // ---- 1.6.2: the names set by Blyant in help

    [Fact]
    public void Help_ListsTheNamesSetByBlyant_AndTellsOne_AndFindsThem()
    {
        var (output, _, _, _) = Run("help\n");
        Assert.Contains(Lang.T("Names set by Blyant, put in any command (reg lists them with their values):"), output);
        // Every name Blyant sets has a row
        Assert.All(ShellRegisters.Reserved, name => Assert.Contains("  $" + name + " ", output));
        // (and "%", which is $file in the place of a file)
        Assert.Equal(ShellRegisters.Reserved.Length + 1, ShellSession.NameRows().Count);
        Assert.Contains("  %  ", output);
        Assert.All(output.TrimEnd('\n').Split('\n'), line => Assert.True(TextMetrics.WidthOf(line) <= 79, line));
        (output, _, _, _) = Run("help $line\nhelp $CLIP\nhelp -t time\n");
        Assert.Contains("$line  " + Lang.T("The line of $file worked on last; with $file it is @"), output);
        // ($clip, $peek and $calc with their commands too)
        Assert.Contains("clip words [-p] file|n line n|n-m", output);
        Assert.Contains("$time  " + Lang.T("The time now, as 14:05"), output);
        Assert.Contains("$now  ", output);
    }

    // ---- 1.6.2: $line kept with $file for the next shell

    [Fact]
    public void Line_IsKeptWithFile_AndIsLineAgainInTheNextShell_WhileTheFileHasIt()
    {
        try
        {
            File.WriteAllText(In("work", "kept.md"), "one\ntwo\nthree\n");
            var (_, _, _, session) = Run("peek kept.md 1\ninput kept.md +2\nnew line\n.\n", terminal: true);
            Assert.Equal(2, session.WorkedLine);
            Shell.RememberLast(session.Directory, session.WorkedFile, session.WorkedLine);
            Assert.Equal("2", FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelllastline"]);
            // The next shell at a terminal goes on at that line: input puts the next line after it
            var (output, _, _, _) = Run("echo $file $line\ninput\nnext\n.\n", terminal: true);
            Assert.Contains(In("work", "kept.md") + " 2\n", output);
            Assert.Equal("one\nnew line\nnext\ntwo\nthree\n", Text("kept.md"));
            // A file made shorter since: the line is not taken
            File.WriteAllText(In("work", "kept.md"), "one\n");
            (output, _, _, _) = Run("echo $line\n", terminal: true);
            Assert.DoesNotContain("\n2\n", output);
            // The line goes with its file: another file without a line keeps none of it
            File.WriteAllText(In("work", "other.md"), "x\n");
            Shell.RememberLast(In("work"), In("work", "other.md"));
            Assert.Null(Shell.LastLine);
            Assert.Equal(string.Empty, FileIo.ParseSettings(FileIo.ReadSettingsFile())["shelllastline"]);
            Assert.Null(Shell.ParseLastLine("x"));
            Assert.Equal(0, Shell.ParseLastLine("0"));
        }
        finally
        {
            Shell.LastFile = string.Empty;
            Shell.LastLine = null;
        }
    }

    // ---- 1.6.2: % is $file in the place of a file

    [Fact]
    public void Percent_IsFile_InThePlaceOfAFile_AndATextKeepsIt()
    {
        File.WriteAllText(In("work", "p.md"), "one\ntwo\n");
        var (output, errors, _, _) = Run("peek %\n");
        Assert.Contains(Lang.T("% is $file, the file worked on last: none yet (peek notes.md)"), errors);
        (output, errors, _, _) = Run("peek p.md 1\npeek %\nlines % 1 dup\ninsert % bottom 100% done, %\ncopy % Draft\n");
        Assert.Equal(string.Empty, errors);
        // (a text keeps % as it is, also alone)
        Assert.Equal("one\none\ntwo\n100% done, %\n", Text("p.md"));
        Assert.True(File.Exists(In("work", "Draft", "p.md")));
        // peek 50% is still a place in the file, and help % tells it
        (output, errors, _, _) = Run("peek p.md 50%\nhelp %\n");
        Assert.Equal(string.Empty, errors);
        Assert.Contains("%  " + Lang.T("$file, in the place of a file: peek %, copy % backup"), output);
    }

    // ---- 1.6.2: the fixes of the review before the release

    [Fact]
    public void Input_KeepsABlankLine_AtTheEndOfAFileWithoutALineBreak()
    {
        File.WriteAllText(In("work", "nl.md"), "a\nb");
        var (output, errors, _, session) = Run("input nl.md bottom\n\nc\n.\necho $line\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("a\nb\n\nc\n", Text("nl.md"));
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "nl.md"), 3, 4), output);
        Assert.EndsWith("\n4\n", output);
        // A blank line typed last is a line too, and the file now ends with a line break
        File.WriteAllText(In("work", "nl.md"), "a\nb");
        Run("input nl.md bottom\n\n.\n");
        Assert.Equal("a\nb\n\n", Text("nl.md"));
        Assert.Equal(3, ShellSession.LinesOfText(Text("nl.md")).Count);
    }

    [Theory]
    // A blank last line is never only the line break of the line before it
    [InlineData("a\nb", "", 2, "a\nb\n\n")]
    [InlineData("a\nb", "c", 2, "a\nb\nc")]
    [InlineData("a\nb\n", "", 2, "a\nb\n\n")]
    [InlineData("a\nb", "", 1, "a\n\nb")]
    public void LinesPutInAtTheEnd_KeepABlankLine(string text, string line, int position, string expected)
    {
        Assert.Equal(expected, ShellSession.WithLinesAt(text, new[] { line }, position).Text);
    }

    [Fact]
    public void LinesTakenOutOrChanged_KeepABlankLastLine()
    {
        // (line 3 taken out of "a", "", "b": the blank line 2 is still a line)
        Assert.Equal("a\n\n", ShellSession.WithoutLines("a\n\nb", new[] { 3 }));
        Assert.Equal("a\n\n", ShellSession.WithLinesChanged("a\nb", 2, 2, string.Empty).Text);
        File.WriteAllText(In("work", "nl.md"), "a\nb");
        var (_, errors, _, _) = Run("lines nl.md 2 remove b\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal(2, ShellSession.LinesOfText(Text("nl.md")).Count);
    }

    [Fact]
    public void Input_InAScript_GetsNoWordsOfTheRun_AndItsTextIsNoCommand()
    {
        File.WriteAllText(In("work", "in.md"), "one\n");
        // "$5" in the text is no word the script takes, nor "$2" and "$*" when it is run with a word
        File.WriteAllText(In("work", "price.txt"), "input in.md bottom\nPrice: $5\n# Heading $1\n.\n");
        var (_, errors, _, _) = Run("run price.txt\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nPrice: $5\n# Heading $1\n", Text("in.md"));
        File.WriteAllText(In("work", "in.md"), "one\n");
        File.WriteAllText(In("work", "file.txt"), "input $1 bottom\nCost $2 and $*\n.\n");
        (_, errors, _, _) = Run("run file.txt in.md\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nCost $2 and $*\n", Text("in.md"));
        // One command and its text: the words go after the command, not after a line of the text
        File.WriteAllText(In("work", "in.md"), "one\n");
        File.WriteAllText(In("work", "one.txt"), "input\ninput\n.\n");
        (_, errors, _, _) = Run("run one.txt in.md +1\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("input\none\n", Text("in.md"));
    }

    // A reader of the lines typed that changes a file when a line is read: as the file changed in the editor, or by
    // another program, while input waits for a line
    private sealed class ChangingReader(string text, int line, Action change) : TextReader
    {
        private readonly StringReader _lines = new(text);
        private int _read;

        public override string? ReadLine()
        {
            if (++_read == line) change();
            return _lines.ReadLine();
        }
    }

    [Fact]
    public void Input_StopsWhenTheFileWasChangedMeanwhile_AndWritesNothingOverIt()
    {
        File.WriteAllText(In("work", "in.md"), "one\n");
        var reader = new ChangingReader("input in.md bottom\ntwo\nthree\n.\n", 3,
            () => File.WriteAllText(In("work", "in.md"), "one\ntwo\nsaved in the editor\n"));
        var output = new StringWriter();
        var errors = new StringWriter();
        Shell.Run(new ShellSession(reader, output, errors, In("work"), false, false));
        Assert.Equal("one\ntwo\nsaved in the editor\n", Text("in.md"));
        Assert.Contains(Lang.F("{0} was changed meanwhile: the line is not put in, and input stops", In("work", "in.md")),
            errors.ToString());
        Assert.Contains(Lang.F("{0}: line {1} put in", In("work", "in.md"), 2), output.ToString());
        // A file deleted meanwhile is not written again
        File.WriteAllText(In("work", "in.md"), "one\n");
        reader = new ChangingReader("input in.md bottom\ntwo\nthree\n.\n", 3, () => File.Delete(In("work", "in.md")));
        Shell.Run(new ShellSession(reader, new StringWriter(), new StringWriter(), In("work"), false, false));
        Assert.False(File.Exists(In("work", "in.md")));
    }

    [Fact]
    public void Input_IntoAFileTooLargeForUndo_TellsItOnce()
    {
        File.WriteAllText(In("work", "big.md"), new string('x', (int)ShellSession.UndoMaxSize + 10) + "\n");
        var (output, errors, _, _) = Run("input big.md bottom\na\nb\nc\n.\n");
        Assert.Equal(string.Empty, errors);
        var note = Lang.F("(this cannot be undone: {0} is larger than 2 MB)", "big.md");
        Assert.Equal(1, output.Split(note).Length - 1);
    }

    [Fact]
    public void LinesDup_RefusesMoreThanTheEditorPutsInAtOnce()
    {
        File.WriteAllText(In("work", "long.md"), new string('x', 25_000) + "\nend\n");
        var (_, errors, _, _) = Run("lines long.md 1 dup 500\n");
        Assert.Contains(Lang.T("Too much text to insert at once (use a smaller count)"), errors);
        Assert.Equal(new string('x', 25_000) + "\nend\n", Text("long.md"));
        (_, errors, _, _) = Run("lines long.md 2 dup 501\n");
        Assert.Contains(Lang.F("After dup: how many copies, 1 to {0} (lines notes.md 4-6 dup 3)", 500), errors);
    }

    [Theory]
    [InlineData("+12", 12)]
    [InlineData("+1", 1)]
    [InlineData("++12", null)]
    [InlineData("+ 12", null)]
    [InlineData("+-1", null)]
    [InlineData("+0", null)]
    [InlineData("12", null)]
    [InlineData("+1,2", null)]
    public void APlaceWithPlus_IsALineWrittenWithDigits(string word, int? expected)
    {
        Assert.Equal(expected, ShellSession.IsPlusLine(word, out var line) ? line : null);
    }

    [Fact]
    public void InsertAndShift_RefuseAPlaceWithTwoSigns()
    {
        File.WriteAllText(In("work", "n.md"), "x\ny\n");
        var (_, errors, _, _) = Run("insert n.md ++5 hi\nlines n.md 1 > n.md ++2\nchange n.md ++1 z\n");
        Assert.Contains(Lang.F("Not a place in a file: {0} (top, bottom or +12)", "++5"), errors);
        Assert.Equal("x\ny\n", Text("n.md"));
        Assert.Equal(3, errors.TrimEnd('\n').Split('\n').Length);
    }

    [Fact]
    public void AnUnknownTime_StaysUnknown_WhenItIsKept()
    {
        // (DateTime.MinValue: a line whose time could not be read; converted, west of UTC it would be a time)
        Assert.Equal("0001-01-01 00:00Z", IsoDateTime.Stored(DateTime.MinValue, "yyyy-MM-dd HH:mm"));
        Assert.True(IsoDateTime.TryParseStored("0001-01-01 00:00Z", "yyyy-MM-dd HH:mm", out var read));
        Assert.Equal(DateTime.MinValue, read);
        // (and as Blyant wrote it before 1.6.2, in local time)
        Assert.True(IsoDateTime.TryParseStored("0001-01-01 00:00", "yyyy-MM-dd HH:mm", out read));
        Assert.Equal(DateTime.MinValue, read);
    }

    // ---- 1.6.2: a and i, input after and before $line, as in ed

    [Fact]
    public void A_IsInput_AndI_PutsTheLinesInBeforeLine()
    {
        File.WriteAllText(In("work", "n.md"), "one\ntwo\nthree\n");
        var (output, errors, _, session) = Run("peek n.md 2\nline 2\ni\nbefore two\n.\necho $line\na\nafter it\n.\n" +
                                               "input before\nB\n.\ni @\nC\n.\ni n.md +1\ntop\n.\n");
        Assert.Equal(string.Empty, errors);
        // i: as line 2, and two goes down; $line the last put in, so a goes on after it, and i before it
        Assert.Equal("top\none\nbefore two\nC\nB\nafter it\ntwo\nthree\n", Text("n.md"));
        Assert.Contains(Lang.F("{0}: line {1} put in", In("work", "n.md"), 2) + "\n2\n", output);
        Assert.Equal(1, session.LastList.Single().At?.Line);
        // In an empty file, at the top
        (_, errors, _, _) = Run("new fresh\ni\nFirst\n.\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("First\n", File.ReadAllText(In("work", "fresh.md")).Replace("\r\n", "\n"));
        // Only input takes "before" (a before is refused), and i a file only with a place, as input takes them
        (_, errors, _, _) = Run("peek n.md 1\na before\ni n.md\n");
        Assert.Equal(2, errors.Split(Lang.T("Give a file and where, or nothing: input, input notes.md bottom, input notes.md +12")).Length - 1);
    }

    [Fact]
    public void AAndI_InAScript_TakeTheLinesAfterThem_AndHelpTellsThem()
    {
        File.WriteAllText(In("work", "n.md"), "one\n");
        File.WriteAllText(In("work", "s.txt"), "a n.md bottom\nCost $1\n.\ni n.md top\n# Title $2\n.\n");
        var (output, errors, _, _) = Run("run s.txt\nhelp i\nhelp\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("# Title $2\none\nCost $1\n", Text("n.md"));
        Assert.Contains("input before", output);
        Assert.Contains("a, i (input)", output);
        Assert.Equal("i n.md top", Tab(Completion(), "i n.md t"));
    }

    // ---- 1.6.2: the dot commands of input (.u, .e, .d, .p, .?)

    [Theory]
    [InlineData(".u", 'u')]
    [InlineData(".e ", 'e')]
    [InlineData(".d", 'd')]
    [InlineData(".p", 'p')]
    [InlineData(".?", '?')]
    [InlineData(".unity", null)]
    [InlineData(" .u", null)]
    [InlineData("...", null)]
    [InlineData(".x", null)]
    [InlineData(".", null)]
    public void InputCommand_IsADotAndALetter_AloneOnTheLine(string line, char? expected)
    {
        Assert.Equal(expected, ShellSession.InputCommand(line));
    }

    [Theory]
    [InlineData("Some text", "Some ")]
    [InlineData("Some  text  ", "Some  ")]
    [InlineData("Some", "")]
    [InlineData("", "")]
    [InlineData("- [ ] Buy milk", "- [ ] Buy ")]
    public void WithoutLastWord_LeavesTheBlankBeforeIt(string line, string expected)
    {
        Assert.Equal(expected, ShellSession.WithoutLastWord(line));
    }

    [Fact]
    public void DotCommands_TakeBackEditDeleteAndShowTheLinesTyped_AtATerminal()
    {
        File.WriteAllText(In("work", "n.md"), "one\ntwo\n");
        // (without line editing the line taken back is shown above the prompt, and typed again)
        var (output, errors, _, session) = Run("peek n.md 1\ninput\n.u\nSome text\n.u\nSome words\nThird linee\n.e\n" +
                                               "Third line!\nGone\n.d\n.p\n.?\nKeep\n.e\n.\n", terminal: true);
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nSome words\nThird line!\nKeep\ntwo\n", Text("n.md"));
        Assert.Contains(Lang.T("No line typed yet: the dot commands work on the lines typed in this input"), output);
        // .u: the line at its number again, without the last word
        Assert.Contains("Some \n$file:2> ", output);
        Assert.Contains("Third linee\n$file:3> ", output);
        Assert.Contains(Lang.F("{0}: line {1} taken out", In("work", "n.md"), 4), output);
        Assert.Contains("  2  Some words\n  3  Third line!\n", output);
        Assert.Contains(Lang.T(".u last word off, .e edit last line, .d delete it, .p show lines, . ends"), output);
        // "." while a line is edited ends input, and the line is as it was; one step of undo for all
        Assert.Contains(Lang.F("{0}: lines {1}-{2} put in", In("work", "n.md"), 2, 4), output);
        Assert.Equal(2, session.LastList.Single().At?.Line);
        File.WriteAllText(In("work", "n.md"), "one\ntwo\n");
        Run("input n.md +2\nA\nB\n.e\nC\n.d\n.\nundo\n", terminal: true);
        Assert.Equal("one\ntwo\n", Text("n.md"));
    }

    [Fact]
    public void DotCommands_AreText_InAScriptOrAPipe()
    {
        File.WriteAllText(In("work", "n.md"), "one\n");
        var (_, errors, _, _) = Run("input n.md bottom\nA line\n.u\n.d\n.\n");
        Assert.Equal(string.Empty, errors);
        Assert.Equal("one\nA line\n.u\n.d\n", Text("n.md"));
    }

    // ---- 1.6.2: input +12, at a line of $file

    [Fact]
    public void InputWithALine_StartsAtThatLineOfFile()
    {
        File.WriteAllText(In("work", "n.md"), "one\ntwo\nthree\n");
        var (output, errors, _, session) = Run("peek n.md 1\ninput +3\nX\n.\ni +1\nTop\n.\na +99\nEnd\n.\n");
        Assert.Equal(string.Empty, errors);
        // As line 3 (three goes down), at the top, and past the end at the bottom
        Assert.Equal("Top\none\ntwo\nX\nthree\nEnd\n", Text("n.md"));
        Assert.Contains(Lang.F("{0}: line {1} put in", In("work", "n.md"), 3), output);
        Assert.Equal(6, session.LastList.Single().At?.Line);
        // Refused: no $file yet, and a line written otherwise than with digits
        (_, errors, _, _) = Run("input +2\n");
        Assert.Contains(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"), errors);
        (_, errors, _, _) = Run("peek n.md 1\ninput ++2\ninput +0\n");
        Assert.Equal(2, errors.Split(Lang.T("Give a file and where, or nothing: input, input notes.md bottom, input notes.md +12")).Length - 1);
        Assert.Contains("input [+line|file|n top|bottom|+line]", Run("help input\n").Output);
    }
}
