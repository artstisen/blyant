namespace Blyant;

/// <summary>
/// The latest files opened, to switch to one at once: a small box on top of the page with the newest of the file
/// history (history.txt), without the file being edited, so the second row of the history comes first. Ctrl+K (in the
/// editor in Normal mode), and ":recent", in the editor, the file browser and on the start page (also its choice
/// Recent files). j k (Up Down) select, Enter opens the
/// selected file and 1 to 9 the file of that number, Esc closes. The caller opens the file picked (as a jump, so the
/// editor asks about changes that are not saved first)
/// </summary>
internal static class RecentBox
{
    /// <summary>
    /// The most files shown
    /// </summary>
    internal const int MaxFiles = 9;

    /// <summary>
    /// True for the key that opens the box: Ctrl+K
    /// </summary>
    internal static bool IsKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.K && (key.Modifiers & ConsoleModifiers.Control) != 0 &&
        (key.Modifiers & ConsoleModifiers.Alt) == 0;

    /// <summary>
    /// Show the box, and open the file picked as a jump (the page is left; from the editor, use Pick and ask first)
    /// </summary>
    internal static void Open()
    {
        if (Pick(null) is { } recent) Jump.ToPlace(new JumpTarget(JumpKind.File, recent));
    }

    /// <summary>
    /// The files of the box: the newest first, without the file being edited (null: none), at most MaxFiles
    /// </summary>
    internal static List<string> Files(IEnumerable<string> history, string? current) =>
        history.Where(path => current == null || !FileOperations.SamePath(path, current)).Take(MaxFiles).ToList();

    /// <summary>
    /// Show the box and let the user pick a file
    /// </summary>
    /// <param name="current">The file being edited, which is left out (null: none)</param>
    /// <returns>The path of the file picked, or null</returns>
    internal static string? Pick(string? current)
    {
        FileHistory.Load();
        var files = Files(FileHistory.Items.Select(entry => entry.Path), current);
        ChoiceBox.Forget();
        if (files.Count == 0)
        {
            ChoiceBox.Message(Lang.T("Recent files"), Lang.T("No other file has been opened yet"), string.Empty);
            return null;
        }

        var caption = Lang.T("Recent files");
        var hints = Lang.T("j/k Select | Enter or 1-9 Open | Esc Close");
        var names = files.Select(Path.GetFileName).Select(name => name ?? string.Empty).ToList();
        var folders = files.Select(path => Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty)).ToList();
        var nameWidth = names.Max(TextMetrics.WidthOf);
        // "1 -> name  folder", and the keys on the last row
        var inner = Math.Min(Init.LineWidth - 6, Math.Max(Math.Max(TextMetrics.WidthOf(caption) + 6,
                TextMetrics.WidthOf(hints) + 2),
            nameWidth + folders.Max(TextMetrics.WidthOf) + 10));
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        const int top = 3; // The tab of the title is on the row above
        var selected = 0;
        var message = string.Empty;
        Tools.HideCursor();
        while (true)
        {
            ChoiceBox.DrawTitleTab(top - 1, left, inner, caption);
            for (var row = 0; row < files.Count; row++)
            {
                Tools.SetCursorEx(top + 1 + row, left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
                Console.Write("| ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.LineNumber);
                Console.Write((row + 1) + " ");
                ConsoleColors.SetColors(row == selected
                    ? ConsoleColors.ColorPreset.SelectorInverted
                    : ConsoleColors.ColorPreset.Normal);
                Console.Write(row == selected ? "->" : "  ");
                var exists = File.Exists(files[row]);
                // A file that is gone is dimmed, and cannot be opened
                ConsoleColors.SetColors(exists ? ConsoleColors.ColorPreset.Normal : ConsoleColors.ColorPreset.Divider);
                var name = names[row] + (exists ? string.Empty : " " + DialogText.FavoriteMissing);
                var room = inner - 6;
                var shown = Tools.TruncateString(" " + name, room, true);
                Console.Write(shown);
                var used = TextMetrics.WidthOf(shown);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                var folder = Tools.TruncateString(new string(' ', Math.Max(2, nameWidth + 3 - used)) + folders[row],
                    Math.Max(0, room - used), true);
                Console.Write(folder);
                Console.Write(new string(' ', Math.Max(0, room - used - TextMetrics.WidthOf(folder))));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
                Console.Write(" |");
            }

            // The keys (or what the last key did)
            Tools.SetCursorEx(top + 1 + files.Count, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write("| ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var bottom = Tools.TruncateString(message.Length > 0 ? message : hints, inner - 2, true);
            Console.Write(bottom + new string(' ', Math.Max(0, inner - 2 - TextMetrics.WidthOf(bottom))));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write(" |");
            Tools.SetCursorEx(top + 2 + files.Count, left);
            Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

            var key = Console.ReadKey(true);
            message = string.Empty;
            var picked = -1;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return null;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L) picked = selected;
            else if (key.KeyChar is >= '1' and <= '9' && key.KeyChar - '1' < files.Count) picked = key.KeyChar - '1';
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow) selected = (selected + 1) % files.Count;
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow) selected = (selected + files.Count - 1) % files.Count;
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End) selected = files.Count - 1;
            else if (key.KeyChar == 'g' || key.Key == ConsoleKey.Home) selected = 0;
            if (picked < 0) continue;
            if (File.Exists(files[picked])) return files[picked];
            selected = picked;
            message = DialogText.FileNotFound;
        }
    }
}
