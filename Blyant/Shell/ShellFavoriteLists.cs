namespace Blyant;

/// <summary>
/// The lists of favorites in the shell, as ":list" in the file browser: "fav lists" shows them, "fav work" (or "fav use
/// work") opens one, "fav main" the main list, and "fav new", "fav rename" and "fav delete" make, rename and delete
/// one. The list opened is kept in the settings, so the full screen shows the same list
/// </summary>
internal sealed partial class ShellSession
{
    // "fav ...": the favorites of the open list, "add" and "remove", or the lists
    private void Favorites(string argument)
    {
        if (Change(argument) is { } add)
        {
            ChangeFavorites(Rest(argument), add);
            return;
        }

        switch (argument.TrimStart().Split(' ', 2)[0].ToLowerInvariant())
        {
            case "lists":
                ShowFavoriteLists();
                break;
            case "new":
                NewFavoriteList(Rest(argument));
                break;
            case "rename":
                RenameFavoriteList(Rest(argument));
                break;
            case "delete":
                DeleteFavoriteList(Rest(argument));
                break;
            case "use":
                ListFavorites(Rest(argument));
                break;
            default:
                ListFavorites(argument);
                break;
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
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

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
            Error(Lang.T("The main list keeps its name"));
            return;
        }

        var result = FileFavorites.RenameList(name, words[1]);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

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

        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        if (wasOpen) FileIo.SaveSettings();
        Line(Lang.F("List deleted: {0}", name));
        if (wasOpen) Line(Lang.F("List: {0}", NamedLists.MainWord));
    }

    // The list of a name as written ("main": the main list, empty), or null (told)
    private static string? FindFavoriteList(string word) => FileFavorites.FindList(word.Trim().Trim('"'));

    private string? FavoriteList(string word)
    {
        if (FindFavoriteList(word) is { } name) return name;
        Error(Lang.F("No list of favorites named {0}", word.Trim().Trim('"')));
        return null;
    }
}
