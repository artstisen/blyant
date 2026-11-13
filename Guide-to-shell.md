# Guide to the shell
The shell is Blyant on lines: you type a command at a prompt, and Blyant writes its answer as lines of text, with no colors and no cursor moved about. It works in any terminal, with a screen reader, and from a script. Files are edited in the editor of the full screen, and closing the editor brings you back to the prompt.

Start it with `--shell`, and leave it with `exit`:

```
blyant --shell                 The shell, in the directory you are in
blyant --shell ~/projects      The shell, in that directory
```

(On Windows `Blyant.exe --shell`, and `dotnet Blyant.dll --shell` for the portable version.)

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

## What stays on the full screen
The corkboard, the Jump menu, the color schemes and logos, Configuration, split views and Zen mode are not in the shell: start Blyant without `--shell` to use them. Your settings, lists and bookmarks are the same in both, as they are kept in the data directory (see [Good to know](Guide-to-installation.md#good-to-know)).
