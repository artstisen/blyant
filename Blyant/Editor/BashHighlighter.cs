namespace Blyant;

/// <summary>
/// Finds the parts of a line of a shell script (Bash, sh) to color: comments (from a "#" that starts a word),
/// strings in ' and " (which can go over several lines), here-documents (&lt;&lt;EOF to the line with EOF),
/// variables ($name, ${name}, $1, $?), which are colored as names, the words of the language and numbers.
/// </summary>
internal static class BashHighlighter
{
    // The state a line starts in: inside a string or a here-document (with a hash of its marker above StateBits)
    private const int Normal = 0;
    private const int InSingle = 1;
    private const int InDouble = 2;
    private const int InHeredoc = 3;
    private const int StateBits = 2;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "then", "else", "elif", "fi", "case", "esac", "for", "select", "while", "until", "do", "done", "in",
        "function", "time", "coproc", "return", "exit", "break", "continue", "local", "export", "readonly",
        "declare", "typeset", "unset", "shift", "source", "eval", "exec", "trap", "set", "true", "false"
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
        if ((state & ((1 << StateBits) - 1)) == InHeredoc)
        {
            // To the line that is its marker (also after tabs, with <<-)
            var marker = CStyleHighlighter.HeredocMarkerEnd(line, 0, end, state >> StateBits);
            Fill(styles, 0, end, SyntaxStyle.String);
            return marker < 0 || !line.AsSpan(marker).IsWhiteSpace() ? state : Normal;
        }

        // A here-document that starts on this line: its text begins on the next line
        var heredoc = Normal;
        while (i < end)
        {
            if (state is InSingle or InDouble)
            {
                var to = StringEnd(line, i, state == InSingle ? '\'' : '"', state == InDouble, out var closed);
                Fill(styles, i, to, SyntaxStyle.String);
                if (!closed) return state;
                state = Normal;
                i = to;
                continue;
            }

            var c = line[i];
            var next = i + 1 < end ? line[i + 1] : '\0';
            if (c == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1]) || line[i - 1] is ';' or '&' or '|' or '('))
            {
                Fill(styles, i, end, SyntaxStyle.Comment);
                break;
            }

            if (c == '\\')
            {
                // An escaped character: \" and \$ are not a string or a variable
                i += 2;
            }
            else if (c is '\'' or '"' || (c == '$' && next == '\''))
            {
                // '...', "..." and $'...'
                var quote = c == '$' ? '\'' : c;
                var from = c == '$' ? i + 2 : i + 1;
                var to = StringEnd(line, from, quote, c == '$' || quote == '"', out var closed);
                Fill(styles, i, to, SyntaxStyle.String);
                if (!closed) state = quote == '"' ? InDouble : InSingle;
                i = to;
            }
            else if (c == '$')
            {
                i = Variable(line, i, styles);
            }
            else if (c == '<' && next == '<' && (i + 2 >= end || line[i + 2] != '<') &&
                     (i == 0 || line[i - 1] != '<') && !InArithmetic(line, i) &&
                     HeredocMarker(line, i + 2) is var (marker, markerEnd))
            {
                // <<EOF, <<-EOF, << 'EOF' and <<"EOF"
                Fill(styles, i, markerEnd, SyntaxStyle.String);
                heredoc = InHeredoc | (CStyleHighlighter.MarkerHash(marker) << StateBits);
                i = markerEnd;
            }
            else if (IsWordChar(c))
            {
                var to = i + 1;
                while (to < end && IsWordChar(line[to])) to++;
                var word = line[i..to];
                // A word of the language is a word by itself: not a part of a path (/bin/test) or an option
                // (--if), and not a name given a value (if=1)
                var alone = (i == 0 || !IsWordChar(line[i - 1])) && (to >= end || line[to] != '=');
                if (alone && Keywords.Contains(word))
                {
                    Fill(styles, i, to, SyntaxStyle.Keyword);
                }
                else if (alone && word.All(char.IsAsciiDigit))
                {
                    Fill(styles, i, to, SyntaxStyle.Number);
                }

                i = to;
            }
            else
            {
                i++;
            }
        }

        // (a string that goes on to the next line comes first)
        return state != Normal ? state : heredoc;
    }

    // A variable at "$": $name, ${name...}, $1, and $? $# $@ $* $$ $! $-. "$(" is left as it is (the command in it
    // is read as code)
    private static int Variable(string line, int i, SyntaxStyle[] styles)
    {
        var end = line.Length;
        var next = i + 1 < end ? line[i + 1] : '\0';
        int to;
        if (next == '{')
        {
            var close = line.IndexOf('}', i + 2);
            to = close < 0 ? end : close + 1;
        }
        else if (char.IsLetter(next) || next == '_')
        {
            to = i + 2;
            while (to < end && (char.IsLetterOrDigit(line[to]) || line[to] == '_')) to++;
        }
        else if (char.IsAsciiDigit(next) || next is '?' or '#' or '@' or '*' or '$' or '!' or '-')
        {
            to = i + 2;
        }
        else
        {
            return i + 1;
        }

        Fill(styles, i, to, SyntaxStyle.Name);
        return to;
    }

    // Where a string ends: after its quote, or at the end of the line. In "..." (and $'...') a backslash escapes
    private static int StringEnd(string line, int from, char quote, bool escapes, out bool closed)
    {
        var i = from;
        while (i < line.Length)
        {
            if (escapes && line[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (line[i] == quote)
            {
                closed = true;
                return i + 1;
            }

            i++;
        }

        closed = false;
        return line.Length;
    }

    // After "<<": "-", blank space, and the marker (EOF, 'EOF' or "EOF"). The marker and where it ends, or null
    private static (string, int)? HeredocMarker(string line, int from)
    {
        var i = from;
        if (i < line.Length && line[i] == '-') i++;
        while (i < line.Length && line[i] == ' ') i++;
        var quote = i < line.Length && line[i] is '"' or '\'' ? line[i] : '\0';
        if (quote != '\0') i++;
        var name = i;
        while (i < line.Length && (char.IsAsciiLetterOrDigit(line[i]) || line[i] == '_')) i++;
        // (not 1<<2)
        if (i == name || char.IsAsciiDigit(line[name])) return null;
        var marker = line[name..i];
        if (quote != '\0')
        {
            if (i >= line.Length || line[i] != quote) return null;
            i++;
        }

        return (marker, i);
    }

    // True if a place on the line is inside "((" ... "))" (arithmetic, where "<<" is a shift: $((1 << n)))
    private static bool InArithmetic(string line, int i)
    {
        var open = 0;
        for (var at = 0; at + 1 < i; at++)
        {
            if (line[at] == '(' && line[at + 1] == '(')
            {
                open++;
                at++;
            }
            else if (line[at] == ')' && line[at + 1] == ')' && open > 0)
            {
                open--;
                at++;
            }
        }

        return open > 0;
    }

    // The characters of a word: a command, an option, a path or a value
    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or '/';

    private static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style) =>
        JavaScriptHighlighter.Fill(styles, from, to, style);
}
