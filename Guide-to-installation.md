# Installing and running Blyant
Blyant comes in two kinds of builds, so pick the one that suits you:

- **A single file for your platform** (recommended): the .NET runtime is built into the file, so nothing else has to be installed. It is about 35-40 MB
- **The portable version**: a small version (about 1 MB) that runs on every platform, but needs the .NET 10 runtime to be installed (see [Install .NET](#install-net) below)

## Download and run Blyant
Download the latest Blyant archive for your platform from the [releases page](https://github.com/artstisen/blyant/releases), and unpack it in a directory of your choice: the archive holds a folder _Blyant_ with the program.

Keep the folder _lang_ next to the program: it holds the languages other than English.

Next to the program is also the user manual, _Blyant-User-Manual.md_ (all the guides of Blyant in one file), which you can read in Blyant itself. The manual is also on the releases page as a PDF (`Blyant-User-Manual-1.6.3.pdf`), to read or print.

| Platform | Download |
|---|---|
| Windows (most PCs) | `Blyant-1.6.3-win-x64.zip` |
| Windows on ARM | `Blyant-1.6.3-win-arm64.zip` |
| Linux (most PCs) | `Blyant-1.6.3-linux-x64.tar.gz` |
| Linux on 64-bit ARM (Raspberry Pi with a 64-bit system) | `Blyant-1.6.3-linux-arm64.tar.gz` |
| Linux on 32-bit ARM (Raspberry Pi with a 32-bit system) | `Blyant-1.6.3-linux-arm.tar.gz` |
| macOS on Apple silicon (M1 and later) | `Blyant-1.6.3-osx-arm64.tar.gz` |
| macOS on Intel | `Blyant-1.6.3-osx-x64.tar.gz` |
| Any platform, with .NET 10 installed | `Blyant-1.6.3-portable.zip` |

On Linux and macOS, unpack the archive with ```tar -xzf Blyant-1.6.3-linux-x64.tar.gz``` (use the name of the file you downloaded): the program is then ready to run.

### Single file
Open your favorite terminal, go to the Blyant directory and start the program:

- **Windows**: type ```Blyant.exe``` (or ```.\Blyant.exe``` in PowerShell)
- **Linux and macOS**: type ```./Blyant``` (Linux and macOS are case sensitive). If you unpacked it with something other than `tar` (some programs drop the permission to run it), make it executable once with ```chmod +x Blyant```. On macOS the program is not signed: if macOS refuses to open it, allow it once with ```xattr -d com.apple.quarantine Blyant```

### Portable version
Unzip `Blyant-1.6.3-portable.zip`: the folder _Blyant_ in it has _Blyant.dll_, _Blyant.deps.json_, _Blyant.runtimeconfig.json_ and the folder _lang_. Open your favorite terminal, go to the Blyant directory and type ```dotnet Blyant.dll``` (Linux and macOS are case sensitive).

### Check the download
The releases page also has `Blyant-1.6.3-checksums.txt`: the SHA256 checksum of each file in the release. With it you can check that a file arrived whole and unchanged. Put it in the folder with your download, and:

- **Linux**: `sha256sum -c Blyant-1.6.3-checksums.txt --ignore-missing` checks the files you downloaded, and says `OK` for each
- **macOS**: `shasum -a 256 -c Blyant-1.6.3-checksums.txt --ignore-missing` does the same
- **Windows**: `Get-FileHash Blyant-1.6.3-win-x64.zip` in PowerShell shows the checksum: compare it with the line for that file in `Blyant-1.6.3-checksums.txt` (upper or lower case does not matter)

## Install .NET
Only the portable version needs this. Make sure the .NET 10 runtime is installed on your system. If it is not, you can get it here:

- [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
- [Install .NET on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [Download the .NET Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Good to know
Blyant keeps its settings and lists (favorites, history, bookmarks, snippets, macros ...) in a directory named "data", next to the Blyant program, so the same settings are used wherever Blyant is started from. If the directory you start Blyant in has a directory "data" with Blyant's settings in it (`settings.ini`), that one is used instead (a folder "data" that belongs to something else, such as a web site, is left alone).

Your projects are folders wherever you choose: a new project is made in a folder you pick, and folders you already have can be added to the projects. Keep the program in a separate folder, not with your projects: if the program folder is replaced by a new version, or removed, none of your writing goes with it.

The data directory also holds the log, `log.txt`. If the program directory cannot be written to (as for a program installed for all users), or the "data" directory there was made by another user (when Blyant was run once with `sudo`), the data directory is "Blyant/data" in your application data (`~/.local/share` on Linux, `AppData\Local` on Windows).

The languages other than English and German are files in the folder "lang" next to the program (see [Language files](#language-files)). To use spell checking in the editor, place spell files in a directory named "spell" next to the program (see the [guide to the editor](Guide-to-editor.md#spell-checking)).

Blyant needs a terminal window of at least 80 columns and 24 rows, the size of a classic terminal (it uses 79 by 23, and keeps the last column and row of the window free). A terminal that opens at 80 by 24, as LXTerminal on a Raspberry Pi or a Linux console does, is enough. With _Automatic size_ off and a size set by hand, Blyant tells you when the window is too small for that size (when Blyant starts, and when the window is made smaller), and asks whether the size should follow the window.

## Starting Blyant with a file
Give a file or a directory when you start Blyant, and it is opened at once, without the start page:

```
blyant notes.md                Open notes.md in the editor (the file browser is in its directory behind it)
blyant +12 notes.md            Open notes.md at line 12 (also blyant notes.md +12)
blyant ~/projects/site         Show the directory in the file browser
blyant .                       Show the directory you are in, in the file browser
blyant --shell                 Use Blyant on lines: commands typed at a prompt (help lists them)
blyant --run "format a.html"   Run a command of the shell (more than one: --run "..." --run "..."), and end
blyant --screen                Start on the full screen, when Blyant is set to start in the shell
blyant --shell --yes < a.txt   Answer yes to the questions of commands not typed (also with --run)
blyant --help                  Show how Blyant is started (also -h)
blyant --version               Show the version (also -v)
```

A relative path is taken from the directory you are in. `+12` opens the file at line 12, as in Vim and other editors: a line past the end opens the last line. If a file named `+12` exists, that file is opened, and Blyant reports `+12` without a file as an error. You can give one file or directory at a time. The [guide to the shell](Guide-to-shell.md) describes the shell options, and the [shell tutorial](Guide-to-shell-tutorial.md) shows how to work with the shell.

Programs that open a file in your editor at a line can use Blyant this way, as [Blyant Validator](#blyant-validator) does.

If the file does not exist yet (but the directory does), Blyant asks whether to make it. A word starting with `-` is read as an option, not as a file to make (unless a file or directory of that name exists), wherever it is given (`blyant +12 --help`): for an unknown option, Blyant says so and shows the usage.

(On Windows the program is `Blyant.exe`, and the portable version is started with `dotnet Blyant.dll notes.md`.)

## Blyant Validator
[Blyant Validator](https://github.com/artstisen/blyant-validator) is a companion tool to Blyant: in the terminal, it validates HTML, CSS, XML, Markdown and Typst, and formats HTML, CSS and XML. It checks each file in a folder again when the file is saved, so the errors and warnings are up to date while you write. It checks links to files and to places in a page, and with `--accessibility` (or `:accessibility`) gives hints for accessible HTML: links and buttons without text, form fields without a label, heading levels that skip. `:typst` turns a Typst document into a PDF (with the Typst CLI, see the [guide to Typst](Guide-to-typst.md#install-the-typst-cli)). It also works alone, but with Blyant as its editor the two work as one: each problem the validator finds opens in Blyant at the line with the problem, and you are back in the validator when you are done.

Set Blyant as the editor in Blyant Validator:

```
:editor "C:\path\to\Blyant.exe" +{line}
```

On Linux and macOS, give the path of `Blyant`. Blyant then opens the file at the line of the problem.

When you are done:

- `:wq` saves and closes the file
- `:exit` (or `:qa`) leaves Blyant at once and takes you back to Blyant Validator. If the file has unsaved changes, you are asked to save or discard them first (or to stay)

`:exit` works on every command line in Blyant, so it also takes you back from a page you went to while editing. The one exception is a page opened on top of the editor (the corkboard, the Jump menu): while the file has unsaved changes, `:exit` is refused there, so close the page and save the file first.

Closing the editor with `:q` instead shows the file browser in the directory of the file, and closing the file browser shows the start page, as after a jump from the [Jump menu](Guide-to-jump-menu.md#jump-menu).

## Language files
English is built into Blyant. The other languages are text files in a folder named `lang` next to the Blyant program: `de.txt` (German), `da.txt` (Danish), `nb.txt` (Norwegian), `sv.txt` (Swedish), `es.txt` (Spanish), `fr.txt` (French), `it.txt` (Italian), `pl.txt` (Polish), `nl.txt` (Dutch) and `fi.txt` (Finnish).

They come with Blyant; keep the folder next to the program when you copy or move the program. If the folder is missing, those languages are not offered (and if one of them was chosen, Blyant starts in English and keeps the choice for when the file is back).

The language files can also be downloaded separately, as the **language file pack** on the [releases page](https://github.com/artstisen/blyant/releases) (`Blyant-languages-1.6.3.zip`): the folder `lang` with the language files, and a read-me. Unzip it next to the Blyant program to put the folder `lang` in place. In the source, the files are in `Blyant/Lang/External`, and `./publish.ps1 languages` makes the pack in _publish/languages_.

Blyant also looks for a folder `lang` in the data directory, and uses it first. A language file there (or next to the program):

- **Changes a translation**: copy the file into the folder in the data directory, and edit the translations: your copy is used instead of the file next to the program, which a new version of Blyant replaces
- **Adds a language**: a file named after the language code (`pt.txt`, `cs.txt`, `pt-br.txt`, in any letter case: `PT-BR.txt`) is found automatically and offered in the list of languages, showing how much is translated. The first line gives the name of the language: `# Blyant: Português (pt)`

The files are UTF-8 text, with each text in English and the translation below it:

```
en: "File deleted"
pl: "Plik usunięty"
```

A translation left empty, or missing, is shown in English. Keep the placeholders (`{0}`), the keys and the commands as they are. In the source, `python tools/langfile.py pl path/to/pl.txt` makes a file with all the texts of Blyant (or brings one up to date, keeping the translations), and `python tools/checklang.py pl path/to/pl.txt` checks it. The list of languages is read again each time it is opened, so you can change a file and try it without restarting Blyant.

## Building from source
With the .NET 10 SDK installed:

```
dotnet build Blyant.sln -c Release
```

The program files are then in _Blyant/bin/Release/net10.0_.

To build executables that include the .NET runtime (so nothing has to be installed to run them), run ```./publish.ps1``` with PowerShell. It creates a single file for each platform in the directory _publish_, and the lightweight version for all platforms (which needs the .NET runtime, and is started with ```dotnet Blyant.dll```) in _publish/portable_.

It also packs each build for a release in _publish/release_: a zip for Windows and the portable version, and a tar.gz for Linux and macOS (where the program is marked as executable), each with a folder _Blyant_, and the language file pack (`Blyant-languages-1.6.3.zip`). `./publish.ps1 win-x64,languages` builds only the ones named.

It needs PowerShell 7.3 or later. It runs the tests first, and publishes nothing if one fails (`-SkipTests` leaves them out); the version must be the same in `Blyant.csproj` (`Version`, `AssemblyVersion`, `FileVersion`) and in `DialogText.ApplicationVersion`.

When a build is published again, its earlier archives are replaced, also those made for an earlier version of Blyant, so only the new ones are in _publish/release_.
