namespace Blyant;

/// <summary>
/// The lists of favorites in the shell, as ":list" in the file browser: "fav lists" shows them, "fav work" (or "fav use
/// work") opens one, "fav main" the main list, and "fav new", "fav rename" and "fav delete" make, rename and delete
/// one. The list opened is kept in the settings, so the full screen shows the same list. "fav filter draft" shows the
/// rows of the open list that have a text in them
/// </summary>
internal sealed partial class ShellSession
{
    // "fav ...": the favorites of the open list, "add" and "remove", or the lists
    private void Favorites(string argument)
    {
        // "fav -t storm", "fav work -t storm", "fav filter draft -t storm": the favorites with a text in a line
        string? search = null;
        if (ListsFavorites(argument))
        {
            (argument, search) = TextOption(argument);
            if (search == string.Empty)
            {
                Error(Lang.T("Give a text after -t: fav -t storm, fav -t *"));
                return;
            }
        }

        // "fav filter draft", "fav -l filter draft"
        if (FilterOf(argument) is { } filter)
        {
            FilterFavorites(filter.Text, filter.Full, search);
            return;
        }

        if (Change(argument) is { } add)
        {
            // ("fav remove [Drafts]": a folder)
            if (!add && FolderFirst(Rest(argument)) != null) RemoveFavoriteFolder(Rest(argument));
            else ChangeFavorites(Rest(argument), add);
            return;
        }

        switch (argument.TrimStart().Split(' ', 2)[0].ToLowerInvariant())
        {
            case "folder":
                NewFavoriteFolder(Rest(argument));
                break;
            case "subfolder":
                NewFavoriteSubfolder(Rest(argument));
                break;
            case "move":
                MoveFavorites(Rest(argument));
                break;
            case "undo":
                UndoFavorites();
                break;
            case "lists":
                ShowFavoriteLists();
                break;
            case "new":
                NewFavoriteList(Rest(argument));
                break;
            case "rename":
                // ("fav rename [Drafts] Old drafts": a folder; "fav rename work writing": a list)
                if (FolderFirst(Rest(argument)) != null) RenameFavoriteFolder(Rest(argument));
                else RenameFavoriteList(Rest(argument));
                break;
            case "delete":
                DeleteFavoriteList(Rest(argument));
                break;
            case "use":
                // (as it is: "fav use -l" opens a list named "-l")
                ListFavorites(Rest(argument), false, search);
                break;
            default:
                ListFavorites(argument, true, search);
                break;
        }
    }

    // True for what follows "fav" that shows a list (and so can go to a register: "fav > $files"): nothing, "-l", a
    // list ("fav work", "fav use work", "fav main") or a filter; not "add", "move", "lists" ... which change or show
    // something else
    private static bool ListsFavorites(string argument) =>
        argument.TrimStart().Split(' ', 2)[0].ToLowerInvariant() is not
            ("add" or "remove" or "folder" or "subfolder" or "move" or "undo" or "lists" or "new" or "rename" or "delete");

    // "filter text" after "fav", "hot" or "again" (with "-l" before it: the full paths, for "fav"; "hist" reads its
    // options itself): the text (without the quotes it can be written in), or null when it is not a filter
    private static (string Text, bool Full)? FilterOf(string argument)
    {
        var text = argument.Trim();
        var full = false;
        while (text == "-l" || text.StartsWith("-l ", StringComparison.Ordinal))
        {
            full = true;
            text = text[2..].TrimStart();
        }

        if (!text.Split(' ', 2)[0].Equals("filter", StringComparison.OrdinalIgnoreCase)) return null;
        var filter = Rest(text).Trim();
        return (Unquoted(filter) ?? filter, full);
    }

    // The favorites of the open list that a text is in (in the name or the path, letter case alike), or that a
    // pattern fits ("*.typ", "chapter-?.md": the name, as find takes it). Shown with the folders they are in, and
    // numbered as in the whole list, which is the last list: "open 7" and "fav move 7 top" are the row 7 of "fav"
    // "search" ("fav -t storm"): only the favorites with that text in a line, with the line found (as find -t); a
    // filter text can be left out then (null)
    private void FilterFavorites(string? text, bool full, string? search = null)
    {
        if (text is { Length: 0 })
        {
            Error(Lang.T("Give a text: fav filter draft (or *.md)"));
            return;
        }

        FileFavorites.Load();
        Line(Lang.F("List: {0}", ListWord(FileFavorites.CurrentList)));
        var items = FileFavorites.Items;
        var rows = new List<(int Index, Item Item)>();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].IsFolder) continue;
            // (with the place kept in the favorite, as "fav" lists it: "open 3" opens the file there)
            var at = items[i].Line > 0 ? (items[i].Line, Math.Max(1, items[i].Column)) : ((int, int)?)null;
            rows.Add((i, new Item(items[i].Path, System.IO.Directory.Exists(items[i].Path), at)));
        }

        // (with a search: the line found, and the row opens at it)
        var pattern = search == null ? null : FileSearch.WildcardText(search);
        var lines = rows.Select(row => pattern == null || row.Item.IsDirectory
            ? null
            : FileSearch.FirstLineWith(row.Item.Path, pattern)).ToList();
        for (var row = 0; row < rows.Count; row++)
        {
            if (lines[row] is { } line) rows[row] = (rows[row].Index, rows[row].Item with { At = (line.Line + 1, 1), Found = search });
        }

        ShowRows(rows.Select(row => row.Item).ToList());
        var names = Names(rows.Select(row => (row.Item.Path, row.Item.IsDirectory)).ToList(), full, false);
        var width = rows.Count.ToString().Length;
        var shown = new HashSet<int>(); // The folders shown above a row already
        var found = 0;
        for (var row = 0; row < rows.Count; row++)
        {
            var favorite = items[rows[row].Index];
            if (text != null && !Fits(text, favorite.Path, null)) continue;
            if (pattern != null && lines[row] == null) continue;

            // (the folder and subfolder it is in, above it, once)
            var folders = new List<int>();
            for (var parent = FileFavorites.Parent(rows[row].Index); parent >= 0; parent = FileFavorites.Parent(parent))
            {
                folders.Insert(0, parent);
            }

            foreach (var folder in folders.Where(shown.Add))
            {
                Line(new string(' ', 2 * items[folder].Level) + "[" + items[folder].Name + "]");
            }

            Row(row + 1, width, names[row] +
                (lines[row] is { } found1 ? ":" + (found1.Line + 1) + "  " + found1.Text : string.Empty), 2 * favorite.Level);
            RowShown(rows[row].Item);
            found++;
        }

        if (found == 0)
        {
            Line(search != null
                ? Lang.F("No favorite has {0} in a line", search)
                : Lang.F("No favorites with {0} in them", text ?? string.Empty));
        }
    }

    // The name of a list as the shell shows and takes it ("main" for the main list: a word of the commands)
    private static string ListWord(string name) => name.Length == 0 ? NamedLists.MainWord : name;

    // "fav lists": the main list and the others, "*" at the one that is open
    private void ShowFavoriteLists()
    {
        FileFavorites.Load();
        foreach (var name in FileFavorites.ListNames().Prepend(string.Empty))
        {
            Line((name == FileFavorites.CurrentList ? "  * " : "    ") + ListWord(name));
        }
    }

    // "fav new reading": a new, empty list, opened
    private void NewFavoriteList(string argument)
    {
        var name = argument.Trim().Trim('"').Trim();
        if (name.Length == 0)
        {
            Error(Lang.T("Give a name: fav new reading"));
            return;
        }

        var result = FileFavorites.NewList(name);
        if (Failed(result)) return;

        FileFavorites.UseList(name);
        FileIo.SaveSettings();
        Line(Lang.F("List made and opened: {0}", name));
    }

    // "fav rename work writing": a list (not the main list) gets another name; the open one stays open
    private void RenameFavoriteList(string argument)
    {
        var words = Words(argument);
        if (words.Count != 2)
        {
            Error(Lang.T("Give the list and its new name: fav rename work writing"));
            return;
        }

        if (FavoriteList(words[0]) is not { } name) return;
        if (name.Length == 0)
        {
            Error(Lang.T("Main list cannot be renamed"));
            return;
        }

        var result = FileFavorites.RenameList(name, words[1]);
        if (Failed(result)) return;

        FileIo.SaveSettings();
        Line(Lang.F("List {0} renamed to {1}", name, words[1].Trim()));
    }

    // "fav delete reading": a list (not the main list) to the trash, after asking (as a file is deleted); the files in
    // it stay where they are. When it was open, the main list is
    private void DeleteFavoriteList(string argument)
    {
        var words = Words(argument);
        if (words.Count != 1)
        {
            Error(Lang.T("Give the list to delete: fav delete reading"));
            return;
        }

        if (FavoriteList(words[0]) is not { } name) return;
        if (name.Length == 0)
        {
            Error(Lang.T("The main list cannot be deleted"));
            return;
        }

        if (!ChoiceBox.ConfirmGuarded(Lang.T("Delete list"), Lang.F("Delete the list {0}?", name),
                Lang.T("The files stay where they are.")))
        {
            Line(Lang.T("Nothing was deleted"));
            return;
        }

        var wasOpen = FileFavorites.CurrentList == name;
        var path = FileFavorites.ListPath(name);
        var result = FileFavorites.DeleteList(name, out var trashedPath);
        // (deleted for good it is logged where it is deleted)
        if (FileOperations.DeleteToTrash)
        {
            FileExplorer.LogFileOperation("Delete (to the trash)",
                path + (trashedPath.Length > 0 ? " -> " + trashedPath : string.Empty), result);
        }

        if (Failed(result)) return;

        if (wasOpen) FileIo.SaveSettings();
        Line(Lang.F("List deleted: {0}", name));
        if (wasOpen) Line(Lang.F("List: {0}", NamedLists.MainWord));
    }

    // The list of a name as written ("main": the main list, empty), or null (told)
    private string? FavoriteList(string word)
    {
        if (FileFavorites.FindList(word.Trim().Trim('"')) is { } name) return name;
        Error(Lang.F("No list of favorites named {0}", word.Trim().Trim('"')));
        return null;
    }
}
