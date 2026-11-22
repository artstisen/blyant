namespace Blyant;

/// <summary>
/// A shell: the commands read from a reader (the terminal, a file of commands, or a test), and what they show written
/// to a writer, as lines of text. It keeps the directory it is in and the last list shown (its rows are numbered:
/// "open 3"). The commands are run in ShellCommands.cs (Execute, RunCommand), and written in the other parts of this
/// class (ShellFiles, ShellText, ShellLines, ShellRun ...); the questions of Blyant (ChoiceBox) are asked here, on
/// lines
/// </summary>
internal sealed partial class ShellSession
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _errors;

    /// <summary>
    /// A row of a list shown: a file or a directory (or a place that is neither, as a page of Blyant: no path)
    /// </summary>
    // (Found: the text a line of it was found by, with find -t: "peek 3" shows that line, marked)
    internal sealed record Item(string Path, bool IsDirectory, (int Line, int Column)? At = null, string? Found = null);

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
            // The file of "again edit" ($cmd): not a file worked on. The last list, $file and $line stay as they
            // were (a $line set by the command is put back when it ends)
            if (value is [{ IsDirectory: false } scratch] && IsCommandsFile(scratch.Path))
            {
                _commandsTouched = true;
                return;
            }

            // (a line found is a row of find -t only: a command that works on the file makes it a file again)
            _lastList = value.Select(item => item.Found is null ? item : item with { Found = null }).ToList();
            _listShown = true;
            // One file: one a command worked on (peek, insert, new ...): $file, and $line the line it worked on. A
            // command on the same file without a line (format, trim, undo) keeps the line; another file without one
            // (made, copied) has none
            if (value is [{ IsDirectory: false } one])
            {
                var path = Path.GetFullPath(one.Path);
                if (one.At is { } at) _line = at.Line;
                else if (_file is null || !FileOperations.NameComparer.Equals(_file, path)) _line = null;
                _file = path;
            }
        }
    }

    // A list shown (ls, fav, find ...): the last list, but $file stays the file worked on last
    private void ShowRows(List<Item> rows)
    {
        _lastList = rows;
        _listShown = true;
    }

    // $file: the file the last command worked on (null: none yet)
    private string? _file;

    // $line: the line of $file the last command worked on (null: none, as after "file plot.md"; 0: the top of a file
    // made with "new"). With $file it is "@": "word @ 9 replace wish"
    private int? _line;

    // The line of $file for $line and "@": the line the last command worked on, else where the cursor was in the
    // editor when the file was closed last (the favorites and the history keep it, as the editor and "resume" take
    // it: "file 2" of "recent"), else none
    private int? FileLine
    {
        get
        {
            if (_file is null) return null;
            return _line ?? FileExplorer.CaretOfFile(_file)?.Line;
        }
    }

    /// <summary>
    /// $line as the commands set it (null: none, and $line is where the editor left the cursor), kept with $file when
    /// the shell is left (shelllastline)
    /// </summary>
    internal int? WorkedLine => _line;

    // The files the command running now has changed, for the history (see LatestChanged)
    private List<string> _changedFiles = new();

    /// <summary>
    /// $file: the file the last command worked on (null: none yet), kept when the shell is left (shelllastfile)
    /// </summary>
    internal string? WorkedFile => _file;

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
        var completion = new ShellCompletion(() => Directory, PathOf) { FileOf = CompletionFile };
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
        // $pwd and $file: of this shell
        ShellRegisters.DirectorySource = () => Directory;
        ShellRegisters.FileSource = () => _file;
        ShellRegisters.LineSource = () => FileLine;
        ShellRegisters.TagSource = () => _tag;
        ShellRegisters.PreviousSource = () => _previousDirectory;
        ShellRegisters.CommandsSource = () => CommandsFileNow;
        Terminal = terminal;
        _yes = yes;
        // $file of the shell left last, while the file is there (typed in at a terminal: not for commands from a file,
        // a pipe or --run, which do only what they say)
        if (terminal && Shell.LastFile.Length > 0 && Path.IsPathFullyQualified(Shell.LastFile) &&
            File.Exists(Shell.LastFile))
        {
            _file = Path.GetFullPath(Shell.LastFile);
            // $line too, while the file has that line (a file made shorter since: the line where the editor left the
            // cursor, as before)
            if (Shell.LastLine is { } kept && LineCount(_file).Count is { } count && kept <= count) _line = kept;
        }

        // The window of the last peek of the shell left last, so "." and "+" go on from it
        if (terminal) RestoreLastPeek();
    }

    // The logo of the start page (the setting logo), without colors, under "- Shell mode -", when the shell starts at
    // a terminal (the setting shelllogo). As plain lines, with a blank line above and below it (easier to read): the
    // blank rows above and below the logo, and the blanks it starts with, left out. Not in a window narrower than the
    // logo: its rows are not broken
    private void ShowLogo()
    {
        var rows = Logo.Current.AllRows.Select(row => row.TrimEnd()).ToList();
        while (rows.Count > 0 && rows[0].Length == 0) rows.RemoveAt(0);
        while (rows.Count > 0 && rows[^1].Length == 0) rows.RemoveAt(rows.Count - 1);
        if (rows.Count == 0) return;
        var indent = rows.Where(row => row.Length > 0).Min(row => row.Length - row.TrimStart(' ').Length);
        rows = rows.Select(row => row.Length >= indent ? row[indent..] : row).ToList();
        // (the last column of the window is not used)
        if (WindowSize?.Invoke() is { } size && size.Width - 1 < rows.Max(TextMetrics.WidthOf)) return;
        Line(Lang.T("- Shell mode -"));
        Line();
        foreach (var row in rows) Line(row);
        Line();
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
            if (Shell.Logo) ShowLogo();
            Line(Lang.F("{0} {1}: help lists the commands, exit leaves", DialogText.ApplicationName,
                DialogText.ApplicationVersion));
        }

        if (StartNote is not null)
        {
            if (Terminal) Line(StartNote);
            else _errors.WriteLine(StartNote);
        }

        // The commands typed in the shells before, for Up, Down and again (only at a terminal: typed)
        if (Terminal) ShellHistory.Load();
        while (true)
        {
            // (the end of the input: Ctrl+D, Ctrl+Z, the end of the file: leave)
            if (ReadAtPrompt(Prompt(), false) is not { } line) break;

            // (typed at the prompt: kept for Up and Down, and "again"; read from a file or a pipe: not. Typed with a
            // blank in front: not kept, as in bash)
            _typed = Terminal;
            _notKept = line.Length > 0 && char.IsWhiteSpace(line[0]);
            if (!RunLine(line)) break;
            if (_inputExpected) SkipInputText();
        }

        EventLog.Add(DialogText.LogShell + ": left"); // English: the log
        return !Terminal && Failures > 0 ? 1 : 0;
    }

    // The line run now was typed at the prompt (in a terminal): it is kept for Up and Down, and for "again"
    private bool _typed;

    // In a run: the next line of the script, for input (null: the end of it)
    private Func<string?>? _runNext;

    // A line of text for input: from the script of a run (its lines, as LinesOfText made them), or read as a command
    // is read, but without Tab and Up (it is text) and kept nowhere. Null at the end of the input (Ctrl+D, the end of
    // a file or a pipe, the end of the script)
    private string? ReadText(string prompt) => _runNext is not null ? _runNext() : ReadAtPrompt(prompt, true);

    // Nothing read yet (the first line of a file of commands can start with a byte order mark)
    private bool _firstRead = true;

    // An input from a file of commands or a pipe has not read its lines of text: it was refused (no file, a place
    // that is not one)
    private bool _inputExpected;

    // The lines of text of an input that was refused, from a file of commands or a pipe, skipped up to the line with
    // only "." (told), so none of them is run as a command. (In a run the script stops at the input refused)
    private void SkipInputText()
    {
        _inputExpected = false;
        Error(Lang.T("The lines of text after it are skipped, up to the line with only ."));
        while (ReadText(string.Empty) is { } line && !IsInputEnd(line))
        {
        }
    }

    // The line reader is used: at a terminal, with the setting shellcompletion on (followed at once)
    private bool LineEditing => _lineReader is not null && Shell.Completion;

    // A line read after a prompt: a command ("text" false: Tab and Up complete and recall), or a line of text for
    // input. With the line reader at a terminal, or from the input (a file, a pipe, or a terminal without it). Ctrl+C
    // drops the line typed, and another is read; null at the end of the input (Ctrl+D, Ctrl+Z, the end of the file)
    private string? ReadAtPrompt(string prompt, bool text) => ReadAtPrompt(prompt, text, null, out _);

    // A line of input taken back to edit (.e, .u): read with the text to start from. Null at the end of the input, or
    // after Ctrl+C ("dropped": the line stays as it was)
    private string? ReadEdited(string prompt, string start, out bool dropped) =>
        ReadAtPrompt(prompt, true, start, out dropped);

    // (a line to edit: "start", which Ctrl+C drops, ending the reading; without line editing it is shown above the
    // prompt, to be typed again)
    private string? ReadAtPrompt(string prompt, bool text, string? start, out bool dropped)
    {
        dropped = false;
        while (true)
        {
            // (a Ctrl+C pressed while the last command ran, or at a question, is not taken for this prompt)
            if (Terminal) Tools.TakeCancelKey();
            string? line;
            if (LineEditing)
            {
                line = _lineReader!.Read(prompt, text, start ?? string.Empty);
                if (_lineReader.Dropped)
                {
                    if (start is null) continue;
                    dropped = true;
                    return null;
                }

                // (the end of the input: the line reader has ended the line, and took Ctrl+C itself, as a key)
                if (line is null) return null;
            }
            else
            {
                if (Terminal)
                {
                    if (start is not null) _output.WriteLine(start);
                    _output.Write(prompt);
                    _output.Flush();
                }

                line = _input.ReadLine();
                // (a file of commands saved with a byte order mark, as Notepad and PowerShell save it)
                if (_firstRead && line is not null) line = line.TrimStart('\uFEFF');
            }

            _firstRead = false;
            if (line is not null) return line;
            // (Ctrl+C can end the reading of a line before Blyant is told of it: it is waited for a moment)
            if (Terminal && SpinWait.SpinUntil(Tools.TakeCancelKey, CancelKeyWait))
            {
                _output.WriteLine();
                if (start is null) continue;
                dropped = true;
                return null;
            }

            if (Terminal) _output.WriteLine();
            return null;
        }
    }

    // The line run now was typed with a blank in front: not kept, neither for Up and "again" nor in the file
    private bool _notKept;

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
        if (!Terminal || !Shell.Paging || ReadKey is null || WindowSize?.Invoke() is not { } size) return null;
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
    private void List(IReadOnlyList<(Item Item, string Text)> rows, string empty) => Filtered(rows, _ => true, empty);

    // A numbered row: the number at the right of a column as wide as the largest number and two more, then the text
    // (indented: a folder in a tree)
    // Lines of a file shown numbered, first to last (from 1), the numbers as wide as the last
    private void Rows(IReadOnlyList<string> lines, int first, int last)
    {
        var width = last.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        for (var i = first; i <= last; i++) Row(i, width, lines[i - 1]);
    }

    private void Row(int number, int width, string text, int indent = 0) =>
        Line(new string(' ', indent) + number.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width + 2) + "  " + text);

    /// <summary>
    /// Files and directories as a list shows them: by name ("/" after a directory), with the folder they are in after
    /// a name that two rows have ("README.md (novel)"; more folders when those are alike too: "storm.md
    /// (part-1/drafts)"), " *" after a favorite (of the open list, as the full screen
    /// marks them; not "marked": the favorites themselves), and "(missing)" when one is not there. "full": the full
    /// paths, without a mark ("fav -l": for scripts). "info 3" tells the rest
    /// </summary>
    internal static List<string> Names(IReadOnlyList<(string Path, bool IsDirectory)> entries, bool full,
        bool marked = true, bool withFolder = false)
    {
        if (full) return entries.Select(entry => Shown(entry.Path, entry.IsDirectory)).ToList();
        if (marked) FileFavorites.Load();
        // "withFolder": the folder before the name ("Book/chapter-3.md"; "recent -f"); two rows alike then get the
        // folder above that after them ("Book/a.md (novel)")
        var names = entries.Select(entry => withFolder ? FolderAndName(entry.Path) : NameOf(entry.Path)).ToList();
        // The folders after a name that more rows have: as few as tell them apart (names the file system tells
        // apart are not alike: "Plot.md" and "plot.md" on Linux)
        var told = new string?[entries.Count];
        foreach (var group in Enumerable.Range(0, entries.Count)
                     .GroupBy(i => names[i], FileOperations.NameComparer).Where(group => group.Count() > 1))
        {
            // (the folders above the one shown: with "withFolder" the folder before the name is one of them)
            var rows = group.ToList();
            var skip = withFolder ? 1 : 0;
            for (var depth = 1; ; depth++)
            {
                var folders = rows.Select(i => Folders(entries[i].Path, skip, depth)).ToList();
                var distinct = folders.Distinct(FileOperations.NameComparer).Count() == rows.Count;
                // (no more folders above: the folder they are in, whole)
                var ended = rows.All(i => Folders(entries[i].Path, skip, depth + 1) == folders[rows.IndexOf(i)]);
                if (distinct || ended)
                {
                    for (var k = 0; k < rows.Count; k++)
                    {
                        told[rows[k]] = distinct ? folders[k]
                            : Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(entries[rows[k]].Path));
                    }

                    break;
                }
            }
        }

        return entries.Select((entry, i) =>
        {
            var there = entry.IsDirectory ? System.IO.Directory.Exists(entry.Path) : FileOperations.IsFileThere(entry.Path);
            var shown = names[i] + (entry.IsDirectory ? "/" : string.Empty);
            if (told[i] is { Length: > 0 } folders) shown += " (" + folders + ")";
            if (marked) shown += FavoriteMark(entry.Path);
            return shown + (there ? string.Empty : " " + DialogText.FavoriteMissing);
        }).ToList();

        // (a root, as "C:\" or "/", has no name: its path)
        static string NameOf(string path)
        {
            var trimmed = Path.TrimEndingDirectorySeparator(path);
            var name = Path.GetFileName(trimmed);
            return name.Length > 0 ? name : trimmed;
        }

        // The names of the folders above a path, "depth" of them after leaving out "skip" of the nearest, from the
        // outer one in, with "/" between ("part-1/drafts"); fewer at the root (whose name is its path)
        static string Folders(string path, int skip, int depth)
        {
            var parts = new List<string>();
            var folder = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));
            for (var i = 0; folder is not null && parts.Count < depth; i++)
            {
                var above = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder));
                // (the root, "C:\" or "/", has no name to show: there the whole folder is told instead)
                if (above is null) break;
                if (i >= skip) parts.Insert(0, NameOf(folder));
                folder = above;
            }

            return string.Join("/", parts);
        }

        // "Book/chapter-3.md" (with "/" on all systems); a file at the root of a drive: its path ("C:\notes.md")
        static string FolderAndName(string path)
        {
            var trimmed = Path.TrimEndingDirectorySeparator(path);
            var parent = Path.GetDirectoryName(trimmed);
            var folder = parent is null ? string.Empty : Path.GetFileName(Path.TrimEndingDirectorySeparator(parent));
            return folder.Length > 0 ? folder + "/" + Path.GetFileName(trimmed) : trimmed;
        }
    }

    /// <summary>
    /// " *" after the name of a favorite (of the open list: FileFavorites read by the caller), as the full screen
    /// marks them in the file browser, the history and the search results; else nothing
    /// </summary>
    internal static string FavoriteMark(string path) => FileFavorites.Contains(path) ? " *" : string.Empty;

    // A text made as wide as the widest of a column (by the columns it takes on the screen), for what follows it
    private static string Padded(string text, int width) =>
        text + new string(' ', Math.Max(0, width - TextMetrics.WidthOf(text)));

    /// <summary>
    /// "-t text" at the end of what follows a list command ("recent -n -t storm", "fav -t # *"): what comes before it,
    /// and the text (in quotes: without them; "" when -t has none). Null text when there is no -t
    /// </summary>
    internal static (string Before, string? Text) TextOption(string argument)
    {
        var trimmed = argument.TrimEnd();
        var at = trimmed == "-t" || trimmed.StartsWith("-t ", StringComparison.Ordinal) ? 0
            : trimmed.IndexOf(" -t ", StringComparison.Ordinal) is var inside and >= 0 ? inside + 1
            : trimmed.EndsWith(" -t", StringComparison.Ordinal) ? trimmed.Length - 2
            : -1;
        if (at < 0) return (argument, null);
        var text = trimmed.Length > at + 3 ? trimmed[(at + 3)..].Trim() : string.Empty;
        return (trimmed[..at].TrimEnd(), Unquoted(text) ?? text);
    }

    // "book": only "-l" can follow (true: given). Null (and told) for another word
    private bool? OnlyLong(string argument)
    {
        var (full, others) = LongList(argument);
        if (others.Length == 0) return full;
        Error(Lang.F("Not taken: {0} (only -l, for the full paths)", others));
        return null;
    }

    // The words that start with "-" at the start of the words after a command ("-l -s filter draft": "-l" and "-s"),
    // and what follows them
    private static (List<string> Options, string After) LeadingOptions(string argument)
    {
        var options = new List<string>();
        var text = argument.Trim();
        while (text.StartsWith('-'))
        {
            var word = text.Split(' ', 2)[0];
            options.Add(word);
            text = text[word.Length..].TrimStart();
        }

        return (options, text);
    }

    // The size of a file as the file browser shows it ("12.4 kB"), or "-" when it is not there
    private static string SizeOf(string path)
    {
        try
        {
            if (FileOperations.IsFileThere(path)) return FileSizeFormatter.FormatSize(FileOperations.FileLength(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // (not readable: as one that is not there)
        }

        return "-";
    }

    // "-l" in the words after a list command: the full paths. The words without it
    private static (bool Full, string Others) LongList(string argument)
    {
        var words = Words(argument);
        var full = words.RemoveAll(word => word == "-l") > 0;
        return (full, string.Join(' ', words));
    }

    // A file or directory as listed: with "/" after a directory, and "(missing)" when it is not there
    private static string Shown(string path, bool isDirectory)
    {
        // (a link to a file that is gone is missing too: on Linux and macOS File.Exists is true for it)
        var there = isDirectory ? System.IO.Directory.Exists(path) : FileOperations.IsFileThere(path);
        return (isDirectory ? Path.TrimEndingDirectorySeparator(path) + "/" : path) +
               (there ? string.Empty : " " + DialogText.FavoriteMissing);
    }

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
                ? Lang.F("(not asked: {0}, --yes given)", choices[0])
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
        if (typed is null) return -1;
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
}
