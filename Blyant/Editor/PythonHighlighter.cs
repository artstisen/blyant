namespace Blyant;

/// <summary>
/// Finds the parts of a line of Python to color: comments (from "#"), strings (in ' or ", with a prefix as r, b or
/// f, and strings in three quotes, which go on over several lines), the words of the language, numbers, and
/// decorators (@property), which are colored as names.
/// </summary>
internal static class PythonHighlighter
{
    // The state a line starts in: inside a string in three quotes, or not
    private const int Normal = 0;
    private const int InTripleSingle = 1;
    private const int InTripleDouble = 2;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "case", "class", "continue",
        "def", "del", "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in", "is",
        "lambda", "match", "nonlocal", "not", "or", "pass", "raise", "return", "self", "try", "while", "with",
        "yield"
    };

    /// <summary>
    /// Find the styles of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="state">The state the line starts in (0 at the start of a file)</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    /// <returns>The state after the line</returns>
    internal static int Scan(string line, int state, SyntaxStyle[] styles)
    {
        var end = line.Length;
        var i = 0;
        if (state != Normal)
        {
            // Inside a string in three quotes that began before: to its end
            var to = TripleEnd(line, 0, end, state == InTripleDouble ? '"' : '\'', out var closed);
            Fill(styles, 0, to, SyntaxStyle.String);
            if (!closed) return state;
            i = to;
        }

        while (i < end)
        {
            var c = line[i];
            if (c == '#')
            {
                Fill(styles, i, end, SyntaxStyle.Comment);
                return Normal;
            }

            if (IsNameStart(c))
            {
                var to = i + 1;
                while (to < end && (char.IsLetterOrDigit(line[to]) || line[to] == '_')) to++;
                if (to < end && line[to] is '"' or '\'' && IsPrefix(line.AsSpan(i, to - i)))
                {
                    // r"text", b'...', f"...", rb"..."
                    var after = String(line, i, to, end, styles, out var open);
                    if (open != Normal) return open;
                    i = after;
                    continue;
                }

                // A word of the language, but not a member (x.match)
                if (!(i > 0 && line[i - 1] == '.') && Keywords.Contains(line[i..to]))
                {
                    Fill(styles, i, to, SyntaxStyle.Keyword);
                }

                i = to;
            }
            else if (c is '"' or '\'')
            {
                var after = String(line, i, i, end, styles, out var open);
                if (open != Normal) return open;
                i = after;
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < end && char.IsAsciiDigit(line[i + 1])))
            {
                var to = i + 1;
                while (to < end && (char.IsLetterOrDigit(line[to]) || line[to] is '.' or '_')) to++;
                Fill(styles, i, to, SyntaxStyle.Number);
                i = to;
            }
            else if (c == '@' && i + 1 < end && IsNameStart(line[i + 1]) && IsLineStart(line, i))
            {
                // A decorator: @property, @app.route
                var to = i + 1;
                while (to < end && (char.IsLetterOrDigit(line[to]) || line[to] is '_' or '.')) to++;
                Fill(styles, i, to, SyntaxStyle.Name);
                i = to;
            }
            else
            {
                i++;
            }
        }

        return Normal;
    }

    // A string from its prefix (at start, or start == quote) and its quote: where it ends. "open" is the state of
    // the next line if it is in three quotes and goes on. (In a raw string, r"...", a "\" is kept, but a quote after
    // it does not end the string either, so it is read as the other strings)
    private static int String(string line, int start, int quote, int end, SyntaxStyle[] styles, out int open)
    {
        open = Normal;
        var c = line[quote];
        int to;
        if (quote + 2 < end && line[quote + 1] == c && line[quote + 2] == c)
        {
            to = TripleEnd(line, quote + 3, end, c, out var closed);
            if (!closed) open = c == '"' ? InTripleDouble : InTripleSingle;
        }
        else
        {
            to = quote + 1;
            while (to < end && line[to] != c)
            {
                if (line[to] == '\\') to++;
                to++;
            }

            to = Math.Min(to + 1, end);
        }

        Fill(styles, start, to, SyntaxStyle.String);
        return to;
    }

    // Where a string in three quotes ends: after the three quotes, or at the end of the line
    private static int TripleEnd(string line, int from, int end, char quote, out bool closed)
    {
        var i = from;
        while (i < end)
        {
            if (line[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (line[i] == quote && i + 2 < end && line[i + 1] == quote && line[i + 2] == quote)
            {
                closed = true;
                return i + 3;
            }

            i++;
        }

        closed = false;
        return Math.Min(i, end);
    }

    // r, b, f, u, and two of them with r (rb, br, fr, rf), in any letter case
    private static bool IsPrefix(ReadOnlySpan<char> word)
    {
        if (word.Length is 0 or > 2) return false;
        foreach (var c in word)
        {
            if (char.ToLowerInvariant(c) is not ('r' or 'b' or 'f' or 'u')) return false;
        }

        return word.Length == 1 || word.Contains('r') || word.Contains('R');
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    // True if only blank space is before the character on the line
    private static bool IsLineStart(string line, int i)
    {
        for (var at = 0; at < i; at++)
        {
            if (!char.IsWhiteSpace(line[at])) return false;
        }

        return true;
    }

    private static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style) =>
        JavaScriptHighlighter.Fill(styles, from, to, style);
}
