# Guide to the shell
The shell is Blyant on lines: you type a command at a prompt, and Blyant writes its answer as lines of text, with no colors and no cursor moved about. It works in any terminal, with a screen reader, and from a script. Files are edited in the editor of the full screen, and closing the editor brings you back to the prompt.

Start it with `--shell`, and leave it with `exit`:

```
blyant --shell                 The shell, in the directory you are in
blyant --shell ~/projects      The shell, in that directory
```

(On Windows `Blyant.exe --shell`, and `dotnet Blyant.dll --shell` for the portable version.)

### Starting in the shell
To have Blyant always start as the shell, turn on _Start in the shell_ on the General page of Configuration (or `set startshell true` in the shell). Then `blyant` alone starts as `blyant --shell` does. A file or directory given (`blyant notes.md`, `blyant +12 page.html`) is still opened on the full screen, so programs that open a file in Blyant (as Blyant Validator does) get the editor. `blyant --screen` starts on the full screen once, and `screen` in the shell comes there too.

### Start directory
Without a directory given, the shell starts in the directory you are in, unless the setting `shelldir` tells another:

- `set shelldir ~/projects` (or a full path): the shell starts there. `~` is your home directory
- `set shelldir last`: the shell starts where you left it the last time (kept as `shelllastdir`)
- `set shelldir .`: in the directory you are in, as without the setting

A directory given (`blyant --shell ~/notes`) is used instead, and so is the directory of the page you leave with `:shell` (the start page has none: then `shelldir`). Commands given with `--run` run in the directory you are in, so scripts do what they say wherever `shelldir` points. A start directory that is not there (moved, on a drive taken out) is told, and the shell starts in the directory you are in. The settings are in the settings file, and not on the pages of Configuration.

## Between the full screen and the shell
You can go from one to the other while Blyant runs:

- `:shell` on any command line of the full screen goes on as the shell, in the directory of the page you leave: from the file browser (and its lists) the directory of the pane, from the editor the directory of the file (which is then the last list: `open 1` opens it again), from the list of projects the directory of the selected project, and else the directory Blyant was started in
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

Every list a command shows is numbered, and a number given to the next command is a row of that list: `open 4` opens `plot.md`, `cd 1` goes to `chapters`. A path works too (`open plot.md`, `cd ../other`), and a path that is not full is taken from the directory you are in. A file or directory that is not there is marked `(missing)`.

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
| `ls [dir\|n]` | List the folders, then the files, of a directory (by name) |
| `cd [dir\|file\|n]` | Go to a directory; `cd` alone goes to your home directory, `cd ~/notes` to one in it. A file (a row of `fav`, `hist`, `find` ...) goes to the directory it is in, and the file is then the last list: `open 1` opens it |
| `up` | Go to the directory above (`cd ..`) |
| `home` | Go to your home directory (as `cd` alone) |
| `data` | Go to the [data directory](Guide-to-installation.md#good-to-know) of Blyant: its settings, lists and log. `cd data` is still a directory named _data_ |
| `open file\|n [+line]` | Edit a file in the editor of the full screen, at a line if one is given (`open plot.md +12`). A directory is gone to |
| `peek file\|n [lines]` | Show the lines of a text file, numbered: the first 20, `peek plot.md 40` the first 40, `peek plot.md 100-120` those, `peek plot.md +100` 20 lines from line 100, and `peek plot.md +100 30` 30 of them. Only read. After a peek from a line, `open 1` opens the file at that line |
| `find [name] [-t text]` | Find files here and in the directories below, by name and by a text in them (see [Files](#files)) |
| `new name` | Make a new, empty file here (`new ideas` makes `ideas.md`: the kind of new files, when no extension is given) |
| `mkdir name` | Make a new directory here |
| `rename file\|n name` | Give a file or directory a new name |
| `copy file\|n dir\|n` | Copy a file or directory into a directory |
| `move file\|n dir\|n` | Move a file or directory into a directory |
| `delete file\|n` (`del`) | Delete a file or directory: to the trash, after asking |
| `info [file\|n]` | Tell about a file or directory (alone: the one you are in): its kind, size, dates and path, and for a text file its characters, words, lines, encoding and line endings |
| `insert file\|n top\|bottom\|+line text` | Put a line of text in a file without opening it: before the first line, after the last, or as line 12 (`+12`). See [Text and settings](#text-and-settings) |
| `change file\|n +line\|from-to [text]` | Change a line (`+12`) or lines (`12-15`) of a file into a text, or delete them when no text is given (asks first). See [Text and settings](#text-and-settings) |
| `shift file\|n lines place` | Move lines in a file without opening it: under a heading (`shift todo.md 4-6 Done`), at the end of its section (`Done+`), before a line (`+12`), to the `top` or the `bottom`. See [Moving lines](#moving-lines) |
| `shift! file\|n lines place` | Move lines as `shift` does, and tick their task boxes (`- [ ]` to `- [x]`), or untick those that are ticked |
| `format file\|n` | Format an HTML, CSS or XML file, as `:format` in the editor does (see [Text and settings](#text-and-settings)) |
| `trim file\|n` | Remove the blank space at the end of the lines, as `:trim` in the editor does; `trim!` also the line breaks of Markdown |
| `set [name [value]]` | Show the settings (`set`), one of them (`set indent`), or change one (`set indent 2`) |
| `reset` | Put all the settings back to their defaults, as _Reset all settings_ on the System page of Configuration (asks first) |
| `reg [name [text\|.\|#n]]` | Keep a text or a path in a register, show one, or list them (see [Registers](#registers)) |
| `reg -d name\|all` | Take a register away, or all of them |
| `clip [path\|name] file\|dir\|n\|.` | Copy the full path of a file or directory (`clip 3`: row 3 of the last list; `clip .`: the directory you are in), or its name, to the clipboard (see [Clipboard](#clipboard)) |
| `clip text file\|n` | Copy the text of a file to the clipboard |
| `clip lines file\|n lines` | Copy lines of a file to the clipboard, given as for `peek` (`40`, `100-120`, `+100 30`) |
| `clip $name` | Copy the value of a register to the clipboard |
| `favorites [list]` (`fav`) | List the favorites of the file browser, with their folders as headings; `fav work` uses the list _work_ from now on, as `:list work` does |
| `history` (`hist`) | List the files opened, the newest first, with when |
| `recent` | List the latest files (the first nine of the history) |
| `hotlist` (`hot`) | List the directory hotlist |
| `fav add\|remove file\|n` | Add a file or directory to the favorites in use, or take it off them |
| `hot add\|remove [dir\|n]` | Add a directory to the hotlist (`hot add` alone: the one you are in), or take it off |
| `bookmarks` (`book`) | List the bookmarks of the Jump menu (a page of Blyant is shown, but opened on the full screen only) |
| `projects` (`proj`) | List the projects |
| `project n\|name` | Go to the directory of a project (`project 2`, `project novel`) |
| `log [n]` | Show the last lines of the [log](Guide-to-start-page.md#log) (20, or as many as you give) |
| `clear` (`cls`) | Clear the screen: the prompt is at the top again |
| `screen` | Go on on the full screen, in the file browser of the directory you are in (see [Between the full screen and the shell](#between-the-full-screen-and-the-shell)) |
| `version` | Show the version of Blyant |
| `help [command]` (`h`) | List the commands, or tell what one does |
| `exit` (`quit`, `q`) | Leave Blyant |

### Typing a command
In a terminal the line of the shell is read key by key:

- `Tab` completes the word you are typing, and `Tab` again gives the next that fits (after the last: what you typed; `Shift+Tab` goes back): the name of a command (`fa` to `favorites`), the words that can follow some of them (the names of the settings after `set`, the lists after `fav`, the projects after `project`, `add` and `remove` after `fav` and `hot`, `top` and `bottom` after `insert`, `top`, `bottom` and the headings of the file after the lines of `shift`, `path`, `name`, `text` and `lines` after `clip`), and the names of files and directories after the commands that take a path (`open pl` to `open plot.md`). `cd` and `ls`, and the directory of `copy` and `move`, take directories only. A directory gets `/` after it, and when it is the only name that fits, `Tab` again goes on into it (`cd pr` to `cd projects/`, then `cd projects/Article/`). A name with a blank in it is put in quotes, and hidden names come only after a `.`
- `Up` and `Down` bring back the commands you typed before, on Windows, Linux and macOS alike; what you have typed is what they start with (`pe` and `Up`: the last `peek`)
- `Left`, `Right`, `Home` and `End` move on the line, `Backspace` and `Delete` delete, `Ctrl+U` deletes the line before the cursor, `Ctrl+W` the word before it, and `Esc` the whole line. A line wider than the window is scrolled sideways on its row
- `Ctrl+C` drops what you have typed, and `Ctrl+D` (or `Ctrl+Z` on Windows) on an empty line leaves, as `exit` does

With a screen reader the line may be read best as the terminal reads it: turn the setting `shellcompletion` off (`set shellcompletion false`, or `shellcompletion=false` in the settings file; not on the pages of Configuration). The line is then read whole after `Enter`, without `Tab` and with the history your terminal gives. Commands from a file, a pipe or `--run` are always read that way.

## Files
The shell works on files with the rules of the file browser:

- A name with blanks in it is written in double quotes: `rename "old notes.md" notes.md`
- `copy` and `move` never overwrite a file or directory of the same name: it is told, and nothing is done. A copy made in the directory it is in gets a new name (`plot-copy.md`)
- `delete` asks first, telling what goes with a directory, and you type the answer in full (`Yes`) to go ahead. It goes to the trash, which you open from the file browser with `:trash`, unless _Delete to trash_ is turned off on the General page of Configuration. Something that cannot be moved to the trash is deleted for good only if you say so
- The files and directories Blyant uses (its data directory, the settings, the favorites, the history) cannot be renamed, moved or deleted from the shell
- What is renamed or moved is followed by the favorites, the history, the hotlist, the projects and the bookmarks, as in the file browser, and what is deleted leaves the favorites
- `find` searches the directory you are in and the directories below it, as the search of the file browser does: hidden files are left out, and at most 5000 files are found. `find plot` finds the names with _plot_ in them, `find *.md` the names that match (`*` and `?`), `find -t storm` the text files with _storm_ in a line (with the line), and `find *.md -t storm` both. `open` on a file found by its text opens it at that line
- After `new`, `mkdir`, `rename`, `copy` and `move`, the new place is the last list: `open 1` opens it

## Text and settings
`format` and `trim` change a file without opening it: they are the commands `:format` and `:trim` of the editor, run on the text of the file, so they do just what they do there (see the [guide to the editor](Guide-to-editor.md#other-commands)). The file is saved only when its text changed, with its line endings and its byte order mark as they were, and it is told: `page.html: Formatted`, `poem.md: Lines trimmed: 3`. A file that cannot be formatted safely is left as it is, and why is told.

- Only files in UTF-8 are changed: a file in another encoding (Windows-1252, say) is left as it is. Open it in the editor, which saves it in UTF-8
- There is no undo: a file changed by `format` or `trim` is saved at once. Use them on files you can get back (a copy, a project kept in Git)

`insert` puts a line of text in a file, as quick notes are written down: `insert todo.md bottom Buy milk`, `insert notes.md top # Monday`, `insert 3 +12 A new line` (a row of the last list). The text is all that follows the place, as you type it (blanks and quotes too), and it is one line. `+12` makes it line 12, before the line that was there, and a line past the end puts it after the last. The file keeps its line endings and its byte order mark, and the file is then the last list, at the line put in: `open 1` opens it there. As for `format`, only a file in UTF-8 is changed, the files Blyant uses are not, and there is no undo.

`change` changes lines of a file into a text, without opening it: `change notes.md +12 A new line` (line 12), `change notes.md 12-15 One line instead` (lines 12 to 15 become one line). The text is all that follows the lines, as you type it, and it is one line. Without a text, the lines are deleted: `change notes.md 12-15` shows them and asks first (No is selected, as there is no undo), and from a file or with `--run` they are only deleted with `--yes`. A range past the end stops at the last line. As for `insert`, the file keeps its line endings and byte order mark, only a file in UTF-8 is changed and not the files Blyant uses, and the file is then the last list at the place changed (`open 1`). (The `:replace` of the editor finds a text and puts another in its place; `change` works on whole lines.)

`set` shows the settings as they are in the settings file (`settings.ini` in the data directory), and changes one as the pages of Configuration do: `set indent 2`, `set wrap false`, `set colorscheme Deep Blue`. The setting is read again at once, as Blyant reads it when it starts. A value the setting cannot have is told, and the setting stays as it was: `indent cannot be 3: it stays 4`. A settings file written by an earlier version of Blyant lacks the settings that came after it: `set` writes the file whole first (as changing a setting in Configuration does), so every setting of this version can be shown and changed.

`reset` puts all the settings back to their defaults, as _Reset all settings_ on the System page of Configuration does: your lists, favorites, bookmarks and files are kept. It asks first, and only `y` (or `Yes`) goes ahead: `Enter` alone answers no. The settings of the shell go back too (`shellpaging` on, `shelldir` empty), and so does the language (English). From a file or with `--run`, nothing is reset unless `--yes` is given. The names and values are those of the settings file, and the guide to [Configuration](Guide-to-start-page.md#configuration) tells what each setting does.

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

- `reg` lists the registers, `reg docs` shows the value of one (alone on its line, as a script reads it with `blyant --run "reg docs"`), `reg -d docs` takes it away, and `reg -d all` all of them
- A name starts with a letter, and has letters, digits and `_` (`docs`, `draft_2`); letter case is alike
- `$name` is put in only for a register that is there: anything else stays as typed, so `insert prices.md bottom Ink costs $5` keeps its `$5`. `$$` is a `$`
- A path with a blank in it is put in quotes where a path is taken (`copy plot.md $docs` with _my docs_), and the text of `insert`, `change`, `reg` and `set` gets the value as it is
- `Tab` completes the names of the registers after a `$`, and after `reg`
- The registers are kept in the data directory (`registers.txt`), so they are there the next time. They are the shell's own: the registers of the editor are not these. `reset` does not take them away (they are not settings), and the log has their names, never their values

## Clipboard
`clip` copies to the clipboard what you would otherwise select and copy by hand:

```
recent
clip 3                      the full path of row 3 (also: clip path 3)
clip name 3                 its name only (plot.md)
clip .                      the directory you are in
clip text todo.md           the whole text of a file
clip lines notes.md +40 10  10 lines from line 40 (as peek takes them: 40, 100-120, +100 30)
clip $note                  the value of a register, as it is
```

- It goes where the [clipboard settings](Guide-to-editor.md#system-clipboard-and-paste-bin) of the editor send a yank: to the clipboard of the system through the terminal (OSC 52, also over SSH) and/or to the paste bin file. Text too long for the terminal goes only to the paste bin, if it is on. What was copied is told, and the path, or how much
- It also goes to the string memory of the editor: after `open` (or `screen`), `p` in the editor puts it in, and `:reglist` lists it. That works also where the terminal has no clipboard (the console of Linux, the Terminal of macOS), or with the clipboard turned off
- `text` and `lines` take text files only. Lines copied are joined by line breaks, without one after the last
- From a file, a pipe or with `--run` and the output going to a file or a program, the clipboard sequence would end up in that output: then only the paste bin is used, and without it `clip` is told it needs a terminal
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
- Questions are not asked: the answer is no, so nothing is done that would ask first. With `--yes` the answer is yes, also to deleting (to the trash): `blyant --shell --yes < commands.txt`
- `open` does not open the editor (it needs a terminal); with `--yes` a file that is not there is still made

`--run` gives the commands on the command line instead, one after each `--run`, and Blyant ends when they are done: what they show, their questions and the exit code are as for commands read from a file.

```
blyant --run "format page.html"
blyant --run "trim notes.md" --run "format site.css" ~/projects/site
```

A directory given with them is where they run (the one you are in, if none is given).

## What stays on the full screen
The corkboard, the Jump menu, the color schemes and logos, Configuration, split views and Zen mode are not in the shell: start Blyant without `--shell` to use them. Your settings, lists and bookmarks are the same in both, as they are kept in the data directory (see [Good to know](Guide-to-installation.md#good-to-know)).
