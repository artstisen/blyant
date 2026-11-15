# Guide to the shell
![The shell: a project listed, a file peeked into, a search by text and a register](docs/screenshots/shell.svg)

The shell is Blyant on lines: you type a command at a prompt, and Blyant writes its answer as plain lines of text, in the colors of your terminal. It works in any terminal, with a screen reader, and from a script. Files are edited in the editor of the full screen, and closing the editor brings you back to the prompt.

Start it with `--shell`, and leave it with `exit`:

```
blyant --shell                 The shell, in the directory you are in
blyant --shell ~/projects      The shell, in that directory
```

(On Windows `Blyant.exe --shell`, and `dotnet Blyant.dll --shell` for the portable version.)

New to the shell? The [shell tutorial](Guide-to-shell-tutorial.md) goes through a day of work with it, task by task: getting around, looking into files, copying and moving, adding lines, a todo list, registers and the clipboard. This guide tells every command.

## Starting in the shell
To have Blyant always start as the shell, turn on _Start in the shell_ on the General page of Configuration (or `set startshell true` in the shell). Then `blyant` alone starts as `blyant --shell` does. A file or directory given (`blyant notes.md`, `blyant +12 page.html`) is still opened on the full screen, so programs that open a file in Blyant (as Blyant Validator does) get the editor. `blyant --screen` starts on the full screen once, and `screen` in the shell comes there too.

## Start directory
Without a directory given, the shell starts in the directory you are in, unless the setting `shelldir` tells another:

- `set shelldir ~/projects` (or a full path): the shell starts there. `~` is your home directory
- `set shelldir last`: the shell starts where you left it the last time (kept as `shelllastdir`)
- `set shelldir .`: in the directory you are in, as without the setting

A directory given (`blyant --shell ~/notes`) is used instead, and so is the directory of the page you leave with `:shell` (the start page, the Jump menu and the corkboard have none: then `shelldir`). Commands given with `--run` run in the directory you are in, so scripts do what they say wherever `shelldir` points. A start directory that is not there (moved, on a drive taken out) is told, and the shell starts in the directory you are in. The settings are in the settings file, and not on the pages of Configuration.

## Between the full screen and the shell
You can go from one to the other while Blyant runs:

- `:shell` on any command line of the full screen goes on as the shell, in the directory of the page you leave: from the file browser (and its lists) the directory of the pane, from the editor the directory of the file (which is then the last list: `open 1` opens it again), from the list of projects the directory of the selected project, and else (the start page, the Jump menu, the corkboard) the start directory of `shelldir`, or the directory Blyant was started in
- `screen` in the shell goes on on the full screen, in the file browser of the directory you are in. Closing the file browser shows the start page

Leaving with `:shell` is as leaving for a jump of the Jump menu: the editor asks about changes that are not saved first (save, discard, or stay), and on a page opened on top of the editor (the Jump menu, the corkboard, the list of projects) `:shell` is not done while the editor has such changes. `exit` in the shell leaves Blyant, wherever it was started.

## Prompt and lists
The prompt shows the directory you are in, from `~` in your home directory:

```
blyant ~/projects/novel> ls
/home/anna/projects/novel
   1  chapters/
   2  notes/
   3  outline.md
   4  plot.md
blyant ~/projects/novel> open 4
```

Every list a command shows is numbered, and a number given to a command after it is a row of that list: `open 4` opens `plot.md`, `cd 1` goes to `chapters`. A path works too (`open plot.md`, `cd ../other`), and a path that is not full is taken from the directory you are in. A file or directory that is not there is marked `(missing)`.

The favorites, the history (`hist` and `recent`) and the bookmarks show the names of the files and directories, not their paths, so a list is quick to read. A name that two rows have gets the folder it is in after it (`README.md (novel)`). `info 3` tells the rest of row 3: its full path, size, dates and, for a text, its words and lines. `-l` shows a list with the full paths (`fav -l`, `hist -l`, `recent -l`, `book -l`, and `ls -l`, `find -l`). The hotlist and the projects show paths, as before.

A favorite (of the open list of favorites) is marked ` *` after its name in `ls`, `find`, `hist`, `recent` and `book`, as the full screen marks it in the file browser, the history and the search results: `plot.md *`, `notes/ *`. `fav` has no marks (every row of it is a favorite), and neither has a list with `-l`: its lines are the paths alone, for a script to read. A line found by `find -t` is not marked, only the file found by its name.

The last list is kept for as long as no other command takes its place, however long that is (until Blyant ends: it is not kept for the next time):

- A list shown takes its place: `ls`, `fav`, `hist`, `recent`, `hot`, `book`, `projects` and `find`
- So does the file or directory a command worked on, as a list of one row (`open 1` opens it): `peek`, `insert`, `change`, `shift`, `format` and `trim` (the file, at its line), `lines` (the other file, at the lines put in), `new`, `mkdir`, `copy`, `move` and `rename` (the new place), and `cd` on a file (the file)
- Other commands leave it as it was: `info` (so `fav`, `info 3`, `info 4` and `open 4` work), `cd` into a directory, `up`, `home`, `data`, `pwd`, `open`, `delete`, `clip`, `reg`, `set`, `help`, `about` and `clear`. The rows are full paths, so `fav`, then `cd` somewhere else, and `open 3` still opens row 3 of the favorites
- A row deleted since (`delete 3`) stays in the list, and is told as not there. When a number is not a row of the list you meant (after `peek 2`, the last list is that file), show the list again

The shell writes in the colors of your terminal (the color scheme is for the full screen), and a character of a file that would steer the terminal (an escape sequence: a color, a title, the clipboard) is shown as `?`. A command that fails in a way it should not is told (`The command failed: ...`), and the shell goes on.

## Long lists
In a terminal, an output longer than the window comes in pages: when the window is full, the shell waits, with `-- More: Enter or Space next page, q stop --` on the last row.

- `Enter` or `Space` (or another key) shows the next page
- `q`, `Esc` or `Ctrl+C` stops: the rest is not shown, and the prompt comes back
- A line wider than the window takes the rows it needs, and the size of the window is looked at again for each page

Commands read from a file or a pipe, and `--run`, are never paged, so `blyant --run "find *.md" | less` gets the whole list. A terminal that does not tell its size is not paged either.

To have every output whole (with a screen reader, say), turn the pages off: `set shellpaging false` in the shell, or `shellpaging=false` in the settings file (`settings.ini` in the data directory). The setting is not on the pages of Configuration.

## Commands
| Command | What it does |
|---|---|
| `pwd` | Show the directory you are in |
| `ls [-a] [-l] [dir\|n]` | List the folders, then the files, of a directory (by name; a favorite marked ` *`). Hidden files are left out, as in the file browser (on Linux and macOS the names that start with `.`); `ls -a` lists them too, and `ls -l` the full paths, without marks |
| `cd [dir\|file\|n]` | Go to a directory; `cd` alone goes to your home directory, `cd ~/notes` to one in it. A file (a row of `fav`, `hist`, `find` ...) goes to the directory it is in, and the file is then the last list: `open 1` opens it |
| `up` | Go to the directory above (`cd ..`) |
| `home` | Go to your home directory (as `cd` alone) |
| `data` | Go to the [data directory](Guide-to-installation.md#good-to-know) of Blyant: its settings, lists and log. `cd data` is still a directory named _data_ |
| `open file\|n [+line]` | Edit a file in the editor of the full screen, at a line if one is given (`open plot.md +12`). A directory is gone to |
| `peek file\|n [lines]` | Show the lines of a text file, numbered: the first 20, `peek plot.md 40` the first 40, `peek plot.md 100-120` those, `peek plot.md +100` 20 lines from line 100, and `peek plot.md +100 30` 30 of them. Only read. After a peek from a line, `open 1` opens the file at that line |
| `peek [file\|n] +\|++\|-\|--` | Move the window of the last peek of the file: `+` 1 line further down, `++` 5, `-` and `--` up again (the settings `shellpeekstep` and `shellpeekbigstep`: `set shellpeekstep 2`; 1 to 1000, and `reset` puts back 1 and 5), as many lines as before (`peek plot.md +`, then `peek +`: the file peeked last; `+`, `++`, `-` or `--` alone on the line do the same, also after other commands; nothing is taken after them: `+ 7` is told). The lines shown are `$peek`. From the top for a file not peeked before; at the end the last lines are shown |
| `find [-l] [name] [-t text]` | Find files here and in the directories below, by name and by a text in them (see [Files](#files)); `find -l` gives the full paths |
| `new name` | Make a new, empty file here (`new ideas` makes `ideas.md`: the kind of new files, when no extension is given). It is put in the latest files at once, as a file opened: `recent` lists it, and `Tab` with nothing typed offers it first |
| `mkdir name` | Make a new directory here |
| `rename file\|n name` | Give a file or directory a new name |
| `copy file\|n dir\|n` | Copy a file or directory into a directory |
| `move file\|n dir\|n` | Move a file or directory into a directory |
| `delete file\|n` (`del`) | Delete a file or directory: to the trash, after asking |
| `info [file\|n]` | Tell about a file or directory (alone: the one you are in): its kind, size, dates and path, and for a text file its characters, words, lines, encoding and line endings. The last list stays as it was |
| `insert file\|n top\|bottom\|+line text` | Put a line of text in a file without opening it: before the first line, after the last, or as line 12 (`+12`). See [Text and settings](#text-and-settings) |
| `change file\|n +line\|from-to [text]` | Change a line (`+12`) or lines (`12-15`) of a file into a text, or delete them when no text is given (asks first). See [Text and settings](#text-and-settings) |
| `shift file\|n lines place` | Move lines in a file without opening it: under a heading (`shift todo.md 4-6 Done`), at the end of its section (`Done+`), before a line (`+12`), to the `top` or the `bottom`. See [Moving lines](#moving-lines) |
| `shift! file\|n lines place` | Move lines as `shift` does, and tick their task boxes (`- [ ]` to `- [x]`), or untick those that are ticked |
| `lines file\|n lines > file\|n place [copy\|cut]` | Copy lines to another file without opening them (`lines notes.md 4-6 > plan.md bottom`), or with `cut` move them there. See [Moving lines](#moving-lines) |
| `lines file\|n lines >\|>> $name [copy\|cut]` | Keep lines in a register (`lines notes.md 4-6 > $notes`), or add them to its end with `>>`; with `cut`, also take them out of the file. A register that holds a path is not written over: give the place in that file (`> $plan bottom`). See [Registers](#registers) |
| `lines file\|n lines start\|end text` | Put a text at the start or the end of lines, the rest of them kept (`lines todo.md 4 start Remember this:`). See [Text in lines](#text-in-lines) |
| `lines file\|n lines unstart\|unend text` | Take a text off the start or the end of the lines that have it |
| `lines file\|n lines replace old new` | Replace a text in lines, the rest of them kept (`lines notes.md 5-9 replace draft final`) |
| `format file\|n` | Format an HTML, CSS or XML file, as `:format` in the editor does (see [Text and settings](#text-and-settings)) |
| `trim file\|n` | Remove the blank space at the end of the lines, as `:trim` in the editor does; `trim!` also the line breaks of Markdown |
| `set [name [value]]` | Show the settings (`set`), one of them (`set indent`), or change one (`set indent 2`, also `set indent=2` as in the settings file) |
| `reset` | Put all the settings back to their defaults, as _Reset all settings_ on the System page of Configuration (asks first) |
| `reload` | Read the settings file and the registers again, after you have edited them in another program (see [Text and settings](#text-and-settings)) |
| `reg [name [text\|.\|#n]]` | Keep a text or a path in a register, show one, or list them (see [Registers](#registers)) |
| `reg name = text`, `reg name += text` | Keep a text in a register (as `reg name text`), or add it to its end, as lines (`reg notes += $peek`) |
| `reg -d name\|all` | Take a register away, or all of them |
| `clip [path\|name] file\|dir\|n\|.` | Copy the full path of a file or directory (`clip 3`: row 3 of the last list; `clip .`: the directory you are in), or its name, to the clipboard (see [Clipboard](#clipboard)) |
| `clip text file\|n` | Copy the text of a file to the clipboard |
| `clip lines file\|n lines` | Copy lines of a file to the clipboard, given as for `lines` (`40`, `4-6`, `4,7,9-10`, `+100 30`, a text in the line). See [Line numbers](#line-numbers) |
| `clip $name` | Copy the value of a register to the clipboard |
| `$clip` | The text `clip` copied last, put in a command as a register is (`insert plan.md bottom $clip`: its lines) |
| `favorites [-l] [list]` (`fav`) | List the favorites of the file browser by name, with their folders as headings (`-l`: the full paths); `fav work` uses the list _work_ from now on, as `:list work` does, and `fav main` the main list again. The open list is told first (`List: main`) |
| `favorites lists` | List the lists of favorites, `*` at the one that is open |
| `favorites new\|delete name` | Make a list and open it (`fav new reading`), or delete one: asked first, to the trash (the files in it stay where they are; the main list is open after the open one is deleted) |
| `favorites rename name new` | Give a list a new name (`fav rename work writing`). The main list keeps its name |
| `favorites use name` | Open a list whose name is a word of `fav` (`fav use new` for a list named _new_) |
| `favorites folder name` | Make a folder of favorites, last in the list (`fav folder Drafts`). See [Folders of favorites](#folders-of-favorites) |
| `favorites subfolder [folder] name` | Make a subfolder, first in a folder (`fav subfolder [Writing] Drafts`) |
| `favorites rename [folder] name` | Give a folder a new name (`fav rename [Drafts] Old drafts`) |
| `favorites remove [folder]` | Remove a folder with its favorites and subfolders, after asking (the files stay) |
| `favorites move rows\|[folder] where` | Move favorites or a folder: `top`, `bottom`, `up`, `down`, `out`, to the place of a row (`5`), or into a `[folder]` |
| `favorites undo` | Put back what `fav remove` removed last |
| `history [-l]` (`hist`) | List the files opened by name, the newest first, with when (`-l`: the full paths) |
| `recent [-l]` | List the latest files (the first nine of the history), by name (`-l`: the full paths) |
| `hotlist` (`hot`) | List the directory hotlist |
| `fav add\|remove file\|n` | Add a file or directory to the favorites in use, or take it off them |
| `hot add\|remove [dir\|n]` | Add a directory to the hotlist (`hot add` alone: the one you are in), or take it off |
| `bookmarks [-l]` (`book`) | List the bookmarks of the Jump menu, by name (`-l`: the full paths; a page of Blyant is shown, but opened on the full screen only) |
| `projects` (`proj`) | List the projects |
| `project n\|name` | Go to the directory of a project (`project 2`, `project novel`) |
| `log [n]` | Show the last lines of the [log](Guide-to-start-page.md#log) (20, or as many as you give) |
| `clear` (`cls`) | Clear the screen: the prompt is at the top again |
| `screen` | Go on on the full screen, in the file browser of the directory you are in (see [Between the full screen and the shell](#between-the-full-screen-and-the-shell)) |
| `version` | Show the version of Blyant |
| `about [license]` | Show the version of Blyant and the commands used the most (`help`, `ls`, `open`, `peek`, `set language de` ...), and `about license` its license, as on the second page of [About](Guide-to-start-page.md#help) |
| `again [n]` | List the commands you typed, the newest first, or run one again by its number (`again 3`; also `!3`, and `!!` for the last). See [Typing a command](#typing-a-command) |
| `run $name\|file` | Run the commands of a register or of a file, one a line, each written out first; it stops at the first that fails. See [Running commands](#running-commands) |
| `help [command]` (`h`) | List the commands, or tell what one does |
| `exit` (`quit`, `q`) | Leave Blyant |

### Typing a command
In a terminal the line of the shell is read key by key:

- `Tab` completes the word you are typing, and `Tab` again gives the next that fits (after the last: what you typed; `Shift+Tab` goes back): the name of a command (`fa` to `favorites`), the words that can follow some of them (the names of the settings after `set`, the lists after `fav`, the projects after `project`, `add` and `remove` after `fav` and `hot`, `top` and `bottom` after `insert`, `top`, `bottom` and the headings of the file after the lines of `shift` and after the other file of `lines`, `path`, `name`, `text` and `lines` after `clip`), and the names of files and directories after the commands that take a path (`open pl` to `open plot.md`). `cd` and `ls`, and the directory of `copy` and `move`, take directories only. A directory gets `/` after it, and when it is the only name that fits, `Tab` again goes on into it (`cd pr` to `cd projects/`, then `cd projects/Article/`). A name with a blank in it is put in quotes, and hidden names come only after a `.`
- With nothing typed after a command that takes a file (`open ` and `Tab`), the files you opened last that are in that directory come first, the last opened first: `Tab`, `Tab` goes between the files you are working on. The other names follow by name. It works after a directory too (`open drafts/` and `Tab`), and for a directory (`cd ` and `Tab`) the directories that have those files come first. Once you have typed a letter, the names come by name. The files opened last are those of `recent` and `hist`. To have all names by name, turn the setting `shellrecentfirst` off (`set shellrecentfirst false`, or `shellrecentfirst=false` in the settings file; not on the pages of Configuration)
- A name with `*` (any characters) or `?` (one character) in it is a pattern: `open *151*` and `Tab` gives `open Blyant-v151.md`, and `Tab` again the next name it fits (letter case alike). It works in the name, also after a directory (`open drafts/*151*`), but not in the directories before it. A hidden name only for a pattern that starts with `.`. Without `Tab`, `*` and `?` are taken as they are: `open *151*` looks for a file of that name
- `Up` and `Down` bring back the commands you typed before, on Windows, Linux and macOS alike; what you have typed is what they start with (`pe` and `Up`: the last `peek`)
- `again` lists the commands you typed, numbered, the newest first, and `again 3` (or `!3`) runs number 3 again, as it was typed: it is written out, then run. `!!` runs the last one. `!3` and `Tab` puts the command on the line instead, to be changed before `Enter` (another line number, another file). A command run again becomes the newest, number 1, and `again` itself is not kept. The last 50 are kept while Blyant runs, never in a file (a command can hold text of a file); commands from a file, a pipe or `--run` are not kept. The list of `again` does not take the place of the last list: `open 3` is still a row of the list shown before it
- `Left`, `Right`, `Home` and `End` move on the line, `Backspace` and `Delete` delete, `Ctrl+U` deletes the line before the cursor, `Ctrl+W` the word before it, and `Esc` the whole line. A line wider than the window is scrolled sideways on its row
- `Ctrl+C` drops what you have typed, and `Ctrl+D` (or `Ctrl+Z` on Windows) on an empty line leaves, as `exit` does

With a screen reader the line may be read best as the terminal reads it: turn the setting `shellcompletion` off (`set shellcompletion false`, at once, or `shellcompletion=false` in the settings file; not on the pages of Configuration). The line is then read whole after `Enter`, without `Tab` and with the history your terminal gives. Commands from a file, a pipe or `--run` are always read that way.

## Line numbers
The commands that work on the lines of a file take line numbers in a few ways. A plain number is a line to work on; a `+` before it is a place in the file, or the first line of a stretch counted from it:

| Written | Means | Taken by |
|---|---|---|
| `12` | line 12 | `change`, `shift`, `lines`, `clip lines` |
| `4-6` | lines 4 to 6 | `change`, `shift`, `lines`, `clip lines`, `peek` |
| `4,7,9-10` | those lines, in their order | `shift`, `lines`, `clip lines` |
| `"Buy milk"` | the one line with that text in it | `shift`, `lines`, `clip lines` |
| `+100 20` | 20 lines from line 100 | `peek`, `lines`, `clip lines` |
| `+`, `++`, `-`, `--` | the window of the last peek moved: 1 or 5 lines down, or up (as set) | `peek`, and alone on the line (`+` is `peek +`) |
| `+12` as a place | at line 12, before the line that is there | `insert`, `shift`, `lines` (after the `>`), and `open` (the cursor there) |

- The `+` marks a place where the word could also be something else: `top`, `bottom` or a heading (a heading can be named _12_), and in `open notes.md +12` the line to go to, as in other editors (`vim +12 notes.md`)
- `peek` alone takes a plain number as how many: `peek notes.md 40` shows the first 40 lines, as it only shows them. Everywhere else `40` is line 40
- `change notes.md +12` is line 12 too: for `change` the `+` can be left out
- A range past the end stops at the last line, and the numbers are those of the file as it is, as `peek` shows them

## Files
The shell works on files with the rules of the file browser:

- A name with blanks in it is written in double quotes: `rename "old notes.md" notes.md`
- When you rename, move or delete the directory you are in (`rename . drafts`, `delete ..`), the shell goes with it, or to the directory above it
- `fav remove` and `hot remove` also take off a file or directory that is gone
- `copy` and `move` never overwrite a file or directory of the same name: it is told, and nothing is done. A copy made in the directory it is in gets a new name (`plot-copy.md`)
- `delete` asks first, telling what goes with a directory, and you type the answer in full (`Yes`) to go ahead. It goes to the trash, which you open from the file browser with `:trash`, unless _Delete to trash_ is turned off on the General page of Configuration. Something that cannot be moved to the trash is deleted for good only if you say so
- The files and directories Blyant uses (its data directory, the settings, the favorites, the history) cannot be renamed, moved or deleted from the shell
- What is renamed or moved is followed by the favorites, the history, the hotlist, the projects and the bookmarks, as in the file browser, and what is deleted leaves the favorites
- `find` searches the directory you are in and the directories below it, as the search of the file browser does: hidden files are left out, and at most 5000 files are found. `find plot` finds the names with _plot_ in them, `find *.md` the names that match (`*` and `?`), `find -t storm` the text files with _storm_ in a line (with the line), and `find *.md -t storm` both. The text takes `*` and `?` too: `find -t storm*night` finds a line with _storm_ and then _night_ in it, `find -t st?rm` _storm_ and _starm_ (a `*` in the text you look for, as `**` of bold, is taken so too, and finds more lines). `open` on a file found by its text opens it at that line
- After `new`, `mkdir`, `rename`, `copy` and `move`, the new place is the last list: `open 1` opens it

## Text and settings
`format` and `trim` change a file without opening it: they are the commands `:format` and `:trim` of the editor, run on the text of the file, so they do just what they do there (see the [guide to the editor](Guide-to-editor.md#other-commands)). The file is saved only when its text changed, with its line endings and its byte order mark as they were, and it is told with the path of the file: `/home/anna/site/page.html: Formatted`, `/home/anna/poems/poem.md: Lines trimmed: 3`, or `Already formatted` when nothing changed. A file that cannot be formatted safely is left as it is, and why is told.

- Only files in UTF-8 are changed: a file in another encoding (Windows-1252, say) is left as it is. Open it in the editor, which saves it in UTF-8
- A file with more than one kind of line endings (some lines CRLF, some LF) is left as it is too: saved, every line would get one kind. The editor tells it, and saves it with one. This goes for `insert`, `change`, `shift` and `lines` too, and so does leaving the files Blyant uses
- There is no undo: a file changed by `format` or `trim` is saved at once. Use them on files you can get back (a copy, a project kept in Git)

`insert` puts a line of text in a file, as quick notes are written down: `insert todo.md bottom Buy milk`, `insert notes.md top # Monday`, `insert 3 +12 A new line` (a row of the last list). The text is all that follows the place, as you type it (blanks and quotes too), and it is one line; `$clip` of lines copied puts in its lines. `+12` makes it line 12, before the line that was there, and a line past the end puts it after the last. The file keeps its line endings and its byte order mark, and the file is then the last list, at the line put in: `open 1` opens it there. As for `format`, only a file in UTF-8 is changed, the files Blyant uses are not, and there is no undo.

`change` changes lines of a file into a text, without opening it: `change notes.md +12 A new line` (line 12), `change notes.md 12-15 One line instead` (lines 12 to 15 become one line). The text is all that follows the lines, as you type it, and it is one line (`$clip` of lines copied: its lines). Without a text, the lines are deleted: `change notes.md 12-15` shows them and asks first (No is selected, as there is no undo), and from a file or with `--run` they are only deleted with `--yes`. A range past the end stops at the last line. As for `insert`, the file keeps its line endings and byte order mark, only a file in UTF-8 is changed and not the files Blyant uses, and the file is then the last list at the place changed (`open 1`). (The `:replace` of the editor finds a text and puts another in its place; `change` works on whole lines.)

`set` shows the settings as they are in the settings file (`settings.ini` in the data directory), and changes one as the pages of Configuration do: `set indent 2`, `set wrap false`, `set colorscheme Deep Blue`. The setting is read again at once, as Blyant reads it when it starts, and `set shelldir ""` empties a setting. A value the setting cannot have is told, and the setting stays as it was: `indent cannot be 3: it stays 4`. A settings file written by an earlier version of Blyant lacks the settings that came after it: `set` writes the file whole first (as changing a setting in Configuration does), so every setting of this version can be shown and changed.

`reset` puts all the settings back to their defaults, as _Reset all settings_ on the System page of Configuration does: your lists, favorites, bookmarks and files are kept. It asks first, and only `y` (or `Yes`) goes ahead: `Enter` alone answers no. The settings of the shell go back too (`shellpaging` on, `shelldir` empty), and so does the language (English). From a file or with `--run`, nothing is reset unless `--yes` is given. The names and values are those of the settings file, and the guide to [Configuration](Guide-to-start-page.md#configuration) tells what each setting does.

`reload` reads the settings file again after you have edited it in another program (saved in the editor of Blyant, `open settings.ini` from `data`, it is used at once), and uses it as Blyant does when it starts: `Settings and registers read again`. A value a setting cannot have is told (`shellpaging cannot be often: it is true`), and the setting has its default, as when Blyant starts. The language changes at once; the color scheme and the other settings of the full screen are used the next time it is shown. The directory you are in, the last list and the commands typed before stay as they were. `reload` also reads the registers again.

## Moving lines
`shift` moves lines to another place in the same file, without opening it, as items are moved from _Todo_ to _Done_:

```
peek todo.md                       the lines, numbered
shift todo.md 5 Done               line 5 under the heading Done
shift todo.md 5-7 Done             lines 5 to 7
shift todo.md 5,8,10-11 Done       lines that are not together, in their order
shift todo.md "Buy milk" Done      the line with "Buy milk" in it
shift todo.md 5 Done+              at the end of the section of Done
shift todo.md 12 +3                before line 3
shift todo.md 12 top               to the top (or bottom)
shift! todo.md "Buy milk" Done     moved, and its task box ticked
```

- The numbers are those of the file as it is, as `peek` shows them
- A place that is not `top`, `bottom` or `+12` is a heading: `Done` is `## Done` (any level, and letter case alike; `"## Done"` and `"Done today"` in quotes also do). The lines go right under it, after a blank line under it, so the newest come first; `Done+` puts them after the last line of its section, before the next heading of its level or above. In a Typst file (`.typ`) the headings are `= Done`. Headings in a block of code are not headings
- A text in place of the lines finds the one line that has it (letter case alike). When more lines have it, or none does, they are shown with their numbers and nothing is moved, and so it is when no heading has the name, or two do (then the headings are shown)
- `shift!` also ticks the task boxes of the lines moved (`- [ ] Milk` to `- [x] Milk`, also in a numbered list), and unticks those that are ticked: `shift! todo.md Milk Done` when it is done, `shift! todo.md Milk Todo` to have it back. A line without a box is moved as it is
- The place cannot be in the lines moved (the heading moved, or `+5` with line 5 moved). Lines already at the place are left so
- The lines moved are shown at their new place, and the file is then the last list there (`open 1`). As for `insert`, the file keeps its line endings and its byte order mark, only a file in UTF-8 is changed and not the files Blyant uses, and there is no undo; nothing is lost by a move, so it is not asked first

`lines` copies lines to another file, or moves them there with `cut` last:

```
lines notes.md 4-6 > plan.md bottom          lines 4 to 6 copied to the end of plan.md
lines notes.md +10 5 > plan.md top           5 lines from line 10 (as peek takes them)
lines todo.md "Buy milk" > done.md Done+ cut the line with "Buy milk" moved to the end of Done
```

- The lines are given as for `shift` (`4`, `4-6`, `4,7,9-10`, a text), or as `+10 5`: 5 lines from line 10. After the `>` come the other file and the place in it, as for `shift`: `top`, `bottom`, `+12`, a heading, or `Heading+`
- `copy` is the default, and can be written too. `cut` takes the lines out of their file, after they are in the other: when something goes wrong, they are in both files, never in neither. The first file is not changed when it was changed since it was read (the lines are then copied, and it is told). A heading named _Cut_ is written in quotes (`"Cut"`), as the place
- The lines put in are shown, and the other file is then the last list there (`open 1`). Both files keep their line endings and byte order mark, only files in UTF-8 are changed and not the files Blyant uses, and nothing is asked: no line is lost. The same file on both sides is for `shift`
- `Tab` completes the other file after the `>`, then the headings of that file, then `copy` and `cut`
- From the command line of your system, give the command in quotes: `blyant --run "lines notes.md 4-6 > plan.md bottom"`. Without them, bash, PowerShell and cmd take the `>` as a redirection to a file

## Text in lines
`lines` also changes the text in lines, keeping what is there: a text put at their start or end, taken off it again, or a text in them replaced. The word after the lines tells what is done:

```
lines todo.md 4 start Remember this:         "Remember this: This is some task"
lines todo.md 4 end (by Friday)              "This is some task (by Friday)"
lines todo.md 5-9 start "- [ ] "             lines 5 to 9 made task items
lines code.md 12 end ";"                     a ";" at the end, with no blank before it
lines todo.md 4 unstart Remember this:       taken off again
lines todo.md 5-9 unstart "- [ ] "           the task boxes taken off
lines notes.md 5-9 replace draft final       "draft" in lines 5 to 9 made "final"
lines notes.md 5-9 replace " (draft)" ""     " (draft)" taken out of them
```

- The lines are given as for `shift` and the other forms of `lines`: `4`, `4-6`, `4,7,9-10`, `+10 5` (5 lines from line 10), or a text that finds one line (`lines todo.md "some task" start Note:`). Every line of them is changed
- `start` and `end` put the text with a blank between it and the line. In quotes it is put as it is: no blank, or the blanks in the quotes (`"- [ ] "`). A blank line is left as it is
- `unstart` and `unend` take the text off the lines that have it at their start or end, with the blank `start` and `end` put there (in quotes: the text only), so they undo `start` and `end`. The other lines are left as they are
- `replace old new` replaces the text everywhere in the lines, with the letter case as typed (`draft`, not `Draft`). A text with blanks is written in quotes, and `""` is no text: what is replaced is taken out
- After the lines, `Tab` completes `start`, `end`, `unstart`, `unend`, `replace` and `>`. A `>` or one of these words later on is part of the text: `lines notes.md 3 start ">"` makes line 3 a quote
- `$name` and `$clip` put in their value: `lines todo.md 4 end $note`
- The lines changed are shown, and the file is then the last list, at the first of them (`open 1`). When no line changes, nothing is written, and it is told. As for `insert`, the file keeps its line endings and byte order mark, only a file in UTF-8 is changed and not the files Blyant uses, and there is no undo; nothing is asked, as no line is lost

## Folders of favorites
The favorites can be kept in folders, as on the favorites page of the full screen: a folder holds favorites and subfolders, and a subfolder favorites (two levels). A folder is only in the list of favorites: nothing is made on disk, and no file is moved. `fav` shows the folders in brackets, with what is in them below them:

```
blyant ~> fav
List: main
  1  plot.md
[Writing]
  [Drafts]
      2  chapter-1.md
      3  chapter-2.md
    4  notes.md
```

The rows of a folder are shown further in than those of no folder, and the rows of a subfolder further still: here `chapter-1.md` and `chapter-2.md` are in Drafts, and `notes.md` is in Writing.

A folder is named as `fav` shows it, in brackets: `[Writing]`, and a subfolder with its folder, `[Writing/Drafts]` (or `[Drafts]` when no other folder has that name). A favorite is named by its row, as `fav` numbers it: the folders are not rows.

```
fav folder Research                  a folder, last in the list
fav subfolder [Writing] Drafts       a subfolder, first in Writing
fav rename [Research] Sources        a folder renamed
fav move 1 [Writing]                 row 1 into Writing, last in it
fav move 2,3 [Writing/Drafts]        rows 2 and 3 into the subfolder
fav move 4 top                       to the top of its folder (or of the list)
fav move 4 bottom                    to the end
fav move 4 up                        one step up (down: one step down)
fav move 4 2                         to the place of row 2, in its folder
fav move 2 out                       out of its folder, last in the list
fav move [Sources] top               a folder, with what is in it
fav move [Sources] [Writing]         a folder into another, as a subfolder
fav move [Writing/Sources] out       a subfolder out of its folder
fav remove [Sources]                 a folder with what is in it (asked first)
fav undo                             what fav remove removed last, put back
```

- The rows are given as in [Line numbers](#line-numbers): `3`, `3-5`, `3,7`. `up` and `down` move one row, or one folder, a step at a time, as `V` with `j` and `k` does on the full screen: a favorite moved onto a folder goes into it, and one moved past the first or last favorite of its folder leaves it
- A folder with subfolders does not go into another folder, as there are two levels at most, and a subfolder goes into a folder at the top. What cannot be done is told, and nothing is moved
- After each change the list is shown again with its new rows, and it is the last list: `open 2` opens row 2 of it. A folder name with blanks is written in quotes (`"[My drafts]"`), and `Tab` completes the names of the folders after `[`, and the places after `fav move 3`
- `fav remove [Sources]` asks first, and tells how many favorites and subfolders go with the folder; the files stay where they are. `fav undo` puts back what `fav remove` (a folder, or a favorite: `fav remove 3`) removed last in this shell, as long as the list is as it left it
- They work on the open list of favorites, and each change is saved at once, so the full screen shows the same folders. `fav undo` puts back in the list it removed from: after another list was opened, it tells which one to open first
- A list of favorites named as one of these words (`folder`, `subfolder`, `move`, `undo` ...) is opened with `fav use folder`, as for `new` (see [Commands](#commands))
- A file whose name starts with a bracket (`[2024].md`) is a file, not a folder, after `fav add` and `fav remove`: a folder is a name in brackets alone

## Registers
A register keeps a text or a path under a name, and `$name` puts it in any command:

```
reg docs ~/projects/novel/docs      a path (or a text: all after the name, as typed)
reg here .                          the directory you are in
reg plot #3                         the full path of row 3 of the last list (fav, hist, find, ls ...)
reg note Buy more ink
copy plot.md $docs                  $docs is ~/projects/novel/docs
cd $here
insert todo.md bottom $note
```

A register can also hold lines, collected from several places of a file, or from several files:

```
reg notes = First idea              set (the same as "reg notes First idea")
peek plot.md 12-14
reg notes += $peek                  the lines peek showed, added at the end
lines chapter-2.md 40-42 >> $notes  lines of a file added at the end
lines todo.md 7 >> $notes cut       ... and taken out of todo.md
reg notes += Remember the attic     a line added
insert ideas.md bottom $notes       all of them put in a file
lines plot.md 3-5 > $notes          ">": the register is those lines alone
```

- `+=` and `>>` add to the end of a register, as lines (a register that is not there is made); `=`, `>` and `reg name text` set it. `$clip`, `$peek` and the other registers can be added (`reg notes += $clip`)
- A register of more than one line is put in as `$clip` and `$peek` are: `insert` and `change` put in its lines, and the other commands are told. `reg` lists its first line and how many lines it has, and `reg notes` shows all of them
- In `registers.txt` the lines after the first start with a tab, so the file can still be edited by hand. The lines you collect are kept there until you take the register away (`reg -d notes`): the text of your files is in that file, in the data directory (never in the log)

- A text in quotes is kept without them: `reg docs "~/my docs"` keeps the path, so `copy plot.md $docs` takes it as one
- `reg` lists the registers, `reg docs` shows the value of one (alone on its line, as a script reads it with `blyant --run "reg docs"`), `reg -d docs` takes it away, and `reg -d all` all of them
- A name starts with a letter, and has letters, digits and `_` (`docs`, `draft_2`); letter case is alike
- `$name` is put in only for a register that is there: anything else stays as typed, so `insert prices.md bottom Ink costs $5` keeps its `$5`. `$$` is a `$`
- A path with a blank in it is put in quotes where a path is taken (`copy plot.md $docs` with _my docs_), and the text of `insert`, `change`, `reg` and `set` gets the value as it is
- `$clip` is the text `clip` copied last (see [Clipboard](#clipboard)): it is put in as a register is, but it is not one to set, and it is not kept in the file
- Some names are set by Blyant, and put in as a register is:

  | Name | Value |
  |---|---|
  | `$pwd` | the directory you are in (`copy 3 $pwd`) |
  | `$file` | the file the last command worked on: opened, peeked, put lines in, made with `new` ... (`peek plot.md`, then `insert $file bottom Done`). A list shown (`ls`, `fav`) does not change it |
  | `$date` | today, as `2026-11-15` |
  | `$time` | the time now, as `14:05` |
  | `$now` | the date and the time, as `2026-11-15 14:05:33` (as `:ts` in the editor) |
  | `$clip` | the text `clip` copied last |
  | `$peek` | the lines `peek` showed last, as they were then: `peek notes.md 4-6`, then `insert plan.md bottom $peek` puts them in another file (`+` and `-` change it, as they move the window) |

  `insert log.md bottom $date Finished chapter 3` puts in a line with the date. The date and the time are written the same in every language. These names are not registers: `reg pwd x` and `reg -d date` are refused, and a line of one of them in `registers.txt` is kept there as a line that is not a register, and not used. `reg` lists them after the registers, under _Set by Blyant_, with their values now, and `reg pwd` shows one. `$file` before a command has worked on a file is told (no file yet), and `$$date` is the text `$date`
- In which commands they can be used: every register and name set by Blyant is put in before the command runs, so all commands take them. Only a value of more than one line (`$clip` or `$peek` of lines) is limited: `insert` and `change` put in its lines, and the other commands are told (nothing is changed):

  | | One line (`$pwd`, `$file`, `$date`, `$time`, `$now`, a register, and `$clip` or `$peek` of one line) | More lines (`$clip`, `$peek`) |
  |---|---|---|
  | `insert file top\|bottom\|+N $peek` | yes | yes, as lines |
  | `change file 3-5 $peek` | yes | yes: lines 3 to 5 become those lines |
  | `lines file 4 start\|end $date` | yes (`lines todo.md 4 end (done $date)`) | no: start and end add to one line |
  | `lines file 4-6 replace old $peek` | yes | no |
  | `lines $file 4-6 > other.md bottom`, `shift $file 4-6 Done` | yes: the file names and places | |
  | `find -t $peek` | yes: that text is looked for | no |
  | `reg note $peek`, `reg notes += $peek` | yes | yes: a register keeps lines |
  | `clip $peek` | yes | yes: all of it |

  An empty `$clip` or `$peek` is not taken by `change` (no text deletes the lines), and `insert` puts it in as a blank line. A value is put in when the command line starts: after `peek a.md 2-3`, `insert $file bottom $peek` puts the lines back into `a.md`; to put them in another file, give its name (`insert b.md bottom $peek`)
- `Tab` completes the names of the registers after a `$` (and the names set by Blyant that have a value: `$clip` once something is copied, `$file` once a file is worked on), and after `reg`
- The registers are kept in the data directory (`registers.txt`), so they are there the next time. The file can be edited in another program: a line is the name, a tab, and the value. A file changed since the shell read it is read again before the next command, and `reg` tells of lines that are not registers (no tab, or a name a register cannot have): they are kept in the file, at its end, so you can put them right. They are the shell's own: the registers of the editor are not these. `reset` does not take them away (they are not settings), and the log has their names, never their values

## Clipboard
`clip` copies to the clipboard what you would otherwise select and copy by hand:

```
recent
clip 3                      the full path of row 3 (also: clip path 3)
clip name 3                 its name only (plot.md)
clip .                      the directory you are in
clip text todo.md           the whole text of a file
clip lines notes.md +40 10  10 lines from line 40 (as lines takes them: 40, 4-6, 4,7, +40 10)
clip $note                  the value of a register, as it is
```

What `clip` copied last is `$clip`, which puts it in a command, as a register does:

```
clip lines notes.md 4-6     three lines
insert plan.md bottom $clip they go at the end of plan.md
clip name 3
insert todo.md top Read $clip
```

- It goes where the [clipboard settings](Guide-to-editor.md#system-clipboard-and-paste-bin) of the editor send a yank: to the clipboard of the system through the terminal (OSC 52, also over SSH) and/or to the paste bin file. Text too long for the terminal goes only to the paste bin, if it is on. What was copied is told, and the path, or how much
- It also goes to the string memory of the editor: after `open` (or `screen`), `p` in the editor puts it in, and `:reglist` lists it. That works also where the terminal has no clipboard (the console of Linux, the Terminal of macOS), or with the clipboard turned off
- `text` and `lines` take text files only. Lines copied are joined by line breaks, without one after the last
- `$clip` is kept in Blyant, not taken from the clipboard of the system: text copied in another program is pasted on the line with the keys of the terminal (`Ctrl+Shift+V`). It is there until Blyant ends, and the next `clip` replaces it. Before the first `clip`, a command with `$clip` is told that nothing is copied yet. Lines copied are put in as lines by `insert` and `change` (`change plan.md 3-5 $clip`), blank lines and blanks at their end too, and the text of a file (`clip text`) as its lines; other commands take one line, and are told when `$clip` has more. An empty `$clip` (the text of a file of one blank line) is not taken by `change`, where no text deletes the lines. `reg` lists it after the registers, with its first line and how many lines it has, and `reg clip` shows it all. `$clip` is not a register: `reg clip text` and `reg -d clip` are refused, and a line `clip` in `registers.txt` is kept there as a line that is not a register
- From a file, a pipe or with `--run` and the output going to a file or a program, the clipboard sequence would end up in that output: then only the paste bin is used, if it is on, and `$clip`, so a script can copy lines and put them in another file
- The log tells only that something was copied, and what kind (`clip: path`), never the text

## Editing a file
`open` shows the editor of the full screen, with all its keys and commands (see the [guide to the editor](Guide-to-editor.md)). When you close it (`:q`, `:wq`), you are back at the prompt, in the directory you were in. A file that is not there yet is made if you want that: the shell asks first.

From the editor:

- `:exit` (or `:qa`) leaves Blyant altogether, asking about changes that are not saved first
- A jump to another place (the Jump menu, `:favorites`, `:browse` ...) closes the editor, asking about changes first, and brings you back to the prompt: the place is not shown. Open it from the shell instead

## Questions
A question of Blyant is asked on a line, with its answers after it: `Y Yes*  N No:`. Type the letter or the number of an answer, or the answer itself; `Enter` alone takes the one with the star. Deleting asks you to type the answer in full, so that a key typed by mistake cannot answer it.

## Commands from a file
The shell also reads its commands from a file or a pipe, one on each line, and stops at the end of them:

```
blyant --shell ~/projects/novel < commands.txt
printf 'ls\nhist\n' | blyant --shell
```

- No prompt is shown, and what went wrong is written to the error output, so a script can tell the two apart. The exit code is 1 when a command failed, else 0
- Questions are not asked: the answer is no, so nothing is done that would ask first, and the command counts as failed. With `--yes` the answer is yes, also to deleting (to the trash): `blyant --shell --yes < commands.txt`. The question, and the answer it got, are written to the error output
- `open` does not open the editor (it needs a terminal), and a file that is not there is not made
- The commands run in the directory Blyant is started in (or the one given), not in the start directory of the setting `shelldir`, and where they end is not kept for `shelldir=last`
- A file of commands saved with a byte order mark (as Notepad and PowerShell can save it) is read as one without
- A line starting with `#` is a comment, and is left out. Inside the shell, `run file` runs such a file too (see [Running commands](#running-commands))

`--run` gives the commands on the command line instead, one after each `--run`, and Blyant ends when they are done: what they show, their questions and the exit code are as for commands read from a file.

```
blyant --run "format page.html"
blyant --run "trim notes.md" --run "format site.css" ~/projects/site
```

A directory given with them is where they run (the one you are in, if none is given).

## Running commands
`run` runs the commands of a register, or of a file, one after another, as if you typed them: a small script you keep and run again.

```
reg tidy = trim notes.md                        a register of commands, one a line
reg tidy += format page.html
reg tidy += insert log.md bottom $$date Tidied
run $tidy                                       runs the three, in order
run weekly.txt                                  the commands of a file
```

- Each command is written out before it runs, as `again` does, so you see what is done. `run` stops at the first command that fails, and tells at which line: the commands after it are not run
- Questions are asked as for any command: `delete` and `change` without a text ask first (from a file or `--run`, the answer is no unless `--yes` is given)
- A `$name` in a command is put in when that command runs. When you set the register, a `$name` is put in at once: write `$$date` to keep `$date` in the register, so that it is the date of each run (`reg tidy` shows what the register holds)
- A line starting with `#` is a comment, and blank lines are left out, in a register, in a file for `run`, and in a file of commands read by `blyant --shell < file`
- A file for `run` is a text file of commands, as for `blyant --shell < file`: one a line. A register or file can run another (`run $more` in it); one that runs itself is stopped, 10 runs deep
- `run file` takes a path as other commands do: `run weekly.txt`, `run $pwd/weekly.txt`, `run $file` (the file the last command worked on) or a register that holds a path (`run $docs/weekly.txt`). `run $tidy` runs the lines of the register `tidy` itself
- `exit` in a run ends the run, not Blyant, and `screen`, `again`, `!3` and `!!` are not run in a run (`!!` would run the run again): they are told, and the run stops there
- `again` keeps `run $tidy`, not the commands it ran, so `!!` runs them all again. From the command line of your system: `blyant --run "run $tidy"` (in quotes, and in bash `\$tidy` or single quotes, as `$` is the shell's own there)

## What stays on the full screen
The corkboard, the Jump menu, the color schemes and logos, Configuration, split views and Zen mode are not in the shell: start Blyant without `--shell` to use them. Your settings, lists and bookmarks are the same in both, as they are kept in the data directory (see [Good to know](Guide-to-installation.md#good-to-know)).
