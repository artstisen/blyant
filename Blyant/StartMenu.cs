namespace Blyant;

/// <summary>
/// The page shown when Blyant starts, in a frame with three tabs (TabFrame): Start, where a file or a project is
/// made or opened (New file, Open file, New project, Open project), Configuration, with the settings on four pages
/// (the sub-tabs General, Layout, Editor and System), and Help (the help, the guides and the about page). Tab goes
/// from page to page. The page is shown again when the file browser is closed, and after a jump.
/// The setting "startmode" tells if the page is shown at startup, or if Blyant goes directly to the file browser.
/// </summary>
internal static class StartMenu
{
    internal enum StartMode
    {
        Select, // Show the start page
        Editor // Open the file browser
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
        return mode == StartMode.Editor ? "editor" : "select";
    }

    /// <summary>
    /// The start mode of its name in the settings file ("notes", of Marbles, shows the start page)
    /// </summary>
    internal static StartMode ParseSetting(string value)
    {
        return value.Trim().ToLowerInvariant() == "editor" ? StartMode.Editor : StartMode.Select;
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
    /// 3 System), or -1 for the Start tab</param>
    internal static void Open(int configurationPage = -1)
    {
        _starting = false;
        _showConfiguration = Math.Clamp(configurationPage, -1, 3);
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
        }

        var start = new ChoicesPage();
        var frame = _frame = new TabFrame(TabRow, () =>
            {
                Logo.DrawLogo(true, animate);
                animate = false;
            },
            new TabItem(() => DialogText.StartTab, start),
            new TabItem(() => DialogText.StartConfiguration, new SettingsPage(0), new SettingsPage(1),
                new SettingsPage(2), new SettingsPage(3)),
            new TabItem(() => Lang.T("Help"), new HelpPage())) { Commands = ReadCommand };
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
            case "file":
            case "browse":
            case "open":
                FileExplorer.Run();
                break;
            case "project":
            case "newproject":
                // ":project name": a new project
                NewProject(argument);
                break;
            case "projects":
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
            case "system":
                ShowPage(4);
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

    // New file: a file made in the directory the file browser was in last (the name is asked for if it is not
    // given; without an extension it is a Markdown file), and opened in the editor with the file browser behind it
    private static void NewFile(string name)
    {
        ChoiceBox.Forget();
        var directory = FileExplorer.LastDirectory;
        name = name.Trim();
        if (name.Length == 0)
        {
            var asked = ChoiceBox.AskText(DialogText.NewFile, Lang.F("The file is made in {0}", directory),
                DialogText.FileNameColon, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        name = FileOperations.WithDefaultExtension(name);
        var path = Path.Combine(directory, name);
        var problem = Tools.FileNameProblem(name) ??
                      (File.Exists(path) || Directory.Exists(path) ? DialogText.NameExists : null);
        if (problem == null)
        {
            try
            {
                // Only a new file: one that was made in the meantime is not emptied
                new FileStream(path, FileMode.CreateNew).Dispose();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                problem = e.Message;
            }
        }

        EventLog.Add(DialogText.LogFile + "New: " + path + (problem == null ? string.Empty : " (" + problem + ")"));
        if (problem != null)
        {
            ChoiceBox.Message(DialogText.NewFile, problem, name);
            return;
        }

        FileExplorer.RunAtFile(Path.GetFullPath(path));
    }

    // New project: a directory made in the projects directory (the name is asked for if it is not given), shown
    // in the file browser
    private static void NewProject(string name)
    {
        ChoiceBox.Forget();
        name = name.Trim();
        if (name.Length == 0)
        {
            var asked = ChoiceBox.AskText(DialogText.NewProject,
                Lang.F("The project is a folder in {0}", Projects.Directory), DialogText.ProjectNameColon,
                string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var status = Projects.Create(name, out var path);
        EventLog.Add(DialogText.LogFile + "New project: " + (path.Length > 0 ? path : name) +
                     (status == DialogText.Success ? string.Empty : " (" + status + ")"));
        if (status != DialogText.Success)
        {
            ChoiceBox.Message(DialogText.NewProject, status, name);
            return;
        }

        FileExplorer.RunAtDirectory(path);
    }

    // Open project: the projects in a list (with what each holds), the one picked shown in the file browser
    private static void OpenProject()
    {
        if (ProjectsBox.Pick() is { } path) FileExplorer.RunAtDirectory(path);
    }

    // The projects directory: picked in the file browser (where it is not there yet, the directory it is to be
    // made in)
    private static void PickProjectsDirectory()
    {
        ChoiceBox.Forget();
        var picked = FileExplorer.PickDirectory(Lang.T("Projects folder"));
        if (picked == null) return;
        Projects.Directory = picked;
        FileIo.SaveSettings();
    }

    private enum Setting
    {
        StartSelect,
        StartEditor,
        StartFavorites, // Shown below StartEditor when it is chosen
        ProjectsDirectory,
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
        ScrollOff,
        Clipboard,
        SyntaxColors,
        Spell,
        Snippets,
        AutoComplete,
        Completions,
        Reset,
        EditSettings,
        Restart,
        Info,
        ViewLog
    }

    // The settings shown, in the order of the pages: some are only shown when they can be used
    private static List<Setting> Settings()
    {
        // General: what happens at startup, the projects directory and the language
        var settings = new List<Setting> { Setting.StartSelect, Setting.StartEditor };
        if (Mode == StartMode.Editor) settings.Add(Setting.StartFavorites);
        settings.Add(Setting.ProjectsDirectory);
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
        settings.Add(Setting.ScrollOff);
        settings.Add(Setting.Clipboard);
        settings.Add(Setting.SyntaxColors);
        settings.Add(Setting.Spell);
        settings.Add(Setting.Snippets);
        settings.Add(Setting.AutoComplete);
        settings.Add(Setting.Completions);
        // System
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
            Setting.StartFavorites => indent + Check(StartInFavorites) + Lang.T("Open the favorites list first"),
            Setting.ProjectsDirectory => indent + Lang.F("Projects folder: {0} (Enter: Change)", Projects.Directory),
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
            Setting.ScrollOff => Check(TextArea.ScrollOff) +
                                 Lang.F("Keep {0} lines around the cursor", TextArea.ScrollOffLines),
            Setting.Clipboard => indent + Lang.T("Clipboard and paste bin (Enter: Change)"),
            Setting.SyntaxColors => Check(SyntaxHighlighter.ColorFiles) + Lang.T("Syntax highlighting"),
            Setting.Spell => indent + SpellText(),
            Setting.Snippets => indent + Lang.T("Snippet editor (Enter: Open)"),
            Setting.AutoComplete => Check(Blyant.Completions.Enabled) + Lang.T("Complete words while typing"),
            Setting.Completions => indent + Lang.T("Completions editor (Enter: Open)"),
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

    private const int PageCount = 4;

    // The settings are shown on four pages: General, Layout, Editor and System
    private static int PageOf(Setting setting)
    {
        return setting switch
        {
            Setting.StartSelect or Setting.StartEditor or Setting.StartFavorites or Setting.ProjectsDirectory
                or Setting.Language => 0,
            Setting.AutoSize or Setting.Size or Setting.Monochrome or Setting.ColorScheme or Setting.Logo
                or Setting.LogoAnimation or Setting.Corners => 1,
            Setting.LineNumbers or Setting.WordWrap or Setting.ScrollOff or Setting.Clipboard
                or Setting.SyntaxColors or Setting.Spell or Setting.Snippets or Setting.AutoComplete
                or Setting.Completions => 2,
            _ => 3
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
            _ => Lang.T("System")
        };
    }

    // A blank row is shown above these settings when there is room: they start a group of their page
    private static bool StartsGroup(Setting setting) => setting is Setting.ProjectsDirectory;

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
    /// The Start tab: make or open a file or a project, the Jump menu, the quick guide, and exit
    /// </summary>
    private sealed class ChoicesPage : ITabPage
    {
        private int _selected;

        private enum Choice
        {
            NewFile,
            OpenFile,
            NewProject,
            OpenProject,
            Jump,
            QuickGuide,
            Exit
        }

        private static readonly Choice[] Shown = Enum.GetValues<Choice>();

        // The keys that open a choice at once (the same in all languages; the numbers only select). A new file: N,
        // the file browser: F, a new project: P, the projects: O, the Jump menu: M (and Ctrl+O, as everywhere; j
        // moves down), the quick guide: U, exit: Q (asks)
        private static string KeysOf(Choice choice) => choice switch
        {
            Choice.NewFile => "N",
            Choice.OpenFile => "F",
            Choice.NewProject => "P",
            Choice.OpenProject => "O",
            Choice.Jump => "M",
            Choice.QuickGuide => "U",
            _ => "Q"
        };

        // In the language in use
        private static string TextOf(Choice choice) => choice switch
        {
            Choice.NewFile => DialogText.NewFile,
            Choice.OpenFile => DialogText.OpenFile,
            Choice.NewProject => DialogText.NewProject,
            Choice.OpenProject => DialogText.OpenProject,
            Choice.Jump => DialogText.StartJump,
            Choice.QuickGuide => DialogText.QuickGuide,
            _ => DialogText.StartExit
        };

        public string Title => DialogText.StartTab;
        public string Keys => DialogText.StartKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        public void Draw(TabArea area, bool full)
        {
            _selected = Math.Clamp(_selected, 0, Shown.Length - 1);
            if (full)
            {
                for (var i = 0; i < Shown.Length; i++)
                {
                    // The number, the key that opens the choice at once, and the choice
                    Tools.SetCursorEx(area.Top + i, area.Left + 3);
                    Console.Write(Tools.TruncateString((i + 1) + " " + KeysOf(Shown[i]) + " .... " +
                                                       TextOf(Shown[i]), area.Width - 4, true));
                }
            }

            for (var i = 0; i < Shown.Length; i++)
            {
                Tools.SetCursorEx(area.Top + i, area.Left);
                // The pointer is highlighted as in the other lists
                if (i == _selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == _selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var count = Shown.Length;
            _selected = Math.Clamp(_selected, 0, count - 1);
            // Ctrl+O: the Jump menu, as on the other pages
            if (Blyant.Jump.IsKey(key)) return Open(Choice.Jump);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.L or ConsoleKey.RightArrow) return Open(Shown[_selected]);
            // The keys of the choices open them at once
            var direct = key.Key switch
            {
                ConsoleKey.N => Choice.NewFile,
                ConsoleKey.F => Choice.OpenFile,
                ConsoleKey.P => Choice.NewProject,
                ConsoleKey.O => Choice.OpenProject,
                ConsoleKey.M => Choice.Jump,
                ConsoleKey.U => Choice.QuickGuide,
                _ => (Choice?)null
            };
            if (direct is { } choice && (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                _selected = Array.IndexOf(Shown, choice);
                return Open(choice);
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
                case Choice.OpenFile:
                    FileExplorer.Run();
                    break;
                case Choice.NewProject:
                    NewProject(string.Empty);
                    break;
                case Choice.OpenProject:
                    OpenProject();
                    break;
                case Choice.Jump:
                    // A place that is opened on top of the page returns here; the others leave it
                    Blyant.Jump.Open();
                    break;
                case Choice.QuickGuide:
                    QuickGuide.Show();
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
            int Row(int i) => area.Top + i + (gaps ? settings.Take(i + 1).Count(StartsGroup) : 0);
            if (full)
            {
                // The settings without a check box are indented to line up with the names of the ones with a
                // check box. On a page without check boxes they line up with the pointer
                var texts = settings.Select(SettingText).ToList();
                var checkBoxes = texts.Any(text => text.StartsWith('['));
                for (var i = 0; i < settings.Count; i++)
                {
                    Tools.SetCursorEx(Row(i), area.Left + 2);
                    var text = checkBoxes ? texts[i] : texts[i].TrimStart();
                    Console.Write(" " + Tools.TruncateString(text, area.Width - 4, true));
                }
            }

            for (var i = 0; i < settings.Count; i++)
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
            case Setting.StartFavorites:
                StartInFavorites = !StartInFavorites;
                break;
            case Setting.ProjectsDirectory:
                // Saves the settings itself
                PickProjectsDirectory();
                return;
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
                }

                return;
        }

        FileIo.SaveSettings();
    }
}
