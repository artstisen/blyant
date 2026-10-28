# Installing and running Blyant
Blyant comes in two kinds of builds, so pick the one that suits you:

- **A single file for your platform** (recommended): the .NET runtime is built into the file, so nothing else has to be installed. It is about 35-40 MB
- **The portable version**: a small version (about 1 MB) that runs on every platform, but needs the .NET 10 runtime to be installed (see [Install .NET](#install-net) below)

## Download and run Blyant
Download the latest Blyant archive for your platform from the [releases page](https://github.com/artstisen/blyant/releases), and unpack it in a directory of your choosing: it holds a folder _Blyant_ with the program in it. Keep the folder _lang_ next to the program: it holds the languages other than English and German.

| Platform | Download |
|---|---|
| Windows (most PCs) | `Blyant-1.1.0-win-x64.zip` |
| Windows on ARM | `Blyant-1.1.0-win-arm64.zip` |
| Linux (most PCs) | `Blyant-1.1.0-linux-x64.tar.gz` |
| Linux on 64-bit ARM (Raspberry Pi with a 64-bit system) | `Blyant-1.1.0-linux-arm64.tar.gz` |
| Linux on 32-bit ARM (Raspberry Pi with a 32-bit system) | `Blyant-1.1.0-linux-arm.tar.gz` |
| macOS on Apple silicon (M1 and later) | `Blyant-1.1.0-osx-arm64.tar.gz` |
| macOS on Intel | `Blyant-1.1.0-osx-x64.tar.gz` |
| Any platform, with .NET 10 installed | `Blyant-1.1.0-portable.zip` |

On Linux and macOS, unpack it with ```tar -xzf Blyant-1.1.0-linux-x64.tar.gz``` (with the name of the file you downloaded): the program can then be run as it is.

### The single file
Open your favorite terminal, go to the Blyant directory and start the program:

- **Windows**: type ```Blyant.exe``` (or ```.\Blyant.exe``` in PowerShell)
- **Linux and macOS**: type ```./Blyant``` (Linux and macOS are case sensitive). If it was not unpacked with `tar` (some programs leave out that it can be run), make it executable once with ```chmod +x Blyant```. On macOS the program is not signed: if macOS refuses to open it, allow it once with ```xattr -d com.apple.quarantine Blyant```

### The portable version
Unzip `Blyant-1.1.0-portable.zip`: its folder _Blyant_ has _Blyant.dll_, _Blyant.deps.json_, _Blyant.runtimeconfig.json_ and the folder _lang_. Open your favorite terminal, go to the Blyant directory and type ```dotnet Blyant.dll``` (Linux and macOS are case sensitive).

## Install .NET
Only the portable version needs this. Ensure that you have the .NET 10 runtime installed on your system. If not you can get it here:

- [Install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
- [Install .NET on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos)
- [Download the .NET Runtime 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Good to know
Blyant keeps its settings and lists (favorites, history, bookmarks, snippets, macros ...) in a directory named "data", next to the Blyant program, so the same settings are used wherever Blyant is started from. If the directory you start Blyant in has a directory "data" of its own with Blyant's settings in it (`settings.ini`), that one is used instead (a folder "data" of something else, as of a web site, is left alone). Your projects are folders wherever you choose: a new project is made in a folder you pick, and folders you already have can be added to the projects. Keep the program in a folder of its own, not with your projects: if the program folder is replaced by a new version, or removed, nothing of your writing goes with it. The data directory also holds the log, `log.txt`. If the program directory cannot be written to (a program installed for all users), or its "data" was made by another user (Blyant run once with `sudo`), the data directory is "Blyant/data" in your application data (`~/.local/share` on Linux, `AppData\Local` on Windows). The languages other than English and German are files in the folder "lang" next to the program (see [Language files](#language-files)). To use spell checking in the editor, place spell files in a directory named "spell" next to it (see the [guide to the editor](Guide-to-editor.md#spell-checking)). Blyant needs a terminal window of at least 80 columns and 24 rows, the size of a classic terminal (it uses 79 by 23, and keeps the last column and row of the window free). A terminal that opens at 80 by 24, as LXTerminal on a Raspberry Pi or a Linux console does, is enough. With _Automatic size_ off and a size set by hand, a window that is not larger than that size is told about when Blyant starts (and when the window is made smaller), and you can let the size follow the window.

## Starting Blyant with a file
Give a file or a directory when you start Blyant, and it is opened at once, without the start page:

```
blyant notes.md                Open notes.md in the editor (the file browser is in its directory behind it)
blyant ~/projects/site         Show the directory in the file browser
blyant .                       Show the directory you are in, in the file browser
blyant --help                  Show how Blyant is started (also -h)
blyant --version               Show the version (also -v)
```

A path that is not a full path is taken from the directory you are in. Closing the editor (`:q`) shows the file browser in the directory of the file, and closing the file browser the start page, as after a jump from the [Jump menu](Guide-to-jump-menu.md#jump-menu); `:exit` leaves Blyant. A file that is not there yet (in a directory that is) is made if you want that: Blyant asks first. A word starting with `-` is an option, not a file to make (unless a file or directory of that name is there): an option Blyant does not know is told, with the usage.

(On Windows the program is `Blyant.exe`, and the portable version is started with `dotnet Blyant.dll notes.md`.)

## Language files
English and German are built into Blyant. The other languages are text files in a folder named `lang` next to the Blyant program: `da.txt` (Danish), `nb.txt` (Norwegian), `sv.txt` (Swedish), `es.txt` (Spanish), `fr.txt` (French), `it.txt` (Italian), `pl.txt` (Polish), `nl.txt` (Dutch) and `fi.txt` (Finnish). They come with Blyant; keep the folder next to the program when you copy or move it. If it is missing, those languages are not offered (and Blyant starts in English if one of them was chosen, keeping the choice for when the file is back).

The language files can also be downloaded on their own, as the **language file pack** on the [releases page](https://github.com/artstisen/blyant/releases) (`Blyant-languages-1.1.0.zip`): the folder `lang` with the language files, the German file that is built into Blyant (in the folder `built-in`, to change the German translation), and a read-me. Unzip it next to the Blyant program to put the folder `lang` in place. In the source, the files are `Blyant/Lang/External` (and `Blyant/Lang/de.txt`), and `./publish.ps1 languages` makes the pack in _publish/languages_.

Blyant also looks for a folder `lang` in the data directory, which is used first. A language file there (or next to the program):

- **Changes a translation**: copy the file (or `de.txt` from the source, `Blyant/Lang`) into the folder, and edit the translations. A file for German is used instead of the one built in, and the list of languages shows `(lang)` after it
- **Adds a language**: a file named by the code of the language (`pt.txt`, `cs.txt`, `pt-br.txt`, in any letter case: `PT-BR.txt`) is found by itself and offered in the list of languages, with how much of it is translated. Its first line names it: `# Blyant: Português (pt)`

The files are UTF-8 text, with each text in English and translated below it:

```
en: "File deleted"
pl: "Plik usunięty"
```

A translation left empty, or missing, is shown in English. Keep the placeholders (`{0}`), the keys and the commands as they are. In the source, `python tools/langfile.py pl path/to/pl.txt` makes a file with all the texts of Blyant (or brings one up to date, keeping the translations), and `python tools/checklang.py pl path/to/pl.txt` checks it. The list of languages is read again each time it is opened, so a file can be changed and tried without restarting Blyant.

## Building from source
With the .NET 10 SDK installed:

```
dotnet build Blyant.sln -c Release
```

The files to run are then found in _Blyant/bin/Release/net10.0_.

To build executables that include the .NET runtime (so nothing has to be installed to run them), run ```./publish.ps1``` with PowerShell. It creates a single file for each platform in the directory _publish_, and the lightweight version for all platforms (which needs the .NET runtime, and is started with ```dotnet Blyant.dll```) in _publish/portable_. It also packs each of them for a release in _publish/release_: a zip for Windows and the portable version, and a tar.gz for Linux and macOS (where the program is marked as one that can be run), each with a folder _Blyant_, and the language file pack (`Blyant-languages-1.1.0.zip`). `./publish.ps1 win-x64,languages` builds only the ones named. It needs PowerShell 7.3 or later. The tests are run first, and nothing is published when one fails (`-SkipTests` leaves them out); the version must be the same in `Blyant.csproj` (`Version`, `AssemblyVersion`, `FileVersion`) and in `DialogText.ApplicationVersion`. The archives of a version that is published again are replaced, also those of an earlier version of Blyant, so only the new ones are in _publish/release_.
