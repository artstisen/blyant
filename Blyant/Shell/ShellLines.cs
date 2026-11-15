namespace Blyant;

/// <summary>
/// "lines": lines of a file copied to another file, or cut ("cut": moved there), without opening them; or the text in
/// them changed: a text put at their start or end ("start", "end"), taken off it ("unstart", "unend"), or a text in
/// them replaced ("replace"). The lines and the place are those of shift. What is written is never logged, only the
/// paths and the lines
/// </summary>
internal sealed partial class ShellSession
{
    // What "lines" does to the text in the lines (after the lines: "lines todo.md 4 start Note:")
    private static readonly string[] LineEdits = { "start", "end", "unstart", "unend", "replace" };

    /// <summary>
    /// What the words after "lines" are: the place of the ">" of a copy (2, or 3 after "+10 5"), or of a word of
    /// LineEdits; -1 for neither. A ">" or a word of them later on is part of the text ("lines todo.md 4 start >")
    /// </summary>
    internal static (int Arrow, int Edit) LinesForm(IReadOnlyList<string> words)
    {
        // ("+10 5": a count after the first line, as peek takes them)
        var at = words.Count > 2 && words[1].StartsWith('+') && words[2].Length > 0 && words[2].All(char.IsAsciiDigit)
            ? 3
            : 2;
        if (words.Count <= at) return (-1, -1);
        // (">>": added to a register)
        if (words[at] is ">" or ">>") return (at, -1);
        return LineEdits.Contains(words[at].ToLowerInvariant()) ? (-1, at) : (-1, -1);
    }

    /// <summary>
    /// The words of "lines" in which a $name puts in a path or a name (made one word): all of them for a copy, and
    /// up to the word of what is done for the others, after which the text is the value as it is
    /// </summary>
    internal static int LinesWords(string argument) =>
        LinesForm(Words(argument)) is { Edit: >= 0 } form ? form.Edit + 1 : int.MaxValue;

    // "lines ...": a copy ("... > other.md place"), or the text in the lines changed
    private void Lines(string argument)
    {
        var form = LinesForm(Words(argument));
        if (form.Edit >= 0) EditLines(argument, form.Edit);
        else CopyLines(argument);
    }

    // "lines todo.md 4 start Remember this:", "lines todo.md 4 end (by Friday)": the text put at the start or the end
    // of the lines, with a blank between; in quotes as it is ("- [ ] ", ";"). Blank lines are left as they are.
    // "unstart" and "unend" take the text off the start or end of the lines that have it (with the blank start and end
    // put there; in quotes: the text only). "replace old new": a text in the lines replaced (letter case as typed;
    // "" for none). As insert: the line endings and byte order mark of the file kept, only a file in UTF-8 and not
    // one Blyant uses, and the file is then the last list, at the first line changed. Nothing is lost by it (unstart
    // undoes start), so it is not asked
    private void EditLines(string argument, int at)
    {
        var words = Words(argument);
        var kind = words[at].ToLowerInvariant();
        var rest = AfterWords(argument, at + 1).Trim();
        string text;
        var replacement = string.Empty;
        var exact = false;
        if (kind == "replace")
        {
            var parts = Words(rest);
            if (parts.Count != 2 || parts[0].Length == 0)
            {
                Error(Lang.T("Give the text and what replaces it: lines notes.md 5-9 replace draft final"));
                return;
            }

            (text, replacement) = (parts[0], parts[1]);
        }
        else
        {
            // (in quotes: as it is, the blanks in it too)
            exact = rest.Length >= 2 && rest[0] == '"' && rest[^1] == '"' && rest.Count(c => c == '"') == 2;
            text = exact ? rest[1..^1] : rest;
            if (text.Length == 0)
            {
                Error(Lang.F("Give the text: lines todo.md 4 {0} Remember this:", kind));
                return;
            }
        }

        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;
        var lines = LinesOfText(file.Text);
        if (PickedLines(words[1], at == 3 ? words[2] : null, lines, item.Path) is not { } picked) return;

        var changed = new List<int>();
        foreach (var number in picked)
        {
            var edited = EditedLine(lines[number - 1], kind, text, exact, replacement);
            if (edited == lines[number - 1]) continue;
            lines[number - 1] = edited;
            changed.Add(number);
        }

        if (changed.Count == 0)
        {
            Line(kind switch
            {
                "start" or "end" => Lang.T("Nothing changed: the lines are blank"),
                "unstart" => Lang.F("Nothing changed: no line starts with {0}", text),
                "unend" => Lang.F("Nothing changed: no line ends with {0}", text),
                _ => Lang.F("Nothing changed: no line has {0} in it", text)
            });
            return;
        }

        var normalized = TextBuffer.Normalize(file.Text);
        var result = file.Save(string.Join("\n", lines) + (normalized.EndsWith('\n') ? "\n" : string.Empty));
        // (the path, what was done and the lines, never the text)
        FileExplorer.LogFileOperation("Edit lines (" + kind + ")", item.Path + ":" + Numbers(changed), result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { new(item.Path, false, (changed[0], 1)) };
        var width = changed[^1].ToString().Length;
        foreach (var number in changed) Line(number.ToString().PadLeft(width + 2) + "  " + lines[number - 1]);
        Line(changed.Count == 1
            ? Lang.F("{0}: line {1} changed", item.Path, changed[0])
            : Lang.F("{0}: {1} lines changed", item.Path, changed.Count));
    }

    /// <summary>
    /// A line with a text put at its start or end ("start", "end": with a blank between, or "exact" as it is; a blank
    /// line as it is), taken off them ("unstart", "unend": with the blank start and end put there, or "exact" the text
    /// only; a line without it as it is), or a text in it replaced ("replace", letter case as given)
    /// </summary>
    internal static string EditedLine(string line, string kind, string text, bool exact, string replacement = "")
    {
        switch (kind)
        {
            case "start":
                return line.Trim().Length == 0 ? line : text + (exact ? string.Empty : " ") + line;
            case "end":
                return line.Trim().Length == 0 ? line : line + (exact ? string.Empty : " ") + text;
            case "unstart":
            {
                if (!line.StartsWith(text, StringComparison.Ordinal)) return line;
                var rest = line[text.Length..];
                return !exact && rest.StartsWith(' ') ? rest[1..] : rest;
            }
            case "unend":
            {
                if (!line.EndsWith(text, StringComparison.Ordinal)) return line;
                var rest = line[..^text.Length];
                return !exact && rest.EndsWith(' ') ? rest[..^1] : rest;
            }
            default:
                return text.Length == 0 ? line : line.Replace(text, replacement, StringComparison.Ordinal);
        }
    }

    // "lines notes.md 4-6 > plan.md bottom", "lines notes.md +1 14 > plan.md top cut", "lines todo.md "Buy milk" >
    // done.md Done+": the lines (as shift takes them, or "+1 14": 14 lines from line 1, as peek takes them) put in the
    // other file at the place (as shift: top, bottom, +12 before line 12, under a heading, Heading+ at the end of its
    // section). "copy" (the default) or "cut" last, not in quotes (a heading "Cut" is written in quotes): cut takes the
    // lines out of their file after they are in the other, so a failure leaves them in both, never in neither. Not
    // asked: no line is lost. As insert and shift: each file keeps its line endings and byte order mark, only files in
    // UTF-8 and not ones Blyant uses, and the other file is then the last list, at the lines put in
    private void CopyLines(string argument)
    {
        var words = Words(argument);
        var arrow = LinesForm(words).Arrow;
        // Into a register: "lines a.md 4-6 > $notes" (">>": added to it), "copy" or "cut" after it
        if (arrow >= 0 && RegisterTarget(words, arrow) is { } register)
        {
            LinesToRegister(words, arrow, register);
            return;
        }

        if (arrow >= 0 && words[arrow] == ">>")
        {
            Error(Lang.T(">> adds lines to a register: lines notes.md 4-6 >> $notes"));
            return;
        }

        if (arrow is < 2 or > 3 || words.Count < arrow + 3 || (arrow == 3 && !words[1].StartsWith('+')))
        {
            Error(Lang.T("Give the lines and where to: lines notes.md 4-6 > plan.md bottom (cut: move them)"));
            return;
        }

        // The place, and "copy" or "cut" after it
        var place = AfterWords(argument, arrow + 2).Trim();
        var cut = false;
        var blank = place.LastIndexOfAny([' ', '\t']);
        if (blank > 0 && place[(blank + 1)..].ToLowerInvariant() is "copy" or "cut")
        {
            cut = place[(blank + 1)..].Equals("cut", StringComparison.OrdinalIgnoreCase);
            place = place[..blank].Trim();
        }

        place = place.Trim('"').Trim();
        if (EditableFile(words[0]) is not { } source) return;
        if (EditableFile(words[arrow + 1]) is not { } target) return;
        if (FileOperations.SamePath(LinkedPath(source.Item.Path), LinkedPath(target.Item.Path)))
        {
            Error(Lang.T("The lines are in that file already: shift moves lines in a file"));
            return;
        }

        var lines = LinesOfText(source.File.Text);
        if (PickedLines(words[1], arrow == 3 ? words[2] : null, lines, source.Item.Path) is not { } picked) return;
        var targetLines = LinesOfText(target.File.Text);
        if (PlaceIn(place, targetLines, target.File.Text, target.Item.Path, null) is not { } position) return;

        var taken = picked.Select(n => lines[n - 1]).ToList();
        var (text, at) = WithLinesAt(target.File.Text, taken, position);
        var result = target.File.Save(text);
        // (the paths and the lines, never the text)
        var what = source.Item.Path + ":" + Numbers(picked) + " to " + target.Item.Path + ":" + at;
        FileExplorer.LogFileOperation(cut ? "Cut lines" : "Copy lines", what, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { new(target.Item.Path, false, (at, 1)) };
        var now = LinesOfText(text);
        var width = (at + taken.Count - 1).ToString().Length;
        for (var i = at; i < at + taken.Count; i++) Line(i.ToString().PadLeft(width + 2) + "  " + now[i - 1]);
        Line(taken.Count == 1
            ? Lang.F("{0}: line {1} put in", target.Item.Path, at)
            : Lang.F("{0}: lines {1}-{2} put in", target.Item.Path, at, at + taken.Count - 1));
        if (!cut) return;

        // Cut: out of their file, as it was read (changed since: left there)
        if (Changed(source.Item.Path, source.File))
        {
            Error(Lang.F("{0} was changed meanwhile: the lines are copied, not taken out", source.Item.Path));
            return;
        }

        var left = WithoutLines(source.File.Text, picked);
        var removed = source.File.Save(left);
        FileExplorer.LogFileOperation("Take out lines", source.Item.Path + ":" + Numbers(picked), removed);
        if (removed != DialogText.Success)
        {
            Error(Lang.F("{0}: the lines are copied, but not taken out: {1}", source.Item.Path, removed));
            return;
        }

        Line(picked.Count == 1
            ? Lang.F("{0}: line {1} taken out", source.Item.Path, picked[0])
            : Lang.F("{0}: {1} lines taken out", source.Item.Path, picked.Count));
    }

    // The register lines go to: "$notes" after the ">" (or ">>"), alone or with "copy" or "cut" after it (a file
    // has a place after it); a name set by Blyant ($file ...) is a path. Null: not a register
    private static string? RegisterTarget(IReadOnlyList<string> words, int arrow)
    {
        if (words.Count <= arrow + 1 || words[arrow + 1] is not ['$', .. var name] || !ShellRegisters.IsName(name) ||
            ShellRegisters.IsReserved(name))
        {
            return null;
        }

        return words.Count == arrow + 2 || (words.Count == arrow + 3 && words[^1].ToLowerInvariant() is "copy" or "cut")
            ? name.ToLowerInvariant()
            : null;
    }

    /// <summary>
    /// The words after "lines" with the "$" of a register the lines go to made "$$", so that it is not put in (the
    /// value of $notes) but stays the name ("lines a.md 4-6 > $notes")
    /// </summary>
    internal static string ProtectRegisterTarget(string argument)
    {
        var words = Words(argument);
        var arrow = LinesForm(words).Arrow;
        if (arrow < 0 || RegisterTarget(words, arrow) == null) return argument;
        var match = System.Text.RegularExpressions.Regex.Match(argument, @"(^|\s)>>?\s+\$(?=[A-Za-z])");
        return match.Success ? argument[..(match.Index + match.Length - 1)] + "$" + argument[(match.Index + match.Length - 1)..] : argument;
    }

    // "lines a.md 4-6 > $notes": the lines kept in a register (">>": added to its end), and with "cut" taken out of
    // their file after the register is saved, as for another file. The register is kept in the data directory
    private void LinesToRegister(List<string> words, int arrow, string register)
    {
        var cut = words.Count == arrow + 3 && words[^1].Equals("cut", StringComparison.OrdinalIgnoreCase);
        var adds = words[arrow] == ">>";
        if (EditableFile(words[0]) is not { } source) return;
        var lines = LinesOfText(source.File.Text);
        if (PickedLines(words[1], arrow == 3 ? words[2] : null, lines, source.Item.Path) is not { } picked) return;
        var text = string.Join("\n", picked.Select(n => lines[n - 1]));
        if (adds) ShellRegisters.Add(register, text);
        else ShellRegisters.Set(register, text);
        var saved = ShellRegisters.Save();
        // English: the log (the name of the register and the lines, never the text)
        EventLog.Add(DialogText.LogShell + ": lines to register " + register + ": " + source.Item.Path + ":" +
                     Numbers(picked));
        if (saved != DialogText.Success)
        {
            Error(saved);
            return;
        }

        Line(adds
            ? picked.Count == 1
                ? Lang.F("Line {0} added to ${1}", picked[0], register)
                : Lang.F("{0} lines added to ${1}", picked.Count, register)
            : picked.Count == 1
                ? Lang.F("Line {0} kept in ${1}", picked[0], register)
                : Lang.F("{0} lines kept in ${1}", picked.Count, register));
        if (!cut) return;

        // Cut: out of their file, as it was read (changed since: left there)
        if (Changed(source.Item.Path, source.File))
        {
            Error(Lang.F("{0} was changed meanwhile: the lines are copied, not taken out", source.Item.Path));
            return;
        }

        var removed = source.File.Save(WithoutLines(source.File.Text, picked));
        FileExplorer.LogFileOperation("Take out lines", source.Item.Path + ":" + Numbers(picked), removed);
        if (removed != DialogText.Success)
        {
            Error(Lang.F("{0}: the lines are copied, but not taken out: {1}", source.Item.Path, removed));
            return;
        }

        Line(picked.Count == 1
            ? Lang.F("{0}: line {1} taken out", source.Item.Path, picked[0])
            : Lang.F("{0}: {1} lines taken out", source.Item.Path, picked.Count));
    }

    // The file a path is (a link: the file it links to, as the file is saved there), to tell the same file under
    // two names
    private static string LinkedPath(string path)
    {
        var full = Path.GetFullPath(path);
        try
        {
            return new FileInfo(full).ResolveLinkTarget(true)?.FullName ?? full;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return full;
        }
    }

    // The numbers of lines for the log ("4-6" for 4, 5 and 6; "4,7")
    internal static string Numbers(List<int> lines) =>
        lines.Count > 1 && lines[^1] - lines[0] == lines.Count - 1
            ? lines[0] + "-" + lines[^1]
            : string.Join(',', lines);

    /// <summary>
    /// A text with lines put in before its line "position" (from 0; the number of lines: after the last), and the
    /// line they start at. The lines end in "\n"; a text that ends with a line break keeps it, and one that does not
    /// gets none after lines put in at the end
    /// </summary>
    internal static (string Text, int Line) WithLinesAt(string text, IReadOnlyList<string> added, int position)
    {
        text = TextBuffer.Normalize(text);
        if (text.Length == 0) return (string.Join("\n", added) + "\n", 1);
        var endsWithBreak = text.EndsWith('\n');
        var lines = LinesOfText(text);
        var index = Math.Clamp(position, 0, lines.Count);
        lines.InsertRange(index, added);
        return (string.Join("\n", lines) + (endsWithBreak ? "\n" : string.Empty), index + 1);
    }

    /// <summary>
    /// A text without some of its lines (from 1). A text that ends with a line break keeps it; none left: empty
    /// </summary>
    internal static string WithoutLines(string text, IReadOnlyCollection<int> taken)
    {
        text = TextBuffer.Normalize(text);
        var endsWithBreak = text.EndsWith('\n');
        var rest = LinesOfText(text).Where((_, i) => !taken.Contains(i + 1)).ToList();
        if (rest.Count == 0) return string.Empty;
        return string.Join("\n", rest) + (endsWithBreak ? "\n" : string.Empty);
    }
}
