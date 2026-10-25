using System.Text;

namespace Blyant;

// The panes below the list of favorites: the note of the selected favorite or folder, and a peek at it (the top of
// the file, what is in the directory, or what is in the folder). They follow the selection, and only show: nothing
// in them can be selected or scrolled. ":panes" turns them off and on, ":peek" the peek (the settings
// "favoritepanes" and "favoritepeek"). The notes are kept in the list of favorites (never next to the files, which
// may be in a repository): N writes the note in the editor.
internal static partial class FileExplorer
{
    /// <summary>
    /// The panes below the list of favorites are shown (setting "favoritepanes")
    /// </summary>
    internal static bool FavoritePanes { get; set; } = true;

    /// <summary>
    /// The peek is shown in the panes below the list of favorites (setting "favoritepeek")
    /// </summary>
    internal static bool FavoritePeek { get; set; } = true;

    // How much of a file the peek reads: the top of it
    private const int FavoritePeekBytes = 16 * 1024;

    // The start of the name of the file of Blyant a note is written in, in the editor (in the data directory,
    // removed after)
    private const string FavoriteNoteFile = "favorite-note"; // English: the name of a file

    // The rows below the title, for the list and the panes
    private static int FavoriteRows => Init.LineCount - ListTop - 2;

    // The rows of text of the panes (0: not shown, also when the window is too low for them)
    private static int FavoritePaneRows =>
        FavoritePanes && FavoriteRows >= 14 ? Math.Min(10, FavoriteRows / 3) : 0;

    private static int FavoritePageSize => FavoriteRows - (FavoritePaneRows > 0 ? FavoritePaneRows + 1 : 0);

    // The column of the line between the note and the peek
    private static int FavoritePaneLine => Init.LineWidth / 2;

    // Where lines between columns meet the divider above the bottom line: the lines of the list, or of the panes
    private static int[] FavoriteBottomLines => FavoritePaneRows == 0 ? _favoriteLines
        : FavoritePeek ? new[] { FavoritePaneLine } : Array.Empty<int>();

    // ":panes" and ":peek": the panes, or the peek in them, on or off (kept in the settings)
    private static void ToggleFavoritePanes(bool peek)
    {
        if (peek)
        {
            FavoritePeek = !FavoritePeek;
            _favoriteMessage = FavoritePeek ? Lang.T("Peek on") : Lang.T("Peek off");
            if (FavoritePeek && !FavoritePanes) FavoritePanes = true;
        }
        else
        {
            FavoritePanes = !FavoritePanes;
            _favoriteMessage = FavoritePanes ? Lang.T("Note and peek shown") : Lang.T("Note and peek hidden");
        }

        if (FavoritePanes && FavoritePaneRows == 0) _favoriteMessage = Lang.T("The window is too low for the panes");
        FileIo.SaveSettings();
    }

    // The panes, below the list: a divider with their names, and the note and the peek of the selected item
    private static void DrawFavoritePanes()
    {
        var rows = FavoritePaneRows;
        if (rows == 0) return;
        var top = ListTop + FavoritePageSize;
        var selected = _favorites.Count > 0 ? _favorites[_favoriteIndex] : -1;
        var favorite = selected >= 0 ? FileFavorites.Items[selected] : null;
        var peek = FavoritePeek;
        var line = FavoritePaneLine;
        Tools.SetCursorEx(top, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        // Where the lines of the list end, and the line between the panes starts
        DrawColumnEnds(top, peek ? _favoriteLines.Append(line).ToArray() : _favoriteLines);
        DrawColumnLabels(top, peek
            ? new[] { (0, Lang.T("Note")), (line, Lang.T("Peek")) }
            : new[] { (0, Lang.T("Note")) }, Array.Empty<int>());

        var noteWidth = (peek ? line : Init.LineWidth) - 3;
        var peekWidth = Init.LineWidth - line - 3;
        var notes = favorite == null ? new List<string>() : NoteLines(favorite.Note, noteWidth, rows);
        var peeks = favorite == null || !peek ? new List<string>() : PeekOf(selected, rows);
        for (var row = 0; row < rows; row++)
        {
            var y = top + 1 + row;
            Tools.SetCursorEx(y, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', Init.LineWidth - 1));
            if (row < notes.Count)
            {
                ConsoleColors.SetColors(favorite!.Note.Length == 0
                    ? ConsoleColors.ColorPreset.Divider
                    : ConsoleColors.ColorPreset.Normal);
                Tools.SetCursorEx(y, 1);
                Console.Write(Tools.TruncateString(notes[row], noteWidth, false));
            }

            if (!peek) continue;
            DrawColumnLine(y, line);
            if (row >= peeks.Count) continue;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(y, line + 2);
            Console.Write(Tools.TruncateString(peeks[row], peekWidth, false));
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

    // What the peek shows of an item of the list: the top of a file, what is in a directory, or what is in a folder
    private static List<string> PeekOf(int number, int rows)
    {
        var favorite = FileFavorites.Items[number];
        if (favorite.IsFolder)
        {
            var items = FileFavorites.InFolder(number);
            if (items.Count == 0) return new List<string> { Lang.T("(empty)") };
            var lines = items.Take(rows).Select(item =>
                new string(' ', 2 * (item.Level - favorite.Level - 1)) +
                (item.IsFolder ? "[" + item.Name + "]"
                    : FavoriteName(item.Path) + (Directory.Exists(item.Path) ? "/" : ""))).ToList();
            if (items.Count > rows) lines[^1] = Lang.F("... and {0} more", items.Count - rows + 1);
            return lines;
        }

        if (Directory.Exists(favorite.Path)) return Projects.Contents(favorite.Path, rows).Split('\n').ToList();
        if (!File.Exists(favorite.Path)) return new List<string> { DialogText.FavoriteMissing };
        return PeekLines(favorite.Path, rows);
    }

    /// <summary>
    /// The top of a file for the peek below the favorites: its first lines (tabs as blank spaces). Only the start of
    /// the file is read, and nothing is written. A file that is not text is told. A file of size 0 is not opened: on
    /// Linux and macOS a named pipe or a device has size 0, and reading one could wait for ever
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
                head = new byte[(int)Math.Min(FavoritePeekBytes, stream.Length)];
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
    /// Taking favorites out of the favorites with P or ":fav" (in the file browser, the history, the search and the
    /// editor) asks first when one of them has a note, as the note goes with it
    /// </summary>
    /// <returns>False if the user said no</returns>
    internal static bool ConfirmNotesRemoved(IEnumerable<string> paths)
    {
        var notes = paths.Count(path => FileFavorites.NoteOf(path).Length > 0);
        if (notes == 0) return true;
        ChoiceBox.Forget();
        return ChoiceBox.Confirm(Lang.T("Remove favorite"),
            notes == 1
                ? Lang.T("Remove the favorite with its note?")
                : Lang.F("Remove the favorites with their {0} notes?", notes),
            Lang.T("The note is kept in the favorites, and is removed with the favorite."), false);
    }

    /// <summary>
    /// The note of a favorite or folder is being written in the editor (the file of it is not a favorite or a bookmark)
    /// </summary>
    internal static bool EditingFavoriteNote { get; private set; }

    /// <summary>
    /// The file a note is written in, in the data directory: one for each favorite or folder (by its path, or by the
    /// folder it is in and its name), so a recovery copy of a note is only offered for the note it was made of
    /// </summary>
    internal static string FavoriteNoteFileName(FileFavorites.Favorite favorite, string parentName)
    {
        var key = favorite.IsFolder
            ? "[" + parentName + "]" + favorite.Level + "[" + favorite.Name + "]"
            : favorite.Path;
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return FavoriteNoteFile + "-" + Convert.ToHexString(hash, 0, 6).ToLowerInvariant() + ".md";
    }

    // N or ":note": the note of the selected favorite or folder, written in the editor. The note is kept in the list
    // of favorites: it is written in a file of Blyant (in the data directory) for the editor, and read back from it,
    // also when the editor is left by a jump (Ctrl+O, :book)
    private static void EditFavoriteNote(int number)
    {
        if (number < 0) return;
        var favorite = FileFavorites.Items[number];
        var parent = FileFavorites.Parent(number);
        var parentName = parent < 0 ? string.Empty : FileFavorites.Items[parent].Name;
        var directory = FileIo.GetDataDirectoryName();
        var path = System.IO.Path.Combine(directory, FavoriteNoteFileName(favorite, parentName));
        string written;
        NavigationException? leaving = null;
        try
        {
            FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(path, new UTF8Encoding(false).GetBytes(favorite.Note));
            EditingFavoriteNote = true;
            try
            {
                EditBlyantFile(path, DialogText.CaptionsFileFavorites, 0);
            }
            catch (NavigationException e)
            {
                // A jump: the note (saved when the editor asked) is kept first
                leaving = e;
            }
            finally
            {
                EditingFavoriteNote = false;
            }

            written = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _favoriteMessage = e.Message;
            if (leaving != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(leaving);
            return;
        }
        finally
        {
            try
            {
                if (File.Exists(path)) FileIo.DeleteFile(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // It is written over the next time
            }
        }

        KeepFavoriteNote(favorite, number, parentName, written);
        if (leaving != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(leaving);
    }

    // The note written in the editor, given to its favorite or folder and saved
    private static void KeepFavoriteNote(FileFavorites.Favorite favorite, int number, string parentName, string written)
    {
        // The list may have been read again while the editor was open (the Jump menu reads it): the same favorite, or
        // the folder of the same name in a folder of the same name (the one at the same place, if there are more)
        if (!FileFavorites.Items.Contains(favorite))
        {
            var same = FindSameFavorite(favorite, number, parentName);
            if (same == null)
            {
                _favoriteMessage = Lang.T("The favorite is no longer in the list: the note was not kept");
                return;
            }

            favorite = same;
        }

        var before = favorite.Note;
        FileFavorites.SetNote(favorite, written);
        if (favorite.Note == before) return;
        SaveFavoriteList();
        if (_favoriteMessage.Length == 0)
        {
            _favoriteMessage = favorite.Note.Length == 0 ? Lang.T("Note removed") : Lang.T("Note kept in the favorites");
        }
    }

    /// <summary>
    /// A favorite or folder in the list as it was read again: the favorite of the same path, or the folder of the same
    /// name and level in a folder of the same name. With more such folders, the one at the place it had (none if it is
    /// not there)
    /// </summary>
    internal static FileFavorites.Favorite? FindSameFavorite(FileFavorites.Favorite favorite, int number,
        string parentName)
    {
        var items = FileFavorites.Items;
        if (!favorite.IsFolder)
        {
            return items.FirstOrDefault(item => !item.IsFolder && FileOperations.SamePath(item.Path, favorite.Path));
        }

        var found = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            var parent = FileFavorites.Parent(i);
            if (items[i].IsFolder && items[i].Level == favorite.Level && items[i].Name == favorite.Name &&
                (parent < 0 ? string.Empty : items[parent].Name) == parentName)
            {
                found.Add(i);
            }
        }

        if (found.Count == 1) return items[found[0]];
        return found.Contains(number) ? items[number] : null;
    }
}
