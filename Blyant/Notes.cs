using System.Text;

namespace Blyant;

/// <summary>
/// The notes of the favorites (favorites.txt) and of the bookmarks (bookmarks.txt): each is kept on one line after
/// its favorite, bookmark or folder, after a "#", with its line breaks written as \n, a backslash as \\ and a tab as
/// \t. They are never kept next to the files, which may be in a repository.
///
/// This class does not touch the console.
/// </summary>
internal static class Notes
{
    /// <summary>
    /// The mark a line of a note starts with (after the blank spaces of its level)
    /// </summary>
    internal const char Mark = '#';

    /// <summary>
    /// A note on one line: the line breaks as \n, a backslash as \\ and a tab as \t
    /// </summary>
    internal static string Escape(string note) =>
        note.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\\", "\\\\").Replace("\n", "\\n")
            .Replace("\t", "\\t");

    /// <summary>
    /// A note as it was written on its line
    /// </summary>
    internal static string Unescape(string written)
    {
        var note = new StringBuilder();
        for (var i = 0; i < written.Length; i++)
        {
            if (written[i] != '\\' || i + 1 == written.Length)
            {
                note.Append(written[i]);
                continue;
            }

            i++;
            note.Append(written[i] switch
            {
                'n' => '\n',
                't' => '\t',
                _ => written[i]
            });
        }

        return note.ToString();
    }

    /// <summary>
    /// A note as it is kept: line breaks as \n, blank lines at its end left out, and nothing if it is blank
    /// </summary>
    internal static string Clean(string note)
    {
        note = note.Replace("\r\n", "\n").Replace('\r', '\n');
        return note.Trim().Length == 0 ? string.Empty : note.TrimEnd();
    }

    /// <summary>
    /// The line of a note, after the line of its item (at the same level)
    /// </summary>
    internal static string Line(int level, string note) => new string(' ', level) + Mark + Escape(note) + "\n";

    /// <summary>
    /// A line that is a note (the blank spaces in front taken away), and the note on it
    /// </summary>
    internal static bool IsNote(string trimmedLine, out string note)
    {
        note = trimmedLine.StartsWith(Mark) ? Unescape(trimmedLine[1..]) : string.Empty;
        return trimmedLine.StartsWith(Mark);
    }
}
