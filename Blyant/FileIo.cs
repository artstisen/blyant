using System.Text;

namespace Blyant;

/// <summary>
/// The files of Blyant itself: the data directory (with the settings and the lists of the file browser, the Jump
/// menu and the editor), writing a file safely, and the settings file
/// </summary>
internal static class FileIo
{
    private const string DataDirectoryName = "data"; // The name of the data directory -DO NOT CHANGE

    /// <summary>
    /// The data directory: "data" in the directory Blyant is started in, if there is one there (as Marbles has
    /// always done), else "data" next to the program, so the same data is used wherever Blyant is started from (a
    /// terminal in another directory, a file opened with Blyant). If the program directory cannot be written to (a
    /// program installed for all users), "Blyant/data" in the user's application data is used.
    /// It is chosen once (for each directory Blyant runs in): a folder "data" made later in the directory Blyant
    /// was started in (with the file browser) does not take the place of the one in use
    /// </summary>
    private static string DataDirectory
    {
        get
        {
            if (DataDirectoryOverride != null) return DataDirectoryOverride;
            var current = Environment.CurrentDirectory;
            if (_chosenIn != current)
            {
                // Only Blyant's own: a folder "data" of something else (a web site, a program) in the directory
                // Blyant is started in is not filled with its settings and lists
                _localData = File.Exists(Path.Combine(DataDirectoryName, SettingsFileName));
                _chosenIn = current;
            }

            return _localData ? DataDirectoryName : ProgramDataDirectory;
        }
    }

    private static string? _chosenIn; // The directory Blyant ran in when the data directory was chosen
    private static bool _localData; // The data directory is "data" in that directory

    /// <summary>
    /// The data directory, for tests (so nothing is written to the data directory of the program)
    /// </summary>
    internal static string? DataDirectoryOverride { get; set; }

    private static string? _programDataDirectory;

    private static string ProgramDataDirectory => _programDataDirectory ??= FindProgramDataDirectory();

    private static string FindProgramDataDirectory()
    {
        var nextToProgram = Path.Combine(AppContext.BaseDirectory, DataDirectoryName);
        // (one that is there must be writable by this user: one made when Blyant was run as root, with sudo, is not
        // used by the user, whose settings, copies and trash could not be written there)
        var exists = Directory.Exists(nextToProgram);
        if (exists ? CanWriteIn(nextToProgram) : CanWriteIn(AppContext.BaseDirectory))
        {
            return nextToProgram;
        }

        var instead = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify), "Blyant", DataDirectoryName);
        // (told in the log: the settings and lists of the one next to the program are not used)
        if (exists)
        {
            LogFile("Data directory next to the program cannot be written: " + nextToProgram + ", using " + instead);
        }

        return instead;
    }

    // True if a file can be made in a directory (it is made and deleted again)
    private static bool CanWriteIn(string directory)
    {
        try
        {
            var test = Path.Combine(directory, ".blyant-" + Guid.NewGuid().ToString("N"));
            using (File.Create(test, 1, FileOptions.DeleteOnClose))
            {
            }

            LogFile("Test file (made and deleted at once): " + Path.GetFullPath(test));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The directory the data directory is in (where the directory "spell" is too)
    /// </summary>
    internal static string DataParentDirectory =>
        Path.GetDirectoryName(Path.GetFullPath(DataDirectory)) ?? AppContext.BaseDirectory;

    internal const string SettingsFileName = "settings.ini";

    internal static string GetDataDirectoryName()
    {
        return DataDirectory;
    }

    internal static string DirectoryCheckErrorMessage = string.Empty;

    internal static bool CheckDataDirectory()
    {
        try
        {
            DirectoryCheckErrorMessage = string.Empty;
            if (!Directory.Exists(DataDirectory))
            {
                // Create data folder
                Init.DataDirectoryCreated = true;
                CreateDirectory(DataDirectory);
                // Exit as data is the only directory
            }

            return true;
        }
        catch (Exception e)
        {
            DirectoryCheckErrorMessage = e.Message;
            return false;
        }
    }

    #region Logged file operations

    // Every change Blyant makes by itself to files and directories goes through these methods (or through
    // WriteAllBytesSafe), so the log tells them all: what was made, written, added to, renamed, copied or deleted,
    // and why it failed. The operations of the file browser (FileOperations) are logged there, as a whole
    // (AGENTS.md). Only paths are logged, never what is written

    private static void LogFile(string text)
    {
        EventLog.Add(DialogText.LogFile + text);
    }

    // Do an operation, and log it (or why it failed, before the error goes on to the caller)
    private static void Logged(string text, Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception e)
        {
            LogFile(text + " - FAILED: " + e.Message);
            throw;
        }

        LogFile(text);
    }

    /// <summary>
    /// Make a directory, and the directories it is in, if they are not there (each one made is logged)
    /// </summary>
    internal static void CreateDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full)) return;
        // The directories that are not there, the outermost first
        var missing = new List<string>();
        for (var directory = full; !string.IsNullOrEmpty(directory) && !Directory.Exists(directory);
             directory = Path.GetDirectoryName(directory))
        {
            missing.Insert(0, directory);
        }

        foreach (var directory in missing) Logged("New directory: " + directory, () => Directory.CreateDirectory(directory));
    }

    /// <summary>
    /// Make a new, empty file. It must not be there: a file that has come since it was looked for is not emptied
    /// </summary>
    internal static void CreateNewFile(string path)
    {
        Logged("New: " + Path.GetFullPath(path), () => new FileStream(path, FileMode.CreateNew).Dispose());
    }

    /// <summary>
    /// Delete a file of Blyant's own (if it is there)
    /// </summary>
    internal static void DeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        Logged("Delete: " + Path.GetFullPath(path), () => File.Delete(path));
    }

    /// <summary>
    /// Give a file another name or place
    /// </summary>
    internal static void MoveFile(string source, string target, bool overwrite)
    {
        Logged("Rename: " + Path.GetFullPath(source) + " -> " + Path.GetFullPath(target),
            () => File.Move(source, target, overwrite));
    }

    /// <summary>
    /// Copy a file
    /// </summary>
    internal static void CopyFile(string source, string target, bool overwrite)
    {
        Logged("Copy: " + Path.GetFullPath(source) + " -> " + Path.GetFullPath(target),
            () => File.Copy(source, target, overwrite));
    }

    /// <summary>
    /// Add text to the end of a file (it is made if it is not there): what is in it is not written again
    /// </summary>
    internal static void AppendText(string path, string text)
    {
        var verb = File.Exists(path) ? "Append: " : "New: ";
        Logged(verb + Path.GetFullPath(path), () => File.AppendAllText(path, text, new UTF8Encoding(false)));
    }

    #endregion

    #region Write file

    private const string TempFileNameExtension = ".tmp";

    /// <summary>
    /// Write the content to a temporary file and then replace the target file with it,
    /// so an interrupted save can never leave a half written file behind.
    /// The file keeps the time it was created (where the system has one that can be set), its permissions (Linux,
    /// macOS) or attributes (Windows). A link is followed: the file it points to is written, and the link stays.
    /// </summary>
    internal static void WriteAllBytesSafe(string path, byte[] content)
    {
        path = LinkedFile(path);
        Logged((File.Exists(path) ? "Write: " : "New: ") + Path.GetFullPath(path), () => WriteSafe(path, content));
    }

    // True if this user may write the file (Linux, macOS): it is opened for writing and closed at once, unchanged
    private static bool MayWrite(string path)
    {
        try
        {
            using (File.OpenHandle(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            // (another reason, as the file being gone: the save tells it)
            return true;
        }
    }

    private static void WriteSafe(string path, byte[] content)
    {
        // A name of its own (in the same directory, so the file is replaced in one step): never a file of the user
        var tempPath = path + "." + Guid.NewGuid().ToString("N")[..8] + TempFileNameExtension;
        // The file that replaces the target is a new file: remember when the target was created, and its permissions
        DateTime? created = null;
        UnixFileMode? mode = null;
        FileAttributes? attributes = null;
        try
        {
            if (File.Exists(path))
            {
                created = File.GetCreationTimeUtc(path);
                if (OperatingSystem.IsWindows())
                {
                    // Read-only stays on the target, so the replacing fails as it should
                    attributes = File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System);
                }
                else
                {
                    mode = File.GetUnixFileMode(path);
                }
            }
        }
        catch
        {
            // Saving matters more than the time and permissions
        }

        // A file that this user may not write (chmod -w, or a file of another user) is not replaced: the replacing
        // would only need the directory to be writable, so it is refused here, as Windows refuses a read-only file.
        // The system is asked (opening it for writing changes nothing in it), so groups and other users count too
        if (mode != null && !MayWrite(path))
        {
            throw new UnauthorizedAccessException(Lang.F("{0} is read only: it is not written", Path.GetFileName(path)));
        }

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None
            };
            if (mode is { } createMode && !OperatingSystem.IsWindows()) options.UnixCreateMode = createMode;
            using (var stream = new FileStream(tempPath, options))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true); // Make sure the content is on the disk before replacing the target file
            }

            // (The umask can have taken permissions away when it was created.) Only where it can be: a memory stick
            // with FAT or exFAT, and some network folders, refuse it, and the file is saved all the same
            if (mode is { } unixMode && !OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(tempPath, unixMode);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // The permissions the file system gives
                }
            }

            File.Move(tempPath, path, true);
        }
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Nothing more to do. The original error is the one to report
            }

            throw;
        }

        // The file is saved: the attributes and the time are kept where they can be
        try
        {
            if (attributes is { } kept && kept != 0) File.SetAttributes(path, File.GetAttributes(path) | kept);
        }
        catch
        {
            // Saving matters more than the attributes
        }

        RestoreCreationTime(path, created);
    }

    // The file a link points to (through all links), or the path itself when it is not a link (or the link is broken)
    private static string LinkedFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.LinkTarget == null) return path;
            var target = info.ResolveLinkTarget(true);
            return target is { Exists: true } ? target.FullName : path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return path;
        }
    }

    // Give a file that has replaced another the time the other was created. Only where the time can be set:
    // on Linux there is none to set (and setting it would change the time the file was last written)
    private static void RestoreCreationTime(string path, DateTime? created)
    {
        if (created == null || !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())) return;
        try
        {
            File.SetCreationTimeUtc(path, created.Value);
        }
        catch
        {
            // The file is saved: the time is not worth failing for
        }
    }

    #endregion

    #region Settings

    private static string GetSettingsFolderAndFileName()
    {
        return
            Path.Combine(DataDirectory,
                SettingsFileName); // Will insert the correct path separator char according to OS/platform (like using the Path.DirectorySeparatorChar)
    }


    private static string _tempSettings = string.Empty;

    internal static void SaveSettings()
    {
        // Collect settings data and save it
        // Section start
        _tempSettings = "; Last modified " + IsoDateTime.GetDateTimeNow() + '\n';
        _tempSettings += "[SETTINGS]" + '\n';
        // Comment
        _tempSettings += '\n';
        _tempSettings += "; DIMENSIONS" + '\n';
        // Output line width (columns)
        _tempSettings += "linewidth=" + Init.LineWidth + '\n';
        // Output line count (rows)
        _tempSettings += "linecount=" + Init.LineCount + '\n';
        // Follow the size of the terminal window
        _tempSettings += "autosize=" + (Init.AutoSize ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Comment
        _tempSettings += '\n';
        _tempSettings += "; OPTIONAL" + '\n';
        // What to show at startup: select (the start page), editor (the file browser) or blank (a blank page)
        _tempSettings += "startmode=" + StartMenu.SettingName(StartMenu.Mode) + '\n';
        // The kind of new files and blank pages: the extension a name without one gets (md, txt, html ...)
        _tempSettings += "filetype=" + FileOperations.DefaultExtension.TrimStart('.') + '\n';
        // The line endings of new files and blank pages: system, lf or crlf
        _tempSettings += "newlineendings=" + TextFile.NewFileLineEndings + '\n';
        // Starting in the file browser: open the favorites list first
        _tempSettings += "startfavorites=" + (StartMenu.StartInFavorites ? DialogText.BTrue : DialogText.BFalse) +
                         '\n';
        // Deleting in the file browser moves to the trash ("trash" in the data directory); false: deleted for good
        _tempSettings += "deletetotrash=" + (FileOperations.DeleteToTrash ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Below the list of favorites: the panes with the note and the peek of the selected item, and the peek in them
        _tempSettings += "favoritepanes=" + (FileExplorer.FavoritePanes ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "favoritepeek=" + (FileExplorer.FavoritePeek ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // The rows of the favorites numbered (and below, of the bookmarks)
        _tempSettings += "favoritenumbers=" + (FileExplorer.FavoriteNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // The same below the bookmarks of the Jump menu
        _tempSettings += "bookmarkpanes=" + (JumpMenu.BookmarkPanes ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "bookmarkpeek=" + (JumpMenu.BookmarkPeek ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "bookmarknumbers=" + (JumpMenu.BookmarkNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // The rows of the panes of the file browser, the history, the hotlist and the search results numbered
        _tempSettings += "filenumbers=" + (FileExplorer.FileNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "historynumbers=" + (FileExplorer.HistoryNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "hotlistnumbers=" + (FileExplorer.HotlistNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "searchnumbers=" + (FileExplorer.SearchNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // The commands of the place in a box above the command line while a command is typed
        _tempSettings += "commandlist=" + (CommandList.Enabled ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // The hints on the start page, and the one shown (the next each time Blyant starts, while they are shown)
        _tempSettings += "hints=" + (Hints.Enabled ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "hintindex=" + Hints.Index + '\n';
        // The clipboard: yanks to the system clipboard (OSC 52) and/or the paste bin, every yank ("all") or only
        // with "+ in front ("plus"), and the paste bin file
        _tempSettings += "osc52=" + (SystemClipboard.Osc52 ? DialogText.BTrue : DialogText.BFalse) + '\n';
        _tempSettings += "pastebin=" + (SystemClipboard.PasteBin ? DialogText.BTrue : DialogText.BFalse) +
                         '\n';
        _tempSettings += "clipboardyank=" + (SystemClipboard.EveryYank ? "all" : "plus") + '\n';
        _tempSettings += "pastebinappend=" + (SystemClipboard.PasteBinAppend ? DialogText.BTrue : DialogText.BFalse) +
                         '\n';
        _tempSettings += "pastebinfile=" + SystemClipboard.PasteBinFile + '\n';
        // The order of the snippets in the editor (file, title or used)
        _tempSettings += "snipsort=" + Snippets.SortName(Snippets.Sort) + '\n';
        // Comment
        _tempSettings += '\n';
        _tempSettings += "; STYLING" + '\n';
        // ASCII style
        _tempSettings += "asciistyle=" + AsciiStyles.GetAsciiStyle() + '\n';
        // Logo animation on the start page
        _tempSettings += "logoanim=" + Logo.SettingName(Logo.Animation) + '\n';
        // The logo: the built-in one (Blyant), or the name of one in the directory "logos"
        _tempSettings += "logo=" + Logo.Current.Name + '\n';
        // The color scheme: a built-in one, or the name of one in the directory "colorschemes"
        _tempSettings += "colorscheme=" + ConsoleColors.Current.Name + '\n';
        // Black and white: the colors of the scheme are kept, but not used
        _tempSettings += "monochrome=" + (ConsoleColors.Monochrome ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Comment
        _tempSettings += '\n';
        _tempSettings += "; FILE BROWSER AND EDITOR" + '\n';
        // The directories last shown in the two panes of the file browser, the names selected in them, and the pane
        // that had the focus (1 or 2)
        _tempSettings += "browsedir1=" + FileExplorer.StartDirectory + '\n';
        _tempSettings += "browsedir2=" + FileExplorer.StartDirectory2 + '\n';
        _tempSettings += "browsesel1=" + FileExplorer.StartSelected + '\n';
        _tempSettings += "browsesel2=" + FileExplorer.StartSelected2 + '\n';
        _tempSettings += "browsepane=" + (FileExplorer.StartPane + 1) + '\n';
        // Where the folder pickers start: the folder picked last (or, ending with a separator, the directory where
        // the picking was canceled)
        _tempSettings += "pickdir=" + FileExplorer.PickPlace + '\n';
        // Two panes side by side in the file browser (false: one pane)
        _tempSettings += "browsedual=" + (FileExplorer.DualPane ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Recovery copies of text that is not saved, in the directory "recovery" (false: none are written)
        _tempSettings += "recovery=" + (Recovery.Enabled ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Complete words while typing in the editor (the lists are in completions.txt)
        _tempSettings += "autocomplete=" + (Completions.Enabled ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Color Markdown, HTML, CSS, JavaScript and JSON files in the editor (the editor command :syntax turns
        // the colors on and off)
        // The language of the texts (en, da, de, nb, sv, es, fr)
        // The language set, also when its file is missing (English is used until it is there again)
        _tempSettings += "language=" + (Lang.Missing ?? Lang.Current) + '\n';
        _tempSettings += "syntaxcolors=" + (SyntaxHighlighter.ColorFiles ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Line numbers in the editor (the editor command :number turns them on and off)
        _tempSettings += "linenumbers=" + (TextArea.LineNumbers ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Long lines in the editor wrapped (on unless turned off)
        _tempSettings += "wrap=" + (TextArea.WordWrap ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Blank space shown in the editor as . (space), > (tab) and $ (line end) (:whitespace)
        _tempSettings += "whitespace=" + (TextArea.ShowWhitespace ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // ' jumps to the place of a mark, as ` (for keyboards where ` is hard to type)
        _tempSettings += "marksapostrophe=" + (EditorEngine.MarksApostrophe ? DialogText.BTrue : DialogText.BFalse) + '\n';
        // Lines kept above and below the cursor when the editor scrolls (on or off, and how many)
        _tempSettings += "scrolloff=" + (TextArea.ScrollOff ? DialogText.BTrue : DialogText.BFalse) +
                         '\n';
        _tempSettings += "scrolllines=" + TextArea.ScrollOffLines + '\n';
        // Comment
        _tempSettings += '\n';
        _tempSettings += "; SPELL CHECKING" + '\n';
        // The languages used by the editor command :spell (spell files in the directory "spell"):
        _tempSettings += "spelllang=" + SpellChecker.Languages + '\n';

        // Write to file
        var status = WriteSettingsFile(_tempSettings);
        if (status != DialogText.Success)
        {
            Menu.StatusMessage(status, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
        }
    }

    /// <summary>
    /// The extension of the copy kept of a settings file that could not be read ("settings.ini.bad")
    /// </summary>
    internal const string UnreadSettingsExtension = ".bad";

    // True when the settings file could not be read at startup: before it is written for the first time, a copy of
    // it is kept (settings.ini.bad), so the settings in it are not lost
    private static bool _keepUnreadSettings;

    /// <summary>
    /// True when the settings file was there but could not be read: it is kept (as a copy) when it is written
    /// </summary>
    internal static bool SettingsUnread => _keepUnreadSettings;

    internal static string WriteSettingsFile(string content)
    {
        try
        {
            if (CheckDataDirectory())
            {
                var path = GetSettingsFolderAndFileName();
                if (_keepUnreadSettings)
                {
                    // Not written at all when the copy cannot be made
                    if (File.Exists(path)) CopyFile(path, path + UnreadSettingsExtension, true);
                    _keepUnreadSettings = false;
                }

                WriteAllBytesSafe(path, new UTF8Encoding(false).GetBytes(content));
                return DialogText.Success;
            }
            else
            {
                return DialogText.StatusUnableToSave;
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }


    /// <summary>
    /// Load the settings file. The settings are read by name, so they can be in any order.
    /// Missing or invalid settings get their default value and unknown settings (as those
    /// of earlier versions) are ignored.
    /// </summary>
    /// <returns>False if the settings file could not be read</returns>
    internal static bool LoadSettings()
    {
        _keepUnreadSettings = false;
        if (CheckDataDirectory())
        {
            try
            {
                var content = ReadSettingsFile();
                if (content == DialogText.ErrorReadingFile)
                {
                    _keepUnreadSettings = true;
                    return false;
                }

                var settings = ParseSettings(content == DialogText.FileNotFound ? string.Empty : content);
                if (settings.Count > 0)
                {
                    ApplySettings(settings);
                    return true;
                }
            }
            catch (Exception e)
            {
                // Why it could not be read is in the log
                EventLog.Add(DialogText.LogFile + "Settings not read: " + e.Message);
                _keepUnreadSettings = true;
                return false;
            }
        }

        Init.UsingDefaultSettings = true;
        return true; // Must be true because a new settings file will be created if none exist
    }

    /// <summary>
    /// Get the "name=value" pairs of a settings file. Sections, comments and blank lines are skipped.
    /// </summary>
    internal static Dictionary<string, string> ParseSettings(string content)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in Tools.SplitStringOnNewLine(content, false))
        {
            var line = Tools.RemoveNewLineChars(rawLine).Trim();
            if (line.Length == 0 || line[0] is '[' or ';' or '#') continue;
            // Split on the first "=" only, as values (paths, logo text) may contain the character
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            settings[line[..separator].Trim()] = line[(separator + 1)..];
        }

        return settings;
    }

    private static void ApplySettings(Dictionary<string, string> settings)
    {
        string Get(string name) => settings.TryGetValue(name, out var value) ? value : string.Empty;
        int? GetNumber(string name)
        {
            var value = Get(name).Trim();
            return value.Length is > 0 and < 9 && Tools.IsNumeric(value) ? Convert.ToInt32(value) : null;
        }

        // Dimensions
        Init.SetLineWidth((byte)Math.Clamp(GetNumber("linewidth") ?? Init.MinLineWidth, 0, byte.MaxValue));
        Init.SetLineCount((byte)Math.Clamp(GetNumber("linecount") ?? Init.MinLineCount, 0, byte.MaxValue));

        // Automatic dimensions (on unless turned off)
        Init.AutoSize = Tools.TrimToLower(Get("autosize")) != DialogText.BFalse;
        StartMenu.Mode = StartMenu.ParseSetting(Get("startmode"));
        // The kind of new files and blank pages (Markdown unless another of the list is set)
        FileOperations.DefaultExtension = FileOperations.ParseFileType(Get("filetype")) ?? FileOperations.MarkdownExtension;
        var lineEndings = Tools.TrimToLower(Get("newlineendings"));
        TextFile.NewFileLineEndings = lineEndings is "lf" or "crlf" ? lineEndings : "system";
        StartMenu.StartInFavorites = Tools.TrimToLower(Get("startfavorites")) == DialogText.BTrue;
        // Deleting to the trash: on unless turned off
        FileOperations.DeleteToTrash = Tools.TrimToLower(Get("deletetotrash")) != DialogText.BFalse;
        // The note and the peek below the favorites: on unless turned off
        FileExplorer.FavoritePanes = Tools.TrimToLower(Get("favoritepanes")) != DialogText.BFalse;
        FileExplorer.FavoritePeek = Tools.TrimToLower(Get("favoritepeek")) != DialogText.BFalse;
        JumpMenu.BookmarkPanes = Tools.TrimToLower(Get("bookmarkpanes")) != DialogText.BFalse;
        JumpMenu.BookmarkPeek = Tools.TrimToLower(Get("bookmarkpeek")) != DialogText.BFalse;
        // The numbers of the rows of the favorites and the bookmarks: off unless turned on
        FileExplorer.FavoriteNumbers = Tools.TrimToLower(Get("favoritenumbers")) == DialogText.BTrue;
        JumpMenu.BookmarkNumbers = Tools.TrimToLower(Get("bookmarknumbers")) == DialogText.BTrue;
        FileExplorer.FileNumbers = Tools.TrimToLower(Get("filenumbers")) == DialogText.BTrue;
        FileExplorer.HistoryNumbers = Tools.TrimToLower(Get("historynumbers")) == DialogText.BTrue;
        FileExplorer.HotlistNumbers = Tools.TrimToLower(Get("hotlistnumbers")) == DialogText.BTrue;
        FileExplorer.SearchNumbers = Tools.TrimToLower(Get("searchnumbers")) == DialogText.BTrue;
        CommandList.Enabled = Tools.TrimToLower(Get("commandlist")) != DialogText.BFalse;
        Hints.Enabled = Tools.TrimToLower(Get("hints")) != DialogText.BFalse;
        Hints.Index = int.TryParse(Get("hintindex").Trim(), out var hint) && hint >= -1 ? hint : -1;
        // The clipboard (OSC 52 on unless turned off, the rest off unless turned on)
        SystemClipboard.Osc52 = Tools.TrimToLower(Get("osc52")) != DialogText.BFalse;
        SystemClipboard.PasteBin = Tools.TrimToLower(Get("pastebin")) == DialogText.BTrue;
        // Every yank unless only the ones with "+ in front are asked for
        SystemClipboard.EveryYank = Tools.TrimToLower(Get("clipboardyank")) != "plus";
        SystemClipboard.PasteBinAppend = Tools.TrimToLower(Get("pastebinappend")) == DialogText.BTrue;
        var pasteBinFile = Get("pastebinfile").Trim();
        // (one of Blyant's own files, written in the settings file by hand, is not used)
        SystemClipboard.PasteBinFile = pasteBinFile.Length > 0 && SystemClipboard.PasteBinProblem(pasteBinFile) == null
            ? pasteBinFile
            : SystemClipboard.DefaultPasteBinFile;
        Snippets.Sort = Snippets.ParseSort(Get("snipsort"));
        var language = Tools.TrimToLower(Get("language"));
        Lang.Missing = null;
        if (!Lang.Set(language) && language.Length > 0) Lang.Missing = language;
        // On unless turned off
        // (named "mdcolors" in an earlier version)
        SyntaxHighlighter.ColorFiles = Tools.TrimToLower(Get("syntaxcolors")) != DialogText.BFalse &&
                                       Tools.TrimToLower(Get("mdcolors")) != DialogText.BFalse;
        // On unless turned off
        TextArea.LineNumbers = Tools.TrimToLower(Get("linenumbers")) == DialogText.BTrue;
        TextArea.WordWrap = Tools.TrimToLower(Get("wrap")) != DialogText.BFalse;
        TextArea.ShowWhitespace = Tools.TrimToLower(Get("whitespace")) == DialogText.BTrue;
        EditorEngine.MarksApostrophe = Tools.TrimToLower(Get("marksapostrophe")) == DialogText.BTrue;
        // On unless turned off
        TextArea.ScrollOff = Tools.TrimToLower(Get("scrolloff")) != DialogText.BFalse;
        var scrollLines = GetNumber("scrolllines");
        TextArea.ScrollOffLines = scrollLines is > 0 and <= 999 ? scrollLines.Value : TextArea.DefaultScrollOffLines;
        ConsoleColors.Monochrome = Tools.TrimToLower(Get("monochrome")) == DialogText.BTrue;

        // ASCII style
        var asciiStyle = GetNumber("asciistyle");
        if (asciiStyle.HasValue)
        {
            AsciiStyles.SetAsciiStyle(asciiStyle.Value);
        }
        else
        {
            AsciiStyles.ResetAsciiStyle();
        }

        // Logo animation (on unless turned off)
        Logo.Animation = Logo.ParseAnimation(Get("logoanim"));
        // The logo by its name (the built-in one if there is none with the name)
        Logo.Current = Logos.Load(Get("logo")) ?? LogoArt.BuiltIn;

        // The color scheme by its name (the default if there is none with the name)
        ConsoleColors.Use(ColorSchemes.Load(Get("colorscheme")) ?? ColorScheme.Default);

        // File browser directory
        // (a settings file from an earlier version has one directory: "browsedir")
        var browseDirectory = Get("browsedir1").Length > 0 ? Get("browsedir1") : Get("browsedir");
        FileExplorer.StartDirectory = Tools.IsValidPath(browseDirectory) ? browseDirectory : string.Empty;
        FileExplorer.StartDirectory2 = Tools.IsValidPath(Get("browsedir2")) ? Get("browsedir2") : string.Empty;
        // The names selected in the panes (one that is gone selects the first row)
        FileExplorer.StartSelected = Get("browsesel1").Trim();
        FileExplorer.StartSelected2 = Get("browsesel2").Trim();
        FileExplorer.StartPane = Get("browsepane").Trim() == "2" ? 1 : 0;
        // (a path that is not a full path on this system is not used: the pickers start where the browser was)
        var pickPlace = Get("pickdir").Trim();
        FileExplorer.PickPlace = Tools.IsValidPath(pickPlace) && Path.IsPathFullyQualified(pickPlace)
            ? pickPlace
            : string.Empty;
        // Two panes unless turned off
        FileExplorer.DualPane = Tools.TrimToLower(Get("browsedual")) != DialogText.BFalse;
        // Recovery copies: on unless turned off
        Recovery.Enabled = Tools.TrimToLower(Get("recovery")) != DialogText.BFalse;
        // Completing words while typing: on unless turned off (a settings file of before, with "false", keeps it off)
        Completions.Enabled = Tools.TrimToLower(Get("autocomplete")) != DialogText.BFalse;

        // Spell checking languages
        var spellLanguages = Get("spelllang").Replace(" ", string.Empty);
        SpellChecker.Languages = spellLanguages.Length > 0 && spellLanguages.Split(',').All(Tools.IsValidFileName)
            ? spellLanguages
            : SpellChecker.DefaultLanguages;

    }

    internal static string ReadSettingsFile()
    {
        _tempSettings = "";
        if (File.Exists(GetSettingsFolderAndFileName()))
        {
            try
            {
                var lines = File.ReadAllLines(GetSettingsFolderAndFileName());
                _tempSettings = string.Join("\n", lines);
            }
            catch
            {
                _tempSettings = DialogText.ErrorReadingFile;
            }
        }
        else
        {
            _tempSettings = DialogText.FileNotFound;
        }

        return _tempSettings;
    }

    #endregion
}

#region File size

public static class FileSizeFormatter
{
    // Load all suffixes in an array  
    private static string[] Suffixes => new[] { " " + Lang.T("bytes"), " kB", " MB", " GB", " TB", " PB" };

    public static string FormatSize(long bytes)
    {
        try
        {
            var counter = 0;
            var number = (decimal) bytes;
            // 1023 bytes stay bytes ("1 kB" from 513 bytes up was rounded)
            var suffixes = Suffixes;
            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number = number / 1024;
                counter++;
            }

            // Shown with one decimal: 1023.97 kB would be "1024.0 kB"
            if (counter > 0 && Math.Round(number, 1) >= 1024 && counter < suffixes.Length - 1)
            {
                number = number / 1024;
                counter++;
            }

            return bytes < 1024 ? $"{number:n0}{suffixes[counter]}" : $"{number:n1}{suffixes[counter]}";
        }
        catch
        {
            return DialogText.NotAvailable;
        }
    }
}

#endregion