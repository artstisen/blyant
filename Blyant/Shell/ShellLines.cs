namespace Blyant;

/// <summary>
/// "lines": lines of a file copied to another file or kept in a register ("> $notes", ">> $notes": added), or cut
/// ("cut": moved there), without opening them; or the text in them changed: a text put at their start or end ("start", "end"), taken off it ("unstart", "unend"), or a text in
/// them replaced ("replace") or removed ("remove"), a text put in at a column ("at"), the lines sorted ("sort", "sort -d") or made upper or lower case or Title Case ("upper",
/// "lower", "title"). The
/// lines and the place are those of shift. What is written is never logged, only the
/// paths and the lines
/// </summary>
internal sealed partial class ShellSession
{
    // What "lines" does to the text in the lines (after the lines: "lines todo.md 4 start Note:")
    private static readonly string[] LineEdits =
        { "start", "end", "unstart", "unend", "replace", "remove", "at", "dup", "sort", "upper", "lower", "title" };

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
        var words = Words(argument);
        var form = LinesForm(words);
        if (form.Edit >= 0) EditLines(argument, form.Edit);
        // (after the lines: neither a ">" nor a word of what is done)
        else if (form.Arrow < 0 && words.Count > 2 && !words.Contains(">") && !words.Contains(">>"))
        {
            Error(Lang.T("After the lines: > file place, > $name, start, end, unstart, unend, replace, remove, at, dup, sort, upper, lower or title"));
        }
        else CopyLines(argument);
    }

    // "lines todo.md 4 start Remember this:", "lines todo.md 4 end (by Friday)": the text put at the start or the end
    // of the lines, with a blank between; in quotes as it is ("- [ ] ", ";"). Blank lines are left as they are.
    // "unstart" and "unend" take the text off the start or end of the lines that have it (with the blank start and end
    // put there; in quotes: the text only). "replace old new": a text in the lines replaced (letter case as typed;
    // "" for none), and "remove text" the text taken out wherever it is in them. In quotes, "" is a quote ("""").
    // "dup", "dup 3": the lines copied right below the last of them (3 copies), as :dup in the editor.
    // "at 10 text": the text put in before column 10 (as the editor and words count them), as typed, no blank added;
    // a line without that column (too short) is told, and nothing is changed. As insert: the line endings and byte order mark of the file kept, only a file in UTF-8 and not
    // one Blyant uses, and the file is then the last list, at the first line changed. Nothing is lost by it (unstart
    // undoes start), so it is not asked. "sort" sorts the lines A to Z ("-a", the default) or Z to A ("-d"), in the
    // same order as :sort in the editor, and puts them back in the places of the lines; "upper" and "lower" make them
    // upper or lower case, as gU and gu in the editor, and "title" each word with a capital first letter (see
    // LineWords.TitleLine). "undo" takes each back
    private void EditLines(string argument, int at)
    {
        var words = Words(argument);
        var kind = words[at].ToLowerInvariant();
        var rest = AfterWords(argument, at + 1).Trim();
        string text;
        var replacement = string.Empty;
        var exact = false;
        // ("at 10": the column; "dup 3": the copies)
        var atColumn = 0;
        var copies = 1;
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
        else if (kind == "at")
        {
            var parts = Words(rest);
            if (parts.Count < 2)
            {
                Error(Lang.T("Give the column and the text: lines notes.md 34 at 10 text"));
                return;
            }

            if (!IsNumber(parts[0], out atColumn) || atColumn < 1)
            {
                Error(Lang.F("Not a column: {0} (as 10, the column words shows)", parts[0]));
                return;
            }

            // (as typed, or in quotes as written; no blank added either way)
            var typed = AfterWords(rest, 1).Trim();
            text = Unquoted(typed) ?? typed;
            if (text.Length == 0)
            {
                Error(Lang.T("Give the column and the text: lines notes.md 34 at 10 text"));
                return;
            }

            exact = true;
        }
        else if (kind == "dup")
        {
            text = string.Empty;
            // (how many copies: 1, or as many as given, at most 500 as :dup takes)
            if (rest.Length > 0 && (!IsNumber(rest, out copies) || copies is < 1 or > EditorEngine.MaxCopies))
            {
                Error(Lang.F("After dup: how many copies, 1 to {0} (lines notes.md 4-6 dup 3)", EditorEngine.MaxCopies));
                return;
            }
        }
        else if (kind is "sort" or "upper" or "lower" or "title")
        {
            text = string.Empty;
            if (kind == "sort" && rest is not ("" or "-a" or "-d"))
            {
                Error(Lang.T("After sort: -a (A to Z, the default) or -d (Z to A)"));
                return;
            }

            if (kind != "sort" && rest.Length > 0)
            {
                Error(Lang.F("Nothing goes after {0}: lines notes.md 3-20 {0}", kind));
                return;
            }

            // ("-d": Z to A; the order is kept in "replacement")
            replacement = rest;
        }
        else
        {
            // (in quotes: as it is, the blanks in it too, and "" a quote)
            var unquoted = Unquoted(rest);
            exact = unquoted is not null;
            text = unquoted ?? rest;
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
        if (kind == "dup")
        {
            Duplicate(item, file, lines, picked, copies);
            return;
        }

        // At a column: every line must have it, or none is changed
        // (blank lines are left as they are, as start and end leave them)
        if (kind == "at" && picked.FirstOrDefault(number => lines[number - 1].Trim().Length > 0 &&
                LineWords.IndexOfColumn(lines[number - 1], atColumn) is null) is var tooShort and > 0)
        {
            Error(Lang.F("Line {0} has no column {1}: nothing changed", tooShort, atColumn));
            return;
        }

        var changed = new List<int>();
        // Sorted: the lines in the order they get, put back in the places of the lines picked
        var sorted = kind == "sort"
            ? EditorEngine.SortedLines(picked.Select(number => lines[number - 1]).ToList(), replacement == "-d", false)
            : null;
        for (var i = 0; i < picked.Count; i++)
        {
            var number = picked[i];
            var edited = sorted is not null ? sorted[i] : EditedLine(lines[number - 1], kind, text, exact, replacement, atColumn);
            if (edited == lines[number - 1]) continue;
            lines[number - 1] = edited;
            changed.Add(number);
        }

        if (changed.Count == 0)
        {
            Line(kind switch
            {
                "start" or "end" or "at" => Lang.T("Nothing changed: the lines are blank"),
                "sort" => Lang.T("Nothing changed: the lines are in that order already"),
                "upper" => Lang.T("Nothing changed: no lowercase letters in the lines"),
                "lower" => Lang.T("Nothing changed: no uppercase letters in the lines"),
                "title" => Lang.T("Nothing changed: the words start with capitals already"),
                "unstart" => Lang.F("Nothing changed: no line starts with {0}", text),
                "unend" => Lang.F("Nothing changed: no line ends with {0}", text),
                _ => Lang.F("Nothing changed: no line has {0} in it", text)
            });
            return;
        }

        // (the path, what was done and the lines, never the text)
        if (!SavedEdit(file, JoinedAs(file, lines), "Edit lines (" + kind + ")", item.Path + ":" + Numbers(changed))) return;

        LastList = new List<Item> { new(item.Path, false, (changed[0], 1)) };
        // (sorted: all the lines, in their new order)
        var shown = sorted is not null ? picked.ToList() : changed;
        var width = shown[^1].ToString().Length;
        foreach (var number in shown) Row(number, width, lines[number - 1]);
        if (sorted is not null) Line(Lang.F("{0}: {1} lines sorted", item.Path, picked.Count));
        else
        {
            Line(changed.Count == 1
                ? Lang.F("{0}: line {1} changed", item.Path, changed[0])
                : Lang.F("{0}: {1} lines changed", item.Path, changed.Count));
        }
    }

    // "lines notes.md 4-6 dup 3": the lines, in their order, copied right below the last of them, as many times as
    // asked. The copies are shown, the file is the last list at the first copy, and $line is the last copy, so
    // "lines @ dup" copies it again. As :dup: not more text than the editor puts in at once, nor a file larger than
    // Blyant opens
    private void Duplicate(Item item, TextFile file, List<string> lines, List<int> picked, int copies)
    {
        var block = picked.Select(number => lines[number - 1]).ToList();
        var size = block.Sum(line => line.Length + 1L) * copies;
        if (size > EditorEngine.MaxPutLength || TextBuffer.Normalize(file.Text).Length + size > TextFile.MaxSize)
        {
            Error(Lang.T("Too much text to insert at once (use a smaller count)"));
            return;
        }

        var added = Enumerable.Range(0, copies).SelectMany(_ => block).ToList();
        var (text, first) = WithLinesAt(file.Text, added, picked[^1]);
        // (the path and the lines, never the text)
        if (!SavedEdit(file, text, "Duplicate lines", item.Path + ":" + Numbers(picked) + " x" + copies)) return;

        LastList = new List<Item> { new(item.Path, false, (first, 1)) };
        var last = first + added.Count - 1;
        _line = last;
        var now = LinesOfText(text);
        var width = last.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        for (var i = first; i <= last; i++) Row(i, width, now[i - 1]);
        TellPutIn(item.Path, first, added.Count);
    }

    /// <summary>
    /// A line with a text put at its start or end ("start", "end": with a blank between, or "exact" as it is; a blank
    /// line as it is), taken off them ("unstart", "unend": with the blank start and end put there, or "exact" the text
    /// only; a line without it as it is), a text in it replaced ("replace", letter case as given) or taken out
    /// ("remove"), put in before a column ("at", the column given), or the line made
    /// upper or lower case ("upper", "lower", as gU and gu in the editor) or Title Case ("title")
    /// </summary>
    internal static string EditedLine(string line, string kind, string text, bool exact, string replacement = "",
        int column = 0)
    {
        switch (kind)
        {
            case "upper":
                return line.ToUpperInvariant();
            case "lower":
                return line.ToLowerInvariant();
            case "title":
                return LineWords.TitleLine(line);
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
            case "remove":
                return text.Length == 0 ? line : line.Replace(text, string.Empty, StringComparison.Ordinal);
            case "at":
            {
                // (a blank line as it is)
                if (line.Trim().Length == 0) return line;
                return LineWords.IndexOfColumn(line, column) is { } index ? line[..index] + text + line[index..] : line;
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
        // (a copy can go to another place of the same file; moving lines in a file is what shift does)
        if (cut && FileOperations.SamePath(LinkedPath(source.Item.Path), LinkedPath(target.Item.Path)))
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
        // (the paths and the lines, never the text)
        var what = source.Item.Path + ":" + Numbers(picked) + " to " + target.Item.Path + ":" + at;
        if (!SavedEdit(target.File, text, cut ? "Cut lines" : "Copy lines", what)) return;

        LastList = new List<Item> { new(target.Item.Path, false, (at, 1)) };
        // ("open 1" at the first line put in; @ at the last, so "insert @" goes after them, as after insert)
        _line = at + taken.Count - 1;
        var now = LinesOfText(text);
        var width = (at + taken.Count - 1).ToString().Length;
        for (var i = at; i < at + taken.Count; i++) Row(i, width, now[i - 1]);
        TellPutIn(target.Item.Path, at, taken.Count);
        if (cut) TakeOut(source, picked);
    }

    // Cut: the lines taken out of their file, after they are in the other file or the register, and only if the file
    // is as it was read (changed since: they are left there, and it is told)
    private void TakeOut((Item Item, TextFile File) source, List<int> picked)
    {
        if (Changed(source.Item.Path, source.File))
        {
            Error(Lang.F("{0} was changed meanwhile: the lines are copied, not taken out", source.Item.Path));
            return;
        }

        var removed = SaveEdit(source.File, WithoutLines(source.File.Text, picked));
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
        if (arrow < 0 || RegisterTarget(words, arrow) is null) return argument;
        var match = System.Text.RegularExpressions.Regex.Match(argument, @"(^|\s)>>?\s+\$(?=[A-Za-z])");
        return match.Success ? argument[..(match.Index + match.Length - 1)] + "$" + argument[(match.Index + match.Length - 1)..] : argument;
    }

    // "lines a.md 4-6 > $notes": the lines kept in a register (">>": added to its end), and with "cut" taken out of
    // their file after the register is saved, as for another file. The register is kept in the data directory
    private void LinesToRegister(List<string> words, int arrow, string register)
    {
        var cut = words.Count == arrow + 3 && words[^1].Equals("cut", StringComparison.OrdinalIgnoreCase);
        var adds = words[arrow] == ">>";
        // A register that holds the path of a file or directory: the place in it was left out, most likely ("> $plan
        // bottom"); its path is not written over
        if (ShellRegisters.Get(register) is { Length: > 0 } held && !held.Contains('\n') &&
            (File.Exists(PathOf(held)) || System.IO.Directory.Exists(PathOf(held))))
        {
            Error(Lang.F("${0} holds a path: give the place in that file (> ${0} bottom), or reg -d {0} first", register));
            return;
        }

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
        if (Failed(saved)) return;

        Line(adds
            ? picked.Count == 1
                ? Lang.F("Line {0} added to ${1}", picked[0], register)
                : Lang.F("{0} lines added to ${1}", picked.Count, register)
            : picked.Count == 1
                ? Lang.F("Line {0} kept in ${1}", picked[0], register)
                : Lang.F("{0} lines kept in ${1}", picked.Count, register));
        if (cut) TakeOut(source, picked);
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
        return (JoinedLines(lines, endsWithBreak), index + 1);
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
        return JoinedLines(rest, endsWithBreak);
    }
}
