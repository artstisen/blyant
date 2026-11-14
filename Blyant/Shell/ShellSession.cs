namespace Blyant;

/// <summary>
/// A shell: the commands read from a reader (the terminal, a file of commands, or a test), and what they show written
/// to a writer, as lines of text. It keeps the directory it is in and the last list shown (its rows are numbered:
/// "open 3"). The commands are in ShellCommands.cs; the questions of Blyant (ChoiceBox) are asked here, on lines
/// </summary>
internal sealed partial class ShellSession
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _errors;

    /// <summary>
    /// A row of a list shown: a file or a directory (or a place that is neither, as a page of Blyant: no path)
    /// </summary>
    internal sealed record Item(string Path, bool IsDirectory, (int Line, int Column)? At = null);

    /// <summary>
    /// The shell runs in a terminal: a prompt is shown, questions are asked, and files can be opened in the editor.
    /// Else the commands are read from a file or a pipe: no prompt, and a question gets the safe answer (no), or yes
    /// with "--yes"
    /// </summary>
    internal bool Terminal { get; }

    /// <summary>
    /// The output goes to a terminal, so "clip" can send to the clipboard through it (OSC 52). False for output to a
    /// file or a program, where the sequence would end up in the output: "clip" then uses the paste bin only
    /// </summary>
    internal bool Clipboard { get; init; }

    private readonly bool _yes;

    /// <summary>
    /// The directory the shell is in (a path that is not full is taken from it)
    /// </summary>
    internal string Directory { get; private set; }

    /// <summary>
    /// The rows of the last list shown (a number given to a command is a row of it, from 1)
    /// </summary>
    internal List<Item> LastList
    {
        get => _lastList;
        private set
        {
            _lastList = value;
            _listShown = true;
        }
    }

    private List<Item> _lastList = new();
    private bool _listShown; // A list has been shown (its rows can be empty)

    /// <summary>
    /// Clears the screen of the terminal ("clear"): set by the shell of the terminal (a test, or commands read from a
    /// file, have no screen to clear)
    /// </summary>
    internal Action? ClearScreen { get; init; }

    /// <summary>
    /// Told when the shell starts (the start directory set is not there)
    /// </summary>
    internal string? StartNote { get; init; }

    /// <summary>
    /// The size of the window of the terminal (columns, rows), for the pages of what a command shows: null (or no
    /// rows) when it is not known, and nothing is paged. Set by the shell of the terminal
    /// </summary>
    internal Func<(int Width, int Height)?>? WindowSize { get; init; }

    /// <summary>
    /// Reads the key at the pause after a page (null: no pages). Set by the shell of the terminal
    /// </summary>
    internal Func<ConsoleKeyInfo>? ReadKey { get; init; }

    /// <summary>
    /// "screen": the shell ended to go on on the full screen, in this directory (null: it was left with "exit")
    /// </summary>
    internal string? ScreenDirectory { get; private set; }

    /// <summary>
    /// A file as the last list, without showing it (the file the shell was started from, in the editor)
    /// </summary>
    internal void ShowFirst(string file) => LastList = new List<Item> { new(file, false) };

    private ShellLineReader? _lineReader; // The line read key by key, with Tab and Up (null: a line as typed)

    /// <summary>
    /// Read the lines key by key (setting "shellcompletion"): Tab completes, Up and Down bring back the commands typed
    /// before, and the keys of the command lines of the full screen edit the line
    /// </summary>
    internal void ReadLinesWith(Func<ConsoleKeyInfo> readKey, Func<int> width)
    {
        var completion = new ShellCompletion(() => Directory, PathOf);
        _lineReader = new ShellLineReader(readKey, _output, width, completion.Next);
    }

    private int _rows; // The rows written on the page (since the command was typed, or the last pause)
    private bool _stopped; // "q" at a pause: the rest of what the command shows is not written

    /// <summary>
    /// The number of commands that failed (from a file, the exit code is 1 when there were any)
    /// </summary>
    internal int Failures { get; private set; }

    internal ShellSession(TextReader input, TextWriter output, TextWriter errors, string directory, bool terminal,
        bool yes = false)
    {
        _input = input;
        _output = output;
        _errors = errors;
        Directory = Path.GetFullPath(directory);
        Terminal = terminal;
        _yes = yes;
    }

    /// <summary>
    /// Read and run the commands until "exit", or the end of the input
    /// </summary>
    /// <returns>The exit code: 1 if a command read from a file failed, else 0</returns>
    internal int Run()
    {
        EventLog.Add(DialogText.LogShell + ": started"); // English: the log
        ShellRegisters.Load();
        if (Terminal)
        {
            Line(Lang.F("{0} {1}: help lists the commands, exit leaves", DialogText.ApplicationName,
                DialogText.ApplicationVersion));
        }

        if (StartNote != null)
        {
            if (Terminal) Line(StartNote);
            else _errors.WriteLine(StartNote);
        }

        var first = true;
        while (true)
        {
            string? line;
            // (a Ctrl+C pressed while the last command ran, or at a question, is not taken for this prompt)
            if (Terminal) Tools.TakeCancelKey();
            // (the setting shellcompletion is followed at once: "set shellcompletion false")
            if (_lineReader != null && Shell.Completion)
            {
                line = _lineReader.Read(Prompt());
                // (kept for Up and Down)
                if (!string.IsNullOrWhiteSpace(line)) CommandHistory.Add(CommandContext.Shell, line);
            }
            else
            {
                if (Terminal)
                {
                    _output.Write(Prompt());
                    _output.Flush();
                }

                line = _input.ReadLine();
                // (a file of commands saved with a byte order mark, as Notepad and PowerShell save it)
                if (first && line != null) line = line.TrimStart('\uFEFF');
            }

            first = false;
            if (line == null)
            {
                // Ctrl+C at the prompt: a new prompt; the end of the input (Ctrl+D, Ctrl+Z, the end of the file):
                // leave. (Ctrl+C can end the reading of a line before Blyant is told of it: it is waited for a moment)
                if (Terminal && SpinWait.SpinUntil(Tools.TakeCancelKey, CancelKeyWait))
                {
                    _output.WriteLine();
                    continue;
                }

                if (Terminal) _output.WriteLine();
                break;
            }

            if (!RunLine(line)) break;
        }

        EventLog.Add(DialogText.LogShell + ": left"); // English: the log
        return !Terminal && Failures > 0 ? 1 : 0;
    }

    // How long Ctrl+C is waited for after the reading of a line ended without one (ms)
    private const int CancelKeyWait = 100;

    // A command typed, run: one that fails in a way no command expects (a bug) is told, and the shell goes on
    private bool RunLine(string line)
    {
        try
        {
            return Execute(line);
        }
        catch (Exception e) when (e is not NavigationException)
        {
            Error(Lang.F("The command failed: {0}", e.Message));
            // English: the log (the kind of failure, never what was typed after the command)
            EventLog.Add(DialogText.LogShell + ": " + CommandLine.Name(line).ToLowerInvariant() + " failed: " +
                         e.GetType().Name);
            return true;
        }
    }

    /// <summary>
    /// The prompt: the directory (in the home directory: from "~")
    /// </summary>
    internal string Prompt() => "blyant " + ShortPath(Directory) + "> ";

    // A path in the home directory from "~" ("~/projects/novel")
    private static string ShortPath(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length == 0) return path;
        if (FileOperations.SamePath(path, home)) return "~";
        return FileOperations.IsInside(path, home)
            ? "~" + Path.DirectorySeparatorChar + Path.GetRelativePath(home, path)
            : path;
    }

    #region Output

    // A line of what a command shows. In a terminal a long output comes in pages (setting "shellpaging"): when the
    // window is full, the shell waits for a key (Enter or Space: the next page, q: the rest is not shown)
    private void Line(string text = "")
    {
        if (_stopped) return;
        text = Printable(text);
        if (PageSize() is { } size)
        {
            // (a line wider than the window takes more rows; so do the lines of a text of several)
            var rows = text.Split('\n').Sum(part => Math.Max(1, (TextMetrics.WidthOf(part) + size.Width - 1) / size.Width));
            if (_rows > 0 && _rows + rows > size.Height - 1 && !Pause(size.Width))
            {
                _stopped = true;
                return;
            }

            _rows += rows;
        }

        _output.WriteLine(text);
    }

    // The size of a page: in a terminal, with paging on (setting "shellpaging"), in a window of a known size. Null:
    // no pages (from a file or a pipe, paging off, or a terminal that does not tell its size)
    private (int Width, int Height)? PageSize()
    {
        if (!Terminal || !Shell.Paging || ReadKey == null || WindowSize?.Invoke() is not { } size) return null;
        return size.Width > 0 && size.Height > 2 ? size : null;
    }

    // The pause after a page, on the last row: true to go on (Enter, Space or another key), false to stop (q, Esc,
    // Ctrl+C). The line of the pause is written over by what comes next
    private bool Pause(int width)
    {
        // (on one row: a narrow window, or a long translation, gets it cut)
        var text = Cut(Lang.T("-- More: Enter or Space next page, q stop --"), width - 1);
        _output.Write(text);
        _output.Flush();
        var key = ReadKey!();
        _output.Write("\r" + new string(' ', Math.Min(TextMetrics.WidthOf(text), Math.Max(0, width - 1))) + "\r");
        _output.Flush();
        _rows = 0;
        return key.Key is not (ConsoleKey.Q or ConsoleKey.Escape) &&
               !(key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control));
    }

    // A new page starts: a command typed, or an answer (the rows before it have been read)
    private void NewPage(int rows = 1)
    {
        _rows = rows;
        _stopped = false;
    }

    /// <summary>
    /// What went wrong, told by another part of Blyant (a file it could not write) while the shell runs
    /// </summary>
    internal void Tell(string text) => Error(text);

    // What went wrong: a command that failed (on the error output, so a script can tell it from what is shown)
    private void Error(string text)
    {
        Failures++;
        _errors.WriteLine(Printable(text));
    }

    /// <summary>
    /// A text as it is written: control characters (of a file shown, a name) as "?", so a file cannot move the cursor,
    /// change the colors or the title, or set the clipboard of the terminal by being shown (escape sequences). Line
    /// breaks and tabs are kept
    /// </summary>
    internal static string Printable(string text)
    {
        if (!text.Any(IsControl)) return text;
        return string.Concat(text.Select(c => IsControl(c) ? '?' : c));

        static bool IsControl(char c) => char.IsControl(c) && c is not ('\n' or '\t');
    }

    /// <summary>
    /// A text cut to a number of columns (whole characters)
    /// </summary>
    internal static string Cut(string text, int columns)
    {
        while (text.Length > 0 && TextMetrics.WidthOf(text) > Math.Max(0, columns))
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        return text;
    }

    /// <summary>
    /// Show a list: each row numbered from 1 (the numbers lined up), and kept as the last list
    /// </summary>
    private void List(IReadOnlyList<(Item Item, string Text)> rows, string empty)
    {
        LastList = rows.Select(row => row.Item).ToList();
        if (rows.Count == 0)
        {
            Line(empty);
            return;
        }

        var width = rows.Count.ToString().Length;
        for (var i = 0; i < rows.Count; i++)
        {
            Line((i + 1).ToString().PadLeft(width + 2) + "  " + rows[i].Text);
        }
    }

    // A file or directory as listed: with "/" after a directory, and "(missing)" when it is not there
    private static string Shown(string path, bool isDirectory)
    {
        var there = isDirectory ? System.IO.Directory.Exists(path) : File.Exists(path);
        return (isDirectory ? Path.TrimEndingDirectorySeparator(path) + "/" : path) +
               (there ? string.Empty : " " + DialogText.FavoriteMissing);
    }

    #endregion

    #region Questions

    // The questions of Blyant (ChoiceBox) while the shell runs: on lines. From a file (not a terminal) nothing is
    // asked: the answer is no (the box closed), or yes with "--yes"

    /// <summary>
    /// Pick one of a few choices: their numbers (or keys) are shown, and the number, the key or the word typed picks
    /// one; Enter alone the one selected from the start. Guarded (deleting): only the first choice typed in full
    /// answers yes
    /// </summary>
    /// <returns>The number of the choice, or -1 for none (no)</returns>
    internal int Pick(string caption, string question, string note, IReadOnlyList<string> choices,
        IReadOnlyList<string> keys, int selected, bool guarded)
    {
        if (!Terminal)
        {
            // On the error output, as it is not what the command shows: and when nothing is done, the command failed
            var answer = _yes && choices.Count > 0 ? 0 : -1;
            Asked(Question(caption, question), note, answer == 0
                ? Lang.F("(not asked: {0}, as --yes tells)", choices[0])
                : Lang.T("(not asked: the commands are not typed, so nothing is done)"), answer < 0);
            return answer;
        }

        // (a question after "q" at a pause is shown)
        NewPage(0);
        Line(Question(caption, question));
        if (note.Length > 0) Line("  " + note);

        // "1 Yes  2 No", or with the keys: "Y Yes  N No"; guarded: the word to type
        var shown = guarded
            ? Lang.F("Type {0} to go ahead (anything else: no)", choices[0])
            : string.Join("  ", choices.Select((choice, i) =>
                (keys.Count > i && keys[i].Length > 0 ? keys[i] : (i + 1).ToString()) + " " + choice +
                (i == selected ? "*" : string.Empty)));
        _output.Write("  " + shown + ": ");
        _output.Flush();
        var typed = _input.ReadLine()?.Trim();
        NewPage();
        if (typed == null) return -1;
        if (guarded) return string.Equals(typed, choices[0], StringComparison.CurrentCultureIgnoreCase) ? 0 : -1;
        if (typed.Length == 0) return selected;
        for (var i = 0; i < choices.Count; i++)
        {
            if (typed == (i + 1).ToString() ||
                (keys.Count > i && keys[i].Length > 0 && string.Equals(typed, keys[i], StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(typed, choices[i], StringComparison.CurrentCultureIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// A message (and a note): written, nothing waited for
    /// </summary>
    internal void Message(string caption, string message, string note)
    {
        Line(Question(caption, message));
        if (note.Length > 0) Line("  " + note);
    }

    // The question (or message) with the caption of its box in front
    private static string Question(string caption, string question) =>
        caption.Length > 0 ? Lang.F("{0}: {1}", caption, question) : question;

    // A question not asked (the commands are read from a file): written on the error output, with what was done
    private void Asked(string question, string note, string answer, bool failed)
    {
        if (failed) Failures++;
        _errors.WriteLine(Printable(question));
        if (note.Length > 0) _errors.WriteLine("  " + Printable(note));
        _errors.WriteLine("  " + answer);
    }

    /// <summary>
    /// Ask for a text (a name): null if nothing is typed (or the commands are read from a file)
    /// </summary>
    internal string? AskText(string caption, string prompt, string label)
    {
        if (!Terminal)
        {
            Asked(Question(caption, prompt), string.Empty,
                Lang.T("(not asked: the commands are not typed, so nothing is done)"), true);
            return null;
        }

        NewPage(0);
        Line(Question(caption, prompt));

        _output.Write("  " + label + " ");
        _output.Flush();
        var typed = _input.ReadLine()?.Trim();
        NewPage();
        return string.IsNullOrEmpty(typed) ? null : typed;
    }

    #endregion
}
