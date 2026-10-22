# Guide to the start page and the file browser

## Start page
Blyant is a text editor for writing: files, and projects of files. When it starts, the start page is shown in a frame with three tabs at its top: **Start**, where you make or open a file or a project, **Configuration**, with the settings, and **Help**, with the help, the guides and the about page. The settings are on four pages, shown as the sub-tabs **General**, **Layout**, **Editor** and **System** in a row at the top of the frame. The tab and sub-tab shown are highlighted in the color of the frame. The start page is shown again when the file browser is closed.

`Tab` goes to the next tab (Start, Configuration, Help, and round to Start again), and `Shift+Tab` the other way. `h` and `l` (or `Left`/`Right`) go to the previous and the next sub-tab of the tab shown, going round, so in Configuration they go through General, Layout, Editor and System. The keys are shown at the right ends of the two rows: `Tab` after the tabs and `h/l` after the sub-tabs, in the color of the frame. A tab opens with the sub-tab it showed last. This is the same in all the frames with tabs (the color schemes, the logos).


### Start
Each choice has a number and a key, shown in front of it. The number selects the choice (then `Enter` opens it), and the key opens it at once. The keys are the same in all languages.

- `1` `N` **New file**: Make a new, empty file and open it in the editor, with the [file browser](#file-browser) behind it. You are asked for its name; a name without an extension makes a Markdown file (`chapter 1` is `chapter 1.md`). The file is made in the directory the file browser was in last (shown in the box), or in the projects folder the first time
- `2` `F` **Open file**: Open the [file browser](#file-browser), where `Enter` opens a file in the editor. Closing the file browser returns here
- `3` `P` **New project**: Make a new [project](#projects): you are asked for its name, a folder of that name is made in the projects folder, and it is shown in the file browser
- `4` `O` **Open project**: The [projects](#projects) in a list to pick from, with what each holds below the list and the start of its note to the right of that. `Enter` shows the project picked in the file browser, `C` picks the color of the project, `N` opens its note in the editor, `I` shows information about it, `/` filters the list, `:` is its command line, `?` shows its keys and `Esc` closes it
- `5` `M` **Jump menu**: Open the [Jump menu](Guide-to-jump-menu.md#jump-menu), to go straight to a file, a directory or a page of Blyant (also with `Ctrl+O`). Pages such as Color schemes and Help open on top of the start page
- `6` `U` **Quick guide**: A short introduction to Blyant: files and projects, the keys for moving around, and the modes of the editor. `Esc` closes it, and `?` shows the help with all the keys
- `7` **Exit**: Close Blyant at once (`Enter` on it). `Q` exits too, after asking

Move the selection with `j`/`k` (or the arrow keys) or the number, and press `Enter`. `?` shows the help, and `Q` (or `Esc`) exits Blyant after asking in a box on top of the page (`Y`/`N`, or `j`/`k` and `Enter`; `Esc` cancels).

### Projects
A project is a folder with the files of a piece of writing in it: the chapters of a book, the parts of an article, notes and drafts. The projects are kept in the **projects folder**: by default the folder `Blyant-Projects` in your Documents folder (`Documents` in your home directory on macOS, and on Linux the documents folder of your desktop, `XDG_DOCUMENTS_DIR`, or else `~/Documents`). It is made when the first project is made. Another folder is set with _Projects folder_ on the General page of Configuration (saved as `projectsdir` in the settings file; `~/Writing` is `Writing` in your home directory). A folder in the settings file that is not a full path on the system Blyant runs on (`C:\Users\...` on Linux, when the settings file is shared between computers) is not used: the default folder is used instead.

A project can be made on Windows, macOS and Linux alike, and synced between them: its name may not have the characters `\ / : * ? " < > |` in it, and may not be the name of another project in other upper or lower case (`novel` when there is `Novel`).

_Open project_ lists the folders in the projects folder (not the hidden ones), in alphabetical order. A project is worked on in the file browser: `N` makes a file in it (a Markdown file when the name has no extension), `A` a folder, and `Enter` opens a file in the editor. In the file browser, `:home` goes to the projects folder.

In the list of projects, each project can have a color and a note:
- `C` picks the **color** of the name of the project in the list (_[Default]_ takes it away). It is kept in the file `.blyant` in the project
- `N` opens the **note** of the project in the editor: what to do next, an idea, where you are. It is the Markdown file `.blyant-note.md` in the project, and the start of it is shown to the right of what the project holds (with `...` where there is more). `:wq` saves it and goes back to the list; a note left empty is removed

Both files are in the folder of the project, so they go with it when it is copied, moved or synced. They start with a dot, so they are not shown with what the project holds.

`I` in the list of projects shows information about the selected project, in a box as `I` in the Jump menu: its name, what the folder holds, when it was made and changed, its color, the start of its note and its path. The list has a command line of its own (`:`, with `Tab` completing the command and `Up`/`Down` bringing back the commands given before), with commands on the selected project:

- `:info` Information about the project, as `I`
- `:path` and `:name` Copy the full path of the project folder, or its name, to the clipboard (and the string memory, as set in the clipboard settings)
- `:color` and `:note` The color and the note, as `C` and `N`
- `:help` (or `?`) The keys of the list
- `:q` (`:quit`, `:close`) Close the list, as `Esc`

The list of projects can also be opened from the file browser (`E` or `:projects`, `:pro`: the project picked is shown in the pane with the focus) and from the editor (`:projects`, `:pro`: the project picked is shown in the file browser, as a jump, after asking about changes that are not saved).

### Configuration
The settings are on four pages. `j` `k` (and `gg` `G`) select a setting on the page, and the number of a row selects it. `Enter` or `Space` changes it. `Esc` or `Q` goes back to the Start tab. Changes are saved at once.

**General**

- **Show the start page on startup** (the default)
- **Start in the file browser**: Go directly to the file browser
  - **Open the favorites list first** (shown when starting in the file browser): Start in the [favorites](#favorites) of the file browser. Closing the list shows the file browser. Saved as `startfavorites` in the settings file
  - The choice is saved as `startmode` (`select` or `editor`) in the settings file
- **Projects folder**: Where the [projects](#projects) are kept. `Enter` opens the file browser to pick the folder: go to it and press `S`
- **Delete to the trash**: Deleting in the file browser moves to the [trash](#the-trash) (on from the start). Off: deleted files cannot be recovered
- **Language**: The language of the menus, help and messages. English and Deutsch (German) are built in; Dansk (Danish), Norsk bokmål (Norwegian), Svenska (Swedish), Español (Spanish), Français (French), Italiano (Italian), Polski (Polish), Nederlands (Dutch) and Suomi (Finnish) are language files in the folder `lang` next to Blyant (see [Language files](Guide-to-installation.md#language-files)). `Enter` shows the languages in a box to pick from; a language from a file shows how much of it is translated (`Polski (pl)  98%`), and `(lang)` after a built-in language means a file in a folder `lang` is used instead of it. Commands, keys and the names of the editor modes (NORMAL, INSERT, VISUAL ...) are English in all languages, so they can be talked about the same way everywhere. The guides, the settings file and the log are in English

**Layout**

- **Automatic size**: Follow the size of the terminal window (also while you write in the editor). On from the start, and after _Reset all settings_ (saved as `autosize` in the settings file). Turned off, the default size (80 x 23) is used until you set another
- **Size**: Set the number of columns and rows by hand (shown when _Automatic size_ is off)
- **Black and white (no colors)**: Show everything in black and white. Your color scheme is kept and is used again when this is turned off
- **Color scheme**: The color scheme in use. `Enter` opens the [color schemes](Guide-to-jump-menu.md#color-schemes), where a scheme is selected, and the colors are edited and saved as a scheme of your own (not shown when _Black and white_ is on)
- **Logo**: The logo in use. `Enter` opens the [logos](Guide-to-jump-menu.md#logos), where a logo is selected, or drawn and saved as one of your own (4 rows of 63 characters)
- **Logo animation**: How fast the logo is swept in from the left when Blyant starts: _Slow_, _Fast_ (the default), _Very fast_ or _Off_. `Enter` shows them in a box to pick from. Saved as `logoanim` (`slow`, `fast`, `veryfast` or `off`) in the settings file
- **Classic ASCII corners**: Draw the corners of boxes, dialogs and tabs with `+` instead of `.` (top) and `'` (bottom). It is seen at once. Saved as `asciistyle` (`1` for `+`, `0` for `.` and `'`) in the settings file

**Editor**

- **Line numbers**: Show line numbers to the left of the text (off from the start)
- **Wrap long lines**: Long lines go on to the next rows (on from the start). When off, the view scrolls sideways (see [the guide to the editor](Guide-to-editor.md#long-lines))
- **Keep 12 lines around the cursor**: When the cursor comes within 12 lines of the top or the bottom of the editor, the text scrolls, so you see what comes next (on from the start). The number is set with `:scrolloff 8` in the editor (see [the guide to the editor](Guide-to-editor.md#scrolling))
- **Clipboard and paste bin**: `Enter` opens the clipboard settings: where yanked text goes out of Blyant (see [the guide to the editor](Guide-to-editor.md#system-clipboard-and-paste-bin))
- **Syntax highlighting**: Color the text of the kinds of files the editor knows (Markdown, HTML, CSS, JavaScript, JSON, XML, C#, PHP), on from the start. It is the setting `syntaxcolors`, which `:syntax off` and `:syntax on` in the editor also change
- **Spell checker**: The languages used when spell checking is turned on in the editor with `:spell` (it is not turned on here). `Enter` shows the languages there is a spell file for in a box, where `Enter` turns a language on or off (several can be on, and the last one stays on) and `Esc` closes. Saved as `spelllang` (`en` or `da,en`). The spell files are in the directory `spell`; without any, the row says so
- **Snippet editor**: Open the file of [snippets](Guide-to-editor.md#snippets), `snippets.txt`, in the editor. If there is no file yet, you are asked whether to make one, with two examples. Closing the editor returns to this page
- **Complete words while typing**: In Insert mode, the word typed is looked up in the lists of completions, and `Tab` completes it (see [Completing words](Guide-to-editor.md#completing-words)). Off from the start; turning it on makes the file of completions, `completions.txt`, with the lists to start with (HTML, CSS, JavaScript, Markdown with bold and italic, and a few for all text); `:complete` in the editor changes it too. It is the setting `autocomplete`
- **Completions editor**: Open the file of completions, `completions.txt`, in the editor. If there is no file yet, it is made with lists to start with. Closing the editor returns to this page
- **Recovery copies of unsaved text**: While a file has changes that are not saved, a copy of the text is kept in `recovery` in the data directory, and offered when the file is opened again (see [Recovery copies](Guide-to-editor.md#recovery-copies)). On from the start. It is the setting `recovery`

**System**

- **Reset all settings**: Set all settings back to their defaults (you are asked first, in a box where _No_ is selected first)
- **Edit settings.ini**: Open the settings file in the editor (it can also be edited from the file system). When the file is saved, Blyant starts over and reads the settings again; closing it without saving returns to the page
- **Restart Blyant**: Start Blyant over: the settings are read again and the start page is shown as at startup (you are asked first, in a box)
- **Info**: Show the version of Blyant, the directory of the program, the data directory and the settings file in a box. Any key closes it
- **View log**: Show the [log](#the-log)

### Commands
On every tab of the start page (Start, the pages of Configuration, Help), `:` opens the command line on the bottom line. `Tab` completes the command you are typing, from the commands of this page, and `Up`/`Down` bring back the commands given here before:

- `:new` (or `:new name`) New file, as `N`; `:file` (or `:open`, `:browse`) The file browser, as _Open file_
- `:project` (or `:project name`, `:newproject`) New project, as `P`; `:projects` (`:pro`, `:proj`) The list of projects, as _Open project_
- `:start` The Start tab; `:config` (or `:general`), `:layout`, `:editor` and `:system` The pages of the Configuration tab (`:editor` is the Editor page of Configuration, not the text editor)
- `:jump` (or `:ju`, `:jmp`) The [Jump menu](Guide-to-jump-menu.md#jump-menu); `:book 3` Jump to bookmark 3
- `:help` (or `:h`) The help of the start page, as `?`; `:about` The about page; `:quick` (or `:guide`) The quick guide
- `:scheme` The color schemes; `:logo` The logos; `:snippet` (or `:snip`) The snippet editor; `:completions` (or `:comp`) The completions editor; `:clipboard` The clipboard settings; `:log` The log
- `:restart` Restart Blyant (asks first); `:reset` Reset all settings (asks first, as on the System page); `:quit`, `:exit` (or `:q`) Exit Blyant (asks first)

The command names are the same in all languages.

### Help
- `1` **Help**: All the keys and commands of Blyant, as `:help` (`?` on a page shows the keys of that page)
- `2` **Quick guide**: The short introduction to Blyant, as `U` on the Start tab
- `3` **Projects guide**, `4` **File browser guide**, `5` **Text editor guide**, `6` **Jump menu guide**: A short guide to each part: what it is for and its most used keys. They are shown as the quick guide: `h`/`l` page, `j`/`k` line, `Esc` closes, and `?` shows all the keys of that part
- `7` **About**: General information about Blyant, and its license on the next page. `Esc` closes it

Select with `j`/`k` or the number and press `Enter`. `Esc` goes back to the Start tab.

## File browser
The file browser (_Open file_, `F` on the start page) is where files and projects are worked on: text files (.md, .txt, .html ...) are opened in the Blyant editor, and files and directories are made, renamed, copied, moved and deleted.

The browser shows two panes side by side, each with its own directory. The pane with the focus has a highlighted label (`PANE 1` or `PANE 2`) in the line above its list, and all keys work on that pane.

- `Tab` Go to the other pane. The selection of the pane without the focus is still shown, but not highlighted
- `T` or `:panes` Show one pane (the one with the focus, in the full width of the window) or two

In a pane that is narrow, as when the window is 80 columns wide, the type column is left out and long names are shortened. With one pane, or a wide window, all columns are shown.

The browser starts in the projects folder (or your home directory, if there is no projects folder yet) and remembers the directories of both panes, the file or directory selected in each, the pane that had the focus and whether one or two panes were shown (`browsedir1`, `browsedir2`, `browsesel1`, `browsesel2`, `browsepane` and `browsedual` in the settings file). They are saved however the browser is left (closed, a jump to another page, or exiting Blyant), and also when a file is opened in the editor. The selection is remembered by name: if the file is gone, the first row is selected.

- `j` `k` Select, `gg` `G` First/Last, `PgUp` `PgDn` One page up/down
- `l` or `Enter` Open the selected directory or file. A file is opened in the editor where the cursor was when you closed it the last time (see [Where the cursor was](#where-the-cursor-was))
- `h`, `-` or `Backspace` Go to the directory above (above the root of a drive on Windows: the list of drives)
- `/text` Only show the names containing "text". `Esc` shows all again
- `N` or `:new name` Create a new file in the directory and open it. Without a name (`N` or `:new`) you are asked for it. A name without an extension makes a Markdown file: `chapter 1` is `chapter 1.md` (a name that starts with a dot, as `.gitignore`, is kept as it is)
- `E` or `:projects` (`:pro`, `:proj`) Show the [projects](#projects) in a list: `Enter` shows the one picked in the pane with the focus, `/` filters and `Esc` closes. While the place to copy or move to is selected, it takes you to a project too
- `I` or `:info` (also in the list of favorites) Show information about the selected file or directory: type, size (for a directory: the number of files and directories in it), when it was created and last changed, and the full path. Any key closes the box
- `V` or `:peek` Peek on or off: the selected file is shown read-only in the other pane (with one pane shown, the second pane is shown while you peek), and the focus stays in the list. The pane of the peek is labeled _peek_ (_PEEK_, highlighted, while the focus is in it), with the name of the file at its top in place of the directory; when the peek ends, the pane has its own label and directory again. The peek follows the selection, so you can go through the files with `j` and `k` (and the other keys that move in the list, also into and out of directories); a directory or a file that is not text is told about in the peek pane. `Tab` goes into the peek: move and search in it with the keys of the editor (`j` `k`, `gg` `G`, `Ctrl+D`, `/text`, `y` to yank to the clipboard ...); what would change the text is refused. The file is shown without word wrap, also when word wrap is on in the editor (`h` `l`, `0` `$` move along a long line); `:wrap` turns it on for this peek only. In the peek, `Enter` opens the file in the editor at the line of the cursor, and `Tab` or `Esc` (or `:q`) goes back to the list. `V` or `Esc` in the list ends the peek, and so do the keys and commands that use the other pane (copy, move, compare ...), before they do what they do. A large file is shown up to about 2000 lines (100 KB), with a last line telling that there is more
- See [Managing files](#managing-files), [Favorites](#favorites), [File history](#file-history) and [Directory hotlist](#directory-hotlist) below for the other keys
- `B` or `:hotadd` Add the directory shown to the [directory hotlist](#directory-hotlist), whatever file is selected in it. `W` or `:hotlist` shows the hotlist
- `R` or `:refresh` Refresh: read the directories again (the selection stays)
- `:path` Copy the full path of the selected file or directory (on `..`: the directory shown) to the system clipboard and/or the paste bin, as set in the [clipboard settings](Guide-to-editor.md#system-clipboard-and-paste-bin)
- `:book 3` Jump to bookmark 3 of the [Jump menu](Guide-to-jump-menu.md#bookmarks) (`:book` alone shows the bookmarks). Also on the start page, and on the pages of the favorites, the history, the hotlist and the search results
- `:bookmark` Bookmark the selected file or directory (on `..`: the directory shown) on the Bookmarks tab of the [Jump menu](Guide-to-jump-menu.md#bookmarks)
- `:diff` Compare two text files: the two marked with `Space`, or (without marks, with two panes) the file selected in each pane. `:diff name` compares the selected file with the file of that name in the directory (or a path). The older file, by the time it was last changed, is the old one, so `+` is what was added since. The comparison is shown as in the editor (see [Comparing](Guide-to-editor.md#comparing)): `n` `N` (or `]c` `[c`) go to the changes, `Enter` opens the file at the line, `Esc` returns here. `:diff -w` does not count blank space. If the line endings or the encodings differ, the line at the top says so (they are not compared). Directories and files that are not text are not compared
- `:name` Copy the name of the selected file or directory (on `..`: of the directory shown) in the same way: `notes.md`, not the whole path
- `:path` and `:name` also put the path or name in the [string memory](Guide-to-editor.md#string-memory) of the editor, as the current string: `p` in the editor (or `CTRL+R` in Insert mode) puts it in the text. This works also when the clipboard and the paste bin are turned off
- `:match` With two panes: the other pane shows the directory of the pane with the focus (with the same file or directory selected). The focus stays where it is
- `:cd path` Go to a directory. `:home` Go to the projects folder (or your home directory, if there is no projects folder yet)
- `:trash` Go to the [trash](#the-trash), with what was deleted
- `:recovery` Go to the [recovery copies](Guide-to-editor.md#recovery-copies) of text that was not saved
- `:data` Go to the data directory itself, with the files Blyant keeps there: `settings.ini`, `snippets.txt` and `completions.txt` (the snippets and the completions of the editor), `favorites.txt`, `history.txt` and `hotlist.txt` (of the file browser), and `jump.txt` and `bookmarks.txt` (of the Jump menu)
- `Tab` on the command line completes the command you are typing, and goes through the commands of the file browser when pressed again; `Up` and `Down` bring back the commands given in the file browser before
- `?` Help
- `Esc`, `Q` or `:q` Close the file browser
- `:exit` Exit Blyant (asks first)

A text file is opened in the [editor](Guide-to-editor.md), where `:w` saves it and `:q` returns to the browser. Files are saved as UTF-8 with the line endings they had. Files larger than 10 MB and files that are not text are not opened.

**Files that are not UTF-8:** When opening a file in another encoding (UTF-16, or the Windows-1252/ISO-8859-1 used by older Western European text files), Blyant asks whether to convert it to UTF-8 and replace the file, or to save the converted text as a copy named with "-utf8" and leave the file as it is. Nothing is written before you save in the editor. Blyant cannot tell which 8-bit encoding a file uses: if the text looks wrong, close the editor without saving.

### Managing files
These work on the selected file or directory. Duplicate, copy, move, favorite and delete also work on several at once (see marking below):

- `A` or `:mkdir name` Create a new directory
- `X` or `:rename name` Rename. Without a name you are asked for it, starting from the current name
- `Y` or `:duplicate` Make a copy in the same directory (named `name-copy`)
- `C` or `:copy` Copy to another directory
- `M` or `:move` Move to another directory
- `D` or `:delete` Delete: move to the [trash](#the-trash) (or delete for good, when that is turned off). You are asked first, and for a directory you are told how many files and directories it contains
- `Space` Mark (or unmark) the selected file or directory and go to the next. A marked one has a `+` in front of its name, and the number marked is shown at the bottom right of the pane. While some are marked, `Y` `C` `M` `P` and `D` work on all the marked ones instead of the selected one: they are duplicated, copied or moved to the directory you select, made favorites (or, if they all are favorites already, removed from the favorites), or deleted after one question that tells how many files and directories are deleted in all. The marks stay while the pane shows the same directory (also with a filter), and `Esc` clears them. If one of them fails, the operation stops there and the message tells which one; those not done stay marked. `X` (rename) and `I` (info) always work on the selected one

Names (of a new file or directory, a new name for one, and the name of a favorites folder) are typed in a box on top of the list: `Enter` accepts, `Esc` cancels. Questions (delete, converting a file that is not UTF-8, and removing favorites) are asked in a box too: `Y` or `N` answers at once, or select with `j` `k` and press `Enter`. `Esc` cancels. When something cannot be undone, the question is highlighted as a warning and _No_ is selected first. The question before deleting cannot be answered with `Y`, and only `Enter` answers it: select _Yes_ with `k` (or the arrow key `Up`) and press `Enter`. `Esc`, `Q`, `h` or `Enter` on _No_ cancels. That way text typed in the browser by mistake, meant for the editor, is unlikely to answer it: in "today", `d` asks to delete and `y` would have answered yes, and in "dark " the `k` selects _Yes_ but the space does not pick it.

A new name (of a file or a directory) follows the same rules on all platforms, so files can be copied between Windows, macOS and Linux: it cannot contain `\ / : * ? " < > |` or control characters, cannot end with a dot or a space, can be at most 255 characters long, and cannot be a name Windows keeps for devices (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`, also with an extension, as `con.txt`). If a name cannot be used, the message tells why, or that a file or directory with that name already exists.

For copy and move you select where to: after `C` or `M` the browser stays as it is, with the name of the file (or the number marked) at the top left, in the attention color (as the name of the pane with the focus). Go to the directory, in either pane, and press `S` to copy or move the file to the directory shown in the pane with the focus. `Esc` cancels. With two panes the focus goes to the other pane, so pressing `S` at once copies or moves to it, and `Tab` takes you back if the place is in the first pane. With one pane you go to the directory in that pane. While you are selecting, the other file keys and commands are not in use (`E`, `:cd`, `:home`, `:data` and `:projects` are). Both panes are updated afterwards, and the focus stays in the pane copied or moved to, with the (last) file copied or moved selected. While a directory is being copied the name of the current file is shown, and `Esc` cancels the copy (what was copied so far is removed).

Things to know:

- **Deleted files go to the trash** of Blyant (see below), not to the recycle bin of the system. With _Delete to the trash_ turned off, deleted files cannot be recovered.
- Nothing is overwritten. An operation is refused if something with the same name is already in the target directory.
- A directory cannot be copied or moved into itself.
- What Blyant is using cannot be renamed, moved or deleted here: the data directory (and the directories it is in), `settings.ini`, the favorites list and the file history.
- Hidden files are not shown in the browser, but they are included when a directory is copied, moved or deleted.

### The trash
What is deleted in the file browser is moved to the trash: the folder `.trash` in the projects folder (hidden from the list of projects, as its name starts with a dot). It is made when the first file is deleted. `:trash` goes there, and a file is restored by moving it back (`M`, or `X` to give it its old name).

- Each file or directory gets the time it was deleted in front of its name: `2026-10-22 14.03.11 chapter 1.md`. So a file deleted twice is kept twice
- The list `trash.txt` in the trash tells where each one was: its name in the trash, when it was deleted, and the folder it came from (separated by tab characters)
- Deleting in the trash is for good, and so is deleting the trash itself, or a folder the trash is in (the projects folder). The question says so
- If something cannot be moved to the trash (it cannot be written to, or the trash is on another drive and the copy fails), you are asked whether to delete it for good instead
- The trash is not emptied by itself: delete what is in it when you no longer need it
- _Delete to the trash_ on the General page of Configuration (setting `deletetotrash`) turns the trash off: files are then deleted for good

### Searching for files
`/text` only narrows the list of the directory shown. To find files in the directories below it as well, or by the text in them, use the search:

- `S` or `:find` Open the search box. It searches the directory of the pane with the focus

In the box:

- **Name**: Part of the name of the file. With `*` (any characters) and `?` (one character) the whole name must fit, as in `*.md`. Leave it empty to search all files for a text
- **Text in files**: Find the text files that contain a text. Files that are not text and files larger than 10 MB are not searched
- **Regular expression**: The name and the text are regular expressions (`^(notes|plan)\.`, `\d{4}-\d\d`)
- **Match case**: Off, so `cat` also finds `Cat`
- **Where**: The directory and its subfolders, or the directory only
- **Search**: Press `Enter` here to search

The box opens in the _Name_ field: type and press `Enter`. `j` `k` select a row, `Enter` or `Space` edit a field or change an option, and `Esc` closes the box. What you entered is remembered.

While it searches, the bottom line shows the directory being read and the number of files found. `Esc` stops the search and shows what was found so far. The search also stops at 5000 files. Hidden files and directories are not searched, and links to directories are not followed.

The files found are shown in a list, with the directory they are in. When you searched for a text, the first line containing it is shown with its line number.

- `j` `k` Select, `gg` `G` First/Last
- `l` or `Enter` Open the file in the editor, at the line that was found. Closing the editor returns to the list
- `B` or `:goto` Show the file in the browser: its directory is opened in the pane with the focus, with the file selected
- `I` or `:info` Show information about the file
- `P` or `:fav` Add the file to the favorites, or remove it
- `/text` Only show the files with "text" in the path or the line found. `Esc` shows all again
- `S` or `:find` Search again
- `Esc`, `Q`, `h` or `-` Return to the browser

### Favorites
Files and directories you use often can be marked as favorites:

- `P` or `:fav` Add the selected file or directory to the favorites, or remove it. Favorites are marked with a `*`
- `F` or `:favorites` Show the list of favorites

In the list of favorites:

- `j` `k` Select, `gg` `G` First/Last
- `l` or `Enter` Open the file in the editor (closing the editor returns to the list). A directory: go to it in the browser
- `B` or `:goto` Show the file in the browser: its directory is opened with the file selected, where it can be renamed, copied, moved or deleted
- `V` Move the selected favorite or folder: it is marked `<>` and highlighted, and follows `j` `k` up and down the list until you press `Esc` (or `Enter`, `V`)
- `C` or `:color` Pick a text color for the favorite from the list of 16 colors
- `P`, `D` or `:remove` Remove it from the favorites (the file itself stays where it is)
- `u` or `:undo` Put back the favorite, or the folder with its favorites, that was removed last
- `A` or `:folder name` Create a folder (see below)
- `/text` Only show the paths containing "text". `Esc` shows all again
- `?` Help
- `Esc`, `Q`, `F`, `h` or `-` Return to the browser

**Folders:** Favorites can be grouped in folders. A folder only exists in the list of favorites; nothing is created on disk and no files are moved.

- `A` or `:folder name` Create a folder in front of the selected item. It is shown as `[-] Name` when open and `[+] Name` when closed, with the number of favorites in it
- `l` or `Enter` on a folder: Open or close it. The favorites in an open folder are shown below it, two spaces in
- `:expand` Open all the folders, `:collapse` Close all the folders
- **Several at once:** Mark the favorites with `Space` (it marks or unmarks the selected favorite and goes to the next; a marked favorite has a `+` in front, and the number of marked is shown at the bottom right). Then press `M` and pick where they go from the list: one of your folders, _New folder..._ (you are asked for its name, and it is placed where the first of the favorites is) or _(No folder)_. Without marks, `M` moves the selected favorite. With marks, `D` removes all the marked favorites (you are asked first), and `u` puts them back. `Esc` clears the marks
- **To the top or the end:** `T` (or `:top`) moves the marked favorites to the top of the list, and `E` (or `:bottom`) to the end, in the order they have. Without marks it moves the selected favorite, or the selected folder with its favorites. A favorite in a folder goes to the top or the end of its folder (`M` moves it out of the folder)
- **Into a folder:** Move a favorite (`V`, then `j` `k`) onto the folder. Moving down onto a folder puts it first in the folder, moving up from below puts it last. A closed folder opens
- **Out of a folder:** Move the favorite on, past the first or the last favorite in the folder
- A folder is moved like a favorite. It is closed first, and moves with its favorites past the favorite or folder next to it
- `X` or `:rename name` Rename the folder
- `C` Pick a color for the folder
- `P`, `D` or `:remove` Remove the folder. If there are favorites in it you are asked first, as they are removed from the list with it (the files stay)

While the list is filtered with `/text` the favorites found are shown without the folders, and nothing can be moved.

The list is kept in the file `favorites.txt` in the data directory (one line per favorite: the color, a tab character and the path; a folder has its name in brackets instead of a path, and the favorites in it start with a blank space). A file that has been closed in the editor has the line and column of the cursor before its path (`3:12` and a tab character). Favorites follow when you rename or move a file or directory in the browser, and are removed when you delete it. A favorite whose file is gone is shown as _(missing)_.

### File history
The file history lists the files you have opened in the editor from the file browser (also from the favorites, the search and the history itself), the last opened first, with the time each was opened. A file opened again moves to the top. It is practical when you work on many files over several days.

- `O` or `:history` (in the file browser) Show the file history
- `j` `k` Select, `gg` `G` First/Last, `PgUp` `PgDn` One page up/down
- `l` or `Enter` Open the file in the editor (`:q` returns to the history)
- `B` or `:goto` Show the file in the file browser: its directory, with the file selected
- `P` or `:fav` Add the file to the favorites, or remove it. A favorite has a `*` after its name
- `I` or `:info` Show information about the file
- `D` or `:remove` Take the file out of the history (the file itself stays)
- `X` or `:clear` Clear the file history. You are asked first, in a box on top of the list
- `:name` Copy the name of the file, `:path` its full path, as in the file browser: to the system clipboard and/or the paste bin, as set, and to the string memory (so `p` in the editor puts it in)
- `/text` Only show the files whose path contains "text". `Esc` shows all again
- `?` Help, `:` the command line
- `Esc`, `Q`, `O`, `h` or `-` Back to the file browser

The history holds the last 100 files: when it is full, the file opened longest ago is dropped. A file that is gone is shown as _(missing)_. The list is kept in the file `history.txt` in the data directory (one line per file: the time it was opened, a tab character and the path, with the line and column of the cursor and a tab character before the path once the file has been closed in the editor).

### Directory hotlist
The directory hotlist keeps directories you want to go back to. `B` (or `:hotadd`) in the file browser adds the directory shown in the pane with the focus, whatever file or directory is selected in it, at the end of the list. A directory that is in the list already stays where it is. Taking directories out is done on the page of the list.

- `W` or `:hotlist` (or `:hot`) in the file browser: Show the hotlist. Each row has the name of the directory (or the name you gave it), and its path after it. The hotlist is also a tab of the [Jump menu](Guide-to-jump-menu.md#jump-menu)
- `j` `k` Select, `gg` `G` First/Last, `PgUp` `PgDn` One page up/down
- `l` or `Enter` Go to the directory, in the pane with the focus (the hotlist closes)
- `V` (or `:move`) Move the directory in the list: it is marked `<>` and highlighted, `j` `k` move it, and `Esc` (or `Enter`, `V`) ends the moving. Not while a filter shows only some of the list
- `T` or `:top` Move the directory to the top, `E` or `:bottom` to the end
- `I` or `:info` Show information about the directory
- `X` or `:rename name` The name the directory is shown with in the hotlist (and the Jump menu), such as "Project docs" for a deep path. Without a name you are asked for it. The directory itself keeps its name; an empty name shows its own name again
- `D` or `:remove` Take the directory out of the hotlist (the directory itself stays). `u` or `:undo` puts it back
- `:clear` Clear the hotlist. You are asked first
- `:name` Copy the name the directory is shown with (the name you gave it, or its own), `:path` its full path, as in the file browser (also to the string memory, for `p` in the editor)
- `/text` Only show the directories whose path contains "text". `Esc` shows all again
- `?` Help, `:` the command line
- `Esc`, `Q`, `W`, `h` or `-` Back to the file browser

The hotlist holds at most 100 directories. A directory that is gone is shown as _(missing)_. When a directory is renamed or moved in the file browser, the hotlist follows it (also the directories in it). The list is kept in `hotlist.txt` in the data directory, with the path of a directory on each line (and after it a tab character and the name you gave it, if any), so it can also be edited by hand (`:data` in the file browser goes there).

### Where the cursor was
When a file is closed in the editor, the line and column of the cursor are kept: in the history, and in the favorites if the file is one. The next time the file is opened, the cursor is placed there, so you go on where you left off. The favorites are looked in first, as the history holds only the last 100 files. If the file has changed since and the line or column is no longer there, the cursor goes to the end of the line or of the text. A file opened from the results of a [search](#searching-for-files) is opened at the line found instead.

## The log
The log lists what has happened since Blyant was started: the startup, commands, and the file operations of the file browser and the start page (new, open, rename, copy, move and delete, with the reason if one failed). It is shown from the System page of Configuration (_View log_), or with `:log`. A deletion tells whether it went to the trash, and where.

- The log opens at the end, with the latest lines
- `h` `l` Previous/Next page, `j` `k` One line, `gg` `G` First/Last
- `/text` Only show the lines containing "text", for example `/failed` or `/file:`. `Esc` shows all again
- `Esc` or `Q` Close the log

The log shown holds the latest 1000 lines since Blyant was started. It is also written to the file `log.txt` in the data directory, with the date in front of each line, so it can be read after Blyant is closed (`:data` in the file browser goes there). When `log.txt` has grown larger than 1 MB, it is renamed `log.old.txt` at startup (replacing the one before) and a new one is begun. Commands are logged by name only, and the text of your files is never logged.

As the log can be read by anyone at the screen, commands are logged by name only (`:new ...`, `:s ...`): search texts and other text typed after a command are not in the log. Something typed on a command line that is not a command is logged as "(not a command)". File names and paths in the file browser are logged in full.

## Logo animation
The logo is drawn with a sweep when Blyant starts. Any key ends the sweep. Its speed is set with _Logo animation_ on the Layout page of the Configuration tab, where it can also be turned off.

## Directories
Blyant keeps its settings and lists in the directory `data`: in the directory it is started from, if there is one there, else next to the program (and where the program directory cannot be written to, `Blyant/data` in the application data of the user). The data directory holds `settings.ini`, the lists of the file browser and the Jump menu, the snippets, the completions, the macros, the color schemes and the logos of your own, the log (`log.txt`) and the recovery copies of text that was not saved (`recovery`). Your projects are not kept there, but in the [projects folder](#projects).
