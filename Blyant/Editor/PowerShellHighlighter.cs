namespace Blyant;

/// <summary>
/// Finds the parts of a line of PowerShell to color: comments ("#", and &lt;# ... #&gt; over several lines),
/// strings in ' and " (which can go over several lines), here-strings (@" ... "@ and @' ... '@), variables
/// ($name, $env:PATH, ${any name}), which are colored as names, the words of the language and the operators
/// (-eq, -and ...) in any letter case, and numbers.
/// </summary>
internal static class PowerShellHighlighter
{
    // The state a line starts in
    private const int Normal = 0;
    private const int InComment = 1; // <# ... #>
    private const int InSingle = 2; // '...'
    private const int InDouble = 3; // "..."
    private const int InHereSingle = 4; // @' ... '@
    private const int InHereDouble = 5; // @" ... "@

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "begin", "break", "catch", "class", "continue", "data", "do", "dynamicparam", "else", "elseif", "end",
        "enum", "exit", "filter", "finally", "for", "foreach", "function", "hidden", "if", "in", "param", "process",
        "return", "static", "switch", "throw", "trap", "try", "until", "using", "while", "workflow"
    };

    private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "ne", "gt", "ge", "lt", "le", "like", "notlike", "match", "notmatch", "contains", "notcontains", "in",
        "notin", "replace", "split", "join", "is", "isnot", "as", "and", "or", "xor", "not", "band", "bor", "bxor",
        "bnot", "shl", "shr", "f", "ceq", "cne", "clike", "cmatch", "creplace", "ieq", "ilike", "imatch"
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
        while (i < end)
        {
            if (state != Normal)
            {
                // Inside a comment or a string that began before: up to its end
                var (to, closed) = state switch
                {
                    InComment => EndOf(line, i, "#>"),
                    InSingle => (StringEnd(line, i, '\'', out var single), single),
                    InDouble => (StringEnd(line, i, '"', out var quoted), quoted),
                    // A here-string ends at a line that starts with "@ (or '@)
                    _ => line.StartsWith(state == InHereDouble ? "\"@" : "'@", StringComparison.Ordinal)
                        ? (2, true)
                        : (end, false)
                };
                Fill(styles, i, to, state == InComment ? SyntaxStyle.Comment : SyntaxStyle.String);
                if (!closed) return state;
                state = Normal;
                i = to;
                continue;
            }

            var c = line[i];
            var next = i + 1 < end ? line[i + 1] : '\0';
            if (c == '<' && next == '#')
            {
                Fill(styles, i, i + 2, SyntaxStyle.Comment);
                state = InComment;
                i += 2;
            }
            else if (c == '#')
            {
                Fill(styles, i, end, SyntaxStyle.Comment);
                return Normal;
            }
            else if (c == '@' && next is '"' or '\'' && line.AsSpan(i + 2).IsWhiteSpace())
            {
                // A here-string: its text is on the next lines
                Fill(styles, i, end, SyntaxStyle.String);
                return next == '"' ? InHereDouble : InHereSingle;
            }
            else if (c is '"' or '\'')
            {
                var to = StringEnd(line, i + 1, c, out var closed);
                Fill(styles, i, to, SyntaxStyle.String);
                if (!closed) return c == '"' ? InDouble : InSingle;
                i = to;
            }
            else if (c == '$')
            {
                i = Variable(line, i, styles);
            }
            else if (c == '-' && IsNameStart(next) && (i == 0 || !IsNameChar(line[i - 1])))
            {
                // An operator (-eq, -and); the parameters of commands (-Path) are not colored
                var to = NameEnd(line, i + 1);
                if (Operators.Contains(line[(i + 1)..to])) Fill(styles, i, to, SyntaxStyle.Keyword);
                i = to;
            }
            else if (IsNameStart(c))
            {
                // A word: also the name of a command (Get-ChildItem), which is not colored
                var to = i + 1;
                while (to < end && (IsNameChar(line[to]) || (line[to] == '-' && to + 1 < end && IsNameStart(line[to + 1]))))
                {
                    to++;
                }

                // (not a member: $x.Count, [Math]::Round)
                var member = i > 0 && line[i - 1] is '.' or ':';
                if (!member && Keywords.Contains(line[i..to])) Fill(styles, i, to, SyntaxStyle.Keyword);
                i = to;
            }
            else if (char.IsAsciiDigit(c) && (i == 0 || !IsNameChar(line[i - 1])))
            {
                // 12, 0x1F, 1.5, 10kb
                var to = i + 1;
                while (to < end && (char.IsAsciiLetterOrDigit(line[to]) || line[to] == '.')) to++;
                Fill(styles, i, to, SyntaxStyle.Number);
                i = to;
            }
            else
            {
                i++;
            }
        }

        return state;
    }

    // A variable at "$": $name, $env:PATH, $script:count, $_, ${any name}. "$(" is left as it is (the code in it
    // is read as code)
    private static int Variable(string line, int i, SyntaxStyle[] styles)
    {
        var end = line.Length;
        int to;
        if (i + 1 < end && line[i + 1] == '{')
        {
            var close = line.IndexOf('}', i + 2);
            to = close < 0 ? end : close + 1;
        }
        else if (i + 1 < end && (IsNameChar(line[i + 1]) || line[i + 1] is '?' or '^' or '$'))
        {
            to = i + 2;
            if (IsNameChar(line[i + 1]))
            {
                to = NameEnd(line, i + 1);
                // A scope or a drive: $env:PATH, $global:x
                if (to + 1 < end && line[to] == ':' && IsNameStart(line[to + 1])) to = NameEnd(line, to + 1);
            }
        }
        else
        {
            return i + 1;
        }

        Fill(styles, i, to, SyntaxStyle.Name);
        return to;
    }

    // Where a string ends: after its quote, or at the end of the line. A quote is written twice inside it ('it''s'),
    // and in "..." a backtick escapes the character after it
    private static int StringEnd(string line, int from, char quote, out bool closed)
    {
        var i = from;
        while (i < line.Length)
        {
            if (quote == '"' && line[i] == '`')
            {
                i += 2;
                continue;
            }

            if (line[i] == quote)
            {
                if (i + 1 < line.Length && line[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }

                closed = true;
                return i + 1;
            }

            i++;
        }

        closed = false;
        return line.Length;
    }

    private static (int, bool) EndOf(string line, int from, string mark)
    {
        var at = line.IndexOf(mark, from, StringComparison.Ordinal);
        return at < 0 ? (line.Length, false) : (at + mark.Length, true);
    }

    private static int NameEnd(string line, int from)
    {
        while (from < line.Length && IsNameChar(line[from])) from++;
        return from;
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style) =>
        JavaScriptHighlighter.Fill(styles, from, to, style);
}
