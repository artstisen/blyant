# Blyant
A free, open-source, cross-platform, Vim-style modal text editor for writing in any terminal program on Windows, Linux and macOS. _Blyant_ is Danish for _pencil_.

**Website: [blyant-editor.com](https://blyant-editor.com)** – docs, downloads, screenshots, etc.

This project began in 2022, with the application architecture and codebase written by hand. In later versions, I’ve used agentic tools to refine, rewrite, test, and conduct code reviews. Blyant is derived from my encryption editor [Marbles](https://github.com/artstisen/marbles), but there are many differences, as Blyant is designed for writing, programming, organizing files, planning projects, etc.

Blyant has several features inspired by popular Neovim plugins for text navigation, commands, and much more. I use Blyant in my professional work, writing software and API documentation in Markdown and HTML, developing CSS and JavaScript widgets as well as .NET productivity and conversion tools, and editing JSON and XML.

**My favorite feature? – Using bookmarks to create custom jump points and open them from anywhere via the command line.**

![The editor in split view, with command suggestions](docs/screenshots/split-view.svg)


## Corkboard
Plan a book, an article or a thesis on a corkboard: chapters, research folders and loose ideas are cards that you place freely, color, and lay on top of each other. Each project has its own corkboard, kept in the project folder, so it goes with the project when you move, copy or sync it.

![Corkboard of a project: chapters, a research folder, files and notes as cards in colors, badges marking the chapters drafted and one to rewrite, with a peek into the selected chapter](docs/screenshots/corkboard.svg)

- **Cards of files, folders and notes**: `:pin` a file or folder from the editor, the file browser or the lists, or write a note card (`A`) with your own text. `Enter` opens the file in the editor, the folder in the file browser, or the note. A badge (`B`), a small round `i` with a note, marks a group of cards
- **Arrange them your way**: move a card with `V` and `h` `j` `k` `l`, put it on top of the others or below them, give it a color and a title of its own, or lay them all out in rows with `:arrange number`, `:arrange name` or `:arrange color`
- **Dividers, lines and frames**: split the board into columns, draw lines and arrows between cards, and put a frame with a title around a group
- **See without opening**: `P` shows the start of the selected card's file (or what a folder holds) below the board, and `I` tells about it
- **A board for each project**: `B` in the list of projects opens its board, and the cards are shown beside the list; `:list` keeps more boards of your own
- **Safe to try**: `D` takes a card off (the file stays), `:clear` takes them all off after asking, and `u` puts them back. Cards follow their files when you rename or move them in Blyant

![The list of projects, with the corkboard of the selected project beside it: its cards and badges in their colors](docs/screenshots/projects.svg)

See the [guide to the corkboard](Guide-to-corkboard.md).

## Key features

**Files and projects**
- The start page: New file, File browser, New project and Open project, a key each
- A project is a folder with the files of a piece of writing, kept in a folder of your choosing
- A corkboard to plan a book or an article: its files, folders and notes as cards you place, color and arrange, with a board for each project that goes with its folder ([guide](Guide-to-corkboard.md))
- Your files are ordinary text files on your own computer, to back up, sync and open with any other program

**A Vim-style editor**
- Normal, Insert and Visual modes (also block selection), counts, text objects, registers and undo
- Split view: the same text in two panes side by side, remembered when you open it again
- Zen mode (`:zen`): only the mode and the file name above the text, and the line and column below it
- Syntax highlighting for Markdown, Typst, HTML, PHP, CSS, JavaScript, JSON, XML, C#, C and C++, Rust, Python, PowerShell, shell scripts, Windows batch files, settings files (INI) and diffs ([which files](Guide-to-editor.md#colors))
- `:format` formats HTML, CSS and XML files as [Blyant Validator](Guide-to-installation.md#blyant-validator) does ([how](Guide-to-editor.md#other-commands))
- Spell checking and word completion
- Typst: colors, completions (`/h1`, `/figure`, `/math` ...) and a [guide to writing with Typst and its CLI](Guide-to-typst.md)
- Search and replace with regular expressions, file comparison (diff), snippets, and a macro recorder whose macros can be edited as text
- An edit history (`:changes`): the latest edits in a list, to see what you did and jump back to it
- A command line with `Tab` completion and command history on every page

**A file manager**
- An orthodox file browser with two panes: create, rename, copy, move, delete and compare
- Favorites (with folders and colors), file history, a directory hotlist and file search by name or text
- Peek at a file without opening it, and open any file at the place you left it

**Quick to get around**
- The Jump menu (`Ctrl+O`): bookmarks, recent places, files, directories and pages in a few keys
- `gf` on a file name or a Markdown link in your text opens that file, so a table of contents leads to the chapters
- Use bookmarks to create custom jump points in files, folders, projects, pages, and settings, and open them from anywhere using the command `:book number`
- The same keys everywhere: `h` `j` `k` `l` to move, `/` to search, `?` for help and `:` for commands
- Built-in guides: a quick guide and short guides to each part of Blyant, and a user manual (Markdown in the download, PDF on the releases page)

**At home in any terminal**
- Pure 7-bit ASCII interface that works with any monospaced font
- Modules: turn off the projects, the corkboard or the Jump menu when you do not need them, and Blyant shows only what you use
- Color schemes and logos, which you can edit and make your own; `:colorscheme name` changes the scheme from any command line
- English built in, and ten more languages as files that can be changed or added to
- Runs on Windows, Linux (also Raspberry Pi) and macOS, as a single file with nothing else to install
- A shell for lines of text (`blyant --shell`): your files and lists with typed commands and numbered lists, for screen readers and scripts, and the full editor a command away ([tutorial](Guide-to-shell-tutorial.md))

## Blyant Validator
**[Blyant Validator](https://github.com/artstisen/blyant-validator)** is a companion tool, which also works on its own: validate HTML, CSS, XML, Markdown and Typst, and format HTML, CSS and XML, side by side with your editor, in any terminal. It checks a file again each time it is saved, and makes a Typst document into a PDF with `:typst`.

The two work together. Set Blyant as the editor of Blyant Validator, and a problem it finds opens in Blyant at its line:

```
:editor "C:\path\to\Blyant.exe" +{line}
```

(On Linux and macOS, give the path of `Blyant`.) Fix the problem, then `:wq` saves and closes the file. `:exit` (or `:qa`) leaves Blyant and takes you back to Blyant Validator at once. If the file has changes that are not saved, `:exit` asks about them first. See [Blyant Validator](Guide-to-installation.md#blyant-validator) in the installation guide.

## Screenshots
The screenshots are of the real program, made with [tools/screenshots.py](tools/screenshots.py) from a made-up writing project, in the color schemes Default, Deep Blue and Tasty Bytes.

| | |
|---|---|
| ![The start page](docs/screenshots/start-page.svg) | ![The file browser with two panes](docs/screenshots/file-browser.svg) |
| The start page, with a hint about a feature | The file browser: two panes, line numbers and favorites (`*`) |
| ![Editing Markdown](docs/screenshots/editor.svg) | ![Favorites in colored folders](docs/screenshots/favorites.svg) |
| Editing Markdown: colored text, line numbers, a search with its count | Favorites in colored folders, with a note and a peek |
| ![The Jump menu](docs/screenshots/jump-menu.svg) | ![Comparing with the saved file](docs/screenshots/compare.svg) |
| The Jump menu: bookmarks of places in files, `:book 1` from anywhere | `:diff`: what you changed since the file was saved |
| ![The color schemes](docs/screenshots/color-schemes.svg) | ![Split view](docs/screenshots/split-view.svg) |
| Color schemes: the colors of the command line, with a sample | Split view, and the commands listed while one is typed |
| ![Another file read only beside the text](docs/screenshots/split-file.svg) | ![The latest files at a key](docs/screenshots/recent-files.svg) |
| `:view` (or `:split name`): another file read only beside the text | `Ctrl+K`: the latest files, to switch to one |
| ![Editing Typst](docs/screenshots/typst.svg) | ![The corkboard of a project](docs/screenshots/corkboard.svg) |
| Editing Typst: markup, math and code in their colors | The corkboard of a project: files, folders and notes as cards |
| ![The shell: a project with typed commands](docs/screenshots/shell.svg) | ![The shell: a todo list kept with insert and shift!](docs/screenshots/shell-todo.svg) |
| The shell (`blyant --shell`): `ls`, `peek`, `find` and a register, in numbered lists | The shell: a task added with `insert`, and ticked and moved to _Done_ with `shift!` |

## Getting started
1. Download the build for your platform from the [releases page](https://github.com/artstisen/blyant/releases), and unzip it into a directory of your choice.
2. Start `Blyant` (or `Blyant.exe` on Windows) in your terminal. `blyant .` opens the file browser in the directory you are in, and `blyant chapter.md` opens the file in the editor.
3. On the start page, press `P` for a new project or `N` for a new file. Press `?` on any page to see its keys, or open the Quick guide on the Help tab of the start page.

See the [guide to installation](Guide-to-installation.md) for the builds and the details.

## Pure 7-bit ASCII TUI
The aim is to make an unbloated set of core tools. I've included functionality that I would normally have added via lightweight, functional plugins and init modifications in Neovim, without disrupting the classic terminal aesthetic.

The interface of Blyant is drawn with standard 128-character ASCII only (no extended ASCII or ANSI character sets). This makes Blyant compatible with all standard monospaced fonts, regardless of the platform. The files can contain any Unicode text.

## About Blyant
When Blyant starts, the start page offers New file, File browser, New project and Open project. A project is a folder anywhere on your computer: a new one is made in a folder you pick, and a folder you already have can be added. It is worked on in the file browser, where text files (.md, .txt, .html ...) are opened in the editor. The file browser also manages files and directories, and keeps your favorites, the file history and a directory hotlist. Blyant can be set to start directly in the file browser.

Blyant is operated in the style of Vim: the editor has Normal, Insert and Visual modes, and the menus and lists use the same keys for moving around (`h` `j` `k` `l`), searching (`/`), help (`?`) and commands (`:`).

## Guides
- [Download and installation](Guide-to-installation.md)
- [Start page, projects and the file browser](Guide-to-start-page.md)
- [Jump menu, color schemes and logos](Guide-to-jump-menu.md)
- [Corkboard](Guide-to-corkboard.md)
- [Editor](Guide-to-editor.md)
- [Writing with Typst](Guide-to-typst.md)
- [Shell](Guide-to-shell.md), and a [shell tutorial](Guide-to-shell-tutorial.md)
- [Changes](CHANGELOG.md)

The guides are also put together as a **user manual**, in two forms:
- **Markdown** (`Blyant-User-Manual.md`), in every release archive next to the program, to read in Blyant itself
- **PDF** (`Blyant-User-Manual-1.5.0.pdf`), made with [Typst](https://typst.app), with a contents page and page numbers: a download of its own on the [releases page](https://github.com/artstisen/blyant/releases), to read or print

Inside Blyant, press `?` for the list of keys.

## Specifications
| | |
|---|---|
| Supported platforms | Windows, Linux and macOS |
| Requirements | None for the single-file builds (the .NET runtime is included); the portable build needs the .NET 10 runtime |
| Application type | Text mode |
| Core code base | Agnostic: no OS-specific logic, .NET handles the differences between the platforms |
| Box drawing | Classic ASCII |
| Console I/O encoding | UTF-8 |
| Languages | English built in; German, Danish, Norwegian (bokmål), Swedish, Spanish, French, Italian, Polish, Dutch and Finnish as language files that can be changed, and more can be added (the commands and keys are the same in all languages). The language files are also a pack of their own on the releases page (`Blyant-languages-1.5.0.zip`) |
| Date/time format | ISO 8601 |
| Software license | [MIT](LICENSE) |

## Building from source
Blyant has no dependencies besides .NET itself.

```
dotnet build Blyant.sln
dotnet test Blyant.sln
dotnet run --project Blyant
```

`publish.ps1` builds self-contained single file executables (with the .NET runtime included) for Windows, Linux and macOS, and the lightweight version for all platforms that needs the .NET runtime.

`AGENTS.md` has the rules for working on the code (for people and for coding agents).

## Tools used
- Written in C#, a single binary file, no dependencies, runs in the terminal you already use
- CLI tools: JetBrains Command Line Tools, .NET CLI compiler, PowerShell, and GitHub CLI
- Agentic tools: Claude Opus 5.5 (scripting/testing/code review)
- Code editor: Blyant with syntax highlighting, code completion, and code snippets
- Programming language: C# 14
- Framework: .NET 10
