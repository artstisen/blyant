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
    private static readonly string[] WordActions = { "replace", "before", "after", "delete", "upper", "lower", "title" };

    // The line "words" (or a change by "word") numbered last, and how: between blanks ("-p") or not. "word" on that
    // line counted the other way is refused when the numbers point at other words (see LineWords.SameWords).
    // Forgotten when another command changes the file (the line can have moved; see Execute)
    private (string Path, int Line, bool Blanks)? _wordsShown;

    // The width the numbered words are shown in (79 columns when it is not known, as to a file)
    private int WordsWidth() => Terminal && WindowSize?.Invoke() is { Width: > 20 } size ? size.Width - 3 : 77;

    // The line of a file a command is about ("words notes.md 12", "+12" too): the file, its lines and the line (from
    // 1); null (and told) when it is not there
    private (Item Item, TextFile File, List<string> Lines, int Line)? LineOf(string fileWord, string lineWord, string usage)
    {
        if (!LinesToChange(lineWord, out var line, out var last) || last != line)
        {
            Error(usage);
            return null;
        }

        if (EditableFile(fileWord) is not { } editable) return null;
        var lines = LinesOfText(editable.File.Text);
        if (line > lines.Count)
        {
            Error(Lang.F("{0} has {1} lines", editable.Item.Path, lines.Count));
            return null;
        }

        return (editable.Item, editable.File, lines, line);
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

    // The numbers of words as words shows them, "#5-#7", as typed without "#": "5-7" (one "#" before each number)
    internal static string WithoutHashes(string numbers) =>
        string.Join('-', numbers.Split('-').Select(part => part.StartsWith('#') ? part[1..] : part));

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
            Error(usage);
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
            Error(usage);
            return;
        }

        var action = words[3].ToLowerInvariant();
        if (!WordActions.Contains(action))
        {
            Error(Lang.F("Not taken: {0} (replace, before, after, delete, upper, lower or title)", words[3]));
            return;
        }

        // ("#9", "#5-#7", "#5-7": the numbers as words shows them)
        if (!LinesToChange(WithoutHashes(words[2]), out var first, out var last))
        {
            Error(Lang.F("Not words: {0} (as 9, or 5-7)", words[2]));
            return;
        }

        // The text after replace, before and after: as typed (in quotes: as it is, blanks too)
        var text = AfterWords(argument, blanks ? 5 : 4).Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"' && text.Count(c => c == '"') == 2) text = text[1..^1];
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
        // ("words -p", then "word" without -p, or the other way: the numbers seen are of the other count. Refused
        // when they point at other words: "a/b" is word 6 between blanks, "b" without. First, so a number seen that
        // the other count does not have is told this way too; a number not seen is told by the counts below)
        if (_wordsShown is { } shown && shown.Blanks != blanks && shown.Line == at.Line &&
            FileOperations.NameComparer.Equals(shown.Path, Path.GetFullPath(at.Item.Path)) &&
            last <= LineWords.Find(line, shown.Blanks).Count && !LineWords.SameWords(line, first, last))
        {
            Error(shown.Blanks
                ? Lang.F("words -p numbered line {0}, and without -p the numbers differ: give -p to word too", at.Line)
                : Lang.F("words numbered line {0} without -p, and with -p the numbers differ: leave out -p", at.Line));
            return;
        }

        var count = LineWords.Find(line, blanks).Count;
        if (count == 0)
        {
            Error(Lang.F("Line {0} has no words", at.Line));
            return;
        }

        if (last > count)
        {
            Error(count == 1
                ? Lang.F("Line {0} has 1 word: {1} is not one", at.Line, last)
                : Lang.F("Line {0} has {1} words: {2} is not one", at.Line, count, last));
            return;
        }

        var changed = LineWords.Change(line, first, last, action, text, blanks);
        if (changed == line)
        {
            Line(Lang.T("Nothing changed: the words are so already"));
            return;
        }

        at.Lines[at.Line - 1] = changed;
        var normalized = TextBuffer.Normalize(at.File.Text);
        var result = SaveEdit(at.File, string.Join("\n", at.Lines) + (normalized.EndsWith('\n') ? "\n" : string.Empty));
        // (the path, what was done and the line, never the text)
        FileExplorer.LogFileOperation("Edit words (" + action + ")", at.Item.Path + ":" + at.Line, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { new(at.Item.Path, false, (at.Line, 1)) };
        // (the words are shown numbered again, this way)
        _wordsShown = (Path.GetFullPath(at.Item.Path), at.Line, blanks);
        Line(at.Line.ToString().PadLeft(4) + "  " + changed);
        Line(Lang.F("{0}: line {1} changed", at.Item.Path, at.Line));
        if (LineWords.Find(changed, blanks).Count > 0) ShowWords(at.Item.Path, at.Line, changed, blanks);
    }
}
