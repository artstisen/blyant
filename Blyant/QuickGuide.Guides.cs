namespace Blyant;

/// <summary>
/// The short guides of the Help tab: projects, the file browser, the text editor and the Jump menu. Each
/// tells what the part is for and its most used keys; "?" shows all of them. Many of the texts are those of the
/// help, so they read (and are translated) the same.
/// </summary>
internal static partial class QuickGuide
{
    private static List<(Kind Kind, string Key, string Text)> JumpGuide() => new()
    {
        (Kind.Title, "", Title(Guide.Jump)),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("The Jump menu takes you to a file, a directory or a page of Blyant with a few keys.")),
        (Kind.Item, ":jump  Ctrl+O", Lang.T("Open the Jump menu (from most places)")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("The tabs")),
        (Kind.Item, Lang.T("Bookmarks"), Lang.T("Places kept with B, or :bookmark in the editor")),
        (Kind.Item, Lang.T("Recent"), Lang.T("The places jumped to, the latest first (jump.txt)")),
        (Kind.Item, Lang.T("Projects"), Lang.T("The projects: Enter shows one in the file browser")),
        (Kind.Item, DialogText.HelpSectionFiles, Lang.T("Its lists: History, Favorites, Hotlist")),
        (Kind.Item, DialogText.ApplicationName, Lang.T("The pages of Blyant: settings, color schemes, logos, help")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Keys")),
        (Kind.Item, "j  k", Lang.T("Select a place")),
        (Kind.Item, "h  l", Lang.T("The list before/after (through all the lists)")),
        (Kind.Item, "Tab  Shift+Tab", Lang.T("The next/previous tab")),
        (Kind.Item, "Enter", Lang.T("Jump: open the file, directory or page")),
        (Kind.Item, "/", Lang.T("Filter the places of the tab by name (Esc: all)")),
        (Kind.Item, "B", Lang.T("Bookmark the selected place (bookmarks.txt)")),
        (Kind.Item, "I  D", Lang.T("Info/Remove (from Recent or Bookmarks)")),
        (Kind.Item, "Esc", Lang.T("Close the Jump menu")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Bookmarks: the places you use the most")),
        (Kind.Item, ":book 3", Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)")),
        (Kind.Item, "V  j  k  Esc", Lang.T("Bookmarks: move the selected one")),
        (Kind.Item, "A", Lang.T("Bookmarks: new folder (Enter opens/closes it)")),
        (Kind.Item, "S", Lang.T("Bookmarks: new subfolder in the folder")),
        (Kind.Item, "W", Lang.T("Bookmarks: new file, bookmarked below, opened")),
        (Kind.Item, "N", Lang.T("Bookmarks: write the note of the bookmark")),
        (Kind.Item, "C", Lang.T("Bookmarks: text color (used before a favorite's)")),
        (Kind.Item, ":path  :name", Lang.T("Copy the path/the name of the file (all tabs)")),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("There is more: press ? here or on the page to see all its keys."))
    };

    private static List<(Kind Kind, string Key, string Text)> FilesGuide() => new()
    {
        (Kind.Title, "", Title(Guide.Files)),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("The file browser shows the files and directories of your computer. Text files are " +
                               "opened in the editor.")),
        (Kind.Item, "F", Lang.T("Open it from the start page (File browser)")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Moving around")),
        (Kind.Item, "j  k", Lang.T("Select (as Down/Up)")),
        (Kind.Item, "l  Enter", Lang.T("Open the directory or file")),
        (Kind.Item, "h  -", Lang.T("Go to the directory above")),
        (Kind.Item, "/text", Lang.T("Only show names containing \"text\" (Esc: all)")),
        (Kind.Item, "Tab", Lang.T("Go to the other pane")),
        (Kind.Item, "T", Lang.T("Show two panes or one")),
        (Kind.Item, ":cd path", Lang.T("Go to a directory")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Files and directories")),
        (Kind.Item, "N", Lang.T("Create a new file and open it")),
        (Kind.Item, "E", Lang.T("The projects: Enter shows one in this pane")),
        (Kind.Item, "A", Lang.T("Create a new directory")),
        (Kind.Item, "X", Lang.T("Rename the file or directory")),
        (Kind.Item, "Space", Lang.T("Mark/Unmark: Y C M P D work on the marked")),
        (Kind.Item, "C", Lang.T("Copy: go to the directory, then S (Esc cancels)")),
        (Kind.Item, "M", Lang.T("Move: go to the directory, then S (Esc cancels)")),
        (Kind.Item, "D", Lang.T("Delete to the trash (asks first)")),
        (Kind.Item, "V", Lang.T("Peek on/off: the file in the other pane, j/k the next")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Finding files again")),
        (Kind.Item, "P", Lang.T("Add to/Remove from the favorites (*)")),
        (Kind.Item, "F", Lang.T("Show the favorites")),
        (Kind.Item, "O", Lang.T("Show the files opened before (file history)")),
        (Kind.Item, "B", Lang.T("Add the directory shown to the hotlist")),
        (Kind.Item, "W", Lang.T("Show the directory hotlist")),
        (Kind.Item, "S", Lang.T("Search for files by name and by text")),
        (Kind.Item, "Esc", Lang.T("Close the file browser")),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("There is more: press ? here or on the page to see all its keys."))
    };

    private static List<(Kind Kind, string Key, string Text)> EditorGuide() => new()
    {
        (Kind.Title, "", Title(Guide.Editor)),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("The editor works like Vim: in NORMAL mode keys move and give commands, in INSERT " +
                               "mode they type text. The mode is shown at the bottom left.")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Modes")),
        (Kind.Item, "i  a", Lang.T("Insert mode before/after the cursor")),
        (Kind.Item, "o  O", Lang.T("Insert mode on a new line below/above")),
        (Kind.Item, "v  V  Q", Lang.T("Visual mode (select text/lines/a block)")),
        (Kind.Item, "Esc", Lang.T("Normal mode (navigate and give commands)")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Save and close")),
        (Kind.Item, ":w", Lang.T("Save")),
        (Kind.Item, ":wq  ZZ", Lang.T("Save and close editor")),
        (Kind.Item, ":q  Esc", Lang.T("Close editor (asks to save changes)")),
        (Kind.Item, ":q!", Lang.T("Close editor without saving")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Moving the cursor (Normal and Visual mode)")),
        (Kind.Item, "h  j  k  l", Lang.T("Left/Down/Up/Right (as the arrow keys)")),
        (Kind.Text, "", ""),
        (Kind.Picture, "", NavigationPicture),
        (Kind.Text, "", ""),
        (Kind.Item, "w  b  e", Lang.T("Next word/Previous word/End of word")),
        (Kind.Item, "0  _  $", Lang.T("Line start/First character/Line end")),
        (Kind.Item, "gg  G", Lang.T("Document top/Document end")),
        (Kind.Item, "5G  :5", Lang.T("Go to line 5")),
        (Kind.Item, "Enter", Lang.T("Jump to a word on the screen: type its letters")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Editing (Normal mode)")),
        (Kind.Item, "x  X", Lang.T("Delete character under/before the cursor")),
        (Kind.Item, "yy  dd  cc", Lang.T("Yank/Delete/Change line (3dd: 3 lines)")),
        (Kind.Item, "p  P", Lang.T("Paste after/before the cursor")),
        (Kind.Item, "u", Lang.F("Undo ({0} steps)", TextBuffer.MaxUndoSteps)),
        (Kind.Item, "U  Ctrl+R", Lang.T("Redo")),
        (Kind.Item, ".", Lang.T("Repeat last change")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Search and replace")),
        (Kind.Item, "/text  ?text", Lang.T("Search forwards/backwards")),
        (Kind.Item, "n  N", Lang.T("Next/Previous match")),
        (Kind.Item, ":%s/old/new/g", Lang.T("Replace all in the document")),
        (Kind.Item, ":replace", Lang.T("Fill in a box instead of writing the command")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Other commands")),
        (Kind.Item, "Ctrl+K", Lang.T("Snippets: insert one at the cursor")),
        (Kind.Item, ":spell", Lang.T("Mark the misspelled words (again: remove the marks)")),
        (Kind.Item, ":bookmark", Lang.T("Bookmark the file being edited (Jump menu)")),
        (Kind.Item, ":split  Tab", Lang.T("Two panes on/off (Tab: go to the other)")),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("There is more: press ? here, or type :help in the editor, to see all its keys."))
    };

    private static List<(Kind Kind, string Key, string Text)> ProjectsGuide() => new()
    {
        (Kind.Title, "", Title(Guide.Projects)),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("A project is a folder with the files of a piece of writing in it: the chapters of a " +
                               "book, the parts of an article, notes and drafts. You pick where each new project " +
                               "goes, and a folder you have already can be added to the projects.")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("On the start page")),
        (Kind.Item, "P  :project", Lang.T("New project, in a folder you pick")),
        (Kind.Item, "O  :projects", Lang.T("Open project: pick one in the list of projects")),
        (Kind.Item, "  C", Lang.T("In the list: the color of the project")),
        (Kind.Item, "  N", Lang.T("In the list: the note of the project, in the editor")),
        (Kind.Item, "N  :new", Lang.T("New file (Tab in the box: another folder)")),
        (Kind.Item, "F  :file", Lang.T("File browser: open, make and manage files")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("In a project")),
        (Kind.Text, "", Lang.T("A project is shown in the file browser: N makes a file in it (a Markdown file when " +
                               "the name has no extension), A a folder, and Enter opens a file in the editor. Esc " +
                               "closes the browser and goes back to the start page. In the editor, :projects opens " +
                               "another project.")),
        (Kind.Item, "E  :projects", Lang.T("The projects: Enter shows one in this pane")),
        (Kind.Item, ":home", Lang.T("Go to the home directory")),
        (Kind.Item, ":bookmark", Lang.T("Bookmark the selected file or directory (Jump menu)")),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("There is more: press ? here or on the page to see all its keys."))
    };
}
