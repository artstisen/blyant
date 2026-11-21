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

    // The window of the last peek of the shell left last, when its file is there (a shell typed in at a terminal)
    private void RestoreLastPeek()
    {
        PeekMarks.Load();
        if (PeekMarks.Last is not { } window || !File.Exists(window.Path)) return;
        _lastPeek = (window.Path, window.First, window.Count, StampOf(window.Path), HashOf(window.Path));
    }

    // A hash of the text of a file, as the last peek keeps it for shellautopeek (0 when it cannot be read)
    private static int HashOf(string path)
    {
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
            Line(name.Length > 0 ? Lang.F("Saved peek {0} deleted", name.ToLowerInvariant()) : Lang.T("Saved peek deleted"));
            return;
        }

        if (LastPeekWindow is not { } window)
        {
            Error(Lang.T("No file peeked yet: peek a file first (peek plot.md)"));
            return;
        }

        if (Failed(PeekMarks.Set(name, window))) return;
        EventLog.Add(DialogText.LogShell + ": peek save"); // English: the log (never the name or the path)
        var lines = window.First.ToString(CultureInfo.InvariantCulture) + "-" +
                    (window.First + window.Count - 1).ToString(CultureInfo.InvariantCulture);
        Line(name.Length > 0
            ? Lang.F("Saved as {0}: lines {1} of {2} (peek back {0})", name.ToLowerInvariant(), lines, window.Path)
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
        if (words.Count == 1 && words[0].All(char.IsAsciiDigit) && int.TryParse(words[0], out var row))
        {
            if (row < 1 || row > saved.Count)
            {
                Error(saved.Count == 0
                    ? Lang.T("No peeks saved yet: peek save keeps the window of the last peek")
                    : Lang.F("No saved peek {0}: {1} are saved (peek saved lists them)", row, saved.Count));
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

            filter = Rest(before).Trim().Trim('"');
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
            Line(search != null
                ? Lang.F("No saved peeks with {0} in their lines", search)
                : Lang.F("No saved peeks with {0} in them", filter!));
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
                : LinesText(window);

        static string LinesText(PeekMarks.Window window) =>
            window.First.ToString(CultureInfo.InvariantCulture) + "-" +
            (window.First + window.Count - 1).ToString(CultureInfo.InvariantCulture);
    }

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

        var last = (int)Math.Min((long)window.First + window.Count - 1, lines.Count);
        for (var number = window.First; number <= last; number++)
        {
            if (FileSearch.Matches(pattern, lines[number - 1])) return (number, lines[number - 1].Trim());
        }

        return null;
    }
}
