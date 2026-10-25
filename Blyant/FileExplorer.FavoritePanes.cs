using System.Text;

namespace Blyant;

// The panes below the list of favorites (NotePanes): the note of the selected favorite or folder, and a peek at it
// (the top of the file, what is in the directory, or what is in the folder). ":panes" turns them off and on, ":peek"
// the peek (the settings "favoritepanes" and "favoritepeek"). The notes are kept in the list of favorites (never next
// to the files, which may be in a repository): N writes the note in the editor (EditListNote, also for the bookmarks).
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

    // The start of the name of the file of Blyant a note of a favorite is written in, in the editor (in the data
    // directory, removed after)
    private const string FavoriteNoteFile = "favorite-note"; // English: the name of a file

    // The rows below the title, for the list and the panes
    private static int FavoriteRows => Init.LineCount - ListTop - 2;

    // The rows of text of the panes (0: not shown, also when the window is too low for them)
    private static int FavoritePaneRows => NotePanes.Rows(FavoritePanes, FavoriteRows);

    private static int FavoritePageSize => FavoriteRows - (FavoritePaneRows > 0 ? FavoritePaneRows + 1 : 0);

    // The column of the line between the note and the peek
    private static int FavoritePaneLine => Init.LineWidth / 2;

    // Where lines between columns meet the divider above the bottom line: the lines of the list, or of the panes
    private static int[] FavoriteBottomLines => FavoritePaneRows == 0 ? _favoriteLines
        : FavoritePeek ? new[] { FavoritePaneLine } : Array.Empty<int>();

    /// <summary>
    /// ":panes" and ":peek" in a list with panes: the panes, or the peek in them, on or off (the peek on shows the
    /// panes too)
    /// </summary>
    /// <param name="peek">True for ":peek"</param>
    /// <param name="panes">The panes are shown</param>
    /// <param name="peeking">The peek is shown</param>
    /// <returns>What is told</returns>
    internal static string TogglePanes(bool peek, ref bool panes, ref bool peeking)
    {
        if (peek)
        {
            peeking = !peeking;
            if (peeking) panes = true;
            return peeking ? Lang.T("Peek on") : Lang.T("Peek off");
        }

        panes = !panes;
        return panes ? Lang.T("Note and peek shown") : Lang.T("Note and peek hidden");
    }

    // ":panes" and ":peek" in the favorites (kept in the settings)
    private static void ToggleFavoritePanes(bool peek)
    {
        var panes = FavoritePanes;
        var peeking = FavoritePeek;
        _favoriteMessage = TogglePanes(peek, ref panes, ref peeking);
        FavoritePanes = panes;
        FavoritePeek = peeking;
        if (FavoritePanes && FavoritePaneRows == 0) _favoriteMessage = Lang.T("The window is too low for the panes");
        FileIo.SaveSettings();
    }

    // The panes, below the list: a divider with their names, and the note and the peek of the selected item
    private static void DrawFavoritePanes()
    {
        var rows = FavoritePaneRows;
        if (rows == 0) return;
        var selected = _favorites.Count > 0 ? _favorites[_favoriteIndex] : -1;
        var favorite = selected >= 0 ? FileFavorites.Items[selected] : null;
        NotePanes.Draw(ListTop + FavoritePageSize, 0, Init.LineWidth, rows, _favoriteLines, favorite?.Note,
            FavoritePeek ? favorite == null ? new List<string>() : PeekOf(selected, rows) : null);
    }

    // What the peek shows of an item of the list: the top of a file, what is in a directory, or what is in a folder
    private static List<string> PeekOf(int number, int rows)
    {
        var favorite = FileFavorites.Items[number];
        if (!favorite.IsFolder) return NotePanes.PeekPlace(favorite.Path, rows);
        return NotePanes.PeekFolder(FileFavorites.InFolder(number).Select(item =>
            new string(' ', 2 * (item.Level - favorite.Level - 1)) +
            (item.IsFolder ? "[" + item.Name + "]"
                : FavoriteName(item.Path) + (Directory.Exists(item.Path) ? "/" : ""))).ToList(), rows);
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
    /// A note of a list (a favorite, a bookmark or a folder) is being written in the editor: the file of it is not a
    /// favorite or a bookmark
    /// </summary>
    internal static bool EditingListNote { get; private set; }

    /// <summary>
    /// The file a note of a favorite is written in (NotePanes.NoteFileName): by its path, or by the folder it is in
    /// and its name
    /// </summary>
    internal static string FavoriteNoteFileName(FileFavorites.Favorite favorite, string parentName) =>
        NotePanes.NoteFileName(FavoriteNoteFile, favorite.IsFolder
            ? "[" + parentName + "]" + favorite.Level + "[" + favorite.Name + "]"
            : favorite.Path);

    /// <summary>
    /// Write a note of a list in the editor: it is written in a file of Blyant (in the data directory) for the editor,
    /// read back from it and given to keep, also when the editor is left by a jump (Ctrl+O, :book; the jump goes on
    /// after). The file is removed after
    /// </summary>
    /// <param name="note">The note as it is</param>
    /// <param name="fileName">The file of the note (NotePanes.NoteFileName)</param>
    /// <param name="caption">The caption of a box telling a file could not be opened</param>
    /// <param name="keep">Given the text of the note as it was left in the editor</param>
    /// <returns>An error message, or null</returns>
    internal static string? EditListNote(string note, string fileName, string caption, Action<string> keep)
    {
        var directory = FileIo.GetDataDirectoryName();
        var path = System.IO.Path.Combine(directory, fileName);
        string written;
        NavigationException? leaving = null;
        try
        {
            FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(path, new UTF8Encoding(false).GetBytes(note));
            EditingListNote = true;
            try
            {
                EditBlyantFile(path, caption, 0);
            }
            catch (NavigationException e)
            {
                // A jump: the note (saved when the editor asked) is kept first
                leaving = e;
            }
            finally
            {
                EditingListNote = false;
            }

            written = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (leaving != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(leaving);
            return e.Message;
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

        keep(written);
        if (leaving != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(leaving);
        return null;
    }

    // N or ":note": the note of the selected favorite or folder, written in the editor
    private static void EditFavoriteNote(int number)
    {
        if (number < 0) return;
        var favorite = FileFavorites.Items[number];
        var parent = FileFavorites.Parent(number);
        var parentName = parent < 0 ? string.Empty : FileFavorites.Items[parent].Name;
        var error = EditListNote(favorite.Note, FavoriteNoteFileName(favorite, parentName),
            DialogText.CaptionsFileFavorites, written => KeepFavoriteNote(favorite, number, parentName, written));
        if (error != null) _favoriteMessage = error;
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
