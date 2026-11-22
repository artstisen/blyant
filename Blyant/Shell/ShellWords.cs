using System.Globalization;

namespace Blyant;

/// <summary>
/// "words" and "word": the words of one line of a file numbered ("words notes.md 12"), and a word or words of it
/// changed by their numbers ("word notes.md 12 9 replace wish"), without opening the file. See LineWords for what a
/// word is. As the other line commands: only a file in UTF-8 and not one Blyant uses, the line endings and byte order
/// mark kept, the file then the last list at that line ($file), undo takes a change back. What is written is never
/// logged, only the path and the line
/// </summary>
internal sealed partial class ShellSession
{
    // What "word" does to the words: the first three take a text after them
    private static readonly string[] WordActions = { "replace", "before", "after", "delete", "move", "upper", "lower", "title" };

    // The line "words" (or a change by "word") numbered last, and how: between blanks ("-p") or not. "word" on that
    // line counted the other way is refused when the numbers point at other words (see LineWords.SameWords).
    // Forgotten when another command changes the file (the line can have moved; see Execute)
    private (string Path, int Line, bool Blanks)? _wordsShown;

    // The width the numbered words are shown in (79 columns when it is not known, as to a file)
    private int WordsWidth() => WindowColumns(DefaultColumns) - 2;

    // The line of a file a command is about ("words notes.md 12", "+12" too): the file, its lines and the line (from
    // 1); null (and told) when it is not there
    // "readOnly": the file is only read (clip words): any text file, also one Blyant uses or one not in UTF-8
    private (Item Item, TextFile File, List<string> Lines, int Line)? LineOf(string fileWord, string lineWord, string usage,
        bool readOnly = false)
    {
        if (!LinesToChange(lineWord, out var line, out var last) || last != line)
        {
            Error(usage);
            return null;
        }

        (Item Item, TextFile File) file;
        if (readOnly)
        {
            if (Existing(fileWord) is not { } item || ReadableFile(item) is not { } read) return null;
            file = (item, read);
        }
        else if (EditableFile(fileWord) is { } editable) file = editable;
        else return null;

        var lines = LinesOfText(file.File.Text);
        if (line > lines.Count)
        {
            Error(Lang.F("{0} has {1} lines", file.Item.Path, lines.Count));
            return null;
        }

        return (file.Item, file.File, lines, line);
    }

    // The words of a line, numbered, a row at a time ("blanks": all between blanks, "-p"). With the setting
    // shellmarkers, between "-> notes.md, line 12" and "<- 9 words" (the whole line: the markers of the top and the end)
    private void ShowWords(string path, int number, string line, bool blanks)
    {
        if (Shell.Markers) Line(TopMarker + Lang.F("{0}, line {1}", Path.GetFileName(path), number));
        foreach (var row in LineWords.Numbered(line, WordsWidth(), blanks)) Line("  " + row);
        if (!Shell.Markers) return;
        var count = LineWords.Find(line, blanks).Count;
        Line(EndMarker + (count == 1 ? Lang.T("1 word") : Lang.F("{0} words", count)));
    }

    /// <summary>
    /// The numbers of words as words shows them: "9", "#9", "#9:32" (with the column the word starts at), "5-7",
    /// "#5-#7", "#5:20-#7:31". Each a number, with one "#" before it or none, and after ":" only digits. Not "+9", "9:",
    /// "9:x" or "5-+7". The columns given are in "columns", to check against the line as it is now
    /// </summary>
    internal static bool WordNumbers(string text, out int first, out int last, out List<(int Word, int Column)> columns)
    {
        first = last = 0;
        columns = new List<(int Word, int Column)>();
        var parts = text.Split('-');
        if (parts.Length > 2) return false;
        var numbers = new List<int>();
        foreach (var typed in parts)
        {
            var part = typed.StartsWith('#') ? typed[1..] : typed;
            var colon = part.IndexOf(':');
            var column = colon >= 0 ? part[(colon + 1)..] : null;
            if (colon >= 0) part = part[..colon];
            if (!IsCount(part, out var number) || (column is not null && !IsCount(column, out _))) return false;
            numbers.Add(number);
            if (column is not null) columns.Add((number, int.Parse(column, CultureInfo.InvariantCulture)));
        }

        first = numbers[0];
        last = numbers[^1];
        return last >= first;

        // A whole number from 1, in ASCII digits only
        static bool IsCount(string digits, out int value)
        {
            value = 0;
            return digits.Length is > 0 and <= 9 && digits.All(char.IsAsciiDigit) &&
                   int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
        }
    }

    // The words given are in the line as words showed them: the other count is not taken for them (words -p, then word
    // without -p: "a/b" is word 6 between blanks, "b" without), each number is a word of the line, and a column given
    // ("#9:32") is where the word starts now. "ranges": the words, and a move's target. False (and told) when not
    private bool WordsAsShown(string path, int lineNumber, string line, bool blanks, IReadOnlyList<(int First, int Last)> ranges,
        IReadOnlyList<(int Word, int Column)> columns)
    {
        // (a number seen that the other count does not have is told this way too; a number not seen by the counts below)
        if (_wordsShown is { } shown && shown.Blanks != blanks && shown.Line == lineNumber &&
            FileOperations.NameComparer.Equals(shown.Path, Path.GetFullPath(path)))
        {
            var other = LineWords.Find(line, shown.Blanks).Count;
            if (ranges.Any(range => range.Last <= other && !LineWords.SameWords(line, range.First, range.Last)))
            {
                Error(shown.Blanks
                    ? Lang.F("words -p numbered line {0}, and without -p the numbers differ: give -p to word too", lineNumber)
                    : Lang.F("words numbered line {0} without -p, and with -p the numbers differ: leave out -p", lineNumber));
                return false;
            }
        }

        var found = LineWords.Find(line, blanks);
        if (found.Count == 0)
        {
            Error(Lang.F("Line {0} has no words", lineNumber));
            return false;
        }

        var past = ranges.Select(range => range.Last).Where(number => number > found.Count).DefaultIfEmpty(0).Max();
        if (past > 0)
        {
            Error(found.Count == 1
                ? Lang.F("Line {0} has 1 word: {1} is not one", lineNumber, past)
                : Lang.F("Line {0} has {1} words: {2} is not one", lineNumber, found.Count, past));
            return false;
        }

        foreach (var (word, column) in columns)
        {
            var now = LineWords.ColumnOf(line, found[word - 1].Start);
            if (now == column) continue;
            Error(Lang.F("Word {0} starts at column {1} now, not {2}: words shows the line as it is", word, now, column));
            return false;
        }

        return true;
    }

    // "-p" first ("words -p notes.md 12", "word -p ..."): a word is all between blanks, with the punctuation and the
    // marks around it. Taken off the words; null (and told) for another option
    private bool? BlanksOption(List<string> words)
    {
        if (words.Count == 0 || !words[0].StartsWith('-')) return false;
        if (words[0] != "-p")
        {
            Error(Lang.F("Not taken: {0} (-p: words between blanks, with punctuation)", words[0]));
            return null;
        }

        words.RemoveAt(0);
        return true;
    }

    // "words notes.md 12": the words of line 12, numbered, for "word"; the file is then $file, at that line
    private void WordsCommand(string argument)
    {
        var words = Words(argument);
        if (BlanksOption(words) is not { } blanks) return;
        var usage = Lang.T("Give a file and a line: words notes.md 12");
        if (words.Count != 2)
        {
            // ("words notes.md 12 9 replace wish": word was meant, a letter away)
            if (words.Count >= 4 && WordActions.Contains(words[3].ToLowerInvariant()))
                Error(Lang.T("To change words: word notes.md 12 9 replace wish (words only shows them)"));
            else Error(usage);
            return;
        }

        if (LineOf(words[0], words[1], usage) is not { } at) return;
        var text = at.Lines[at.Line - 1];
        LastList = new List<Item> { new(at.Item.Path, false, (at.Line, 1)) };
        _wordsShown = (Path.GetFullPath(at.Item.Path), at.Line, blanks);
        var count = LineWords.Find(text, blanks).Count;
        if (count == 0)
        {
            Line(Lang.F("{0}, line {1}: no words", at.Item.Path, at.Line));
            return;
        }

        ShowWords(at.Item.Path, at.Line, text, blanks);
        // (the markers tell the file, the line and the words)
        if (Shell.Markers) return;
        Line(count == 1
            ? Lang.F("{0}, line {1}: 1 word", at.Item.Path, at.Line)
            : Lang.F("{0}, line {1}: {2} words", at.Item.Path, at.Line, count));
    }

    // "word notes.md 12 9 replace wish", "word notes.md 12 14 before handful of", "word notes.md 12 5-6 delete",
    // "word notes.md 12 1-4 title": the words of line 12 changed by their numbers (as "words" shows them). The text is
    // all that follows the action, as typed (in quotes as it is). The line is shown changed, and its words numbered
    // again, as the numbers can have moved
    private void WordCommand(string argument)
    {
        var words = Words(argument);
        if (BlanksOption(words) is not { } blanks) return;
        var usage = Lang.T("Give a file, a line, the words and what to do: word notes.md 12 9 replace wish");
        if (words.Count < 4)
        {
            // ("word notes.md 12": words was meant, a letter away)
            if (words.Count == 2) Error(Lang.T("To see the words of a line: words notes.md 12 (word changes them)"));
            else Error(usage);
            return;
        }

        var action = words[3].ToLowerInvariant();
        if (!WordActions.Contains(action))
        {
            Error(Lang.F("Not taken: {0} (replace, before, after, delete, move, upper, lower or title)", words[3]));
            return;
        }

        // ("#9", "#5-#7", "#5-7", "#9:32": the numbers as words shows them)
        if (!WordNumbers(words[2], out var first, out var last, out var columns))
        {
            Error(Lang.F("Not words: {0} (as 9, or 5-7)", words[2]));
            return;
        }

        // The text after replace, before and after: as typed (in quotes: as it is, blanks too)
        var text = AfterWords(argument, blanks ? 5 : 4).Trim();
        text = Unquoted(text) ?? text;
        // "move before 6", "move after 11": where the words go, by the number of a word as words showed it (before
        // the move; "#6" and "#6:19" too)
        var target = 0;
        var moveBefore = false;
        if (action == "move")
        {
            var where = Words(text);
            if (where.Count != 2 || where[0].ToLowerInvariant() is not ("before" or "after") ||
                !WordNumbers(where[1], out target, out var targetLast, out var targetColumns) || targetLast != target)
            {
                Error(Lang.T("Give where to: word notes.md 12 9 move before 6 (or after 6)"));
                return;
            }

            moveBefore = where[0].Equals("before", StringComparison.OrdinalIgnoreCase);
            columns.AddRange(targetColumns);
            text = string.Empty;
        }

        var takesText = action is "replace" or "before" or "after";
        if (takesText && text.Length == 0)
        {
            Error(Lang.F("Give the text: word notes.md 12 9 {0} wish", action));
            return;
        }

        if (!takesText && text.Length > 0)
        {
            Error(Lang.F("Nothing goes after {0}: word notes.md 12 9 {0}", action));
            return;
        }

        if (LineOf(words[0], words[1], usage) is not { } at) return;
        var line = at.Lines[at.Line - 1];
        // (the words, and the target of a move, as words showed them)
        var ranges = action == "move" ? new[] { (first, last), (target, target) } : new[] { (first, last) };
        if (!WordsAsShown(at.Item.Path, at.Line, line, blanks, ranges, columns)) return;
        if (action == "move" && target >= first && target <= last)
        {
            Error(Lang.F("Word {0} is one of the words moved: give a word outside them", target));
            return;
        }

        // (Moved gives null only when the words could not be counted again after they are taken out: a guard, as
        // taking them out leaves the other words as they were in every line tried)
        var changed = action == "move"
            ? LineWords.Moved(line, first, last, moveBefore, target, blanks)
            : LineWords.Change(line, first, last, action, text, blanks);
        if (changed is null)
        {
            Error(Lang.T("The words cannot be moved there: give the numbers words shows now"));
            return;
        }

        if (changed == line)
        {
            Line(Lang.T("Nothing changed: the words are so already"));
            return;
        }

        at.Lines[at.Line - 1] = changed;
        // (the path, what was done and the line, never the text)
        if (!SavedEdit(at.File, JoinedAs(at.File, at.Lines), "Edit words (" + action + ")", at.Item.Path + ":" + at.Line)) return;

        LastList = new List<Item> { new(at.Item.Path, false, (at.Line, 1)) };
        // (the words are shown numbered again, this way)
        _wordsShown = (Path.GetFullPath(at.Item.Path), at.Line, blanks);
        Line(at.Line.ToString().PadLeft(4) + "  " + changed);
        Line(Lang.F("{0}: line {1} changed", at.Item.Path, at.Line));
        if (LineWords.Find(changed, blanks).Count > 0) ShowWords(at.Item.Path, at.Line, changed, blanks);
    }
}
