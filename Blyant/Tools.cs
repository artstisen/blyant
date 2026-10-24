using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Blyant;

internal static class Tools
{
    /// <summary>
    /// A text cut to a length, for the screen: control characters (as an escape code in a file name) are blank
    /// spaces, so they are never sent to the terminal
    /// </summary>
    /// <param name="s">The text</param>
    /// <param name="length">The number of terminal cells it may take (a wide character takes two)</param>
    /// <param name="dots">True to end a text that was cut with "..."</param>
    public static string TruncateString(string s, int length, bool dots)
    {
        if (!IsValid(s) || length <= 0) return string.Empty;
        s = TextMetrics.Printable(s);
        if (TextMetrics.WidthOf(s) <= length) return s;
        return dots && length > 3 ? TextMetrics.StartPart(s, length - 3) + "..." : TextMetrics.StartPart(s, length);
    }


    public static string[] SplitStringOnNewLine(string input, bool trim)
    {
        if (IsValid(input))
        {
            if (trim)
            {
                input = input.Trim();
            }

            return input.Split(
                new[] {"\r\n", "\r", "\n"},
                StringSplitOptions.None);
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    public static List<string> SplitStringOnNewLine(string input)
    {
        if (IsValid(input))
        {
            var tempList = new List<string>();
            tempList.AddRange(input.Split(
                new[] {"\r\n", "\r", "\n"},
                StringSplitOptions.None));

            return tempList;
        }
        else
        {
            return new List<string>();
        }
    }

    public static string RemoveNewLineChars(string input)
    {
        if (IsValid(input))
        {
            input = input.Replace("\n", string.Empty);
            input = input.Replace("\r", string.Empty);
            input = input.Replace("\r\n", string.Empty);
        }

        return input;
    }

    public static string TrimToLower(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        input = input.Trim();
        input = input.ToLower();
        return input;
    }

    // Not null or empty
    public static bool IsValid(string input)
    {
        return !string.IsNullOrEmpty(input);
    }

    public static bool IsValidFileName(string input)
    {
        if (!IsValid(input)) return false;
        var invalidChars = Path.GetInvalidFileNameChars();
        return invalidChars.All(c => !input.Contains(c));
    }

    // The characters a name of a file cannot contain on Windows. Not used on any platform, so files can be copied
    // between Windows, macOS and Linux
    private const string ReservedFileNameCharacters = "\\/:*?\"<>|";

    // The names Windows keeps for devices, also with an extension ("con.txt")
    private static readonly string[] ReservedFileNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    /// <summary>
    /// What is wrong with a new name of a file or directory, or null if it can be used. The rules are the same on
    /// all platforms (those of Windows), so the names work everywhere
    /// </summary>
    public static string? FileNameProblem(string name)
    {
        if (name.Length == 0 || name is "." or "..") return DialogText.InvalidFileName;
        foreach (var c in name)
        {
            if (char.IsControl(c)) return DialogText.NameControlCharacters;
            if (ReservedFileNameCharacters.Contains(c)) return DialogText.NameCharacter(c);
        }

        if (name.EndsWith('.') || name.EndsWith(' ')) return DialogText.NameEnd;
        if (name.Length > 255) return DialogText.NameTooLong;
        var stem = name.Split('.')[0].TrimEnd();
        if (ReservedFileNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            return DialogText.NameReserved(stem.ToUpperInvariant());
        }

        return null;
    }

    public static bool IsValidPath(string input)
    {
        if (!IsValid(input)) return false;
        var invalidChars = Path.GetInvalidPathChars();
        return invalidChars.All(c => !input.Contains(c));
    }


    public static bool IsNumeric(string s)
    {
        // Only 0-9: \d also matches the digits of other scripts, which Convert.ToInt32 cannot read
        return Regex.IsMatch(s, "^[0-9]+$");
    }


    internal static int WordCount(string text)
    {
        int wordCount = 0, index = 0;

        // skip whitespace until first word
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        while (index < text.Length)
        {
            // check if current char is part of a word
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
                index++;

            wordCount++;

            // skip whitespace until next word
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        return wordCount;
    }

    internal static int CharacterCount(string text)
    {
        int charCount = 0, index = 0;

        // skip whitespace until first word
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        while (index < text.Length)
        {
            //check if current char is part of a word
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
                charCount++;
            }

            // skip whitespace until next word
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        return charCount;
    }

    internal static int LineCount(string text)
    {
        if (!string.IsNullOrEmpty(text) && text.Contains("\n"))
        {
            return SplitStringOnNewLine(text, false).Length;
        }

        return 0;
    }


    private static string _tempCenterScreenText = string.Empty;

    public static void WriteCenterScreen(string text)
    {
        _tempCenterScreenText = text;
        Tools.CursorTopLeft();
        Tools.CursorRight((Init.LineWidth / 2) - (_tempCenterScreenText.Length / 2));
        Tools.CursorDown(Init.LineCount / 2);
        Console.Write(_tempCenterScreenText);
    }

    public static void WriteCenterScreen(string[] text)
    {
        var lineWidth = Init.LineWidth / 2;
        var lineCount = Init.LineCount / 2;
        lineCount -= (text.Length / 2);
        CursorTopLeft();
        CursorDown(lineCount);
        for (int i = 0; i < text.Length; i++)
        {
            CursorRight((lineWidth) - (text[i].Length / 2));
            Console.Write(text[i]);
            CursorTopLeft();
            lineCount += 1;
            CursorDown(lineCount);
        }
    }


    /// <summary>
    /// Edit the settings file in the editor (from the Configuration page)
    /// </summary>
    /// <returns>True if the file was saved: Blyant must start over to read the settings again</returns>
    internal static bool EditSettingsFile()
    {
        ClearScreen();
        Actions.DrawEditSettingsMenu();
        TextArea textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
            Init.LineCount - 3, FileIo.ReadSettingsFile(), 0, false, false, false, 1);
        // Cleanup
        textArea.ClearUndoRedoCache();
        return textArea.SaveWasInvoked;
    }

    /// <summary>
    /// Read a line typed at the cursor (as Console.ReadLine), which Esc cancels
    /// </summary>
    /// <param name="maxLength">The most characters that can be typed</param>
    /// <returns>The text, or null if canceled</returns>
    internal static string? ReadLineOrEscape(int maxLength)
    {
        var text = string.Empty;
        ShowCursor();
        try
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape) return null;
                if (key.Key == ConsoleKey.Enter) return text;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (text.Length == 0) continue;
                    var length = TextMetrics.PreviousElementLength(text, text.Length);
                    var cells = TextMetrics.WidthOf(text[^length..]);
                    text = text[..^length];
                    SetCursorEx(Console.CursorTop, Math.Max(0, Console.CursorLeft - cells));
                    Console.Write(new string(' ', cells));
                    SetCursorEx(Console.CursorTop, Math.Max(0, Console.CursorLeft - cells));
                }
                else if (!char.IsControl(key.KeyChar) &&
                         text.Length + (char.IsHighSurrogate(key.KeyChar) ? 2 : 1) <= maxLength)
                {
                    text += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
            }
        }
        finally
        {
            HideCursor();
        }
    }


    #region String memory (Internal clipboard)

    internal const int MaxLineMemoryCount = 64;
    private static List<string> _stringMemory = new();
    private static int _currentLineMemoryIndex;

    internal static void NextLineMemoryIndex()
    {
        if (_stringMemory.Count <= 0) return;
        if (_currentLineMemoryIndex + 1 < _stringMemory.Count)
        {
            _currentLineMemoryIndex += 1;
        }
        else
        {
            _currentLineMemoryIndex = 0;
        }
    }

    internal static void PreviousLineMemoryIndex()
    {
        if (_stringMemory.Count > 0)
        {
            if (_currentLineMemoryIndex - 1 >= 0)
            {
                _currentLineMemoryIndex -= 1;
            }
            else
            {
                _currentLineMemoryIndex = _stringMemory.Count - 1;
            }
        }
    }

    internal static void ClearLineMemory()
    {
        if (_stringMemory.Count > 0)
        {
            for (int i = 0; i < _stringMemory.Count; i++)
            {
                _stringMemory[i] = DialogText.Cleared;
            }
        }

        _stringMemory.Clear();
        _currentLineMemoryIndex = 0;
    }

    /// <summary>
    /// Add a string to the string memory and make it the current one.
    /// A string already in memory is not added again, but becomes the current one.
    /// </summary>
    internal static void StoreInLineMemory(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var index = _stringMemory.IndexOf(text);
        if (index >= 0)
        {
            _currentLineMemoryIndex = index;
            return;
        }

        _stringMemory.Add(text);
        LimitLineMemory();
        _currentLineMemoryIndex = _stringMemory.Count - 1;
    }

    /// <summary>
    /// The number of the current string and the total count, like "2/5"
    /// </summary>
    internal static string GetLineMemoryPosition()
    {
        return (_currentLineMemoryIndex + 1) + "/" + _stringMemory.Count;
    }

    private static void LimitLineMemory()
    {
        if (_stringMemory.Count > MaxLineMemoryCount)
        {
            _stringMemory.RemoveAt(0);
        }
    }

    internal static bool HasLineMemory()
    {
        if (_stringMemory.Count > 0)
        {
            return true;
        }

        return false;
    }

    internal static string GetCurrentLineFromLineMemory()
    {
        if (_stringMemory.Count > 0 && _currentLineMemoryIndex >= 0 && _currentLineMemoryIndex < _stringMemory.Count)
        {
            return _stringMemory[_currentLineMemoryIndex];
        }
        else
        {
            return string.Empty;
        }
    }

    internal static List<string> GetAllLinesFromLineMemory()
    {
        return _stringMemory;
    }

    /// <summary>
    /// Make a string of the string memory the current one (the one "p" inserts)
    /// </summary>
    internal static void SetLineMemoryIndex(int index)
    {
        if (index >= 0 && index < _stringMemory.Count) _currentLineMemoryIndex = index;
    }

    /// <summary>
    /// The number of the current string in the string memory (counted from 0)
    /// </summary>
    internal static int LineMemoryIndex => _currentLineMemoryIndex;

    /// <summary>
    /// A string of the string memory as one row of a list: line breaks are shown as " | " (the blank space at
    /// the start and end of the lines is left out), and whole lines start with "(lines)"
    /// </summary>
    internal static string LineMemoryRow(string text)
    {
        var lines = text.EndsWith('\n');
        var row = string.Join(" | ", text.TrimEnd('\n').Split('\n').Select(line => line.Trim()));
        return lines ? Lang.F("(lines) {0}", row) : row;
    }

    #endregion



    /// <summary>
    /// Restart Blyant and continue as when it is started: with the start page (or what it is set to show)
    /// </summary>
    internal static void RestartFromStartPage()
    {
        Init.Log(" *** Restart *** ", false);
        ClearScreen();
        ClearEditorHelpData();
        Init.RunInitialization();
        StartMenu.Start();
        Navigation.Wait();
    }

    /// <summary>
    /// An error that nothing else caught: it is told about, and Blyant starts over from the start page (the
    /// settings are read again)
    /// </summary>
    internal static void FatalError(Exception error)
    {
        // The kind of error only: its text can have a path in it
        Init.Log("Error: " + error.GetType().Name, false);
        ChoiceBox.Message(DialogText.CaptionsKeyError, DialogText.ErrorUnsupportedAction, error.Message);
        Init.Log(" *** Restart *** ", false);
        ClearEditorHelpData();
        Init.RunInitialization();
        StartMenu.Open();
    }

    internal static void ActionUnsupported(string errorMsg)
    {
        ChoiceBox.Message(DialogText.CaptionsKeyError, DialogText.ErrorUnsupportedAction, errorMsg);
    }




    /// <summary>
    /// A text cut to fit on a line of the window after a number of columns already used (texts in other
    /// languages can be longer, and writing past the last column of the bottom line would scroll the window)
    /// </summary>
    internal static string FitLine(string text, int used)
    {
        var room = Math.Max(0, Init.LineWidth - 1 - used);
        // Key hints ("a | b | c") lose whole hints from the end
        while (TextMetrics.WidthOf(text) > room && text.LastIndexOf(" | ", StringComparison.Ordinal) > 0)
        {
            text = text[..text.LastIndexOf(" | ", StringComparison.Ordinal)];
        }

        while (TextMetrics.WidthOf(text) > room)
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        return text;
    }

    internal static void ClearScreen()
    {
        // The screen is cleared with the background of the color scheme
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Clear();
        ChoiceBox.Forget();
    }


    internal static void ExitApplication()
    {
        // The places of the file browser, if Blyant is left from it (or from the editor opened from it)
        FileExplorer.RememberOnExit();
        ShowCursor(); // Remember to show cursor again for continuous work in the console
        // Exit the application reverting to the original console colors
        ConsoleColors.Reset();
        Console.Clear();
        Console.WriteLine(DialogText.ApplicationExit);
        Environment.Exit(0);
    }

    // JaggedRule
    private static bool _tempJagged;
    private static StringBuilder _tempJaggedLine = new();

    internal static string WriteJaggedLine(int length)
    {
        if (length > 1)
        {
            _tempJagged = false;
            _tempJaggedLine = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                if (_tempJagged)
                {
                    _tempJagged = false;
                    _tempJaggedLine.Append("=");
                }
                else
                {
                    _tempJagged = true;
                    _tempJaggedLine.Append("-");
                }
            }

            return _tempJaggedLine.ToString();
        }
        else
        {
            return "-";
        }
    }


    // Shortcuts
    //private static string tempPageNumber = "Page 1/2";
    private static List<Shortcut> _shortcutList = new();


    // Clear data to enable rewriting it with new values for shortcuts
    public static void ClearEditorHelpData()
    {
        _shortcutList.Clear();
    }

    // Start by "collecting" shortcuts in a list
    // and the print them out with pagination
    /// <summary>
    /// The parts of the help: the keys of a page of Blyant
    /// </summary>
    internal enum HelpTopic
    {
        All,
        Start, // The page shown at startup, and its configuration
        Files, // The file browser
        Favorites, // The favorites of the file browser
        History, // The files opened from the file browser
        Hotlist, // The directory hotlist of the file browser
        Search, // The files found by a search in the file browser
        ColorScheme, // The color scheme editor
        Logos, // The logos and the logo editor
        Jump, // The Jump menu
        Projects, // The list of projects
        Clipboard, // The clipboard settings
        Editor
    }

    /// <summary>
    /// Show the help: the keys of the page it is asked for from, with the option to see all keys
    /// </summary>
    public static void WriteEditorHelp(HelpTopic topic = HelpTopic.All)
    {
        FillHelp();
        ShowHelp(topic);
    }

    /// <summary>
    /// What a command does, from its row in the help of a place (or else anywhere in the help): the row whose keys
    /// have ":name" ("Duplicate the selection (or the line)" for "dup"). Null if the help has no row for it
    /// </summary>
    internal static string? CommandDescription(HelpTopic topic, string name)
    {
        FillHelp();
        var command = ":" + name;
        bool Names(Shortcut row) => !row.IsHeadline && row.GetDescription().Length > 0 &&
                                    row.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                        .Any(key => key.Equals(command, StringComparison.OrdinalIgnoreCase));
        var (first, count) = HelpRange(topic);
        return (_shortcutList.Skip(first).Take(count).FirstOrDefault(Names) ?? _shortcutList.FirstOrDefault(Names))
            ?.GetDescription();
    }

    // The rows of the help, made once (in the language in use: they are made again when it changes)
    private static void FillHelp()
    {
        if (_shortcutList.Count == 0)
        {
            _shortcutList = new List<Shortcut>();
            // Start by collecting all shortcuts
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Keyboard Functions"), true));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionStart, true));
            _shortcutList.Add(new Shortcut("j k 1-9", Lang.T("Select"), false));
            _shortcutList.Add(new Shortcut("gg G", Lang.T("First/Last (also in Configuration)"), false));
            _shortcutList.Add(new Shortcut("Enter", Lang.T("Open the selected"), false));
            _shortcutList.Add(new Shortcut("N B R F P O M U", Lang.T("Open the choice of the key at once"), false));
            _shortcutList.Add(new Shortcut("  N", Lang.T("New file (Tab in the box: another folder)"), false));
            _shortcutList.Add(new Shortcut("  B", Lang.T("Blank page: write first, the name and folder when saved"), false));
            _shortcutList.Add(new Shortcut("  R", Lang.T("Recover unsaved text (shown when there is some)"), false));
            _shortcutList.Add(new Shortcut("  F", Lang.T("File browser: open, make and manage files"), false));
            _shortcutList.Add(new Shortcut("  P", Lang.T("New project: a folder in the projects folder"), false));
            _shortcutList.Add(new Shortcut("  O", Lang.T("Open project: pick one in the list of projects"), false));
            _shortcutList.Add(new Shortcut("      C", Lang.T("In the list: the color of the project"), false));
            _shortcutList.Add(new Shortcut("      N", Lang.T("In the list: the note of the project, in the editor"), false));
            _shortcutList.Add(new Shortcut("  M Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut("  U", Lang.T("Quick guide: a short introduction"), false));
            _shortcutList.Add(new Shortcut("  " + DialogText.StartExit, Lang.T("Exit Blyant (without asking)"), false));
            _shortcutList.Add(new Shortcut("Tab", Lang.T("Next tab: Start, Configuration, Help"), false));
            _shortcutList.Add(new Shortcut("Shift+Tab", Lang.T("The tab before"), false));
            _shortcutList.Add(new Shortcut("Q Esc", Lang.T("Exit Blyant, asking first (Configuration: to Start)"), false));
            _shortcutList.Add(new Shortcut("X", Lang.T("Hide the hints (Configuration, System: show them)"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line, on every tab (Tab completes)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut(":new :new name", Lang.T("New file (Tab in the box: another folder)"), false));
            _shortcutList.Add(new Shortcut(":blank", Lang.T("Blank page: write first, the name and folder when saved"), false));
            _shortcutList.Add(new Shortcut(":recover", Lang.T("Recover unsaved text (shown when there is some)"), false));
            _shortcutList.Add(new Shortcut(":file :open", Lang.T("File browser: open, make and manage files"), false));
            _shortcutList.Add(new Shortcut(":project name", Lang.T("New project (asks for the name if not given)"), false));
            _shortcutList.Add(new Shortcut(":projects :pro", Lang.T("Open project: the list of projects"), false));
            _shortcutList.Add(new Shortcut(":start :config", Lang.T("The Start tab/Configuration (General)"), false));
            _shortcutList.Add(new Shortcut(":layout :editor", Lang.T("The pages of Configuration (and :system)"), false));
            _shortcutList.Add(new Shortcut(":jump :help", Lang.T("The Jump menu/The help (this list)"), false));
            _shortcutList.Add(new Shortcut(":about :quick", Lang.T("About/The quick guide"), false));
            _shortcutList.Add(new Shortcut(":scheme :logo", Lang.T("Color schemes/Logos"), false));
            _shortcutList.Add(new Shortcut(":snip :clipboard", Lang.T("Snippet editor/Clipboard settings"), false));
            _shortcutList.Add(new Shortcut(":completions", Lang.T("Completions editor"), false));
            _shortcutList.Add(new Shortcut(":log :restart", Lang.T("View log/Restart Blyant"), false));
            _shortcutList.Add(new Shortcut(":reset", DialogText.ConfigReset, false));
            _shortcutList.Add(new Shortcut(":q :exit", Lang.T("Exit Blyant (asks first)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Configuration"), Lang.T("The pages General, Layout, Editor and System"), false));
            _shortcutList.Add(new Shortcut("j k 1-9", Lang.T("Select a setting (or the number of its row)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Startup"), Lang.T("The start page, the file browser or a blank page"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("New files"), Lang.T("Their kind (Markdown, plain text, HTML ...): Enter"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Projects folder"), Lang.T("Enter: in Documents, another, or none"), false));
            _shortcutList.Add(new Shortcut("Enter Space", Lang.T("Change the selected setting"), false));
            _shortcutList.Add(new Shortcut("l h", Lang.T("Configuration: the next/previous page"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Spell checker"), Lang.T("Enter: turn languages on or off in a list"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Snippet editor"), Lang.T("The file of snippets in the editor"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Completions"), Lang.T("Complete words while typing, and the file"), false));
            _shortcutList.Add(new Shortcut("  " + DialogText.FileInfo, Lang.T("The version, and where the program and the data are"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Color scheme"), Lang.T("Enter: pick from a list (or edit the colors)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Language"), Lang.T("Enter: pick from a list"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Layout"), Lang.T("Logo animation: Slow, Fast, Very fast or Off"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Layout"), Lang.T("Classic ASCII corners: + instead of . and '"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("View log"), Lang.T("/text only shows lines containing \"text\""), false));
            _shortcutList.Add(new Shortcut("  " + FileIo.SettingsFileName, Lang.T("Edit the settings file (saved: Blyant restarts)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Help"), Lang.T("The tab Help: all the keys, guides and About"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionFiles, true));
            _shortcutList.Add(new Shortcut(":jump Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut("j k", Lang.T("Select (as Down/Up)"), false));
            _shortcutList.Add(new Shortcut("gg G", Lang.T("First/Last"), false));
            _shortcutList.Add(new Shortcut("PgUp PgDn", Lang.T("One page up/down"), false));
            _shortcutList.Add(new Shortcut("l Enter", Lang.T("Open the directory or file"), false));
            _shortcutList.Add(new Shortcut("h - Backspace", Lang.T("Go to the directory above"), false));
            _shortcutList.Add(new Shortcut("/text", Lang.T("Only show names containing \"text\" (Esc: all)"), false));
            _shortcutList.Add(new Shortcut("Tab", Lang.T("Go to the other pane"), false));
            _shortcutList.Add(new Shortcut("T :panes", Lang.T("Show two panes or one"), false));
            _shortcutList.Add(new Shortcut(":match", Lang.T("The other pane shows this directory too"), false));
            _shortcutList.Add(new Shortcut("N :new name", Lang.T("Create a new file and open it"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("No extension"), Lang.T("A Markdown file: \"chapter\" is chapter.md"), false));
            _shortcutList.Add(new Shortcut("E :projects :pro", Lang.T("The projects: Enter shows one in this pane"), false));
            _shortcutList.Add(new Shortcut("I :info", Lang.T("Show type, size, dates and path"), false));
            _shortcutList.Add(new Shortcut("A :mkdir name", Lang.T("Create a new directory"), false));
            _shortcutList.Add(new Shortcut("X :rename name", Lang.T("Rename the file or directory"), false));
            _shortcutList.Add(new Shortcut("Space", Lang.T("Mark/Unmark: Y C M P D work on the marked"), false));
            _shortcutList.Add(new Shortcut("V :peek", Lang.T("Peek on/off: the file in the other pane, j/k the next"), false));
            _shortcutList.Add(new Shortcut("  Tab", Lang.T("Peeking: into the peek, to scroll (Tab: back)"), false));
            _shortcutList.Add(new Shortcut("Y :duplicate", Lang.T("Make a copy in the same directory"), false));
            _shortcutList.Add(new Shortcut("C :copy", Lang.T("Copy: go to the directory, then S (Esc cancels)"), false));
            _shortcutList.Add(new Shortcut("M :move", Lang.T("Move: go to the directory, then S (Esc cancels)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Two panes"), Lang.T("C and M go to the other pane: S copies to it"), false));
            _shortcutList.Add(new Shortcut("D :delete", Lang.T("Delete to the trash (asks first)"), false));
            _shortcutList.Add(new Shortcut("P :fav", Lang.T("Add to/Remove from the favorites (*)"), false));
            _shortcutList.Add(new Shortcut("F :favorites", Lang.T("Show the favorites"), false));
            _shortcutList.Add(new Shortcut("O :history", Lang.T("Show the files opened before (file history)"), false));
            _shortcutList.Add(new Shortcut("B :hotadd", Lang.T("Add the directory shown to the hotlist"), false));
            _shortcutList.Add(new Shortcut("W :hotlist", Lang.T("Show the directory hotlist"), false));
            _shortcutList.Add(new Shortcut("S :find", Lang.T("Search for files by name and by text"), false));
            _shortcutList.Add(new Shortcut(":path", Lang.T("Copy the path to the clipboard (:clipboard)"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T(":path and :name: also p in the editor"), false));
            _shortcutList.Add(new Shortcut(":name", Lang.T("Copy the name to the clipboard"), false));
            _shortcutList.Add(new Shortcut(":bookmark", Lang.T("Bookmark the selected file or directory (Jump menu)"), false));
            _shortcutList.Add(new Shortcut(":diff", Lang.T("Compare 2 marked files, or 1 in each pane"), false));
            _shortcutList.Add(new Shortcut(":diff name", Lang.T("Compare the selected file with that file"), false));
            _shortcutList.Add(new Shortcut(":diff -w", Lang.T("Compare, blank space does not count"), false));
            _shortcutList.Add(new Shortcut("R :refresh", Lang.T("Refresh"), false));
            _shortcutList.Add(new Shortcut(":cd path", Lang.T("Go to a directory"), false));
            _shortcutList.Add(new Shortcut(":home", Lang.T("Go to the projects folder (or the home directory)"), false));
            _shortcutList.Add(new Shortcut(":data", Lang.T("Go to the data directory (settings, snippets ...)"), false));
            _shortcutList.Add(new Shortcut(":trash", Lang.T("Go to the trash (what was deleted)"), false));
            _shortcutList.Add(new Shortcut(":recovery", Lang.T("Go to the recovery copies of unsaved text"), false));
            _shortcutList.Add(new Shortcut(":project add", Lang.T("Add the directory shown to the projects"), false));
            _shortcutList.Add(new Shortcut(":protect off on", Lang.T("Lift/restore the protection of Blyant's files (this session)"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (Tab completes the command)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut("Esc Q :q", Lang.T("Close the file browser"), false));
            _shortcutList.Add(new Shortcut(":exit", Lang.T("Exit Blyant (asks first)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Text files"), Lang.T("Opened in the editor"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Saving"), Lang.T("As UTF-8, with the line endings of the file"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Not UTF-8"), Lang.T("Can be converted (replace file or save copy)"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionSearch, true));
            _shortcutList.Add(new Shortcut(":jump Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Name"), Lang.T("Part of the name, or with * and ? (*.md)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Text in files"), Lang.T("Text files containing the text"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Regular expr."), Lang.T("Name and text are regular expressions"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select in the list of files found"), false));
            _shortcutList.Add(new Shortcut("l Enter", Lang.T("Open the file (at the line found)"), false));
            _shortcutList.Add(new Shortcut("B :goto", Lang.T("Show the file in the file browser"), false));
            _shortcutList.Add(new Shortcut("I :info", Lang.T("Show type, size, dates and path"), false));
            _shortcutList.Add(new Shortcut("P :fav", Lang.T("Add to/Remove from the favorites (*)"), false));
            _shortcutList.Add(new Shortcut("/text", Lang.T("Only show the files with \"text\" (Esc: all)"), false));
            _shortcutList.Add(new Shortcut("S :find", Lang.T("Search again"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (Tab completes the command)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut("Esc Q h -", Lang.T("Back to the file browser"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionFavorites, true));
            _shortcutList.Add(new Shortcut(":jump Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select"), false));
            _shortcutList.Add(new Shortcut("l Enter", Lang.T("Open the file (a directory: go to it)"), false));
            _shortcutList.Add(new Shortcut("B :goto", Lang.T("Show the file in the file browser"), false));
            _shortcutList.Add(new Shortcut("V", Lang.T("Move the favorite or folder (j k, then Esc)"), false));
            _shortcutList.Add(new Shortcut("A :folder name", Lang.T("New folder (a group of favorites)"), false));
            _shortcutList.Add(new Shortcut("l Enter", Lang.T("On a folder: Open/Close it"), false));
            _shortcutList.Add(new Shortcut(":expand", Lang.T("Open all the folders"), false));
            _shortcutList.Add(new Shortcut(":collapse", Lang.T("Close all the folders"), false));
            _shortcutList.Add(new Shortcut("X :rename name", Lang.T("Rename the folder"), false));
            _shortcutList.Add(new Shortcut("Space", Lang.T("Mark/Unmark the favorite (several can be marked)"), false));
            _shortcutList.Add(new Shortcut("M :move", Lang.T("Move the marked (or selected) to a folder"), false));
            _shortcutList.Add(new Shortcut("T :top", Lang.T("Move the marked (or selected) to the top"), false));
            _shortcutList.Add(new Shortcut("E :bottom", Lang.T("Move the marked (or selected) to the end"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("In a folder"), Lang.T("T and E move to the top/end of the folder"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Marked"), Lang.T("D removes them all. Esc clears the marks"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Into a folder"), Lang.T("M, or move the favorite onto the folder"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Out of a folder"), Lang.T("Move it past the first or last in it"), false));
            _shortcutList.Add(new Shortcut("C :color", Lang.T("Pick the text color of the favorite"), false));
            _shortcutList.Add(new Shortcut(":path :name", Lang.T("Copy the path/the name shown (also p in the editor)"), false));
            _shortcutList.Add(new Shortcut("I :info", Lang.T("Show type, size, dates and path"), false));
            _shortcutList.Add(new Shortcut("P D :remove", Lang.T("Remove the favorite or folder (files stay)"), false));
            _shortcutList.Add(new Shortcut("/text", Lang.T("Only show paths containing \"text\" (Esc: all)"), false));
            _shortcutList.Add(new Shortcut("u :undo", Lang.T("Put back what was removed last"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (Tab completes the command)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut("Esc Q F h -", Lang.T("Back to the file browser"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("The list"), Lang.T("Kept in favorites.txt in the data directory"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionHistory, true));
            _shortcutList.Add(new Shortcut(":jump Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select"), false));
            _shortcutList.Add(new Shortcut("l Enter", Lang.T("Open the file in the editor"), false));
            _shortcutList.Add(new Shortcut("B :goto", Lang.T("Show the file in the file browser"), false));
            _shortcutList.Add(new Shortcut("P :fav", Lang.T("Add to/Remove from the favorites (*)"), false));
            _shortcutList.Add(new Shortcut("I :info", Lang.T("Show type, size, dates and path"), false));
            _shortcutList.Add(new Shortcut("D :remove", Lang.T("Take the file out of the history (it stays)"), false));
            _shortcutList.Add(new Shortcut("X :clear", Lang.T("Clear the file history (asks first)"), false));
            _shortcutList.Add(new Shortcut(":path :name", Lang.T("Copy the path/the name shown (also p in the editor)"), false));
            _shortcutList.Add(new Shortcut("/text", Lang.T("Only show paths containing \"text\" (Esc: all)"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (Tab completes the command)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut("Esc Q O h -", Lang.T("Back to the file browser"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("The list"), Lang.F("The last {0} files opened, kept in history.txt", FileHistory.MaxCount), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionHotlist, true));
            _shortcutList.Add(new Shortcut(":jump Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select"), false));
            _shortcutList.Add(new Shortcut("l Enter", Lang.T("Go to the directory (in the pane with the focus)"), false));
            _shortcutList.Add(new Shortcut("V :move", Lang.T("Move the directory (j k, then Esc)"), false));
            _shortcutList.Add(new Shortcut("T :top", Lang.T("Move the directory to the top"), false));
            _shortcutList.Add(new Shortcut("E :bottom", Lang.T("Move the directory to the end"), false));
            _shortcutList.Add(new Shortcut("I :info", Lang.T("Show type, size, dates and path"), false));
            _shortcutList.Add(new Shortcut("X :rename name", Lang.T("The name shown (the directory keeps its own)"), false));
            _shortcutList.Add(new Shortcut("D :remove", Lang.T("Take it out of the hotlist (the directory stays)"), false));
            _shortcutList.Add(new Shortcut("u :undo", Lang.T("Put back what was removed last"), false));
            _shortcutList.Add(new Shortcut(":clear", Lang.T("Clear the hotlist (asks first)"), false));
            _shortcutList.Add(new Shortcut(":path :name", Lang.T("Copy the path/the name shown (also p in the editor)"), false));
            _shortcutList.Add(new Shortcut("/text", Lang.T("Only show paths containing \"text\" (Esc: all)"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (Tab completes the command)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut("Esc Q W h -", Lang.T("Back to the file browser"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("The list"), Lang.F("At most {0} directories, kept in hotlist.txt", DirectoryHotlist.MaxCount), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionProjects, true));
            _shortcutList.Add(new Shortcut("O :projects :pro", Lang.T("Open project (start page; E in the file browser)"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select a project"), false));
            _shortcutList.Add(new Shortcut("Enter l", Lang.T("Show the project in the file browser"), false));
            _shortcutList.Add(new Shortcut("Tab", Lang.T("Tabs: All, Added, Projects folder (when both kinds)"), false));
            _shortcutList.Add(new Shortcut("P :new", Lang.T("New project (Added tab: in a folder you pick)"), false));
            _shortcutList.Add(new Shortcut("A :add", Lang.T("Add an existing folder to the projects"), false));
            _shortcutList.Add(new Shortcut("D :remove", Lang.T("Take an added project off the list (asks first)"), false));
            _shortcutList.Add(new Shortcut("C :color", Lang.T("The color of the project in the lists"), false));
            _shortcutList.Add(new Shortcut("N :note", Lang.T("The note of the project, in the editor"), false));
            _shortcutList.Add(new Shortcut("I :info", Lang.T("Information about the project folder"), false));
            _shortcutList.Add(new Shortcut(":path :name", Lang.T("Copy the path/the name of the folder"), false));
            _shortcutList.Add(new Shortcut("/", Lang.T("Filter the projects by name (Esc: all)"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (Tab completes the command)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut("? :help", Lang.T("The keys of the list (this help)"), false));
            _shortcutList.Add(new Shortcut("Esc Q :q", Lang.T("Close the list"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionJump, true));
            _shortcutList.Add(new Shortcut(":jump :ju :jmp", Lang.T("Open the Jump menu (from most places)"), false));
            _shortcutList.Add(new Shortcut("Ctrl+O", Lang.T("Open the Jump menu (from most places)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Tabs"), Lang.T("Bookmarks, Recent, Projects, File browser, Blyant"), false));
            _shortcutList.Add(new Shortcut("  " + DialogText.CaptionsFiles, Lang.T("Its lists: History, Favorites, Hotlist"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select a place"), false));
            _shortcutList.Add(new Shortcut("h l", Lang.T("The list before/after (through all the lists)"), false));
            _shortcutList.Add(new Shortcut("Tab Shift+Tab", Lang.T("The next/previous tab"), false));
            _shortcutList.Add(new Shortcut("Enter", Lang.T("Jump: open the file, directory or page"), false));
            _shortcutList.Add(new Shortcut("/", Lang.T("Filter the places of the tab by name (Esc: all)"), false));
            _shortcutList.Add(new Shortcut("B", Lang.T("Bookmark the selected place (bookmarks.txt)"), false));
            _shortcutList.Add(new Shortcut("I D", Lang.T("Info/Remove (from Recent or Bookmarks)"), false));
            _shortcutList.Add(new Shortcut("Esc Q", Lang.T("Close the Jump menu"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Recent"), Lang.T("The places jumped to, the latest first (jump.txt)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Projects"), Lang.T("The projects: Enter shows one in the file browser"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Bookmarks"), Lang.T("Places kept with B, or :bookmark in the editor"), false));
            _shortcutList.Add(new Shortcut("V j k Esc", Lang.T("Bookmarks: move the selected one"), false));
            _shortcutList.Add(new Shortcut("T E", Lang.T("Bookmarks: move it to the top/end"), false));
            _shortcutList.Add(new Shortcut("X", Lang.T("Bookmarks: the name shown (the place keeps its own)"), false));
            _shortcutList.Add(new Shortcut("C :color", Lang.T("Bookmarks: text color (used before a favorite's)"), false));
            _shortcutList.Add(new Shortcut("A :folder", Lang.T("Bookmarks: new folder (Enter opens/closes it)"), false));
            _shortcutList.Add(new Shortcut("Space M", Lang.T("Bookmarks: mark several/move them to a folder"), false));
            _shortcutList.Add(new Shortcut(":expand :collapse", Lang.T("Bookmarks: open/close all the folders"), false));
            _shortcutList.Add(new Shortcut("u :clearbook", Lang.T("Bookmarks: put back the removed/clear (asks first)"), false));
            _shortcutList.Add(new Shortcut(":import favorites", Lang.T("Bookmarks: copy the favorites of the file browser"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("A command (on the Bookmarks tab)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut(":path :name", Lang.T("Copy the path/the name of the file (all tabs)"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut("  :book 3", Lang.T("The number is shown at the right of the bookmark"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionClipboard, true));
            _shortcutList.Add(new Shortcut("\"+y \"+yy", Lang.T("Editor: yank to the clipboard (and/or the paste bin)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Every yank"), Lang.T("Can be set to go out too (not deletes)"), false));
            _shortcutList.Add(new Shortcut(":pb", Lang.T("Write the selection (or the text) to the paste bin"), false));
            _shortcutList.Add(new Shortcut(":pbappend", Lang.T("Add the selection (or the text) to the paste bin"), false));
            _shortcutList.Add(new Shortcut(":pbread", Lang.T("Insert the text of the paste bin at the cursor"), false));
            _shortcutList.Add(new Shortcut(":pbclear", Lang.T("Empty the paste bin"), false));
            _shortcutList.Add(new Shortcut(":path", Lang.T("File browser, editor: copy the path of the file"), false));
            _shortcutList.Add(new Shortcut(":clipboard", Lang.T("The clipboard settings (start page, editor)"), false));
            _shortcutList.Add(new Shortcut("j k Enter Space", Lang.T("Settings: select and change"), false));
            _shortcutList.Add(new Shortcut("h l", Lang.T("Settings: the choice of a row (every yank, add/replace)"), false));
            _shortcutList.Add(new Shortcut("  OSC 52", Lang.T("Windows Terminal, iTerm2, kitty, WezTerm, tmux, SSH"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Paste bin"), Lang.T("For terminals without OSC 52 (Raspberry Pi)"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionColorScheme, true));
            _shortcutList.Add(new Shortcut("Tab", Lang.T("Next tab: Schemes, Colors, Preview"), false));
            _shortcutList.Add(new Shortcut("l h", Lang.T("Colors: the next/previous page (Page 1 to 3)"), false));
            _shortcutList.Add(new Shortcut("j k gg G", Lang.T("Select a scheme or a group of colors"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Schemes"), Lang.T("Enter use, Y duplicate, R rename, D delete"), false));
            _shortcutList.Add(new Shortcut("Enter Space", Lang.T("On a group: pick its color from a list"), false));
            _shortcutList.Add(new Shortcut("T", Lang.T("Pick the text color (Highlight, Attention ... Tabs)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Tab highlight"), Lang.T("Focused tab ([Default]: Tab focus on the tab frames)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Tab focus"), Lang.T("Its name, when the tab is not highlighted"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Other tabs"), Lang.T("The names of the tabs not shown"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Tab corner"), Lang.T("The left side, corner and top of the tab shown"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Selector"), Lang.T("The arrow -> of lists, menus and settings"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Tab text"), Lang.T("The text in frames with tabs and in boxes"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.F("Page {0}", 3), Lang.T("Syntax 1-12: the colors of colored text"), false));
            _shortcutList.Add(new Shortcut("E", Lang.T("Back to the colors of the scheme in use"), false));
            _shortcutList.Add(new Shortcut("S", Lang.T("Save as a scheme of your own (asks for a name)"), false));
            _shortcutList.Add(new Shortcut("Esc Q", Lang.T("Close (asks to save changes)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Open"), Lang.T(":scheme, or Configuration: Color scheme"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionLogos, true));
            _shortcutList.Add(new Shortcut("Tab", Lang.T("Logos, Editor, Colors"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Logos"), Lang.T("Enter use, E edit, Y duplicate, R rename, D delete"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Editor"), Lang.T("4 rows of 63 characters, with a ruler above"), false));
            _shortcutList.Add(new Shortcut("h j k l", Lang.T("Move (0 $: start/end of the row, gg G: first/last row)"), false));
            _shortcutList.Add(new Shortcut("i R", Lang.T("REPLACE mode: what is typed is drawn (Esc: NORMAL)"), false));
            _shortcutList.Add(new Shortcut("a", Lang.T("REPLACE mode after the cursor"), false));
            _shortcutList.Add(new Shortcut("x r", Lang.T("Clear the character/Replace it with the next key"), false));
            _shortcutList.Add(new Shortcut("dd u", Lang.T("Clear the row/Undo"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Colors"), Lang.T("Each row in a color of its own, or all in one"), false));
            _shortcutList.Add(new Shortcut("l h", Lang.T("Colors: Rows/Cells (on the grid of Cells: L H)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Cells"), Lang.T("Cells in colors of their own, painted on the grid"), false));
            _shortcutList.Add(new Shortcut("c Space", Lang.T("Pick the color to paint with/Paint the cell"), false));
            _shortcutList.Add(new Shortcut("e", Lang.T("Paint with the color of the cell"), false));
            _shortcutList.Add(new Shortcut("x X", Lang.T("Clear the color of the cell/of all the cells"), false));
            _shortcutList.Add(new Shortcut("v", Lang.T("Select a rectangle to paint or clear (Esc: cancel)"), false));
            _shortcutList.Add(new Shortcut("S", Lang.T("Save as a logo of your own (asks for a name)"), false));
            _shortcutList.Add(new Shortcut("Esc Q", Lang.T("Close (asks to save changes)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Open"), Lang.T(":logo, or Configuration: Logo"), false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionEditor, true));
            _shortcutList.Add(new Shortcut(":jump Ctrl+O", Lang.T("The Jump menu: files, directories and pages"), false));
            _shortcutList.Add(new Shortcut(":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Modes"), true));
            _shortcutList.Add(new Shortcut("Esc", Lang.T("Normal mode (navigate and give commands)"), false));
            _shortcutList.Add(new Shortcut("i a", Lang.T("Insert mode before/after the cursor"), false));
            _shortcutList.Add(new Shortcut("I A", Lang.T("Insert mode at line start/end"), false));
            _shortcutList.Add(new Shortcut("o O", Lang.T("Insert mode on a new line below/above"), false));
            _shortcutList.Add(new Shortcut("v V Q", Lang.T("Visual mode (select text/lines/a block)"), false));
            _shortcutList.Add(new Shortcut(":vsel :vline", Lang.T("Select text/lines (as v and V)"), false));
            _shortcutList.Add(new Shortcut(":block", Lang.T("Select a block (as Q)"), false));
            _shortcutList.Add(new Shortcut("gv", Lang.T("Select the last selection again"), false));
            _shortcutList.Add(new Shortcut(":", Lang.T("Command line (type a command, press Enter)"), false));
            _shortcutList.Add(new Shortcut("  Tab", Lang.T("Complete the command (again: the next, Shift: back)"), false));
            _shortcutList.Add(new Shortcut("  Up Down", Lang.T("Earlier commands of this page"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Save and close"), true));
            _shortcutList.Add(new Shortcut(":w", Lang.T("Save"), false));
            _shortcutList.Add(new Shortcut(":wq :x ZZ", Lang.T("Save and close editor"), false));
            _shortcutList.Add(new Shortcut(":q Esc", Lang.T("Close editor (asks to save changes)"), false));
            _shortcutList.Add(new Shortcut(":q! ZQ", Lang.T("Close editor without saving"), false));
            _shortcutList.Add(new Shortcut("Ctrl+C", Lang.T("Does not close the editor (it tells how to)"), false));
            _shortcutList.Add(new Shortcut(":e :e!", Lang.T("Read the file again (!: drop changes)"), false));
            _shortcutList.Add(new Shortcut(":help", Lang.T("Show this help"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Moving the cursor (Normal and Visual mode)"), true));
            _shortcutList.Add(new Shortcut("h j k l", Lang.T("Left/Down/Up/Right (as the arrow keys)"), false));
            _shortcutList.Add(new Shortcut("w b e", Lang.T("Next word/Previous word/End of word"), false));
            _shortcutList.Add(new Shortcut("W B E", Lang.T("As above, words separated by blank space"), false));
            _shortcutList.Add(new Shortcut("0 _ $", Lang.T("Line start/First character/Line end"), false));
            _shortcutList.Add(new Shortcut("gg G", Lang.T("Document top/Document end"), false));
            _shortcutList.Add(new Shortcut("5G :5", Lang.T("Go to line 5"), false));
            _shortcutList.Add(new Shortcut("{ }", Lang.T("Previous/Next blank line"), false));
            _shortcutList.Add(new Shortcut("f. t.", Lang.T("To/Before next \".\" on line (F T: backwards)"), false));
            _shortcutList.Add(new Shortcut("; ,", Lang.T("Repeat f/t forwards/backwards"), false));
            _shortcutList.Add(new Shortcut("PgUp PgDn", Lang.T("Move one page up/down"), false));
            _shortcutList.Add(new Shortcut("CTRL+U CTRL+D", Lang.T("Move half a page up/down"), false));
            _shortcutList.Add(new Shortcut("gm gM", Lang.T("Move half the screen width right/left"), false));
            _shortcutList.Add(new Shortcut("zh zl", Lang.T("Not wrapped: scroll the view left/right"), false));
            _shortcutList.Add(new Shortcut("zH zL", Lang.T("Not wrapped: scroll half the width left/right"), false));
            _shortcutList.Add(new Shortcut("zs ze", Lang.T("Not wrapped: the cursor at the left/right side"), false));
            _shortcutList.Add(new Shortcut("zt zz zb", Lang.T("Scroll cursor row to top/center/bottom"), false));
            _shortcutList.Add(new Shortcut("5j 3w ...", Lang.T("A number repeats a movement or command"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Search"), true));
            _shortcutList.Add(new Shortcut("/text ?text", Lang.T("Search forwards/backwards"), false));
            _shortcutList.Add(new Shortcut("/\\d+", Lang.T("Regular expressions can be used"), false));
            _shortcutList.Add(new Shortcut("/Text", Lang.T("Uppercase letters make the search match case"), false));
            _shortcutList.Add(new Shortcut("n N", Lang.T("Next/Previous match"), false));
            _shortcutList.Add(new Shortcut("*", Lang.T("Search for the word under the cursor"), false));
            _shortcutList.Add(new Shortcut("Esc :noh", Lang.T("Remove search highlighting (Esc again closes)"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Spell checking"), true));
            _shortcutList.Add(new Shortcut(":spell", Lang.T("Mark the misspelled words (again: remove the marks)"), false));
            _shortcutList.Add(new Shortcut(":spell da,en", Lang.T("As above, and set the languages"), false));
            _shortcutList.Add(new Shortcut(":nospell", Lang.T("Remove the marks"), false));
            _shortcutList.Add(new Shortcut("]s gs :spellnext", Lang.T("Next misspelled word"), false));
            _shortcutList.Add(new Shortcut("[s gS :spellprev", Lang.T("Previous misspelled word"), false));
            _shortcutList.Add(new Shortcut("z= :suggest", Lang.T("Suggest spellings for the word"), false));
            _shortcutList.Add(new Shortcut("zg :spelladd", Lang.T("Add the word to the word list"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Replace (command line)"), true));
            _shortcutList.Add(new Shortcut(":replace", Lang.T("Fill in a box instead of writing the command"), false));
            _shortcutList.Add(new Shortcut(":s/old/new/", Lang.T("Replace first \"old\" on the line with \"new\""), false));
            _shortcutList.Add(new Shortcut(":s/old/new/g", Lang.T("Replace all on the line"), false));
            _shortcutList.Add(new Shortcut(":%s/old/new/g", Lang.T("Replace all in the document"), false));
            _shortcutList.Add(new Shortcut(":2,9s/old/new/g", Lang.T("Replace all on line 2 to 9"), false));
            _shortcutList.Add(new Shortcut("V ... :s/old/new/g", Lang.T("Replace all on the selected lines"), false));
            _shortcutList.Add(new Shortcut("  & \\1 \\n", Lang.T("In \"new\": the match/group 1/line break"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Editing (Normal mode)"), true));
            _shortcutList.Add(new Shortcut("x X", Lang.T("Delete character under/before the cursor"), false));
            _shortcutList.Add(new Shortcut("r.", Lang.T("Replace character with \".\""), false));
            _shortcutList.Add(new Shortcut("d c y", Lang.T("Delete/Change/Yank (copy) + movement:"), false));
            _shortcutList.Add(new Shortcut("  dw d$ dG", Lang.T("Delete word/to line end/to document end"), false));
            _shortcutList.Add(new Shortcut("  cw ciw ci\"", Lang.T("Change word/whole word/text in quotes"), false));
            _shortcutList.Add(new Shortcut("  yy dd cc", Lang.T("Yank/Delete/Change line (3dd: 3 lines)"), false));
            _shortcutList.Add(new Shortcut("  yiw yip dip", Lang.T("Yank word/Yank paragraph/Delete paragraph"), false));
            _shortcutList.Add(new Shortcut("  di\" diB dap", Lang.T("Delete in quotes/in { }/paragraph and blank lines"), false));
            _shortcutList.Add(new Shortcut("D C Y", Lang.T("Delete/Change to line end, Yank line"), false));
            _shortcutList.Add(new Shortcut("p P", Lang.T("Paste after/before the cursor"), false));
            _shortcutList.Add(new Shortcut("J", Lang.T("Join line with the next"), false));
            _shortcutList.Add(new Shortcut(">> <<", Lang.T("Indent/Unindent line"), false));
            _shortcutList.Add(new Shortcut("~", Lang.T("Switch case of character"), false));
            _shortcutList.Add(new Shortcut("gUU guu", Lang.T("Line to UPPERCASE/lowercase"), false));
            _shortcutList.Add(new Shortcut("gUw guw", Lang.T("Word to UPPERCASE/lowercase"), false));
            _shortcutList.Add(new Shortcut("u", Lang.F("Undo ({0} steps)", TextBuffer.MaxUndoSteps), false));
            _shortcutList.Add(new Shortcut("U CTRL+R", Lang.T("Redo"), false));
            _shortcutList.Add(new Shortcut(".", Lang.T("Repeat last change"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Visual mode (select with the movement keys)"), true));
            _shortcutList.Add(new Shortcut("d y c", Lang.T("Delete/Yank/Change selection"), false));
            _shortcutList.Add(new Shortcut("p", Lang.T("Replace selection with pasted text"), false));
            _shortcutList.Add(new Shortcut("U u ~", Lang.T("Selection to UPPERCASE/lowercase/switch"), false));
            _shortcutList.Add(new Shortcut("> <", Lang.T("Indent/Unindent the selected lines"), false));
            _shortcutList.Add(new Shortcut("J K", Lang.T("V-LINE: Move the selected lines down/up"), false));
            _shortcutList.Add(new Shortcut("J", Lang.T("VISUAL: Join the selected lines"), false));
            _shortcutList.Add(new Shortcut("viw vaw", Lang.T("Select word (a: with the blank space after it)"), false));
            _shortcutList.Add(new Shortcut("vi\" va\" vi' va'", Lang.T("Select in quotes (a: with the quotes)"), false));
            _shortcutList.Add(new Shortcut("vib viB", Lang.T("Select in ( )/in { }, also over lines (a: with them)"), false));
            _shortcutList.Add(new Shortcut("vip vap", Lang.T("Select paragraph (a: with the blank lines after it)"), false));
            _shortcutList.Add(new Shortcut("o", Lang.T("Move cursor to other end of selection"), false));
            _shortcutList.Add(new Shortcut("v V Q", Lang.T("Switch to text/lines/block (again: leave)"), false));
            _shortcutList.Add(new Shortcut("gv", Lang.T("Select the last selection again"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Visual block (Q: the same columns on each line)"), true));
            _shortcutList.Add(new Shortcut("$", Lang.T("The block goes to the end of each line"), false));
            _shortcutList.Add(new Shortcut("I A", Lang.T("Type before/after the block on each line"), false));
            _shortcutList.Add(new Shortcut("d y c", Lang.T("Delete/Yank/Change the block"), false));
            _shortcutList.Add(new Shortcut("D Y C", Lang.T("The same, to the end of each line"), false));
            _shortcutList.Add(new Shortcut("p", Lang.T("Paste (a yanked block is pasted as a block)"), false));
            _shortcutList.Add(new Shortcut("r", Lang.T("Replace each character with the one typed"), false));
            _shortcutList.Add(new Shortcut("U u ~", Lang.T("UPPERCASE/lowercase/switch case"), false));
            _shortcutList.Add(new Shortcut("O o", Lang.T("Other corner on the line/Opposite corner"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Insert mode"), true));
            _shortcutList.Add(new Shortcut("Tab", Lang.F("Insert blank space x {0}", EditorEngine.TabWidth), false));
            _shortcutList.Add(new Shortcut("CTRL+W", Lang.T("Delete word before the cursor"), false));
            _shortcutList.Add(new Shortcut("CTRL+U", Lang.T("Delete line before the cursor"), false));
            _shortcutList.Add(new Shortcut("CTRL+R", Lang.T("Paste current memory string"), false));
            _shortcutList.Add(new Shortcut("CTRL+K", Lang.T("Snippets: insert one at the cursor"), false));
            _shortcutList.Add(new Shortcut("Tab", Lang.T("Completions shown: complete (more: the next)"), false));
            _shortcutList.Add(new Shortcut("Shift+Tab Enter", Lang.T("Completions: the previous/use the one chosen"), false));
            _shortcutList.Add(new Shortcut("Esc", Lang.T("Completions: close the list (again: NORMAL)"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.F("String Memory (limit: {0} strings)", MaxLineMemoryCount), true));
            _shortcutList.Add(new Shortcut("y d c", Lang.T("Yanked/deleted text is added to memory"), false));
            _shortcutList.Add(new Shortcut("CTRL+P CTRL+N", Lang.T("Select previous/next memory string"), false));
            _shortcutList.Add(new Shortcut("mem 3/11", Lang.T("Status line: the selected string/how many there are"), false));
            _shortcutList.Add(new Shortcut(":reg :registers", Lang.T("Show current memory string"), false));
            _shortcutList.Add(new Shortcut(":reglist", Lang.T("All the strings in a box: pick one to insert"), false));
            _shortcutList.Add(new Shortcut(":putall", Lang.T("Insert all memory strings"), false));
            _shortcutList.Add(new Shortcut(":regclear", Lang.T("Clear string memory"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Other commands"), true));
            _shortcutList.Add(new Shortcut(":ts", Lang.T("Insert ISO timestamp (YYYY-MM-DD HH:MM:SS)"), false));
            _shortcutList.Add(new Shortcut(":trim", Lang.T("Trim blank space (line start/end)"), false));
            _shortcutList.Add(new Shortcut(":ol :ul", Lang.T("Numbered/Bulleted list (selected lines)"), false));
            _shortcutList.Add(new Shortcut(":dup", Lang.T("Duplicate the selection (or the line)"), false));
            _shortcutList.Add(new Shortcut(":dup 5", Lang.T("Make 5 copies of the selection (or the line)"), false));
            _shortcutList.Add(new Shortcut(":surround /bold", Lang.T("Put a completion around the selection (Tab: the list)"), false));
            _shortcutList.Add(new Shortcut(":sort :sort!", Lang.T("Sort the selected lines (or all), !: reversed"), false));
            _shortcutList.Add(new Shortcut(":wrap", Lang.T("Wrap long lines on/off (:set wrap, :set nowrap)"), false));
            _shortcutList.Add(new Shortcut(":scrolloff", Lang.T("Keep lines around the cursor on/off (12)"), false));
            _shortcutList.Add(new Shortcut(":scrolloff 8", Lang.T("Keep 8 lines (999: the middle, 0: off)"), false));
            _shortcutList.Add(new Shortcut(":sort i", Lang.T("Sort, upper and lower case alike"), false));
            _shortcutList.Add(new Shortcut("\"+y", Lang.T("Yank to the system clipboard (:clipboard)"), false));
            _shortcutList.Add(new Shortcut(":pb :pbread", Lang.T("Write to/Read from the paste bin"), false));
            _shortcutList.Add(new Shortcut(":info", Lang.T("Info: characters, words, lines, dates and path"), false));
            _shortcutList.Add(new Shortcut(":path", Lang.T("Copy the path of the file to the clipboard"), false));
            _shortcutList.Add(new Shortcut(":snip :sn", Lang.T("Snippets: insert, filter, sort, add, rename, delete"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Selection"), Lang.T(":snip puts a snippet with $0 around it"), false));
            _shortcutList.Add(new Shortcut(":snip add", Lang.T("Keep the selection (or the line) as a snippet"), false));
            _shortcutList.Add(new Shortcut(":snip edit", Lang.T("Edit the file of snippets (snippets.txt)"), false));
            _shortcutList.Add(new Shortcut("  $0 (css)", Lang.T("Snippets: the cursor goes to $0, (css) first in css"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Macros"), true));
            _shortcutList.Add(new Shortcut(":record", Lang.T("Record a macro (Esc in NORMAL mode stops it)"), false));
            _shortcutList.Add(new Shortcut(":play name 3", Lang.T("Play a macro (3 times); Tab completes the name"), false));
            _shortcutList.Add(new Shortcut(":macro", Lang.T("The macros in a list: Enter plays one"), false));
            _shortcutList.Add(new Shortcut("  macros/*.macro", Lang.T("A file for each macro, which can be edited"), false));
            _shortcutList.Add(new Shortcut(":complete", Lang.T("Complete words while typing on/off"), false));
            _shortcutList.Add(new Shortcut(":complete on/off", Lang.T("Turn completing on/off"), false));
            _shortcutList.Add(new Shortcut(":complete edit", Lang.T("Edit the file of completions (completions.txt)"), false));
            _shortcutList.Add(new Shortcut(":syntax", Lang.T("Colors on/off (on in the files of a known kind)"), false));
            _shortcutList.Add(new Shortcut(":syntax html", Lang.T("Color as html (or md css js json xml cs php ini)"), false));
            _shortcutList.Add(new Shortcut(":syntax cpp", Lang.T("Color as C/C++ (or rust py)"), false));
            _shortcutList.Add(new Shortcut(":syntax ps", Lang.T("Color as PowerShell (or bat sh)"), false));
            _shortcutList.Add(new Shortcut(":number :nu", Lang.T("Line numbers on/off"), false));
            _shortcutList.Add(new Shortcut(":split", Lang.T("Two panes on/off (Tab: go to the other)"), false));
            _shortcutList.Add(new Shortcut("Tab Shift+Tab", Lang.T("Split: go to the other pane (Normal mode)"), false));
            _shortcutList.Add(new Shortcut(":only :q", Lang.T("Split: close the other pane/this pane"), false));
            _shortcutList.Add(new Shortcut(":yankall", Lang.T("Yank all the text (as gg V G y; the clipboard as y)"), false));
            _shortcutList.Add(new Shortcut(":new name", Lang.T("New file next to this one"), false));
            _shortcutList.Add(new Shortcut(":projects :pro", Lang.T("Open a project in the file browser"), false));
            _shortcutList.Add(new Shortcut(":bookmark", Lang.T("Bookmark the file being edited (Jump menu)"), false));
            _shortcutList.Add(new Shortcut(":favorite :fav", Lang.T("Add/remove the file as a favorite"), false));
            _shortcutList.Add(new Shortcut(":diff :diff -w", Lang.T("Compare with the saved text (-w: not blank space)"), false));
            _shortcutList.Add(new Shortcut("  " + Lang.T("Comparison"), Lang.T("n N ]c [c: the next/previous change"), false));
            _shortcutList.Add(new Shortcut(string.Empty, Lang.T("Enter: open at the line, Esc: close"), false));
            _shortcutList.Add(new Shortcut(":w name.diff", Lang.T("Save a comparison as a file"), false));
            _shortcutList.Add(new Shortcut(":rename", Lang.T("Rename the file"), false));
            _shortcutList.Add(new Shortcut(":%y :%d :2,9d", Lang.T("Yank all/Delete all/Delete line 2 to 9"), false));
            _shortcutList.Add(new Shortcut(":clear", Lang.T("Clear all"), false));
        }
    }

    // The lines of the help that belong to a topic: from its headline to the next headline (the editor: the
    // rest of the list, as it has several headlines)
    private static (int First, int Count) HelpRange(HelpTopic topic)
    {
        var headline = topic switch
        {
            HelpTopic.Start => DialogText.HelpSectionStart,
            HelpTopic.Files => DialogText.HelpSectionFiles,
            HelpTopic.Favorites => DialogText.HelpSectionFavorites,
            HelpTopic.History => DialogText.HelpSectionHistory,
            HelpTopic.Hotlist => DialogText.HelpSectionHotlist,
            HelpTopic.Search => DialogText.HelpSectionSearch,
            HelpTopic.Clipboard => DialogText.HelpSectionClipboard,
            HelpTopic.ColorScheme => DialogText.HelpSectionColorScheme,
            HelpTopic.Logos => DialogText.HelpSectionLogos,
            HelpTopic.Jump => DialogText.HelpSectionJump,
            HelpTopic.Projects => DialogText.HelpSectionProjects,
            HelpTopic.Editor => DialogText.HelpSectionEditor,
            _ => string.Empty
        };
        var first = _shortcutList.FindIndex(s => s.IsHeadline && s.GetDescription() == headline);
        if (first < 0) return (0, _shortcutList.Count);
        var end = topic == HelpTopic.Editor
            ? -1
            : _shortcutList.FindIndex(first + 1, s => s.IsHeadline);
        return (first, (end < 0 ? _shortcutList.Count : end) - first);
    }

    // The help viewer: the list of keys shown in pages, which can be browsed both ways and searched.
    // When asked for with a topic only the keys of the topic are shown, and "?" switches to all keys and back
    private static void ShowHelp(HelpTopic topic)
    {
        var (topicFirst, topicCount) = HelpRange(topic);
        var showAll = topic == HelpTopic.All;
        var lines = showAll ? _shortcutList : _shortcutList.GetRange(topicFirst, topicCount);
        var top = 0;
        var search = string.Empty;
        var match = -1;
        var message = string.Empty;
        HideCursor();
        while (true)
        {
            Init.ApplyAutoSize();
            var pageSize = Init.LineCount - 1;
            var maxTop = Math.Max(0, lines.Count - pageSize);
            top = Math.Clamp(top, 0, maxTop);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            ClearScreen();
            for (var row = 0; row < pageSize && top + row < lines.Count; row++)
            {
                var item = lines[top + row];
                SetCursorEx(row, 0);
                item.WriteShortcut(false, true);
                // Mark the lines matching the search
                if (search.Length > 0 && item.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    SetCursorEx(row, 0);
                    ConsoleColors.SetColors(top + row == match
                        ? ConsoleColors.ColorPreset.HighlightInverted
                        : ConsoleColors.ColorPreset.Divider);
                    Console.Write(">");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }
            }

            // Page number and keys
            var pages = Math.Max(1, (lines.Count + pageSize - 1) / pageSize);
            var page = top >= maxTop ? pages : top / pageSize + 1;
            SetCursorEx(Init.LineCount - 1, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHint);
            var pageText = " " + Lang.F("Page {0} of {1}", page, pages) + " ";
            Console.Write(pageText);
            if (message.Length > 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write(FitLine(" " + message + " ", pageText.Length));
            }
            else
            {
                Console.Write(FitLine(topic == HelpTopic.All ? DialogText.HelpKeys :
                    showAll ? DialogText.HelpKeysAll : DialogText.HelpKeysTopic, pageText.Length));
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            message = string.Empty;

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                break;
            }

            if (key.KeyChar == '?' && topic != HelpTopic.All)
            {
                // Switch between the keys of the page and all keys (shown from where the page is found)
                showAll = !showAll;
                lines = showAll ? _shortcutList : _shortcutList.GetRange(topicFirst, topicCount);
                top = showAll ? topicFirst : 0;
                search = string.Empty;
                match = -1;
                continue;
            }

            if (key.KeyChar == '/')
            {
                // Search: go to the first matching line from the top of the page
                var text = CommandLine.Read('/', false);
                if (text == null) continue;
                search = text;
                match = FindHelpLine(lines, search, top - 1, true);
                if (match < 0)
                {
                    message = DialogText.HelpNotFound + search;
                    search = string.Empty;
                }
            }
            else if (key.KeyChar is 'n' or 'N' && search.Length > 0)
            {
                match = FindHelpLine(lines, search, match, key.KeyChar == 'n');
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                top = maxTop;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') top = 0;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                top = 0;
            }
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                top++;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                top--;
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.PageDown or ConsoleKey.Spacebar
                     or ConsoleKey.Enter)
            {
                top += pageSize;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.PageUp or ConsoleKey.B)
            {
                top -= pageSize;
            }

            // Show the current match, with a few lines above it
            if (match >= 0 && key.KeyChar is '/' or 'n' or 'N')
            {
                top = match - 2;
            }
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        ClearScreen();
    }

    // The next (or previous) line of the help containing a text, wrapping around. -1 if there is none
    private static int FindHelpLine(List<Shortcut> lines, string search, int from, bool forward)
    {
        var count = lines.Count;
        for (var i = 1; i <= count; i++)
        {
            var index = ((from + (forward ? i : -i)) % count + count) % count;
            if (lines[index].Text.Contains(search, StringComparison.OrdinalIgnoreCase)) return index;
        }

        return -1;
    }


    #region Cursor

    public static void ShowCursor()
    {
        try
        {
            Console.CursorVisible = true;
        }
        catch
        {
            // Do nothing
        }
    }

    public static void HideCursor()
    {
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // Do nothing
        }
    }

    private static int _rowPosition;
    private static int _columnPosition;

    private static void InitializeCursor()
    {
        _rowPosition = Console.CursorTop;
        _columnPosition = Console.CursorLeft;
    }

    public static void CursorTopLeft()
    {
        _rowPosition = 0;
        _columnPosition = 0;
        Refresh();
    }

    public static void CursorTopLeftDown(int linesDown)
    {
        CursorTopLeft();
        CursorDown(linesDown);
    }

    public static void SetCursorEx(int top, int left)
    {
        _rowPosition = top;
        _columnPosition = left;
        Refresh();
    }

    private static void Refresh()
    {
        try
        {
            // On Linux and macOS a place outside the window is not refused (the terminal would scroll): it is told
            // as on Windows
            if (!OperatingSystem.IsWindows() && IsOutsideWindow(_columnPosition, _rowPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(_rowPosition));
            }

            Console.SetCursorPosition(_columnPosition, _rowPosition);
        }
        catch
        {
            // The position is outside the terminal window: it has been made smaller than the dimensions in use.
            // What is being drawn cannot be completed, so the page must be drawn again
            Init.DimensionsExceptionCaught = true;
            _redrawRequested = true;
            WindowTooSmall();
        }
    }

    // True if a place is outside the terminal window (false if its size is not known)
    private static bool IsOutsideWindow(int column, int row)
    {
        try
        {
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;
            return width > 0 && height > 0 && (column >= width || row >= height);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// True if the terminal window is smaller than the dimensions in use (false if its size is not known)
    /// </summary>
    internal static bool IsWindowTooSmall()
    {
        try
        {
            return Console.WindowWidth - 1 < Init.LineWidth || Console.WindowHeight - 1 < Init.LineCount;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The terminal window has been made smaller than the dimensions in use: they follow the window (automatic
    /// size), or the user is asked to make the window larger or to let the dimensions follow it. The screen is
    /// cleared: the page must be drawn again
    /// </summary>
    internal static void WindowTooSmall()
    {
        if (Init.AutoSize && Init.ApplyAutoSize())
        {
            // The dimensions follow the window: nothing to ask about
            ClearScreen();
            return;
        }

        ClearScreen();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(" " + DialogText.AttentionColon);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.WriteLine(" " + DialogText.ErrorClientSize);
        if (Init.AutoSize)
        {
            // The window is smaller than the smallest dimensions that can be used
            Console.WriteLine("(" + Lang.F("Dimensions must be at least {0}", Init.MinLineWidth + "x" + Init.MinLineCount) + ")");
            Console.WriteLine(DialogText.EnlargeWindow);
            KeyOrLargerWindow();
            Init.ApplyAutoSize();
        }
        else
        {
            Console.WriteLine("(" + Lang.F("Dimensions must be at least {0}", Init.LineWidth + "x" + Init.LineCount) + ")");
            Console.WriteLine(DialogText.FitOutputDimensions);
            if (KeyOrLargerWindow()?.Key == ConsoleKey.Y)
            {
                // Use the size of the window from now on (not smaller than the minimum)
                Init.AutoSize = true;
                if (!Init.ApplyAutoSize()) Init.SetDefaultOutputDimensions();
                FileIo.SaveSettings();
            }
        }

        ClearScreen();
    }

    // A key, or null as soon as the window is large enough for the dimensions in use (it was made larger while
    // the question was shown)
    private static ConsoleKeyInfo? KeyOrLargerWindow()
    {
        while (true)
        {
            try
            {
                if (Console.KeyAvailable) break;
            }
            catch
            {
                // Keys cannot be asked about (input from a file): wait for one
                break;
            }

            if (!IsWindowTooSmall()) return null;
            Thread.Sleep(50);
        }

        return Console.ReadKey(true);
    }

    private static bool _redrawRequested;

    // Ctrl+C was pressed (the signal comes on another thread: Init)
    private static int _cancelKey;

    /// <summary>
    /// Ctrl+C was pressed: it is cancelled, and the editor tells how to close it (TakeCancelKey)
    /// </summary>
    internal static void NoteCancelKey() => Interlocked.Exchange(ref _cancelKey, 1);

    /// <summary>
    /// True (once) if Ctrl+C was pressed since this was asked last
    /// </summary>
    internal static bool TakeCancelKey() => Interlocked.Exchange(ref _cancelKey, 0) == 1;

    // Blyant went on after it was stopped with Ctrl+Z (Linux, macOS: the signal comes on another thread: Init)
    private static int _continued;

    /// <summary>
    /// Blyant goes on after Ctrl+Z and "fg": the screen shows what the shell wrote, so the page is drawn again
    /// </summary>
    internal static void NoteContinued() => Interlocked.Exchange(ref _continued, 1);

    /// <summary>
    /// True (once) if Blyant went on after Ctrl+Z since this was asked last
    /// </summary>
    internal static bool TakeContinued() => Interlocked.Exchange(ref _continued, 0) == 1;

    /// <summary>
    /// True (once) if a page could not be drawn because the terminal window was made smaller, and must be
    /// drawn again with the dimensions that are now in use
    /// </summary>
    internal static bool TakeRedrawRequest()
    {
        var requested = _redrawRequested;
        _redrawRequested = false;
        return requested;
    }


    public static void CursorReturn()
    {
        InitializeCursor();

        if (_columnPosition > 0)
        {
            _columnPosition = 0;
            Refresh();
        }
    }


    public static void CursorLeft()
    {
        InitializeCursor();

        if (_columnPosition > 0)
        {
            _columnPosition--;
            Refresh();
        }
        else
        {
            CursorUp(true);
        }
    }


    public static void CursorLeft(int count)
    {
        if (count > 0)
        {
            InitializeCursor();

            for (int i = 0; i < count; i++)
            {
                if (_columnPosition > 0)
                {
                    _columnPosition--;
                    Refresh();
                }
                else
                {
                    CursorUp(true);
                    return;
                }
            }
        }
    }

    public static void CursorRight()
    {
        InitializeCursor();

        if (_columnPosition < Init.LineWidth - 1)
        {
            _columnPosition++;
            Refresh();
        }
        else
        {
            CursorDown(true);
        }
    }

    public static void CursorRight(int count)
    {
        if (count > 0)
        {
            InitializeCursor();

            for (int i = 0; i < count; i++)
            {
                if (_columnPosition < Init.LineWidth - 1)
                {
                    _columnPosition++;
                    Refresh();
                }
                else
                {
                    CursorDown(true);
                    return;
                }
            }
        }
    }

    public static void CursorUp(int rows)
    {
        InitializeCursor();

        if (_rowPosition > rows)
        {
            _rowPosition -= rows;
            Refresh();
        }
    }

    public static void CursorUp(bool returnCursor)
    {
        InitializeCursor();

        if (_rowPosition > 0)
        {
            _rowPosition--;
            Refresh();

            if (returnCursor)
                CursorReturn();
            else if (_columnPosition > 0)
            {
                CursorLeft();
            }
        }
    }

    public static void CursorDown()
    {
        InitializeCursor();

        if (_rowPosition < Init.LineCount - 1)
        {
            _rowPosition++;
            Refresh();
        }
    }

    public static void CursorDown(int rows)
    {
        InitializeCursor();

        if (_rowPosition < Init.LineCount - 1)
        {
            _rowPosition += rows;
            Refresh();
        }
    }

    public static void CursorDown(bool returnCursor)
    {
        InitializeCursor();

        if (_rowPosition < Init.LineCount - 1)
        {
            _rowPosition++;
            Refresh();

            if (returnCursor)
                CursorReturn();
            else if (_columnPosition > 0)
            {
                CursorLeft();
            }
        }
    }

    #endregion

    #region Cursor memory

    private static List<CursorMemory> _cursorMemory = new();

    private static void InitializeCursorMemory()
    {
        if (_cursorMemory.Count != 0) return;
        _cursorMemory = new List<CursorMemory>();
        for (int i = 0; i < 10; i++)
        {
            _cursorMemory.Add(new CursorMemory(0, 0));
        }
    }

    /// <summary>
    /// This method has 10 memory banks for storing cursor positions
    /// </summary>
    /// <param name="memoryBank"></param>
    public static void SaveCursorPosition(int memoryBank)
    {
        InitializeCursorMemory();
        if (memoryBank >= 0 && memoryBank <= 9)
        {
            _cursorMemory[memoryBank].Left = Console.CursorLeft;
            _cursorMemory[memoryBank].Top = Console.CursorTop;
        }
    }

    /// <summary>
    /// This method has 10 memory banks for storing cursor positions
    /// </summary>
    /// <param name="memoryBank"></param>
    public static void LoadCursorPosition(int memoryBank)
    {
        InitializeCursorMemory();
        if (memoryBank >= 0 && memoryBank <= 9)
        {
            SetCursorEx(_cursorMemory[memoryBank].Top, _cursorMemory[memoryBank].Left);
        }
    }

    #endregion


    #region Progress bar

    private const decimal ProgressbarWidth = 40;
    private const char ProgressBarChar = '#';

    /// <summary>
    /// Will return "[####################....................] 50%"
    /// </summary>
    /// <param name="currentValue"></param>
    /// <param name="maximumValue"></param>
    /// <returns></returns>
    internal static string GetProgressBar(decimal currentValue, decimal maximumValue)
    {
        try
        {
            // Calculate progress bar and insert the value into the progressBarText
            var progressBarProgress = new string(ProgressBarChar,
                Convert.ToInt32(currentValue / (maximumValue / ProgressbarWidth)));
            // Add the calculated content to the text output
            var progressBarText = "[" + progressBarProgress +
                                  new string('.', (int) ProgressbarWidth - progressBarProgress.Length) + "] " +
                                  ((currentValue / maximumValue) * 100).ToString("0") + "%";
            // Write the progress bar
            return progressBarText;
        }
        catch
        {
            return DialogText.NotAvailable;
        }
    }

    #endregion

}

internal class CursorMemory
{
    public int Left { get; set; }
    public int Top { get; set; }

    public CursorMemory(int left, int top)
    {
        Left = left;
        Top = top;
    }
}
