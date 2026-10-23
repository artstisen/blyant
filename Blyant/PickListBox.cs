namespace Blyant;

/// <summary>
/// A box on top of the page with a list of texts to pick one from (as the strings of the string memory,
/// ":reglist"): j/k (and gg/G, PgUp/PgDn) select, "/" filters the rows, Enter picks and Esc closes. The first
/// lines of the selected text are shown below the list
/// </summary>
internal static class PickListBox
{
    private const int PreviewRows = 5;

    // Where the parts of the box are
    private static int _top;
    private static int _left;
    private static int _inner;
    private static int _listTop;
    private static int _listRows;
    private static int _previewTop;
    private static int _previewRows;
    private static Extras? _extras;

    /// <summary>
    /// What a list can have besides its rows (as the list of projects): a color for each row, a text to the right
    /// of the preview, and keys of its own
    /// </summary>
    internal sealed class Extras
    {
        /// <summary>
        /// The color of the text of a row (null: the normal color)
        /// </summary>
        internal Func<int, ConsoleColor?>? RowColor { get; init; }

        /// <summary>
        /// The text to the right of the preview, wrapped (the preview then takes the left half)
        /// </summary>
        internal Func<int, string>? Side { get; init; }

        /// <summary>
        /// A key pressed on a row (-1 when the list is empty): whether it was used (the box is drawn again: the key
        /// may have shown another box), closes the list, or the list must be made again (Show returns Reload)
        /// </summary>
        internal Func<ConsoleKeyInfo, int, KeyUse>? Key { get; init; }

        /// <summary>
        /// The tabs of the list, shown at the right of the filter line (none: no tabs)
        /// </summary>
        internal IReadOnlyList<string>? Tabs { get; init; }

        /// <summary>
        /// The tab shown
        /// </summary>
        internal int ActiveTab { get; init; }

        /// <summary>
        /// Shown in the list when it has no rows (what to do to get some)
        /// </summary>
        internal string? EmptyText { get; init; }
    }

    /// <summary>
    /// What a key of a list (Extras.Key) did
    /// </summary>
    internal enum KeyUse
    {
        None, // Not a key of the list: the box handles it
        Used,
        Close, // The list is closed without picking (":q")
        Reload // The rows have changed, or another tab is shown: Show returns Reload, to be called again
    }

    /// <summary>
    /// What Show returns when a key of the list asked for it to be made again (KeyUse.Reload)
    /// </summary>
    internal const int Reload = -2;

    // A message shown in place of the filter until the next key (ShowMessage)
    private static string _message = string.Empty;

    /// <summary>
    /// Show a message in the box (what a key of the list did: a path copied ...), until the next key
    /// </summary>
    internal static void ShowMessage(string message) => _message = message;

    /// <summary>
    /// Show the list and let the user pick a row
    /// </summary>
    /// <param name="caption">The title of the box</param>
    /// <param name="rows">The rows (one line each), which the filter looks in</param>
    /// <param name="preview">The text shown below the list for a row</param>
    /// <param name="selected">The row selected from the start</param>
    /// <param name="hints">The keys, on the bottom line</param>
    /// <param name="extras">Colors, a text to the right of the preview, and keys of the list (null: none)</param>
    /// <returns>The number of the row picked, or -1</returns>
    internal static int Show(string caption, IReadOnlyList<string> rows, Func<int, string> preview, int selected,
        string hints, Extras? extras = null)
    {
        // In the color of text in frames and boxes
        using var frameText = ConsoleColors.FrameTextScope();
        _extras = extras;
        _message = string.Empty;

        var filter = string.Empty;
        var filtering = false;
        var view = Enumerable.Range(0, rows.Count).ToList();
        selected = Math.Max(0, view.IndexOf(selected));
        var scroll = 0;
        var shownScroll = -1;
        var shownSelected = -1;
        DrawBox(caption);
        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, view.Count - 1));
            if (selected < scroll) scroll = selected;
            if (selected >= scroll + _listRows) scroll = selected - _listRows + 1;
            scroll = Math.Clamp(scroll, 0, Math.Max(0, view.Count - _listRows));
            if (scroll != shownScroll)
            {
                DrawList(rows, view, scroll, filter);
                shownScroll = scroll;
                shownSelected = -1;
            }

            if (selected != shownSelected)
            {
                DrawPointers(view, scroll, selected);
                DrawPreview(view.Count > 0 ? preview(view[selected]) : string.Empty,
                    view.Count > 0 && extras?.Side is { } side ? side(view[selected]) : string.Empty);
                shownSelected = selected;
            }

            DrawFilter(filter, filtering);
            ChoiceBox.WriteHints(filtering ? DialogText.SnippetFilterKeys : hints);
            if (filtering)
            {
                Tools.SetCursorEx(_top + 2, _left + 3 + TextMetrics.WidthOf(FilterText(filter)));
                Tools.ShowCursor();
            }
            else
            {
                Tools.SetCursorEx(Init.LineCount - 1, 0);
            }

            var key = Console.ReadKey(true);
            Tools.HideCursor();
            _message = string.Empty;
            if (filtering)
            {
                if (key.Key is ConsoleKey.Tab or ConsoleKey.DownArrow or ConsoleKey.UpArrow)
                {
                    // Between the rows that match while typing (Shift+Tab, Up: back), going round
                    var back = key.Key == ConsoleKey.UpArrow ||
                               (key.Key == ConsoleKey.Tab && (key.Modifiers & ConsoleModifiers.Shift) != 0);
                    selected = PickListBox.NextMatch(selected, view.Count, back);
                    continue;
                }

                var before = filter;
                if (key.Key == ConsoleKey.Enter)
                {
                    filtering = false;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    filtering = false;
                    filter = string.Empty;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (filter.Length > 0)
                    {
                        filter = filter[..^TextMetrics.PreviousElementLength(filter, filter.Length)];
                    }
                }
                else if (!char.IsControl(key.KeyChar) && filter.Length < 40)
                {
                    filter += key.KeyChar;
                }

                if (filter != before)
                {
                    view = Filter(rows, filter);
                    selected = 0;
                    shownScroll = -1;
                }

                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            var c = key.KeyChar;
            if (key.Key == ConsoleKey.Escape && filter.Length > 0)
            {
                // Esc takes the filter away first
                var current = view.Count > 0 ? view[selected] : -1;
                filter = string.Empty;
                view = Filter(rows, filter);
                selected = Math.Max(0, view.IndexOf(current));
                shownScroll = -1;
                continue;
            }

            if (key.Key == ConsoleKey.Escape || c is 'q' or 'Q' || navigation == ConsoleKey.LeftArrow) return -1;
            if ((key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow) && view.Count > 0)
            {
                return view[selected];
            }

            var use = extras?.Key is { } onKey ? onKey(key, view.Count > 0 ? view[selected] : -1) : KeyUse.None;
            if (use == KeyUse.Close) return -1;
            if (use == KeyUse.Reload) return Reload;
            if (use == KeyUse.Used)
            {
                // Again: the key may have opened another list (the editor of a note, and in it ":reglist")
                _extras = extras;
                DrawBox(caption);
                shownScroll = -1;
                continue;
            }

            if (navigation == ConsoleKey.DownArrow) selected = Math.Min(selected + 1, Math.Max(0, view.Count - 1));
            if (navigation == ConsoleKey.UpArrow) selected = Math.Max(selected - 1, 0);
            if (key.Key == ConsoleKey.PageDown) selected = Math.Min(selected + _listRows, Math.Max(0, view.Count - 1));
            if (key.Key == ConsoleKey.PageUp) selected = Math.Max(selected - _listRows, 0);
            if (c == 'G') selected = Math.Max(0, view.Count - 1);
            if (c == 'g' && Console.ReadKey(true).KeyChar == 'g') selected = 0;
            if (c == '/') filtering = true;
        }
    }

    /// <summary>
    /// The next (or the one before) of a number of rows, going round from the last to the first
    /// </summary>
    internal static int NextMatch(int selected, int count, bool back)
    {
        if (count <= 0) return 0;
        return (selected + (back ? count - 1 : 1)) % count;
    }

    /// <summary>
    /// The rows that have the filter in them (upper and lower case alike)
    /// </summary>
    internal static List<int> Filter(IReadOnlyList<string> rows, string filter)
    {
        return Enumerable.Range(0, rows.Count)
            .Where(i => filter.Length == 0 || rows[i].Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #region Drawing

    private static void DrawBox(string caption)
    {
        ChoiceBox.Forget();
        var inner = Init.LineWidth - 6;
        // (and the tab of the title above it)
        var height = Math.Max(8, Init.LineCount - 5);
        _previewRows = Math.Clamp(height - 13, 1, PreviewRows);
        // A blank row, the filter, a blank row, the list, a blank row, the preview and a blank row
        _listRows = height - 5 - _previewRows;
        var (top, left, width, _) = ChoiceBox.DrawFrame(caption, inner, height);
        _top = top;
        _left = left;
        _inner = width;
        _listTop = top + 4;
        _previewTop = _listTop + _listRows + 1;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_previewTop - 1, left + 3);
        Console.Write(new string('-', width - 4));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static string FilterText(string filter) => Lang.F("Filter: {0}", filter);

    private static void DrawFilter(string filter, bool filtering)
    {
        var text = filtering || filter.Length > 0 ? FilterText(filter) : string.Empty;
        // The tabs at the right of the line, the tab shown highlighted ("All | Added | Projects folder")
        var tabs = _extras?.Tabs is { Count: > 0 } names ? names : null;
        var tabsWidth = tabs == null ? 0 : tabs.Sum(name => TextMetrics.WidthOf(name) + 2) + tabs.Count - 1;
        var room = Math.Max(10, _inner - 4 - (tabsWidth > 0 ? tabsWidth + 2 : 0));
        Tools.SetCursorEx(_top + 2, _left + 3);
        if (_message.Length > 0 && !filtering)
        {
            // A message: highlighted, with a blank space on each side in the highlight (as in the Jump menu)
            var message = " " + Tools.TruncateString(_message, room - 2, true) + " ";
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(message);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', Math.Max(0, room - TextMetrics.WidthOf(message))));
        }
        else
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var shown = Tools.TruncateString(text, room, true);
            Console.Write(shown + new string(' ', Math.Max(0, room - TextMetrics.WidthOf(shown))));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        if (tabs == null || tabsWidth > _inner - 14) return;
        Tools.SetCursorEx(_top + 2, _left + _inner - 1 - tabsWidth);
        for (var i = 0; i < tabs.Count; i++)
        {
            if (i > 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write("|");
            }

            ConsoleColors.SetColors(i == _extras!.ActiveTab
                ? ConsoleColors.ColorPreset.SelectorInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + tabs[i] + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawList(IReadOnlyList<string> rows, List<int> view, int scroll, string filter)
    {
        for (var row = 0; row < _listRows; row++)
        {
            Tools.SetCursorEx(_listTop + row, _left + 1);
            var i = scroll + row;
            var text = i < view.Count
                ? TextMetrics.Printable(Tools.TruncateString(rows[view[i]], _inner - 8, true))
                : i == 0 && filter.Length > 0
                    ? DialogText.PickListNoMatch
                    : i == 0 && rows.Count == 0 && _extras?.EmptyText is { } empty
                        ? Tools.TruncateString(empty, _inner - 8, true)
                        : string.Empty;
            Console.Write("     ");
            if (i < view.Count && _extras?.RowColor?.Invoke(view[i]) is { } color) ConsoleColors.SetTextColor(color);
            Console.Write(text);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', Math.Max(0, _inner - 5 - TextMetrics.WidthOf(text))));
        }

        // More above or below
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_listTop, _left + _inner - 2);
        Console.Write(scroll > 0 ? "^" : " ");
        Tools.SetCursorEx(_listTop + _listRows - 1, _left + _inner - 2);
        Console.Write(scroll + _listRows < view.Count ? "v" : " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawPointers(List<int> view, int scroll, int selected)
    {
        for (var row = 0; row < _listRows && scroll + row < view.Count; row++)
        {
            Tools.SetCursorEx(_listTop + row, _left + 2);
            var isSelected = scroll + row == selected;
            ConsoleColors.SetColors(isSelected
                ? ConsoleColors.ColorPreset.SelectorInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(isSelected ? "->" : "  ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The preview takes the width of the box, or the left half when there is a text to the right of it (wrapped)
    private static void DrawPreview(string text, string side)
    {
        var lines = text.Length == 0 ? Array.Empty<string>() : text.Split('\n');
        var hasSide = _extras?.Side != null;
        var width = hasSide ? (_inner - 4) / 2 - 1 : _inner - 4;
        // "| " between the two, and a blank column before the border
        var sideWidth = _inner - 2 - width - 2 - 2;
        var sideLines = side.Length == 0 ? new List<string>() : ChoiceBox.Wrap(side, sideWidth);
        for (var row = 0; row < _previewRows; row++)
        {
            Tools.SetCursorEx(_previewTop + row, _left + 1);
            var line = row < lines.Length
                ? "  " + TextMetrics.Printable(Tools.TruncateString(lines[row].Replace("\t", "    "), width, true))
                : string.Empty;
            if (row == _previewRows - 1 && lines.Length > _previewRows) line = "  ...";
            var used = hasSide ? width + 2 : _inner;
            Console.Write(line + new string(' ', Math.Max(0, used - TextMetrics.WidthOf(line))));
            if (!hasSide) continue;

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write("| ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var part = row < sideLines.Count ? TextMetrics.Printable(sideLines[row]) : string.Empty;
            // The last row ends with "..." when there is more
            if (row == _previewRows - 1 && sideLines.Count > _previewRows)
            {
                part = Tools.TruncateString(part, Math.Max(0, sideWidth - 3), false).TrimEnd() + "...";
            }

            Console.Write(part + new string(' ', Math.Max(0, _inner - used - 2 - TextMetrics.WidthOf(part))));
        }
    }

    #endregion
}
