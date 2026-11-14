namespace Blyant;

/// <summary>
/// The line of the shell in a terminal (setting "shellcompletion"): a command typed key by key, with Tab completing
/// it (ShellCompletion), Up and Down for the commands typed before (CommandHistory), and the keys of the command lines
/// of the full screen: Left, Right, Home, End, Backspace, Delete, Ctrl+U (the line before the cursor), Ctrl+W (the
/// word before it) and Esc (the whole line). The line is written on one row: a line wider than the window is scrolled
/// sideways, as on the command line of the full screen. Ctrl+C drops the line; Ctrl+D (Ctrl+Z on Windows) on an empty
/// line ends the input. Only carriage returns and spaces move about on the row: it works the same in every terminal.
/// The keys and the size of the window are given (a test gives its own)
/// </summary>
internal sealed class ShellLineReader
{
    private readonly Func<ConsoleKeyInfo> _readKey;
    private readonly TextWriter _output;
    private readonly Func<int> _width;
    private readonly Func<string, bool, string> _complete;

    private string _text = string.Empty;
    private int _cursor; // In _text (a char index)
    private int _scroll; // The first char shown, for a line wider than the window
    private int _drawn; // The columns written after the prompt the last time (written over with blanks)

    /// <param name="readKey">The next key</param>
    /// <param name="output">Where the line is written</param>
    /// <param name="width">The width of the window</param>
    /// <param name="complete">The line after Tab (true) or Shift+Tab (false)</param>
    internal ShellLineReader(Func<ConsoleKeyInfo> readKey, TextWriter output, Func<int> width,
        Func<string, bool, string> complete)
    {
        _readKey = readKey;
        _output = output;
        _width = width;
        _complete = complete;
    }

    /// <summary>
    /// Read a line after a prompt
    /// </summary>
    /// <returns>The line (empty after Ctrl+C), or null at the end of the input (Ctrl+D or Ctrl+Z on an empty line)</returns>
    internal string? Read(string prompt)
    {
        _text = string.Empty;
        _cursor = 0;
        _scroll = 0;
        _drawn = 0;
        var recall = new CommandRecall(CommandContext.Shell);
        Draw(prompt);
        while (true)
        {
            var key = _readKey();
            var control = (key.Modifiers & ConsoleModifiers.Control) != 0;
            // (a terminal can give Enter, Tab and Backspace as their characters only)
            if (key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n')
            {
                _output.WriteLine();
                return _text;
            }

            if ((control && key.Key == ConsoleKey.C) || key.KeyChar == '\u0003')
            {
                // Ctrl+C: the line is dropped
                _output.WriteLine();
                return string.Empty;
            }

            if ((control && key.Key is ConsoleKey.D or ConsoleKey.Z) || key.KeyChar is '\u0004' or '\u001a')
            {
                // Ctrl+D, Ctrl+Z: the end of the input, on an empty line
                if (_text.Length > 0) continue;
                _output.WriteLine();
                return null;
            }

            var pressed = key.KeyChar switch
            {
                '\t' => ConsoleKey.Tab,
                '\b' or '\u007f' => ConsoleKey.Backspace,
                _ => key.Key
            };
            switch (pressed)
            {
                case ConsoleKey.Tab:
                    _text = _complete(_text, (key.Modifiers & ConsoleModifiers.Shift) == 0);
                    _cursor = _text.Length;
                    recall.Reset();
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.DownArrow:
                    var recalled = key.Key == ConsoleKey.UpArrow ? recall.Previous(_text) : recall.Next();
                    if (recalled == null) break;
                    _text = recalled;
                    _cursor = _text.Length;
                    break;
                case ConsoleKey.LeftArrow:
                    if (_cursor > 0) _cursor -= TextMetrics.PreviousElementLength(_text, _cursor);
                    break;
                case ConsoleKey.RightArrow:
                    if (_cursor < _text.Length) _cursor += TextMetrics.ElementLength(_text, _cursor);
                    break;
                case ConsoleKey.Home:
                    _cursor = 0;
                    break;
                case ConsoleKey.End:
                    _cursor = _text.Length;
                    break;
                case ConsoleKey.Escape:
                    _text = string.Empty;
                    _cursor = 0;
                    recall.Reset();
                    break;
                case ConsoleKey.Backspace:
                    if (_cursor == 0) break;
                    var back = TextMetrics.PreviousElementLength(_text, _cursor);
                    _text = _text.Remove(_cursor - back, back);
                    _cursor -= back;
                    recall.Reset();
                    break;
                case ConsoleKey.Delete:
                    if (_cursor >= _text.Length) break;
                    _text = _text.Remove(_cursor, TextMetrics.ElementLength(_text, _cursor));
                    recall.Reset();
                    break;
                case ConsoleKey.U when control:
                    // The line before the cursor
                    _text = _text[_cursor..];
                    _cursor = 0;
                    recall.Reset();
                    break;
                case ConsoleKey.W when control:
                    // The word before the cursor (and the blanks after it)
                    var from = _cursor;
                    while (from > 0 && char.IsWhiteSpace(_text[from - 1])) from--;
                    while (from > 0 && !char.IsWhiteSpace(_text[from - 1])) from--;
                    _text = _text.Remove(from, _cursor - from);
                    _cursor = from;
                    recall.Reset();
                    break;
                default:
                    if (key.KeyChar < ' ' || char.IsControl(key.KeyChar) || control) break;
                    _text = _text.Insert(_cursor, key.KeyChar.ToString());
                    _cursor++;
                    recall.Reset();
                    break;
            }

            Draw(prompt);
        }
    }

    /// <summary>
    /// The text written for the line now (without the prompt), and where the cursor is in it: for a test
    /// </summary>
    internal (string Text, int Cursor) Shown => (_text, _cursor);

    // Write the row: the prompt (its end, in a narrow window) and the part of the line that fits, blanks over what was
    // written before, and the cursor put in its place by writing the row again up to it
    private void Draw(string prompt)
    {
        var width = Math.Max(20, _width()) - 1;
        // (a prompt that leaves less than 20 columns for the line: its end)
        if (TextMetrics.WidthOf(prompt) > width - 20) prompt = TextMetrics.KeepEnd(prompt, Math.Max(10, width - 20));
        var room = Math.Max(5, width - TextMetrics.WidthOf(prompt));
        // The part of the line shown: the cursor in it (the line scrolled sideways)
        if (_cursor < _scroll) _scroll = _cursor;
        while (TextMetrics.WidthOf(_text[_scroll.._cursor]) > room - 1)
        {
            _scroll += TextMetrics.ElementLength(_text, _scroll);
        }

        var end = _scroll;
        while (end < _text.Length && TextMetrics.WidthOf(_text[_scroll..(end + TextMetrics.ElementLength(_text, end))]) <= room - 1)
        {
            end += TextMetrics.ElementLength(_text, end);
        }

        var shown = TextMetrics.Printable(_text[_scroll..end]);
        var columns = TextMetrics.WidthOf(shown);
        _output.Write("\r" + prompt + shown + new string(' ', Math.Max(0, _drawn - columns)));
        _output.Write("\r" + prompt + TextMetrics.Printable(_text[_scroll.._cursor]));
        _output.Flush();
        _drawn = columns;
    }
}
