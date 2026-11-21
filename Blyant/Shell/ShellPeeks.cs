using System.Globalization;

namespace Blyant;

/// <summary>
/// "peek save", "peek back" and "peek saved": windows of peek kept to come back to (see PeekMarks), and the window of
/// the last peek kept for the next shell
/// </summary>
internal sealed partial class ShellSession
{
    // The words after peek that save, show again and list the windows kept (taken as these, not as files: a file of
    // that name is "./save")
    internal static readonly string[] PeekMarkWords = { "save", "back", "saved" };

    /// <summary>
    /// The window of the last peek of this shell, as PeekMarks keeps it (null: none)
    /// </summary>
    internal PeekMarks.Window? LastPeekWindow =>
        _lastPeek is { } last ? new PeekMarks.Window(last.Path, last.First, last.Count) : null;

    /// <summary>
    /// The window of the last peek was changed in this shell (a peek, forget): it is kept for the next shell when it is
    /// left. A window taken from the shell left before, and not changed, is left in peeks.txt as it is, also when its
    /// file was not there when this shell started (a drive not plugged in)
    /// </summary>
    internal bool LastPeekChanged => _lastPeekChanged;

    private bool _lastPeekChanged;

    // The window of the last peek of the shell left last, when its file is there (a shell typed in at a terminal)
    private void RestoreLastPeek()
    {
        PeekMarks.Load();
        if (PeekMarks.Last is not { } window || !File.Exists(window.Path)) return;
        _lastPeek = (window.Path, window.First, window.Count, StampOf(window.Path), HashOf(window.Path));
    }

    // A hash of the text of a file, as the last peek keeps it for shellautopeek (0 when it cannot be read, or the
    // setting is off: the file is then not read when the shell starts)
    private static int HashOf(string path)
    {
        if (!Shell.AutoPeek) return 0;
        try
        {
            return new FileInfo(path).Length > AutoPeekReadLimit ? 0 : TextFile.Load(path).Text.GetHashCode();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return 0;
        }
    }

    // "peek save", "peek save intro", "peek save -d intro", "peek back", "peek back intro", "peek back 2", "peek saved"
    // ("filter storm", "-t harbour"). False when the words are not these (a peek of a file)
    private bool PeekMark(List<string> words, string argument)
    {
        var kind = words[0].ToLowerInvariant();
        if (!PeekMarkWords.Contains(kind)) return false;
        switch (kind)
        {
            case "saved":
                ListSavedPeeks(AfterWords(argument, 1));
                return true;
            case "save":
                SavePeek(words.Skip(1).ToList());
                return true;
            default:
                PeekBack(words.Skip(1).ToList());
                return true;
        }
    }

    // "peek save [name]": the window of the last peek kept; "peek save -d [name]": a window kept deleted
    private void SavePeek(List<string> words)
    {
        var delete = words.Count > 0 && words[0] == "-d";
        if (delete) words.RemoveAt(0);
        if (words.Count > 1 || (words.Count == 1 && words[0].StartsWith('-')))
        {
            Error(Lang.T("Give one name, or none: peek save, peek save intro, peek save -d intro"));
            return;
        }

        var name = words.Count == 1 ? words[0] : string.Empty;
        if (name.Length > 0 && !PeekMarks.IsName(name))
        {
            Error(Lang.F("Not a name for a saved peek: {0} (a letter first, then letters, digits, - and _)", name));
            return;
        }

        if (delete)
        {
            if (!PeekMarks.Remove(name, out var removed))
            {
                Error(name.Length > 0
                    ? Lang.F("No saved peek named {0} (peek saved lists them)", name)
                    : Lang.T("No peek saved without a name (peek saved lists them)"));
                return;
            }

            if (Failed(removed)) return;
            EventLog.Add(DialogText.LogShell + ": peek save -d"); // English: the log (never the name or the path)
            Line(name.Length > 0 ? Lang.F("Saved peek {0} deleted", PeekMarks.KeyOf(name)) : Lang.T("Saved peek deleted"));
            return;
        }

        if (LastPeekWindow is not { } window)
        {
            Error(Lang.T("No file peeked yet: peek a file first (peek plot.md)"));
            return;
        }

        // (a tab or a line break in the path, as Linux and macOS allow: it would break the line of peeks.txt)
        if (!PeekMarks.Keepable(window.Path))
        {
            Error(Lang.F("A path with a tab or a line break cannot be saved: {0}", window.Path));
            return;
        }

        if (Failed(PeekMarks.Set(name, window))) return;
        EventLog.Add(DialogText.LogShell + ": peek save"); // English: the log (never the name or the path)
        var lines = RangeText(window);
        Line(name.Length > 0
            ? Lang.F("Saved as {0}: lines {1} of {2} (peek back {0})", PeekMarks.KeyOf(name), lines, window.Path)
            : Lang.F("Saved: lines {0} of {1} (peek back)", lines, window.Path));
    }

    // "peek back", "peek back intro", "peek back 2" (row 2 of peek saved): the window kept shown again, as the file is
    // now; it is then the last peek, so "+" and "." go on from it
    private void PeekBack(List<string> words)
    {
        if (words.Count > 1)
        {
            Error(Lang.T("Give one name or row, or none: peek back, peek back intro, peek back 2"));
            return;
        }

        PeekMarks.Load();
        var saved = PeekMarks.Saved;
        PeekMarks.Window? window;
        // (a number is a row: a name starts with a letter)
        if (words.Count == 1 && words[0].Length > 0 && words[0].All(char.IsAsciiDigit))
        {
            var row = int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
            if (row < 1 || row > saved.Count)
            {
                Error(saved.Count == 0
                    ? Lang.T("No peeks saved yet: peek save keeps the window of the last peek")
                    : Lang.F("No saved peek {0}: {1} are saved (peek saved lists them)", words[0], saved.Count));
                return;
            }

            window = saved[row - 1].Window;
        }
        else
        {
            var name = words.Count == 1 ? words[0] : string.Empty;
            window = PeekMarks.Get(name);
            if (window == null)
            {
                Error(name.Length > 0
                    ? Lang.F("No saved peek named {0} (peek saved lists them)", name)
                    : saved.Count == 0
                        ? Lang.T("No peeks saved yet: peek save keeps the window of the last peek")
                        : Lang.T("No peek saved without a name (peek saved lists them)"));
                return;
            }
        }

        if (!File.Exists(window.Path))
        {
            Error(Lang.F("Not found: {0}", window.Path));
            return;
        }

        // (as the window of the last peek is shown again: at the end of a file that got shorter)
        PeekWindow(new List<string>
        {
            window.Path, "+" + window.First.ToString(CultureInfo.InvariantCulture),
            window.Count.ToString(CultureInfo.InvariantCulture)
        }, null, 1, true, false);
    }

    // "peek saved": the windows kept, numbered for "peek back 2": the name ("-" for the one without a name), the lines
    // and the file, marked "(missing)" when it is not there. "filter storm": those with a text in the name or the path
    // (or a pattern that fits the name or the name of the file: "ch*"); "-t harbour": those with a text in the lines
    // of the window, as the file is now, with the first line found ("chapter-3.md:43  The harbour lights"). Both can
    // be given; the rows keep the numbers of the whole list
    private void ListSavedPeeks(string rest)
    {
        var (before, search) = TextOption(rest);
        before = before.Trim();
        string? filter = null;
        if (before.Length > 0)
        {
            if (!CommandLine.Name(before).Equals("filter", StringComparison.OrdinalIgnoreCase))
            {
                Error(Lang.T("After peek saved: filter text, -t text, or both"));
                return;
            }

            var typed = Rest(before).Trim();
            filter = Unquoted(typed) ?? typed;
        }

        if (filter is { Length: 0 } || search is { Length: 0 })
        {
            Error(Lang.T("Give a text: peek saved filter storm, peek saved -t harbour"));
            return;
        }

        PeekMarks.Load();
        var saved = PeekMarks.Saved;
        if (saved.Count == 0)
        {
            Line(Lang.T("No peeks saved yet: peek save keeps the window of the last peek"));
            return;
        }

        var pattern = search != null ? FileSearch.WildcardText(search) : null;
        var rows = new List<(int Number, string Name, PeekMarks.Window Window, (int Line, string Text)? Found)>();
        for (var i = 0; i < saved.Count; i++)
        {
            var (name, window) = saved[i];
            if (filter != null && !Fits(filter, window.Path, name)) continue;
            (int, string)? found = null;
            if (pattern != null)
            {
                found = FirstLineInWindow(window, pattern);
                if (found == null) continue;
            }

            rows.Add((i + 1, name, window, found));
        }

        if (rows.Count == 0)
        {
            Line(search == null ? Lang.F("No saved peeks with {0} in the name or path", filter!)
                : filter == null ? Lang.F("No saved peeks with {0} in the lines", search)
                : Lang.F("No saved peeks with {0} in the name or path and {1} in the lines", filter, search));
            return;
        }

        var width = saved.Count.ToString(CultureInfo.InvariantCulture).Length;
        var nameWidth = rows.Max(row => Math.Max(row.Name.Length, 1));
        var placeWidth = rows.Max(row => Place(row.Window, row.Found).Length);
        foreach (var (number, name, window, found) in rows)
        {
            var shown = found is { } line
                ? Place(window, found).PadRight(placeWidth) + "  " + line.Text
                : Place(window, null).PadRight(placeWidth) + "  " + window.Path +
                  (File.Exists(window.Path) ? string.Empty : " " + Lang.T("(missing)"));
            Row(number, width, (name.Length > 0 ? name : "-").PadRight(nameWidth) + "  " + shown);
        }

        // The lines of a window ("40-49"), or the file and the line found in it ("chapter-3.md:43")
        static string Place(PeekMarks.Window window, (int Line, string Text)? found) =>
            found is { } line
                ? Path.GetFileName(window.Path) + ":" + line.Line.ToString(CultureInfo.InvariantCulture)
                : RangeText(window);
    }

    // The lines of a window, as "40-49" (in long: a window read from a file edited by hand can end past int)
    private static string RangeText(PeekMarks.Window window) =>
        window.First.ToString(CultureInfo.InvariantCulture) + "-" +
        ((long)window.First + window.Count - 1).ToString(CultureInfo.InvariantCulture);


    // The first line of a window with a text, as the file is now: its number and its text (trimmed). Null when none
    // has it, or the file is gone or cannot be read
    private static (int Line, string Text)? FirstLineInWindow(PeekMarks.Window window, System.Text.RegularExpressions.Regex pattern)
    {
        List<string> lines;
        try
        {
            if (!File.Exists(window.Path)) return null;
            lines = LinesOfText(TextFile.Load(window.Path).Text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }

        // (the lines peek back shows: at the end of a file that got shorter, the last lines, as many)
        var last = (int)Math.Min((long)window.First + window.Count - 1, lines.Count);
        var first = window.First;
        if ((long)window.First + window.Count - 1 > lines.Count) first = Math.Max(1, last - window.Count + 1);
        for (var number = first; number <= last; number++)
        {
            if (FileSearch.Matches(pattern, lines[number - 1])) return (number, lines[number - 1].Trim());
        }

        return null;
    }
}
