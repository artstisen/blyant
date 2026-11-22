namespace Blyant;

/// <summary>
/// "shift": lines of a file moved to another place in it (under a heading, before a line, to the top or the bottom,
/// up or down by lines), without opening it, as items of a list are moved from "Todo" to "Done"; with "-s" a whole
/// section (a heading and what is under it), also up or down past the sections at its level. "shift!" also ticks the task boxes of the
/// lines moved ("- [ ]" to "- [x]"), or unticks those that are ticked. The lines a command takes (PickedLines) and
/// the place in a file (PlaceIn) serve "lines" too, and tell what is wrong; the rest (the line sets, the headings, the
/// move) does not touch the console, so it can be tested
/// </summary>
internal sealed partial class ShellSession
{
    // "shift todo.md 4-6 Done": the lines (by number: "4", "4-6", "4,7,9-10"; or the one line that has a text in it:
    // "Buy milk") to the place: under the heading "Done" (after a blank line under it), "Done+" at the end of its
    // section, "+12" before line 12, "top" or "bottom", or "up" and "down" by a line or more ("down 3"). The numbers
    // are those of the file as it is. "shift -s notes.md Ideas up": the section of a heading instead of lines. As
    // insert and change: its line endings and byte order mark kept, only a file in UTF-8 and not one Blyant uses, and
    // the file is then the last list, at the lines moved. Lines are not lost by it, so it is not asked
    private void Shift(string argument, bool tick)
    {
        var command = tick ? "shift!" : "shift";
        var section = Words(argument) is ["-s", ..];
        if (section) argument = AfterWords(argument, 1);
        var words = Words(argument);
        var typedPlace = AfterWords(argument, 2).Trim();
        var place = typedPlace.Trim('"').Trim();
        if (words.Count < 3 || place.Length == 0 || words[1].Trim().Length == 0)
        {
            Error(section
                ? Lang.F("Give a file, a heading and the place: {0} -s notes.md Ideas up", command)
                : Lang.F("Give a file, the lines and the place: {0} todo.md 4-6 Done", command));
            return;
        }

        if (EditableFile(words[0]) is not { } editable) return;
        var (item, file) = editable;
        var lines = LinesOfText(file.Text);
        var headings = Headings(file.Text, IsTypst(item.Path));
        (int Line, int Level, string Name)? heading = null;
        List<int> moved;
        if (section)
        {
            if (SectionHeading(words[1], headings, lines, item.Path) is not { } found) return;
            heading = found;
            moved = SectionLines(lines, headings, found);
        }
        else if (PickedLines(words[1], null, lines, item.Path) is { } picked) moved = picked;
        else return;

        // "up", "down 3" (not in quotes: "Up" in quotes is a heading)
        int position;
        if (!typedPlace.StartsWith('"') && Step(place) is { } step)
        {
            if (heading is { } of)
            {
                if (SectionStep(lines, headings, of, step.Up, step.Count) is not { } over)
                {
                    Line(step.Up
                        ? Lang.F("Nothing moved: no section above {0} at its level", of.Name)
                        : Lang.F("Nothing moved: no section below {0} at its level", of.Name));
                    return;
                }

                position = over;
            }
            else position = step.Up ? Math.Max(0, moved[0] - 1 - step.Count) : (int)Math.Min(lines.Count, (long)moved[^1] + step.Count);
        }
        else if (PlaceIn(place, lines, file.Text, item.Path, moved) is { } at)
        {
            position = at;
            // (a section at the bottom goes before the blank lines at the end of the file, which stay last)
            if (heading is not null && at == lines.Count)
            {
                while (position > 0 && lines[position - 1].Trim().Length == 0 && !moved.Contains(position)) position--;
            }
        }
        else return;

        var (text, first, ticked) = Shifted(file.Text, moved, position, tick);
        // (before the spacing of a section: blank lines put in where nothing moved are no move)
        if (text == TextBuffer.Normalize(file.Text))
        {
            Line(Lang.T("Nothing moved: the lines are there already"));
            return;
        }

        if (heading is not null) (text, first) = SpacedSection(file.Text, text, moved, first);

        // (the path and the lines, never the text)
        if (!SavedEdit(file, text, heading is not null ? "Shift section" : "Shift lines",
                item.Path + ":" + Numbers(moved) + " to " + first))
        {
            return;
        }

        LastList = new List<Item> { new(item.Path, false, (first, 1)) };
        var now = LinesOfText(text);
        if (heading is { } moving)
        {
            // (a section can be long: its heading, and how many lines)
            Line(first.ToString().PadLeft(first.ToString().Length + 2) + "  " + now[first - 1]);
            Line(Lang.F("{0}: section {1} ({2} lines) moved to line {3}", item.Path, moving.Name, moved.Count, first));
        }
        else
        {
            Rows(now, first, first + moved.Count - 1);
            Line(moved.Count == 1
                ? Lang.F("{0}: line moved to line {1}", item.Path, first)
                : Lang.F("{0}: {1} lines moved to line {2}", item.Path, moved.Count, first));
        }

        if (tick) Line(Lang.F("Task boxes ticked or unticked: {0}", ticked));
    }

    /// <summary>
    /// "up", "down", "up 3", "down 2" (letter case alike): the way and how many lines (or sections). Null for another
    /// place. A count larger than the file stops at its top or bottom
    /// </summary>
    internal static (bool Up, int Count)? Step(string place)
    {
        var parts = place.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2) return null;
        var way = parts[0].ToLowerInvariant();
        if (way is not ("up" or "down")) return null;
        var count = 1;
        if (parts.Length == 2)
        {
            if (parts[1].Length == 0 || !parts[1].All(char.IsAsciiDigit)) return null;
            count = int.TryParse(parts[1], out var given) ? given : int.MaxValue;
            if (count < 1) return null;
        }

        return (way == "up", count);
    }

    // The heading of "shift -s": by a line in the section ("12", "+12", as "@" gives it: the heading above it, or on
    // it) or by its name, as a place is found (whole, a pattern, the start). Null (and told) if it is not there, or
    // several fit
    private (int Line, int Level, string Name)? SectionHeading(string word, List<(int Line, int Level, string Name)> headings,
        List<string> lines, string path)
    {
        if (headings.Count == 0)
        {
            Error(Lang.F("{0} has no headings: shift -s moves a section under a heading", Path.GetFileName(path)));
            return null;
        }

        var number = word.StartsWith('+') ? word[1..] : word;
        if (number.Length > 0 && number.All(char.IsAsciiDigit))
        {
            var line = int.TryParse(number, out var given) ? given : int.MaxValue;
            if (line <= lines.Count && headings.LastOrDefault(h => h.Line <= line) is { Name: not null } at) return at;
            Error(line > lines.Count
                ? Lang.F("{0} has {1} lines", path, lines.Count)
                : Lang.F("Line {0} is above the first heading: give a heading (shift -s notes.md Ideas up)", line));
            return null;
        }

        var name = HeadingName(word);
        var (found, fitted) = FindHeadings(name, headings);
        if (found.Count == 1) return found[0];
        foreach (var h in found.Count > 1 ? found : headings) Line("  " + h.Line + "  " + lines[h.Line - 1]);
        Error(found.Count == 0
            ? Lang.F("No heading {0} in {1}", name, Path.GetFileName(path))
            : fitted
                ? Lang.F("{0} headings fit {1}: give more of the name, or the line (+12)", found.Count, name)
                : Lang.F("{0} headings are named {1}: give the line (+12)", found.Count, name));
        return null;
    }

    /// <summary>
    /// The lines of the section of a heading (from 1): the heading and what is under it, up to the next heading of
    /// the same level or above, its blank lines at the end too. The last section of the file ends at its last line
    /// that is not blank: the blank lines at the end of the file stay there
    /// </summary>
    internal static List<int> SectionLines(List<string> lines, List<(int Line, int Level, string Name)> headings,
        (int Line, int Level, string Name) heading)
    {
        var next = headings.FirstOrDefault(h => h.Line > heading.Line && h.Level <= heading.Level).Line;
        var end = next > 0 ? next - 1 : lines.Count;
        if (next == 0)
        {
            while (end > heading.Line && lines[end - 1].Trim().Length == 0) end--;
        }

        return Enumerable.Range(heading.Line, end - heading.Line + 1).ToList();
    }

    /// <summary>
    /// Where a section goes ("up" or "down", past "count" sections): before the line (from 0) of the file as it is.
    /// Only the sections of the same level under the same heading count; it stops at the first or last of them. Null
    /// when there is none to pass
    /// </summary>
    internal static int? SectionStep(List<string> lines, List<(int Line, int Level, string Name)> headings,
        (int Line, int Level, string Name) heading, bool up, int steps)
    {
        int? position = null;
        var at = heading;
        for (var i = 0; i < steps; i++)
        {
            if (up)
            {
                // The heading before it at its level or above: one of its level is the section above
                var before = headings.LastOrDefault(h => h.Line < at.Line && h.Level <= heading.Level);
                if (before.Name is null || before.Level != heading.Level) break;
                at = before;
                position = before.Line - 1;
            }
            else
            {
                var section = SectionLines(lines, headings, at);
                var after = headings.FirstOrDefault(h => h.Line == section[^1] + 1);
                if (after.Name is null || after.Level != heading.Level) break;
                at = after;
                position = SectionLines(lines, headings, after)[^1];
            }
        }

        return position;
    }

    /// <summary>
    /// A section moved keeps the file spaced as it was: when a blank line was above the heading or at the end of the
    /// section, there is one blank line above the section at the new place and one after it (not two: blank lines it
    /// brings and blank lines that were there are not added up), and the file ends as before (with as many blank
    /// lines, and with a line break or not). The text, and the line the heading is at now
    /// </summary>
    internal static (string Text, int At) SpacedSection(string before, string text, List<int> moved, int at)
    {
        var was = LinesOfText(TextBuffer.Normalize(before));
        var spaced = (moved[0] >= 2 && was[moved[0] - 2].Trim().Length == 0) || was[moved[^1] - 1].Trim().Length == 0;
        if (!spaced) return (text, at);
        var endsWithBreak = TextBuffer.Normalize(before).EndsWith('\n');
        var lines = LinesOfText(text);
        var end = at - 1 + moved.Count; // (from 0: the line after the section)
        // (the section ends with a blank line, and one was there already: only the section's)
        while (end < lines.Count && lines[end - 1].Trim().Length == 0 && lines[end].Trim().Length == 0)
        {
            lines.RemoveAt(end);
        }

        if (end < lines.Count && lines[end - 1].Trim().Length > 0 && lines[end].Trim().Length > 0)
        {
            lines.Insert(end, string.Empty);
        }
        if (at > 1 && lines[at - 2].Trim().Length > 0)
        {
            lines.Insert(at - 1, string.Empty);
            at++;
        }

        // (blank lines at the end of the file: as many as before, taken away or put back)
        var blankEnd = was.AsEnumerable().Reverse().TakeWhile(line => line.Trim().Length == 0).Count();
        var blank = lines.AsEnumerable().Reverse().TakeWhile(line => line.Trim().Length == 0).Count();
        for (; blank > blankEnd && lines.Count > at; blank--) lines.RemoveAt(lines.Count - 1);
        for (; blank < blankEnd; blank++) lines.Add(string.Empty);

        return (JoinedLines(lines, endsWithBreak), at);
    }

    /// <summary>
    /// The commands that take "@" as the file and the line: in the place of the file (after "-p" or "-s"). Tab
    /// completion counts "@" as two words for them
    /// </summary>
    internal static readonly HashSet<string> TakesAt = new()
    {
        "words", "word", "change", "insert", "input", "a", "i", "blank", "peek", "shift", "shift!", "lines", "open", "edit"
    };

    // The names of input: "input", and "a" and "i" as in the line editor ed (after and before the line)
    internal static bool IsInput(string name) => name is "input" or "a" or "i";

    // "word @ 9 replace wish": "@" in the place of the file is $file at $line, the file and the line the last command
    // worked on ("$file +12"; for insert the line after it, "+13", so the text goes after the line), or where the
    // cursor was in the editor (see FileLine). In an empty file (after "new") it is the top, line 0: insert puts the
    // first line there, open, edit and peek take the file from line 1, and the others are refused. Another word, or
    // "@" in a text ("insert @ Ask @anna"), is as it is. Null (and told) when there is no line to take
    internal string? WithAt(string name, string argument)
    {
        // ("input" and "a" alone: after $line, as "input @"; "i" alone or "i @", "input before" and "input @ before":
        // before $line, as the i of ed)
        var input = IsInput(name);
        // ("input -e", "input -e @": $line itself, to edit; "input -e +12": line 12 of $file)
        var editing = name == "input" && Words(argument) is ["-e", ..];
        if (editing && Words(argument) is ["-e", var lineOnly] && IsPlusLine(lineOnly, out _))
        {
            if (_file is not null) return "-e $file " + lineOnly;
            Error(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"));
            return null;
        }

        if (editing && Words(argument) is ["-e"]) argument = "-e @";
        // ("input +12", "a +12", "i +12": $file, the lines as line 12, as "input $file +12" puts them)
        // ("blank +12", "blank +12 3": the same, with a count)
        if ((input && Words(argument) is [var only] && IsPlusLine(only, out _)) ||
            (name == "blank" && Words(argument) is [var lineWord, ..] and ([_] or [_, _]) && IsPlusLine(lineWord, out _)))
        {
            if (_file is not null) return "$file " + argument.Trim();
            Error(Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)"));
            return null;
        }

        var before = name == "i" ? Words(argument) is [] or ["@"] : name == "input" && Words(argument) is ["before"] or ["@", "before"];
        if (before || (input && argument.Trim().Length == 0)) argument = "@";
        // ("blank" alone, and "blank 3": after $line, as "blank @" and "blank @ 3")
        if (name == "blank" && Words(argument) is var given && (given is [] || (given is [var many] && IsNumber(many, out _))))
        {
            argument = ("@ " + argument.Trim()).TrimEnd();
        }
        // ("clip words @ 6-9": after the kind, as word takes it)
        var clipWords = name == "clip" && CommandLine.Name(argument).Equals("words", StringComparison.OrdinalIgnoreCase);
        if (!TakesAt.Contains(name) && !clipWords) return argument;
        var words = Words(argument);
        var at = clipWords ? 1 : 0;
        while (at < words.Count && (words[at] is "-p" or "-s" || (editing && words[at] == "-e"))) at++;
        if (at >= words.Count || words[at] != "@") return argument;
        var from = AfterWords(argument, at);
        // (a file named "@" is written in quotes)
        if (!from.TrimStart().StartsWith('@')) return argument;
        if (_file is null || FileLine is not { } line)
        {
            Error(_file is null
                ? Lang.T("@ is the file and the line of the last command: none yet (words notes.md 12)")
                : Lang.F("@ has no line in {0} yet: peek it, or give the line (insert {0} +5 ...)",
                    Path.GetFileName(_file)));
            return null;
        }

        // The line checked against the file as it is now (changed since, by another program or the editor): a line
        // past the end is refused, not taken as the end. An empty file is at its top. (A file that cannot be read is
        // told by the command)
        if (LineCount(_file).Count is { } count)
        {
            if (count == 0) line = 0;
            else if (line > count)
            {
                Error(Lang.F("@ is line {0}, and {1} has {2} lines: line bottom sets the last", line,
                    Path.GetFileName(_file), count));
                return null;
            }
        }

        // (the top of a file, before line 1, as in an empty file: open, edit and peek take the file from line 1, and
        // only insert can put a line there; the others have no line to work on)
        if (line == 0 && name is "open" or "edit" or "peek") line = 1;
        if (line == 0 && name is not ("insert" or "blank") && !input)
        {
            Error(Lang.F("@ is at the top of {0}, before line 1: insert @ text puts a line there",
                Path.GetFileName(_file)));
            return null;
        }

        var head = argument[..(argument.Length - from.Length)];
        var rest = AfterWords(argument, at + 1);
        // (before: as line $line, the line it is goes down; in an empty file, at the top)
        var put = "$file +" + (editing ? line : before ? Math.Max(line, 1) : name is "insert" or "blank" || input ? line + 1 : line);
        return head + put + (rest.Length > 0 ? " " + rest : string.Empty);
    }

    // The lines of a file a command takes (from 1, in order): by number ("4", "4-6", "4,7,9-10"), the one line with a
    // text in it ("Buy milk"), or with a count "+100 20" (20 lines from line 100, as peek takes them). Null (and told)
    // if they are not there
    private List<int>? PickedLines(string word, string? count, List<string> lines, string path)
    {
        if (count is not null)
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
                    : Lang.F("{0} lines have {1} in them: give the line number", having.Count, word));
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
        if (IsPlusLine(place, out var before))
        {
            if (moved is not null && moved.Contains(before))
            {
                Error(Lang.F("Target {0} is among the lines moved", place));
                return null;
            }

            return Math.Min(before, lines.Count + 1) - 1;
        }

        var headings = Headings(text, IsTypst(path));
        var atEnd = place.EndsWith('+');
        var name = HeadingName(atEnd ? place[..^1] : place);
        // (a heading whose name ends with "+", "## C++": that heading, not the end of the section of "C+")
        if (atEnd && headings.Any(h => string.Equals(h.Name, HeadingName(place), StringComparison.OrdinalIgnoreCase)))
        {
            atEnd = false;
            name = HeadingName(place);
        }

        var (found, fitted) = FindHeadings(name, headings);
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

        if (moved is not null && moved.Contains(found[0].Line))
        {
            Error(Lang.F("Target {0} is among the lines moved", place));
            return null;
        }

        return atEnd ? EndOfSection(lines, headings, found[0]) : UnderHeading(lines, found[0].Line);
    }

    // The headings a name stands for: those of that whole name (letter case alike); when none, the ones a pattern
    // fits, or the ones that start with it ("fitted": not by the whole name). A heading with "*" or "?" in it is found
    // whole first, and a start with "?" in it ("Why?") as it is when no pattern fits
    private static (List<(int Line, int Level, string Name)> Found, bool Fitted) FindHeadings(string name,
        List<(int Line, int Level, string Name)> headings)
    {
        var found = headings.Where(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var fitted = found.Count == 0 && name.Length > 0;
        if (fitted && ShellCompletion.IsPattern(name))
        {
            found = headings.Where(h => ShellCompletion.PatternFits(name, h.Name)).ToList();
        }

        if (fitted && found.Count == 0)
        {
            found = headings.Where(h => h.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return (found, fitted);
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
            if (fence is null && (line.StartsWith("```") || line.StartsWith("~~~")))
            {
                fence = line[..3];
                continue;
            }

            if (fence is not null)
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
        return (JoinedLines(rest, endsWithBreak), index + 1, ticked);
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
