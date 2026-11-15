# Settings file
Blyant keeps its settings in `settings.ini`, a text file in the data directory (see [Good to know](Guide-to-installation.md#good-to-know)). _Info_ on the System page of Configuration shows where it is. Most settings are changed on the pages of Configuration or with a command, and are saved at once. The file is for reading them, and for the few that are set nowhere else.

## Reading and changing it
The file starts with `[SETTINGS]`, and has one setting a line, `name=value`. A line starting with `;` is a comment: Blyant writes the time of the last change and a heading over each group of settings so.

```
; Last modified 2026-11-15 14:05:33
[SETTINGS]

; DIMENSIONS
linewidth=79
linecount=23
autosize=true
```

- Open it in the editor with _Edit settings.ini_ on the System page of Configuration: when it is saved, Blyant starts over with the settings in it. In the shell, `open settings.ini` from `data` opens it, and the settings are used when it is saved; `reload` reads the file again after it was changed in another program
- In the shell, `set` lists the settings as they are in the file, `set indent 2` changes one, and `set shelldir ""` empties one (see the [guide to the shell](Guide-to-shell.md#text-and-settings))
- `true` and `false` turn a setting on and off. A value a setting cannot have gives it its default, and so does a setting that is not in the file: a file written by an earlier version of Blyant lacks the settings that came after it
- Blyant writes the whole file each time a setting changes: comments and lines of your own are not kept, and names it does not know are left out (as the lines of Marbles, which Blyant came from)
- _Reset all settings_ on the System page (and `reset` in the shell) puts the settings back to their defaults. The language, the spell languages and the places Blyant keeps (the directories of the file browser, `pickdir`, the hint shown next) stay as they are, and so do your lists, favorites, bookmarks and files
- A file that cannot be read when Blyant starts is kept as `settings.ini.bad` before a new one is written, so the settings in it are not lost

The tables below tell each setting, the values it takes (the default in bold), and where it is changed.

## Start and general
| Setting | Values | What it is |
|---|---|---|
| `startmode` | **select**, editor, blank | Start on the start page, in the file browser or on a blank page (General page of Configuration) |
| `startfavorites` | **false**, true | Start in the favorites, when starting in the file browser (_Open favorites list first_) |
| `startshell` | **false**, true | Start as the shell (_Start in the shell_) |
| `filetype` | **md**, typ, txt, html, css, js, json, xml, cs, php, ini | The kind of a new file without an extension, and of a blank page (_New files and blank pages_) |
| `newlineendings` | **system**, lf, crlf | Line endings of new files (_Line endings of new files_) |
| `deletetotrash` | **true**, false | Deleting in the file browser moves to the trash (_Delete to trash_) |
| `commandlist` | **true**, false | List the commands above the command line while one is typed |
| `language` | **en**, de, da, nb, sv, es, fr, it, pl, nl, fi | The language of the menus, help and messages (_Language_). A language whose file is missing is kept, and English is used meanwhile |
| `hints` | **true**, false | A hint on the Start tab (System page; `X` on the Start tab hides them) |
| `hintindex` | **-1**, a number | The hint shown next time (kept by Blyant) |

## Size and looks
| Setting | Values | What it is |
|---|---|---|
| `autosize` | **true**, false | Follow the size of the terminal window (_Automatic size_ on the Layout page) |
| `linewidth` | **79**, up to 255 | Columns used when _Automatic size_ is off (_Size_) |
| `linecount` | **23**, up to 255 | Rows used when _Automatic size_ is off |
| `colorscheme` | **Default**, the name of a scheme | The color scheme in use (_Color scheme_, `:colorscheme name`) |
| `monochrome` | **false**, true | Black and white, no colors (_Black and white_) |
| `logo` | **Blyant**, the name of a logo | The logo in use (_Logo_) |
| `logoanim` | **fast**, slow, veryfast, off | How fast the logo is swept in at the start (_Logo animation_) |
| `asciistyle` | **0**, 1 | Corners of boxes as `.` and `'`, or as `+` (_Classic ASCII corners_) |

## Editor
| Setting | Values | What it is |
|---|---|---|
| `linenumbers` | **false**, true | Line numbers left of the text (_Line numbers_ on the Editor page) |
| `gutter` | **false**, true | A blank margin of four columns when the line numbers are off |
| `wrap` | **true**, false | Long lines go on to the next rows (_Wrap long lines_) |
| `whitespace` | **false**, true | Show blank space as `.` `>` `$` (`:whitespace`) |
| `indent` | **4**, 2, 8 | Spaces that `Tab` inserts, and that `>>` and `<<` indent by |
| `zen` | **false**, true | Zen mode: only mode, name and line (`:zen`) |
| `zencolors` | **true**, false | The mode at the top in Zen mode in the color of its mode (_Colored mode label_) |
| `marksapostrophe` | **false**, true | `'a` jumps to the exact place of mark `a`, as `` `a `` does |
| `scrolloff` | **true**, false | Keep lines above and below the cursor when the text scrolls (_Lines kept around cursor_) |
| `scrolllines` | **12**, 1 to 999 | How many lines are kept (`:scrolloff 8`) |
| `syntaxcolors` | **true**, false | Color the text of the kinds of files the editor knows (`:syntax on`, `:syntax off`) |
| `spelllang` | **en**, a language, or several (`da,en`) | The languages of `:spell` (_Spell checker_) |
| `autocomplete` | **true**, false | Complete words while typing (_Complete words while typing_, `:complete`) |
| `snipsort` | **file**, title, used | The order of the snippets (`S` in the list of snippets) |
| `recovery` | **true**, false | Recovery copies of unsaved text (_Recovery copies of unsaved text_) |

## Clipboard and paste bin
Changed in _Clipboard and paste bin_ on the Editor page (see [System clipboard and paste bin](Guide-to-editor.md#system-clipboard-and-paste-bin)).

| Setting | Values | What it is |
|---|---|---|
| `osc52` | **true**, false | Yanked text goes to the clipboard through the terminal |
| `pastebin` | **false**, true | Yanked text is written to the paste bin file |
| `clipboardyank` | **all**, plus | Every yank goes out of Blyant, or only one with `"+` in front of it (`"+yy`) |
| `pastebinappend` | **false**, true | A yank is added to the end of the paste bin file, instead of replacing what is in it |
| `pastebinfile` | **paste-bin.txt**, a path | The paste bin file (`:pbpath` shows it) |

## Lists
Changed on the Lists page of Configuration, or with the commands of the lists (`:number`, `:panes`, `:peek`).

| Setting | Values | What it is |
|---|---|---|
| `favoritelist` | **empty** (the main list), a name | The list of favorites in use (`:list name`; `fav name` in the shell) |
| `favoritepanes` | **true**, false | The Note and Peek panes below the favorites |
| `favoritepeek` | **true**, false | The Peek pane of the favorites |
| `favoritenumbers` | **false**, true | Numbers on the rows of the favorites |
| `filenumbers` | **false**, true | Numbers on the rows of the panes of the file browser |
| `historynumbers` | **false**, true | Numbers on the rows of the history |
| `hotlistnumbers` | **false**, true | Numbers on the rows of the hotlist |
| `searchnumbers` | **false**, true | Numbers on the rows of the search results |
| `bookmarkpanes` | **true**, false | The Note and Peek panes of the bookmarks in the Jump menu |
| `bookmarkpeek` | **true**, false | The Peek pane of the bookmarks |
| `bookmarknumbers` | **false**, true | Numbers on the rows of the bookmarks |

## File browser
Kept by Blyant as you use the file browser, so it opens as you left it.

| Setting | Values | What it is |
|---|---|---|
| `browsedual` | **true**, false | Two panes, or one |
| `browsepane` | **1**, 2 | The pane that had the focus |
| `browsedir1` | a path | The directory of the left pane |
| `browsedir2` | a path | The directory of the right pane |
| `browsesel1` | a path | The file or directory selected in the left pane |
| `browsesel2` | a path | The file or directory selected in the right pane |
| `pickdir` | a path | Where the box that picks a folder (for a project) opens |

## Corkboard and modules
| Setting | Values | What it is |
|---|---|---|
| `boardname` | **empty** (the main board), a name | The board that is open (`:board name`) |
| `boardpeek` | **true**, false | The peek below the board |
| `boardmode` | **all**, both, cards, badges, lines | What `h` `j` `k` `l` and `Tab` go to on the board (`M`, `:mode`) |
| `moduleprojects` | **true**, false | The projects (Modules page of Configuration) |
| `moduleboard` | **true**, false | The corkboard |
| `modulejump` | **true**, false | The Jump menu and the bookmarks |

## Shell
Changed with `set` in the shell, or in the file: they are not on the pages of Configuration (only `startshell` is, above). See the [guide to the shell](Guide-to-shell.md).

| Setting | Values | What it is |
|---|---|---|
| `shellpaging` | **true**, false | A long output comes in pages in a terminal |
| `shellcompletion` | **true**, false | `Tab` and the keys of the line; off: the line is read as the terminal reads it (for screen readers) |
| `shellrecentfirst` | **true**, false | `Tab` with nothing typed offers the files opened last first |
| `shellautopeek` | **false**, true | After a command that changed the file of the last peek, its window is shown again (as `.` shows it) |
| `shellpeekstep` | **1**, 1 to 1000 | Lines that `+` and `-` move the window of the last peek |
| `shellpeekbigstep` | **5**, 1 to 1000 | Lines that `++` and `--` move it |
| `shellundosteps` | **10**, 1 to 50 | How many changes of files `undo` can go back through (`insert`, `change`, `lines` ...) |
| `shelldir` | **empty**, a path (`~/notes`), last | The directory the shell starts in: empty, the one Blyant is started in; `last`, the one it was left in |
| `shelllastdir` | a path | With `shelldir=last`, where the shell was left: kept when you leave a shell typed in at a terminal (`exit`, `screen`), never by commands from a file, a pipe or `--run` |
| `shelllastfile` | a path | `$file` when the shell was left, kept as `shelllastdir` is (whatever `shelldir` is): `$file` again in the next shell typed in at a terminal, while the file is there |
