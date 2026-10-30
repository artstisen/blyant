namespace Blyant;

/// <summary>
/// Finds the parts of a line of Typst (.typ) that are to be colored in the editor. Typst has three modes: markup
/// (the text, with headings "= ", *strong*, _emphasis_, lists, `raw`, labels, references and web addresses), math
/// (between $ and $) and code (after #, and inside the brackets of code). Code can hold content in [brackets],
/// which is markup again.
///
/// The state a line ends in tells the brackets that are open (for each, whether it holds code or content), and
/// whether the line ends inside a block comment, a block of raw text (```) or math. This is a simple reading and
/// not a complete one: strong and emphasis are found on one line only, a statement (#let, #set ...) goes on to the
/// end of its line (or a ";"), and brackets nested more than 23 deep are not followed.
/// This class does not touch the console.
/// </summary>
internal static class TypstHighlighter
{
    // The state: the open brackets, one bit each from the outermost (1: content [..], 0: code (..) or {..}), how
    // many there are, and the blocks the line ends inside
    private const int MaxDepth = 23;
    private const int DepthShift = 23;
    private const int DepthMask = 0x1F;
    private const int InComment = 1 << 28;
    private const int InRaw = 1 << 29;
    private const int InMath = 1 << 30;

    // The words of the code of Typst
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "let", "set", "show", "import", "include", "if", "else", "for", "while", "in", "not", "and", "or", "as",
        "break", "continue", "return", "context", "none", "auto", "true", "false"
    };

    // The words after # that start a statement, which goes on to the end of the line
    private static readonly HashSet<string> Statements = new(StringComparer.Ordinal)
    {
        "let", "set", "show", "import", "include", "if", "for", "while", "context", "return", "break", "continue"
    };

    // The units of numbers (2pt, 90deg, 1fr)
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    {
        "pt", "mm", "cm", "in", "em", "fr", "deg", "rad"
    };

    /// <summary>
    /// True if the name of a file says it is a Typst file
    /// </summary>
    internal static bool IsTypstFile(string fileName) =>
        Path.GetExtension(fileName).Equals(".typ", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Find the styles of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="state">The state the line starts in (0 for the first line)</param>
    /// <param name="styles">The style of each character (as long as the line)</param>
    /// <returns>The state after the line</returns>
    internal static int Scan(string line, int state, SyntaxStyle[] styles)
    {
        var reader = new Reader(line, state, styles);
        reader.Run();
        return reader.State;
    }

    private sealed class Reader(string line, int state, SyntaxStyle[] styles)
    {
        private int _pos;
        private int _stack = state & ((1 << MaxDepth) - 1);
        private int _depth = (state >> DepthShift) & DepthMask;
        private bool _comment = (state & InComment) != 0;
        private bool _raw = (state & InRaw) != 0;
        private bool _math = (state & InMath) != 0;

        // Brackets beyond the deepest that is followed
        private int _overflow;

        // A statement (#let, #set ...) in markup: code to the end of the line, at the depth it started at
        private bool _statement;
        private int _statementDepth;

        // Where a call in markup (#name) has its brackets: after "(..)" or "[..]" closes at that depth, another
        // may follow (#figure(..)[..])
        private readonly List<int> _calls = new();

        // Markup at the start of the line (or of a block of content), before its first character that is not blank
        private bool _lineStart = true;

        internal int State => _stack | (Math.Min(_depth, MaxDepth) << DepthShift) | (_comment ? InComment : 0) |
                              (_raw ? InRaw : 0) | (_math ? InMath : 0);

        private bool TopIsContent => _depth > 0 && ((_stack >> (_depth - 1)) & 1) == 1;

        private bool InCode => _statement && _depth == _statementDepth || _depth > 0 && !TopIsContent;

        internal void Run()
        {
            if (_raw)
            {
                RawBlockLine();
            }

            while (_pos < line.Length)
            {
                if (_comment) BlockComment();
                else if (_math) MathText();
                else if (InCode) CodeStep();
                else MarkupStep();
            }
        }

        private void Fill(SyntaxStyle style, int from, int to) => Array.Fill(styles, style, from, to - from);

        private char At(int i) => i < line.Length ? line[i] : '\0';

        private void Push(bool content)
        {
            if (_depth >= MaxDepth)
            {
                _overflow++;
                return;
            }

            _stack = content ? _stack | (1 << _depth) : _stack & ~(1 << _depth);
            _depth++;
        }

        private void Pop()
        {
            if (_overflow > 0)
            {
                _overflow--;
                return;
            }

            if (_depth > 0) _depth--;
            if (_statement && _depth < _statementDepth) _statement = false;
            // A call goes on with more brackets ("#f(x)[body]"), and "] else [" goes on with the statement
            if (_calls.Count > 0 && _calls[^1] == _depth)
            {
                if (At(_pos) is '(' or '[')
                {
                    Push(At(_pos) == '[');
                    _pos++;
                    return;
                }

                _calls.RemoveAt(_calls.Count - 1);
            }

            var word = _pos;
            while (word < line.Length && line[word] == ' ') word++;
            if (!line.AsSpan(word).StartsWith("else") || IsIdentPart(At(word + 4))) return;
            Fill(SyntaxStyle.Keyword, word, word + 4);
            _pos = word + 4;
            _statement = true;
            _statementDepth = _depth;
        }

        // ---- Blocks over several lines

        private void BlockComment()
        {
            var end = line.IndexOf("*/", _pos, StringComparison.Ordinal);
            var stop = end < 0 ? line.Length : end + 2;
            Fill(SyntaxStyle.Comment, _pos, stop);
            _pos = stop;
            if (end >= 0) _comment = false;
        }

        private void RawBlockLine()
        {
            var end = line.IndexOf("```", StringComparison.Ordinal);
            var stop = end < 0 ? line.Length : end + 3;
            while (end >= 0 && stop < line.Length && line[stop] == '`') stop++;
            Fill(SyntaxStyle.CodeBlock, 0, stop);
            _pos = stop;
            if (end >= 0) _raw = false;
        }

        private void MathText()
        {
            var end = _pos;
            while (end < line.Length && line[end] != '$')
            {
                end += line[end] == '\\' ? 2 : 1;
            }

            var stop = Math.Min(line.Length, end + 1);
            Fill(SyntaxStyle.Number, _pos, stop);
            _pos = stop;
            if (end < line.Length) _math = false;
        }

        // `raw`, or ``` that starts a block of raw text (over several lines, unless it ends on the line)
        private void Raw()
        {
            var start = _pos;
            var ticks = 0;
            while (At(start + ticks) == '`') ticks++;
            if (ticks >= 3)
            {
                var end = line.IndexOf(new string('`', ticks), start + ticks, StringComparison.Ordinal);
                if (end < 0)
                {
                    Fill(SyntaxStyle.CodeBlock, start, line.Length);
                    _pos = line.Length;
                    _raw = true;
                    return;
                }

                Fill(SyntaxStyle.Code, start, end + ticks);
                _pos = end + ticks;
                return;
            }

            // `raw` (`` is empty raw text)
            var close = ticks == 2 ? start + 1 : line.IndexOf('`', start + 1);
            var stop = close < 0 ? line.Length : close + 1;
            Fill(SyntaxStyle.Code, start, stop);
            _pos = stop;
        }

        private void LineComment()
        {
            Fill(SyntaxStyle.Comment, _pos, line.Length);
            _pos = line.Length;
        }

        private void StartBlockComment()
        {
            Fill(SyntaxStyle.Comment, _pos, _pos + 2);
            _pos += 2;
            _comment = true;
        }

        private void StartMath()
        {
            styles[_pos] = SyntaxStyle.Number;
            _pos++;
            _math = true;
        }

        // ---- Markup

        private void MarkupStep()
        {
            var c = line[_pos];
            if (_lineStart)
            {
                if (c is ' ' or '\t')
                {
                    _pos++;
                    return;
                }

                _lineStart = false;
                if (LineStart()) return;
            }

            switch (c)
            {
                case '\\':
                    // An escape (\#, \u{1f600}), or a line break at the end of a line
                    _pos += 2;
                    break;
                case '/' when At(_pos + 1) == '/':
                    LineComment();
                    break;
                case '/' when At(_pos + 1) == '*':
                    StartBlockComment();
                    break;
                case '`':
                    Raw();
                    break;
                case '$':
                    StartMath();
                    break;
                case '*':
                    Emphasis('*', SyntaxStyle.Bold);
                    break;
                case '_':
                    Emphasis('_', SyntaxStyle.Italic);
                    break;
                case '<':
                    if (!Label()) _pos++;
                    break;
                case '@':
                    Reference();
                    break;
                case 'h' when Url():
                    break;
                case '#':
                    Hash();
                    break;
                case '[' when _depth > 0:
                    // Brackets in content go in pairs
                    Push(true);
                    _pos++;
                    break;
                case ']' when TopIsContent:
                    _pos++;
                    Pop();
                    break;
                default:
                    _pos++;
                    break;
            }
        }

        // The start of a line of markup: a heading ("== "), or the marker of a list ("- ", "+ ", "1. ", "/ Term:")
        private bool LineStart()
        {
            var c = line[_pos];
            if (c == '=')
            {
                var level = 0;
                while (At(_pos + level) == '=') level++;
                if (At(_pos + level) is not (' ' or '\0')) return false;
                Fill(SyntaxStyle.Heading1 + Math.Min(level, 6) - 1, _pos, line.Length);
                _pos = line.Length;
                return true;
            }

            if (c is '-' or '+' && At(_pos + 1) is ' ' or '\0')
            {
                styles[_pos] = SyntaxStyle.ListMarker;
                _pos++;
                return true;
            }

            if (char.IsAsciiDigit(c))
            {
                var end = _pos;
                while (char.IsAsciiDigit(At(end))) end++;
                if (At(end) != '.' || At(end + 1) is not (' ' or '\0')) return false;
                Fill(SyntaxStyle.ListMarker, _pos, end + 1);
                _pos = end + 1;
                return true;
            }

            if (c == '/' && At(_pos + 1) == ' ')
            {
                // A term ("/ Term: description"), in bold to its colon
                styles[_pos] = SyntaxStyle.ListMarker;
                var colon = line.IndexOf(':', _pos + 2);
                if (colon > 0) Fill(SyntaxStyle.Bold, _pos + 2, colon);
                _pos = colon > 0 ? colon + 1 : _pos + 1;
                return true;
            }

            return false;
        }

        // *strong* or _emphasis_ on the line: the marks are at the edges of words
        private void Emphasis(char mark, SyntaxStyle style)
        {
            var start = _pos;
            if (start > 0 && char.IsLetterOrDigit(line[start - 1]) || char.IsWhiteSpace(At(start + 1)) ||
                start + 1 == line.Length)
            {
                _pos++;
                return;
            }

            for (var i = start + 1; i < line.Length; i++)
            {
                if (line[i] == '\\')
                {
                    i++;
                    continue;
                }

                if (line[i] != mark || i == start + 1 || char.IsWhiteSpace(line[i - 1]) ||
                    char.IsLetterOrDigit(At(i + 1)))
                {
                    continue;
                }

                Fill(style, start, i + 1);
                _pos = i + 1;
                return;
            }

            _pos++;
        }

        // <label>
        private bool Label()
        {
            var end = _pos + 1;
            if (!IsIdentStart(At(end))) return false;
            while (IsIdentPart(At(end)) || At(end) is '.' or ':' or '-') end++;
            if (At(end) != '>') return false;
            Fill(SyntaxStyle.Name, _pos, end + 1);
            _pos = end + 1;
            return true;
        }

        // @reference (not inside a word, as an e-mail address)
        private void Reference()
        {
            var end = _pos + 1;
            if (_pos > 0 && char.IsLetterOrDigit(line[_pos - 1]) || !IsIdentPart(At(end)))
            {
                _pos++;
                return;
            }

            while (IsIdentPart(At(end)) || At(end) is '.' or ':' or '-' && IsIdentPart(At(end + 1))) end++;
            Fill(SyntaxStyle.Name, _pos, end);
            _pos = end;
        }

        // A web address, without the punctuation after it
        private bool Url()
        {
            var rest = line.AsSpan(_pos);
            if (!rest.StartsWith("https://") && !rest.StartsWith("http://")) return false;
            var end = _pos;
            var open = 0;
            while (end < line.Length && !char.IsWhiteSpace(line[end]))
            {
                if (line[end] is '(' or '[') open++;
                else if (line[end] is ')' or ']' && --open < 0) break;
                end++;
            }

            while (end > _pos && line[end - 1] is '.' or ',' or ';' or ':' or '!' or '?') end--;
            Fill(SyntaxStyle.LinkUrl, _pos, end);
            _pos = end;
            return true;
        }

        // # in markup: a statement (#let x = 1), a word of the language (#none), a call or a name (#image(..),
        // #title), or code or content in brackets (#(1 + 2), #{ .. }, #[ .. ])
        private void Hash()
        {
            var start = _pos;
            var next = At(start + 1);
            if (IsIdentStart(next))
            {
                var end = IdentEnd(start + 1);
                var word = line[(start + 1)..end];
                if (Keywords.Contains(word))
                {
                    Fill(SyntaxStyle.Keyword, start, end);
                    _pos = end;
                    if (Statements.Contains(word))
                    {
                        _statement = true;
                        _statementDepth = _depth;
                    }

                    return;
                }

                Fill(SyntaxStyle.Name, start, end);
                _pos = end;
                // Fields (#calc.pi), and the brackets of a call
                while (At(_pos) == '.' && IsIdentStart(At(_pos + 1)))
                {
                    var fieldEnd = IdentEnd(_pos + 1);
                    Fill(SyntaxStyle.Name, _pos, fieldEnd);
                    _pos = fieldEnd;
                }

                if (At(_pos) is '(' or '[')
                {
                    _calls.Add(_depth);
                    Push(At(_pos) == '[');
                    _pos++;
                }

                return;
            }

            if (next is '(' or '{' or '[')
            {
                styles[start] = SyntaxStyle.Keyword;
                Push(next == '[');
                _pos = start + 2;
                return;
            }

            _pos++;
        }

        // ---- Code

        private void CodeStep()
        {
            var c = line[_pos];
            // (a line of code is not the start of a line of markup)
            if (c is not (' ' or '\t')) _lineStart = false;
            switch (c)
            {
                case '/' when At(_pos + 1) == '/':
                    LineComment();
                    break;
                case '/' when At(_pos + 1) == '*':
                    StartBlockComment();
                    break;
                case '"':
                    Quoted();
                    break;
                case '$':
                    StartMath();
                    break;
                case '`':
                    Raw();
                    break;
                case '<':
                    if (!Label()) _pos++;
                    break;
                case '(' or '{':
                    Push(false);
                    _pos++;
                    break;
                case '[':
                    Push(true);
                    _pos++;
                    _lineStart = false;
                    break;
                case ')' or '}':
                    _pos++;
                    if (_depth > 0 && !TopIsContent || _overflow > 0) Pop();
                    break;
                case ';':
                    if (_statement && _depth == _statementDepth) _statement = false;
                    _pos++;
                    break;
                default:
                    if (char.IsAsciiDigit(c) && (_pos == 0 || !IsIdentPart(line[_pos - 1]))) Number();
                    else if (IsIdentStart(c)) Word();
                    else _pos++;
                    break;
            }
        }

        // "text", with escapes (\")
        private void Quoted()
        {
            var end = _pos + 1;
            while (end < line.Length && line[end] != '"')
            {
                end += line[end] == '\\' ? 2 : 1;
            }

            var stop = Math.Min(line.Length, end + 1);
            Fill(SyntaxStyle.String, _pos, stop);
            _pos = stop;
        }

        // 12, 0xff, 1.5e3, and with a unit: 2pt, 90deg, 1fr, 50%
        private void Number()
        {
            var end = _pos;
            if (line[end] == '0' && At(end + 1) is 'x' or 'o' or 'b')
            {
                end += 2;
                while (char.IsAsciiHexDigit(At(end))) end++;
            }
            else
            {
                while (char.IsAsciiDigit(At(end)) || At(end) == '.' && char.IsAsciiDigit(At(end + 1))) end++;
                if (At(end) == 'e' && (char.IsAsciiDigit(At(end + 1)) ||
                                       At(end + 1) is '-' or '+' && char.IsAsciiDigit(At(end + 2))))
                {
                    end += 2;
                    while (char.IsAsciiDigit(At(end))) end++;
                }

                if (At(end) == '%')
                {
                    end++;
                }
                else
                {
                    var unit = end;
                    while (char.IsAsciiLetterLower(At(unit))) unit++;
                    if (unit > end && Units.Contains(line[end..unit])) end = unit;
                }
            }

            Fill(SyntaxStyle.Number, _pos, end);
            _pos = end;
        }

        // A word of the language, or the name of a function that is called
        private void Word()
        {
            var end = IdentEnd(_pos);
            var word = line[_pos..end];
            if (Keywords.Contains(word))
            {
                Fill(SyntaxStyle.Keyword, _pos, end);
            }
            else if (At(end) is '(' or '[')
            {
                // (a call has its brackets right after the name: "x [" is not one)
                Fill(SyntaxStyle.Name, _pos, end);
            }

            _pos = end;
        }

        private int IdentEnd(int from)
        {
            var end = from;
            while (IsIdentPart(At(end)) || At(end) == '-' && IsIdentPart(At(end + 1)) && At(end + 1) != '-') end++;
            return end;
        }
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
