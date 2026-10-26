namespace Blyant;

internal static class DialogText
{
    // Application information
    public const string ApplicationName = "Blyant";
    public const string ApplicationCopyright = "Copyright (c) 2022-2026 Lars Brandt Stisen";
    public const string ApplicationBy = "by Lars Brandt Stisen (c) 2022-2026";
    public const string ApplicationVersion = "v0.1.0"; // For display. Right click the Blyant project icon to set assembly version

    //                                                                                \n <- Max line length
    //-----------------------------------------------------------------------------
    public static string About => Lang.T(
        "Blyant is a text editor for writing, in the style of Vim: files and\n" +
        "projects (folders of files) on your own computer, in Markdown or any other\n" +
        "kind of text. It is written in C# and uses UTF-8 console I/O encoding.\n\n" +
        "Your settings, favorites, bookmarks, snippets and macros are stored in the\n" +
        "\"data\" directory alongside the Blyant program files, and your projects in\n" +
        "the projects folder (Configuration). You can copy them to any backup,\n" +
        "portable or cloud medium.\n\n" +
        "This application is cross-platform software that can run on different\n" +
        "systems. Due to the difference between terminal programs and their\n" +
        "tradition of using specific keyboard shortcuts for built-in functions,\n" +
        "Blyant is operated with plain keys and commands in the style of Vim.\n" +
        "Press ? for help.");

    //                                                                                \n <- Max line length
    // --------------------------------------------------------------------------------
    public static string MultipleInstances => Lang.T("TIP: Run multiple instances of Blyant using folder duplicates with different\n" +
        "names. Change color settings to distinguish between instances. Running\n" +
        "multiple instances from the same folder can overwrite the lists and the\n" +
        "settings of each other, as they share the same data directory.\n");


    // Mode labels and Caption
    public static string LogReview => Lang.T("Log review");
    public static string CaptionsSettings => Lang.T("Settings");
    public static string CaptionsColorScheme => Lang.T("Color schemes");
    public static string SchemeEditorKeys => Lang.T("j/k Select | Enter Pick | T Text | E Reset | S Save | h/l Page | Esc Close");
    public static string SchemePreviewKeys => Lang.T("Tab Schemes | S Save | ? Help | Esc Close");
    public static string HelpSectionColorScheme => Lang.T("Color schemes");
    public static string HelpSectionLogos => Lang.T("Logos");
    public static string CaptionsClear => Lang.T("CLEAR");
    public static string CaptionsEdit => Lang.T("EDIT");
    // The mode of the editor, on the divider above the text (EDIT for Insert mode)
    public static string DividerNavigate => Lang.T("NAVIGATE");
    public static string DividerSelect => Lang.T("SELECT");
    public static string DividerCommand => Lang.T("COMMAND");
    public static string DividerJump => Lang.T("JUMP");
    public static string CaptionsEditor => Lang.T("Editor");
    public static string CaptionsStringMemory => Lang.T("String memory");
    public static string ColorDefault => Lang.T("[Default]");

    // The find-and-replace box of the editor
    public static string ReplaceCaption => Lang.T("Replace");
    public static string ReplaceFind => Lang.T("Find:");
    public static string ReplaceWith => Lang.T("Replace with:");
    public static string ReplaceMatchCase => Lang.T("Match case");
    public static string ReplaceRegex => Lang.T("Regular expression");
    public static string ReplaceWhere => Lang.T("Where:");
    public static string ReplaceWholeText => Lang.T("Whole text");
    public static string ReplaceCurrentLine => Lang.T("Current line");
    public static string ReplaceSelectedLines => Lang.T("Selected lines");
    public static string ReplaceRun => Lang.T("Replace all");
    public static string ReplaceKeys => Lang.T("Enter/Space Edit or change | j/k Select | Esc Cancel");
    public static string ColorCaption => Lang.T("Color");
    public static string TextColorCaption => Lang.T("Text color");
    public static string BackgroundCaption => Lang.T("Background");
    public static string NewFile => Lang.T("New file");
    public static string BookmarkFileOnly => Lang.T("Only a file is bookmarked from the editor");
    public static string FavoriteNoteNotAFile => Lang.T("This is a note, kept in its list (not a file)");
    public static string RenameFile => Lang.T("Rename file");
    public static string DeleteFile => Lang.T("Delete file");
    public static string FileInfo => Lang.T("Info");
    public static string Save => Lang.T("Save");
    public static string Restart => Lang.T("Restart");
    public static string Exit => Lang.T("Exit");
    public static string CaptionsKeyError => Lang.T("Keyboard");


    public static string InitNoData => Lang.T("No data");
    public static string NotAvailable => Lang.T("N/A");
    public static string SavIndicator => " " + Lang.T("Unsaved") + " ";
    public static string ReadOnlyLabel => " " + CaptionsReadOnly + " ";
    public static string EditorHelpHint => Lang.T(":help shows the keys of the editor");
    public static string CancelKeyHint => Lang.T("Ctrl+C does not close the editor: type :q and press Enter");

    public static string HitAnyKeyToContinue => Lang.T("[Press any key to continue]");
    public static string HitAnyKeyOrEscape => Lang.T("[Press a key to continue/Esc to exit]");

    // Yes/No dialogs
    private static string YesNo => Lang.T("[Y] Yes  [N] No");
    public static string SaveQuestion => Lang.T("Save changes?");
    public static string SaveChoiceYes => Lang.T("Y    Yes, save and close");
    public static string SaveChoiceNo => Lang.T("N    No, close without saving");
    public static string SaveChoiceContinue => Lang.T("E    Save and continue editing");
    public static string SaveChoiceCancel => Lang.T("Esc  Cancel (continue editing)");

    public static string SaveChangesYn => Lang.T("Save changes?");
    public static string ClearTextYn => Lang.T("Clear text?");
    public static string FitOutputDimensions => Lang.T("Follow the size of the terminal window (automatic size)?") + " " + YesNo;
    public static string EnlargeWindow => Lang.T("Make the terminal window larger and press a key");
    // The same questions in a box on top of the page (ChoiceBox), which shows the choices itself
    public static string ExitQuestion => Lang.T("Exit Blyant?");
    public static string RestartQuestion => Lang.T("Restart Blyant?");
    public static string ResetSettingsQuestion => Lang.T("Revert to default settings?");
    public static string SaveChangesQuestion => Lang.T("Save changes?");
    public static string ChoiceCancel => Lang.T("Cancel");
    // The boxes for picking a color scheme and a language
    public static string SchemeSaveCaption => Lang.T("Save the color scheme");
    public static string CaptionsLogos => Lang.T("Logos");
    public static string LogoSaveCaption => Lang.T("Save the logo");
    public static string LogoSavePrompt => Lang.T("Type a name for the logo and press Enter");
    public static string LogoNameInvalid => Lang.T("Not a name a logo can have");
    public static string LogoNameBuiltIn => Lang.T("A built-in logo has that name");
    public static string LogoNameTaken => Lang.T("Another logo has that name");
    public static string LogoRenameCaption => Lang.T("Rename the logo");
    public static string LogoDuplicateCaption => Lang.T("Duplicate the logo");
    public static string LogoBuiltInRename => Lang.T("A built-in logo cannot be renamed");
    public static string LogoDiscardQuestion => Lang.T("Use or edit another logo?");
    public static string LogoBuiltInDelete => Lang.T("A built-in logo cannot be deleted");
    public static string LogoListKeys => Lang.T("j/k Select | Enter Use | E Edit | Y Duplicate | R Rename | D Delete");
    public static string LogoEditorKeys => Lang.T("hjkl Move | i/R Draw | x Clear | r Char | u Undo | S Save | Esc Close");
    public static string LogoColorKeys => Lang.T("j/k Select | Enter Pick | S Save | h/l Page | ? Help | Esc Close");
    public static string LogoCellKeys => Lang.T("hjkl Move | c Color | Space Paint | x Clear | v Select | u Undo | Esc Close");
    public static string LogoCellVisualKeys => Lang.T("hjkl Move | Space Paint | x Clear | Esc NORMAL");
    public static string LogoReplaceKeys => Lang.T("Backspace Clear | Enter Next row | Esc NORMAL");
    public static string SchemeSavePrompt => Lang.T("Type a name for the color scheme and press Enter");
    public static string SchemeNameInvalid => Lang.T("Not a name a color scheme can have");
    public static string SchemeNameBuiltIn => Lang.T("A built-in color scheme has that name");
    public static string SchemeNameTaken => Lang.T("Another color scheme has that name");
    public static string SchemeRenameCaption => Lang.T("Rename the color scheme");
    public static string SchemeDuplicateCaption => Lang.T("Duplicate the color scheme");
    public static string SchemeBuiltInRename => Lang.T("The built-in color schemes cannot be renamed");
    public static string SchemeDiscardQuestion => Lang.T("Use another scheme?");
    public static string ChangesNotSaved => Lang.T("The changes are not saved.");
    public static string SchemeBuiltInDelete => Lang.T("The built-in color schemes cannot be deleted");
    public static string SchemeDeleteNote => Lang.T("Its file is deleted.");
    public static string SchemeListKeys => Lang.T("j/k Select | Enter Use | Y Duplicate | R Rename | D Delete | Tab Colors");
    public static string PickLanguageCaption => Lang.T("Language");
    public static string PickLanguageQuestion => Lang.T("Select the language");
    public static string ClearStringMemory => Lang.T("Clear string memory?");

    // Selection dialogs

    // Input/Output/Information
    public static string TitleColon => Lang.T("Title:");
    public static string NameColon => Lang.T("Name:");

    public static string AttentionColon => Lang.T("Attention: ");

    // Log
    public const string InitInitializing = "Initializing...";
    public const string InitCompleted = "Initialization complete.";
    public const string InitSetEncoding = "Set I/O encoding...";
    public const string InitSetDefaults = "Set default values...";
    public const string InitDirectoryCheck = "File directory check...";
    public const string InitOk = "[OK]";
    public const string InitFailed = "[FAILED]";
    public const string InitLoadingSettings = "Loading settings...";
    public const string InitLoadingSettingsError = "Unable to interpret " + FileIo.SettingsFileName;
    public const string InitDimensions = "Initializing output variables...";
    public const string InitUpdateDimensions = "Updating output variables...";
    public const string InitDirectoryCheckError = "Directory check failed: ";

    public const string DirectoryCreated = "Directory created";
    public const string DefaultSettings = "Default settings";


    public static string InvalidFileName => Lang.T("Invalid file name");
    // What is wrong with a new name of a file or directory (Tools.FileNameProblem)
    public static string NameExists => Lang.T("A file or directory with that name already exists");
    public static string ListNotRead => Lang.T("The list could not be read, so it was not changed");
    public static string NameControlCharacters => Lang.T("A name cannot contain control characters");
    public static string NameEnd => Lang.T("A name cannot end with a dot or a space");
    public static string NameTooLong => Lang.T("A name can be at most 255 characters long");
    public static string NameCharacter(char c) => Lang.F("A name cannot contain the character {0}", c.ToString());
    public static string NameReserved(string name) => Lang.F("{0} is a name kept by Windows and cannot be used", name);
    public static string FileDeleted => Lang.T("File deleted");


    // File browser
    public static string CaptionsFiles => Lang.T("Files");

    public static string StartTab => Lang.T("Start");
    public static string FileBrowser => Lang.T("File browser");
    public static string NewProject => Lang.T("New project");
    public static string OpenProject => Lang.T("Open project");
    public static string ProjectInfo => Lang.T("Project info");
    public static string AddProject => Lang.T("Add a project");
    // A blank page: a text that is not a file yet, named and placed when it is saved
    public static string BlankPage => Lang.T("Blank page");
    public static string Untitled => Lang.T("(untitled)");
    public static string SaveAs => Lang.T("Save as");
    public static string NotSaved => Lang.T("Not saved");
    public static string SaveFirst => Lang.T("Save the text first (:w)");
    public static string RecoverUnsaved => Lang.T("Recover unsaved text");
    public static string ProjectAlready => Lang.T("This folder is a project already");
    public static string ProjectIsTheFolder => Lang.T("This is the projects folder: its folders are the projects");
    public static string ProjectMissing => Lang.T("The folder is not there (moved, renamed or deleted)");
    public static string HelpSectionProjects => Lang.T("List of projects");
    public static string NoProjectsYet => Lang.T("No projects yet: New project makes one, Open project adds a folder");
    public static string FileNameColon => Lang.T("File name:");
    public static string ProjectNameColon => Lang.T("Project name:");
    public static string StartConfiguration => Lang.T("Configuration");
    public static string StartExit => Lang.T("Exit");
    public static string QuickGuide => Lang.T("Quick guide");
    public static string StartJump => Lang.T("Jump menu");
    public static string QuickGuideKeys => Lang.T("| h/l Page | j/k Line | ? All keys | Esc Close");
    public static string GuideKeys => Lang.T("| h/l Page | j/k Line | ? Keys | Esc Close");
    public static string StartConfigSelect => Lang.T("Show the start page on startup");
    public static string StartConfigEditor => Lang.T("Start in the file browser");
    public static string StartKeys => Lang.T("j/k 1-7 Select | Enter Open | Tab Configuration | ? Help | Q Exit");
    public static string StartHelpKeys => Lang.T("j/k Select | Enter Open | Tab Start | ? Help | Esc Back");
    public static string StartConfigKeys => Lang.T("j/k Select | Enter/Space Change | h/l Page | Tab Next tab | Esc Back");
    public static string ConfigMonochrome => Lang.T("Black and white (no colors)");
    public static string ConfigAutoSize => Lang.T("Automatic size (follow the terminal window)");
    public static string ConfigSize => Lang.T("Size: ");
    public static string ConfigSizeNote => Lang.T(" (Enter: Change)");
    public static string ConfigLineNumbers => Lang.T("Line numbers");
    public static string ConfigReset => Lang.T("Reset all settings");
    public static string ConfigRestart => Lang.T("Restart Blyant");
    public static string ConfigViewLog => Lang.T("View log");
    public static string ConfigLanguage => Lang.T("Language: ");

    public static string EditorReloaded => Lang.T("Read from the file again");
    public static string EditorReloadUnsaved => Lang.T("Unsaved changes (:e! reads the file anyway)");
    public static string EditorReloadFilesOnly => Lang.T("Only text files can be read again");
    public static string LineNumbersOn => Lang.T("Line numbers on");
    public static string LineNumbersOff => Lang.T("Line numbers off");
    public static string WrapOn => Lang.T("Long lines are wrapped");
    public static string WrapOff => Lang.T("Long lines are not wrapped (zh zl: scroll)");
    public static string SyntaxOn => Lang.T("Colors on: ");
    public static string SyntaxOff => Lang.T("Colors off");
    public static string SyntaxUnknown => Lang.T("Use md, html, css, js, json, xml, cs, php or diff, not: ");

    // The log
    public static string LogKeys => Lang.T("| h/l Page | j/k Line | gg G First/Last | /text Filter | Esc Close");
    public const string LogCommand = "Command";
    public const string LogEditorCommand = "Editor";
    public const string LogFilesCommand = "Files";
    public const string LogFile = "File: ";
    public static string MonochromeIsOn => Lang.T("Black and white is on: the colors are used when it is turned off");
    public static string Drives => Lang.T("Drives");
    public const string DirectoryMark = "<DIR>";
    public static string FileBrowserKeys => Lang.T("j/k Select | l Open | V Peek | E Projects | F Faves | O History | W Hotlist");

    public static string FileBrowserDualKeys => Lang.T("Tab Pane | l Open | V Peek | E Projects | F Faves | O History | W Hotlist");
    public static string FileTargetDualKeys => Lang.T("Tab Pane | l Open | h Up | / Filter | S {0} | Esc Cancel");
    public static string FileTargetKeys => Lang.T("j/k Select | l Open | h Up | / Filter | S {0} | Esc Cancel");
    public static string FileCopyHere => Lang.T("Copy here");
    public static string FileMoveHere => Lang.T("Move here");
    public static string FileSelectTarget => Lang.T("Go to the directory and press S (Esc cancels)");

    // File management in the file browser
    public static string NewDirectory => Lang.T("New directory");
    public static string DeleteDirectory => Lang.T("Delete directory");
    public static string DeleteNamed => Lang.T("Delete \"{0}\"?");
    public static string DeleteMarkedCaption => Lang.T("Delete files and directories");
    // Deleting to the trash (a directory in the projects folder)
    public static string TrashNamed => Lang.T("Move \"{0}\" to the trash?");
    public static string TrashFileNote => Lang.T("It can be found again with :trash");
    public static string TrashNotPossible => Lang.T("It cannot be moved to the trash");
    public static string FileItemTrashed => Lang.T("Moved to the trash: ");
    public static string TrashEmpty => Lang.T("The trash is empty");
    // Recovery copies of text that was not saved
    public static string CaptionsRecovery => Lang.T("Recovery");
    public static string RecoveryEmpty => Lang.T("There are no recovery copies");
    public static string FileMarkedKeys => Lang.T("Space Mark | Y Duplicate | C Copy | M Move | P Fave | D Delete | Esc Unmark");
    // The keys of a box of choices (ChoiceBox)
    public static string ChoiceKeys => Lang.T("j/k Select | Enter Choose | Esc Cancel");
    public static string AskTextKeys => Lang.T("Enter OK | Esc Cancel");
    // The clipboard: yanked text sent to the system clipboard (OSC 52) and/or the paste bin file
    public static string CaptionsClipboard => Lang.T("Clipboard");
    public static string HelpSectionClipboard => Lang.T("Clipboard and paste bin");
    public static string ClipboardKeys => Lang.T("j/k Select | Enter/Space Change | h/l Choice | ? Help | Esc Back");
    public static string ClipboardNote => Lang.T("The system clipboard is reached through the terminal (OSC 52): it works in Windows Terminal, iTerm2, kitty, WezTerm and tmux, also over SSH, but not in the console of a Raspberry Pi or the Terminal of macOS. There the paste bin file can be used: other programs can read it, and :pbread puts its text in the editor.");
    public static string ClipboardAllOff => Lang.T("The clipboard is off (:clipboard)");
    public static string CopiedToClipboard => Lang.T("Copied to the clipboard");
    public static string CopiedToPasteBin => Lang.T("Copied to the paste bin");
    public static string CopiedToBoth => Lang.T("Copied (clipboard, paste bin)");
    public static string CopiedToMemory => Lang.T("Copied to the string memory (p in the editor)");
    public static string CopiedToClipboardAndMemory => Lang.T("Copied to the clipboard and the string memory");
    public static string CopiedToPasteBinAndMemory => Lang.T("Copied to the paste bin and the string memory");
    public static string CopiedToAllAndMemory => Lang.T("Copied (clipboard, paste bin, string memory)");
    public static string PasteBinWritten => Lang.T("Written to the paste bin");
    public static string PasteBinAdded => Lang.T("Added to the paste bin");
    public static string PasteBinInserted => Lang.T("Inserted from the paste bin");
    public static string PasteBinCleared => Lang.T("The paste bin is empty now");
    public static string PasteBinEmpty => Lang.T("The paste bin is empty");
    public static string NoFilePath => Lang.T("This text has no file path");

    // A list to pick from (the string memory: ":reglist")
    public static string PickListNoMatch => Lang.T("Nothing matches the filter");
    public static string StringMemoryKeys => Lang.T("j/k Select | Enter Insert | / Filter | Esc Close");

    // Snippets (":snip" in the editor)
    public static string CaptionsSnippets => Lang.T("Snippets");
    public static string SnippetKeys =>
        Lang.T("Enter Insert | / Filter | S Sort | A Add | R Rename | D Delete | E Edit");
    public static string SnippetFilterKeys => Lang.T("Tab Next | Enter OK | Esc Clear");
    public static string SnippetSortFile => Lang.T("Sort: as in the file");
    public static string SnippetSortTitle => Lang.T("Sort: by title");
    public static string SnippetSortUsed => Lang.T("Sort: last used first");
    public static string SnippetNone => Lang.T("No snippets yet (A: add one)");
    public static string SnippetNoMatch => Lang.T("No titles match the filter");
    public static string SnippetCreate => Lang.T("There is no file of snippets. Make one?");
    public static string SnippetAdd => Lang.T("Add a snippet");
    public static string SnippetAddPrompt => Lang.T("Type the title of the snippet and press Enter");
    public static string SnippetRename => Lang.T("Rename the snippet");
    public static string SnippetRenamePrompt => Lang.T("Type the new title and press Enter");
    public static string SnippetTitleTaken => Lang.T("A snippet has that title already");
    public static string SnippetDeleteNote => Lang.T("It is taken out of the file of snippets.");
    public static string SnippetAdded => Lang.T("Snippet added");
    public static string SnippetsFileOpen => Lang.T("This is the file of snippets");
    public static string CompletionsFileOpen => Lang.T("This is the file of completions");
    public static string CompletionsSwitchNote =>
        Lang.T("The file of completions is opened, and this text again when it is closed.");
    public static string CaptionsCompletions => Lang.T("Completions");
    // Comparisons (":diff")
    public static string CaptionsCompare => Lang.T("COMPARE");
    public static string DiffHint => Lang.T("n N: changes, Enter: open, Esc: close");
    public static string DiffReadOnly => Lang.T("Compare: read only (Enter opens the file)");
    public static string DiffSame => Lang.T("The files are the same");
    public static string DiffSameIgnoringSpace => Lang.T("The same, except for blank space");
    public static string DiffBlankSpaceIgnored => Lang.T("(blank space ignored)");
    public static string DiffNoChanges => Lang.T("No changes");
    public static string DiffNoMoreChanges => Lang.T("No more changes after this");
    public static string DiffNoChangesBefore => Lang.T("No changes before this");
    public static string DiffNotHere => Lang.T(":diff compares a file with the saved one");
    public static string DiffSelectFile => Lang.T("Select a file to compare");
    public static string DiffMarkTwo => Lang.T("Mark two files to compare");
    public static string DiffHowTo => Lang.T("Mark two files, select one in each pane, or :diff name");
    public static string DiffSameFile => Lang.T("That is the same file");
    public static string DiffReplaceNote => Lang.T("The file is replaced by the comparison.");
    public static string WriteCopyNotHere => Lang.T(":w name saves a comparison (:diff) as a file");
    public static string CompleteOn => Lang.T("Completing words: on");
    public static string CompleteOff => Lang.T("Completing words: off");
    public static string CompleteOneKeys => Lang.T("Tab Complete");
    public static string CompleteChooseKeys => Lang.T("Tab Choose");
    public static string CompleteManyKeys => Lang.T("Tab Next | Enter Use");
    public static string PeekHint => Lang.T("Peek: Esc closes, Enter opens the file");
    public static string PeekFocusHint => Lang.T("Peek: Tab or Esc goes back to the list, Enter opens the file");
    public static string FilePeekKeys => Lang.T("j/k Peek next | Tab Into peek | l Open | V Esc End peek");
    public static string PeekReadOnly => Lang.T("Peek: read only (Enter opens the file)");
    public static string PeekSelectFile => Lang.T("Select a file to peek at");
    public static string PeekNotText => Lang.T("Not a text file");
    public static string PeekMore => Lang.T("... (open the file to see the rest)");
    public static string FileKeepsItsName => Lang.T("This file keeps its name: Blyant uses it");
    public static string SnippetsNotHere => Lang.T("Not while the settings are edited");
    public static string SnippetSwitchNote => Lang.T("The file of snippets is opened, and this text again when it is closed.");
    public static string MatchNeedsTwoPanes => Lang.T("Only with two panes (T shows them)");
    public static string TypeNameAndPressEnter => Lang.T("Type the name and press Enter");
    public static string ChoiceYes => Lang.T("Yes");
    public static string ChoiceNo => Lang.T("No");
    public static string DeleteFileNote => Lang.T("The file cannot be recovered");
    public static string DeleteEmptyDirectoryNote => Lang.T("The directory is empty");
    public static string FileItemDeleted => Lang.T("Deleted: ");
    public static string FileCopyTo => Lang.T("Copy to: ");
    public static string FileMoveTo => Lang.T("Move to: ");
    public static string FileCopied => Lang.T("Copied to ");
    public static string FileMoved => Lang.T("Moved to ");
    public static string FileCopying => Lang.T("Copying (Esc: Cancel): ");
    public static string FileCanceled => Lang.T("Canceled");
    public static string FileIntoItself => Lang.T("A directory cannot be copied or moved into itself");
    public static string FileAlreadyThere => Lang.T("It is already in that directory");
    public static string FileUsedByBlyant => Lang.T("Used by Blyant: it cannot be renamed, moved or deleted here");

    // Information about a file in the file browser
    public static string InfoCaptionFile => Lang.T("File info");
    public static string CaptionsJump => Lang.T("Jump");
    public static string CaptionsJumpInfo => Lang.T("Jump info");
    public static string HelpSectionJump => Lang.T("Jump menu");
    public static string JumpBookmarkKeys =>
        Lang.T("Enter Jump | W New | A Folder | S Subfolder | N Note | V Move | C Color");
    public static string BookmarkMoveFiltered => Lang.T("Remove the filter (Esc) to move bookmarks and add folders");
    public static string BookmarkMarkFolder => Lang.T("Only bookmarks can be marked and moved to a folder (not folders)");
    public static string BookNumbersNote => Lang.T("The numbers are on the Bookmarks tab of the Jump menu.");
    public static string BookmarksEmpty => Lang.T("No bookmarks yet: B on a list, or :bookmark");
    public static string BookmarkRename => Lang.T("Name of the bookmark");
    public static string BookmarkRenameNote => Lang.T("The place keeps its name (empty: its name is shown)");
    public static string BookmarksClearNote => Lang.T("The places themselves stay.");
    public static string BookmarksCleared => Lang.T("The bookmarks are cleared");
    public static string JumpKeys => Lang.T("j/k Select | Enter Jump | B Bookmark | / Filter | I Info | Esc Close");
    public static string CaptionsReadOnly => Lang.T("Read only");
    public static string EditorReadOnly => Lang.T("Read only (:readonly in a list)");
    public static string InfoCaptionDocument => Lang.T("Document info");
    public static string InfoCaptionDirectory => Lang.T("Directory info");
    public static string InfoName => Lang.T("Name:");
    public static string InfoType => Lang.T("Type:");
    public static string InfoSize => Lang.T("Size:");
    public static string InfoContent => Lang.T("Content:");
    public static string InfoCreated => Lang.T("Created:");
    public static string InfoChanged => Lang.T("Changed:");
    public static string InfoFavorite => Lang.T("Favorite:");
    public static string InfoFavoriteYes => Lang.T("Yes");
    public static string InfoPath => Lang.T("Path:");
    public static string InfoTypeDirectory => Lang.T("Directory");
    public static string InfoTypeNoExtension => Lang.T("File");
    public static string InfoTypeReadOnly => Lang.T(", read-only");

    // Searching for files in the file browser
    public static string SearchCaption => Lang.T("Search files");
    public static string SearchResultCaption => Lang.T("Files found");
    public static string HelpSectionSearch => Lang.T("File browser search");
    public static string SearchIn => Lang.T("In: ");
    public static string SearchName => Lang.T("Name:");
    public static string SearchText => Lang.T("Text in files:");
    public static string SearchSubfolders => Lang.T("This directory and its subfolders");
    public static string SearchThisDirectory => Lang.T("This directory only");
    public static string SearchRun => Lang.T("Search");
    public static string SearchNothing => Lang.T("Write a name or a text to search for");
    public static string SearchInvalidPattern => Lang.T("Not a regular expression: ");
    public static string SearchProgress => Lang.T("Searching (Esc: Stop), {0} found: ");
    public static string SearchCapped => Lang.T(" (the search stops at this number)");
    public static string SearchStopped => Lang.T(" (stopped)");
    public static string SearchResultKeys => Lang.T("Enter Open | B Show | I Info | P Fave | S Search | / Filter | Esc Close");

    // The favorites of the file browser
    public static string CaptionsFileFavorites => Lang.T("Favorite files");
    public static string HelpSectionFavorites => Lang.T("File browser favorites");
    // The history of the file browser (the files opened from it)
    public static string HelpSectionHistory => Lang.T("File history");
    public static string CaptionsFileHistory => Lang.T("File history");
    public static string HistoryEmpty => Lang.T("No files yet: the files opened from the file browser are listed here");
    public static string HelpSectionHotlist => Lang.T("Directory hotlist");
    public static string CaptionsHotlist => Lang.T("Hotlist");
    public static string HotlistEmpty => Lang.T("No directories yet: B in the file browser adds the one shown");
    public static string HotlistKeys => Lang.T("Enter Go | V Move | T Top | E End | D Remove | / Filter | Esc Close");
    // Shown while an item of a list is moved (the favorites, the hotlist, the bookmarks)
    public static string Moving => Lang.T("Moving: j/k, then Esc");
    public static string HotlistMoveFiltered => Lang.T("Remove the filter (Esc) to move");
    public static string HotlistNoDirectory => Lang.T("Go to a directory first");
    public static string FavoriteFolderNoPath => Lang.T("A folder of favorites has no path (:name copies its name)");
    public static string HotlistRename => Lang.T("Name in the hotlist");
    public static string HotlistRenameNote => Lang.T("The directory keeps its name (empty: its name is shown)");
    public static string HotlistCleared => Lang.T("The hotlist is cleared");
    public static string HotlistClearNote => Lang.T("The directories themselves stay.");
    public static string HistoryKeys => Lang.T("Enter Open | B Show | P Fave | D Remove | X Clear | / Filter | Esc Close");
    public static string HistoryClearCaption => Lang.T("Clear file history");
    public static string HistoryClearNote => Lang.T("The files themselves are not changed");
    public static string HistoryCleared => Lang.T("The file history is cleared");
    // The labels of the columns of the lists of the file browser (favorites, history, hotlist, search results)
    public static string ColumnName => Lang.T("Name");
    public static string ColumnPath => Lang.T("Path");
    public static string ColumnOpened => Lang.T("Opened");
    public static string ColumnLine => Lang.T("Line");
    public static string ColumnInfo => Lang.T("Info");
    public static string FavoritesHeader => Lang.T("Files and directories marked as favorites (P in the file browser)");
    public static string FavoritesEmpty => Lang.T("No favorites yet: press P on a file or directory in the file browser");
    public static string FavoritesExpanded => Lang.T("All folders are open");
    public static string FavoritesCollapsed => Lang.T("All folders are closed");
    public static string FavoritesNoFolders => Lang.T("There are no folders");
    public static string FavoritesKeys => Lang.T("B Show | W New | A Folder | S Subfolder | N Note | V Move | C Color");
    public static string FavoriteAdded => Lang.T("Added to the favorites");
    public static string FavoriteRemoved => Lang.T("Removed from the favorites");
    public static string FavoriteMissing => Lang.T("(missing)");
    public static string FavoriteMoveFiltered => Lang.T("Remove the filter (Esc) to move favorites and add folders");
    public static string FavoriteNewFolder => Lang.T("New folder");
    public static string FavoriteNoFolder => Lang.T("(No folder)");
    public static string FavoriteMoveOne => Lang.T("Move to folder");
    public static string FavoriteMoveMany => Lang.T("Move {0} favorites to folder");
    public static string FavoriteMovedOne => Lang.T("Moved to: ");
    public static string FavoriteMovedMany => Lang.T("{0} favorites moved to: ");
    public static string FavoriteMarkFolder => Lang.T("Only favorites can be marked and moved to a folder (not folders)");
    public static string FavoritesMarkedKeys => Lang.T("Space Mark | M To folder | T/E Top/End | D Remove | Esc Clear the marks");
    public static string FavoriteRemoveCaption => Lang.T("Remove favorites");
    public static string FavoriteRemoveMany => Lang.T("Remove the {0} marked favorites from the list?");
    public static string FavoriteRemovedMany => Lang.T("{0} removed from the favorites (u: Undo)");
    public static string FavoriteRenameFolder => Lang.T("Rename folder");
    public static string FavoriteDeleteFolder => Lang.T("Remove folder");
    public static string FavoriteFolderRemoved => Lang.T("Removed the folder: ");
    public static string FavoriteFolderName => Lang.T("The name cannot be used for a folder");
    public static string FavoriteNothingToUndo => Lang.T("Nothing to undo");
    public static string FavoriteRestored => Lang.T("Put back in the favorites");
    public static string FavoriteRenameFile => Lang.T("Only folders are renamed here. B shows the file, where X renames it");
    public static string HelpSectionFiles => Lang.T("File browser");
    public static string HelpKeys => Lang.T("| h/l Page | j/k Line | /text Search | n/N Next/Previous | Esc Close");
    public static string HelpKeysTopic => Lang.T("| h/l Page | j/k Line | / Search | n/N Next | ? All keys | Esc Close");
    public static string HelpKeysAll => Lang.T("| h/l Page | j/k Line | / Search | n/N Next | ? This page | Esc Close");
    public static string HelpSectionStart => Lang.T("Start page");
    public static string HelpSectionEditor => Lang.T("Editor");
    public static string HelpNotFound => Lang.T("Not found: ");
    public static string FilePickKeys => Lang.T("j/k Select | l/Enter Open/Select file | h Up | / Filter | Esc Cancel");
    public static string DirectoryPickKeys => Lang.T("j/k Select | l/Enter Open | h Up | S Select this directory | Esc Cancel");
    public static string FileTooLarge => Lang.T("The file is too large to open (the limit is 10 MB)");
    public static string FileNotText => Lang.T("This is not a text file");
    public static string DirectoryNotFound => Lang.T("Directory not found");
    public static string NotUtf8Note => Lang.T("Files are saved as UTF-8.");
    public static string ConvertReplace => Lang.T("Convert to UTF-8 and replace the file when saving");
    public static string ConvertCopy => Lang.T("Convert to UTF-8 and save a copy (the file stays as it is)");
    public static string ConvertCancel => Lang.T("Cancel");

    // Spell checking
    public static string SpellFileInvalid => Lang.T("Not a spell file that can be used (a UTF-8 .spl file is needed)");
    public static string CaptionsSpell => Lang.T("Spell checker");
    public static string SpellFileNotFound(string path) => Lang.F("Spell file not found: {0}", path);
    public static string SpellNoLanguage => Lang.T("No language given");
    public static string SpellNoWord => Lang.T("No word to add");
    public static string SpellOff => Lang.T("Spell checking is off (turn it on with :spell)");
    public static string SpellOn => Lang.T("Spell checking: ");
    public static string SpellTurnedOff => Lang.T("Spell checking off");
    public static string SpellNoMore => Lang.T("No misspelled words found");
    public static string SpellNoSuggestions => Lang.T("No suggestions for: ");
    public static string SpellWordAdded => Lang.T("Added to the word list: ");
    public static string SpellSuggestions => Lang.T("Spelling");
    public static string SpellAddWord => Lang.T("Add to word list");
    public const string SpellStatus = "SPELL";

    // Command line
    public static string NotACommand(string command) => Lang.F("Not a command: {0}", command);
    public static string ApplicationExit => Lang.F("{0} exit", ApplicationName);
    public const string CommandPrompt = "COMMAND:"; // Shown in front of a command being typed

    // Editor status line
    public const string EditorModeNormal = "NORMAL";
    public const string EditorModeInsert = "INSERT";
    public const string EditorModeVisual = "VISUAL";
    public const string EditorModeVisualLine = "V-LINE";
    public const string EditorModeVisualBlock = "V-BLOCK";
    public const string EditorMemory = "mem";
    public static string EditorSaved => Lang.T("Saved");
    public static string EditorNoChanges => Lang.T("No changes to save");

    public static string FileNotFound => Lang.T("File not found");
    public static string ErrorReadingFile => Lang.T("Error reading file");
    public static string StatusUnableToSave => Lang.T("Error: Unable to save changes");
    public static string ErrorFileAlreadyExist => Lang.T("A file with the same name already exists in the target location");
    public static string ErrorBufferSize => Lang.T("Attention: Unable to use specified output dimensions. Reverting to default.");
    public static string ErrorClientSize => Lang.T("Current terminal dimensions are unsupported.");
    public static string ErrorUnsupportedAction => Lang.T("Action unsupported");


    public const string Success = "ok";
    public const string Cleared = "cleared";
    public const string BTrue = "true";
    public const string BFalse = "false";

}