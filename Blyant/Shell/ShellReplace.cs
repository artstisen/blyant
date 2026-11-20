namespace Blyant;

/// <summary>
/// "replace": a text replaced everywhere in a file ("replace chapter-3.md Maren Marit"), with the letter case as typed,
/// or any letter case ("-i"); "-n" only shows what would change. Within lines: a text with a line break cannot be
/// typed. The lines changed are shown (at most as many as peek shows), and how many were replaced in how many lines.
/// As the other commands that change text: only a file in UTF-8 and not one Blyant uses, the line endings and byte
/// order mark kept, one step for undo, and the paths logged, never the texts
/// </summary>
internal sealed partial class ShellSession
{
    // The options of replace, before the file
    private static readonly string[] ReplaceOptions = { "-n", "-i" };

    /// <summary>
    /// The words of "replace" in which a $name puts in a path (made one word): the options and the file. The texts
    /// after them are put in as they are
    /// </summary>
    internal static int ReplaceWords(string argument) =>
        Words(argument).TakeWhile(word => word.StartsWith('-')).Count() + 1;

    // "replace [-n] [-i] file|n old new"
    private void Replace(string argument)
    {
        var words = Words(argument);
        var (show, anyCase) = (false, false);
        while (words.Count > 0 && words[0].StartsWith('-') && words[0].Length > 1)
        {
            if (!ReplaceOptions.Contains(words[0]))
            {
                Error(Lang.F("Not taken: {0} (-n: only show what would change, -i: any letter case)", words[0]));
                return;
            }

            if (words[0] == "-n") show = true;
            else anyCase = true;
            words.RemoveAt(0);
        }

        if (words.Count != 3 || words[1].Length == 0)
        {
            Error(Lang.T("Give the file, the text and what replaces it: replace notes.md draft final"));
            return;
        }

        var fileWord = words[0];
        if (fileWord == "@")
        {
            if (_file == null)
            {
                Error(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"));
                return;
            }

            fileWord = _file;
        }

        if (EditableFile(fileWord) is not { } editable) return;
        var (item, file) = editable;
        var (text, replacement) = (words[1], words[2]);
        var comparison = anyCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var lines = LinesOfText(file.Text);
        var changed = new List<int>();
        var count = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var (edited, found) = ReplacedIn(lines[i], text, replacement, comparison);
            count += found;
            if (edited == lines[i]) continue;
            lines[i] = edited;
            changed.Add(i + 1);
        }

        if (count == 0)
        {
            Line(Lang.F("No {0} in {1}", text, Path.GetFileName(item.Path)));
            return;
        }

        if (changed.Count == 0)
        {
            Line(Lang.T("Nothing changed: the text is so already"));
            return;
        }

        if (!show)
        {
            var normalized = TextBuffer.Normalize(file.Text);
            var result = SaveEdit(file, string.Join("\n", lines) + (normalized.EndsWith('\n') ? "\n" : string.Empty));
            // (the path and the lines, never the texts)
            FileExplorer.LogFileOperation("Replace", item.Path + ":" + Numbers(changed), result);
            if (result != DialogText.Success)
            {
                Error(result);
                return;
            }
        }

        // (the file at the first line changed: "open 1", "peek @")
        LastList = new List<Item> { new(item.Path, false, (changed[0], 1)) };
        ShowReplaced(item.Path, lines, changed);
        var first = changed[0];
        if (show)
        {
            Line(count == 1 ? Lang.F("{0}: 1 would be replaced, in line {1} (-n: nothing written)", item.Path, first)
                : changed.Count == 1 ? Lang.F("{0}: {1} would be replaced, in line {2} (-n: nothing written)", item.Path, count, first)
                : Lang.F("{0}: {1} would be replaced, in {2} lines (-n: nothing written)", item.Path, count, changed.Count));
        }
        else
        {
            Line(count == 1 ? Lang.F("{0}: 1 replaced, in line {1}", item.Path, first)
                : changed.Count == 1 ? Lang.F("{0}: {1} replaced, in line {2}", item.Path, count, first)
                : Lang.F("{0}: {1} replaced, in {2} lines", item.Path, count, changed.Count));
        }
    }

    // The lines changed, numbered (as they are after the replace), at most as many as peek shows (the setting
    // shellpeeklines), between the markers of peek: above, whether the first line of the file is among them; below,
    // whether the last is, and how many lines of the file were changed
    private void ShowReplaced(string path, List<string> lines, List<int> changed)
    {
        var shown = changed.Take(Shell.PeekLines).ToList();
        if (Shell.Markers) Line((changed[0] > 1 ? AboveMarker : TopMarker) + Path.GetFileName(path));
        var width = shown[^1].ToString().Length;
        foreach (var number in shown) Line(number.ToString().PadLeft(width + 2) + "  " + lines[number - 1]);
        if (changed.Count > shown.Count) Line("  " + Lang.F("(and {0} more lines)", changed.Count - shown.Count));
        if (!Shell.Markers) return;
        Line((changed[^1] < lines.Count ? BelowMarker : EndMarker) + (changed.Count == 1
            ? Lang.F("1 line of {0}", lines.Count)
            : Lang.F("{0} lines of {1}", changed.Count, lines.Count)));
    }

    /// <summary>
    /// A line with a text replaced everywhere in it, from the left, and how many times (a text found is not searched
    /// again in what replaced it)
    /// </summary>
    internal static (string Line, int Count) ReplacedIn(string line, string text, string replacement,
        StringComparison comparison)
    {
        if (text.Length == 0) return (line, 0);
        var at = line.IndexOf(text, comparison);
        if (at < 0) return (line, 0);
        var result = new System.Text.StringBuilder();
        var from = 0;
        var count = 0;
        while (at >= 0)
        {
            result.Append(line, from, at - from).Append(replacement);
            from = at + text.Length;
            count++;
            at = line.IndexOf(text, from, comparison);
        }

        result.Append(line, from, line.Length - from);
        return (result.ToString(), count);
    }
}
