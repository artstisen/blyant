namespace Blyant;

// The favorites of the file browser, shown as a list: the files and directories the user has marked, in the
// order and with the colors set by the user, and grouped in folders. The list itself is kept by the class
// FileFavorites.
internal static partial class FileExplorer
{
    private enum FavoriteAction
    {
        Close,
        Open, // Open the file (or go to the directory)
        GoTo // Show where the file is, in the browser
    }

    private const int FavoritePickerLeft = 28;

    private static List<int> _favorites = new(); // The favorites shown (their numbers in the whole list)
    private static int _favoriteIndex;
    private static int _favoriteTop;
    private static string _favoriteFilter = string.Empty;
    private static string _favoriteMessage = string.Empty;
    private static bool _favoriteMoving; // The selected favorite follows j/k up and down the list

    // Show the favorites until the list is closed. Opening a file returns to the list afterwards
    private static KeyResult BrowseFavorites()
    {
        _favoriteFilter = string.Empty;
        _favoriteMessage = string.Empty;
        _favoriteMoving = false;
        _favoriteMarks.Clear();
        while (true)
        {
            var action = ShowFavorites(out var path);
            // Shown alone (from the start page): closed back there
            if (action == FavoriteAction.Close) return _favoritesOnly ? KeyResult.Close : KeyResult.Handled;
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
            {
                _favoriteMessage = DialogText.FileNotFound;
                continue;
            }

            if (action == FavoriteAction.GoTo || isDirectory)
            {
                // Show the directory, or the directory of the file with the file selected
                var parent = isDirectory && action == FavoriteAction.Open
                    ? path
                    : System.IO.Path.GetDirectoryName(path);
                if (parent == null || !Directory.Exists(parent))
                {
                    _favoriteMessage = DialogText.DirectoryNotFound;
                    continue;
                }

                ChangeDirectory(parent, isDirectory && action == FavoriteAction.Open ? null : path);
                // The file browser is shown now, and closed as usual
                _favoritesOnly = false;
                return KeyResult.Handled;
            }

            _message = string.Empty;
            if (Open(new Item(System.IO.Path.GetFileName(path), path, false, new FileInfo(path).Length)))
            {
                return KeyResult.Close;
            }

            _favoriteMessage = _message;
            _message = string.Empty;
            // A file written with ":w name" or ":saveas name" and put in the favorites is selected
            SelectFavoriteFile(_lastWrittenPath);
        }
    }

    // Select a favorite by its path (also when the list was filtered)
    private static void SelectFavoriteFile(string path)
    {
        if (path.Length == 0) return;
        var number = FileFavorites.Items.ToList()
            .FindIndex(item => !item.IsFolder && FileOperations.SamePath(item.Path, path));
        if (number < 0) return;
        _favoriteFilter = string.Empty;
        FileFavorites.Reveal(number);
        FilterFavorites();
        _favoriteIndex = Math.Max(0, _favorites.IndexOf(number));
    }

    private static void FilterFavorites()
    {
        _favorites = new List<int>();
        for (var i = 0; i < FileFavorites.Items.Count; i++)
        {
            var item = FileFavorites.Items[i];
            if (_favoriteFilter.Length > 0)
            {
                // The favorites and folders found (by the path, the name of a folder, or the note) are shown without
                // the folders they are in
                if ((item.IsFolder ? item.Name : item.Path).Contains(_favoriteFilter, StringComparison.OrdinalIgnoreCase) ||
                    item.Note.Contains(_favoriteFilter, StringComparison.OrdinalIgnoreCase))
                {
                    _favorites.Add(i);
                }

                continue;
            }

            // Not in a folder that is closed
            if (FileFavorites.IsShown(i)) _favorites.Add(i);
        }
    }

    private static FavoriteAction ShowFavorites(out string path)
    {
        path = string.Empty;
        var redraw = true;
        while (true)
        {
            FilterFavorites();
            _favoriteIndex = Math.Clamp(_favoriteIndex, 0, Math.Max(0, _favorites.Count - 1));
            // Follow the size of the terminal window, and draw again if the page could not be drawn
            if (Init.ApplyAutoSize()) redraw = true;
            DrawFavorites(redraw);
            if (Tools.TakeRedrawRequest()) DrawFavorites(true);
            redraw = true;
            var message = _favoriteMessage;
            var hadMessage = message.Length > 0;
            _favoriteMessage = string.Empty;
            var selected = _favorites.Count > 0 ? _favorites[_favoriteIndex] : -1;
            var key = Keyboard.ReadKey();
            // The window was resized (or Blyant went on after Ctrl+Z): all is drawn again, with the message
            if (Keyboard.IsRedraw(key))
            {
                _favoriteMessage = message;
                continue;
            }

            if (Jump.IsKey(key))
            {
                // The Jump menu (a count typed before it is dropped)
                _listCount.Take();
                Jump.Open();
                FindFavoriteMarks();
                continue;
            }

            // A count in front of a key: 5j 5k (5 rows), 5gg 5G (row 5). Esc only drops it
            if (_listCount.Add(key))
            {
                redraw = hadMessage;
                continue;
            }

            var count = _listCount.Take();
            if (count != null && key.Key == ConsoleKey.Escape)
            {
                redraw = hadMessage;
                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;

            if (_favoriteMoving)
            {
                // The selected favorite is moved until Esc is pressed
                if (navigation is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.J or ConsoleKey.K)
                {
                    var up = navigation is ConsoleKey.UpArrow or ConsoleKey.K;
                    var moved = FileFavorites.Items[selected];
                    // With a count: that many steps (each can go into or out of a folder)
                    var steps = 0;
                    while (steps < (count ?? 1) && FileFavorites.Move(FileFavorites.IndexOf(moved), up)) steps++;
                    if (steps > 0)
                    {
                        // The selection follows it (a folder it has moved into is opened)
                        FilterFavorites();
                        _favoriteIndex = Math.Max(0,
                            _favorites.FindIndex(number => ReferenceEquals(FileFavorites.Items[number], moved)));
                        SaveFavoriteList();
                    }
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.V)
                {
                    _favoriteMoving = false;
                }

                continue;
            }

            if (key.KeyChar == ':')
            {
                var command = CommandLine.Read(CommandContext.Favorites);
                if (command is "jump" or "ju" or "jmp")
                {
                    // The Jump menu
                    Jump.Open();
                    FindFavoriteMarks();
                    continue;
                }

                if (command != null && CommandLine.Name(command) == "book")
                {
                    // ":book 3": jump to bookmark 3
                    Jump.ToBookmark(CommandLine.Argument(command));
                    FindFavoriteMarks();
                    continue;
                }

                // A name can follow the command
                if (command != null && CommandLine.Name(command) is "folder" or "subfolder" or "rename" or "new" or "import")
                {
                    command = CommandLine.Name(command);
                }

                switch (command)
                {
                    case null:
                        break;
                    case "q":
                    case "quit":
                    case "close":
                        return FavoriteAction.Close;
                    case "h":
                    case "help":
                        Tools.WriteEditorHelp(Tools.HelpTopic.Favorites);
                        break;
                    case "color":
                        PickFavoriteColor(selected);
                        break;
                    case "info":
                        ShowFavoriteInfo(selected);
                        break;
                    case "remove":
                        RemoveFavorite(selected);
                        break;
                    case "move":
                        MoveFavoritesToFolder(selected);
                        break;
                    case "top":
                    case "bottom":
                        MoveFavoritesToEnd(selected, command == "top");
                        break;
                    case "u":
                    case "undo":
                        UndoRemoveFavorite();
                        break;
                    case "folder":
                        NewFavoriteFolder(selected, CommandLine.LastArgument);
                        break;
                    case "subfolder":
                        NewFavoriteSubfolder(selected, CommandLine.LastArgument);
                        break;
                    case "note":
                        EditFavoriteNote(selected);
                        break;
                    case "new":
                        if (NewFavoriteFile(selected, CommandLine.LastArgument) is not { } made) break;
                        path = made;
                        return FavoriteAction.Open;
                    case "panes":
                    case "peek":
                        ToggleFavoritePanes(command == "peek");
                        break;
                    case "number":
                    case "nonumber":
                        // The numbers of the rows on or off (kept in the settings)
                        FavoriteNumbers = command == "number";
                        _favoriteMessage = FavoriteNumbers ? DialogText.LineNumbersOn : DialogText.LineNumbersOff;
                        FileIo.SaveSettings();
                        break;
                    case "expand":
                    case "collapse":
                        SetFavoriteFolders(selected, command == "collapse");
                        break;
                    case "rename":
                        RenameFavoriteFolder(selected, CommandLine.LastArgument);
                        break;
                    case "name":
                        // The name shown: of the file or directory, or of the folder
                        if (selected < 0) break;
                        _favoriteMessage = CopyOfList(FileFavorites.Items[selected].IsFolder
                            ? FileFavorites.Items[selected].Name
                            : FavoriteName(FileFavorites.Items[selected].Path));
                        break;
                    case "path":
                        if (selected < 0) break;
                        _favoriteMessage = FileFavorites.Items[selected].IsFolder
                            ? DialogText.FavoriteFolderNoPath
                            : CopyOfList(FileFavorites.Items[selected].Path);
                        break;
                    case "import":
                        ImportBookmarks(CommandLine.LastArgument);
                        break;
                    case "goto":
                        if (selected < 0 || FileFavorites.Items[selected].IsFolder) break;
                        path = FileFavorites.Items[selected].Path;
                        return FavoriteAction.GoTo;
                    default:
                        CommandLine.ShowUnknown(command);
                        break;
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(Tools.HelpTopic.Favorites);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the paths containing a text
                _favoriteFilter = CommandLine.Read('/', false) ?? string.Empty;
                _favoriteIndex = 0;
            }
            else if (key.KeyChar == 'G')
            {
                _favoriteIndex = ListCount.Row(key.KeyChar == 'G' ? count : null, _favorites.Count, false);
                redraw = hadMessage;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _favoriteIndex = ListCount.Row(count, _favorites.Count, true);
                redraw = hadMessage;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.F or ConsoleKey.Backspace ||
                     navigation == ConsoleKey.LeftArrow || key.KeyChar == '-')
            {
                // Esc removes the marks, and then a filter, before it closes the list
                if (key.Key == ConsoleKey.Escape && _favoriteMarks.Count > 0)
                {
                    _favoriteMarks.Clear();
                }
                else if (key.Key == ConsoleKey.Escape && _favoriteFilter.Length > 0)
                {
                    _favoriteFilter = string.Empty;
                }
                else
                {
                    return FavoriteAction.Close;
                }
            }
            else if (navigation == ConsoleKey.DownArrow)
            {
                _favoriteIndex += count ?? 1;
                redraw = hadMessage;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _favoriteIndex = Math.Max(0, _favoriteIndex - (count ?? 1));
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageDown)
            {
                _favoriteIndex += FavoritePageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageUp)
            {
                _favoriteIndex -= FavoritePageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                _favoriteIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.End)
            {
                _favoriteIndex = _favorites.Count - 1;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.A)
            {
                NewFavoriteFolder(selected, string.Empty);
            }
            else if (key.Key == ConsoleKey.S)
            {
                NewFavoriteSubfolder(selected, string.Empty);
            }
            else if (key.Key == ConsoleKey.W &&
                     (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                if (NewFavoriteFile(selected, string.Empty) is not { } made) continue;
                path = made;
                return FavoriteAction.Open;
            }
            else if (key.Key == ConsoleKey.U &&
                     (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                UndoRemoveFavorite();
            }
            else if (selected < 0)
            {
                // The other keys need a favorite to work on
            }
            else if (key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow)
            {
                if (FileFavorites.Items[selected].IsFolder)
                {
                    // Open or close the folder
                    FileFavorites.Items[selected].Collapsed = !FileFavorites.Items[selected].Collapsed;
                    SaveFavoriteList();
                    continue;
                }

                path = FileFavorites.Items[selected].Path;
                return FavoriteAction.Open;
            }
            else if (key.Key == ConsoleKey.X)
            {
                RenameFavoriteFolder(selected, string.Empty);
            }
            else if (key.Key == ConsoleKey.B)
            {
                if (FileFavorites.Items[selected].IsFolder) continue;
                path = FileFavorites.Items[selected].Path;
                return FavoriteAction.GoTo;
            }
            else if (key.Key == ConsoleKey.V)
            {
                if (_favoriteFilter.Length > 0)
                {
                    // The order is that of the whole list
                    _favoriteMessage = DialogText.FavoriteMoveFiltered;
                }
                else
                {
                    _favoriteMoving = true;
                }
            }
            else if (key.Key == ConsoleKey.Spacebar)
            {
                // Mark (or unmark) the favorite and go to the next
                var item = FileFavorites.Items[selected];
                if (item.IsFolder)
                {
                    _favoriteMessage = DialogText.FavoriteMarkFolder;
                }
                else
                {
                    if (!_favoriteMarks.Remove(item)) _favoriteMarks.Add(item);
                    if (_favoriteIndex < _favorites.Count - 1) _favoriteIndex++;
                }
            }
            else if (key.Key == ConsoleKey.M)
            {
                MoveFavoritesToFolder(selected);
            }
            else if (key.Key is ConsoleKey.T or ConsoleKey.E)
            {
                MoveFavoritesToEnd(selected, key.Key == ConsoleKey.T);
            }
            else if (key.Key is ConsoleKey.P or ConsoleKey.D)
            {
                RemoveFavorite(selected);
            }
            else if (key.Key == ConsoleKey.C)
            {
                PickFavoriteColor(selected);
            }
            else if (key.Key == ConsoleKey.I)
            {
                ShowFavoriteInfo(selected);
            }
            else if (key.Key == ConsoleKey.N)
            {
                EditFavoriteNote(selected);
            }
        }
    }

    // Open or close all the folders
    private static void SetFavoriteFolders(int selected, bool collapsed)
    {
        // The folder of the selected favorite is selected when the favorite is no longer shown
        var keep = selected < 0 ? null : FileFavorites.Items[collapsed ? FileFavorites.FolderOf(selected) : selected];
        if (FileFavorites.SetAllCollapsed(collapsed) == 0)
        {
            _favoriteMessage = DialogText.FavoritesNoFolders;
            return;
        }

        SaveFavoriteList();
        FilterFavorites();
        if (keep != null)
        {
            _favoriteIndex = Math.Max(0,
                _favorites.FindIndex(number => ReferenceEquals(FileFavorites.Items[number], keep)));
        }

        _favoriteMessage = collapsed ? DialogText.FavoritesCollapsed : DialogText.FavoritesExpanded;
    }

    private static void SaveFavoriteList()
    {
        var result = FileFavorites.Save();
        if (result != DialogText.Success) _favoriteMessage = result;
    }

    // ":import bookmarks": the bookmarks of the Jump menu copied into the favorites, after asking with what it will
    // do (the bookmarks stay; places that are favorites already are left out, folders of the same name merged, and
    // the bookmarks of pages of Blyant left out, as a favorite is a file or a directory)
    private static void ImportBookmarks(string argument)
    {
        if (!argument.Trim().Equals("bookmarks", StringComparison.OrdinalIgnoreCase))
        {
            _favoriteMessage = Lang.T(":import bookmarks  copies the bookmarks of the Jump menu here");
            return;
        }

        Bookmarks.Load();
        var bookmarks = Bookmarks.Items;
        if (!bookmarks.Any(bookmark => !bookmark.IsFolder))
        {
            _favoriteMessage = Lang.T("There are no bookmarks to copy");
            return;
        }

        var plan = FileFavorites.ImportBookmarks(bookmarks, false);
        if (plan.Added == 0 && plan.Notes == 0)
        {
            _favoriteMessage = plan.Skipped == 0
                ? Lang.T("Only bookmarks of pages: a favorite is a file or a directory")
                : Lang.T("All the bookmarks are favorites already");
            return;
        }

        ChoiceBox.Forget();
        // What will be done, a sentence for each that there is
        var sentences = new List<string>();
        if (plan.Added > 0) sentences.Add(Lang.F("{0} files and directories are added.", plan.Added));
        if (plan.NewFolders > 0) sentences.Add(Lang.F("New folders: {0}.", plan.NewFolders));
        if (plan.MergedFolders > 0) sentences.Add(Lang.F("Added to folders of the same name: {0}.", plan.MergedFolders));
        if (plan.Skipped > 0) sentences.Add(Lang.F("{0} are favorites already, and are left out.", plan.Skipped));
        if (plan.LeftOut > 0) sentences.Add(Lang.F("Bookmarks of pages left out: {0}.", plan.LeftOut));
        if (plan.Notes > 0) sentences.Add(Lang.F("Notes copied: {0} (a note a favorite has is kept).", plan.Notes));
        // A favorite shows the name of its file
        if (bookmarks.Any(bookmark => !bookmark.IsFolder && bookmark.Name.Length > 0 &&
                                      bookmark.Target.Kind is JumpKind.File or JumpKind.Directory &&
                                      !FileFavorites.Contains(bookmark.Target.Target)))
        {
            sentences.Add(Lang.T("The names given to bookmarks are not copied."));
        }

        var note = string.Join(" ", sentences);
        if (!ChoiceBox.Confirm(DialogText.CaptionsFileFavorites, Lang.T("Copy the bookmarks to the favorites?"), note,
                false))
        {
            return;
        }

        var done = FileFavorites.ImportBookmarks(bookmarks, true);
        SaveFavoriteList();
        EventLog.Add(DialogText.LogFile + "Bookmarks copied to the favorites: " + done.Added + " added, " +
                     done.Skipped + " left out, " + done.LeftOut + " pages");
        if (_favoriteMessage.Length == 0) _favoriteMessage = Lang.F("{0} bookmarks copied to the favorites", done.Added);
    }

    // Ask for the name of a new folder (if it is not given) and add it in front of the selected item
    private static void NewFavoriteFolder(int selected, string name)
    {
        if (_favoriteFilter.Length > 0)
        {
            _favoriteMessage = DialogText.FavoriteMoveFiltered;
            return;
        }

        if (name.Length == 0)
        {
            var asked = AskName(DialogText.FavoriteNewFolder, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var index = FileFavorites.AddFolder(name, selected);
        if (index < 0)
        {
            _favoriteMessage = DialogText.FavoriteFolderName;
            return;
        }

        FilterFavorites();
        _favoriteIndex = Math.Max(0, _favorites.IndexOf(index));
        SaveFavoriteList();
    }

    // W or ":new name": a new file, made with the box of the start page (the name, with ":new name" filled in, and
    // Tab for another folder), in the folder of the selected favorite to start with. It becomes a favorite below the
    // selected item (first in a folder that is selected) and is opened in the editor, and the list comes back after.
    // Returns the path of the file, or null
    private static string? NewFavoriteFile(int selected, string name)
    {
        ChoiceBox.Forget();
        var path = StartMenu.AskNewFile(DialogText.NewFile, name, FavoriteFolderForNewFile(selected));
        Tools.ClearScreen();
        if (path == null) return null;
        try
        {
            // Only a new file: one that was made in the meantime is not emptied (it is logged, or why it failed)
            FileIo.CreateNewFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _favoriteMessage = e.Message;
            return null;
        }

        // Shown and selected, also when the list was filtered
        var index = FileFavorites.AddBelow(path, selected);
        _favoriteFilter = string.Empty;
        _favoriteMarks.Clear();
        SaveFavoriteList();
        FilterFavorites();
        if (index >= 0) _favoriteIndex = Math.Max(0, _favorites.IndexOf(index));
        return path;
    }

    /// <summary>
    /// The folder a new file made from the favorites starts in: the directory of the selected file, the selected
    /// directory, or for a folder of favorites the one of the first file or directory in it (null: none, or it is not
    /// there; the folder the file browser was in last is used)
    /// </summary>
    internal static string? FavoriteFolderForNewFile(int selected)
    {
        if (selected < 0 || selected >= FileFavorites.Items.Count) return null;
        var item = FileFavorites.Items[selected];
        if (item.IsFolder)
        {
            item = FileFavorites.InFolder(selected).FirstOrDefault(favorite => !favorite.IsFolder);
            if (item == null) return null;
        }

        if (Directory.Exists(item.Path)) return item.Path;
        var directory = System.IO.Path.GetDirectoryName(item.Path);
        return File.Exists(item.Path) && directory != null && Directory.Exists(directory) ? directory : null;
    }

    // S or ":subfolder name": ask for the name of a new subfolder (if it is not given) and add it to the folder of the
    // selected item: first in it when the folder is selected, else in front of the item (or of its subfolder)
    private static void NewFavoriteSubfolder(int selected, string name)
    {
        if (_favoriteFilter.Length > 0)
        {
            _favoriteMessage = DialogText.FavoriteMoveFiltered;
            return;
        }

        if (selected < 0 || FileFavorites.Items[selected] is { IsFolder: false, Level: 0 })
        {
            _favoriteMessage = Lang.T("Select a folder (or a favorite in one) to add a subfolder to it");
            return;
        }

        if (name.Length == 0)
        {
            var asked = AskName(Lang.T("New subfolder"), string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var index = FileFavorites.AddSubfolder(name, selected);
        if (index < 0)
        {
            _favoriteMessage = DialogText.FavoriteFolderName;
            return;
        }

        FilterFavorites();
        _favoriteIndex = Math.Max(0, _favorites.IndexOf(index));
        SaveFavoriteList();
    }

    private static void RenameFavoriteFolder(int number, string name)
    {
        if (number < 0) return;
        if (!FileFavorites.Items[number].IsFolder)
        {
            // A file is renamed where it is
            _favoriteMessage = DialogText.FavoriteRenameFile;
            return;
        }

        if (name.Length == 0)
        {
            var asked = AskName(DialogText.FavoriteRenameFolder, FileFavorites.Items[number].Name);
            if (asked == null) return;
            name = asked;
        }

        if (FileFavorites.RenameFolder(number, name))
        {
            SaveFavoriteList();
        }
        else
        {
            _favoriteMessage = DialogText.FavoriteFolderName;
        }
    }

    // The favorites the user has marked (to move to a folder, or remove, in one go)
    private static readonly HashSet<FileFavorites.Favorite> _favoriteMarks = new();

    // The list was read again (by the Jump menu): the marked favorites are found in it by their paths
    private static void FindFavoriteMarks()
    {
        if (_favoriteMarks.All(mark => FileFavorites.Items.Contains(mark))) return;
        var paths = _favoriteMarks.Select(mark => mark.Path).ToList();
        _favoriteMarks.Clear();
        foreach (var item in FileFavorites.Items)
        {
            if (!item.IsFolder && paths.Exists(path => FileOperations.SamePath(path, item.Path))) _favoriteMarks.Add(item);
        }
    }

    // The favorites a key works on: the marked ones, or the selected one if none are marked
    private static List<FileFavorites.Favorite> MarkedOrSelected(int selected)
    {
        if (_favoriteMarks.Count > 0)
        {
            return FileFavorites.Items.Where(item => _favoriteMarks.Contains(item)).ToList();
        }

        return selected >= 0 && !FileFavorites.Items[selected].IsFolder
            ? new List<FileFavorites.Favorite> { FileFavorites.Items[selected] }
            : new List<FileFavorites.Favorite>();
    }

    // Move the marked favorites (or the selected one) to a folder picked from a list: one of the folders,
    // a new folder, or out of the folders
    private static void MoveFavoritesToFolder(int selected)
    {
        var items = MarkedOrSelected(selected);
        if (items.Count == 0)
        {
            _favoriteMessage = DialogText.FavoriteMarkFolder;
            return;
        }

        var folders = FileFavorites.Items.Where(item => item.IsFolder).ToList();
        var choices = new List<string> { DialogText.FavoriteNewFolder + "...", DialogText.FavoriteNoFolder };
        // A subfolder below its folder, further in
        choices.AddRange(folders.Select(folder => new string(' ', 3 * folder.Level) + "[" + folder.Name + "]"));
        var caption = items.Count == 1
            ? DialogText.FavoriteMoveOne
            : DialogText.FavoriteMoveMany.Replace("{0}", items.Count.ToString());
        var choice = PickFromList(caption, choices, folders.Count > 0 ? 2 : 0);
        if (choice < 0) return;
        FileFavorites.Favorite? folder = null;
        if (choice == 0)
        {
            // A new folder, where the first of the favorites is
            var name = AskName(DialogText.FavoriteNewFolder, string.Empty);
            if (name == null) return;
            var first = -1;
            for (var i = 0; i < FileFavorites.Items.Count && first < 0; i++)
            {
                if (ReferenceEquals(FileFavorites.Items[i], items[0])) first = i;
            }

            var index = FileFavorites.AddFolder(name, first);
            if (index < 0)
            {
                _favoriteMessage = DialogText.FavoriteFolderName;
                return;
            }

            folder = FileFavorites.Items[index];
        }
        else if (choice >= 2)
        {
            folder = folders[choice - 2];
        }

        if (FileFavorites.MoveToFolder(items, folder) < 0) return;
        _favoriteMarks.Clear();
        // Show them, also when the list was filtered
        _favoriteFilter = string.Empty;
        FilterFavorites();
        _favoriteIndex = Math.Max(0,
            _favorites.FindIndex(number => ReferenceEquals(FileFavorites.Items[number], folder ?? items[0])));
        _favoriteMessage = (items.Count == 1 ? DialogText.FavoriteMovedOne
                               : DialogText.FavoriteMovedMany.Replace("{0}", items.Count.ToString())) +
                           (folder == null ? DialogText.FavoriteNoFolder : folder.Name);
        SaveFavoriteList();
    }

    // Move the marked favorites (or the selected favorite or folder) to the top or the end of the list (a
    // favorite in a folder: of the folder)
    private static void MoveFavoritesToEnd(int selected, bool top)
    {
        var items = MarkedOrSelected(selected);
        if (items.Count == 0 && selected >= 0) items.Add(FileFavorites.Items[selected]);
        if (items.Count == 0) return;
        FileFavorites.MoveToEnd(items, top);
        _favoriteMarks.Clear();
        // The first of them is selected
        FilterFavorites();
        _favoriteIndex = Math.Max(0,
            _favorites.FindIndex(number => ReferenceEquals(FileFavorites.Items[number], items[0])));
        _favoriteMessage = top ? Lang.T("Moved to the top") : Lang.T("Moved to the end");
        SaveFavoriteList();
    }

    // A list of choices in a box on top of the page (also the folders of the bookmarks, in the Jump menu). Returns the
    // number of the one picked, or -1
    internal static int PickFromList(string caption, List<string> choices, int selected)
    {
        const int top = 3; // The tab of the title is on the row above
        var rows = Math.Min(choices.Count, Init.LineCount - 6);
        var inner = Math.Min(Init.LineWidth - 6,
            Math.Max(caption.Length + 4, choices.Max(choice => choice.Length) + 6));
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        var first = 0;
        Tools.HideCursor();
        while (true)
        {
            // The list scrolls if there are more folders than rows
            if (selected < first) first = selected;
            if (selected >= first + rows) first = selected - rows + 1;
            ChoiceBox.DrawTitleTab(top - 1, left, inner, caption);
            for (var row = 0; row < rows; row++)
            {
                var number = first + row;
                Tools.SetCursorEx(top + 1 + row, left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
                Console.Write("| ");
                if (number == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(number == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" " + Tools.TruncateString(choices[number], inner - 5, true).PadRight(inner - 4));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
                Console.Write('|');
            }

            Tools.SetCursorEx(top + 1 + rows, left);
            Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return -1;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L) return selected;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow) selected = (selected + 1) % choices.Count;
            if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + choices.Count - 1) % choices.Count;
            }

            if (key.KeyChar == 'G' || key.Key == ConsoleKey.End) selected = choices.Count - 1;
            if (key.KeyChar == 'g' || key.Key == ConsoleKey.Home) selected = 0;
        }
    }

    // D, P or ":remove": the marked favorites, or the selected favorite, or a folder with what is in it, after asking
    // (u puts them back)
    private static void RemoveFavorite(int number)
    {
        if (_favoriteMarks.Count > 0)
        {
            // All the marked favorites
            var count = _favoriteMarks.Count;
            var notes = _favoriteMarks.Count(item => item.Note.Length > 0);
            if (!ChoiceBox.Confirm(DialogText.FavoriteRemoveCaption,
                    count == 1
                        ? Lang.T("Remove the marked favorite from the list?")
                        : DialogText.FavoriteRemoveMany.Replace("{0}", count.ToString()),
                    Lang.T("The files stay where they are.") +
                    (notes > 0 ? " " + Lang.T("Their notes are removed with them.") : ""),
                    false))
            {
                return;
            }

            count = FileFavorites.RemoveMany(_favoriteMarks);
            _favoriteMarks.Clear();
            _favoriteMessage = DialogText.FavoriteRemovedMany.Replace("{0}", count.ToString());
            SaveFavoriteList();
            return;
        }

        if (number < 0) return;
        var item = FileFavorites.Items[number];
        if (item.IsFolder)
        {
            // The favorites and subfolders in a folder are removed with it
            var count = FileFavorites.FolderCount(number);
            var subfolders = FileFavorites.SubfolderCount(number);
            var note = subfolders > 0
                ? Lang.F("In it: {0} favorites and {1} subfolders, removed from the list too (the files stay).",
                    count, subfolders)
                : count == 0
                    ? Lang.T("The folder is empty.")
                    : count == 1
                        ? Lang.T("The favorite in it is removed from the list too (the file stays).")
                        : Lang.F("The {0} favorites in it are removed from the list too (the files stay).", count);
            if (item.Note.Length > 0) note += " " + Lang.T("Its note is removed with it.");
            if (!ChoiceBox.Confirm(item.Level > 0 ? Lang.T("Remove subfolder") : DialogText.FavoriteDeleteFolder,
                    Lang.F("Remove \"{0}\" from the favorites?", Tools.TruncateString(item.Name, 40, true)), note,
                    false))
            {
                return;
            }

            FileFavorites.RemoveAt(number);
            _favoriteMessage = DialogText.FavoriteFolderRemoved + item.Name;
            SaveFavoriteList();
            return;
        }

        var name = System.IO.Path.GetFileName(FileFavorites.Items[number].Path);
        if (!ChoiceBox.Confirm(Lang.T("Remove favorite"),
                Lang.F("Remove \"{0}\" from the favorites?", Tools.TruncateString(FavoriteName(item.Path), 40, true)),
                Lang.T("The file stays where it is.") +
                (item.Note.Length > 0 ? " " + Lang.T("Its note is removed with it.") : ""),
                false))
        {
            return;
        }

        FileFavorites.RemoveAt(number);
        _favoriteMessage = DialogText.FavoriteRemoved + ": " + name;
        SaveFavoriteList();
    }

    // Put back the favorite, or the folder with its favorites, that was removed last
    private static void UndoRemoveFavorite()
    {
        var index = FileFavorites.UndoRemove();
        if (index < 0)
        {
            _favoriteMessage = DialogText.FavoriteNothingToUndo;
            return;
        }

        // Show it, also when the list is filtered or it is in a closed folder
        FileFavorites.Reveal(index);
        _favoriteFilter = string.Empty;
        FilterFavorites();
        _favoriteIndex = Math.Max(0, _favorites.IndexOf(index));
        _favoriteMessage = DialogText.FavoriteRestored;
        SaveFavoriteList();
    }

    // Show information about the file or directory of a favorite
    private static void ShowFavoriteInfo(int number)
    {
        if (number < 0 || FileFavorites.Items[number].IsFolder) return;
        var path = FileFavorites.Items[number].Path;
        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            _favoriteMessage = DialogText.FileNotFound;
            return;
        }

        _message = string.Empty;
        ShowInfo(new Item(FavoriteName(path), path, isDirectory, isDirectory ? 0 : new FileInfo(path).Length));
        _favoriteMessage = _message;
        _message = string.Empty;
    }

    private static void PickFavoriteColor(int number)
    {
        if (number < 0) return;
        if (ColorPicker.Pick(DialogText.ColorCaption, FavoritePickerLeft, true, FileFavorites.Items[number].Color,
                null, null, out var color))
        {
            FileFavorites.SetColor(number, color);
            SaveFavoriteList();
        }
    }

    // The columns of the lines between the columns of the list (none when it is empty)
    private static int[] _favoriteLines = Array.Empty<int>();

    private static int _shownFavoriteTop = -1;
    private static int _shownFavoriteIndex = -1;

    private static void DrawFavorites(bool all)
    {
        if (_favoriteIndex < _favoriteTop) _favoriteTop = _favoriteIndex;
        if (_favoriteIndex >= _favoriteTop + FavoritePageSize) _favoriteTop = _favoriteIndex - FavoritePageSize + 1;
        _favoriteTop = Math.Max(0, _favoriteTop);

        Tools.HideCursor();
        if (!all && _favoriteTop == _shownFavoriteTop)
        {
            // Move the selection pointer (the panes show the item selected)
            DrawFavoritePointer(_shownFavoriteIndex, false);
            DrawFavoritePointer(_favoriteIndex, true);
            if (_favoriteIndex != _shownFavoriteIndex) DrawFavoritePanes();
            _shownFavoriteIndex = _favoriteIndex;
            DrawFavoriteStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsFileFavorites, string.Empty, 0);
        Menu.WriteNameInTopBar();
        Tools.SetCursorEx(1, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(_favorites.Count == 0 && _favoriteFilter.Length == 0
            ? DialogText.FavoritesEmpty
            : DialogText.FavoritesHeader);
        // The columns: the names, the directories they are in, and what they are (size, directory, number of
        // favorites in a folder), with lines between them and their labels in the divider line
        var nameWidth = 0;
        foreach (var number in _favorites)
        {
            nameWidth = Math.Max(nameWidth, FavoriteLabel(number).Length);
        }

        nameWidth = Math.Min(nameWidth, Init.LineWidth / 2 - 6 - FavoriteNumberWidth);
        var infos = _favorites.Select(FavoriteInfo).ToList();
        var infoWidth = Math.Max(TextMetrics.WidthOf(DialogText.ColumnInfo) + 2,
            infos.Count == 0 ? 0 : infos.Max(info => TextMetrics.WidthOf(info.Text)));
        // The lines between the columns: after the name, and before the info
        var nameLine = 3 + FavoriteNumberWidth + nameWidth + 2;
        var infoLine = Init.LineWidth - 2 - infoWidth - 1;
        var columns = _favorites.Count > 0;
        _favoriteLines = columns ? new[] { nameLine, infoLine } : Array.Empty<int>();
        Tools.SetCursorEx(2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        if (columns)
        {
            DrawColumnLabels(2, new[] { (1 + FavoriteNumberWidth, DialogText.ColumnName), (nameLine, DialogText.ColumnPath), (infoLine, DialogText.ColumnInfo) },
                new[] { nameLine, infoLine });
            for (var row = 0; row < FavoritePageSize; row++)
            {
                DrawColumnLine(ListTop + row, nameLine);
                DrawColumnLine(ListTop + row, infoLine);
            }
        }

        for (var row = 0; row < FavoritePageSize && _favoriteTop + row < _favorites.Count; row++)
        {
            var index = _favoriteTop + row;
            var favorite = FileFavorites.Items[_favorites[index]];
            var (info, isDirectory, exists) = infos[index];
            DrawFavoritePointer(index, index == _favoriteIndex);
            var name = Tools.TruncateString(FavoriteLabel(_favorites[index]) + (isDirectory ? "/" : ""),
                nameWidth + 1, true);
            var moving = _favoriteMoving && index == _favoriteIndex;
            if (moving)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            }
            else if (favorite.Color != null)
            {
                ConsoleColors.SetTextColor(favorite.Color.Value);
            }
            else
            {
                ConsoleColors.SetColors(isDirectory || favorite.IsFolder
                    ? ConsoleColors.ColorPreset.HeaderColor
                    : ConsoleColors.ColorPreset.Normal);
            }

            if (_favoriteMarks.Contains(favorite))
            {
                // Marked
                var colors = (Console.ForegroundColor, Console.BackgroundColor);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write("+");
                ConsoleColors.SetForeground(colors.ForegroundColor);
                ConsoleColors.SetBackground(colors.BackgroundColor);
                Console.Write(name);
            }
            else
            {
                Console.Write(" " + name);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            // The directory it is in (the end of a long path is shown), between the lines
            var directory = favorite.IsFolder
                ? string.Empty
                : System.IO.Path.GetDirectoryName(favorite.Path) ?? string.Empty;
            var column = nameLine + 2;
            var room = infoLine - 1 - column;
            if (room > 8)
            {
                directory = TextMetrics.KeepEnd(directory, room);
                Tools.SetCursorEx(ListTop + row, column);
                Console.Write(directory);
            }

            // What it is, at the right (sizes line up)
            if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Tools.SetCursorEx(ListTop + row, Init.LineWidth - TextMetrics.WidthOf(info) - 1);
            Console.Write(info);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        _shownFavoriteTop = _favoriteTop;
        _shownFavoriteIndex = _favoriteIndex;
        DrawFavoritePanes();
        DrawFavoriteStatus();
    }

    // What the info column shows for an item of the list: the number of favorites in a folder, that a file is
    // missing, that it is a directory, or the size of the file. And whether it is a directory, and is there
    private static (string Text, bool IsDirectory, bool Exists) FavoriteInfo(int number)
    {
        var favorite = FileFavorites.Items[number];
        if (favorite.IsFolder)
        {
            var count = FileFavorites.FolderCount(number);
            return (count == 1 ? Lang.T("1 favorite") : Lang.F("{0} favorites", count), false, true);
        }

        var isDirectory = Directory.Exists(favorite.Path);
        var exists = isDirectory || File.Exists(favorite.Path);
        if (!exists) return (DialogText.FavoriteMissing, false, false);
        if (isDirectory) return (DialogText.DirectoryMark, true, true);
        try
        {
            return (FileSizeFormatter.FormatSize(new FileInfo(favorite.Path).Length), false, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (DialogText.FavoriteMissing, false, false);
        }
    }

    /// <summary>
    /// The labels of the columns of a list, in its divider line (a row of "-"), with a "+" where a line between
    /// columns meets it. Each label starts at a column (a line, or the left), a blank space in
    /// </summary>
    internal static void DrawColumnLabels(int row, IReadOnlyList<(int Column, string Label)> labels,
        IReadOnlyList<int> lines)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        foreach (var line in lines)
        {
            Tools.SetCursorEx(row, line);
            Console.Write("+");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        foreach (var (column, label) in labels)
        {
            Tools.SetCursorEx(row, column + 2);
            Console.Write(" " + label + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// Where the lines between the columns of a list end: a "+" in a divider line
    /// </summary>
    internal static void DrawColumnEnds(int row, IReadOnlyList<int> lines)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        foreach (var line in lines)
        {
            Tools.SetCursorEx(row, line);
            Console.Write("+");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// A line between two columns of a list, on a row
    /// </summary>
    internal static void DrawColumnLine(int row, int column)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(row, column);
        Console.Write("|");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // What is shown for an item of the list: the name of the file, or the name of a folder with a mark that tells if
    // it is open, two blank spaces in for each folder it is in
    private static string FavoriteLabel(int number)
    {
        var item = FileFavorites.Items[number];
        // The folders are not shown when the list is filtered
        var indent = _favoriteFilter.Length == 0 ? new string(' ', 2 * item.Level) : string.Empty;
        if (item.IsFolder) return indent + (item.Collapsed ? "[+] " : "[-] ") + item.Name;
        return indent + FavoriteName(item.Path);
    }

    private static string FavoriteName(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        return name.Length > 0 ? name : path;
    }

    // The width of the numbers of the rows (when they are on): the largest number, and a blank space
    private static int FavoriteNumberWidth => ListNumbers.Width(FavoriteNumbers, _favorites.Count);

    // The selection pointer of a row (and before it the number of the row, when the numbers are on)
    private static void DrawFavoritePointer(int index, bool selected)
    {
        var row = index - _favoriteTop;
        if (row < 0 || row >= FavoritePageSize || index >= _favorites.Count) return;
        Tools.SetCursorEx(ListTop + row, 0);
        ListNumbers.Write(index, FavoriteNumberWidth, selected);

        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.SelectorInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(selected ? (_favoriteMoving ? "<>" : "->") : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected favorite, and a message or the keys
    private static void DrawFavoriteStatus()
    {
        Tools.SetCursorEx(Init.LineCount - 2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        // Where the lines between the columns end
        DrawColumnEnds(Init.LineCount - 2, FavoriteBottomLines);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);

        var status = " " + (_favorites.Count == 0 ? 0 : _favoriteIndex + 1) + "/" + _favorites.Count + " ";
        if (_listCount.Shown.Length > 0) status = " " + _listCount.Shown + status;
        if (_favoriteFilter.Length > 0) status += "/" + _favoriteFilter + " ";
        if (_favoriteMarks.Count > 0) status = " " + Lang.F("{0} marked", _favoriteMarks.Count) + " -" + status;
        Tools.SetCursorEx(Init.LineCount - 2, Init.LineWidth - status.Length - 2);
        Console.Write(status);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_favoriteMessage.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_favoriteMessage, Init.LineWidth - 4, true) + " ");
        }
        else if (_favoriteMoving)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(" " + DialogText.Moving + " ");
        }
        else if (_favoriteMarks.Count > 0)
        {
            // What can be done with the marked favorites
            Menu.WriteKeyHints(DialogText.FavoritesMarkedKeys);
        }
        else
        {
            Menu.WriteKeyHints(DialogText.FavoritesKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
