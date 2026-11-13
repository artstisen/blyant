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
        if (Terminal)
        {
            Line(Lang.F("{0} {1}: help lists the commands, exit leaves", DialogText.ApplicationName,
                DialogText.ApplicationVersion));
        }

        while (true)
        {
            if (Terminal)
            {
                _output.Write(Prompt());
                _output.Flush();
            }

            var line = _input.ReadLine();
            if (line == null)
            {
                // Ctrl+C at the prompt: a new prompt; the end of the input (Ctrl+D, Ctrl+Z, the end of the file):
                // leave
                if (Terminal && Tools.TakeCancelKey())
                {
                    _output.WriteLine();
                    continue;
                }

                if (Terminal) _output.WriteLine();
                break;
            }

            if (!Execute(line)) break;
        }

        EventLog.Add(DialogText.LogShell + ": left"); // English: the log
        return !Terminal && Failures > 0 ? 1 : 0;
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

    // A line of what a command shows
    private void Line(string text = "") => _output.WriteLine(text);

    // What went wrong: a command that failed (on the error output, so a script can tell it from what is shown)
    private void Error(string text)
    {
        Failures++;
        _errors.WriteLine(text);
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
        Line(caption.Length > 0 ? caption + ": " + question : question);
        if (note.Length > 0) Line("  " + note);
        if (!Terminal)
        {
            var answer = _yes && choices.Count > 0 ? 0 : -1;
            Line("  " + (answer == 0
                ? Lang.F("(not asked: {0}, as --yes tells)", choices[0])
                : Lang.T("(not asked: the commands are not typed, so nothing is done)")));
            return answer;
        }

        // "1 Yes  2 No", or with the keys: "Y Yes  N No"; guarded: the word to type
        var shown = guarded
            ? Lang.F("Type {0} to go ahead (anything else: no)", choices[0])
            : string.Join("  ", choices.Select((choice, i) =>
                (keys.Count > i && keys[i].Length > 0 ? keys[i] : (i + 1).ToString()) + " " + choice +
                (i == selected ? "*" : string.Empty)));
        _output.Write("  " + shown + ": ");
        _output.Flush();
        var typed = _input.ReadLine()?.Trim();
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
        Line(caption.Length > 0 ? caption + ": " + message : message);
        if (note.Length > 0) Line("  " + note);
    }

    /// <summary>
    /// Ask for a text (a name): null if nothing is typed (or the commands are read from a file)
    /// </summary>
    internal string? AskText(string caption, string prompt, string label)
    {
        Line(caption.Length > 0 ? caption + ": " + prompt : prompt);
        if (!Terminal)
        {
            Line("  " + Lang.T("(not asked: the commands are not typed, so nothing is done)"));
            return null;
        }

        _output.Write("  " + label + " ");
        _output.Flush();
        var typed = _input.ReadLine()?.Trim();
        return string.IsNullOrEmpty(typed) ? null : typed;
    }

    #endregion
}
