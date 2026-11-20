namespace Blyant;

/// <summary>
/// The shell ("blyant --shell"): Blyant used on lines, in any terminal, with a screen reader or from a script. A
/// command is typed at a prompt, and what it shows is written as lines of text: no colors, and no cursor moved. The
/// lists are numbered, and the number of a row of the last list can be given to the next command ("open 3"). Files are
/// edited in the editor of the full screen, and closing it is back at the prompt. See ShellSession
/// </summary>
internal static class Shell
{
    /// <summary>
    /// The shell that runs (null: Blyant runs on the full screen)
    /// </summary>
    internal static ShellSession? Current { get; private set; }

    /// <summary>
    /// A long output comes in pages in a terminal (setting "shellpaging", on unless turned off: only in the settings
    /// file, or with "set shellpaging false" in the shell)
    /// </summary>
    internal static bool Paging { get; set; } = true;

    /// <summary>
    /// In a terminal the line is read key by key: Tab completes, Up and Down bring back the commands typed before
    /// (setting "shellcompletion", on unless turned off: off, the line is read as the terminal reads it, which some
    /// screen readers read best). Only in the settings file, or with "set shellcompletion false"
    /// </summary>
    internal static bool Completion { get; set; } = true;

    /// <summary>
    /// Tab with nothing typed offers the files opened last first, those in the directory completed (setting
    /// "shellrecentfirst", on unless turned off: off, all names in order). Only in the settings file, or with "set"
    /// </summary>
    internal static bool RecentFirst { get; set; } = true;

    /// <summary>
    /// After a command that changed the file of the last peek, its window is shown again (setting "shellautopeek",
    /// off unless turned on). Only in the settings file, or with "set"
    /// </summary>
    internal static bool AutoPeek { get; set; }

    /// <summary>
    /// peek and words show the lines of a file between two markers: "-&gt; notes.md" above them ("-^": more lines
    /// above), and "&lt;- lines 46-60 of 60" below them ("-v": more lines below) (setting "shellmarkers", on unless
    /// turned off).
    /// Only in the settings file, or with "set"
    /// </summary>
    internal static bool Markers { get; set; } = DefaultMarkers;

    internal const bool DefaultMarkers = true;

    /// <summary>
    /// The shell starts with the logo of the start page, without colors, under "- Shell mode -" (setting "shelllogo",
    /// on unless turned off; typed in at a terminal only). Only in the settings file, or with "set"
    /// </summary>
    internal static bool Logo { get; set; } = DefaultLogo;

    internal const bool DefaultLogo = true;

    /// <summary>
    /// How many lines peek shows when none are given (setting "shellpeeklines", 1 to 1000; only in the settings file,
    /// or with "set")
    /// </summary>
    internal static int PeekLines { get; set; } = DefaultPeekLines;

    internal const int DefaultPeekLines = 15;

    /// <summary>
    /// How many lines "peek +" and "peek -" move the window of the last peek (setting "shellpeekstep", 1 to 1000;
    /// only in the settings file, or with "set")
    /// </summary>
    internal static int PeekStep { get; set; } = DefaultPeekStep;

    /// <summary>
    /// How many lines "peek ++" and "peek --" move it (setting "shellpeekbigstep")
    /// </summary>
    internal static int PeekBigStep { get; set; } = DefaultPeekBigStep;

    internal const int DefaultPeekStep = 1;
    internal const int DefaultPeekBigStep = 5;

    /// <summary>
    /// A step of peek, or the lines it shows, from the settings file: a whole number from 1 to 1000, else the default
    /// </summary>
    internal static int ParsePeekStep(string? value, int fallback) =>
        int.TryParse(value?.Trim(), out var lines) && lines is >= 1 and <= 1000 ? lines : fallback;

    /// <summary>
    /// How many changes of files "undo" can go back through (setting "shellundosteps", 10 to 200; only in the settings
    /// file, or with "set")
    /// </summary>
    internal static int UndoSteps { get; set; } = DefaultUndoSteps;

    internal const int DefaultUndoSteps = 50;

    // The fewest and the most steps the setting takes (all steps together keep at most ShellSession.UndoMaxBytes)
    internal const int MinUndoSteps = 10;
    internal const int MaxUndoSteps = 200;

    /// <summary>
    /// The steps of undo from the settings file: a whole number from 10 to 200, else the default. 1 to 9, which 1.5.7
    /// took (1 to 50), are 10: the fewest steps there are now, not the default
    /// </summary>
    internal static int ParseUndoSteps(string? value) =>
        !int.TryParse(value?.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
            out var steps) || steps is < 1 or > MaxUndoSteps
            ? DefaultUndoSteps
            : Math.Max(steps, MinUndoSteps);

    /// <summary>
    /// Blyant starts as the shell (setting "startshell", Configuration: General), as "blyant --shell" does, unless a
    /// file or directory is given (it is opened on the full screen) or "--screen"
    /// </summary>
    internal static bool StartInShell { get; set; }

    /// <summary>
    /// The directory the shell starts in when none is given (setting "shelldir"): a path ("~/projects" from the home
    /// directory), "last" (the one it was left in, kept as "shelllastdir"), or empty: the one Blyant is started in
    /// </summary>
    internal static string StartSetting { get; set; } = string.Empty;

    /// <summary>
    /// The directory the shell was left in (setting "shelllastdir"), where it starts with shelldir=last
    /// </summary>
    internal static string LastDirectory { get; set; } = string.Empty;

    /// <summary>
    /// $file when the shell was left (setting "shelllastfile"): $file again in the next shell typed in at a terminal,
    /// while the file is there
    /// </summary>
    internal static string LastFile { get; set; } = string.Empty;

    // The value of shelldir for the directory the shell was left in
    private const string Last = "last";

    /// <summary>
    /// The directory the shell starts in when none is given: that of the setting shelldir, else the current one. One
    /// that is not there (moved, a drive taken out, a path of another system) is told (note), and the current one is
    /// used: the shell always starts
    /// </summary>
    internal static string StartIn(string current, out string? note)
    {
        note = null;
        var set = StartSetting.Trim();
        var wanted = string.Equals(set, Last, StringComparison.OrdinalIgnoreCase) ? LastDirectory.Trim() : set;
        if (wanted.Length == 0) return current;
        if (FullPath(wanted, current) is { } path && Directory.Exists(path)) return path;
        note = Lang.F("The start directory {0} is not there: the shell starts here", wanted);
        return current;
    }

    // A path of the setting as a full path ("~" is the home directory), or null if it is not a path
    private static string? FullPath(string path, string current)
    {
        try
        {
            return Path.GetFullPath(FromHome(path), current);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// A path typed with "~" for the home directory ("~", "~/notes") as a path from it; another path as it is
    /// </summary>
    internal static string FromHome(string path)
    {
        if (path != "~" && !path.StartsWith("~/") && !path.StartsWith("~" + Path.DirectorySeparatorChar)) return path;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.Length == 1 ? home : Path.Combine(home, path[2..]);
    }

    /// <summary>
    /// Blyant is left from the full screen shown from the shell (":exit" in the editor opened with "open"): the shell is
    /// left too, as with "exit" (its directory kept with shelldir=last)
    /// </summary>
    internal static void OnExit()
    {
        if (Current is not { Terminal: true } session) return;
        EventLog.Add(DialogText.LogShell + ": left"); // English: the log
        RememberLast(session.Directory, session.WorkedFile);
    }

    /// <summary>
    /// The shell writes on lines, in the colors of the terminal (Blyant started as the shell, or the shell runs and
    /// the full screen is not shown from it): the colors of a scheme are not set
    /// </summary>
    internal static bool OnLines => Init.Plain || Prompts != null;

    /// <summary>
    /// A shell typed in at a terminal is left: with shelldir=last the directory it is left in is kept (shelllastdir),
    /// to start there next time, and $file (shelllastfile), to be $file again. Saved when one of them changed
    /// </summary>
    internal static void RememberLast(string directory, string? file = null)
    {
        // Only these two lines of the settings file are changed: a setting edited by hand in another program meanwhile
        // (without "reload") is kept. A path with a line break (Linux allows it) would break the file: not kept
        var changed = new List<(string Name, string Value)>();
        if (string.Equals(StartSetting.Trim(), Last, StringComparison.OrdinalIgnoreCase) && LastDirectory != directory &&
            Keepable(directory))
        {
            LastDirectory = directory;
            changed.Add(("shelllastdir", directory));
        }

        if (file != null && LastFile != file && Keepable(file))
        {
            LastFile = file;
            changed.Add(("shelllastfile", file));
        }

        if (changed.Count > 0) FileIo.SaveSettingValues(changed);

        static bool Keepable(string path) => path.IndexOfAny(['\r', '\n']) < 0;
    }

    /// <summary>
    /// ":shell" on the full screen: the main loop goes on as the shell, in this directory (the file: the last list,
    /// "open 1"). Null: none asked for
    /// </summary>
    internal sealed record Switch(string? Directory, string? File);

    internal static Switch? Requested { get; set; }

    /// <summary>
    /// "screen" in the shell: the directory the full screen goes on in (the file browser there), when the shell has
    /// ended. Null: none asked for (the shell ended with "exit")
    /// </summary>
    internal static string? ScreenRequested { get; private set; }

    /// <summary>
    /// The directory "screen" asked for (and it is forgotten), or null
    /// </summary>
    internal static string? TakeScreenRequest()
    {
        var directory = ScreenRequested;
        ScreenRequested = null;
        return directory;
    }

    /// <summary>
    /// The full screen is shown from the shell (the editor of a file opened with "open"): its boxes ask the questions
    /// </summary>
    internal static bool OnScreen { get; set; }

    /// <summary>
    /// The shell that asks the questions of Blyant on lines (ChoiceBox), or null: while the shell runs, and the full
    /// screen is not shown from it
    /// </summary>
    internal static ShellSession? Prompts => OnScreen ? null : Current;

    /// <summary>
    /// Run the shell on the terminal until it is left ("exit", or the end of the commands read from a file)
    /// </summary>
    /// <param name="directory">The directory it starts in</param>
    /// <param name="yes">Commands read from a file (not a terminal): their questions are answered yes</param>
    /// <returns>The exit code: 1 if a command read from a file failed, else 0</returns>
    internal static int Run(string directory, bool yes, string? file = null, string? note = null)
    {
        var terminal = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        var session = new ShellSession(Console.In, Console.Out, Console.Error, directory, terminal, yes)
        {
            StartNote = note,
            Clipboard = !Console.IsOutputRedirected,
            ClearScreen = terminal ? Console.Clear : null,
            WindowSize = terminal ? WindowSizeOfTerminal : null,
            ReadKey = terminal ? ReadPauseKey : null
        };
        if (terminal) session.ReadLinesWith(ReadPauseKey, WindowWidth);
        // (from the editor: the file is the last list, "open 1" opens it again)
        if (file != null && File.Exists(file)) session.ShowFirst(file);
        var code = Run(session);
        // (commands read from a file or a pipe do not change where the shell typed in starts)
        if (terminal) RememberLast(session.Directory, session.WorkedFile);
        return code;
    }

    // The size of the window, read for each page (the window can be made larger or smaller meanwhile); null when the
    // terminal does not tell it (then nothing is paged)
    private static (int Width, int Height)? WindowSizeOfTerminal()
    {
        try
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    // The width of the window (80 when the terminal does not tell it)
    private static int WindowWidth()
    {
        try
        {
            return Console.WindowWidth > 0 ? Console.WindowWidth : 80;
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException or InvalidOperationException)
        {
            return 80;
        }
    }

    // The key at the pause after a page, and the keys of the line read key by key (Ctrl+C as a key: it stops the output, and does not end Blyant)
    private static ConsoleKeyInfo ReadPauseKey()
    {
        var before = Console.TreatControlCAsInput;
        try
        {
            Console.TreatControlCAsInput = true;
            return Console.ReadKey(true);
        }
        finally
        {
            Console.TreatControlCAsInput = before;
        }
    }

    /// <summary>
    /// Run a shell (of the terminal, or of a test) until it is left
    /// </summary>
    internal static int Run(ShellSession session)
    {
        Current = session;
        ScreenRequested = null;
        try
        {
            var code = session.Run();
            ScreenRequested = session.ScreenDirectory;
            return code;
        }
        finally
        {
            Current = null;
            OnScreen = false;
        }
    }
}
