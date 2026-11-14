# Guide to the shell
The shell is Blyant on lines: you type a command at a prompt, and Blyant writes its answer as lines of text, with no colors and no cursor moved about. It works in any terminal, with a screen reader, and from a script. Files are edited in the editor of the full screen, and closing the editor brings you back to the prompt.

Start it with `--shell`, and leave it with `exit`:

```
blyant --shell                 The shell, in the directory you are in
blyant --shell ~/projects      The shell, in that directory
```

(On Windows `Blyant.exe --shell`, and `dotnet Blyant.dll --shell` for the portable version.)

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
| `cd [dir\|n]` | Go to a directory; `cd` alone goes to your home directory, `cd ~/notes` to one in it |
| `up` | Go to the directory above (`cd ..`) |
| `open file\|n [+line]` | Edit a file in the editor of the full screen, at a line if one is given (`open plot.md +12`). A directory is gone to |
| `peek file\|n [lines]` | Show the lines of a text file, numbered: the first 20, `peek plot.md 40` the first 40, `peek plot.md 100-120` those. Only read |
| `find [name] [-t text]` | Find files here and in the directories below, by name and by a text in them (see [Files](#files)) |
| `new name` | Make a new, empty file here (`new ideas` makes `ideas.md`: the kind of new files, when no extension is given) |
| `mkdir name` | Make a new directory here |
| `rename file\|n name` | Give a file or directory a new name |
| `copy file\|n dir\|n` | Copy a file or directory into a directory |
| `move file\|n dir\|n` | Move a file or directory into a directory |
| `delete file\|n` (`del`) | Delete a file or directory: to the trash, after asking |
| `info [file\|n]` | Tell about a file or directory (alone: the one you are in): its kind, size, dates and path, and for a text file its characters, words, lines, encoding and line endings |
| `format file\|n` | Format an HTML, CSS or XML file, as `:format` in the editor does (see [Text and settings](#text-and-settings)) |
| `trim file\|n` | Remove the blank space at the end of the lines, as `:trim` in the editor does; `trim!` also the line breaks of Markdown |
| `set [name [value]]` | Show the settings (`set`), one of them (`set indent`), or change one (`set indent 2`) |
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

`Ctrl+C` at the prompt drops what you have typed, and `Ctrl+D` (Linux, macOS) or `Ctrl+Z` and `Enter` (Windows) leave, as `exit` does. The commands you have typed come back with the up arrow on Windows; on Linux and macOS your terminal decides.

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

`set` shows the settings as they are in the settings file (`settings.ini` in the data directory), and changes one as the pages of Configuration do: `set indent 2`, `set wrap false`, `set colorscheme Deep Blue`. The setting is read again at once, as Blyant reads it when it starts. A value the setting cannot have is told, and the setting stays as it was: `indent cannot be 3: it stays 4`. The names and values are those of the settings file, and the guide to [Configuration](Guide-to-start-page.md#configuration) tells what each setting does.

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
