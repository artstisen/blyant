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
    /// The directory the shell starts in when none is given (setting "shelldir"): a path ("~/projects" from the home
    /// directory), "last" (the one it was left in, kept as "shelllastdir"), or empty: the one Blyant is started in
    /// </summary>
    internal static string StartSetting { get; set; } = string.Empty;

    /// <summary>
    /// The directory the shell was left in (setting "shelllastdir"), where it starts with shelldir=last
    /// </summary>
    internal static string LastDirectory { get; set; } = string.Empty;

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
        if (path == "~" || path.StartsWith("~/") || path.StartsWith("~" + Path.DirectorySeparatorChar))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = path.Length == 1 ? home : Path.Combine(home, path[2..]);
        }

        try
        {
            return Path.GetFullPath(path, current);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// With shelldir=last: the directory the shell is left in is kept (shelllastdir), to start there next time
    /// </summary>
    internal static void RememberLast(string directory)
    {
        if (!string.Equals(StartSetting.Trim(), Last, StringComparison.OrdinalIgnoreCase)) return;
        LastDirectory = directory;
        FileIo.SaveSettings();
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
            ClearScreen = terminal ? Console.Clear : null,
            WindowSize = terminal ? WindowSizeOfTerminal : null,
            ReadKey = terminal ? ReadPauseKey : null
        };
        if (terminal && Completion) session.ReadLinesWith(ReadPauseKey, WindowWidth);
        // (from the editor: the file is the last list, "open 1" opens it again)
        if (file != null && File.Exists(file)) session.ShowFirst(file);
        var code = Run(session);
        RememberLast(session.Directory);
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
