# Rules for working on Blyant

For anyone changing the code, people and coding agents alike.

## Blyant and Marbles
Blyant starts from the code of Marbles v1.5.3, which goes on as a program of its own (in its own repository). Blyant is a Markdown-focused tool for writing and for managing projects and their files: the encrypted data files of Marbles (`.mbls`), its login page and Note Management are not part of it. A project is a folder in the projects folder (`Projects`, setting `projectsdir`). Lines that Marbles wrote in the lists for its data files (`data` in `jump.txt` and `bookmarks.txt`) are skipped when read, and comments that tell what Marbles did name Marbles on purpose.

## Commands
Every place with a command line (start page, file browser, favorites, history, hotlist, search results, Jump menu, editor) has a table of its commands in `Blyant/Commands.cs`. `Tab` completes from these tables, and the log uses them to tell a command from other text.

**When a command is added, renamed or removed, change the table of that place in `Commands.cs` in the same change.**

- A command goes in the first list of its table (the names `Tab` completes to). A short form of it (`q` for `quit`) goes in the second list, which is accepted but not offered
- A command used in several places goes in the table of each of them
- A new place with a command line gets a value in `CommandContext`, a table, and passes its context to `CommandLine.Read`
- `CommandTests` compares the tables with the code that runs the commands (it reads the source files), and fails with the names that differ. If the code that runs the commands of a place is moved or restructured, update where that test looks
- Also add the command to the help (`Tools.cs`, the section of the place) and to the guide that covers it
- When what a key or command does changes (a key gets another meaning, a command does more or is refused in a case), update the help rows of the place, its key hints on the bottom line, the guide and `CHANGELOG.md` in the same change, and the language files when a text changes
- The short guides in the program (the Help tab of the start page: `QuickGuide.cs` and `QuickGuide.Guides.cs`) show the most used keys of projects, the file browser, the editor and the Jump menu. When such a key or command changes, update its guide too

## Texts and languages
Text shown to the user is written in English in the code and given to `Lang.T("...")`, or `Lang.F("... {0} ...", value)` when it has values in it. German is built into the program (`Blyant/Lang/de.txt`); the other languages are files in `Blyant/Lang/External/` (`da.txt` ...), published in the folder `lang` next to the program.

- Write the whole text in the call, as one text: `Lang.F("{0} files found", count)`, not `count + " files found"` (word order differs between languages), and not `Lang.T(condition ? "a" : "b")` (write two calls). A name made while running (a color, a month) is listed in `Lang.Names`
- What stays English in all languages: commands, keys, the names of the editor modes (NORMAL, INSERT, VISUAL ...), the settings file, the log, and the guides. Keys are not translated in key hints either (`j/k Select`: only `Select` is)
- After adding or changing texts, run `python tools/langfile.py xx` for each language file (de, da, nb, sv, es, fr, it, pl, nl, fi): new texts get an empty translation, which is shown in English until it is filled in. Then translate them, and run `python tools/checklang.py xx` (0 errors; its warnings point at translations that may be too long for the screen)
- The German file is built into the program, and the others are copied next to it: build again to see a change in a file
- The screen is 79 columns (a window of 80x24 is enough: the last column and row of the window are not used): keep translations about as long as the English. Key hints, help rows and page footers that are too long are cut at the edge (`Tools.FitLine`), labels in boxes take the width of their text, and nothing may be placed at a fixed column after a text that can be translated
- `LangTests` checks the language files against the code (missing or unknown texts, placeholders, keys, blank space) and fails on text written to the screen without `Lang.T`. A line that is meant to stay English gets a comment containing "English"
- Text that is kept (a field, a list made at startup) is not translated when the language changes: look it up when it is shown

## Checks before a change is done
- `dotnet test Blyant.sln` passes
- The change is tried in the running program, not only in tests
- Help (`?` / `:help`), the guides (`Guide-to-*.md`) and `CHANGELOG.md` describe the change
- A new setting is read and written by name in `FileIo` and counted in `SettingsTests`

## Code
- Source files are UTF-8 with BOM and CRLF line endings. Markdown files have LF
- No dependencies besides .NET itself
- `EditorEngine`, `TextBuffer`, the highlighters and `CommandCompletion` do not touch the console, so they can be tested. Keep it that way: drawing belongs in `TextArea` and the pages
- Text shown to the user is kept in `DialogText` (or written in a `Lang.T` call where it is used)
- The log (`EventLog`) can be read by anyone at the screen: never log the text of files or search texts. Commands are logged by name only. It is also written to `log.txt` in the data directory

## Files
The files of the user, and Blyant's own files, must never be left empty or half written, whenever Blyant is stopped (the terminal closed, the computer turned off).

- A file that is there is replaced with `FileIo.WriteAllBytesSafe`: it writes a temporary file next to it, flushes it to the disk, and then puts it in the place of the file in one step. Never `File.WriteAllText`, `File.WriteAllBytes`, `FileMode.Create` or `FileMode.Truncate`, which empty the file first
- A new file is made with `FileMode.CreateNew`, so a file that has come since it was looked for is not emptied
- A list that only grows (`log.txt`, `trash.txt` in the trash, the words added to the spell checker, the paste bin when added to) is appended to (`File.AppendAllText`): what is in it is not written again, so an interrupted write can only lose the line being added
- Deleting in the file browser goes to the trash (`FileOperations.MoveToTrash`) unless the setting `deletetotrash` is off, and is always asked about first, in a box that a word typed by mistake cannot answer (`ChoiceBox.ConfirmGuarded`)
- What only shows a file (a peek, a comparison) never writes it
- Every file operation is logged (the path, never what is written). Blyant's own operations go through `FileIo`: `WriteAllBytesSafe`, `CreateNewFile`, `CreateDirectory`, `DeleteFile`, `MoveFile`, `CopyFile` and `AppendText` log what they do, and why it failed. The operations of the file browser (`FileOperations`) are logged by the file browser, as a whole (one line for a directory copied)
- `SafeWriteTests` reads the source and fails on the ways of writing that empty a file first, and on file operations outside `FileIo`, `FileOperations` and `EventLog`

## Releases
- The version is set in `DialogText.ApplicationVersion` and in `Blyant.csproj`: `Version` (the product version, `0.1.0`), `AssemblyVersion` and `FileVersion` (`0.1.0.0`). Change all four together
- `publish.ps1` builds the `publish` folder: it runs the tests first, and stops if the four versions differ
