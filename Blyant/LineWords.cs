using System.Globalization;
using System.Text.RegularExpressions;

namespace Blyant;

/// <summary>
/// The words of a line, for "words" and "word" in the shell (and "lines ... title"): a word is letters and digits
/// (with the accents and marks that go with them, also as characters of their own), with a "'" or "-" between two of
/// them in it ("don't", "well-known"). What is around a word (punctuation, the marks of Markdown, blanks) is not part
/// of it, and stays where it is when the word is changed. With "blanks" ("words -p" in the shell) a word is all
/// between blanks instead: "text," and "**bold**" with what is around them. Does not touch the console, so it can be
/// tested
/// </summary>
internal static partial class LineWords
{
    // The characters that join two parts of one word ("don't", "it’s", "well-known")
    private static bool Joins(char c) => c is '\'' or '’' or '-';

    // A letter or digit at a place of a line (a letter outside the basic range is two chars: both are part of it)
    private static bool IsLetterAt(string line, int i) =>
        char.IsLetterOrDigit(line, i) || (char.IsLowSurrogate(line[i]) && i > 0 && char.IsLetterOrDigit(line, i - 1));

    // An accent or another mark that goes with the letter before it ("e" and a combining acute: "é"; the vowel signs
    // of Hindi), or a joiner of two letters
    private static bool IsMarkAt(string line, int i) =>
        CharUnicodeInfo.GetUnicodeCategory(line[i]) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark ||
        line[i] is '‌' or '‍';

    /// <summary>
    /// The words first to last (from 1) are the same words counted both ways, between blanks ("-p") and not: the word
    /// of each number is the only word inside the word between blanks of that number ("text" in "text,"). False when
    /// a number points at another word, or at a part of one ("a/b" is one word between blanks and two without, and a
    /// "-" alone counts only between blanks), or at none
    /// </summary>
    internal static bool SameWords(string line, int first, int last)
    {
        var between = Find(line, true);
        var words = Find(line);
        for (var i = first; i <= last; i++)
        {
            if (i < 1 || i > between.Count || i > words.Count) return false;
            var b = between[i - 1];
            var inside = words.Where(w => w.Start >= b.Start && w.Start + w.Length <= b.Start + b.Length).ToList();
            if (inside is not [var only] || only != words[i - 1]) return false;
        }

        return true;
    }

    /// <summary>
    /// The words of a line, in order: where each starts, and how long it is ("blanks": all between blanks)
    /// </summary>
    internal static List<(int Start, int Length)> Find(string line, bool blanks = false)
    {
        var words = new List<(int, int)>();
        var i = 0;
        if (blanks)
        {
            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i]))
                {
                    i++;
                    continue;
                }

                var from = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                words.Add((from, i - from));
            }

            return words;
        }

        while (i < line.Length)
        {
            if (!IsLetterAt(line, i))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < line.Length && (IsLetterAt(line, i) || IsMarkAt(line, i) ||
                                       (Joins(line[i]) && i + 1 < line.Length && IsLetterAt(line, i + 1))))
            {
                i++;
            }

            words.Add((start, i - start));
        }

        return words;
    }

    /// <summary>
    /// The column of a place in a line, as the editor shows it on the status line: from 1, a wide character (and an
    /// emoji) two columns
    /// </summary>
    internal static int ColumnOf(string line, int index) => TextMetrics.WidthOfRange(line, 0, index) + 1;

    /// <summary>
    /// The place in a line of a column (as ColumnOf gives it): a column one past the last character is the end of the
    /// line. Null for a column past that, or in the middle of a wide character
    /// </summary>
    internal static int? IndexOfColumn(string line, int column)
    {
        if (column < 1) return null;
        var i = 0;
        var cells = 0;
        while (cells + 1 < column)
        {
            if (i >= line.Length) return null;
            var length = TextMetrics.ElementLength(line, i);
            cells += TextMetrics.Width(line, i, length);
            i += length;
        }

        return cells + 1 == column ? i : null;
    }

    /// <summary>
    /// The words of a line numbered for the screen, with the column each starts at, "#1:1 This  #2:6 is  #3:9 a ...",
    /// in rows no wider than the width (a word longer than that has a row to itself)
    /// </summary>
    internal static List<string> Numbered(string line, int width, bool blanks = false)
    {
        var rows = new List<string>();
        var row = string.Empty;
        var words = Find(line, blanks);
        for (var n = 0; n < words.Count; n++)
        {
            // ("#" before the number: told apart from a word that is a number, "#3:9 5"; word takes "#3" and "#3:9"
            // too. The column after ":", for "lines ... at 9")
            var part = "#" + (n + 1) + ":" + ColumnOf(line, words[n].Start) + " " +
                       line.Substring(words[n].Start, words[n].Length);
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
    /// A word with the first letter capital, the rest as it is ("the" is "The", "NASA" stays "NASA"; with marks
    /// before it, as a word between blanks has them: "**bold**" is "**Bold**"); "title" of "word" and "lines"
    /// </summary>
    internal static string Title(string word)
    {
        var first = 0;
        while (first < word.Length && !char.IsLetterOrDigit(word[first])) first++;
        if (first == word.Length) return word;
        return word[..first] + char.ToUpperInvariant(word[first]) + word[(first + 1)..];
    }

    // The parts of a line that title leaves as they are: code in backticks, the address of a link "](...)" or
    // "<...>", and a web address
    [GeneratedRegex(@"`[^`]*`|\]\([^)]*\)|<[^>\s]*>|\b[A-Za-z][A-Za-z0-9+.-]*://[^\s)\]>]+")]
    private static partial Regex KeptAsIs();

    /// <summary>
    /// A line with each word in title case ("lines ... title"), but code, link addresses and web addresses as they
    /// are; the rest as it is
    /// </summary>
    internal static string TitleLine(string line)
    {
        var kept = KeptAsIs().Matches(line).Select(match => (match.Index, End: match.Index + match.Length)).ToList();
        var words = Find(line).Where(word => !kept.Any(part => word.Start >= part.Index && word.Start < part.End))
            .ToList();
        return ChangeEach(line, words, Title);
    }

    /// <summary>
    /// A line with the words first to last (from 1) changed: "replace" (all of them by the text), "before" and "after"
    /// (the text put in front of the first or after the last, with a blank between), "delete" (taken out, see
    /// Deleted), "upper", "lower" and "title"
    /// </summary>
    internal static string Change(string line, int first, int last, string action, string text, bool blanks = false)
    {
        var words = Find(line, blanks);
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

    // The words changed one by one, the rest of the line as it is (put together again from the parts between them,
    // so a word changed in length does not move the rest)
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

    // The marks that close one that opens: "(" with ")", "**" with "**", a quote with a quote
    private static string Closing(string opening) =>
        new(opening.Reverse().Select(c => c switch
        {
            '(' => ')', '[' => ']', '{' => '}', '<' => '>', '“' => '”', '‘' => '’', '«' => '»',
            _ => c
        }).ToArray());

    // Punctuation that ends a word in a sentence: kept when the word is taken out ("the end." is "the.")
    private static bool Closes(char c) => c is '.' or ',' or ';' or ':' or '!' or '?' or '…';

    // Words taken out. Marks around them that open and close each other go with them ("**bold**", "(see)", "\"why\"");
    // marks that do not (the "(" of "(see chapter 3)") stay, and so does the punctuation that ends a sentence or a
    // part of it ("the end." is "the."). Then the blank space is put right: one of the blanks around them goes, none
    // is left at the start or end of the line, and none between a mark and what it opens or closes
    private static string Deleted(string line, List<(int Start, int Length)> words, int first, int last)
    {
        var from = words[first - 1].Start;
        var to = words[last - 1].Start + words[last - 1].Length;
        // The part between blanks the words are in, and whether another word is in it too ("a" of "a/b")
        var partFrom = from;
        while (partFrom > 0 && !char.IsWhiteSpace(line[partFrom - 1])) partFrom--;
        var partTo = to;
        while (partTo < line.Length && !char.IsWhiteSpace(line[partTo])) partTo++;
        var alone = words.All(word => word.Start + word.Length <= partFrom || word.Start >= partTo ||
                                      (word.Start >= from && word.Start + word.Length <= to));
        if (alone)
        {
            var opening = line[partFrom..from];
            var closing = Closing(opening);
            if (opening.Length > 0 && line[to..partTo].StartsWith(closing, StringComparison.Ordinal))
            {
                from = partFrom;
                to += closing.Length;
            }
        }

        var before = line[..from];
        var after = line[to..];
        // (the blank space where the words were)
        var space = before.Length > 0 && char.IsWhiteSpace(before[^1]);
        if (space && (after.Length == 0 || char.IsWhiteSpace(after[0]) || Closes(after[0]) ||
                      after[0] is ')' or ']' or '}' or '”' or '’' or '»'))
        {
            // "a b c" without b: one blank; "the end." without end: "the."; at the end: no blank left
            before = after.Length == 0 ? before.TrimEnd() : before[..^1];
        }
        else if (after.Length > 0 && char.IsWhiteSpace(after[0]) &&
                 (before.Length == 0 || before[^1] is '(' or '[' or '{' or '“' or '‘' or '«' or '"' or '*' or '_'))
        {
            // "b c" without b, "(see chapter" without see: no blank at the start or after the mark
            after = after.TrimStart();
        }

        return before + after;
    }
}
