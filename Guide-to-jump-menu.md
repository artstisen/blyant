# Guide to the Jump menu, color schemes and logos

The same keys are used everywhere in Blyant:

- `j` `k` (or `Down`/`Up`) Select in a list, or scroll a text
- `gg` `G` First/Last (in a list, or the top/end of a text)
- `/` Search or filter
- `?` Help: the list of keys. The help is shown in pages (`h` `l` previous/next page, `j` `k` one line) and can be searched: `/text` goes to the first line containing "text", and `n` `N` to the next/previous. The help shows the keys of the page you are on (start page, file browser, favorites, Jump menu or editor); press `?` in the help to see all keys, and `?` again to return to the keys of the page
- `Ctrl+O` or `:jump` (`:ju`, `:jmp`) Open the [Jump menu](#jump-menu), to go straight to a file, a directory or a page of Blyant
- `:` Command line: type a command and press `Enter`
- While you type a command, a box above the command line lists the commands of the page in columns, narrowed to those that start with what you have typed (as `Tab` completes them): `:d` leaves `delete`, `diff`, `dup` ... The part you have typed is in the accent color, and a command typed whole (or completed with `Tab`) is highlighted, with what it does on the line at the bottom of the box (from the help). After a space it lists what can follow the command, where Blyant has a list of it (`:play ` the macros, `:surround ` the completions). Short forms (`:q`, `:w`) are not listed. The box goes away with the command line; it can be turned off in Configuration (General: _List commands above command line_, setting `commandlist`)
- `Tab` on the command line completes the command you are typing: `:proj` and `Tab` gives `:project`. Press `Tab` again for the next command that starts the same (`Shift+Tab` for the previous); after the last one, what you typed is shown again. With nothing typed, `Tab` goes through all the commands that can be used on the page. Only the name of the command is completed, not what follows it (a name, a number), and the short forms (`:q`, `:h`) are not shown. This works on every command line: on the start page, in the file browser and its lists, the Jump menu and the editor, each with its own commands
- If one of the lists of the file browser (favorites, history, hotlist) or of the Jump menu (bookmarks, recent places) cannot be read, as when another program has the file open for a moment, it is not saved until it has been read again, so nothing in it is lost (a message tells it)
- `Up` and `Down` on the command line bring back the commands given before on the same page (each page has its own, the newest first, at most 20). With the start of a command typed, only the commands that start with it are shown (`:co` and `Up` finds `:copy`, not `:delete`); `Down` past the newest gives back what you typed. The commands are kept in memory only, never written to a file, and are forgotten when you close Blyant
- `:q` Close/Return
- `Esc` Return/Cancel

## Jump menu
The Jump menu takes you straight to a place in Blyant. It is opened with `Ctrl+O`, or the command `:jump` (`:ju`, `:jmp`), from the start page, the file browser (with its favorites, history and search results) and the editor, and it is a choice of the Start tab of the start page (`5` `M`, or `Ctrl+O` there too). It is a box on top of the page with five tabs, one of them with sub-tabs:

- **Bookmarks**: the places you have kept as bookmarks (see [Bookmarks](#bookmarks) below)
- **Recent**: the places you have jumped to with the menu, the latest first, with the time (today) or the date of the jump. Only jumps made in the menu are kept, not files and pages opened in other ways (the tab says so next to the filter)
- **Projects**: the [projects](Guide-to-start-page.md#projects), in alphabetical order and in their colors. `Enter` shows the project picked in the file browser. Colors and notes are set in the list of projects (_Open project_, `:projects` or `:pro`)
- **File browser**, with the lists of the [file browser](Guide-to-start-page.md#file-browser) as sub-tabs:
  - **History**: the files opened from the file browser, with when they were opened
  - **Favorites**: the files and directories of the favorites of the file browser. A favorite with a color of its own is shown in it, on every tab
  - **Hotlist**: the directories of the [directory hotlist](Guide-to-start-page.md#directory-hotlist), in its order and with the names they are shown with there
- **Blyant**: the pages of Blyant: the Configuration tab (General, Layout, Editor, System), Color schemes, Logos, Clipboard, Snippet editor, Completions editor, Help and About

In a language where the names of the tabs do not fit in a row, _File browser_ is shown shorter (as _Files_).

The list has columns, with lines between them and their names in the line above the list (as the lists of the file browser): **Name**, then on the Bookmarks tab **Command** (the command that jumps to the bookmark), or **When** on Recent and History, then **Path** (the directory the file or directory is in; a page has none) and **Type** (the kind of place: `FILE`, `DIR` or `PAGE`) at the right. The name column is as wide as the longest name (at most half the menu), so the command is right after the name also in a wide window, and the path takes the room that is left. A long name is shortened with `...` in the middle, so its start and its extension are seen, and a long path from its start, so its end is seen. In a window too narrow for the path, it is left out. A place that is gone (a file deleted or moved) is shown dimmed with _(missing)_.

- `j` `k` (or `Down`/`Up`, `gg` `G`, `PgUp` `PgDn`) Select a place (the number of the selected place and how many there are, `5/8`, are at the right of the bottom line of the menu). With a count: `5j` `5k` move 5 rows down/up and `5gg` (or `5G`) goes to row 5 of the list (a row, not bookmark 5: the folders are rows too); the count is shown next to _/ Filter_ while you type it, and `Esc` drops it. While a bookmark is moved (`V`), `3j` moves it 3 steps
- `h` `l` (or `Left`/`Right`) The list before/after: through all the lists in a row, the sub-tabs of the file browser too
- `Tab` `Shift+Tab` The next/previous tab (on the file browser: its first list)
- `/` Filter the places of the tab: only the ones with the text in their names are shown. `Esc` shows all again
- `Enter` Jump to the selected place
- `B` Bookmark the selected place (on any list)
- `I` Information about the place: its name, kind and full path, whether it is there, and when it was jumped to or opened
- `D` Take the place out of the jump history (on the Jumps tab)
- `:` A command (see the Bookmarks tab below)
- `:path` Copy the full path of the selected file or directory, and `:name` its name (the name of the file or directory itself, also when a bookmark or a directory of the hotlist is shown with a name of its own), as in the file browser: to the system clipboard and/or the paste bin, as set, and to the string memory (so `p` in the editor puts it in). On every tab; a page of Blyant and a folder of bookmarks have neither
- `Esc` (or `Q`, `Ctrl+O`) Close the menu; `?` shows its keys. The menu opens on the tab it was on the last time (while Blyant runs), so jumping between files from the Files or Favorites tab goes on there

### Bookmarks
Bookmarks are places you keep to jump to: a file, a directory (a project) or a page of Blyant. `B` on any list of the Jump menu adds the selected place, and `:bookmark` in the editor adds the file being edited (in the file browser: the selected file or directory, on `..` the directory shown). A place is bookmarked once, and at the end of the list. On the Bookmarks tab:

- `Enter` Jump to the bookmark (on a folder: open or close it)
- `V` Move the selected bookmark or folder: it is marked `<>` and highlighted, `j` `k` move it, and `Esc` (or `Enter`, `V`) ends the moving. Not while a filter shows only some of them
- `T` `E` (or `:top`, `:bottom`) Move it to the top/end (a bookmark in a folder: of the folder)
- `X` (or `:rename name`) The name the bookmark is shown with, such as _Budget_ for `…/2026/oekonomi/budget-final-v3.md` (the place keeps its own name; an empty name shows it again). On a folder: its name
- `C` (or `:color`) Pick a text color for the bookmark or folder from the list of 16 colors. The color of a bookmark is used before the color the place has as a favorite of the file browser
- `D` (or `:remove`) Remove the bookmark (the place itself stays), and `u` (or `:undo`) puts it back
- `:clearbook` Clear the bookmarks (you are asked first)
- `:import favorites` Copy the favorites of the file browser into the bookmarks. You are told first what will be added: the files and directories (also those that are gone, which are shown as _(missing)_), in their order, after the bookmarks there are; the folders of the favorites as bookmark folders and their subfolders as subfolders, with their colors and closed if they are; a folder with the name of a bookmark folder is added to it (and a subfolder to a subfolder of the same name in it); the notes, also to a bookmark or folder that is there without a note (a note a bookmark has is kept); and a place that is a bookmark already is left out. The favorites stay as they are. `:import bookmarks` in the list of favorites copies the other way

**Folders:** Bookmarks can be grouped in folders, as the favorites of the file browser, with the same keys. A folder only exists in the list.

- `A` or `:folder name` Create a folder in front of the selected bookmark. It is shown as `[-] Name (3)` when open and `[+] Name (3)` when closed, with the number of bookmarks in it; the bookmarks of an open folder are shown below it, two spaces in
- `S` or `:subfolder name` Create a subfolder in a folder: first in it when the folder is selected, else in front of the selected bookmark (or of its subfolder). A folder holds bookmarks and subfolders, and a subfolder holds bookmarks; the bookmarks of a subfolder are shown four spaces in
- `Enter` on a folder: Open or close it. `:expand` opens all the folders, `:collapse` closes them
- **Into and out of a folder:** move a bookmark (`V`, then `j` `k`) onto a folder to put it in, and on past the first or last bookmark of the folder to take it out (in a folder, into and out of a subfolder the same way). A folder moves with its bookmarks and subfolders, and a subfolder among the bookmarks of its folder, and on into the folder above or below
- **Several at once:** mark bookmarks with `Space` (a marked bookmark has a `+` in front); `M` (or `:move`) moves the marked bookmarks (or the selected one) to a folder picked from a list (or a new folder, or out of the folders), `T` `E` to the top or the end, and `D` removes them. `Esc` takes the marks away
- `D` on a folder: Remove it with its bookmarks (you are asked first if there are bookmarks in it); `u` puts it back

- `W` or `:new` (`:new name` fills in the name) Make a new file, in the same box as _New file_ on the start page. It starts in the folder of the selected bookmark: the directory of a file, the selected directory, or for a folder the directory of the first file in it (else the folder the file browser was in last); `Tab` chooses another folder, where that folder is one of the places. The file is bookmarked below the selected bookmark (first in a folder that is selected) and opened in the editor: that is a jump, so the menu closes (`Ctrl+O` shows it again, with the new bookmark)

**Notes:** a bookmark or a folder can have a note, as the favorites of the file browser. `N` (or `:note`) writes it in the editor, with as many lines as you like; save it and close the editor. The notes are kept in `bookmarks.txt`, never next to the files. Removing a bookmark or folder with a note asks first, as the note goes with it.

**Note and Peek:** below the bookmarks are two panes for the selected bookmark or folder: **Note**, and **Peek**, the top of the file (what is in a directory or a folder, or what a page of Blyant is). They follow the selection and cannot be focused, and the peek only reads the start of a file. `:panes` hides them (and shows them again) and `:peek` the peek, kept in the settings `bookmarkpanes` and `bookmarkpeek` (apart from those of the favorites). In a window that is too low they are not shown. They are on the Bookmarks tab only.

**Line numbers:** `:number` numbers the rows of the Bookmarks tab, dimmed at the left, as `12gg` and `5j` count them (folders are rows too, so a row number is not the number of `:book`), and `:nonumber` takes them away. Off from the start; kept in the setting `bookmarknumbers`. The settings of the bookmarks are also on the Lists page of Configuration.

While the list is filtered with `/text`, the bookmarks and folders whose name or note contains the text are shown without the folders they are in, and nothing can be moved.

**Jumping with a command:** each bookmark has a number, shown next to its name on the Bookmarks tab (`:book 1`, `:book 2` ...): it is its place among the bookmarks of the list (the folders are not counted, and the bookmarks in a closed folder are), so moving a bookmark changes its number, and opening or closing a folder does not. `:book 3` jumps to bookmark 3 from anywhere the Jump menu can be opened with `:jump`: the start page, the file browser (with its favorites, history, hotlist and search results) and the editor (which asks about changes that are not saved first, as a jump does). `:book` alone opens the Jump menu on the Bookmarks tab. A number that is not a bookmark is told about in a box, with the numbers there are. In the Jump menu itself, `:book 3` jumps too (on every tab), and `:book` goes to the Bookmarks tab.

The bookmarks are kept in `bookmarks.txt` in the data directory, one on each line: the kind (`file`, `dir` or `page`), a tab character, the place, and after other tab characters the name you gave it and its color (if any), so the file can also be edited by hand. A folder has its name in brackets (`[Work]`), its color and `collapsed` if it is closed, and the bookmarks in it are the lines after it that start with a blank space (a folder line with a blank space in front, in a folder, is a subfolder, and the bookmarks in it start with two). A note is on the line after its bookmark or folder, after a `#` (an older Blyant leaves these lines out). There is no limit to the number of bookmarks. A file or directory that is renamed or moved in the file browser (or renamed with `:rename` in the editor) keeps its bookmark, and one that is gone is shown dimmed with _(missing)_.

Where a jump goes:

- **A file** is opened in the editor (where the cursor was the last time), with the file browser in its directory behind it. Closing the browser shows the start page
- **A directory** is shown in the file browser
- **A page of Configuration** (on the Blyant tab) opens the start page on that page
- **Color schemes, Logos, Clipboard, Snippet editor, Completions editor, Help and About** open on top of the page you jumped from; closing them returns there. A jump from one of them that would leave it is not made while the page below has changes that are not saved (as text in the editor): you are told, and close the page on top to save or discard them first

When you jump from the editor and the text has changes that are not saved, you are asked whether to save them first (`Esc` stays in the editor).

The jumps are kept in the file `jump.txt` in the data directory, one line for each place (at most 100): the time, a tab character, the kind (`file`, `dir` or `page`), a tab character and the place (the path of a file or directory, or the name of a page such as `config/editor`). The lines of data files that Marbles kept in these files (`data`) are left out.

## Color schemes
The colors of Blyant are chosen and edited on a page of their own, _COLOR SCHEMES_, opened with `:scheme` on the start page or from its Configuration tab (select _Color scheme_ on the Layout page and press `Enter`). It is a frame with three tabs, as the start page: **Schemes**, **Colors** (with the sub-tabs _Page 1_ to _Page 4_) and **Preview**. `Tab` goes from tab to tab, and `Shift+Tab` the other way. `h` and `l` (or `Left`/`Right`) go through the pages of Colors.

**Schemes** lists the color schemes: the nine built-in ones (Default, Deep Blue, Blyant Quest, Tasty Bytes, Yellow Monochrome, Green Monochrome, Ice Warp, Gray Box and Bright Typist, black text on a white background, as on paper), and below them the ones you have saved yourself. The one in use is marked `[x]`. A scheme can have a logo that goes with it: _Default_, _Deep Blue_ and _Gray Box_ have the logo _Blyant_, and _Blyant Quest_ and _Tasty Bytes_ the logo _Reflections_, which is used too when the scheme is selected (in a scheme file this is the line `uselogo=Reflections`, with the name of a logo; the logo can be changed afterwards as usual).

- `j` `k` (or `Up`/`Down`, `gg` `G`) Select a scheme
- `Enter` (or `Space`, `l`) Use the selected scheme: it is used at once, and its name is saved (`colorscheme` in the settings file). Its colors are then the ones edited on the Colors tab
- `Y` Duplicate the selected scheme (a built-in one too) as a scheme of your own: type a name for the copy and press `Enter` (the name with ` 2` is suggested). The copy is selected in the list, and the scheme in use stays as it is. A name that is taken, or that of a built-in scheme, cannot be used
- `R` Rename a scheme of your own: type the new name and press `Enter`. Its file is renamed, and if it is the scheme in use, the setting follows. The name of a built-in scheme or of another scheme of yours cannot be used, and the built-in schemes cannot be renamed
- `D` Delete a scheme of your own (you are asked first). The built-in schemes cannot be deleted

**Colors** shows the colors of the scheme being edited, each group with a sample and the names of its colors. _Page 1_ has Text, Logo and labels, Dividers, Favorites, Accent, Editor title, Line numbers, Current line number, Key hints, Unsaved label and Background; _Page 2_ has Highlight, Attention, Warning, Selector, Tab text, Tab frames, Tab corner, Box borders, Tab highlight, Tab focus and Other tabs, and _Highlight focused tabs_; _Page 3_ has Command line, Command box borders, Command box, Typed part, Whole command, Command hints and Command error, with a small command line and its box below them in these colors; _Page 4_ has the colors of colored text in the editor, Syntax 1 to 12.

- `j` `k` Select a group
- `Enter` (or `Space`) Pick its color from a list of the 16 colors: select with `j` `k` and press `Enter` (or `l`), or press the letter of the color, or `Esc` to cancel (see [The list of colors](#the-list-of-colors)). For Highlight, Attention, Warning, Tabs and the pairs of Page 3 (shown inverted) this is the background color, and `T` picks the text color
- `[Default]` (for the colors that have it) is not a color of its own: the color follows the one it names, as _[Default]: the dividers_ for the tab frames, so changing the dividers changes them too. Pick a color to keep it apart. The built-in schemes have all such colors set, so changing one color changes only that one
- **Accent**: the keys in the Quick guide (it was called _Preview title_; a scheme file with `previewtitle` is still read). It is also the color Syntax 2 has from the start
- **Line numbers** and **Current line number**: the line numbers of the editor, and the number of the line the cursor is on. `[Default]` is the color of the dividers, and of the text
- **Selector**: the arrow `->` that shows the selection in lists, menus, settings and boxes. `Enter` picks its background and `T` the arrow itself. It has colors of its own (in the built-in schemes the same as Highlight), so it stays as it is when the colors of Highlight are changed. A scheme file without `selector` and `selectortext` gets the colors of its highlight
- **The command line** (Page 3): the colors of the command line (`:`) and of the box of commands above it, on every page that has one, the editor too. They can be set apart from all other colors. `[Default]` is the color each had before it could be set, so a scheme of your own looks as it did; the built-in schemes have them all set to those colors (the box on the background of the scheme)
  - **Command line**: the line a command is typed on, and the `/` of the filters of the lists and the search of the editor. `[Default]` is the highlight. The mode label of the editor (`COMMAND`) keeps the highlight, as the other mode labels
  - **Command box borders**: the frame of the box of commands. `[Default]` is the box borders
  - **Command box**: the background of the box (`Enter`), and the names of the commands and what the command typed does (`T`). `[Default]` is the background and the text
  - **Typed part**: the part of the names that is typed. `[Default]` is the accent
  - **Whole command**: a name typed whole. `[Default]` is the selector
  - **Command hints**: the keys at the bottom of the box (_Tab completes, Enter runs, Esc cancels_), _+12 more_ and _No command starts with that_. `[Default]` is the dividers
  - **Command error**: _Not a command_ on the pages, and _Not an editor command_ in the editor. Other errors keep the colors of Attention. `[Default]` is Attention
  - No text of the command line or the box can have the color it is on, also not by a color it follows: Accent cannot be the background of the box while Typed part follows it
- **Syntax 1** to **Syntax 12** (Page 4): the colors of colored text in the editor (`:syntax`). They are shared by all the kinds of text that are colored (Markdown, HTML, CSS, JavaScript, JSON, XML, C#, PHP), and each is shown with what it is used for: 1 keywords, tags and selectors (and bold and list marks in Markdown), 2 names of attributes and properties (italic), 3 text in quotes and values (code), 4 numbers, 5 links, 6 comments (quotes, web addresses and rules), and 7 to 12 the headings 1 to 6 of Markdown. `[Default]` is the color each had before it could be set, shown after it: the colors of the logo, the accent, the favorites, the editor title and the dividers for 1, 2, 3, 5 and 6, and fixed colors for the numbers and the headings
- **Unsaved label**: the label _Unsaved_ shown at the top right of the editor when the text has changes that are not saved. `Enter` picks its background and `T` its text; `[Default]` is the text inverted
- **Key hints**: the keys on the bottom line of the pages (the key shown inverted, and what it does). `[Default]` is the color of the text
- **Tab text**: the text inside the frames with tabs (the start page and its Configuration, the color schemes, the logos) and in the boxes shown on top of a page (messages, questions, lists to pick from). The names of the tabs and sub-tabs, the titles of the boxes and the highlighted text of a message or question keep their own colors. `[Default]` is the color of the text. In a scheme file it is `frametext` (`tabtext` is the text of _Tab highlight_)
- **Tab frames**: the frames with tabs (the start page, this page). **Tab corner**: the left side, the corner and the top of the tab shown in such a frame (not the corner at its right end). In a lighter color than the frame it makes the tab stand out, as if light fell on it from the top left; `[Default]` is the color of the tab frames. **Box borders**: the borders and title tabs of the boxes shown on top of a page. `[Default]` (the default) is the color of the dividers
- **Tab highlight**: the tab and sub-tab shown (the focused tab), and the titles of the boxes, when they are highlighted. `[Default]` is the color of the tab frames as the background, with the text in the color of **Tab focus**, so the focused tab keeps its color also when it is highlighted (`T` gives the text a color of its own). In black and white the highlight is shown inverted. **Highlight focused tabs** (`Enter` or `Space` on it): off, the focused tab is not highlighted: its name has the color of **Tab focus**, and it is open into the frame
- **Tab focus**: the name of the focused tab and sub-tab (and the titles of the boxes) when the highlight is off. **Other tabs**: the names of the tabs and sub-tabs not shown. `[Default]` is the color of the text. With a color of their own, the focused tab can be told apart with the highlight off. The default scheme does so: the focused tab is cyan, the others dark gray, and the borders of the boxes gray
- **Background**: `[Default]` shows the text on the terminal's own background, so a transparent or blurred terminal window stays that way. Pick a color to give Blyant a background of its own: the whole window is filled with it, and the terminal's background is used again when Blyant exits. The background cannot have the color of the text on it
- A color shown in brackets in the list cannot be picked, as the text would not be visible on its background
- `E` Back to the colors of the scheme in use
- `S` Save the colors as a scheme of your own: type a name for it and press `Enter` (a scheme of your own is suggested by its name; a built-in name cannot be used, and another scheme of yours with the name is replaced if you want that). The scheme is then the one in use. Nothing you change is used until you save

**Preview** shows the colors being edited on a small screen on their background, with a frame with tabs, a box, and two lines of the editor with line numbers and colored text.

- `Esc` (or `Q`) Close. If something was changed and not saved, you are asked in a box whether to save it (`Y`, `N`, or `Esc` to stay)
- `Ctrl+O` The [Jump menu](#jump-menu), on every tab. A jump that leaves the page asks the same about changes that are not saved; a page that opens on top (Help, the logos) comes back here, and a jump to Color schemes keeps this page as it is
- `?` Show the help of the page

**Schemes of your own** are kept as text files in the directory `colorschemes` in the data directory (the name of the scheme and `.txt`), with a line for each color (`text=Gray`, `tabframes=default`, `tabhighlight=true`). They can be copied to another computer, or edited by hand: the colors are Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow, Gray, DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow and White, and `default` for the colors that can have it. If the scheme in use is missing when Blyant starts, the default scheme is used. _Reset all settings_ goes back to the default scheme.

With _Black and white_ on, the colors can still be edited: they are used when black and white is turned off.

### The list of colors
Wherever a color is picked (the colors of a scheme, the color of a favorite, a bookmark or a folder), the 16 colors are shown in a list in a box, each in its own color, with a number and a letter in front of it, as the choices of the start page:

- The letter picks the color at once: `A` Black, `B` Dark Blue, `C` Dark Green, `D` Dark Cyan, `E` Dark Red, `F` Dark Magenta, `H` Dark Yellow, `I` Gray, `M` Dark Gray, `N` Blue, `O` Green, `P` Cyan, `R` Red, `S` Magenta, `T` Yellow and `U` White. `X` picks _[Default]_ (no color of its own), where the list has it. The letters are the same in all languages, and leave out `g` `j` `k` `l` and `q`, which the list uses
- `1` to `9` select the first nine colors; `Enter` (or `Space`, `l`) then picks the selected one
- `j` `k` (or `Up`/`Down`) select, `gg` `G` the first/last
- `Esc` (or `Q`) closes the list without picking

A color shown in brackets cannot be picked (it would not be seen on its background): its letter only selects it.

## Logos
The logo of the start page can be one of your own. The logos are chosen and drawn on a page of their own, _LOGOS_, opened with `:logo` on the start page or from its Configuration tab (select _Logo_ on the Layout page and press `Enter`). It is a frame with three tabs: **Logos**, **Editor** and **Colors**. `Tab` goes from one to the next, and `h` `l` between the two pages of Colors.

**Logos** shows the selected logo at the top and the logos below it: the five built-in logos, and the ones you have saved yourself. The built-in logos are _Blyant_ (the one used from the start), _Reflections_ (the Blyant logo with a color for each row, from blue to dark green) _Adventure_ (a hut in the forest with smoke from the chimney, a mountain with snow on its top and a stream, with colors for its cells) and _Islands_ (two islands with palms, the sun over the water and stars in the sky, with colors for its cells). They are also there to work on: `E` edits one, and `S` saves it as a logo of your own with another name. The one in use is marked `[x]`.

- `j` `k` (or `Up`/`Down`, `gg` `G`) Select a logo: it is shown above the list
- `Enter` (or `Space`, `l`) Use the selected logo: it is used at once, and its name is saved (`logo` in the settings file)
- `E` Edit the selected logo (on the Editor tab)
- `Y` Duplicate the selected logo (a built-in one too) as a logo of your own: type a name for the copy and press `Enter` (the name with ` 2` is suggested). The copy is selected in the list, and the logo in use stays as it is. A name that is taken, or that of a built-in logo, cannot be used
- `R` Rename a logo of your own: type the new name and press `Enter`. Its file is renamed, and if it is the logo in use, the setting follows. The name of a built-in logo or of another logo of yours cannot be used, and the built-in logos cannot be renamed
- `D` Delete a logo of your own (you are asked first). The built-in logos cannot be deleted

**Editor** shows the logo on a grid of 4 rows of 63 characters, the size of the Blyant logo, in a box with a ruler above it (a dot for each column, `:` for every 5th, and the tens as digits). The cursor cannot leave the grid. The editor has the modes of the [text editor](Guide-to-editor.md), shown below the grid with the row and column of the cursor:

- **NORMAL**: `h` `j` `k` `l` (or the arrow keys) move, `0` `$` go to the start/end of the row, `gg` `G` to the first/last row. `x` clears the character under the cursor, `r` and a character replaces it, `dd` clears the row, and `u` undoes (the last 100 changes)
- `i` or `R` go to **REPLACE** mode, `a` after the cursor: what you type is drawn over the grid and the cursor moves on. `Backspace` clears the character before the cursor, `Enter` goes to the start of the next row, the arrow keys move, and `Esc` goes back to NORMAL
- `S` Save the logo as one of your own: type a name for it and press `Enter` (a logo of your own is suggested by its name; _Blyant_ cannot be used, and another logo of yours with the name is replaced if you want that). The logo is then the one in use
- `Esc` (or `Q`) in NORMAL mode closes the page. If something was changed and not saved, you are asked in a box whether to save it (`Y`, `N`, or `Esc` to stay)
- `Ctrl+O` The [Jump menu](#jump-menu), on every tab (not in REPLACE mode). A jump that leaves the page asks the same about changes that are not saved; a page that opens on top (Help, the color schemes) comes back here, and a jump to Logos keeps this page as it is

**Colors** has the sub-tabs _Rows_ and _Cells_. **Rows** shows the logo at the top and the color of each of its rows below it. `j` `k` select a row and `Enter` (or `Space`) picks its color from the list of colors; `[Default]` is the color of _Logo and labels_ of the color scheme. **All rows** gives all four rows the color picked, or with `[Default]` sets them all back to the color of the logo. The colors are saved with the logo (`S`), as a line `colors=` after its rows in the file (`colors=Cyan,Cyan,Blue,default`). In black and white the logo is white.

**Cells** shows the logo on the grid of the Editor tab, where each cell can be painted in a color of its own. A cell with a color of its own is drawn in it; the others have the color of their row. Below the grid are the color you paint with and the color of the cell under the cursor.

- `h` `j` `k` `l` (or the arrow keys) move, `0` `$` go to the start/end of the row, `gg` `G` to the first/last row. As `h` and `l` move in the grid here, `H` and `L` go to the other page of Colors (the hint at the right end of the sub-tabs says `H/L`)
- `c` Pick the color to paint with from the list of colors
- `e` Paint with the color of the cell under the cursor, when it has a color of its own (the line _Color of the cell_ shows the tip `(e: pick this color)`). It is a quick way to go on with a color that is already in the logo
- `Space` Paint the cell under the cursor and move to the next one (the first time, the color is asked for)
- `x` Clear the color of the cell: it has the color of its row again
- `v` Select a rectangle (**VISUAL**): move to the opposite corner, then `Space` paints all its cells and `x` clears them. `Esc` (or `v`) gives up the selection
- `X` Clear the colors of all the cells
- `u` Undo, `S` Save, `Esc` (or `Q`) Close

A new color for a row (or for all rows) does not change the cells that have a color of their own, and neither does drawing another character in a cell on the Editor tab: the color belongs to the place, not to the character. Blank space has no color to see, so paint the characters. The colors of the cells are saved with the logo as four lines `cells=` after the others, one for each row, with a character for each cell: the number of the color as a hex digit (`0` Black, `1` DarkBlue, `2` DarkGreen, `3` DarkCyan, `4` DarkRed, `5` DarkMagenta, `6` DarkYellow, `7` Gray, `8` DarkGray, `9` Blue, `A` Green, `B` Cyan, `C` Red, `D` Magenta, `E` Yellow, `F` White) and `.` for a cell without a color of its own (`cells=CCC...EE`).

Only the printable ASCII characters can be typed (letters, digits and signs such as `_ - " % # /`), so the logo looks the same in every terminal. **Logos of your own** are kept as text files in the directory `logos` in the data directory (the name of the logo and `.txt`), with the 4 rows of the logo. They can be copied to another computer or edited by hand; a file can have other characters that take one column (as the block characters `█ ▀ ▄`), but not all terminal fonts have them (the console of a Raspberry Pi does not), so ASCII is the safe choice. Rows longer than 63 characters are cut, and shorter ones filled with blank space. If the logo in use is missing when Blyant starts, _Blyant_ is used. _Reset all settings_ goes back to _Blyant_. Rows without a color of their own are drawn in the color of _Logo and labels_ of the color scheme.
