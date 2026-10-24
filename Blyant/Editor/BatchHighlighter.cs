namespace Blyant;

/// <summary>
/// Finds the parts of a line of a Windows batch file (.bat, .cmd) to color: comments (REM and "::"), labels
/// (":name"), variables (%name%, %1, %%i, %~dp0 and !name!), strings in quotes, the words of the language (in any
/// letter case) and numbers. Each line is read by itself: nothing goes on to the next line.
/// </summary>
internal static class BatchHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "else", "for", "in", "do", "goto", "call", "exit", "set", "setlocal", "endlocal", "echo", "off", "on",
        "not", "exist", "defined", "errorlevel", "equ", "neq", "lss", "leq", "gtr", "geq", "shift", "pushd", "popd",
        "cd", "chdir", "start", "pause", "cls", "title", "copy", "xcopy", "del", "erase", "move", "md", "mkdir", "rd",
        "rmdir", "ren", "rename", "type", "choice", "nul", "con", "enabledelayedexpansion", "enableextensions"
    };

    /// <summary>
    /// Find the styles of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    internal static void Scan(string line, SyntaxStyle[] styles)
    {
        var end = line.Length;
        var i = 0;
        while (i < end && (char.IsWhiteSpace(line[i]) || line[i] == '@')) i++;
        if (i + 1 < end && line[i] == ':' && line[i + 1] == ':')
        {
            Fill(styles, i, end, SyntaxStyle.Comment);
            return;
        }

        if (i < end && line[i] == ':')
        {
            // A label
            Fill(styles, i, end, SyntaxStyle.Name);
            return;
        }

        i = 0;
        while (i < end)
        {
            var c = line[i];
            if (c == '"')
            {
                var close = line.IndexOf('"', i + 1);
                var to = close < 0 ? end : close + 1;
                Fill(styles, i, to, SyntaxStyle.String);
                // The variables in it are names
                for (var at = i + 1; at < to;)
                {
                    at = line[at] is '%' or '!' ? Variable(line, at, to, styles) : at + 1;
                }

                i = to;
            }
            else if (c is '%' or '!')
            {
                i = Variable(line, i, end, styles);
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                var to = i + 1;
                while (to < end && (char.IsLetterOrDigit(line[to]) || line[to] is '_' or '.' or '-')) to++;
                var word = line[i..to];
                // (a word that is part of a path or an option, as \echo or /off, is not a word of the language)
                var alone = i == 0 || line[i - 1] is not ('\\' or '/' or ':' or '.' or '-');
                if (alone && word.Equals("rem", StringComparison.OrdinalIgnoreCase) &&
                    (to >= end || char.IsWhiteSpace(line[to])))
                {
                    Fill(styles, i, end, SyntaxStyle.Comment);
                    return;
                }

                if (alone && Keywords.Contains(word))
                {
                    // (with the @ in front of it: @echo)
                    Fill(styles, i > 0 && line[i - 1] == '@' ? i - 1 : i, to, SyntaxStyle.Keyword);
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
    }

    // A variable at "%" or "!": %name%, %1, %*, %%i, %~dp0, %~nx1, !name!. Where it ends (after the "%" or "!"
    // when it is not a variable)
    private static int Variable(string line, int i, int end, SyntaxStyle[] styles)
    {
        var mark = line[i];
        int to;
        if (mark == '%' && i + 1 < end && (char.IsAsciiDigit(line[i + 1]) || line[i + 1] == '*'))
        {
            to = i + 2;
        }
        else if (mark == '%' && i + 2 < end && line[i + 1] == '%' && char.IsLetter(line[i + 2]))
        {
            // %%i (in a for loop)
            to = i + 3;
        }
        else if (mark == '%' && i + 1 < end && line[i + 1] == '~')
        {
            // %~dp0, %~nx1, %~f%%i
            to = i + 2;
            while (to < end && char.IsLetter(line[to])) to++;
            if (to < end && char.IsAsciiDigit(line[to])) to++;
        }
        else
        {
            // %name% and !name!: to the same mark, with no blank space before it
            var close = line.IndexOf(mark, i + 1, end - i - 1);
            if (close <= i + 1 || line.AsSpan(i + 1, close - i - 1).ContainsAny(' ', '\t')) return i + 1;
            to = close + 1;
        }

        Fill(styles, i, Math.Min(to, end), SyntaxStyle.Name);
        return Math.Min(to, end);
    }

    private static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style) =>
        JavaScriptHighlighter.Fill(styles, from, to, style);
}
