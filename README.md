# Blyant
A free, open-source, cross-platform, Vim-style modal text editor for writing in any terminal program on Windows, Linux and macOS. _Blyant_ is Danish for _pencil_.

Blyant is a sister application of [Marbles](https://github.com/artstisen/marbles), the Vim-style editor for encrypted notes, and starts from its code base (Marbles v1.5.3). Where Marbles is about private notes kept in an encrypted data file, Blyant is about writing: Markdown files, organised in projects.

> **Status: in progress.** The encrypted notes of Marbles are gone, and Blyant starts with files and projects. A project is a folder of files for now; a page for managing the files of a project is planned.

## Key features

**Files and projects**
- The start page: New file, Open file, New project and Open project, a key each
- A project is a folder with the files of a piece of writing, kept in a projects folder of your choosing
- Your files are ordinary text files on your own computer, to back up, sync and open with any other program

**A Vim-style editor**
- Normal, Insert and Visual modes (also block selection), counts, text objects, registers and undo
- Split view: the same text in two panes side by side, remembered when you open it again
- Syntax highlighting (Markdown, HTML, PHP, CSS, JavaScript, C#, JSON, XML), spell checking and word completion
- Search and replace with regular expressions, file comparison (diff), snippets, and a macro recorder whose macros can be edited as text
- A command line with `Tab` completion and command history on every page

**A file manager**
- An orthodox file browser with two panes: create, rename, copy, move, delete and compare
- Favorites (with folders and colors), file history, a directory hotlist and file search by name or text
- Peek at a file without opening it, and open any file at the place you left it

**Quick to get around**
- The Jump menu (`Ctrl+O`): bookmarks, recent places, files, directories and pages in a few keys
- The same keys everywhere: `h` `j` `k` `l` to move, `/` to search, `?` for help and `:` for commands
- Built-in guides: a quick guide and short guides to each part of Blyant

**At home in any terminal**
- Pure 7-bit ASCII interface that works with any monospaced font
- Color schemes and logos, which you can edit and make your own
- English and German built in, and nine more languages as files that can be changed or added to
- Runs on Windows, Linux (also Raspberry Pi) and macOS, as a single file with nothing else to install

## Getting started
1. Download the build for your platform from the [releases page](https://github.com/artstisen/blyant/releases), and unzip it into a directory of your choice.
2. Start `Blyant` (or `Blyant.exe` on Windows) in your terminal. `blyant .` opens the file browser in the directory you are in, and `blyant chapter.md` opens the file in the editor.
3. On the start page, press `P` for a new project or `N` for a new file. Press `?` on any page to see its keys, or open the Quick guide from the start page.

See the [guide to installation](Guide-to-installation.md) for the builds and the details.

## Pure 7-bit ASCII TUI
The aim is to make an unbloated set of core tools. I've included functionality that I would normally have added via lightweight, functional plugins and init modifications in Neovim, without disrupting the classic terminal aesthetic.

The interface of Blyant is drawn with standard 128-character ASCII only (no extended ASCII or ANSI character sets). This makes Blyant compatible with all standard monospaced fonts, regardless of the platform. The files can contain any Unicode text.

## About Blyant
When Blyant starts, the start page offers New file, Open file, New project and Open project. A project is a folder in the projects folder (by default `Blyant-Projects` in your Documents folder), and it is worked on in the file browser, where text files (.md, .txt, .html ...) are opened in the editor. The file browser also manages files and directories, and keeps your favorite files, the file history and a directory hotlist. Blyant can be set to start directly in the file browser.

Blyant is operated in the style of Vim: the editor has Normal, Insert and Visual modes, and the menus and lists use the same keys for moving around (`h` `j` `k` `l`), searching (`/`), help (`?`) and commands (`:`).

## Windows Terminal
See the [guide to installing the Windows Terminal CRT mod](Guide-to-windows-terminal-crt.md).

## Guides
- [Download and installation](Guide-to-installation.md)
- [Start page, projects and the file browser](Guide-to-start-page.md)
- [Jump menu, color schemes and logos](Guide-to-jump-menu.md)
- [Editor](Guide-to-editor.md)
- [Changes](CHANGELOG.md)
- [PowerShell scripting tips](Guide-to-windows-terminal-crt.md)

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
| Languages | English and German built in; Danish, Norwegian (bokmål), Swedish, Spanish, French, Italian, Polish, Dutch and Finnish as language files that can be changed, and more can be added (the commands and keys are the same in all languages). The language files are also a pack of their own on the releases page (`Blyant-languages-0.1.0.zip`) |
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
- Code editor: Marbles and Blyant
- Programming language: C# 14
- Framework: .NET 10
- Claude Opus 5.5: Scripting/testing/code review
