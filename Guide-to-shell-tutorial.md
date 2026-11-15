# Shell tutorial
The shell is Blyant on lines: you type a command at a prompt, and what it shows is written below it, as in a terminal. It does what the full screen does with files (finding them, looking into them, copying, moving, adding a line, ticking off a task) without leaving the line you type on. It works in any terminal, over SSH, with a screen reader, and from scripts.

This tutorial goes through a day of work on a small novel project, one task at a time. Each part shows what you type after the prompt, and what Blyant writes back. The last part, [Practice](#practice), is ten short steps to try on two files of your own. The [guide to the shell](Guide-to-shell.md) tells all the commands and their details.

The project used in the examples is in `~/novel` (`/home/anna/novel`):

```
novel/
  chapters/
    01-attic.md
    02-storm.md
  notes/
    places.md
  plot.md
  todo.md
```

## Starting the shell
Start Blyant with `--shell`, and a directory if you want to start in one:

```
blyant --shell ~/novel
```

```
Blyant v1.5.2: help lists the commands, exit leaves
blyant ~/novel>
```

The prompt shows where you are: `~` is your home directory. `about` shows the commands used the most, `help` lists them all, `help shift` tells what one of them does, and `exit` (or `Ctrl+D`) leaves Blyant. `set language de` has Blyant speak German (`en`, `da`, `nb`, `sv`, `es`, `fr`, `it`, `pl`, `nl` and `fi` are there too); the commands stay as they are.

Other ways in:

- `:shell` on any command line of the full screen goes on as the shell, in the directory of the page you were on, and `screen` in the shell goes back to the file browser there. You can go back and forth as often as you like
- _Start in the shell_ on the General page of Configuration has Blyant start as the shell every time (`blyant --screen` then starts on the full screen once)
- The setting `shelldir` is the directory the shell starts in: `set shelldir ~/novel`, or `set shelldir last` to start where you left it

## Getting around
`ls` lists the directory you are in: the folders first, then the files, each with a number:

```
blyant ~/novel> ls
/home/anna/novel
  1  chapters/
  2  notes/
  3  plot.md
  4  todo.md
```

The numbers are the key to the shell. A number given to a command is a row of the last list shown, so you seldom type a name. The list is kept until another list is shown, or a command works on a file (`peek`, `insert`, `copy` ...: that file is then the list, as row 1); `cd` into a directory, `open`, `clip` and `reg` leave it as it was. See [Prompt and lists](Guide-to-shell.md#prompt-and-lists) for all of them:

```
blyant ~/novel> cd 1
blyant ~/novel/chapters> ls
/home/anna/novel/chapters
  1  01-attic.md
  2  02-storm.md
blyant ~/novel/chapters> up
blyant ~/novel>
```

- `cd 1` goes into row 1, `cd chapters` does the same by name, and `cd ../other` or `cd ~/letters` take a path
- `up` goes to the directory above, `home` (or `cd` alone) to your home directory, and `pwd` shows where you are
- `ls -a` lists hidden files too (as `.gitignore`), which `ls` leaves out, as the file browser does
- `Tab` completes what you type: `cd ch` and `Tab` gives `cd chapters/`, and `Tab` again goes on into it. `Up` and `Down` bring back the commands you typed before
- `open ` and `Tab` with nothing typed gives the file you opened last in this directory, and `Tab` again the one before it: quick to go between the files you are working on. `open chapters/*storm*` and `Tab` gives `open chapters/02-storm.md`: `*` stands for any characters, and `?` for one
- `cd` on a file goes to the directory it is in, and makes the file the last list, so `open 1` opens it:

```
blyant ~/novel> cd chapters/02-storm.md
In /home/anna/novel/chapters (the directory of 02-storm.md)
```

A list longer than the window comes in pages: `Enter` or `Space` gives the next page, and `q` stops it.

## Looking into a file
`peek` shows the lines of a text file, numbered, without opening it (and it never changes it):

```
blyant ~/novel> peek 3
  1  # Plot
  2  
  3  Anna finds a letter in the attic.
  4  The storm comes that night.
```

- `peek plot.md 40` shows the first 40 lines (20 when no number is given), and `peek plot.md 100-120` those lines
- `peek chapters/02-storm.md +3 2` shows 2 lines from line 3. The file is then the last list at that line, so `open 1` opens it right there:

```
blyant ~/novel> peek chapters/02-storm.md +3 2
  3  The storm came at night.
  4  The lights went out.
blyant ~/novel> open 1
```

`info plot.md` tells about a file: its size and dates, and for a text file its characters, words, lines, encoding and line endings.

## Finding files
`find` searches the directory you are in and those below it, by name, by a text in the files, or both:

```
blyant ~/novel> find storm
  1  chapters/02-storm.md
blyant ~/novel> find -t storm
  1  plot.md:4  The storm comes that night.
  2  chapters/02-storm.md:3  The storm came at night.
blyant ~/novel> find *.md -t attic
  1  plot.md:3  Anna finds a letter in the attic.
  2  chapters/01-attic.md:4  Anna climbed to the attic.
```

What is found is a list like any other: `peek 2` shows it, and `open 2` opens it at the line found.

## Editing a file
`open` edits a file in the editor of the full screen, with all its keys and commands:

```
blyant ~/novel> open plot.md
blyant ~/novel> open 3 +12
```

`+12` opens it at line 12. When you close the editor (`:wq`, `:q`), you are back at the prompt, where you were. A file that is not there is made if you want that: the shell asks first.

## Making, copying and moving
The shell works on files with the rules of the file browser: nothing is overwritten, and what is deleted goes to the trash.

```
blyant ~/novel> new scenes
Made: /home/anna/novel/scenes.md
blyant ~/novel> mkdir old
Made: /home/anna/novel/old
blyant ~/novel> copy plot.md old
Copied to /home/anna/novel/old/plot.md
blyant ~/novel> rename scenes.md scene-list.md
Renamed: /home/anna/novel/scene-list.md
blyant ~/novel> move scene-list.md notes
Moved to /home/anna/novel/notes/scene-list.md
```

- `new` makes an empty file (`.md` when no extension is given), and `mkdir` a directory
- `copy` and `move` take what, and the directory it goes into; `rename` takes what, and its new name. Rows work for both: `copy 3 1` copies row 3 into row 1
- After them the new place is the last list: `open 1` opens it
- A name with blanks in it is written in quotes: `rename "old notes.md" notes.md`

`delete` asks first, and tells what goes with a directory. You type the answer in full to go ahead:

```
blyant ~/novel> delete old
Delete directory: Move "old" to the trash?
  With all its content (files: 1, directories: 0). It can be found again in the trash (:trash in the file browser)
  Type Yes to go ahead (anything else: no): Yes
Moved to the trash: /home/anna/novel/old
```

The trash is opened with `:trash` in the file browser (`screen` takes you there), where a file can be put back.

## Adding and changing lines
`insert` puts a line of text in a file without opening it, as quick notes are written down: at the `top`, at the `bottom`, or as line 12 (`+12`). The text is all that follows, as you type it:

```
blyant ~/novel> insert plot.md bottom Then the phone rings.
/home/anna/novel/plot.md: line 5 put in
blyant ~/novel> insert plot.md +4 The letter is from her sister.
/home/anna/novel/plot.md: line 4 put in
blyant ~/novel> peek plot.md
  1  # Plot
  2  
  3  Anna finds a letter in the attic.
  4  The letter is from her sister.
  5  The storm comes that night.
  6  Then the phone rings.
```

`change` changes a line (`+4`), or lines (`4-6`, which become one line), into a text. Without a text, the lines are deleted, after they are shown and you answer yes:

```
blyant ~/novel> change plot.md +4 The letter is from her mother.
/home/anna/novel/plot.md: line 4 changed
blyant ~/novel> change plot.md 6
  6  Then the phone rings.
Delete lines: Delete line 6 of plot.md?
  Y Yes  N No*: y
/home/anna/novel/plot.md: line 6 deleted
```

`lines` changes the text in lines, and keeps what is there: `start` puts a text before a line, and `end` after it:

```
blyant ~/novel> lines plot.md 4 start Draft:
  4  Draft: The letter is from her mother.
/home/anna/novel/plot.md: line 4 changed
blyant ~/novel> lines plot.md 4 unstart Draft:
  4  The letter is from her mother.
/home/anna/novel/plot.md: line 4 changed
```

- `unstart` and `unend` take the text off again, and `lines plot.md 3-5 replace letter note` replaces a word in lines 3 to 5
- In quotes the text is put as it is, without a blank between: `lines todo.md 5-7 start "- [ ] "` makes lines 5 to 7 tasks

There is no undo for `insert`, `change` and `lines`: `peek` first, to be sure of the line numbers. A file keeps its line endings and its byte order mark, and the files Blyant uses are not changed.

## Todo list
![A task added with insert, and ticked and moved under Done with shift!](docs/screenshots/shell-todo.svg)

`todo.md` is a list of tasks under headings:

```
blyant ~/novel> peek todo.md
   1  # Week 46
   2  
   3  ## Todo
   4  
   5  - [ ] Rewrite chapter two
   6  - [ ] Check the dates
   7  - [ ] Call the editor
   8  
   9  ## Done
  10  
  11  - [x] Outline
```

`shift` moves lines to another place in the same file, and a heading can be the place. `shift!` also ticks the task box of each line it moves. The dates are checked:

```
blyant ~/novel> shift! todo.md 6 Done
  10  - [x] Check the dates
/home/anna/novel/todo.md: line moved to line 10
Task boxes ticked or unticked: 1
```

A text finds the line, so you need not look up its number:

```
blyant ~/novel> shift! todo.md "Call the editor" Done
  9  - [x] Call the editor
/home/anna/novel/todo.md: line moved to line 9
Task boxes ticked or unticked: 1
blyant ~/novel> peek todo.md
   1  # Week 46
   2  
   3  ## Todo
   4  
   5  - [ ] Rewrite chapter two
   6  
   7  ## Done
   8  
   9  - [x] Call the editor
  10  - [x] Check the dates
  11  - [x] Outline
```

- The lines go right under the heading, so the newest come first. `Done+` puts them at the end of its section instead: `shift! todo.md Rewrite Done+`
- `shift!` on a ticked line unticks it, so a task moved back is open again: `shift! todo.md Outline Todo`
- Several lines move together, in their order: `shift todo.md 5-7 Done`, `shift todo.md 5,8,10 Done`
- Other places: `+3` (before line 3), `top` and `bottom`
- `shift` without `!` moves the lines as they are, for any kind of list: scenes in an outline, items in a shopping list
- `Tab` after the lines completes the headings of the file

At the end of the week the tasks done go to a file of their own. `lines` copies lines to another file, and with `cut` moves them there:

```
blyant ~/novel> new done
Made: /home/anna/novel/done.md
blyant ~/novel> lines todo.md 9-11 > done.md bottom cut
  1  - [x] Call the editor
  2  - [x] Check the dates
  3  - [x] Outline
/home/anna/novel/done.md: lines 1-3 put in
/home/anna/novel/todo.md: 3 lines taken out
```

- Without `cut` the lines are copied, and stay where they were
- The places are those of `shift`: `top`, `bottom`, `+12`, a heading or `Heading+`, now in the other file. The lines too, also `+9 3` (3 lines from line 9)

## Registers
A register keeps a path or a text under a name, and `$name` puts it in any command. They are kept until you take them away, also when Blyant is started again.

Keep the directory you are in with `.`, and a row of the last list with `#`:

```
blyant ~/novel> cd chapters
blyant ~/novel/chapters> reg chap .
  $chap = /home/anna/novel/chapters
blyant ~/novel/chapters> up
blyant ~/novel> ls
/home/anna/novel
  1  chapters/
  2  notes/
  3  done.md
  4  plot.md
  5  todo.md
blyant ~/novel> reg plot #4
  $plot = /home/anna/novel/plot.md
```

Or a text, all that follows the name:

```
blyant ~/novel> reg note Check the dates in chapter two
  $note = Check the dates in chapter two
blyant ~/novel> reg
  $chap /home/anna/novel/chapters
  $note Check the dates in chapter two
  $plot /home/anna/novel/plot.md
```

Then use them anywhere:

```
blyant ~/novel> copy $plot $chap
Copied to /home/anna/novel/chapters/plot.md
blyant ~/novel> insert todo.md +5 - [ ] $note
/home/anna/novel/todo.md: line 5 put in
blyant ~/novel> cd $chap
blyant ~/novel/chapters>
```

- `reg -d note` takes a register away, and `reg -d all` all of them
- A path with a blank in it is kept in quotes: `reg docs "~/my docs"`
- `Tab` completes the names of the registers after a `$`
- The registers of the shell are its own: the string memory of the editor is not these

## Clipboard
`clip` copies to the clipboard what you would otherwise select and copy by hand, as `:path` and `:name` do in the file browser:

```
blyant ~/novel> ls
...
blyant ~/novel> clip 3
Copied to the clipboard and the string memory
  /home/anna/novel/plot.md
blyant ~/novel> clip name 3
Copied to the clipboard and the string memory
  plot.md
```

- `clip .` copies the path of the directory you are in, `clip text plot.md` the whole text of a file, `clip lines plot.md +3 2` two lines from line 3, and `clip $note` the value of a register
- It goes where the clipboard settings of the editor send a yank: the clipboard of your system (through the terminal), and/or the paste bin file
- It also goes to the string memory of the editor: after `open`, `p` in the editor puts it in. That works also in a terminal without a clipboard

What you copied last is `$clip`, which puts it in a command, as a register does. Lines copied go in as lines:

```
blyant ~/novel> clip lines plot.md +3 2
Copied to the clipboard and the string memory
  Lines 3-4 of /home/anna/novel/plot.md
blyant ~/novel> new ideas
Made: /home/anna/novel/ideas.md
blyant ~/novel> insert ideas.md bottom $clip
/home/anna/novel/ideas.md: lines 1-2 put in
```

- `$clip` is kept in Blyant until it ends: it is not read from the clipboard of your system. Text copied in another program is pasted on the line with the keys of your terminal
- `reg` lists it after the registers, and `reg clip` shows all of it

## Favorites, history and hotlist
The lists of the full screen are the lists of the shell:

```
blyant ~/novel> fav add plot.md
Added to the favorites: /home/anna/novel/plot.md
blyant ~/novel> fav add chapters
Added to the favorites: /home/anna/novel/chapters
blyant ~/novel> fav
List: main
  1  plot.md
  2  chapters/
blyant ~/novel> hot add
Added to the hotlist: /home/anna/novel
```

- `fav` lists the favorites, `hist` the files you have opened (newest first), and `recent` the latest nine of them. `open 1`, `peek 1` and `cd 1` work on each of them
- They show the names. `info 1` tells where row 1 is, its size and dates, and the list stays, so `info 2` and `open 2` work after it. `fav -l` shows the full paths
- `hot` lists the directory hotlist, and `cd 2` goes to row 2 of it. `hot add` adds the directory you are in, and `hot remove 2` takes one off
- `book` lists the bookmarks of the Jump menu, `projects` the projects, and `project novel` goes to the directory of one
- `fav remove 1` takes a favorite off the list (the file itself stays)

The favorites can be kept in several lists, as with `:list` in the file browser: one for the novel, one for research. The list you open is the one the full screen shows too:

```
blyant ~/novel> fav new research
List made and opened: research
blyant ~/novel> fav add notes
Added to the favorites: /home/anna/novel/notes
blyant ~/novel> fav lists
    main
  * research
blyant ~/novel> fav main
List: main
  1  plot.md
  2  chapters/
```

- `fav research` opens the list again, and `fav main` goes back to the main list
- `fav rename research sources` gives a list a new name, and `fav delete sources` deletes it, after asking (the files in it stay where they are)

## Tidying files
`format` and `trim` are the commands `:format` and `:trim` of the editor, run on a file without opening it:

```
blyant ~/site> format index.html
/home/anna/site/index.html: Formatted
blyant ~/novel> trim plot.md
/home/anna/novel/plot.md: No blank space at line ends
```

`format` formats HTML, CSS and XML files, as Blyant Validator does. `trim` removes the blank space at the ends of the lines (`trim!` also the line breaks of Markdown).

## Settings
`set` shows the settings, one of them, or changes one, as the pages of Configuration do:

```
blyant ~/novel> set indent
  indent = 4
blyant ~/novel> set indent 2
  indent = 2
blyant ~/novel> set shelldir ~/novel
  shelldir = ~/novel
```

`reset` puts all the settings back to their defaults (it asks first). Your files and lists are kept.

Settings can also be changed in the settings file itself. `data` goes to the data directory of Blyant (`~/blyant/data` when Blyant is in `~/blyant`), where it is. `open settings.ini` edits it in the editor, and what you save there is used at once. When you edit it in another program, `reload` uses what you saved, without starting Blyant again:

```
blyant ~/novel> data
blyant ~/blyant/data> reload
Settings and registers read again
```

The registers are kept in the same directory, in `registers.txt` (a name, a tab, and the value on each line). When you edit that file, the shell reads it again by itself before the next command.

## Scripts
The commands of the shell can also be run from the command line, or read from a file, as a small script:

```
blyant --run "format index.html" ~/site
blyant --run "insert todo.md bottom - [ ] Buy ink" ~/novel
printf 'cd chapters\nfind -t storm\n' | blyant --shell ~/novel
blyant --shell ~/novel < weekly.txt
```

- No prompt is shown, and what went wrong goes to the error output. The exit code is 1 when a command failed
- Questions are not asked: the answer is no, so nothing is deleted. With `--yes` the answer is yes
- `reg docs` alone writes the value of a register on its own line, so a script can read it: `blyant --run "reg docs"`

## Full screen and shell side by side
What you do on the full screen, and the command for it in the shell:

| On the full screen | In the shell |
|---|---|
| The file browser of a directory | `ls`, `cd 2`, `up` |
| `Enter` on a file | `open 3` |
| Peek (`V` in the file browser) | `peek 3`, `peek 3 +100 20` |
| Search for files (`S` or `:find`) | `find plot`, `find -t storm` |
| New file, new directory | `new ideas`, `mkdir drafts` |
| Copy, move, rename, delete | `copy 3 1`, `move 3 1`, `rename 3 notes.md`, `delete 3` |
| Favorites, history, hotlist | `fav`, `hist`, `recent`, `hot` |
| Projects, bookmarks | `projects`, `project novel`, `book` |
| `:path`, `:name` | `clip 3`, `clip name 3` |
| `:format`, `:trim` in the editor | `format 3`, `trim 3` |
| A line added in the editor | `insert 3 bottom text` |
| Lines changed or deleted in the editor | `change 3 +12 text`, `change 3 12-15` |
| Lines cut and put under another heading | `shift 3 4-6 Done` |
| Lines copied or moved to another file | `lines 3 4-6 > 5 bottom`, `lines 3 4-6 > 5 bottom cut` |
| Copy and paste | `clip lines 3 4-6`, then `insert 5 top $clip` |
| Configuration | `set`, `set indent 2` |
| `:shell` | `screen` |

## Practice
Ten steps to train the commands of this tutorial, on two files of your own. Type each command after the prompt: what Blyant writes back is shown under it. To keep the files apart, make a directory for them first, and go into it:

```
blyant ~> mkdir practice
Made: /home/anna/practice
blyant ~> cd practice
```

**1. Create a file.** `new` makes an empty file, and gives it `.md` when the name has no extension:

```
blyant ~/practice> new file1
Made: /home/anna/practice/file1.md
```

**2. Insert the text "File 1" at the top of the file.** All that follows `top` is the text:

```
blyant ~/practice> insert file1.md top File 1
/home/anna/practice/file1.md: line 1 put in
```

**3. Peek at the change.** `peek` shows the lines of the file, numbered:

```
blyant ~/practice> peek file1.md
  1  File 1
```

**4. Insert another line below the first.** `bottom` puts it after the last line:

```
blyant ~/practice> insert file1.md bottom A second line
/home/anna/practice/file1.md: line 2 put in
blyant ~/practice> peek file1.md
  1  File 1
  2  A second line
```

**5. Move the first line below the second.** `shift` moves lines in a file: line 1 to the `bottom`. The line is shown at its new place:

```
blyant ~/practice> shift file1.md 1 bottom
  2  File 1
/home/anna/practice/file1.md: line moved to line 2
```

**6. Create a second file:**

```
blyant ~/practice> new file2
Made: /home/anna/practice/file2.md
```

**7. Copy line 2 of file1 into file2.** `lines` takes the file and the lines, a `>`, and the other file and the place in it. Line 2 of `file1.md` is now "File 1":

```
blyant ~/practice> lines file1.md 2 > file2.md top
  1  File 1
/home/anna/practice/file2.md: line 1 put in
```

**8. Change the line in file2 into "File 2".** `change` takes the line (`+1`) and the new text:

```
blyant ~/practice> change file2.md +1 File 2
/home/anna/practice/file2.md: line 1 changed
blyant ~/practice> peek file2.md
  1  File 2
```

**9. Copy the whole text of file2.** `clip text` copies it to the clipboard, and keeps it as `$clip`:

```
blyant ~/practice> clip text file2.md
Copied to the clipboard and the string memory
  /home/anna/practice/file2.md (7 characters)
```

**10. Insert it at the bottom of file1.** `$clip` puts in what was copied last:

```
blyant ~/practice> insert file1.md bottom $clip
/home/anna/practice/file1.md: line 3 put in
blyant ~/practice> peek file1.md
  1  A second line
  2  File 1
  3  File 2
```

That is the whole round: making files, adding, moving, copying and changing lines, without opening an editor. `open file1.md` shows the result in the editor of the full screen. To try again, `delete file1.md` and `delete file2.md` (they go to the trash), or start over in another directory.
