namespace Blyant;

/// <summary>
/// The page shown when Blyant starts, in a frame with three tabs (TabFrame): Start, where a file or a project is
/// made or opened (New file, File browser, New project, Open project), Configuration, with the settings on four pages
/// (the sub-tabs General, Layout, Editor and System), and Help (the help, the guides and the about page). Tab goes
/// from page to page. The page is shown again when the file browser is closed, and after a jump.
/// The setting "startmode" tells if the page is shown at startup, or if Blyant goes directly to the file browser.
/// </summary>
internal static class StartMenu
{
    internal enum StartMode
    {
        Select, // Show the start page
        Editor, // Open the file browser
        Blank // Open a blank page (the start page comes when it is closed)
    }

    /// <summary>
    /// What happens when Blyant starts (setting "startmode")
    /// </summary>
    internal static StartMode Mode { get; set; } = StartMode.Select;

    // True at startup: the setting "startmode" is followed (else the page is shown)
    private static bool _starting;

    // The page of the Configuration tab to show first (0 to 3), or -1 for the Start tab
    private static int _showConfiguration = -1;

    // The row of the tabs (below the logo)
    private const int TabRow = 6;

    /// <summary>
    /// When Blyant starts in the file browser: open its favorites list first (setting "startfavorites")
    /// </summary>
    internal static bool StartInFavorites { get; set; }

    /// <summary>
    /// The name of a start mode in the settings file
    /// </summary>
    internal static string SettingName(StartMode mode)
    {
        return mode switch
        {
            StartMode.Editor => "editor",
            StartMode.Blank => "blank",
            _ => "select"
        };
    }

    /// <summary>
    /// The start mode of its name in the settings file ("notes", of Marbles, shows the start page)
    /// </summary>
    internal static StartMode ParseSetting(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "editor" => StartMode.Editor,
            "blank" => StartMode.Blank,
            _ => StartMode.Select
        };
    }

    /// <summary>
    /// Blyant has started: the next time the page is to be shown, the setting "startmode" is followed
    /// </summary>
    internal static void Start()
    {
        _starting = true;
        _showConfiguration = -1;
    }

    /// <summary>
    /// Show a tab the next time the page is shown
    /// </summary>
    /// <param name="configurationPage">Show a page of the Configuration tab first (0 General, 1 Layout, 2 Editor,
    /// 3 Lists, 4 System), or -1 for the Start tab</param>
    internal static void Open(int configurationPage = -1)
    {
        _starting = false;
        _showConfiguration = Math.Clamp(configurationPage, -1, 4);
    }

    /// <summary>
    /// Show the page (called by the main loop). It is left for another place (a jump, see Navigation) or by
    /// exiting Blyant
    /// </summary>
    internal static void Run()
    {
        var animate = Logo.TakeAnimation();
        if (_starting)
        {
            _starting = false;
            if (Mode == StartMode.Editor) FileExplorer.Run(StartInFavorites);
            if (Mode == StartMode.Blank) FileExplorer.EditBlank();
        }

        var start = new ChoicesPage();
        var frame = _frame = new TabFrame(TabRow, () =>
            {
                Logo.DrawLogo(true, animate);
                animate = false;
            },
            new TabItem(() => DialogText.StartTab, start),
            new TabItem(() => DialogText.StartConfiguration, new SettingsPage(0), new SettingsPage(1),
                new SettingsPage(2), new SettingsPage(3), new SettingsPage(4)),
            new TabItem(() => Lang.T("Help"), new HelpPage()))
        {
            Commands = ReadCommand,
            // Ctrl+O: the Jump menu, on every tab and sub-tab, and Ctrl+K: the latest files
            OnJumpKey = () => Blyant.Jump.Open(),
            OnRecentKey = RecentBox.Open
        };
        if (_showConfiguration >= 0) frame.Selected = 1 + _showConfiguration;
        _showConfiguration = -1;
        // The page has nothing that closes it: it is left by a jump or by exiting
        while (true) frame.Run();
    }

    // The frame of the page while it is shown (for the commands that go to a tab)
    private static TabFrame? _frame;

    // ":" on any tab: a command of the start page
    private static TabResult ReadCommand()
    {
        var command = CommandLine.Read(CommandContext.Start);
        return command == null ? TabResult.Redraw : RunCommand(command);
    }

    // The commands of the start page: make or open a file or a project, go to a tab or a page of Configuration,
    // open the Jump menu or a page of Blyant, show the help, or exit
    private static TabResult RunCommand(string command)
    {
        var argument = CommandLine.Argument(command);
        switch (CommandLine.Name(command))
        {
            case "new":
                // ":new name": a new file (in the directory of the file browser)
                NewFile(argument);
                break;
            case "blank":
                // A blank page: the editor at once, the name and the folder asked for when it is saved
                FileExplorer.EditBlank();
                break;
            case "recover":
                // The recovery copies of text that was not saved, to open one
                RecoverUnsaved();
                break;
            case "file":
            case "browse":
            case "open":
                FileExplorer.Run();
                break;
            case "favorites":
            case "favs":
                // The favorites of the file browser at once (closing them returns here, as Favorites does)
                FileExplorer.RunFavorites();
                break;
            case "project":
            case "newproject":
                // ":project name": a new project
                NewProject(argument);
                break;
            case "projects":
            case "proj":
            case "pro":
                OpenProject();
                break;
            case "start":
                ShowPage(0);
                break;
            case "config":
            case "general":
                ShowPage(1);
                break;
            case "layout":
                ShowPage(2);
                break;
            case "editor":
                ShowPage(3);
                break;
            case "lists":
                ShowPage(4);
                break;
            case "system":
                ShowPage(5);
                break;
            case "jump":
            case "ju":
            case "jmp":
                Blyant.Jump.Open();
                break;
            case "book":
                // ":book 3": jump to bookmark 3
                Blyant.Jump.ToBookmark(argument);
                break;
            case "recent":
                // The latest files, to open one (the page drawn first: the box of commands has just been taken away)
                _frame?.Redraw();
                RecentBox.Open();
                break;
            case "splits":
                // A file with another beside it, as it was closed (the page drawn first, as for ":recent")
                _frame?.Redraw();
                if (SplitsBox.Pick() is { } split) Blyant.Jump.ToPlace(new JumpTarget(JumpKind.File, split.Left));
                break;
            case "help":
            case "h":
                // The keys of the start page, without going to the Help tab
                Tools.WriteEditorHelp(Tools.HelpTopic.Start);
                break;
            case "about":
                Actions.ShowAboutPages();
                break;
            case "quick":
            case "guide":
                QuickGuide.Show();
                break;
            case "scheme":
                SchemeEditor.Show();
                break;
            case "logo":
                LogoEditor.Show();
                break;
            case "snippet":
            case "snip":
                ChoiceBox.Forget();
                if (SnippetBox.EnsureFile()) FileExplorer.EditSnippets(0);
                break;
            case "completions":
            case "comp":
                ChoiceBox.Forget();
                FileExplorer.EditCompletions();
                break;
            case "clipboard":
                ClipboardPage.Show();
                break;
            case "log":
                EventLog.Show();
                break;
            case "restart":
                ChangeSetting(Setting.Restart, true);
                break;
            case "reset":
                // As Reset all settings on the System page: asked first
                ChangeSetting(Setting.Reset, true);
                break;
            case "quit":
            case "exit":
            case "q":
                // As Q on the Start tab: asked first
                if (ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false))
                {
                    Tools.ExitApplication();
                }

                break;
            default:
                CommandLine.ShowUnknown(command);
                break;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        return TabResult.Redraw;
    }

    // Go to a page of the frame (0 Start, 1 to 4 the pages of Configuration)
    private static void ShowPage(int page)
    {
        if (_frame != null) _frame.Selected = page;
    }

    // New file: a file made in the directory the file browser was in last, or another (Tab in the box: the places
    // saved, or one picked), and opened in the editor with the file browser behind it. The name is asked for, also
    // when it is given (":new name"), so the folder can be changed; without an extension it is a Markdown file
    private static void NewFile(string name)
    {
        ChoiceBox.Forget();
        var path = AskNewFile(DialogText.NewFile, name);
        if (path == null) return;
        try
        {
            // Only a new file: one that was made in the meantime is not emptied (it is logged, or why it failed)
            FileIo.CreateNewFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ChoiceBox.Message(DialogText.NewFile, e.Message, Path.GetFileName(path));
            return;
        }

        FileExplorer.RunAtFile(path);
    }

    // Recover unsaved text: the recovery copies (of blank pages and of files), the newest first, with the start of
    // the text below the list. Enter opens one (a blank page with its text, or the file, where what to do is
    // asked), D deletes one (asked first)
    private static void RecoverUnsaved()
    {
        while (true)
        {
            ChoiceBox.Forget();
            var copies = Recovery.All();
            if (copies.Count == 0)
            {
                ChoiceBox.Message(DialogText.RecoverUnsaved, DialogText.RecoveryEmpty, string.Empty);
                return;
            }

            var rows = copies.Select(copy => copy.Written.ToLocalTime().ToString("yyyy-MM-dd HH:mm",
                    System.Globalization.CultureInfo.InvariantCulture) + "  " +
                (copy.IsUntitled ? DialogText.Untitled : Path.GetFileName(copy.FilePath))).ToList();
            var extras = new PickListBox.Extras
            {
                PreviewLabel = Lang.T("Text"),
                Key = (key, i) =>
                {
                    if (i < 0 || char.ToUpperInvariant(key.KeyChar) != 'D') return PickListBox.KeyUse.None;
                    if (!ChoiceBox.ConfirmGuarded(DialogText.RecoverUnsaved, Lang.T("Delete the recovered text?"),
                            Lang.T("This cannot be undone")))
                    {
                        return PickListBox.KeyUse.Used;
                    }

                    Recovery.DeleteCopy(copies[i].CopyPath);
                    return PickListBox.KeyUse.Reload;
                }
            };
            var picked = PickListBox.Show(Lang.F("Recover unsaved text ({0})", copies.Count), rows,
                i => (copies[i].IsUntitled ? DialogText.BlankPage : copies[i].FilePath) + "\n" + copies[i].Text, 0,
                Lang.T("j/k Select | Enter Open | D Delete | / Filter | Esc Close"), extras);
            if (picked == PickListBox.Reload) continue;
            if (picked < 0) return;
            FileExplorer.OpenRecoveryCopy(copies[picked]);
            return;
        }
    }

    /// <summary>
    /// Ask for the name and the folder of a new file (New file, and a blank page saved): the name in a box, in the
    /// directory the file browser was in last, or another (Tab: the places saved, or one picked). A name without an
    /// extension is a Markdown file. A name that cannot be used, or is there, is told, and asked for again
    /// </summary>
    /// <param name="caption">The title of the box</param>
    /// <param name="name">The name to start with (can be empty)</param>
    /// <param name="suggested">The folder to start with instead of the one the file browser was in last (the
    /// folder of the favorite selected), or null. It is one of the places Tab shows too</param>
    /// <returns>The full path of the file to make (it is not made), or null if canceled</returns>
    internal static string? AskNewFile(string caption, string name, string? suggested = null)
    {
        if (suggested != null && !Directory.Exists(suggested)) suggested = null;
        var directory = suggested ?? FileExplorer.LastDirectory;
        name = name.Trim();
        while (true)
        {
            var asked = ChoiceBox.AskTextOrTab(caption,
                Lang.F("The file is made in {0}", directory) + "\n" + Lang.T("Tab: choose another folder"),
                DialogText.FileNameColon, name, Lang.T("Enter OK | Tab Another folder | Esc Cancel"), out var tab);
            if (tab)
            {
                // Tab: another folder (the name typed is kept)
                name = asked ?? string.Empty;
                if (PickNewFileFolder(directory, suggested) is { } other) directory = other;
                Tools.ClearScreen();
                ChoiceBox.Forget();
                continue;
            }

            if (asked == null) return null;
            name = asked;
            var file = FileOperations.WithDefaultExtension(name);
            var path = Path.Combine(directory, file);
            var problem = Tools.FileNameProblem(file) ??
                          (File.Exists(path) || Directory.Exists(path) ? DialogText.NameExists : null);
            if (problem == null) return Path.GetFullPath(path);
            // A name that cannot be used (the file is not tried): told, and the name asked for again
            EventLog.Add(DialogText.LogFile + "New: " + path + " (" + problem + ")");
            ChoiceBox.Message(caption, problem, file);
            Tools.ClearScreen();
            ChoiceBox.Forget();
        }
    }

    /// <summary>
    /// The places a new file can be made in, besides one picked: the directories of the two panes of the file
    /// browser and the folder picked last (and first, the folder of the favorite selected,
    /// when the file is made from the favorites). Each once, only those that are there, in this order
    /// </summary>
    internal static List<(string Directory, string Label)> NewFilePlaces(string? favorite = null)
    {
        var places = new List<(string Directory, string Label)>();

        void Add(string directory, string label)
        {
            if (directory.Length == 0 || !Directory.Exists(directory)) return;
            directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            if (directory.Length == 0 || places.Any(place => FileOperations.SamePath(place.Directory, directory))) return;
            places.Add((directory, label));
        }

        if (favorite != null) Add(favorite, Lang.T("favorite"));
        Add(FileExplorer.StartDirectory, Lang.T("pane 1"));
        Add(FileExplorer.StartDirectory2, Lang.T("pane 2"));
        // The folder picked (or, ending with a separator, the one the picking was canceled in)
        var picked = FileExplorer.PickPlace;
        if (picked.Length > 0) Add(Path.TrimEndingDirectorySeparator(picked), Lang.T("picked last"));
        return places;
    }

    // Tab in the box of a new file: the places saved in a list (1-9, j/k gg G and Enter), or B to pick one in the
    // file browser. With one place or none, the file browser at once. Returns the directory, or null (Esc: back)
    private static string? PickNewFileFolder(string current, string? favorite)
    {
        var places = NewFilePlaces(favorite);
        if (places.Count > 1)
        {
            // (it covers the box of the name, which is drawn again after it)
            var keys = places.Select((_, i) => (i + 1).ToString()).Append("B").Append("Esc").ToList();
            // The path keeps its end (where it is), and after it what the place is
            var room = Math.Max(16, Init.LineWidth - 26 - places.Max(place => TextMetrics.WidthOf(place.Label)));
            var choices = places.Select(place => TextMetrics.KeepEnd(place.Directory, room) + "  (" + place.Label + ")")
                .Append(Lang.T("Browse...")).Append(Lang.T("Back")).ToList();
            var selected = Math.Max(0, places.FindIndex(place => FileOperations.SamePath(place.Directory, current)));
            var choice = ChoiceBox.Pick(Lang.T("Where to make the file"), Lang.T("The folder of the new file"),
                string.Empty, choices, keys, selected, false);
            if (choice < 0 || choice == places.Count + 1) return null;
            if (choice < places.Count) return places[choice].Directory;
        }

        // Browse: picked in the file browser (where a folder was picked last)
        ChoiceBox.Forget();
        return FileExplorer.PickDirectory(Lang.T("Where to make the file"));
    }

    // New project: a directory made where the user picks (the name is asked for if it is not given), shown in the
    // file browser
    private static void NewProject(string name)
    {
        if (ProjectsBox.NewProject(name) is { } path) FileExplorer.RunAtDirectory(path);
    }

    // Open project: the projects in a list (with what each holds), the one picked shown in the file browser
    private static void OpenProject()
    {
        if (ProjectsBox.Pick() is { } path) FileExplorer.RunAtDirectory(path);
    }

    // The kind of new files and blank pages: picked in a list (1-9, then letters as in the list of languages, j/k
    // gg G and Enter; Esc: as it is)
    private static void PickFileType()
    {
        ChoiceBox.Forget();
        var types = FileOperations.FileTypes;
        var choices = types.Select(type => FileOperations.FileTypeName(type.Extension) + " (" + type.Extension + ")")
            .ToList();
        // A key is one character: 1 to 9, then letters (not those that move in the box: g h j k l q)
        const string more = "ABCDEFIMNOPRSTUVWXYZ";
        var keys = types.Select((_, i) => i < 9 ? (i + 1).ToString() : more[i - 9].ToString()).ToList();
        var selected = Math.Max(0, types.ToList().FindIndex(type => type.Extension == FileOperations.DefaultExtension));
        var picked = ChoiceBox.Pick(Lang.T("New files and blank pages"),
            Lang.T("Assumed file type (redefined upon saving)"), string.Empty, choices,
            keys, selected, false);
        if (picked < 0) return;
        FileOperations.DefaultExtension = types[picked].Extension;
        FileIo.SaveSettings();
    }

    private enum Setting
    {
        StartSelect,
        StartEditor,
        StartFavorites, // Shown below StartEditor when it is chosen
        StartBlank,
        FileType,
        NewLineEndings,
        DeleteToTrash,
        CommandList,
        Monochrome,
        Language,
        ColorScheme,
        AutoSize,
        Size,
        Logo,
        LogoAnimation,
        Corners,
        LineNumbers,
        WordWrap,
        Whitespace,
        MarksApostrophe,
        ScrollOff,
        Clipboard,
        SyntaxColors,
        Spell,
        Snippets,
        AutoComplete,
        Completions,
        Recovery,
        FileNumbers,
        FavoriteNumbers,
        HistoryNumbers,
        HotlistNumbers,
        SearchNumbers,
        FavoritePanes,
        FavoritePeek, // Below FavoritePanes: it is in the panes
        BookmarkNumbers,
        BookmarkPanes,
        BookmarkPeek, // Below BookmarkPanes
        Hints,
        Reset,
        EditSettings,
        Restart,
        Info,
        ViewLog
    }

    // The settings shown, in the order of the pages: some are only shown when they can be used
    private static List<Setting> Settings()
    {
        // General: what happens at startup, new files, deleting and the language
        var settings = new List<Setting> { Setting.StartSelect, Setting.StartEditor };
        if (Mode == StartMode.Editor) settings.Add(Setting.StartFavorites);
        settings.Add(Setting.StartBlank);
        settings.Add(Setting.FileType);
        settings.Add(Setting.NewLineEndings);
        settings.Add(Setting.DeleteToTrash);
        settings.Add(Setting.CommandList);
        settings.Add(Setting.Language);
        // Layout
        settings.Add(Setting.AutoSize);
        if (!Init.AutoSize) settings.Add(Setting.Size);
        settings.Add(Setting.Monochrome);
        if (!ConsoleColors.Monochrome) settings.Add(Setting.ColorScheme);
        settings.Add(Setting.Logo);
        settings.Add(Setting.LogoAnimation);
        settings.Add(Setting.Corners);
        // Editor
        settings.Add(Setting.LineNumbers);
        settings.Add(Setting.WordWrap);
        settings.Add(Setting.Whitespace);
        settings.Add(Setting.MarksApostrophe);
        settings.Add(Setting.ScrollOff);
        settings.Add(Setting.Clipboard);
        settings.Add(Setting.SyntaxColors);
        settings.Add(Setting.Spell);
        settings.Add(Setting.Snippets);
        settings.Add(Setting.AutoComplete);
        settings.Add(Setting.Completions);
        settings.Add(Setting.Recovery);
        // Lists: the numbers of the rows of each list, in the order of the lists, then the panes of the favorites of
        // the file browser and of the bookmarks of the Jump menu
        settings.Add(Setting.FileNumbers);
        settings.Add(Setting.FavoriteNumbers);
        settings.Add(Setting.HistoryNumbers);
        settings.Add(Setting.HotlistNumbers);
        settings.Add(Setting.SearchNumbers);
        settings.Add(Setting.BookmarkNumbers);
        settings.Add(Setting.FavoritePanes);
        settings.Add(Setting.FavoritePeek);
        settings.Add(Setting.BookmarkPanes);
        settings.Add(Setting.BookmarkPeek);
        // System
        settings.Add(Setting.Hints);
        settings.Add(Setting.Reset);
        settings.Add(Setting.EditSettings);
        settings.Add(Setting.Restart);
        settings.Add(Setting.Info);
        settings.Add(Setting.ViewLog);
        return settings;
    }

    private static string Check(bool isChecked) => isChecked ? "[x] " : "[ ] ";

    private static string SettingText(Setting setting)
    {
        const string indent = "    ";
        return setting switch
        {
            Setting.StartSelect => Check(Mode == StartMode.Select) + DialogText.StartConfigSelect,
            Setting.StartEditor => Check(Mode == StartMode.Editor) + DialogText.StartConfigEditor,
            Setting.StartBlank => Check(Mode == StartMode.Blank) + Lang.T("Start on a blank page"),
            Setting.FileType => indent + Lang.F("New files and blank pages: {0} (Enter: Change)",
                FileOperations.FileTypeName(FileOperations.DefaultExtension) + " (" + FileOperations.DefaultExtension + ")"),
            Setting.NewLineEndings => indent + Lang.F("Line endings of new files: {0} (Enter: Change)",
                TextFile.NewFileLineEndings switch
                {
                    "lf" => "LF",
                    "crlf" => "CRLF",
                    _ => Lang.F("System ({0})", TextFile.LabelOf(Environment.NewLine))
                }),
            Setting.StartFavorites => indent + Check(StartInFavorites) + Lang.T("Open favorites list first"),
            Setting.DeleteToTrash => Check(FileOperations.DeleteToTrash) + Lang.T("Delete to trash (:trash in file browser)"),
            Setting.CommandList => Check(Blyant.CommandList.Enabled) + Lang.T("List commands above command line"),
            Setting.Hints => Check(Blyant.Hints.Enabled) + Lang.T("Show hints on start page"),
            Setting.Monochrome => Check(ConsoleColors.Monochrome) + DialogText.ConfigMonochrome,
            Setting.Language => indent + DialogText.ConfigLanguage + Lang.CurrentName,
            Setting.ColorScheme => indent + Lang.F("Color scheme: {0} (Enter: Select/Edit)",
                ColorSchemes.DisplayName(ConsoleColors.Current.Name)),
            Setting.Logo => indent + Lang.F("Logo: {0} (Enter: Select/Edit)", Blyant.Logo.Current.Name),
            Setting.LogoAnimation => indent + Lang.F("Logo animation: {0}", AnimationName(Blyant.Logo.Animation)),
            Setting.Corners => Check(AsciiStyles.GetAsciiStyle() == 1) + Lang.T("Classic ASCII corners"),
            Setting.AutoSize => Check(Init.AutoSize) + DialogText.ConfigAutoSize,
            Setting.Size => indent + DialogText.ConfigSize + Init.LineWidth + " x " + Init.LineCount +
                            DialogText.ConfigSizeNote,
            Setting.LineNumbers => Check(TextArea.LineNumbers) + DialogText.ConfigLineNumbers,
            Setting.WordWrap => Check(TextArea.WordWrap) + Lang.T("Wrap long lines"),
            Setting.Whitespace => Check(TextArea.ShowWhitespace) + Lang.T("Show blank space as . > $ (:whitespace)"),
            Setting.MarksApostrophe => Check(EditorEngine.MarksApostrophe) + Lang.T("Marks: ' jumps to the exact place"),
            Setting.ScrollOff => Check(TextArea.ScrollOff) +
                                 Lang.F("Keep {0} lines around cursor", TextArea.ScrollOffLines),
            Setting.Clipboard => indent + Lang.T("Clipboard and paste bin (Enter: Change)"),
            Setting.SyntaxColors => Check(SyntaxHighlighter.ColorFiles) + Lang.T("Syntax highlighting"),
            Setting.Spell => indent + SpellText(),
            Setting.Snippets => indent + Lang.T("Snippet editor (Enter: Open)"),
            Setting.AutoComplete => Check(Blyant.Completions.Enabled) + Lang.T("Complete words while typing"),
            Setting.Completions => indent + Lang.T("Completions editor (Enter: Open)"),
            Setting.Recovery => Check(Blyant.Recovery.Enabled) + Lang.T("Recovery copies of unsaved text (:recovery)"),
            Setting.FileNumbers => Check(FileExplorer.FileNumbers) + Lang.T("Line numbers in file browser"),
            Setting.FavoriteNumbers => Check(FileExplorer.FavoriteNumbers) + Lang.T("Line numbers in Favorites"),
            Setting.HistoryNumbers => Check(FileExplorer.HistoryNumbers) + Lang.T("Line numbers in History"),
            Setting.HotlistNumbers => Check(FileExplorer.HotlistNumbers) + Lang.T("Line numbers in Hotlist"),
            Setting.SearchNumbers => Check(FileExplorer.SearchNumbers) + Lang.T("Line numbers in Search results"),
            Setting.FavoritePanes => Check(FileExplorer.FavoritePanes) + Lang.T("Panes in Favorites (note and peek)"),
            Setting.FavoritePeek => indent + Check(FileExplorer.FavoritePeek) + Lang.T("Peek in Favorites"),
            Setting.BookmarkNumbers => Check(JumpMenu.BookmarkNumbers) + Lang.T("Line numbers in Bookmarks"),
            Setting.BookmarkPanes => Check(JumpMenu.BookmarkPanes) + Lang.T("Panes in Bookmarks (note and peek)"),
            Setting.BookmarkPeek => indent + Check(JumpMenu.BookmarkPeek) + Lang.T("Peek in Bookmarks"),
            Setting.Info => indent + DialogText.FileInfo,
            Setting.EditSettings => indent + Lang.F("Edit {0}", FileIo.SettingsFileName),
            Setting.Restart => indent + DialogText.ConfigRestart,
            Setting.ViewLog => indent + DialogText.ConfigViewLog,
            _ => indent + DialogText.ConfigReset
        };
    }

    // The languages of the spell checker (":spell" turns it on in the editor), or that there are no spell files
    private static string SpellText()
    {
        return SpellChecker.AvailableLanguages().Count == 0
            ? Lang.T("Spell checker: no spell files")
            : Lang.F("Spell checker: {0}", SpellChecker.Languages);
    }

    // The settings are shown on five pages: General, Layout, Editor, Lists and System
    private static int PageOf(Setting setting)
    {
        return setting switch
        {
            Setting.StartSelect or Setting.StartEditor or Setting.StartFavorites or Setting.StartBlank
                or Setting.FileType or Setting.NewLineEndings or Setting.DeleteToTrash or Setting.CommandList
                or Setting.Language => 0,
            Setting.AutoSize or Setting.Size or Setting.Monochrome or Setting.ColorScheme or Setting.Logo
                or Setting.LogoAnimation or Setting.Corners => 1,
            Setting.LineNumbers or Setting.WordWrap or Setting.Whitespace or Setting.MarksApostrophe or Setting.ScrollOff or Setting.Clipboard
                or Setting.SyntaxColors or Setting.Spell or Setting.Snippets or Setting.AutoComplete
                or Setting.Completions or Setting.Recovery => 2,
            Setting.FileNumbers or Setting.FavoriteNumbers or Setting.HistoryNumbers or Setting.HotlistNumbers
                or Setting.SearchNumbers or Setting.BookmarkNumbers or Setting.FavoritePanes or Setting.FavoritePeek
                or Setting.BookmarkPanes or Setting.BookmarkPeek => 3,
            _ => 4
        };
    }

    // In the language in use
    private static string PageTitle(int page)
    {
        return page switch
        {
            0 => Lang.T("General"),
            1 => Lang.T("Layout"),
            2 => Lang.T("Editor"),
            3 => Lang.T("Lists"),
            _ => Lang.T("System")
        };
    }

    // A blank row is shown above these settings when there is room: they start a group of their page
    private static bool StartsGroup(Setting setting) =>
        setting is Setting.DeleteToTrash or Setting.FavoritePanes or Setting.BookmarkPanes;

    // A setting that has no effect as the settings are now (the peek, while its panes are off): shown dimmed
    private static bool IsDimmed(Setting setting) =>
        setting == Setting.FavoritePeek && !FileExplorer.FavoritePanes ||
        setting == Setting.BookmarkPeek && !JumpMenu.BookmarkPanes;

    // Pick the languages of the spell checker from the ones there is a spell file for: Enter turns one on or
    // off, until the box is closed with Esc
    private static void PickSpellLanguages()
    {
        var available = SpellChecker.AvailableLanguages();
        ChoiceBox.Forget();
        if (available.Count == 0)
        {
            ChoiceBox.Message(DialogText.CaptionsSpell,
                DialogText.SpellFileNotFound(Path.GetFullPath(SpellChecker.DirectoryName)), string.Empty);
            return;
        }

        var keys = Enumerable.Range(1, available.Count).Select(n => n <= 9 ? n.ToString() : " ").ToArray();
        var selected = 0;
        while (true)
        {
            var used = SpellChecker.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var choices = available.Select(language =>
                Check(used.Contains(language, StringComparer.OrdinalIgnoreCase)) + language).ToArray();
            var picked = ChoiceBox.Pick(DialogText.CaptionsSpell, Lang.T("The languages of the spell checker"),
                Lang.T("Enter turns a language on or off. Esc closes."), choices, keys, selected, false);
            if (picked < 0) return;
            selected = picked;
            SpellChecker.Languages = SpellChecker.ToggleLanguage(available, SpellChecker.Languages, available[picked]);
            FileIo.SaveSettings();
        }
    }

    // The speeds of the sweep of the logo, as they are listed
    private static readonly Logo.AnimationSpeed[] AnimationSpeeds =
    {
        Blyant.Logo.AnimationSpeed.Slow, Blyant.Logo.AnimationSpeed.Fast, Blyant.Logo.AnimationSpeed.VeryFast,
        Blyant.Logo.AnimationSpeed.Off
    };

    // In the language in use
    private static string AnimationName(Logo.AnimationSpeed speed)
    {
        return speed switch
        {
            Blyant.Logo.AnimationSpeed.Slow => Lang.T("Slow"),
            Blyant.Logo.AnimationSpeed.VeryFast => Lang.T("Very fast"),
            Blyant.Logo.AnimationSpeed.Off => Lang.T("Off"),
            _ => Lang.T("Fast")
        };
    }

    // Pick how fast the logo is swept in (or that it is not) from a list in a box
    private static void PickLogoAnimation()
    {
        ChoiceBox.Forget();
        var picked = ChoiceBox.Pick(Lang.T("Logo animation"), Lang.T("How fast the logo is swept in on the start page"),
            string.Empty, AnimationSpeeds.Select(AnimationName).ToArray(), new[] { "1", "2", "3", "4" },
            Math.Max(0, Array.IndexOf(AnimationSpeeds, Blyant.Logo.Animation)), false);
        if (picked < 0) return;
        Blyant.Logo.Animation = AnimationSpeeds[picked];
        FileIo.SaveSettings();
    }

    // Where Blyant is: its version, and the directories of the program and of the data
    private static void ShowInfo()
    {
        ChoiceBox.Forget();
        ChoiceBox.ShowRows(DialogText.FileInfo, new List<(string Label, string Value)>
        {
            (Lang.T("Version:"), DialogText.ApplicationName + " " + DialogText.ApplicationVersion),
            (Lang.T("Program:"), Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory))),
            (Lang.T("Data:"), Path.GetFullPath(FileIo.GetDataDirectoryName())),
            (Lang.T("Settings:"), Path.Combine(Path.GetFullPath(FileIo.GetDataDirectoryName()), FileIo.SettingsFileName))
        });
    }

    /// <summary>
    /// The Start tab: make or open a file or a project, the latest files, the favorites, the Jump menu, and exit
    /// </summary>
    private sealed class ChoicesPage : ITabPage
    {
        private int _selected;

        private enum Choice
        {
            NewFile,
            BlankPage,
            Recover, // Only while there are recovery copies
            FileBrowser,
            NewProject,
            OpenProject,
            Recent,
            Favorites,
            Jump,
            Exit
        }

        // The choices shown: all, but Recover only while there are recovery copies (looked at when the page is
        // drawn in full)
        private Choice[] _shown = Shown();

        private static Choice[] Shown()
        {
            var recover = Recovery.All().Count > 0;
            return Enum.GetValues<Choice>().Where(choice => choice != Choice.Recover || recover).ToArray();
        }

        // The keys that open a choice at once (the same in all languages; the numbers only select). A new file: N,
        // a blank page: B, recovering unsaved text: R, the file browser: F, a new project: P, the projects: O, the latest
        // files: E (and Ctrl+K), the favorites: V, the Jump menu: M (and Ctrl+O, as everywhere; j and k move), exit: Q
        // (asks)
        private static string KeysOf(Choice choice) => choice switch
        {
            Choice.NewFile => "N",
            Choice.BlankPage => "B",
            Choice.Recover => "R",
            Choice.FileBrowser => "F",
            Choice.NewProject => "P",
            Choice.OpenProject => "O",
            Choice.Favorites => "V",
            Choice.Recent => "E",
            Choice.Jump => "M",
            _ => "Q"
        };

        // In the language in use
        private static string TextOf(Choice choice) => choice switch
        {
            Choice.NewFile => DialogText.NewFile,
            Choice.BlankPage => DialogText.BlankPage,
            Choice.Recover => DialogText.RecoverUnsaved,
            Choice.FileBrowser => DialogText.FileBrowser,
            Choice.NewProject => DialogText.NewProject,
            Choice.OpenProject => DialogText.OpenProject,
            Choice.Favorites => Lang.T("Favorites"),
            Choice.Recent => Lang.T("Recent files"),
            Choice.Jump => DialogText.StartJump,
            _ => DialogText.StartExit
        };

        public string Title => DialogText.StartTab;
        public string Keys => DialogText.StartKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        public void Draw(TabArea area, bool full)
        {
            if (full)
            {
                // (a choice that comes or goes keeps the selection on the same choice)
                var current = _shown[Math.Clamp(_selected, 0, _shown.Length - 1)];
                _shown = Shown();
                _selected = Math.Max(0, Array.IndexOf(_shown, current));
            }

            _selected = Math.Clamp(_selected, 0, _shown.Length - 1);
            if (full)
            {
                for (var i = 0; i < _shown.Length; i++)
                {
                    // The number, the key that opens the choice at once, and the choice
                    Tools.SetCursorEx(area.Top + i, area.Left + 3);
                    // (the tenth is 0)
                    Console.Write(Tools.TruncateString((i + 1) % 10 + " " + KeysOf(_shown[i]) + " .... " +
                                                       TextOf(_shown[i]), area.Width - 4, true));
                }

                // The row of a choice that has gone (Recover) is cleared
                Tools.SetCursorEx(area.Top + _shown.Length, area.Left);
                Console.Write(new string(' ', Math.Max(0, area.Width - 1)));
                _hintShown = Hints.Enabled && DrawHint(area);
            }

            for (var i = 0; i < _shown.Length; i++)
            {
                Tools.SetCursorEx(area.Top + i, area.Left);
                // The pointer is highlighted as in the other lists
                if (i == _selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == _selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        private bool _hintShown; // A hint is shown: X hides the hints

        // The hint, in a box to the right of the menu (none if there is no room for it). True if it is shown
        private bool DrawHint(TabArea area)
        {
            var menu = _shown.Max(choice => TextMetrics.WidthOf((_shown.Length + " " + KeysOf(choice) + " .... " +
                                                                  TextOf(choice))));
            var left = area.Left + 3 + menu + 4;
            var width = area.Left + area.Width - 2 - left;
            var inner = width - 4;
            if (inner < MinHintWidth) return false;
            // (the next hint, the first time the page is shown after Blyant has started)
            Hints.AdvanceOnce();
            var (text, number) = Hints.Current();
            var lines = TextMetrics.Wrap(text, inner);
            if (lines.Count + 2 > area.Height) return false;
            var title = Lang.F("Hint {0} of {1}", number, Hints.All().Length);
            var hide = Lang.T("Hide hints");
            var corner = AsciiStyles.GetTopCorner();
            var bottomCorner = AsciiStyles.GetBottomCorner();
            // The top, with the number of the hint
            Tools.SetCursorEx(area.Top, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(corner + "-- ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            Console.Write(title);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" " + new string('-', Math.Max(0, width - 6 - TextMetrics.WidthOf(title))) + corner);
            // The text
            for (var i = 0; i < lines.Count; i++)
            {
                Tools.SetCursorEx(area.Top + 1 + i, left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write("| ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(lines[i] + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(lines[i]))));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(" |");
            }

            // The bottom, with the key that hides the hints (the key is the same in all languages)
            Tools.SetCursorEx(area.Top + 1 + lines.Count, left);
            Console.Write(bottomCorner + new string('-', Math.Max(0, width - 6 - TextMetrics.WidthOf(hide))) + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            Console.Write("X"); // English: a key
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" " + hide + " " + bottomCorner);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            return true;
        }

        // The narrowest text of a hint (a narrower window has no hint)
        private const int MinHintWidth = 24;

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var count = _shown.Length;
            _selected = Math.Clamp(_selected, 0, count - 1);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.L or ConsoleKey.RightArrow) return Open(_shown[_selected]);
            // The keys of the choices open them at once
            var direct = key.Key switch
            {
                ConsoleKey.N => Choice.NewFile,
                ConsoleKey.B => Choice.BlankPage,
                ConsoleKey.R when _shown.Contains(Choice.Recover) => Choice.Recover,
                ConsoleKey.F => Choice.FileBrowser,
                ConsoleKey.P => Choice.NewProject,
                ConsoleKey.O => Choice.OpenProject,
                ConsoleKey.V => Choice.Favorites,
                ConsoleKey.M => Choice.Jump,
                ConsoleKey.E => Choice.Recent,
                _ => (Choice?)null
            };
            if (direct is { } choice && (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                _selected = Array.IndexOf(_shown, choice);
                return Open(choice);
            }

            if (key.Key == ConsoleKey.X && _hintShown &&
                (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                // The hints are hidden (Configuration, System: shown again)
                Hints.Enabled = false;
                FileIo.SaveSettings();
                return TabResult.Redraw;
            }

            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                if (ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false))
                {
                    Tools.ExitApplication();
                }

                return TabResult.Redraw;
            }

            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                _selected = (_selected + 1) % count;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _selected = (_selected + count - 1) % count;
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < count)
            {
                _selected = key.KeyChar - '1';
            }
            else if (key.KeyChar == '0' && count >= 10)
            {
                // The tenth (Exit, when Recover is shown)
                _selected = 9;
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _selected = count - 1;
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                _selected = 0;
            }

            return TabResult.None;
        }

        // Open a choice. A file or a project opens the file browser, which comes back here when it is closed
        private static TabResult Open(Choice choice)
        {
            switch (choice)
            {
                case Choice.NewFile:
                    NewFile(string.Empty);
                    break;
                case Choice.BlankPage:
                    FileExplorer.EditBlank();
                    break;
                case Choice.Recover:
                    RecoverUnsaved();
                    break;
                case Choice.FileBrowser:
                    FileExplorer.Run();
                    break;
                case Choice.NewProject:
                    NewProject(string.Empty);
                    break;
                case Choice.OpenProject:
                    OpenProject();
                    break;
                case Choice.Favorites:
                    // Closing them returns here
                    FileExplorer.RunFavorites();
                    break;
                case Choice.Jump:
                    // A place that is opened on top of the page returns here; the others leave it
                    Blyant.Jump.Open();
                    break;
                case Choice.Recent:
                    // The file picked is opened as a jump (closing the box returns here)
                    RecentBox.Open();
                    break;
                default:
                    // Exit without asking
                    Tools.ExitApplication();
                    break;
            }

            return TabResult.Redraw;
        }
    }

    /// <summary>
    /// The Help tab: the help (all the keys), the quick guide, the short guides and the about page
    /// </summary>
    private sealed class HelpPage : ITabPage
    {
        private int _selected;

        // In the language in use
        private static string[] Choices =>
            new[] { Lang.T("Help") }.Concat(QuickGuide.All.Select(QuickGuide.Title)).Append(Lang.T("About")).ToArray();

        public string Title => Lang.T("Help");
        public string Keys => DialogText.StartHelpKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        public void Draw(TabArea area, bool full)
        {
            var choices = Choices;
            if (full)
            {
                for (var i = 0; i < choices.Length; i++)
                {
                    Tools.SetCursorEx(area.Top + i, area.Left + 3);
                    Console.Write(Tools.TruncateString((i + 1) + " .... " + choices[i], area.Width - 4, true));
                }
            }

            for (var i = 0; i < choices.Length; i++)
            {
                Tools.SetCursorEx(area.Top + i, area.Left);
                if (i == _selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == _selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var count = Choices.Length;
            // Back to the Start tab
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return TabResult.FirstTab;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow)
            {
                if (_selected == 0)
                {
                    // As ":help": all the keys
                    Tools.WriteEditorHelp();
                }
                else if (_selected <= QuickGuide.All.Length)
                {
                    QuickGuide.Show(QuickGuide.All[_selected - 1]);
                }
                else
                {
                    Actions.ShowAboutPages();
                }

                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
                return TabResult.Redraw;
            }

            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                _selected = (_selected + 1) % count;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _selected = (_selected + count - 1) % count;
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < count)
            {
                _selected = key.KeyChar - '1';
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _selected = count - 1;
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                _selected = 0;
            }

            return TabResult.None;
        }
    }

    /// <summary>
    /// A page of the Configuration tab. A change is saved in the settings file at once
    /// </summary>
    private sealed class SettingsPage : ITabPage
    {
        private readonly int _page;
        private Setting? _selected; // Kept by the setting, as rows come and go (the size, the color scheme)

        // A page with more settings than rows scrolls: the first setting shown, and the one drawn last time
        private int _scroll;
        private int _shownScroll = -1;

        internal SettingsPage(int page)
        {
            _page = page;
        }

        public string Title => PageTitle(_page);
        public string Keys => DialogText.StartConfigKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        private List<Setting> PageSettings => Settings().Where(setting => PageOf(setting) == _page).ToList();

        private int SelectedIndex(List<Setting> settings) =>
            _selected is { } selected ? Math.Max(0, settings.IndexOf(selected)) : 0;

        public void Draw(TabArea area, bool full)
        {
            var settings = PageSettings;
            var selected = SelectedIndex(settings);
            // A blank row above the groups of the page, when there is room for them
            var groups = settings.Count(StartsGroup);
            var gaps = settings.Count + groups <= area.Height;
            // More settings than rows: the page scrolls to keep the selected one shown, with "^" and "v" at the right
            // while there are more above or below (as in the lists of boxes)
            var rows = Math.Max(1, area.Height);
            var scrolls = settings.Count > rows;
            if (!scrolls) _scroll = 0;
            else if (selected < _scroll) _scroll = selected;
            else if (selected >= _scroll + rows) _scroll = selected - rows + 1;
            _scroll = Math.Clamp(_scroll, 0, Math.Max(0, settings.Count - rows));
            // Scrolled: the rows are written again
            if (_scroll != _shownScroll) full = true;
            _shownScroll = _scroll;
            var last = Math.Min(settings.Count, _scroll + rows);
            int Row(int i) => area.Top + i - _scroll + (gaps ? settings.Take(i + 1).Count(StartsGroup) : 0);
            if (full)
            {
                // The settings without a check box are indented to line up with the names of the ones with a
                // check box. On a page without check boxes they line up with the pointer
                var texts = settings.Select(SettingText).ToList();
                var checkBoxes = texts.Any(text => text.StartsWith('['));
                // (room for the marks at the right of a page that scrolls; the rows are written to their end)
                var room = area.Width - (scrolls ? 6 : 4);
                for (var i = _scroll; i < last; i++)
                {
                    Tools.SetCursorEx(Row(i), area.Left + 2);
                    var text = " " + Tools.TruncateString(checkBoxes ? texts[i] : texts[i].TrimStart(), room, true);
                    if (IsDimmed(settings[i])) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    Console.Write(text);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    if (scrolls) Console.Write(new string(' ', Math.Max(0, room + 1 - TextMetrics.WidthOf(text))));
                }

                if (scrolls)
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    Tools.SetCursorEx(area.Top, area.Left + area.Width - 2);
                    Console.Write(_scroll > 0 ? "^" : " ");
                    Tools.SetCursorEx(area.Top + rows - 1, area.Left + area.Width - 2);
                    Console.Write(last < settings.Count ? "v" : " ");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }
            }

            for (var i = _scroll; i < last; i++)
            {
                Tools.SetCursorEx(Row(i), area.Left);
                if (i == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var settings = PageSettings;
            var selected = SelectedIndex(settings);
            var count = settings.Count;
            var setting = settings[selected];
            // Back to the Start tab
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return TabResult.FirstTab;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                _selected = settings[(selected + 1) % count];
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _selected = settings[(selected + count - 1) % count];
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < count)
            {
                _selected = settings[key.KeyChar - '1'];
            }
            else if (key.KeyChar == '0' && count >= 10)
            {
                // The tenth
                _selected = settings[9];
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _selected = settings[count - 1];
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                _selected = settings[0];
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar &&
                     setting == Setting.ColorScheme)
            {
                // The color schemes: select and edit them
                SchemeEditor.Show();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar &&
                     setting == Setting.Logo)
            {
                // The logos: select and edit them
                LogoEditor.Show();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar && setting == Setting.Spell)
            {
                // Pick the languages of the spell checker in a box
                PickSpellLanguages();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar && setting == Setting.Language)
            {
                // Pick the language in a box
                Actions.PickLanguage();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            {
                ChangeSetting(setting, true);
                _selected = setting;
                return TabResult.Redraw;
            }

            return TabResult.None;
        }
    }

    private static void ChangeSetting(Setting setting, bool next)
    {
        switch (setting)
        {
            case Setting.StartSelect:
                Mode = StartMode.Select;
                break;
            case Setting.StartEditor:
                Mode = StartMode.Editor;
                break;
            case Setting.StartBlank:
                Mode = StartMode.Blank;
                break;
            case Setting.FileType:
                // Saves the settings itself
                PickFileType();
                return;
            case Setting.StartFavorites:
                StartInFavorites = !StartInFavorites;
                break;
            case Setting.NewLineEndings:
                // System, LF, CRLF, and round again
                TextFile.NewFileLineEndings = TextFile.NewFileLineEndings switch
                {
                    "system" => "lf",
                    "lf" => "crlf",
                    _ => "system"
                };
                break;
            case Setting.Whitespace:
                TextArea.ShowWhitespace = !TextArea.ShowWhitespace;
                break;
            case Setting.MarksApostrophe:
                EditorEngine.MarksApostrophe = !EditorEngine.MarksApostrophe;
                break;
            case Setting.DeleteToTrash:
                FileOperations.DeleteToTrash = !FileOperations.DeleteToTrash;
                break;
            case Setting.CommandList:
                Blyant.CommandList.Enabled = !Blyant.CommandList.Enabled;
                break;
            case Setting.Hints:
                Blyant.Hints.Enabled = !Blyant.Hints.Enabled;
                break;
            case Setting.Recovery:
                Blyant.Recovery.Enabled = !Blyant.Recovery.Enabled;
                break;
            case Setting.FileNumbers:
                FileExplorer.FileNumbers = !FileExplorer.FileNumbers;
                break;
            case Setting.FavoriteNumbers:
                FileExplorer.FavoriteNumbers = !FileExplorer.FavoriteNumbers;
                break;
            case Setting.HistoryNumbers:
                FileExplorer.HistoryNumbers = !FileExplorer.HistoryNumbers;
                break;
            case Setting.HotlistNumbers:
                FileExplorer.HotlistNumbers = !FileExplorer.HotlistNumbers;
                break;
            case Setting.SearchNumbers:
                FileExplorer.SearchNumbers = !FileExplorer.SearchNumbers;
                break;
            case Setting.FavoritePanes:
                FileExplorer.FavoritePanes = !FileExplorer.FavoritePanes;
                break;
            case Setting.FavoritePeek:
                FileExplorer.FavoritePeek = !FileExplorer.FavoritePeek;
                break;
            case Setting.BookmarkNumbers:
                JumpMenu.BookmarkNumbers = !JumpMenu.BookmarkNumbers;
                break;
            case Setting.BookmarkPanes:
                JumpMenu.BookmarkPanes = !JumpMenu.BookmarkPanes;
                break;
            case Setting.BookmarkPeek:
                JumpMenu.BookmarkPeek = !JumpMenu.BookmarkPeek;
                break;
            case Setting.Monochrome:
                ConsoleColors.Monochrome = !ConsoleColors.Monochrome;
                break;
            case Setting.Corners:
                // The corners are looked up each time a box or a tab is drawn
                AsciiStyles.SetAsciiStyle(AsciiStyles.GetAsciiStyle() == 1 ? 0 : 1);
                break;
            case Setting.Language:
                Lang.Set(Lang.Next(next));
                break;
            case Setting.AutoSize:
                Init.AutoSize = !Init.AutoSize;
                // Turned off: back to the default size
                if (!Init.AutoSize) Init.SetDefaultOutputDimensions();
                Init.ApplyAutoSize();
                break;
            case Setting.Size:
                // Saves the settings itself
                Actions.DisplayOutputDimensions();
                return;
            case Setting.LineNumbers:
                TextArea.LineNumbers = !TextArea.LineNumbers;
                break;
            case Setting.ScrollOff:
                TextArea.ScrollOff = !TextArea.ScrollOff;
                break;
            case Setting.WordWrap:
                TextArea.WordWrap = !TextArea.WordWrap;
                break;
            case Setting.SyntaxColors:
                SyntaxHighlighter.ColorFiles = !SyntaxHighlighter.ColorFiles;
                break;
            case Setting.Snippets:
                // The file of snippets in the editor (it is made first, if the user wants that)
                ChoiceBox.Forget();
                if (SnippetBox.EnsureFile()) FileExplorer.EditSnippets(0);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
                return;
            case Setting.AutoComplete:
                // Turned on: the file of completions is made if there is none
                Blyant.Completions.Enabled = !Blyant.Completions.Enabled;
                if (Blyant.Completions.Enabled) Blyant.Completions.EnsureFile();
                break;
            case Setting.Completions:
                // The file of completions in the editor (made first if there is none)
                ChoiceBox.Forget();
                FileExplorer.EditCompletions();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
                return;
            case Setting.Info:
                ShowInfo();
                return;
            case Setting.LogoAnimation:
                // Saves the settings itself
                PickLogoAnimation();
                return;
            case Setting.ViewLog:
                EventLog.Show();
                return;
            case Setting.Clipboard:
                // Saves the settings itself
                ClipboardPage.Show();
                return;
            case Setting.EditSettings:
                // The settings file in the editor. Saved: Blyant starts over and reads the settings again
                if (Tools.EditSettingsFile()) Tools.RestartFromStartPage();
                Tools.ClearScreen();
                return;
            case Setting.Restart:
                if (ChoiceBox.Confirm(DialogText.Restart, DialogText.RestartQuestion, string.Empty, false))
                {
                    // Blyant starts over: the settings are read again, and the start page is shown as at startup
                    Tools.RestartFromStartPage();
                }

                return;
            default:
                if (ChoiceBox.Confirm(DialogText.CaptionsSettings, DialogText.ResetSettingsQuestion, string.Empty, true))
                {
                    Actions.ApplyDefaultSettings();
                    // Completing words is on again: the file is made if there is none (one that is there is kept)
                    Blyant.Completions.EnsureFile();
                }

                return;
        }

        FileIo.SaveSettings();
    }
}
