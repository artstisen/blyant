using System.Text;

namespace Blyant;

/// <summary>
/// The panes below a list with notes (the favorites of the file browser, the bookmarks of the Jump menu): the note
/// of the selected item, and a peek at it (the top of a file, what is in a directory or a folder). They follow the
/// selection and only show: nothing in them can be selected or scrolled. Each list turns them on and off with
/// ":panes" and ":peek", kept in settings of its own
/// </summary>
internal static class NotePanes
{
    // How much of a file the peek reads: the top of it
    private const int PeekBytes = 16 * 1024;

    /// <summary>
    /// The rows of text of the panes for the rows a list has (0: not shown, also when there are too few rows)
    /// </summary>
    /// <param name="shown">The panes are on</param>
    /// <param name="available">The rows of the list without the panes</param>
    /// <param name="least">The fewest rows the list can have for the panes to be shown (a list in a box has fewer)</param>
    internal static int Rows(bool shown, int available, int least = 14) =>
        shown && available >= least ? Math.Min(10, available / 3) : 0;

    /// <summary>
    /// Draw the panes: a divider at a row, with their names (and the ends of the lines of the list above), and below
    /// it the note and (when the peek is on) the peek, side by side
    /// </summary>
    /// <param name="top">The row of the divider</param>
    /// <param name="left">The first column of the panes</param>
    /// <param name="width">The columns they take</param>
    /// <param name="rows">The rows of text below the divider</param>
    /// <param name="listLines">The columns of the lines between the columns of the list, which end at the divider</param>
    /// <param name="note">The note of the selected item, or null if nothing is selected</param>
    /// <param name="peek">The lines of the peek, or null when the peek is off</param>
    internal static void Draw(int top, int left, int width, int rows, IReadOnlyList<int> listLines, string? note,
        IReadOnlyList<string>? peek)
    {
        var line = left + width / 2;
        Tools.SetCursorEx(top, left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', width));
        // Where the lines of the list end, and the line between the panes starts
        FileExplorer.DrawColumnEnds(top, peek != null ? listLines.Append(line).ToArray() : listLines);
        FileExplorer.DrawColumnLabels(top, peek != null
            ? new[] { (left, Lang.T("Note")), (line, Lang.T("Peek")) }
            : new[] { (left, Lang.T("Note")) }, Array.Empty<int>());

        var noteWidth = (peek != null ? line - left : width) - 3;
        var peekWidth = left + width - line - 3;
        var notes = note == null ? new List<string>() : NoteLines(note, noteWidth, rows);
        // (the last column of the screen is not written, so the terminal does not scroll)
        var clear = Math.Min(width, Init.LineWidth - 1 - left);
        for (var row = 0; row < rows; row++)
        {
            var y = top + 1 + row;
            Tools.SetCursorEx(y, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', Math.Max(0, clear)));
            if (row < notes.Count)
            {
                ConsoleColors.SetColors(note!.Length == 0
                    ? ConsoleColors.ColorPreset.Divider
                    : ConsoleColors.ColorPreset.Normal);
                Tools.SetCursorEx(y, left + 1);
                Console.Write(Tools.TruncateString(notes[row], noteWidth, false));
            }

            if (peek == null) continue;
            FileExplorer.DrawColumnLine(y, line);
            if (row >= peek.Count) continue;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(y, line + 2);
            Console.Write(Tools.TruncateString(peek[row], peekWidth, false));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The lines of a note in a pane: wrapped to its width, at the most a number of rows (the last "..." if there is
    /// more), or "(no note)"
    /// </summary>
    internal static List<string> NoteLines(string note, int width, int rows)
    {
        if (note.Length == 0) return new List<string> { Lang.T("(no note)") };
        var lines = new List<string>();
        foreach (var line in note.Split('\n'))
        {
            lines.AddRange(ChoiceBox.Wrap(line.Replace("\t", "    "), Math.Max(1, width)));
            if (lines.Count > rows) break;
        }

        if (lines.Count <= rows) return lines;
        lines = lines.Take(rows).ToList();
        lines[^1] = "...";
        return lines;
    }

    /// <summary>
    /// What the peek shows of a file or directory: the top of the file, or what is in the directory
    /// </summary>
    internal static List<string> PeekPlace(string path, int rows)
    {
        if (Directory.Exists(path)) return Projects.Contents(path, rows).Split('\n').ToList();
        if (!File.Exists(path)) return new List<string> { DialogText.FavoriteMissing };
        return PeekLines(path, rows);
    }

    /// <summary>
    /// What the peek shows of a folder of a list: the names of what is in it (a subfolder in brackets, the items of
    /// a subfolder two blank spaces in), the last line telling how many more there are
    /// </summary>
    internal static List<string> PeekFolder(IReadOnlyList<string> names, int rows)
    {
        if (names.Count == 0) return new List<string> { Lang.T("(empty)") };
        var lines = names.Take(rows).ToList();
        if (names.Count > rows) lines[^1] = Lang.F("... and {0} more", names.Count - rows + 1);
        return lines;
    }

    /// <summary>
    /// The top of a file for the peek: its first lines (tabs as blank spaces). Only the start of the file is read,
    /// and nothing is written. A file that is not text is told. A file of size 0 is not opened: on Linux and macOS a
    /// named pipe or a device has size 0, and reading one could wait for ever
    /// </summary>
    internal static List<string> PeekLines(string path, int rows)
    {
        try
        {
            if (new FileInfo(path).Length == 0) return new List<string> { Lang.T("(empty)") };
            byte[] head;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                head = new byte[(int)Math.Min(PeekBytes, stream.Length)];
                var read = 0;
                while (read < head.Length)
                {
                    var count = stream.Read(head, read, head.Length - read);
                    if (count == 0) break;
                    read += count;
                }

                Array.Resize(ref head, read);
            }

            if (head.Length == 0) return new List<string> { Lang.T("(empty)") };
            if (Array.IndexOf(head, (byte)0) >= 0) return new List<string> { Lang.T("(not a text file)") };
            var text = new UTF8Encoding(false).GetString(head).TrimStart('﻿');
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Take(rows)
                .Select(line => line.Replace("\t", "    ")).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string> { e.Message };
        }
    }

    /// <summary>
    /// The file a note is written in for the editor, in the data directory: one for each item (by a key that tells
    /// it from the others: its path, or the folder it is in and its name), so a recovery copy of a note is only
    /// offered for the note it was made of
    /// </summary>
    /// <param name="prefix">The start of the name, for the list ("favorite-note", "bookmark-note")</param>
    /// <param name="key">What tells the item from the others</param>
    internal static string NoteFileName(string prefix, string key)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return prefix + "-" + Convert.ToHexString(hash, 0, 6).ToLowerInvariant() + ".md";
    }
}
