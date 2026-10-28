namespace Blyant;

/// <summary>
/// The file browser, opened from the start page (File browser, Open project) and the Jump menu. It lists the
/// directories and files of a directory in one or two panes, and opens text files in the editor.
/// File management is found in FileExplorer.Manage.cs, the list of favorites in FileExplorer.Favorites.cs,
/// the files opened before in FileExplorer.History.cs and the search for files in FileExplorer.Search.cs.
/// </summary>
internal static partial class FileExplorer
{
    private const int ListTop = 3; // The first row of the list

    // A count typed in front of a key in the file browser and its lists (favorites, history, hotlist, search results)
    private static readonly ListCount _listCount = new();
    private const string ParentName = "..";
    private const string CopyNameSuffix = "-utf8";

    private sealed record Item(string Name, string Path, bool IsDirectory, long Size);

    /// <summary>
    /// The directories shown in the two panes when the browser is opened (settings "browsedir1" and
    /// "browsedir2"). Empty = the home directory (HomeDirectory)
    /// </summary>
    internal static string StartDirectory { get; set; } = string.Empty;

    internal static string StartDirectory2 { get; set; } = string.Empty;

    /// <summary>
    /// The names of the files or directories selected in the two panes when the browser was closed (settings
    /// "browsesel1" and "browsesel2"): selected again when it is opened. Empty, or a name that is gone: the first row
    /// </summary>
    internal static string StartSelected { get; set; } = string.Empty;

    internal static string StartSelected2 { get; set; } = string.Empty;

    /// <summary>
    /// The pane that has the focus when the browser is opened: 0 or 1 (setting "browsepane")
    /// </summary>
    internal static int StartPane { get; set; }

    /// <summary>
    /// True to show two panes side by side, false to show one (setting "browsedual")
    /// </summary>
    internal static bool DualPane { get; set; } = true;

    /// <summary>
    /// The rows of the panes are numbered, as 12gg counts them ("..": 1). Settings "filenumbers", "historynumbers",
    /// "hotlistnumbers" and "searchnumbers" for the panes, the history, the hotlist and the search results (the
    /// history and the hotlist also in the Jump menu), off unless turned on
    /// </summary>
    internal static bool FileNumbers { get; set; }

    internal static bool HistoryNumbers { get; set; }
    internal static bool HotlistNumbers { get; set; }
    internal static bool SearchNumbers { get; set; }

    // A list of the content of a directory
    private sealed class Pane
    {
        internal string Directory = string.Empty; // Empty = the list of drives (Windows)
        internal List<Item> Items = new();
        internal int Index; // The selected item
        internal int Top; // The first item shown
        internal string Filter = string.Empty;
        internal int ShownTop = -1; // What is on screen: the first item shown and the item selected
        internal int ShownIndex = -1;
        internal readonly HashSet<string> Marks = new(); // The paths of the marked files and directories
        internal string MarksDirectory = string.Empty; // The directory the marks were made in
    }

    // The two panes of the browser, and the one used when a file or directory is picked for something else
    // (always a single pane)
    private static readonly Pane[] Panes = { new(), new() };
    private static readonly Pane PickPane = new();
    private static int _active; // The pane with the focus

    private static bool IsDual => _mode == BrowseMode.Edit && DualPane;

    // The pane the keys work on
    private static Pane ActivePane => _mode == BrowseMode.Edit ? Panes[_active] : PickPane;

    // The other pane (when two are shown)
    private static Pane OtherPane => Panes[1 - _active];

    private static string _message = string.Empty;
    private static TextFile? _file; // The file being edited

    private static int PageSize => Init.LineCount - ListTop - 2;

    private enum BrowseMode
    {
        Edit, // Open text files in the editor
        PickFile, // Select a file for the caller
        PickDirectory // Select a directory for the caller (files are not shown)
    }

    private static BrowseMode _mode;
    private static string _caption = string.Empty;
    private static string? _picked;

    /// <summary>
    /// Show the file browser. Returns when it is closed
    /// </summary>
    /// <param name="favorites">True to open the favorites list first</param>
    internal static void Run(bool favorites = false)
    {
        _openFavorites = favorites;
        Browse(BrowseMode.Edit, DialogText.CaptionsFiles);
    }

    /// <summary>
    /// Show the favorites of the file browser (Favorites on the start page, ":favorites"): closing them returns to
    /// where they were opened from. A file or directory shown from them (B, a directory opened) shows the file
    /// browser, which is then closed as usual
    /// </summary>
    internal static void RunFavorites()
    {
        _favoritesOnly = true;
        try
        {
            Run(true);
        }
        finally
        {
            _favoritesOnly = false;
        }
    }

    // The favorites are shown alone (RunFavorites): closing them closes the file browser too
    private static bool _favoritesOnly;

    // A jump: the directory to show, and the file to open in the editor when the browser is shown
    private static string? _jumpDirectory;
    private static string? _jumpFile;
    private static (int Line, int Column)? _jumpAt; // Where in the file (a bookmark made in the editor)

    // The file is opened at this line and column (not where it was left)
    private static (int Line, int Column)? _openAtCaret;

    /// <summary>
    /// Open the file browser in the directory of a file, and the file in the editor (a jump), at a line and column
    /// (null: where the cursor was left)
    /// </summary>
    internal static void RunAtFile(string path, (int Line, int Column)? at = null)
    {
        _jumpDirectory = System.IO.Path.GetDirectoryName(path);
        _jumpFile = path;
        _jumpAt = at;
        Run();
    }

    /// <summary>
    /// Open the file browser in a directory (a jump)
    /// </summary>
    internal static void RunAtDirectory(string path)
    {
        _jumpDirectory = path;
        _jumpFile = null;
        Run();
    }

    private static bool _openFavorites; // The favorites list is opened as soon as the file browser is shown

    /// <summary>
    /// Let the user select a file
    /// </summary>
    /// <returns>The path of the file, or null if canceled</returns>
    internal static string? PickFile(string caption)
    {
        return Browse(BrowseMode.PickFile, caption);
    }

    /// <summary>
    /// Let the user select a directory
    /// </summary>
    /// <returns>The path of the directory, or null if canceled</returns>
    internal static string? PickDirectory(string caption)
    {
        return Browse(BrowseMode.PickDirectory, caption);
    }

    // Show a directory in a pane (the home directory if it does not exist), with the file or directory of that
    // name selected (null: the first row)
    private static void StartPaneIn(Pane pane, string directory, string? select = null)
    {
        var active = _active;
        if (_mode == BrowseMode.Edit) _active = Array.IndexOf(Panes, pane);
        pane.Filter = string.Empty;
        pane.Directory = string.Empty;
        var exists = System.IO.Directory.Exists(directory);
        ChangeDirectory(exists ? directory : HomeDirectory(), exists && select?.Length > 0 ? select : null);
        pane.ShownTop = -1;
        _active = active;
    }

    private static string? Browse(BrowseMode mode, string caption)
    {
        // Opened from a browser that is open (a folder picked from the list of projects, opened from the browser
        // or the editor): that browser is as it was when this one is closed
        if (_browseDepth > 0)
        {
            var saved = (_mode, _caption, _pending, _picked, _message, _active, _browsing, _peeking);
            _browseDepth++;
            _peeking = false;
            try
            {
                return BrowseOnce(mode, caption);
            }
            finally
            {
                _browseDepth--;
                (_mode, _caption, _pending, _picked, _message, _active, _browsing, _peeking) = saved;
            }
        }

        _browseDepth++;
        try
        {
            return BrowseOnce(mode, caption);
        }
        finally
        {
            _browseDepth--;
        }
    }

    // The browsers open (one inside another: Browse)
    private static int _browseDepth;

    private static string? BrowseOnce(BrowseMode mode, string caption)
    {
        _pending = null;
        _mode = mode;
        _caption = caption;
        _picked = null;
        _message = string.Empty;
        if (mode == BrowseMode.Edit)
        {
            FileFavorites.Load();
            _active = Math.Clamp(StartPane, 0, 1);
            StartPaneIn(Panes[0], StartDirectory, StartSelected);
            // The second pane starts where the first is until it has a directory of its own
            if (StartDirectory2.Length > 0)
            {
                StartPaneIn(Panes[1], StartDirectory2, StartSelected2);
            }
            else
            {
                StartPaneIn(Panes[1], Panes[0].Directory);
            }
        }
        else if (mode == BrowseMode.PickDirectory && PickStart() is { } place)
        {
            // Where a folder was picked last (or the picking canceled)
            StartPaneIn(PickPane, place.Directory, place.Select);
        }
        else
        {
            StartPaneIn(PickPane, StartDirectory);
        }

        // A jump: the directory of the jump, and the file opened in the editor
        var jumpFile = _jumpFile;
        var jumpAt = _jumpAt;
        if (mode == BrowseMode.Edit && _jumpDirectory != null && System.IO.Directory.Exists(_jumpDirectory))
        {
            StartPaneIn(ActivePane, _jumpDirectory);
        }

        _jumpDirectory = null;
        _jumpFile = null;
        _jumpAt = null;
        // The places of the panes are saved however the browser is left: closed, a jump (to another page), or
        // exiting Blyant (Tools.ExitApplication)
        _browsing = mode == BrowseMode.Edit;
        string? result = null;
        try
        {
            result = BrowseLoop(mode, jumpFile, jumpAt);
            return result;
        }
        finally
        {
            if (mode == BrowseMode.PickDirectory) RememberPickPlace(result);
            if (mode == BrowseMode.Edit)
            {
                _browsing = false;
                RememberPanes();
            }
        }
    }

    // The browser is shown for editing files (not for picking one): its places are saved when Blyant exits
    private static bool _browsing;

    /// <summary>
    /// Save the places of the file browser if Blyant exits while it is open (also from the editor opened from it)
    /// </summary>
    internal static void RememberOnExit()
    {
        if (_browsing) RememberPanes();
    }

    // Save the directories of the panes, the file or directory selected in each, and the pane with the focus, for
    // the next time the browser is opened
    private static void RememberPanes()
    {
        if (Panes[0].Directory.Length > 0)
        {
            StartDirectory = Panes[0].Directory;
            StartSelected = SelectedName(Panes[0]);
        }

        if (Panes[1].Directory.Length > 0)
        {
            StartDirectory2 = Panes[1].Directory;
            StartSelected2 = SelectedName(Panes[1]);
        }

        StartPane = _active;
        FileIo.SaveSettings();
    }

    // The name of the file or directory selected in a pane (empty on "..": the first row)
    /// <summary>
    /// Where the folder pickers start (setting "pickdir"): the folder picked last, or what was selected where the
    /// picking was canceled (a path ending with a separator: that directory, with nothing selected). Empty: where
    /// the file browser was. The pickers share it, and the panes of the file browser are not changed by it
    /// </summary>
    internal static string PickPlace { get; set; } = string.Empty;

    // The directory a folder picker starts in, and the name selected in it (null: none)
    private static (string Directory, string? Select)? PickStart()
    {
        if (PickPlace.Length == 0) return null;
        var place = PickPlace;
        if (place.EndsWith(System.IO.Path.DirectorySeparatorChar) || place.EndsWith(System.IO.Path.AltDirectorySeparatorChar))
        {
            var directory = System.IO.Path.TrimEndingDirectorySeparator(place);
            if (directory.Length == 0) directory = place;
            return Directory.Exists(directory) ? (directory, null) : null;
        }

        // The folder picked: the one it is in, with it selected (the folders next to it are near)
        var parent = System.IO.Path.GetDirectoryName(place);
        if (parent == null || !Directory.Exists(parent)) return null;
        return (parent, System.IO.Path.GetFileName(place));
    }

    // A folder picker is closed: the folder picked, or where it was canceled, is where the next one starts
    private static void RememberPickPlace(string? picked)
    {
        string place;
        if (picked != null)
        {
            place = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(picked));
            // A root ("C:\") has no folder it is in: started in itself
            if (System.IO.Path.GetDirectoryName(place) == null) place = System.IO.Path.GetFullPath(picked);
        }
        else if (PickPane.Directory.Length > 0)
        {
            var name = SelectedName(PickPane);
            place = name.Length > 0 && name != ParentName
                ? System.IO.Path.Combine(PickPane.Directory, name)
                : System.IO.Path.TrimEndingDirectorySeparator(PickPane.Directory) + System.IO.Path.DirectorySeparatorChar;
        }
        else
        {
            // The list of drives: as before
            return;
        }

        if (place == PickPlace) return;
        PickPlace = place;
        FileIo.SaveSettings();
    }

    private static string SelectedName(Pane pane)
    {
        if (pane.Index <= 0 || pane.Index >= pane.Items.Count) return string.Empty;
        return pane.Items[pane.Index].Name;
    }

    private static string? BrowseLoop(BrowseMode mode, string? jumpFile, (int Line, int Column)? jumpAt)
    {
        Init.ApplyAutoSize();
        Draw(true);
        if (Tools.TakeRedrawRequest()) Draw(true);
        if (mode == BrowseMode.Edit && jumpFile != null && File.Exists(jumpFile))
        {
            var info = new FileInfo(jumpFile);
            _openAtCaret = jumpAt;
            var close = Open(new Item(info.Name, info.FullName, false, info.Length));
            _openAtCaret = null;
            if (close) return _picked;
            LoadItems(info.Name);
            Draw(true);
        }

        while (true)
        {
            // Starting in the favorites list is the same as pressing F (closing the list shows the file browser)
            var key = _openFavorites && _mode == BrowseMode.Edit
                ? new ConsoleKeyInfo('F', ConsoleKey.F, true, false, false)
                : Keyboard.ReadKey();
            _openFavorites = false;
            // The window was resized (or Blyant went on after Ctrl+Z): all is drawn again, with the message (nothing
            // on the disk changed: the panes are not read again)
            if (Keyboard.IsRedraw(key))
            {
                Init.ApplyAutoSize();
                Draw(true);
                if (Tools.TakeRedrawRequest()) Draw(true);
                continue;
            }

            // Moving the selection only redraws the selection pointer (unless the list has to scroll)
            var redraw = true;
            var hadMessage = _message.Length > 0;
            _message = string.Empty;
            // A count in front of a key, in the pane with the focus: 5j 5k (5 rows), 5gg 5G (row 5), shown below the
            // pane. Esc only drops it. Other keys do what they do once
            if (_listCount.Add(key))
            {
                Draw(hadMessage);
                continue;
            }

            var count = _listCount.Take();
            if (count != null && key.Key == ConsoleKey.Escape)
            {
                Draw(hadMessage);
                continue;
            }

            // A key that uses the other pane (copy, move, compare ...) ends the peek first
            if (_peeking && !KeepsPeek(key)) StopPeek();
            if (_mode == BrowseMode.Edit && _pending == null && Jump.IsKey(key))
            {
                // The Jump menu
                Jump.Open();
            }
            else if (_peeking && key.Key == ConsoleKey.Escape)
            {
                StopPeek();
            }
            else if (_peeking && key.Key == ConsoleKey.Tab)
            {
                FocusPeek();
            }
            else if (_pending != null && key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Not copied or moved after all
                StopSelectingTarget();
            }
            else if (_pending != null && key.Key is ConsoleKey.S or ConsoleKey.Spacebar)
            {
                CompleteSelectingTarget();
            }
            else if (key.KeyChar == ':')
            {
                // The command is read as typed, as paths can be case sensitive
                var command = CommandLine.Read(':', false,
                    _mode == BrowseMode.Edit ? CommandContext.Files : CommandContext.FilePicker);
                var name = command == null ? string.Empty : CommandLine.Name(command).ToLowerInvariant();
                // A command ends the peek first (":peek" ends it itself)
                if (_peeking && command != null && name != "peek") StopPeek();
                if (_pending != null && command != null &&
                    name is not ("cd" or "home" or "data" or "projects" or "proj" or "pro" or "h" or "help" or "panes"))
                {
                    // Only going to a directory while the place to copy or move to is selected
                    _message = DialogText.FileSelectTarget;
                    command = null;
                    name = string.Empty;
                }

                if (command != null)
                {
                    // File names and paths are logged, but not something typed that is not a command
                    EventLog.Add(DialogText.LogFilesCommand + ": :" +
                                 (Commands.IsKnown(CommandContext.Files, name) ? command : "(not a command)"));
                }

                if (name is "q" or "quit" or "close")
                {
                    break;
                }

                if (name == "exit" && _mode == BrowseMode.Edit)
                {
                    // Leave Blyant (if the user wants that)
                    if (ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false))
                    {
                        Tools.ExitApplication();
                    }

                    Draw(true);
                    continue;
                }

                if (name is "home")
                {
                    ChangeDirectory(HomeDirectory(), null);
                }
                else if (name is "projects" or "proj" or "pro" && _mode == BrowseMode.Edit)
                {
                    OpenProject();
                }
                else if (name is "data")
                {
                    // The data directory (with the settings, the snippets, the favorites ...)
                    ChangeDirectory(System.IO.Path.GetFullPath(FileIo.GetDataDirectoryName()), null);
                }
                else if (name is "project" && _mode == BrowseMode.Edit)
                {
                    // ":project add": the directory shown is added to the projects
                    var argument = command!.Length > name.Length ? command[name.Length..].Trim() : string.Empty;
                    if (!argument.Equals("add", StringComparison.OrdinalIgnoreCase))
                    {
                        _message = Lang.T(":project add  adds the directory shown to the projects");
                    }
                    else if (ActivePane.Directory.Length == 0)
                    {
                        _message = DialogText.DirectoryNotFound;
                    }
                    else if (ProjectsBox.AddFolder(ActivePane.Directory) != null)
                    {
                        _message = Lang.F("Added to the projects: {0}", System.IO.Path.GetFileName(
                            System.IO.Path.TrimEndingDirectorySeparator(ActivePane.Directory)));
                    }

                    Draw(true);
                }
                else if (name is "trash" && _mode == BrowseMode.Edit)
                {
                    // What was deleted (in the data directory). Deleting there is for good
                    if (Directory.Exists(FileOperations.TrashDirectory))
                    {
                        ChangeDirectory(FileOperations.TrashDirectory, null);
                    }
                    else
                    {
                        _message = DialogText.TrashEmpty;
                    }
                }
                else if (name is "recovery" && _mode == BrowseMode.Edit)
                {
                    // The recovery copies of text that was not saved (in the data directory)
                    if (Directory.Exists(Recovery.Directory) &&
                        Directory.EnumerateFileSystemEntries(Recovery.Directory).Any())
                    {
                        ChangeDirectory(Recovery.Directory, null);
                    }
                    else
                    {
                        _message = DialogText.RecoveryEmpty;
                    }
                }
                else if (name is "h" or "help")
                {
                    Tools.WriteEditorHelp(Tools.HelpTopic.Files);
                }
                else if (name is "number" or "nonumber")
                {
                    // The numbers of the rows of the panes on or off (kept in the settings)
                    FileNumbers = name == "number";
                    _message = FileNumbers ? DialogText.LineNumbersOn : DialogText.LineNumbersOff;
                    FileIo.SaveSettings();
                }
                else if (name == "cd" && command != null)
                {
                    // Go to a directory by path (a relative path starts from the directory shown)
                    var path = CommandLine.Argument(command);
                    if (path.Length > 0 && ActivePane.Directory.Length > 0)
                    {
                        path = System.IO.Path.GetFullPath(path, ActivePane.Directory);
                    }

                    if (Directory.Exists(path))
                    {
                        ChangeDirectory(System.IO.Path.GetFullPath(path), null);
                    }
                    else
                    {
                        _message = DialogText.DirectoryNotFound;
                    }
                }
                else if (command != null)
                {
                    var result = _mode == BrowseMode.Edit
                        ? RunFileCommand(name, CommandLine.Argument(command))
                        : KeyResult.NotHandled;
                    if (result == KeyResult.Close) break;
                    if (result == KeyResult.NotHandled) CommandLine.ShowUnknown(command);
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(Tools.HelpTopic.Files);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the names containing a text
                ActivePane.Filter = CommandLine.Read('/', false) ?? string.Empty;
                LoadItems(null);
            }
            else if (key.KeyChar == 'G')
            {
                ActivePane.Index = ListCount.Row(count, ActivePane.Items.Count, false);
                redraw = false;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') ActivePane.Index = ListCount.Row(count, ActivePane.Items.Count, true);
                redraw = false;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Esc removes the marks, and then a filter, before it closes the browser
                if (key.Key == ConsoleKey.Escape && ActivePane.Marks.Count > 0)
                {
                    ActivePane.Marks.Clear();
                }
                else if (key.Key == ConsoleKey.Escape && ActivePane.Filter.Length > 0)
                {
                    ActivePane.Filter = string.Empty;
                    LoadItems(null);
                }
                else
                {
                    break;
                }
            }
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                ActivePane.Index = Math.Min(ActivePane.Index + (count ?? 1), Math.Max(0, ActivePane.Items.Count - 1));
                redraw = false;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                ActivePane.Index = Math.Max(0, ActivePane.Index - (count ?? 1));
                redraw = false;
            }
            else if (key.Key == ConsoleKey.PageDown || Keyboard.IsControl(key, ConsoleKey.D) ||
                     Keyboard.IsControl(key, ConsoleKey.F))
            {
                ActivePane.Index += PageSize;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.PageUp || Keyboard.IsControl(key, ConsoleKey.U) ||
                     Keyboard.IsControl(key, ConsoleKey.B))
            {
                ActivePane.Index -= PageSize;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.Home)
            {
                ActivePane.Index = 0;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.End)
            {
                ActivePane.Index = ActivePane.Items.Count - 1;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.Backspace || key.KeyChar == '-')
            {
                GoToParent();
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.Enter)
            {
                // (while the place to copy or move to is selected, only directories are opened)
                if (ActivePane.Items.Count > 0 &&
                    (_pending == null || ActivePane.Items[ActivePane.Index].IsDirectory) &&
                    Open(ActivePane.Items[ActivePane.Index]))
                {
                    // A file was picked: the browser is closed
                    break;
                }
            }
            else if (_mode == BrowseMode.PickDirectory && key.Key is ConsoleKey.S or ConsoleKey.Spacebar)
            {
                // Select the directory shown
                if (ActivePane.Directory.Length > 0)
                {
                    _picked = ActivePane.Directory;
                    break;
                }
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.Tab)
            {
                // The focus goes to the other pane
                if (IsDual) _active = 1 - _active;
                redraw = false;
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.T)
            {
                TogglePanes();
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.E &&
                     (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                // The projects (also while the place to copy or move to is selected)
                OpenProject();
            }
            else if (_pending != null)
            {
                // The other keys do nothing until the place to copy or move to is selected
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.Spacebar)
            {
                ToggleMark();
                redraw = false;
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.N)
            {
                NewFile(string.Empty);
            }
            else if (key.Key == ConsoleKey.R)
            {
                LoadItems(ActivePane.Items.Count > 0 ? ActivePane.Items[ActivePane.Index].Name : null);
            }
            else if (_mode == BrowseMode.Edit && HandleFileKey(key) == KeyResult.Close)
            {
                break;
            }

            // Follow the size of the terminal window, and draw again if the page could not be drawn
            if (Init.ApplyAutoSize()) redraw = true;
            // What was done in one pane can have changed what the other shows
            if (redraw && IsDual) Reload(OtherPane);
            Draw(redraw || hadMessage);
            if (Tools.TakeRedrawRequest()) Draw(true);
        }

        // (Shown again with the panes as they were)
        StopPeek();
        return _picked;
    }

    // Show one pane or two
    private static void TogglePanes()
    {
        DualPane = !DualPane;
        // A single pane shows the one that had the focus
        Reload(OtherPane);
    }

    // Read the content of the directory of a pane again, keeping the selection
    private static void Reload(Pane pane)
    {
        var selected = pane.Items.Count > 0 && pane.Index < pane.Items.Count ? pane.Items[pane.Index] : null;
        var index = pane.Index;
        var top = pane.Top;
        var message = _message;
        if (!LoadItems(pane, null))
        {
            // The directory is gone (it can have been moved or deleted from the other pane)
            pane.Directory = HomeDirectory();
            pane.Filter = string.Empty;
            LoadItems(pane, null);
            _message = message;
            return;
        }

        var found = selected == null ? -1 : pane.Items.FindIndex(item => item.Name == selected.Name);
        pane.Index = found >= 0 ? found : Math.Min(index, Math.Max(0, pane.Items.Count - 1));
        pane.Top = top;
    }

    /// <summary>
    /// The directory the file browser was in last (in the pane that had the focus), or its home directory: where
    /// New file on the start page makes a file
    /// </summary>
    internal static string LastDirectory
    {
        get
        {
            var last = StartPane == 1 && StartDirectory2.Length > 0 ? StartDirectory2 : StartDirectory;
            return last.Length > 0 && Directory.Exists(last) ? System.IO.Path.GetFullPath(last) : HomeDirectory();
        }
    }

    // Where the browser starts when it has no directory of its own (":home"): the home directory of the user
    private static string HomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && Directory.Exists(home) ? home : System.IO.Path.GetFullPath(FileIo.GetDataDirectoryName());
    }

    #region Directories

    private static void ChangeDirectory(string directory, string? select)
    {
        var previous = ActivePane.Directory;
        ActivePane.Directory = directory;
        ActivePane.Filter = string.Empty;
        if (!LoadItems(select))
        {
            // Stay where we were if the directory cannot be read
            ActivePane.Directory = previous;
            var message = _message;
            LoadItems(null);
            _message = message;
        }
    }

    private static void GoToParent()
    {
        if (ActivePane.Directory.Length == 0) return;
        var parent = Directory.GetParent(ActivePane.Directory)?.FullName;
        var name = System.IO.Path.GetFileName(ActivePane.Directory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (parent != null)
        {
            ChangeDirectory(parent, name);
        }
        else if (OperatingSystem.IsWindows())
        {
            // Above the root of a drive is the list of drives
            ChangeDirectory(string.Empty, ActivePane.Directory);
        }
    }

    // Read the content of the directory of the pane with the focus
    private static bool LoadItems(string? select)
    {
        return LoadItems(ActivePane, select);
    }

    // Read the content of the directory of a pane: directories first, then files, both sorted by name
    private static bool LoadItems(Pane pane, string? select)
    {
        var items = new List<Item>();
        try
        {
            if (pane.Directory.Length == 0)
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady) items.Add(new Item(drive.Name, drive.Name, true, 0));
                }
            }
            else
            {
                items.Add(new Item(ParentName, string.Empty, true, 0));
                var info = new DirectoryInfo(pane.Directory);
                foreach (var directory in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (IsShown(directory, pane.Filter)) items.Add(new Item(directory.Name, directory.FullName, true, 0));
                }

                if (_mode != BrowseMode.PickDirectory)
                {
                    foreach (var file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (IsShown(file, pane.Filter)) items.Add(new Item(file.Name, file.FullName, false, file.Length));
                    }
                }
            }
        }
        catch (Exception e)
        {
            _message = e.Message;
            return false;
        }

        pane.Items = items;
        // The marks are kept while the same directory is shown (also with a filter). The ones gone are dropped
        if (pane.MarksDirectory != pane.Directory) pane.Marks.Clear();
        pane.Marks.RemoveWhere(path => !File.Exists(path) && !Directory.Exists(path));
        pane.MarksDirectory = pane.Directory;
        pane.Index = Math.Max(0, select == null ? 0 : items.FindIndex(i => i.Name == select || i.Path == select));
        pane.Top = 0;
        return true;
    }

    // The names in a directory for the peek, as a pane would show them (LoadItems, without a filter): directories
    // first (ending in "/"), then the files, both sorted by name, at most "count" lines. When it cannot be read: why
    private static List<string> PeekContents(string directory, int count)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            var names = info.EnumerateDirectories().Where(d => IsShown(d, string.Empty))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Select(d => d.Name + "/").ToList();
            if (_mode != BrowseMode.PickDirectory)
            {
                names.AddRange(info.EnumerateFiles().Where(f => IsShown(f, string.Empty))
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Select(f => f.Name));
            }

            if (names.Count == 0) return new List<string> { Lang.T("(empty)") };
            if (names.Count <= count) return names;
            var shown = names.Take(count).ToList();
            shown[^1] = Lang.F("... and {0} more", names.Count - count + 1);
            return shown;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string> { e.Message };
        }
    }

    private static bool IsShown(FileSystemInfo info, string filter)
    {
        if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        return filter.Length == 0 || info.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Opening files

    /// <returns>True if a file was picked and the browser must close</returns>
    private static bool Open(Item item)
    {
        if (item.IsDirectory)
        {
            if (item.Name == ParentName)
            {
                GoToParent();
            }
            else
            {
                ChangeDirectory(item.Path, null);
            }

            return false;
        }

        if (_mode == BrowseMode.PickFile)
        {
            _picked = item.Path;
            return true;
        }

        TextFile file;
        try
        {
            file = TextFile.Load(item.Path);
        }
        catch (Exception e)
        {
            _message = e.Message;
            return false;
        }

        var converted = false;
        if (!file.IsUtf8)
        {
            // Ask how to convert the file
            var selection = ChoiceBox.Pick(DialogText.CaptionsFiles,
                Lang.F("This file is not UTF-8. It is read as {0}.", file.EncodingName), DialogText.NotUtf8Note,
                new[] { DialogText.ConvertReplace, DialogText.ConvertCopy, DialogText.ConvertCancel },
                new[] { "A", "B", "Esc" }, 0, false);
            if (selection == 1)
            {
                file.Path = GetCopyPath(item.Path);
            }
            else if (selection != 0)
            {
                return false;
            }

            converted = true;
        }

        Edit(file, converted);
        // The file is selected (by the name it has now, if it was renamed in the editor), or the last file written
        // with ":w name" or ":saveas name" (they are in the same directory; a filter that hides it is taken away)
        var select = System.IO.Path.GetFileName(_lastWrittenPath.Length > 0 ? _lastWrittenPath
            : _lastEditedPath.Length > 0 ? _lastEditedPath : file.Path);
        if (_lastWrittenPath.Length > 0 && ActivePane.Filter.Length > 0 &&
            !select.Contains(ActivePane.Filter, StringComparison.OrdinalIgnoreCase))
        {
            ActivePane.Filter = string.Empty;
        }

        LoadItems(select);
        return false;
    }

    // The path for the UTF-8 copy of a file: "name-utf8.txt" (numbered if it exists)
    private static string GetCopyPath(string path)
    {
        var directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        var name = System.IO.Path.GetFileNameWithoutExtension(path) + CopyNameSuffix;
        var extension = System.IO.Path.GetExtension(path);
        var copy = System.IO.Path.Combine(directory, name + extension);
        for (var number = 2; File.Exists(copy); number++)
        {
            copy = System.IO.Path.Combine(directory, name + number + extension);
        }

        return copy;
    }

    // Where the file being opened is opened (a line found, a peek, a bookmark), taken for this file only: the places
    // are cleared at once, as a jump away from the editor leaves through Navigation.Wait, past the code that would
    // clear them after Open, and the next file opened would get them
    private static (int Index, bool Found, (int Line, int Column)? Caret) TakeOpenPlace()
    {
        var place = (_openAtIndex, _openAtFound, _openAtCaret);
        _openAtIndex = 0;
        _openAtFound = false;
        _openAtCaret = null;
        return place;
    }

    // Open a text file in the editor
    private static void Edit(TextFile file, bool modified)
    {
        var openPlace = TakeOpenPlace();
        // The places of the browser are saved before the editor is opened, should Blyant not be closed as usual
        if (_browsing) RememberPanes();
        _file = file;
        _lastWrittenPath = string.Empty;
        // Text of the file that was not saved when Blyant stopped: the user is asked what to do with it
        var recovered = OfferRecovery(file, out var recoveredCopy);
        EventLog.Add(DialogText.LogFile + "Open: " + file.Path);
        AddToHistory(file.Path);
        try
        {
            // The line found by a search, or else where the cursor was when the file was closed last time (a
            // favorite first, then the history)
            var (cursorIndex, openAtFound, openAtCaret) = openPlace;
            (int Index, bool OtherLeft)? split = null;
            if (openAtCaret is { } at)
            {
                // A bookmark made in the editor: where it was made
                cursorIndex = CaretPosition.IndexOf(recovered ?? _file?.Text ?? string.Empty, at.Line, at.Column);
            }
            else if (!openAtFound && CaretOfFile(file.Path) is { } caret)
            {
                cursorIndex = CaretPosition.IndexOf(recovered ?? _file?.Text ?? string.Empty, caret.Line, caret.Column);
                // Closed in a split: opened split again
                split = CaretPosition.SplitOf(caret.Split, recovered ?? _file?.Text ?? string.Empty);
            }

            while (true)
            {
                Tools.ClearScreen();
                Actions.DrawEditFileMenu();
                TextArea.NextSplit = split;
                TextArea.NextMarks = SessionMarks.GetValueOrDefault(file.Path);
                // The recovered text is opened as changes that are not saved yet
                if (recovered != null) modified = true;
                TextArea.NextRecoveredCopy = recovered != null ? recoveredCopy : null;
                var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                    Init.LineCount - 3, recovered ?? _file?.Text ?? string.Empty, cursorIndex, false, false, false,
                    TextArea.SaveAsFile, modified);
                recovered = null;
                textArea.ClearUndoRedoCache();
                if (_file != null) file = _file; // Renamed in the editor
                KeepCaret(file.Path, textArea);
                // ":new name": the new file (made next to this one) in its place
                if (TextArea.TakeNewDocumentRequest() is { } newName)
                {
                    var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file.Path) ?? string.Empty, newName);
                    _file = TextFile.Create(path);
                    file = _file;
                    EventLog.Add(DialogText.LogFile + "Open: " + file.Path);
                    AddToHistory(file.Path);
                    cursorIndex = 0;
                    split = null;
                    modified = false;
                    continue;
                }

                // ":snip edit" or ":complete edit": that file, and then this file again (as it is on disk)
                if (TextArea.TakeSideFileRequest() is not { } sideFile) break;
                cursorIndex = textArea.GetCursorIndex();
                split = textArea.SplitPlace;
                EditSideFile(sideFile);
                if (!File.Exists(file.Path)) break;
                _file = TextFile.Load(file.Path);
                file = _file;
                modified = false;
            }
        }
        catch (Exception e) when (e is not NavigationException)
        {
            _message = e.Message;
        }
        finally
        {
            _lastEditedPath = _file?.Path ?? file.Path;
            _file = null;
        }
    }

    /// <summary>
    /// A blank page in the editor: a text that is not a file yet, Markdown, named and placed when it is saved (as a
    /// new file). With the text of a recovery copy (a blank page that was not saved, or a file that is gone), the
    /// copy goes with the changes. When the editor is closed, the page it was opened from is shown again
    /// </summary>
    internal static void EditBlank(string text = "", string? recoveredCopy = null)
    {
        var outer = _file;
        _file = TextFile.Create(string.Empty);
        UntitledCopy = Recovery.NewUntitledCopyPath();
        EventLog.Add(DialogText.LogFile + "Open: (blank page)" +
                     (recoveredCopy != null ? " from " + recoveredCopy : string.Empty));
        try
        {
            Tools.ClearScreen();
            Actions.DrawEditFileMenu();
            TextArea.NextRecoveredCopy = recoveredCopy;
            var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                Init.LineCount - 3, text, 0, false, false, false, TextArea.SaveAsFile, text.Length > 0);
            textArea.ClearUndoRedoCache();
            // Saved as a file: opened where the cursor was, the next time
            if (!IsUntitled) KeepCaret(_file.Path, textArea);
        }
        catch (Exception e) when (e is not NavigationException)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.BlankPage, e.Message, string.Empty);
        }
        finally
        {
            _file = outer;
            UntitledCopy = string.Empty;
        }
    }

    /// <summary>
    /// Open the text of a recovery copy: a blank page that was not saved (or a file that is gone) as a blank page
    /// with the text, a file that is there in the file browser and the editor, where what to do is asked
    /// </summary>
    internal static void OpenRecoveryCopy(Recovery.Copy copy)
    {
        if (!copy.IsUntitled && File.Exists(copy.FilePath))
        {
            RunAtFile(copy.FilePath);
            return;
        }

        EditBlank(copy.Text, copy.CopyPath);
    }

    // The path of the file edited last (it can have been renamed in the editor)
    private static string _lastEditedPath = string.Empty;

    // A file is opened that has a recovery copy (text that was not saved when Blyant stopped, see Recovery): the
    // recovered text can be opened, compared with the file, or given up. Returns the text to open, or null to open
    // the file as it is (Esc keeps the copy, to decide later). Nothing is asked when the copy has the text of the
    // file, or another Blyant that is running is editing it. "copyPath" is the file of the copy offered (the newest)
    private static string? OfferRecovery(TextFile file, out string copyPath)
    {
        copyPath = string.Empty;
        var copy = Recovery.Find(file.Path);
        if (copy == null) return null;
        copyPath = copy.CopyPath;
        if (Recovery.WrittenByOtherRunning(copy))
        {
            EventLog.Add(DialogText.LogFile + "Recovery copy: " + file.Path + " - kept (another Blyant is editing it)");
            return null;
        }

        if (TextBuffer.Normalize(copy.Text) == TextBuffer.Normalize(file.Text))
        {
            Recovery.DeleteCopy(copy.CopyPath);
            return null;
        }

        var name = System.IO.Path.GetFileName(file.Path);
        var note = Lang.F("Written {0}.", copy.Written.ToLocalTime().ToString("yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            if (File.Exists(file.Path) && File.GetLastWriteTimeUtc(file.Path) > copy.Written)
            {
                note += " " + Lang.T("The file was changed after the copy was made.");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Only a note
        }

        while (true)
        {
            var choice = ChoiceBox.Pick(DialogText.CaptionsRecovery,
                Lang.F("\"{0}\" has changes that were not saved.", Tools.TruncateString(name, 40, true)), note,
                new[]
                {
                    Lang.T("Open the recovered text"), Lang.T("Show the differences"),
                    Lang.T("Keep the file as it is"), Lang.T("Decide later")
                },
                new[] { "R", "D", "K", "Esc" }, 0, false);
            switch (choice)
            {
                case 0:
                    EventLog.Add(DialogText.LogFile + "Recovery copy opened: " + file.Path);
                    return copy.Text;
                case 1:
                    Tools.HideCursor();
                    DiffView.Show(file.Text, copy.Text, Lang.F("{0} (saved)", name), Lang.F("{0} (recovered)", name),
                        System.IO.Path.GetDirectoryName(file.Path) ?? Recovery.Directory, false, string.Empty, out _);
                    Tools.ClearScreen();
                    ChoiceBox.Forget();
                    break;
                case 2:
                    if (ChoiceBox.ConfirmGuarded(DialogText.CaptionsRecovery, Lang.T("Delete the recovered text?"),
                            Lang.T("The file is opened as it is. This cannot be undone")))
                    {
                        Recovery.DeleteCopy(copy.CopyPath);
                        EventLog.Add(DialogText.LogFile + "Recovery copy deleted: " + file.Path);
                        return null;
                    }

                    break;
                default:
                    // Later: the copy stays, and is offered the next time
                    return null;
            }
        }
    }

    /// <summary>
    /// Give the file being edited another name in its directory (":rename" in the editor). The favorites, the
    /// history and the jumps follow it. The text in the editor is not saved: ":w" saves it to the file by its new
    /// name. The settings file and the file of snippets keep their names
    /// </summary>
    /// <param name="name">The new name, or empty to ask for it</param>
    /// <returns>DialogText.Success, an error message, or empty if the box was closed without a name</returns>
    internal static string RenameCurrentFile(string name)
    {
        if (_file == null) return DialogText.FileNotFound;
        if (IsUntitled) return DialogText.SaveFirst;
        var path = System.IO.Path.GetFullPath(_file.Path);
        var settings = System.IO.Path.GetFullPath(System.IO.Path.Combine(FileIo.GetDataDirectoryName(),
            FileIo.SettingsFileName));
        if (FileOperations.SamePath(path, settings) ||
            FileOperations.SamePath(path, System.IO.Path.GetFullPath(Snippets.FilePath)))
        {
            return DialogText.FileKeepsItsName;
        }

        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            var asked = AskName(DialogText.RenameFile, System.IO.Path.GetFileName(path));
            if (asked == null) return string.Empty;
            name = asked;
        }

        var result = FileOperations.Rename(path, name, out var renamed);
        LogOperation("Rename", path + " -> " + name, result);
        if (result != DialogText.Success) return result;
        _file.Path = renamed;
        PathRenamed(path, renamed);
        return DialogText.Success;
    }

    /// <summary>
    /// A file or directory has a new path: the favorites, the history and the jumps follow it
    /// </summary>
    internal static void PathRenamed(string oldPath, string newPath)
    {
        FileFavorites.Load();
        if (FileFavorites.PathChanged(oldPath, newPath)) FileFavorites.Save();
        FileHistory.Load();
        if (FileHistory.PathChanged(oldPath, newPath)) FileHistory.Save();
        JumpHistory.Load();
        if (JumpHistory.PathChanged(oldPath, newPath)) JumpHistory.Save();
        Bookmarks.Follow(oldPath, newPath);
    }

    // Where the cursor was in a file when it was closed: kept in the favorites first, then in the history
    private static (int Line, int Column, string Split)? CaretOfFile(string path)
    {
        FileFavorites.Load();
        FileHistory.Load();
        return FileFavorites.CaretOf(path) ?? FileHistory.CaretOf(path);
    }

    /// <summary>
    /// Keep where the cursor is in the file being edited, when the editor is left by a jump
    /// </summary>
    // The marks of the files closed in this session ("ma"), by their paths: set again when a file is opened
    private static readonly Dictionary<string, Dictionary<char, TextPos>> SessionMarks =
        new(FileOperations.NamesIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static void KeepCaretOfCurrentFile(TextArea textArea)
    {
        if (_file != null && !IsUntitled) KeepCaret(_file.Path, textArea);
    }

    // Keep where the cursor is in a file that is closed in the editor: in the history, and in the favorites if
    // it is one
    private static void KeepCaret(string path, TextArea textArea)
    {
        // The marks, while Blyant runs
        SessionMarks[path] = textArea.CopyMarks();
        var text = textArea.GetText();
        var (line, column) = CaretPosition.Of(text, textArea.GetCursorIndex());
        // Closed in a split: where the cursor of the other pane was, and on which side
        var split = string.Empty;
        if (textArea.SplitPlace is { } other)
        {
            var (otherLine, otherColumn) = CaretPosition.Of(text, other.Index);
            split = CaretPosition.SplitText(otherLine, otherColumn, other.OtherLeft);
        }

        FileHistory.Load();
        if (FileHistory.SetCaret(path, line, column, split))
        {
            var status = FileHistory.Save();
            if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "History: " + status);
        }

        FileFavorites.Load();
        if (FileFavorites.SetCaret(path, line, column, split))
        {
            var status = FileFavorites.Save();
            if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "Favorites: " + status);
        }
    }

    /// <summary>
    /// Open the file of snippets or completions that the editor was closed for (TextArea.TakeSideFileRequest)
    /// </summary>
    internal static void EditSideFile(TextArea.SideFile file)
    {
        if (file.Completions)
        {
            EditCompletions();
        }
        else
        {
            EditSnippets(file.Line);
        }
    }

    /// <summary>
    /// Open the file of snippets in the editor (at a line), from the editor of a file (":snip edit"). The file
    /// being edited is kept
    /// </summary>
    internal static void EditSnippets(int line)
    {
        EditBlyantFile(Snippets.FilePath, DialogText.CaptionsSnippets, line);
    }

    /// <summary>
    /// Open the file of completions in the editor (it is made first if there is none), as the file of snippets
    /// </summary>
    internal static void EditCompletions()
    {
        var status = Completions.EnsureFile();
        if (status != DialogText.Success)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.CaptionsCompletions, status, string.Empty);
            return;
        }

        EditBlyantFile(Completions.FilePath, DialogText.CaptionsCompletions, 0);
    }

    /// <summary>
    /// Open the note of a project in the editor (N in the list of projects), as the file of snippets. A file
    /// being edited is kept
    /// </summary>
    internal static void EditNote(string path)
    {
        EditBlyantFile(path, DialogText.OpenProject, 0);
    }

    /// <summary>
    /// A file of Blyant (snippets, completions, the note of a project) is being edited
    /// </summary>
    internal static bool EditingBlyantFile { get; private set; }

    // Open a file of Blyant (snippets, completions) in the editor at a line
    private static void EditBlyantFile(string path, string caption, int line)
    {
        var outer = _file;
        var editingBefore = EditingBlyantFile;
        EditingBlyantFile = true;
        try
        {
            _file = TextFile.Load(path);
            var recovered = OfferRecovery(_file, out var recoveredCopy);
            EventLog.Add(DialogText.LogFile + "Open: " + _file.Path);
            Tools.ClearScreen();
            Actions.DrawEditFileMenu();
            TextArea.NextRecoveredCopy = recovered != null ? recoveredCopy : null;
            var text = recovered ?? _file.Text;
            var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                Init.LineCount - 3, text, Snippets.IndexOfLine(text, line), false, false, false,
                TextArea.SaveAsFile, recovered != null);
            textArea.ClearUndoRedoCache();
            TextArea.TakeSideFileRequest();
        }
        catch (Exception e) when (e is not NavigationException)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(caption, e.Message, string.Empty);
        }
        finally
        {
            _file = outer;
            EditingBlyantFile = editingBefore;
        }
    }

    /// <summary>
    /// The name of the file being edited (for the title of the editor)
    /// </summary>
    internal static string CurrentFileName => _file == null ? string.Empty : System.IO.Path.GetFileName(_file.Path);

    /// <summary>
    /// The text being edited is a blank page: not a file yet (it is named when it is saved)
    /// </summary>
    internal static bool IsUntitled => _file != null && _file.Path.Length == 0;

    /// <summary>
    /// The file of the recovery copy of the blank page being edited (empty if it is not one)
    /// </summary>
    internal static string UntitledCopy { get; private set; } = string.Empty;

    /// <summary>
    /// The full path of the file being edited (empty if none)
    /// </summary>
    internal static string CurrentFilePath =>
        _file == null || _file.Path.Length == 0 ? string.Empty : System.IO.Path.GetFullPath(_file.Path);

    /// <summary>
    /// The type and encoding of the file being edited, and line endings of the file being edited, for the bottom line of the editor:
    /// "HTML UTF-8 (BOM) CRLF"
    /// </summary>
    internal static string CurrentFileLabel
    {
        get
        {
            if (_file == null) return string.Empty;
            // (a blank page is Markdown until it is saved)
            var extension = IsUntitled ? FileOperations.DefaultExtension : System.IO.Path.GetExtension(_file.Path);
            // (".blyant" is a type, as in the file browser)
            var type = extension.Length > 1
                ? Tools.TruncateString(extension[1..].ToUpperInvariant(), 10, true) + " "
                : string.Empty;
            return type + _file.EncodingLabel + " " + _file.LineEndingLabel;
        }
    }

    /// <summary>
    /// The line endings of the file being edited: "CRLF" or "LF" (empty if none)
    /// </summary>
    internal static string CurrentLineEndingLabel => _file?.LineEndingLabel ?? string.Empty;

    /// <summary>
    /// Save the file being edited with other line endings from now on (":lineendings lf"). False if it has them
    /// already, or no file is edited
    /// </summary>
    internal static bool SetLineEnding(string ending)
    {
        if (_file == null || _file.LineEnding == ending) return false;
        _file.SetLineEnding(ending);
        return true;
    }

    /// <summary>
    /// What the editor tells when the file was read with more than one kind of line endings (null: one kind)
    /// </summary>
    internal static string? MixedLineEndingsNote => _file is { MixedLineEndings: true } file
        ? Lang.F("Mixed line endings: saved as {0} (:lineendings changes it)", file.LineEndingLabel)
        : null;

    /// <summary>
    /// Read the file being edited from disk again (it can have been changed by another program)
    /// </summary>
    /// <param name="text">The text of the file</param>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string ReloadCurrentFile(out string text)
    {
        text = string.Empty;
        if (_file == null) return DialogText.FileNotFound;
        if (IsUntitled) return DialogText.SaveFirst;
        try
        {
            if (!File.Exists(_file.Path)) return DialogText.FileNotFound;
            _file = TextFile.Load(_file.Path);
        }
        catch (Exception e)
        {
            return e.Message;
        }

        EventLog.Add(DialogText.LogFile + "Reload: " + _file.Path);
        text = _file.Text;
        return DialogText.Success;
    }

    /// <summary>
    /// Save the text of the file being edited (called by the editor)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string SaveCurrentFile(string text, string name = "")
    {
        if (_file == null) return DialogText.StatusUnableToSave;
        if (IsUntitled)
        {
            // A blank page: where and as what is asked (as for a new file), and the file is made
            var path = StartMenu.AskNewFile(DialogText.SaveAs, name);
            if (path == null) return DialogText.NotSaved;
            try
            {
                FileIo.CreateNewFile(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return e.Message;
            }

            _file.Path = path;
            AddToHistory(path);
            // The file browser shows it where it was made, the next time
            StartDirectory = System.IO.Path.GetDirectoryName(path) ?? StartDirectory;
            StartSelected = System.IO.Path.GetFileName(path);
            StartPane = 0;
        }

        return _file.Save(text);
    }

    /// <summary>
    /// The file a name given to ":w name" or ":saveas name" is: next to the file being edited, and with its
    /// extension if the name has none
    /// </summary>
    internal static string PathNextToCurrent(string currentPath, string name)
    {
        name = name.Trim();
        if (System.IO.Path.GetExtension(name).Length == 0) name += System.IO.Path.GetExtension(currentPath);
        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(currentPath)) ?? string.Empty;
        return System.IO.Path.Combine(directory, name);
    }

    /// <summary>
    /// ":w name" and ":saveas name": the text being edited saved as a new file next to the file, with the name typed
    /// (and the extension of the file if it has none), the encoding and line endings of the file. A file of that
    /// name is replaced only if the user says so. With saveAs the new file is the one edited from then on; else the
    /// file being edited stays as it is (with its changes not saved). If the file is a favorite, the user is asked
    /// whether the new one goes below it in the favorites
    /// </summary>
    /// <returns>Null if it was saved, else why not (empty: the user gave up)</returns>
    internal static string? SaveCurrentFileAs(string text, string name, bool saveAs)
    {
        if (_file == null || IsUntitled) return DialogText.NoFilePath;
        var original = System.IO.Path.GetFullPath(_file.Path);
        var path = PathNextToCurrent(original, name);
        var fileName = System.IO.Path.GetFileName(path);
        if (Tools.FileNameProblem(fileName) is { } problem) return problem;
        if (FileOperations.SamePath(path, original)) return Lang.T("That is the file being edited (:w saves it)");
        if (Directory.Exists(path)) return DialogText.NameExists;
        var replace = File.Exists(path);
        if (replace)
        {
            ChoiceBox.Forget();
            if (!ChoiceBox.Confirm(DialogText.SaveAs, Lang.F("Replace {0}?", fileName),
                    Lang.T("A file of that name is there: it is replaced by this text."), true))
            {
                return string.Empty;
            }
        }

        var copy = _file.WithPath(path);
        try
        {
            // A new file is made as such first (a file that has come since it was looked for is not emptied)
            if (!replace) FileIo.CreateNewFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
        }

        var status = copy.Save(text);
        if (status != DialogText.Success) return status;
        _lastWrittenPath = path;
        if (saveAs)
        {
            _file = copy;
            AddToHistory(path);
        }

        OfferFavoriteBelow(original, path);
        return null;
    }

    // The files written with ":w name" or ":saveas name" that were put below a favorite (the favorite, and the last
    // put below it): the next one made from the same favorite goes after them, so they keep the order they were made in
    private static readonly List<(string Favorite, string Last)> PutBelow = new();

    // A file written from a favorite: the user is asked whether it goes below the favorite in the favorites (after
    // the files put below it before)
    private static void OfferFavoriteBelow(string original, string path)
    {
        FileFavorites.Load();
        if (!FileFavorites.Contains(original) || FileFavorites.Contains(path)) return;
        ChoiceBox.Forget();
        if (!ChoiceBox.Confirm(Lang.T("Favorites"),
                Lang.F("Add {0} to the favorites, below {1}?", System.IO.Path.GetFileName(path),
                    System.IO.Path.GetFileName(original)), string.Empty, false))
        {
            return;
        }

        var before = PutBelow.FindLastIndex(put => FileOperations.SamePath(put.Favorite, original));
        var after = before >= 0 && FileFavorites.Contains(PutBelow[before].Last) ? PutBelow[before].Last : original;
        var index = FileFavorites.Items.ToList()
            .FindIndex(item => !item.IsFolder && FileOperations.SamePath(item.Path, after));
        if (FileFavorites.AddBelow(path, index) < 0) return;
        PutBelow.Add((original, path));
        var status = FileFavorites.Save();
        if (status != DialogText.Success) _message = status;
    }

    // The last file written with ":w name" or ":saveas name" while the editor was open (empty: none): it is selected
    // in the file browser, or in the favorites when it is one, when the editor is closed
    private static string _lastWrittenPath = string.Empty;

    /// <summary>
    /// What is wrong with a name for a new file next to the file being edited (":new name" in the editor): not a
    /// name of a file (a path is not), or one that is there. Null if it can be made
    /// </summary>
    internal static string? NewFileProblem(string name)
    {
        if (_file == null) return DialogText.NoFilePath;
        if (IsUntitled) return DialogText.SaveFirst;
        if (Tools.FileNameProblem(name) is { } problem) return problem;
        var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_file.Path) ?? string.Empty, name);
        return File.Exists(path) || Directory.Exists(path) ? DialogText.NameExists : null;
    }

    /// <summary>
    /// Make an empty file next to the file being edited (":new name"; NewFileProblem has checked the name)
    /// </summary>
    /// <returns>Null if it was made, else why it was not</returns>
    internal static string? CreateFileNextToCurrent(string name)
    {
        if (_file == null) return DialogText.NoFilePath;
        var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_file.Path) ?? string.Empty, name);
        try
        {
            // Only a new file: one that was made since the name was checked is not emptied (logged)
            FileIo.CreateNewFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
        }

        return null;
    }

    // A project picked in the list of projects is shown in the pane with the focus
    private static void OpenProject()
    {
        if (ProjectsBox.Pick() is { } path && Directory.Exists(path)) ChangeDirectory(path, null);
    }

    // Create a file in the directory shown and open it in the editor. The name is asked for if it is not given; a
    // name without an extension is a Markdown file
    private static void NewFile(string name)
    {
        if (ActivePane.Directory.Length == 0) return;
        name = name.Trim();
        if (name.Length == 0)
        {
            var asked = AskName(DialogText.NewFile, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        name = FileOperations.WithDefaultExtension(name);
        var path = System.IO.Path.Combine(ActivePane.Directory, name);
        var problem = Tools.FileNameProblem(name) ??
                      (File.Exists(path) || Directory.Exists(path) ? DialogText.NameExists : null);
        if (problem != null)
        {
            _message = problem;
            LogOperation("New file", path, _message);
            return;
        }

        try
        {
            // Only a new file: one that has come since it was looked for is not emptied (logged)
            FileIo.CreateNewFile(path);
        }
        catch (Exception e)
        {
            _message = e.Message;
            return;
        }

        Edit(TextFile.Create(path), false);
        LoadItems(name);
    }

    #endregion

    #region Drawing

    private static int _shownActive = -1; // The pane that had the focus when the browser was drawn last
    private static bool _shownDual;

    // The columns of a pane: where it starts and how wide it is. Two panes share the window with a
    // separator between them
    private static (int Left, int Width) PaneArea(int number)
    {
        if (!IsDual) return (0, Init.LineWidth);
        var half = (Init.LineWidth - 1) / 2;
        return number == 0 ? (0, half) : (half + 1, Init.LineWidth - half - 2);
    }

    /// <summary>
    /// Draw the file browser
    /// </summary>
    /// <param name="all">False if only the selection has moved, or the focus has gone to the other pane:
    /// then only the selection pointers, the pane labels and the status are drawn (the whole screen is
    /// still drawn if the list has to scroll)</param>
    private static void Draw(bool all)
    {
        var shown = IsDual ? Panes : new[] { ActivePane };
        foreach (var pane in shown)
        {
            // Keep the selection within the list and the view
            pane.Index = Math.Clamp(pane.Index, 0, Math.Max(0, pane.Items.Count - 1));
            if (pane.Index < pane.Top) pane.Top = pane.Index;
            if (pane.Index >= pane.Top + PageSize) pane.Top = pane.Index - PageSize + 1;
        }

        Tools.HideCursor();
        if (!all && _shownDual == IsDual && shown.All(pane => pane.Top == pane.ShownTop))
        {
            if (IsDual && _shownActive != _active)
            {
                // The focus has moved to the other pane
                for (var number = 0; number < Panes.Length; number++) DrawPaneHeader(number);
            }

            // Move the selection pointers (not in the pane the peek is on)
            for (var number = 0; number < shown.Length; number++)
            {
                if (IsPeekPane(number)) continue;
                var pane = shown[number];
                DrawPointer(number, pane.ShownIndex, false);
                DrawPointer(number, pane.Index, true);
                pane.ShownIndex = pane.Index;
            }

            _shownActive = _active;
            DrawStatus();
            // The peek follows the selection
            DrawPeek(false);
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        // While the place to copy or move to is selected, the label stands out
        Menu.WriteModeLabel(_caption, string.Empty, 0, _pending != null);
        Menu.WriteNameInTopBar();
        if (Unprotected && _mode == BrowseMode.Edit)
        {
            // Blyant's own files can be renamed, moved and deleted (":protect off"): told at the top right
            var marker = " " + Lang.T("UNPROTECTED") + " ";
            Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - TextMetrics.WidthOf(marker) - 1));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.WarningInverted);
            Console.Write(marker);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        for (var number = 0; number < shown.Length; number++)
        {
            var pane = shown[number];
            DrawPaneHeader(number);
            for (var row = 0; row < PageSize && pane.Top + row < pane.Items.Count && !IsPeekPane(number); row++)
            {
                DrawItem(number, pane, pane.Top + row);
            }

            pane.ShownTop = pane.Top;
            pane.ShownIndex = pane.Index;
        }

        if (IsDual)
        {
            // The line between the panes
            var column = PaneArea(1).Left - 1;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(1, column);
            Console.Write('|');
            for (var row = 0; row < PageSize; row++)
            {
                Tools.SetCursorEx(ListTop + row, column);
                Console.Write('|');
            }
        }

        _shownActive = _active;
        _shownDual = IsDual;
        DrawStatus();
        DrawPeek(true);
    }

    // The pane the peek is shown on, instead of its list (the one without the focus)
    private static bool IsPeekPane(int number) => _peeking && IsDual && number != _active;

    // The pane that is drawn as a number (a single pane is always the first)
    private static Pane ShownPane(int number) => IsDual ? Panes[number] : ActivePane;

    private static bool HasFocus(int number) => !IsDual || number == _active;

    // The two lines above the list of a pane: the directory, and a divider that tells which pane has the focus
    private static void DrawPaneHeader(int number)
    {
        var pane = ShownPane(number);
        var (left, width) = PaneArea(number);
        // The pane with the peek is labeled so, with the name of the file it shows, or the path of the directory. The
        // focus is in the peek while Tab has gone into it
        var peek = IsPeekPane(number);
        var focused = peek ? _peekFocused : HasFocus(number) && !_peekFocused;
        // The directory (the end of a long path is shown)
        var room = width - 2;
        var path = peek ? PeekedDirectory() ?? (SelectedItem() is { IsDirectory: false } item ? item.Name : string.Empty)
            : pane.Directory.Length == 0 ? DialogText.Drives : pane.Directory;
        path = TextMetrics.KeepEnd(path, room);
        Tools.SetCursorEx(1, left);
        ConsoleColors.SetColors(focused
            ? ConsoleColors.ColorPreset.HeaderColor
            : ConsoleColors.ColorPreset.Divider);
        Console.Write(" " + path.PadRight(room));
        Tools.SetCursorEx(2, left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', IsDual && number == 0 ? width : Math.Min(width, Init.LineWidth - left)));
        if (!IsDual) return;
        if (number == 0) Console.Write('+');
        // The label of the pane: highlighted on the one with the focus (in the attention color while the place to
        // copy or move to is selected, as the label at the top)
        Tools.SetCursorEx(2, left + 2);
        var label = peek ? Lang.T("PEEK") : Lang.F("PANE {0}", number + 1);
        if (focused)
        {
            ConsoleColors.SetColors(_pending != null
                ? ConsoleColors.ColorPreset.AttentionInverted
                : ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(" " + label + " ");
        }
        else
        {
            Console.Write(" " + label.ToLowerInvariant() + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // A row of the list of a pane: the name, the type (if the pane is wide enough for it) and the size
    private static void DrawItem(int number, Pane pane, int index)
    {
        var item = pane.Items[index];
        var row = index - pane.Top;
        var (left, width) = PaneArea(number);
        DrawPointer(number, index, index == pane.Index);
        var showType = width >= MinWidthForType;
        var type = ItemType(item);
        // In a narrow pane a directory is marked where the size would be
        var size = item.IsDirectory ? (showType ? string.Empty : DialogText.DirectoryMark)
            : FileSizeFormatter.FormatSize(item.Size);
        var sizeEnd = left + width - 1;
        var typeColumn = sizeEnd - 1 - SizeColumnWidth - TypeColumnWidth;
        var nameEnd = showType ? typeColumn - 1 : sizeEnd - SizeColumnWidth - 1;
        var name = Tools.TruncateString(item.Name + (item.IsDirectory && item.Name != ParentName ? "/" : ""),
            Math.Max(4, nameEnd - (left + 3) - 2 - ListNumbers.Width(FileNumbers, pane.Items.Count)), true);
        var nameColors = item.IsDirectory ? ConsoleColors.ColorPreset.HeaderColor : ConsoleColors.ColorPreset.Normal;
        if (item.Path.Length > 0 && pane.Marks.Contains(item.Path))
        {
            // Marked
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write("+");
            ConsoleColors.SetColors(nameColors);
            Console.Write(name);
        }
        else
        {
            ConsoleColors.SetColors(nameColors);
            Console.Write(" " + name);
        }
        if (_mode == BrowseMode.Edit && item.Path.Length > 0 && FileFavorites.Contains(item.Path))
        {
            // A favorite
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
            Console.Write(" *");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (showType)
        {
            Tools.SetCursorEx(ListTop + row, typeColumn);
            Console.Write(type);
        }

        Tools.SetCursorEx(ListTop + row, sizeEnd - size.Length);
        Console.Write(size);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private const int MinWidthForType = 50; // The type column is left out in a pane that is narrower

    private const int TypeColumnWidth = 11; // The width of "[Directory]"
    private const int SizeColumnWidth = 11; // The width of "1,023 bytes"

    // What is shown in the type column: a directory, or the extension of the file ("TXT"). A name that starts with
    // a dot and has no other (".blyant", ".gitignore") is its type, as the info box tells
    private static string ItemType(Item item)
    {
        if (item.IsDirectory) return DialogText.DirectoryMark;

        var extension = System.IO.Path.GetExtension(item.Name);
        if (extension.Length < 2) return string.Empty;
        return Tools.TruncateString(extension[1..].ToUpperInvariant(), TypeColumnWidth, true);
    }

    // Draw (or remove) the selection pointer in front of an item of a pane, leaving the cursor after it (and before
    // it the number of the row, when the numbers are on). In the pane without the focus the pointer is not highlighted,
    // nor its number
    private static void DrawPointer(int number, int index, bool selected)
    {
        var pane = ShownPane(number);
        var row = index - pane.Top;
        if (row < 0 || row >= PageSize || index >= pane.Items.Count) return;
        Tools.SetCursorEx(ListTop + row, PaneArea(number).Left);
        ListNumbers.Write(index, ListNumbers.Width(FileNumbers, pane.Items.Count), selected && HasFocus(number));
        ConsoleColors.SetColors(!selected ? ConsoleColors.ColorPreset.Normal :
            HasFocus(number) ? ConsoleColors.ColorPreset.SelectorInverted : ConsoleColors.ColorPreset.Divider);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected item of each pane, and a message or the keys
    private static void DrawStatus()
    {
        var count = IsDual ? Panes.Length : 1;
        for (var number = 0; number < count; number++)
        {
            var pane = ShownPane(number);
            var (left, width) = PaneArea(number);
            Tools.SetCursorEx(Init.LineCount - 2, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(new string('-', IsDual && number == 0 ? width : Math.Min(width, Init.LineWidth - left)));
            if (IsDual && number == 0) Console.Write('+');
            if (IsPeekPane(number)) continue;
            var status = " " + (pane.Items.Count == 0 ? 0 : pane.Index + 1) + "/" + pane.Items.Count + " ";
            if (pane.Filter.Length > 0) status += "/" + pane.Filter + " ";
            // A count being typed (5j), below the pane it moves in
            if (pane == ActivePane && _listCount.Shown.Length > 0) status = " " + _listCount.Shown + status;
            if (pane.Marks.Count > 0) status = " " + Lang.F("{0} marked", pane.Marks.Count) + " -" + status;
            status = Tools.TruncateString(status, Math.Max(4, width - 6), true);
            Tools.SetCursorEx(Init.LineCount - 2, left + width - status.Length - 2);
            Console.Write(status);
        }

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_message.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_message, Init.LineWidth - 4, true) + " ");
        }
        else
        {
            Menu.WriteKeyHints(_mode switch
            {
                BrowseMode.PickFile => DialogText.FilePickKeys,
                BrowseMode.PickDirectory => DialogText.DirectoryPickKeys,
                _ when _pending != null => (IsDual ? DialogText.FileTargetDualKeys : DialogText.FileTargetKeys)
                    .Replace("{0}", _pending.IsMove ? DialogText.FileMoveHere : DialogText.FileCopyHere),
                // What can be done with the marked files and directories
                _ when ActivePane.Marks.Count > 0 => DialogText.FileMarkedKeys,
                _ when _peeking => DialogText.FilePeekKeys,
                _ => IsDual ? DialogText.FileBrowserDualKeys : DialogText.FileBrowserKeys
            });
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
