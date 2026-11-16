namespace Blyant;

/// <summary>
/// The folders of the favorites in the shell, and the favorites arranged, as on the favorites page of the full
/// screen: "fav folder", "fav subfolder", "fav rename [folder]", "fav remove [folder]", "fav move" and "fav undo". A
/// folder is named as "fav" shows it, in brackets ("[Drafts]", a subfolder "[Writing/Drafts]"), and a favorite by its
/// row in "fav" (the folders are not rows) or, for "fav move", by its name ("plot.md", "chapter-*.md"). Each change is
/// saved, and the list is shown again with its new rows
/// </summary>
internal sealed partial class ShellSession
{
    // What "fav undo" puts back: the list of favorites as it was before the last "fav remove", and as it was after it
    // (it is put back only while the list is so: not over a change made since). Kept while the shell runs
    private (string List, string Before, string After)? _favoritesRemoved;

    // A folder named as typed: "[Drafts]" or "Drafts" for a folder or subfolder of that name (the only one), or
    // "[Writing/Drafts]" for the subfolder in Writing. Letter case alike. -1 (and told) if there is none, or more
    private int FavoriteFolder(string word)
    {
        var name = word.Trim().Trim('"').Trim();
        if (name.StartsWith('[') && name.EndsWith(']')) name = name[1..^1].Trim();
        var items = FileFavorites.Items;
        var found = Enumerable.Range(0, items.Count)
            .Where(i => items[i].IsFolder && items[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (found.Count == 0)
        {
            // "Writing/Drafts": the subfolder Drafts in the folder Writing (a name can have a "/" in it: that first)
            for (var cut = name.IndexOf('/'); cut > 0 && found.Count == 0; cut = name.IndexOf('/', cut + 1))
            {
                var (top, sub) = (name[..cut].Trim(), name[(cut + 1)..].Trim());
                found = Enumerable.Range(0, items.Count).Where(i => items[i].IsFolder && items[i].Level == 1 &&
                    items[i].Name.Equals(sub, StringComparison.OrdinalIgnoreCase) &&
                    items[FileFavorites.Parent(i)].Name.Equals(top, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        if (found.Count == 1) return found[0];
        Error(found.Count == 0
            ? Lang.F("No folder [{0}] in the favorites (fav shows them)", name)
            : Lang.F("{0} folders are named [{1}]: give the parent folder too, [Writing/{1}]", found.Count, name));
        return -1;
    }

    // A folder written first in a text: "[Drafts] rest", "[My drafts] rest" or ""[My drafts]" rest" (the folder,
    // and what follows it), or null when the text does not start with a folder
    private static (string Folder, string After)? FolderFirst(string text)
    {
        text = text.TrimStart();
        var quoted = text.StartsWith("\"[");
        if (!quoted && !text.StartsWith('[')) return null;
        var end = quoted ? text.IndexOf("]\"", StringComparison.Ordinal) : text.IndexOf(']');
        if (end < 0) return null;
        var close = end + (quoted ? 2 : 1);
        // ("[2024].md": a file whose name starts with "[", not a folder)
        if (close < text.Length && !char.IsWhiteSpace(text[close])) return null;
        return (text[..close], text[close..].Trim());
    }

    // The favorites (not the folders), in the order "fav" numbers them
    private static List<FileFavorites.Favorite> FavoriteRows() =>
        FileFavorites.Items.Where(item => !item.IsFolder).ToList();

    // A change of the favorites saved, logged (what was done, never a note) and told, and the list shown again
    private void FavoritesChanged(string done, string log)
    {
        var result = FileFavorites.Save();
        // English: the log
        EventLog.Add(DialogText.LogFile + "Favorites: " + log);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        Line(done);
        ListFavorites(string.Empty);
    }

    // "fav folder Drafts": a folder, last in the list
    private void NewFavoriteFolder(string argument)
    {
        var name = argument.Trim().Trim('"').Trim();
        if (name.Length == 0)
        {
            Error(Lang.T("Give a name: fav folder Drafts"));
            return;
        }

        FileFavorites.Load();
        if (FileFavorites.AddFolder(name, -1) < 0)
        {
            Error(Lang.F("Not a folder name: {0} ([ and ] are not allowed)", name));
            return;
        }

        FavoritesChanged(Lang.F("Folder made: [{0}]", name), "folder made");
    }

    // "fav subfolder [Writing] Drafts": a subfolder, first in a folder at the top
    private void NewFavoriteSubfolder(string argument)
    {
        if (FolderFirst(argument) is not { } parts || parts.After.Trim('"').Trim().Length == 0)
        {
            Error(Lang.T("Give the folder and a name: fav subfolder [Writing] Drafts"));
            return;
        }

        FileFavorites.Load();
        if (FavoriteFolder(parts.Folder) is var folder && folder < 0) return;
        var name = parts.After.Trim('"').Trim();
        if (FileFavorites.Items[folder].Level > 0)
        {
            Error(Lang.F("[{0}] is a subfolder: subfolders go in top-level folders (two levels at most)",
                FileFavorites.Items[folder].Name));
            return;
        }

        if (FileFavorites.AddSubfolder(name, folder) < 0)
        {
            Error(Lang.F("Not a folder name: {0} ([ and ] are not allowed)", name));
            return;
        }

        FavoritesChanged(Lang.F("Folder made: [{0}]", FileFavorites.Items[folder].Name + "/" + name), "subfolder made");
    }

    // "fav rename [Drafts] Old drafts": a folder gets another name
    private void RenameFavoriteFolder(string argument)
    {
        if (FolderFirst(argument) is not { } parts || parts.After.Trim('"').Trim().Length == 0)
        {
            Error(Lang.T("Give the folder and its new name: fav rename [Drafts] Old drafts"));
            return;
        }

        FileFavorites.Load();
        if (FavoriteFolder(parts.Folder) is var folder && folder < 0) return;
        var name = parts.After.Trim('"').Trim();
        if (!FileFavorites.RenameFolder(folder, name))
        {
            Error(Lang.F("Not a folder name: {0} ([ and ] are not allowed)", name));
            return;
        }

        FavoritesChanged(Lang.F("Folder renamed: [{0}]", name), "folder renamed");
    }

    // "fav remove [Drafts]": a folder with the favorites and subfolders in it, after asking (the files stay).
    // "fav undo" puts it back
    private void RemoveFavoriteFolder(string argument)
    {
        if (FolderFirst(argument) is not { } parts || parts.After.Length > 0)
        {
            Error(Lang.T("Give one folder: fav remove [Drafts]"));
            return;
        }

        FileFavorites.Load();
        if (FavoriteFolder(parts.Folder) is var index && index < 0) return;
        var folder = FileFavorites.Items[index];
        var count = FileFavorites.FolderCount(index);
        var subfolders = FileFavorites.SubfolderCount(index);
        var note = subfolders > 0
            ? Lang.F("Removed with the folder (the files stay), favorites: {0}, subfolders: {1}.", count,
                subfolders)
            : count == 0
                ? Lang.T("The folder is empty.")
                : count == 1
                    ? Lang.T("The favorite in it is removed from the list too (the file stays).")
                    : Lang.F("The favorites in it are removed from the list too (the files stay): {0}.", count);
        // (No selected, as change does when it deletes lines: Enter alone keeps the folder)
        if (!ChoiceBox.Confirm(folder.Level > 0 ? Lang.T("Remove subfolder") : DialogText.FavoriteDeleteFolder,
                Lang.F("Remove \"{0}\" from the favorites?", folder.Name), note, true))
        {
            Line(Lang.T("Nothing was removed"));
            return;
        }

        var before = FileFavorites.Serialize();
        FileFavorites.RemoveAt(index);
        KeepForUndo(before);
        FavoritesChanged(Lang.F("Folder removed: [{0}] (fav undo puts it back)", folder.Name),
            "folder removed");
    }

    // The list as it was before a removal, and as it is after it: for "fav undo"
    private void KeepForUndo(string before) =>
        _favoritesRemoved = (FileFavorites.CurrentList, before, FileFavorites.Serialize());

    // "fav undo": what "fav remove" removed last (a favorite, or a folder with its favorites) put back where it was,
    // while the list is as the removal left it
    private void UndoFavorites()
    {
        FileFavorites.Load();
        if (_favoritesRemoved is not { } removed)
        {
            Error(Lang.T("Nothing to put back: fav remove has removed nothing here"));
            return;
        }

        if (removed.List != FileFavorites.CurrentList)
        {
            Error(Lang.F("fav undo puts back in the list {0}: open it first (fav {0})", ListWord(removed.List)));
            return;
        }

        if (FileFavorites.Serialize() != removed.After)
        {
            Error(Lang.T("The favorites have changed since: nothing is put back"));
            return;
        }

        FileFavorites.Parse(removed.Before);
        _favoritesRemoved = null;
        FavoritesChanged(Lang.T("Put back what was removed last"), "undo");
    }

    // "fav move 3 top", "fav move 3,5 [Drafts]", "fav move plot.md top", "fav move chapter-*.md [Book]", "fav move
    // [Drafts] up": favorites (by their rows in "fav": "3", "3-5", "3,7", or by their names, a pattern or a path; see
    // FavoritesNamed) or a folder ("[Drafts]") moved: "top" or "bottom" (of the folder they are in, or of the list),
    // "up" or "down" one step (one favorite or folder, as V with j and k), "out" (out of their folders: favorites
    // last in the list, a subfolder after its folder), into a folder ("[Drafts]": at the top of it; a folder goes into a
    // folder at the top as a subfolder), or, for favorites, to the place of a row ("5": in front of it, in its
    // folder)
    private void MoveFavorites(string argument)
    {
        FileFavorites.Load();
        var usage = MoveUsage;
        string where;
        FileFavorites.Favorite? folder = null;
        List<FileFavorites.Favorite> moved = new();
        if (FolderFirst(argument) is { } parts)
        {
            if (FavoriteFolder(parts.Folder) is var index && index < 0) return;
            folder = FileFavorites.Items[index];
            where = parts.After;
        }
        else
        {
            var words = Words(argument);
            if (words.Count < 2)
            {
                Error(usage);
                return;
            }

            var rows = FavoriteRows();
            if (LineSet(words[0], rows.Count, out var numbers) && numbers.Count > 0)
            {
                if (numbers[^1] > rows.Count || numbers[0] < 1)
                {
                    Error(rows.Count == 1
                        ? Lang.F("No row {0}: the favorites have 1 row", numbers[^1])
                        : Lang.F("No row {0}: the favorites have {1} rows", numbers[^1], rows.Count));
                    return;
                }

                moved = numbers.Select(n => rows[n - 1]).ToList();
            }
            else if (FavoritesNamed(words[0]) is { } named)
            {
                moved = named;
            }
            else
            {
                return;
            }

            where = AfterWords(argument, 1).Trim();
        }

        if (where.Length == 0)
        {
            Error(usage);
            return;
        }

        var what = folder != null
            ? "[" + folder.Name + "]"
            : string.Join(", ", moved.Select(FavoriteName));
        var error = folder != null ? MoveFolder(folder, where) : MoveRows(moved, where);
        if (error == null) FavoritesChanged(Lang.F("Moved: {0}", what), "move");
        else if (error.Length > 0) Error(error);
    }

    // The name of a favorite as "fav" shows it, without the "/" of a directory (a root: its path), as the favorites
    // page names it
    internal static string FavoriteName(FileFavorites.Favorite favorite) => FileExplorer.FavoriteName(favorite.Path);

    // The favorites a name typed stands for: the one of that name ("plot.md", "chapters/"), or all those a pattern
    // fits ("chapter-*.md"), in their order; a path ("~/novel/plot.md", "drafts/plot.md") the one of that path. Letter
    // case alike, as "fav" shows the names. Null (and told) when none is, or when more have the name (their rows told:
    // the row is then given)
    private List<FileFavorites.Favorite>? FavoritesNamed(string word)
    {
        var name = word.Trim().Trim('"');
        var trimmed = Path.TrimEndingDirectorySeparator(name);
        var rows = FavoriteRows();
        List<int> found;
        // (a path: with a separator of this system, or from the home directory; on Linux a "\" can be in a name)
        if (trimmed.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 ||
            trimmed == "~")
        {
            var path = PathOf(trimmed);
            found = Enumerable.Range(0, rows.Count).Where(i => FileOperations.SamePath(rows[i].Path, path)).ToList();
        }
        else if (ShellCompletion.IsPattern(trimmed))
        {
            found = Enumerable.Range(0, rows.Count).Where(i =>
                ShellCompletion.PatternFits(trimmed, FavoriteName(rows[i]))).ToList();
        }
        else
        {
            found = Enumerable.Range(0, rows.Count).Where(i =>
                FavoriteName(rows[i]).Equals(trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (found.Count == 0)
        {
            Error(Lang.F("No favorite named {0} (fav shows them)", name));
            return null;
        }

        if (found.Count > 1 && !ShellCompletion.IsPattern(trimmed))
        {
            Error(Lang.F("{0} favorites are named {1}: give the row ({2})", found.Count, name,
                string.Join(", ", found.Select(i => i + 1))));
            return null;
        }

        return found.Select(i => rows[i]).ToList();
    }

    private static string MoveUsage => Lang.T("Give what and where: fav move 3 top (bottom, up, down, out, 5, [Drafts])");

    // A folder moved (null: done; "": told already; else what went wrong)
    private string? MoveFolder(FileFavorites.Favorite folder, string where)
    {
        var index = FileFavorites.IndexOf(folder);
        switch (where.Trim('"').ToLowerInvariant())
        {
            case "top":
            case "bottom":
                FileFavorites.MoveToEnd(new List<FileFavorites.Favorite> { folder }, where.Equals("top",
                    StringComparison.OrdinalIgnoreCase));
                return null;
            case "up":
            case "down":
            {
                // (a folder moved this way is closed, as V closes it: kept as it was)
                var collapsed = folder.Collapsed;
                var up = where.Equals("up", StringComparison.OrdinalIgnoreCase);
                if (!FileFavorites.Move(index, up))
                {
                    return up ? Lang.F("[{0}] is at the top", folder.Name) : Lang.F("[{0}] is at the end", folder.Name);
                }

                folder.Collapsed = collapsed;
                return null;
            }
            case "out":
                if (folder.Level == 0) return Lang.F("[{0}] is not in a folder", folder.Name);
                return FileFavorites.MoveFolderTo(folder, null) < 0 ? Lang.F("[{0}] is not in a folder", folder.Name) : null;
        }

        if (FolderFirst(where) is not { } into)
        {
            return Lang.T("A folder moves to top, bottom, up, down, out, or into a [folder]");
        }

        if (FavoriteFolder(into.Folder) is var target && target < 0) return string.Empty;
        if (folder.Level == 0 && FileFavorites.SubfolderCount(index) > 0)
        {
            return Lang.F("{0} has subfolders: it cannot go into another folder", folder.Name);
        }

        return FileFavorites.MoveFolderTo(folder, FileFavorites.Items[target]) < 0
            ? Lang.F("[{0}] cannot go into [{1}]: only into another top-level folder", folder.Name,
                FileFavorites.Items[target].Name)
            : null;
    }

    // Favorites moved (null: done; "": told already; else what went wrong)
    private string? MoveRows(List<FileFavorites.Favorite> moved, string where)
    {
        switch (where.Trim('"').ToLowerInvariant())
        {
            case "top":
            case "bottom":
                FileFavorites.MoveToEnd(moved, where.Equals("top", StringComparison.OrdinalIgnoreCase));
                return null;
            case "up":
            case "down":
            {
                if (moved.Count != 1) return Lang.T("up and down move one row: fav move 3 up");
                var up = where.Equals("up", StringComparison.OrdinalIgnoreCase);
                if (!FileFavorites.Move(FileFavorites.IndexOf(moved[0]), up))
                {
                    return up ? Lang.T("The row is at the top") : Lang.T("The row is at the end");
                }

                return null;
            }
            case "out":
            {
                var inFolders = moved.Where(favorite => favorite.Level > 0).ToList();
                if (inFolders.Count == 0) return Lang.T("Not in a folder: fav move 3 [Drafts] puts it in one");
                FileFavorites.MoveToFolder(inFolders, null);
                return null;
            }
        }

        if (FolderFirst(where) is { } into)
        {
            if (FavoriteFolder(into.Folder) is var target && target < 0) return string.Empty;
            FileFavorites.MoveToFolder(moved, FileFavorites.Items[target]);
            return null;
        }

        // The place of a row: in front of it
        var rows = FavoriteRows();
        if (!int.TryParse(where, out var row)) return MoveUsage;
        if (row < 1 || row > rows.Count)
        {
            return rows.Count == 1
                ? Lang.F("No row {0}: the favorites have 1 row", row)
                : Lang.F("No row {0}: the favorites have {1} rows", row, rows.Count);
        }

        var place = rows[row - 1];
        if (moved.Contains(place)) return Lang.F("Target {0} is among the rows moved", row);
        foreach (var favorite in moved) FileFavorites.MoveBefore(favorite, place);
        return null;
    }
}
