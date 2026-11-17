namespace Blyant;

/// <summary>
/// The words of a line, for "words" and "word" in the shell (and "lines ... title"): a word is letters and digits,
/// with a "'" or "-" between two of them in it ("don't", "well-known"). What is around a word (punctuation, the marks
/// of Markdown, blanks) is not part of it, and stays where it is when the word is changed. Does not touch the
/// console, so it can be tested
/// </summary>
internal static class LineWords
{
    // The characters that join two parts of one word ("don't", "it’s", "well-known")
    private static bool Joins(char c) => c is '\'' or '’' or '-';

    /// <summary>
    /// The words of a line, in order: where each starts, and how long it is
    /// </summary>
    internal static List<(int Start, int Length)> Find(string line)
    {
        var words = new List<(int, int)>();
        var i = 0;
        while (i < line.Length)
        {
            if (!char.IsLetterOrDigit(line[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < line.Length && (char.IsLetterOrDigit(line[i]) ||
                                       (Joins(line[i]) && i + 1 < line.Length && char.IsLetterOrDigit(line[i + 1]))))
            {
                i++;
            }

            words.Add((start, i - start));
        }

        return words;
    }

    /// <summary>
    /// The words of a line numbered for the screen, "1 This  2 is  3 a ...", in rows no wider than the width (a word
    /// longer than that has a row to itself)
    /// </summary>
    internal static List<string> Numbered(string line, int width)
    {
        var rows = new List<string>();
        var row = string.Empty;
        var words = Find(line);
        for (var n = 0; n < words.Count; n++)
        {
            var part = (n + 1) + " " + line.Substring(words[n].Start, words[n].Length);
            if (row.Length > 0 && TextMetrics.WidthOf(row) + 2 + TextMetrics.WidthOf(part) > width)
            {
                rows.Add(row);
                row = string.Empty;
            }

            row += (row.Length > 0 ? "  " : string.Empty) + part;
        }

        if (row.Length > 0) rows.Add(row);
        return rows;
    }

    /// <summary>
    /// A word with the first letter capital and the rest small ("tHE" is "The"); "title" of "word" and "lines"
    /// </summary>
    internal static string Title(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    /// <summary>
    /// A line with each word in title case ("lines ... title"); the rest as it is
    /// </summary>
    internal static string TitleLine(string line) => ChangeEach(line, Find(line), Title);

    /// <summary>
    /// A line with the words first to last (from 1) changed: "replace" (all of them by the text), "before" and "after"
    /// (the text put in front of the first or after the last, with a blank between), "delete" (taken out, with what
    /// is around them up to the blanks when nothing else is there, and one blank), "upper", "lower" and "title"
    /// </summary>
    internal static string Change(string line, int first, int last, string action, string text)
    {
        var words = Find(line);
        var from = words[first - 1].Start;
        var to = words[last - 1].Start + words[last - 1].Length;
        switch (action)
        {
            case "replace":
                return line[..from] + text + line[to..];
            case "before":
                return line[..from] + text + " " + line[from..];
            case "after":
                return line[..to] + " " + text + line[to..];
            case "delete":
                return Deleted(line, words, first, last);
            case "upper":
                return ChangeEach(line, words.GetRange(first - 1, last - first + 1), word => word.ToUpperInvariant());
            case "lower":
                return ChangeEach(line, words.GetRange(first - 1, last - first + 1), word => word.ToLowerInvariant());
            default:
                return ChangeEach(line, words.GetRange(first - 1, last - first + 1), Title);
        }
    }

    // The words changed one by one, the rest of the line as it is (the words do not change in length: ToUpper of
    // one letter can be two, so they are put together again from the parts between them)
    private static string ChangeEach(string line, IReadOnlyList<(int Start, int Length)> words, Func<string, string> change)
    {
        var result = new System.Text.StringBuilder();
        var at = 0;
        foreach (var (start, length) in words)
        {
            result.Append(line, at, start - at);
            result.Append(change(line.Substring(start, length)));
            at = start + length;
        }

        result.Append(line, at, line.Length - at);
        return result.ToString();
    }

    // Words taken out: with what is around them up to the blanks ("text," and "**bold**" go whole) when the words are
    // all of what is there (not "a" of "a/b"), and then one of the blanks around them, so no two are left
    private static string Deleted(string line, List<(int Start, int Length)> words, int first, int last)
    {
        var from = words[first - 1].Start;
        var to = words[last - 1].Start + words[last - 1].Length;
        // The part between blanks the words are in
        var partFrom = from;
        while (partFrom > 0 && !char.IsWhiteSpace(line[partFrom - 1])) partFrom--;
        var partTo = to;
        while (partTo < line.Length && !char.IsWhiteSpace(line[partTo])) partTo++;
        // (no other word in that part, before or after the ones taken out)
        var alone = words.All(word => word.Start + word.Length <= partFrom || word.Start >= partTo ||
                                      (word.Start >= from && word.Start + word.Length <= to));
        if (alone) (from, to) = (partFrom, partTo);
        // One blank goes with them: the one after, or at the end of the line the one before
        if (to < line.Length && line[to] == ' ' && (from == 0 || char.IsWhiteSpace(line[from - 1]))) to++;
        else if (from > 0 && line[from - 1] == ' ' && (to == line.Length || char.IsWhiteSpace(line[to]))) from--;
        return line[..from] + line[to..];
    }
}
