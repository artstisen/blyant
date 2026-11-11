# Rules for working on Blyant

For anyone changing the code, people and coding agents alike.

## Blyant and Marbles
Blyant starts from the code of Marbles v1.5.3, which goes on as a program of its own (in its own repository). Blyant is a Markdown-focused tool for writing and for managing projects and their files: the encrypted data files of Marbles (`.mbls`), its login page and Note Management are not part of it. A project is a folder anywhere, kept in the list of projects (`projects.txt` in the data directory). Lines that Marbles wrote in the lists for its data files (`data` in `jump.txt` and `bookmarks.txt`) are skipped when read, and comments that tell what Marbles did name Marbles on purpose.

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
Text shown to the user is written in English in the code and given to `Lang.T("...")`, or `Lang.F("... {0} ...", value)` when it has values in it. Only English is built into the program. The other languages, German too, are files in `Blyant/Lang/External/` (`de.txt`, `da.txt` ...), published in the folder `lang` next to the program.

- Write the whole text in the call, as one text: `Lang.F("{0} files found", count)`, not `count + " files found"` (word order differs between languages), and not `Lang.T(condition ? "a" : "b")` (write two calls). A name made while running (a color, a month) is listed in `Lang.Names`
- Keep texts short: leave out "the" where the text reads well without it, as in labels, settings and key hints (`List commands above command line`, not `List the commands above the command line`)
- The same goes for the headings and titles of the Markdown files (the guides, `README.md`, `CHANGELOG.md` and the other files for GitHub) and of the user manual (the titles in `tools/manual.py`): `Trash`, not `The trash`; `Syntax colors`, not `Colors of the text`. Keep "the" after a verb or a preposition where English needs it (`Arranging the cards`, `On the corkboard`). A heading renamed changes its anchor: change the links to it too (in all the Markdown files, and the parts in `tools/manual.py`)
- What stays English in all languages: commands, keys, the names of the editor modes (NORMAL, INSERT, VISUAL ...), the settings file, the log, and the guides. Keys are not translated in key hints either (`j/k Select`: only `Select` is)
- After adding or changing texts, run `python tools/langfile.py xx` for each language file (de, da, nb, sv, es, fr, it, pl, nl, fi): new texts get an empty translation, which is shown in English until it is filled in. It also writes `Blyant/Lang/texts.txt`, the texts of the code, which is built into the program to count how much of a language file is translated (`LangTests` fails when it is out of date). Then translate them, and run `python tools/checklang.py xx` (0 errors; its warnings point at translations that may be too long for the screen)
- The language files are copied next to the program when it is built: build again to see a change in a file
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

## Linux and macOS
Blyant is written and tested on Windows, but it must work the same on Linux and macOS, and those cannot be tried on this computer. Code behind `!OperatingSystem.IsWindows()` and tests marked `[UnixFact]` do not run on Windows: only the GitHub workflow `Build and test` runs them, on all three systems.

- Do not take the behavior of the file system from Windows. `File.Exists` is true on Linux and macOS for a link to a file that is not there (on Windows it is false), names of files are case-sensitive on Linux, but on macOS usually not (its file systems take `Plot` and `plot` as the same name, as Windows does): code must work both ways, and a test of names that differ only in case first checks that the file system tells them apart. A name starting with `.` is hidden, and the separator is `/`: use `Path.Combine`, never a written `\`
- On Windows a path that starts with `/` or `\` without a drive (`/docs/plan.md`) is taken from the drive Blyant was started on, which can be another than the drive of the file it is about (on the GitHub workflow they differ: `C:` and `D:`). Take the drive from a path you know (`Path.GetPathRoot` of the file), never from `Path.GetFullPath` alone
- A change to files, paths, links or the terminal gets tests of what differs between the systems, also a `[UnixFact]` when it cannot be tried on Windows
- Passing tests on Windows say nothing about the rest: look at the workflow, also for a change that seems to be about Windows only

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
- The version is set in `DialogText.ApplicationVersion` and in `Blyant.csproj`: `Version` (the product version, `1.0.0`), `AssemblyVersion` and `FileVersion` (`1.0.0.0`). Change all four together
- `publish.ps1` builds the `publish` folder: it runs the tests first, and stops if the four versions differ
- `publish.ps1` also makes the user manual from the guides (`tools/manual.py`, with Typst for the PDF). The manual takes the parts of the guides by their headings, in the order of `CHAPTERS` in the script: a part added to a guide, or a heading renamed, is given its place there in the same change (the script stops on a part it does not take). Run `python tools/manual.py --pdf` to see the manual in `publish/manual`. The manual says "chapter" where a guide says "guide" ("the guide to the editor" becomes "the chapter on the editor"): the script does this each time (`as_manual`), so write the guides as guides
- A release is made in this order: push `main`, wait until `Build and test` has passed on Windows, Linux and macOS, and only then tag it and publish the release. The tests on Windows do not cover Linux and macOS (see [Linux and macOS](#linux-and-macos))
- Before anything is pushed, check what goes to GitHub: the files, the commits to push (`git log origin/main..`, author and committer too) and the release archives:
  - **No sensitive information**: no private email address, password, API key, token, private key, and no personal path (`C:\Users\<name>`) or data of the author's own. Commits are made with the GitHub address (`<id>+<user>@users.noreply.github.com`), never a private one. Search for these before pushing, also in the archives of `publish/release`
  - **Only files of the project**: no files of the computer it was made on (scratch files, scripts and tryouts of a session, logs, `data` folders, `publish`, a `.env`), and nothing that has no place in the release. Look at the list of files changed since the last release (`git diff --stat vX.Y.Z..`): each must belong to a change in the changelog. Files that must never come along go in `.gitignore`
- Every release has SHA256 checksums: `publish.ps1` writes `Blyant-<version>-checksums.txt` in `publish/release`, a line for each file of the release (the archives, the language pack and the manual). Check it before uploading (`sha256sum -c Blyant-<version>-checksums.txt` in `publish/release`: every line `OK`), and upload it with the other files. The installation guide tells how a download is checked ([Check the download](Guide-to-installation.md#check-the-download))
