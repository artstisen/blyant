namespace Blyant;

// The lists of favorites (":list"): one list is open at a time, and the page shows it
internal static partial class FileExplorer
{
    // The name of a list as it is shown (the main list has none of its own)
    private static string FavoriteListName(string name) => name.Length == 0 ? Lang.T("Main list") : name;

    // ":list": the lists in a box, to open one, add one, rename one or delete one. ":list name" opens one at once
    private static void ShowFavoriteLists(string argument)
    {
        if (argument.Trim().Length > 0)
        {
            if (FileFavorites.FindList(argument) is { } found) OpenFavoriteList(found);
            else _favoriteMessage = Lang.F("No list named {0}", argument.Trim());
            return;
        }

        while (true)
        {
            var lists = FileFavorites.ListNames().Prepend(string.Empty).ToList();
            string? open = null;
            var picked = PickListBox.Show(Lang.T("Lists of favorites"), lists.Select(FavoriteListName).ToList(),
                row => FileFavorites.ListPreview(lists[row]), Math.Max(0, lists.IndexOf(FileFavorites.CurrentList)),
                Lang.T("j/k Select | Enter Open | N New | R Rename | D Delete | Esc Close"), new PickListBox.Extras
                {
                    Key = (key, row) =>
                    {
                        if ((key.Modifiers & ConsoleModifiers.Control) != 0 || row < 0) return PickListBox.KeyUse.None;
                        switch (key.Key)
                        {
                            case ConsoleKey.N:
                                open = NewFavoriteList();
                                return open != null ? PickListBox.KeyUse.Close : PickListBox.KeyUse.Used;
                            case ConsoleKey.R:
                                return RenameFavoriteList(lists[row]);
                            case ConsoleKey.D:
                                return DeleteFavoriteList(lists[row]);
                            default:
                                return PickListBox.KeyUse.None;
                        }
                    }
                });
            if (picked == PickListBox.Reload) continue;
            if (open != null) OpenFavoriteList(open);
            else if (picked >= 0 && lists[picked] != FileFavorites.CurrentList) OpenFavoriteList(lists[picked]);
            return;
        }
    }

    // A list is shown from its top, without what was marked or filtered in the one before it
    private static void OpenFavoriteList(string name)
    {
        FileFavorites.UseList(name);
        ShowOtherFavoriteList();
        _favoriteMessage = Lang.F("List: {0}", FavoriteListName(FileFavorites.CurrentList));
    }

    private static void ShowOtherFavoriteList()
    {
        FileIo.SaveSettings();
        _favoriteMarks.Clear();
        _favoriteFilter = string.Empty;
        _favoriteIndex = 0;
    }

    // The box for a new list: the name of the list made, or null
    private static string? NewFavoriteList()
    {
        ChoiceBox.Forget();
        var name = ChoiceBox.AskText(Lang.T("New list"), DialogText.TypeNameAndPressEnter, DialogText.NameColon,
            string.Empty);
        if (name == null) return null;
        var result = FileFavorites.NewList(name);
        if (result == DialogText.Success) return name.Trim();
        ChoiceBox.Message(Lang.T("New list"), result, string.Empty);
        return null;
    }

    private static PickListBox.KeyUse RenameFavoriteList(string name)
    {
        ChoiceBox.Forget();
        if (name.Length == 0)
        {
            ChoiceBox.Message(Lang.T("Rename list"), Lang.T("The main list keeps its name"), string.Empty);
            return PickListBox.KeyUse.Used;
        }

        var newName = ChoiceBox.AskText(Lang.T("Rename list"), DialogText.TypeNameAndPressEnter, DialogText.NameColon, name);
        if (newName == null) return PickListBox.KeyUse.Used;
        var result = FileFavorites.RenameList(name, newName);
        if (result == DialogText.Success)
        {
            FileIo.SaveSettings();
            return PickListBox.KeyUse.Reload;
        }

        ChoiceBox.Message(Lang.T("Rename list"), result, string.Empty);
        return PickListBox.KeyUse.Used;
    }

    private static PickListBox.KeyUse DeleteFavoriteList(string name)
    {
        ChoiceBox.Forget();
        if (name.Length == 0)
        {
            ChoiceBox.Message(Lang.T("Delete list"), Lang.T("The main list cannot be deleted"), string.Empty);
            return PickListBox.KeyUse.Used;
        }

        if (!ChoiceBox.ConfirmGuarded(Lang.T("Delete list"), Lang.F("Delete the list {0}?", name),
                Lang.T("The files stay where they are.")))
        {
            return PickListBox.KeyUse.Used;
        }

        var wasOpen = FileFavorites.CurrentList == name;
        var path = FileFavorites.ListPath(name);
        var result = FileFavorites.DeleteList(name, out var trashedPath);
        // (deleted for good it is logged where it is deleted)
        if (FileOperations.DeleteToTrash)
        {
            LogOperation("Delete (to the trash)", path + (trashedPath.Length > 0 ? " -> " + trashedPath : string.Empty),
                result);
        }

        if (result != DialogText.Success)
        {
            ChoiceBox.Message(Lang.T("Delete list"), result, string.Empty);
            return PickListBox.KeyUse.Used;
        }

        // (the main list is open now)
        if (wasOpen) ShowOtherFavoriteList();
        return PickListBox.KeyUse.Reload;
    }
}
