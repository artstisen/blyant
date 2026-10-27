namespace Blyant;

/// <summary>
/// What the pages share: the frame of the editor, picking the language, the size of the output, the default
/// settings and the about pages
/// </summary>
internal static class Actions
{
    /// <summary>
    /// Pick the language in a box on top of the page. The choice is saved in the settings file
    /// </summary>
    internal static void PickLanguage()
    {
        // Found again: a file may have been put in a folder "lang" since
        Lang.Refresh();
        var languages = Lang.Available;
        var choices = languages.Select(LanguageChoice).ToArray();
        // A key is one character: 1 to 9, then letters (not those that move in the box: g h j k l q)
        const string more = "ABCDEFIMNOPRSTUVWXYZ";
        var keys = Enumerable.Range(0, languages.Length)
            .Select(n => n < 9 ? (n + 1).ToString() : n - 9 < more.Length ? more[n - 9].ToString() : string.Empty)
            .ToArray();
        var picked = ChoiceBox.Pick(DialogText.PickLanguageCaption, DialogText.PickLanguageQuestion, string.Empty,
            choices, keys, Math.Max(0, Array.FindIndex(languages, language => language.Code == Lang.Current)), false);
        if (picked < 0) return;
        Lang.Set(languages[picked].Code);
        FileIo.SaveSettings();
    }

    // A language in the list: its name and code, "(lang)" when a file in a folder "lang" replaces the one built in,
    // and how much of a file in a folder is translated
    private static string LanguageChoice(Lang.Language language)
    {
        var text = language.Name + " (" + language.Code + ")";
        if (language.Replaced) text += " (" + Lang.FolderName + ")"; // English: the name of the folder
        if (language.Percent is { } percent) text += "  " + percent + "%";
        return text;
    }

    private static bool _changeOutputDimensionsAccelerated;

    private static string GetAcceleratedState()
    {
        return _changeOutputDimensionsAccelerated ? "x2" : "x1";
    }

    internal static void DisplayOutputDimensions()
    {
        var originalLineCount = Init.LineCount;
        var originalLineWidth = Init.LineWidth;
        retry:
        // "<^v>"
        // Display dimensions
        Init.DisplayOutputDimensions();
        // Take input
        string[] instructions =
        {
            Lang.T("Set output dimensions..."),
            "+----+",
            "| " + GetAcceleratedState() + " |",
            "+----+",
            Lang.T("Press the arrow keys (or h/j/k/l) to change dimensions"),
            Lang.T("Press Tab to toggle increment size (x1/x2)"),
            Lang.T("Press E to reset dimensions"),
            Lang.T("Press Enter to save"),
            "",
            Lang.T("Press Esc to cancel")
        };
        var tempC = Keyboard.ToNavigationKey(Keyboard.AwaitAnyKeyWithInstructions(instructions));
        if (tempC.Key == ConsoleKey.Tab)
        {
            _changeOutputDimensionsAccelerated = !_changeOutputDimensionsAccelerated;
            goto retry;
        }
        else if (tempC.Key == ConsoleKey.UpArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 3; i++)
                {
                    Init.DecreaseLineCount();
                }
            }
            else
            {
                Init.DecreaseLineCount();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.DownArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 3; i++)
                {
                    Init.IncreaseLineCount();
                }
            }
            else
            {
                Init.IncreaseLineCount();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.LeftArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 6; i++)
                {
                    Init.DecreaseLineWidth();
                }
            }
            else
            {
                Init.DecreaseLineWidth();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.RightArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 6; i++)
                {
                    Init.IncreaseLineWidth();
                }
            }
            else
            {
                Init.IncreaseLineWidth();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.E)
        {
            _changeOutputDimensionsAccelerated = false;
            Init.SetDefaultOutputDimensions();
            goto retry;
        }
        else if (tempC.Key == ConsoleKey.Enter)
        {
            // Dimensions set by hand take over from the automatic
            Init.AutoSize = false;
            FileIo.SaveSettings();
        }
        else if (tempC.Key == ConsoleKey.Escape)
        {
            Init.SetLineCount(originalLineCount);
            Init.SetLineWidth(originalLineWidth);
        }
        else
        {
            goto retry;
        }
    }

    // The editor frame for a plain text file opened from the file browser
    internal static void DrawEditFileMenu()
    {
        Tools.CursorTopLeft();
        // (a blank page is not named until it is saved)
        DrawEditorFrame(DialogText.CaptionsEditor,
            FileExplorer.IsUntitled ? DialogText.Untitled : FileExplorer.CurrentFileName, string.Empty);
    }

    /// <summary>
    /// The top of the view of a comparison (DiffView), as that of the editor: the caption, and what is compared
    /// where the editor names the file. The cursor is left where the text starts
    /// </summary>
    internal static void DrawCompareMenu(string about)
    {
        DrawEditorFrame(DialogText.CaptionsCompare, about, string.Empty);
    }

    internal static void DrawEditSettingsMenu()
    {
        Tools.CursorTopLeft();
        DrawEditorFrame(DialogText.CaptionsEditor, FileIo.SettingsFileName, string.Empty);
    }

    // The frame of the editor: the top row (the caption, the name of what is edited, and the place of the labels
    // Unsaved and Read only at the right), the divider the editor writes its mode on, and the bottom line. The
    // cursor is left where the text starts, below the divider
    private static void DrawEditorFrame(string caption, string title, string reminder)
    {
        Menu.WriteEditorTop(caption, title, reminder);
        Tools.SetCursorEx(1, 0);
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.EditorTitle);
        Tools.SaveCursorPosition(1);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Tools.LoadCursorPosition(1);
    }

    /// <summary>
    /// Set all settings back to their defaults and save them
    /// </summary>
    internal static void ApplyDefaultSettings()
    {
        Tools.ClearEditorHelpData();
        AsciiStyles.ResetAsciiStyle();
        ConsoleColors.Monochrome = false;
        SyntaxHighlighter.ColorFiles = true;
        Completions.Enabled = true;
        FileExplorer.DualPane = true;
        TextArea.LineNumbers = false;
        TextArea.WordWrap = true;
        TextArea.ShowWhitespace = false;
        TextFile.NewFileLineEndings = "system";
        TextArea.ScrollOff = true;
        TextArea.ScrollOffLines = TextArea.DefaultScrollOffLines;
        Snippets.Sort = Snippets.SortOrder.File;
        ConsoleColors.Use(ColorScheme.Default);
        Logo.Current = LogoArt.BuiltIn;
        Logo.Animation = Logo.AnimationSpeed.Fast;
        StartMenu.Mode = StartMenu.StartMode.Select;
        FileOperations.DefaultExtension = FileOperations.MarkdownExtension;
        StartMenu.StartInFavorites = false;
        FileOperations.DeleteToTrash = true;
        FileExplorer.FavoritePanes = true;
        FileExplorer.FavoritePeek = true;
        JumpMenu.BookmarkPanes = true;
        JumpMenu.BookmarkPeek = true;
        FileExplorer.FavoriteNumbers = false;
        JumpMenu.BookmarkNumbers = false;
        FileExplorer.FileNumbers = false;
        FileExplorer.HistoryNumbers = false;
        FileExplorer.HotlistNumbers = false;
        FileExplorer.SearchNumbers = false;
        CommandList.Enabled = true;
        Hints.Enabled = true;
        Recovery.Enabled = true;
        SystemClipboard.SetDefaults();
        // The dimensions follow the window (and are the default ones when it is not known, or smaller)
        Init.SetDefaultOutputDimensions();
        Init.AutoSize = true;
        Init.ApplyAutoSize();
        FileIo.SaveSettings();
    }

    private static string _tempAboutText = string.Empty;
    private static string TempPageNumber => Lang.F("Page {0}/{1}", 1, 2);

    /// <summary>
    /// Show the about page and, unless Esc is pressed, the license. The page it was opened from must be drawn
    /// again afterwards
    /// </summary>
    internal static void ShowAboutPages()
    {
        Tools.ClearScreen();
        Tools.CursorTopLeft();

        // Show help
        // Copyright
        _tempAboutText = DialogText.ApplicationName + " " + DialogText.ApplicationVersion + " " +
                         DialogText.ApplicationBy;
        _tempAboutText += Environment.NewLine + Environment.NewLine;
        _tempAboutText += DialogText.About;
        _tempAboutText += Environment.NewLine + Environment.NewLine;
        _tempAboutText += DialogText.MultipleInstances;
        _tempAboutText += Environment.NewLine + Environment.NewLine;
        // Show in read only editor (page 1)
        var dummy = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1, 22, _tempAboutText, 0, false,
            true, false, 0);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 2);
        Tools.CursorRight(Init.LineWidth - (TempPageNumber.Length + 1));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(TempPageNumber);
        Tools.CursorReturn();
        Tools.CursorRight();
        if (Keyboard.AwaitAnyKeyOrEscape())
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        _tempAboutText = string.Empty;
        _tempAboutText = SoftwareLicense.GetLicenseText();
        // Show in read only editor (page 2)
        var dummy1 = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1, 22, _tempAboutText, 0, false,
            true, false, 0);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 2);
        Tools.CursorRight(Init.LineWidth - (TempPageNumber.Length + 1));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(Lang.F("Page {0}/{1}", 2, 2));
        Tools.CursorReturn();
        Tools.CursorRight();
        Keyboard.AwaitAnyKey();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void WriteHorizontalRule(bool jaggedLine, string label, bool colorizeLabel,
        ConsoleColors.ColorPreset preset)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (jaggedLine)
        {
            Console.Write(Tools.WriteJaggedLine(Init.LineWidth));
        }
        else
        {
            Console.Write(new string('-', Init.LineWidth));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (label.Length > 0)
        {
            Tools.CursorReturn();
            Tools.CursorRight(Init.LineWidth / 2);
            Tools.CursorLeft((label.Length / 2) + 1);
            if (colorizeLabel)
            {
                ConsoleColors.SetColors(preset);
                Console.Write(" " + label + " ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else
            {
                Console.Write(" " + label + " ");
            }
        }

        Tools.CursorReturn();
        Tools.CursorDown();
    }
}
