namespace Blyant;

/// <summary>
/// "shift": lines of a file moved to another place in it (under a heading, before a line, to the top or the bottom),
/// without opening it, as items of a list are moved from "Todo" to "Done". "shift!" also ticks the task boxes of the
/// lines moved ("- [ ]" to "- [x]"), or unticks those that are ticked. The lines a command takes (PickedLines) and
/// the place in a file (PlaceIn) serve "lines" too, and tell what is wrong; the rest (the line sets, the headings, the
/// move) does not touch the console, so it can be tested
/// </summary>
internal sealed partial class ShellSession
{
    // "shift todo.md 4-6 Done": the lines (by number: "4", "4-6", "4,7,9-10"; or the one line that has a text in it:
    // "Buy milk") to the place: under the heading "Done" (after a blank line under it), "Done+" at the end of its
    // section, "+12" before line 12, "top" or "bottom". The numbers are those of the file as it is. As insert and
    // change: its line endings and byte order mark kept, only a file in UTF-8 and not one Blyant uses, and the file is
    // then the last list, at the lines moved. Lines are not lost by it, so it is not asked
    private void Shift(string argument, bool tick)
    {
        var command = tick ? "shift!" : "shift";
        var words = Words(argument);
        var place = AfterWords(argument, 2).Trim().Trim('"').Trim();
        if (words.Count < 3 || place.Length == 0 || words[1].Trim().Length == 0)
        {
            Error(Lang.F("Give a file, the lines and the place: {0} todo.md 4-6 Done", command));
            return;
        }

        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;
        var lines = LinesOfText(file.Text);
        if (PickedLines(words[1], null, lines, item.Path) is not { } moved) return;
        if (PlaceIn(place, lines, file.Text, item.Path, moved) is not { } position) return;

        var (text, at, ticked) = Shifted(file.Text, moved, position, tick);
        if (text == TextBuffer.Normalize(file.Text))
        {
            Line(Lang.T("Nothing moved: the lines are there already"));
            return;
        }

        var result = file.Save(text);
        // (the path and the lines, never the text)
        FileExplorer.LogFileOperation("Shift lines", item.Path + ":" + Numbers(moved) + " to " + at, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { new(item.Path, false, (at, 1)) };
        var now = LinesOfText(text);
        var width = (at + moved.Count - 1).ToString().Length;
        for (var i = at; i < at + moved.Count; i++) Line(i.ToString().PadLeft(width + 2) + "  " + now[i - 1]);
        Line(moved.Count == 1
            ? Lang.F("{0}: line moved to line {1}", item.Path, at)
            : Lang.F("{0}: {1} lines moved to line {2}", item.Path, moved.Count, at));
        if (tick) Line(Lang.F("Task boxes ticked or unticked: {0}", ticked));
    }

    // The lines of a file a command takes (from 1, in order): by number ("4", "4-6", "4,7,9-10"), the one line with a
    // text in it ("Buy milk"), or with a count "+100 20" (20 lines from line 100, as peek takes them). Null (and told)
    // if they are not there
    private List<int>? PickedLines(string word, string? count, List<string> lines, string path)
    {
        if (count != null)
        {
            if (!LinesOf(word, count, out var first, out var last))
            {
                Error(Lang.F("Not lines: {0} (as 4, 4-6, 4,7,9-10, or +100 20)", word + " " + count));
                return null;
            }

            if (first > lines.Count)
            {
                Error(Lang.F("{0} has {1} lines", path, lines.Count));
                return null;
            }

            return Enumerable.Range(first, Math.Min(last, lines.Count) - first + 1).ToList();
        }

        if (!LineSet(word, lines.Count, out var picked))
        {
            var having = lines.Select((line, i) => (Line: line, Number: i + 1))
                .Where(row => row.Line.Contains(word, StringComparison.OrdinalIgnoreCase)).ToList();
            if (having.Count != 1)
            {
                foreach (var row in having) Line("  " + row.Number + "  " + row.Line);
                Error(having.Count == 0
                    ? Lang.F("No line has {0} in it (or give the lines: 4, 4-6, 4,7,9-10)", word)
                    : Lang.F("{0} lines have {1} in them: give the number of the line", having.Count, word));
                return null;
            }

            picked = new List<int> { having[0].Number };
        }

        if (picked[^1] > lines.Count)
        {
            Error(Lang.F("{0} has {1} lines", path, lines.Count));
            return null;
        }

        return picked;
    }

    // The place in a file (its lines and text) where lines go: before which line (from 0) of the file as it is.
    // "top", "bottom", "+12" (before line 12), a heading (under it), "Heading+" (at the end of its section). A heading
    // is named by its whole text (letter case alike); when none has it, the one heading that starts with it ("Done"
    // for "Done today"), or the one a pattern fits ("Chap*", "*today"). The lines
    // moved in the same file ("moved"; null: from another file) cannot hold the place. Null (and told) if it is not
    // there
    private int? PlaceIn(string place, List<string> lines, string text, string path, List<int>? moved)
    {
        var lower = place.ToLowerInvariant();
        if (lower == "top") return 0;
        if (lower == "bottom") return lines.Count;
        if (place.Length > 1 && place[0] == '+' && int.TryParse(place[1..], out var before) && before > 0)
        {
            if (moved != null && moved.Contains(before))
            {
                Error(Lang.F("The place is in the lines moved: {0}", place));
                return null;
            }

            return Math.Min(before, lines.Count + 1) - 1;
        }

        var headings = Headings(text, IsTypst(path));
        var atEnd = place.EndsWith('+');
        var name = HeadingName(atEnd ? place[..^1] : place);
        var found = headings.Where(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        // (no heading of that whole name: the start of one, or a pattern; a heading with "*" in it is found whole first)
        var fitted = found.Count == 0 && name.Length > 0;
        if (fitted)
        {
            found = ShellCompletion.IsPattern(name)
                ? headings.Where(h => ShellCompletion.PatternFits(name, h.Name)).ToList()
                : headings.Where(h => h.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (found.Count != 1)
        {
            foreach (var h in found.Count > 1 ? found : headings) Line("  " + h.Line + "  " + lines[h.Line - 1]);
            Error(found.Count == 0
                ? Lang.F("No heading {0} in {1} (or +12, top, bottom)", name, Path.GetFileName(path))
                : fitted
                    ? Lang.F("{0} headings fit {1}: give more of the name, or the line (+12)", found.Count, name)
                    : Lang.F("{0} headings are named {1}: give the line (+12)", found.Count, name));
            return null;
        }

        if (moved != null && moved.Contains(found[0].Line))
        {
            Error(Lang.F("The place is in the lines moved: {0}", place));
            return null;
        }

        return atEnd ? EndOfSection(lines, headings, found[0]) : UnderHeading(lines, found[0].Line);
    }

    /// <summary>
    /// The lines of "4", "+4", "4-6" or "4,7,9-10" (from 1), in order, each once. False for anything else (a text).
    /// A range stops at the last line of the file ("count"); one that starts after it gives its first line only (so
    /// it is told that the file has fewer lines), and a range as large as "1-2147483647" is never made into a list
    /// </summary>
    internal static bool LineSet(string text, int count, out List<int> lines)
    {
        lines = new List<int>();
        if (text.Length == 0) return false;
        foreach (var part in text.Split(','))
        {
            if (!LinesToChange(part, out var first, out var last)) return false;
            if (first > count)
            {
                lines.Add(first);
                continue;
            }

            for (var i = first; i <= Math.Min(last, count); i++) lines.Add(i);
        }

        lines = lines.Distinct().Order().ToList();
        return true;
    }

    /// <summary>
    /// The headings of a text: their line (from 1), level and name. Markdown headings ("## Done"), and for Typst
    /// ("= Done") those; not in a block of code (``` or ~~~)
    /// </summary>
    internal static List<(int Line, int Level, string Name)> Headings(string text, bool typst)
    {
        var headings = new List<(int, int, string)>();
        string? fence = null;
        var lines = LinesOfText(text);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].TrimStart();
            if (fence == null && (line.StartsWith("```") || line.StartsWith("~~~")))
            {
                fence = line[..3];
                continue;
            }

            if (fence != null)
            {
                if (line.StartsWith(fence)) fence = null;
                continue;
            }

            var mark = typst ? '=' : '#';
            var level = line.TakeWhile(c => c == mark).Count();
            if (level is < 1 or > 6 || level >= line.Length || !char.IsWhiteSpace(line[level])) continue;
            // (Markdown: an indent of four spaces is code)
            if (!typst && lines[i].Length - line.Length > 3) continue;
            var name = HeadingName(line);
            if (name.Length > 0) headings.Add((i + 1, level, name));
        }

        return headings;
    }

    /// <summary>
    /// True for a Typst file (its headings are "= Done")
    /// </summary>
    internal static bool IsTypst(string path) => Path.GetExtension(path).Equals(".typ", StringComparison.OrdinalIgnoreCase);

    // The name of a heading, as typed or in the file: without the marks in front ("## ", "= ") and the closing ones
    // of Markdown ("## Done ##")
    private static string HeadingName(string text)
    {
        var name = text.Trim().TrimStart('#', '=').Trim();
        var closing = name.TrimEnd('#');
        return (closing.Length < name.Length && closing.EndsWith(' ') ? closing : name).Trim();
    }

    // Under a heading: after it, and after a blank line under it (from 0: the line the lines go before)
    private static int UnderHeading(List<string> lines, int heading) =>
        heading < lines.Count && lines[heading].Trim().Length == 0 ? heading + 1 : heading;

    // At the end of the section of a heading: after its last line that is not blank, before the next heading of the
    // same level or above (or the end). A section with nothing in it: under the heading
    private static int EndOfSection(List<string> lines, List<(int Line, int Level, string Name)> headings,
        (int Line, int Level, string Name) heading)
    {
        var next = headings.FirstOrDefault(h => h.Line > heading.Line && h.Level <= heading.Level).Line;
        var end = next > 0 ? next - 1 : lines.Count; // The lines of the section: heading.Line + 1 .. end (from 1)
        while (end > heading.Line && lines[end - 1].Trim().Length == 0) end--;
        return end > heading.Line ? end : UnderHeading(lines, heading.Line);
    }

    /// <summary>
    /// A text with lines (from 1, in order) moved to before the line "position" of the text as it is (from 0; the
    /// number of lines: after the last), the line they start at now, and how many task boxes were ticked or unticked
    /// (with "tick"). A text that ends with a line break keeps it
    /// </summary>
    internal static (string Text, int At, int Ticked) Shifted(string text, IReadOnlyList<int> moved, int position,
        bool tick)
    {
        text = TextBuffer.Normalize(text);
        var endsWithBreak = text.EndsWith('\n');
        var lines = LinesOfText(text);
        var taken = moved.Select(n => lines[n - 1]).ToList();
        var ticked = 0;
        if (tick)
        {
            for (var i = 0; i < taken.Count; i++)
            {
                var changed = Ticked(taken[i]);
                if (changed == taken[i]) continue;
                taken[i] = changed;
                ticked++;
            }
        }

        var index = position - moved.Count(n => n - 1 < position);
        var rest = lines.Where((_, i) => !moved.Contains(i + 1)).ToList();
        rest.InsertRange(index, taken);
        return (string.Join("\n", rest) + (endsWithBreak ? "\n" : string.Empty), index + 1, ticked);
    }

    /// <summary>
    /// A line of a list with its task box ticked ("- [ ] Milk" to "- [x] Milk"), or unticked ("[x]" or "[X]" to
    /// "[ ]"); another line as it is
    /// </summary>
    internal static string Ticked(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
        // The mark of the item: "-", "*", "+", or a number and "." or ")"
        if (i < line.Length && line[i] is '-' or '*' or '+') i++;
        else
        {
            var start = i;
            while (i < line.Length && char.IsAsciiDigit(line[i])) i++;
            if (i == start || i >= line.Length || line[i] is not ('.' or ')')) return line;
            i++;
        }

        if (i + 3 >= line.Length || line[i] != ' ') return line;
        while (i < line.Length && line[i] == ' ') i++;
        if (i + 2 >= line.Length || line[i] != '[' || line[i + 2] != ']') return line;
        var box = line[i + 1];
        var now = box == ' ' ? 'x' : box is 'x' or 'X' ? ' ' : box;
        return box == now ? line : line[..(i + 1)] + now + line[(i + 2)..];
    }
}
