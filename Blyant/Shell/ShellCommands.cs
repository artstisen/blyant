namespace Blyant;

/// <summary>
/// The commands of the shell (CommandContext.Shell in Commands.cs). Each takes what it works on as its argument: a
/// path, a name, or the number of a row of the last list shown
/// </summary>
internal sealed partial class ShellSession
{
    /// <summary>
    /// The commands, as "help" lists them (the names in Commands.cs)
    /// </summary>
    private static IEnumerable<(string Command, string Text)> HelpRows() => new[]
    {
        ("pwd", Lang.T("Show the directory you are in")),
        ("ls [-a] [-l] [dir|n]", Lang.T("List a directory (-a: hidden ones too, -l: full paths)")),
        ("cd [dir|file|n]", Lang.T("Go to a directory, or that of a file (alone: home)")),
        ("up", Lang.T("Go to the directory above")),
        ("home", Lang.T("Go to your home directory")),
        ("data", Lang.T("Go to the data directory of Blyant (settings, lists, log)")),
        ("open file|n [+line]", Lang.T("Edit a file on the full screen (closing it is back here)")),
        ("peek file|n [lines]", Lang.T("Show the lines of a file (the first 20, 40, 100-120, or +100 30)")),
        ("find [-l] [name] [-t text]", Lang.T("Find files here and below, by name (*.md) and by text")),
        ("new name", Lang.T("Make a new, empty file here (.md if no extension)")),
        ("mkdir name", Lang.T("Make a new directory here")),
        ("rename file|n name", Lang.T("Give a file or directory a new name")),
        ("copy file|n dir|n", Lang.T("Copy a file or directory into a directory")),
        ("move file|n dir|n", Lang.T("Move a file or directory into a directory")),
        ("delete file|n", Lang.T("Delete a file or directory, to the trash (del)")),
        ("info [file|n]", Lang.T("Tell about a file or directory (alone: this one)")),
        ("insert file|n top|bottom|+line text", Lang.T("Put a line of text in a file, without opening it")),
        ("change file|n +line|from-to [text]", Lang.T("Change lines of a file into a text, or delete them")),
        ("shift file|n lines place", Lang.T("Move lines in a file: under a heading, +line, top or bottom")),
        ("shift! file|n lines place", Lang.T("Move lines, and tick or untick their task boxes")),
        ("lines file|n lines > file|n place", Lang.T("Copy lines to another file (cut last: move them)")),
        ("lines file|n lines start|end text", Lang.T("Put a text at the start or the end of lines")),
        ("lines file lines unstart|unend text", Lang.T("Take a text off the start or the end of lines")),
        ("lines file|n lines replace old new", Lang.T("Replace a text in lines, the rest kept")),
        ("format file|n", Lang.T("Format an HTML, CSS or XML file, as :format does")),
        ("trim file|n", Lang.T("Remove blank space at line ends, as :trim does (trim!)")),
        ("set [name [value]]", Lang.T("Show the settings, or change one")),
        ("reset", Lang.T("Reset all settings to the defaults (asks first)")),
        ("reload", Lang.T("Read the settings file and the registers again, after editing them")),
        ("clip [path|name] file|dir|n|.", Lang.T("Copy a path, or a name, to the clipboard")),
        ("clip text file|n", Lang.T("Copy the text of a file to the clipboard")),
        ("clip lines file|n lines", Lang.T("Copy lines of a file (4, 4-6, 4,7, +100 20)")),
        ("clip $name", Lang.T("Copy the value of a register to the clipboard")),
        ("clip ... $clip", Lang.T("The text copied last, in a command")),
        ("reg [name [text|.|#n]]", Lang.T("Keep a text or a path in a register: $name puts it in a command")),
        ("reg -d name|all", Lang.T("Take a register away, or all of them")),
        ("favorites [-l] [list]", Lang.T("List the favorites (fav), or those of another list")),
        ("favorites -l", Lang.T("Lists with full paths (also hist, recent, book)")),
        ("favorites lists", Lang.T("List the lists of favorites (*: the open one)")),
        ("favorites main", Lang.T("Open the main list of favorites again")),
        ("favorites new|delete name", Lang.T("Make a list and open it, or delete one")),
        ("favorites rename name new", Lang.T("Give a list of favorites a new name")),
        ("favorites use name", Lang.T("Open a list named as a word (new, lists ...)")),
        ("favorites folder name", Lang.T("Make a folder of favorites, last in the list")),
        ("favorites subfolder [f] name", Lang.T("Make a subfolder, first in the folder [f]")),
        ("favorites rename [f] name", Lang.T("Give a folder of favorites a new name")),
        ("favorites remove [f]", Lang.T("Remove a folder with its favorites (asks first)")),
        ("favorites move rows|[f] where", Lang.T("Move: top, bottom, up, down, out, 5, [folder]")),
        ("favorites undo", Lang.T("Put back what fav remove removed last")),
        ("favorites add|remove file|n", Lang.T("Add to the favorites, or take off them")),
        ("history [-l]", Lang.T("List the files opened (hist)")),
        ("recent [-l]", Lang.T("List the latest files")),
        ("hotlist", Lang.T("List the directory hotlist (hot)")),
        ("hotlist add|remove [dir|n]", Lang.T("Add a directory to the hotlist (this one), or take it off")),
        ("bookmarks [-l]", Lang.T("List the bookmarks (book)")),
        ("projects", Lang.T("List the projects (proj)")),
        ("project n|name", Lang.T("Go to the directory of a project")),
        ("log [n]", Lang.T("Show the last lines of the log (20)")),
        ("clear", Lang.T("Clear the screen (also cls)")),
        ("screen", Lang.T("Go on on the full screen, in the file browser here")),
        ("version", Lang.T("Show the version of Blyant")),
        ("about [license]", Lang.T("Show the version and the commands used the most, or the license")),
        ("again [n]", Lang.T("List the commands typed, or run one again (!n, !!)")),
        ("help [command]", Lang.T("List the commands, or tell what one does")),
        ("exit", Lang.T("Leave Blyant (also quit, q)"))
    };

    /// <summary>
    /// Run a line typed (a command and what follows it)
    /// </summary>
    /// <returns>False when the shell is to be left</returns>
    internal bool Execute(string line)
    {
        line = line.Trim();
        if (line.Length == 0) return true;
        // "!!", "!3", "again 3": a command typed before, written out and run again (as typed then)
        if (AgainNumber(line) is { } number)
        {
            var kept = CommandHistory.Of(CommandContext.Shell);
            if (number < 1 || number > kept.Count)
            {
                NewPage();
                Error(kept.Count == 0
                    ? Lang.T("No commands typed yet (they are kept while Blyant runs)")
                    : Lang.F("No command {0}: {1} are kept (again lists them)", line, kept.Count));
                return true;
            }

            line = kept[number - 1];
            NewPage();
            Line(line);
        }

        // Typed at the prompt: kept for Up and Down and for "again" (the command an "again" ran, not the "again")
        if (_typed && !CommandLine.Name(line).Equals("again", StringComparison.OrdinalIgnoreCase))
        {
            CommandHistory.Add(CommandContext.Shell, line);
        }

        // By name only (never what follows it: a path, a name, the value of a register)
        EventLog.AddCommand(DialogText.LogShell, line);
        // (the file of the registers edited by hand since: read again, so $name and reg use what is in it)
        ShellRegisters.Refresh();
        // $name: the value of the register (in the paths a value with blanks is one word; the text of insert, change,
        // reg and set, and what find and project take, as it is)
        // $clip: the text clip copied last. Told when nothing is copied yet, and when it has more than one line where
        // one line is taken (only insert and change put in lines, and "clip $clip" copies it again)
        // ("help $clip" tells of it: nothing is put in after help)
        var name = CommandLine.Name(line).ToLowerInvariant();
        var argument = CommandLine.Argument(line);
        var help = name is "help" or "h";
        if (!help && ShellRegisters.Mentions(line, ShellRegisters.ClipName) && ClipProblem(name, line) is { } problem)
        {
            NewPage();
            Error(problem);
            return true;
        }

        // Put in after the line is split into the command and what follows it, so a value is not trimmed: the blank
        // lines and blanks at the end of $clip stay. ("clip $note" alone copies the value as it is: not put in first,
        // where it would be taken as a path)
        if (!help && !IsClipOfRegister(line))
        {
            argument = ShellRegisters.Expand(argument, name switch
            {
                "insert" or "change" => 2,
                "reg" or "set" => 1,
                "find" or "project" => 0,
                // (the text of "lines todo.md 4 start ..." as it is)
                "lines" => LinesWords(argument),
                _ => int.MaxValue
            });
        }

        // (the line of the command typed is the first row of its page)
        NewPage();
        return RunCommand(name, argument);
    }

    /// <summary>
    /// The number of the command a line runs again: "!!" (1, the last), "!3", "again 3"; 0 for "again" with a word
    /// that is not a number (told as no command). Null for any other line ("again" alone lists them)
    /// </summary>
    internal static int? AgainNumber(string line)
    {
        line = line.Trim();
        if (line == "!!") return 1;
        if (line.Length > 1 && line[0] == '!' && line[1..].All(char.IsAsciiDigit))
        {
            return int.TryParse(line[1..], out var bang) ? bang : 0;
        }

        if (!CommandLine.Name(line).Equals("again", StringComparison.OrdinalIgnoreCase)) return null;
        var argument = CommandLine.Argument(line);
        if (argument.Length == 0) return null;
        return argument.All(char.IsAsciiDigit) && int.TryParse(argument, out var number) ? number : 0;
    }

    // "again": the commands typed at the prompt, the newest first, numbered for "again 3" and "!3" (not a last list:
    // "open 3" is still a row of the list shown before)
    private void ListAgain()
    {
        var kept = CommandHistory.Of(CommandContext.Shell);
        if (kept.Count == 0)
        {
            Line(Lang.T("No commands typed yet (they are kept while Blyant runs)"));
            return;
        }

        var width = kept.Count.ToString().Length;
        for (var i = 0; i < kept.Count; i++) Line((i + 1).ToString().PadLeft(width + 2) + "  " + kept[i]);
    }

    // Why $clip cannot be used in a command line (null: it can)
    private static string? ClipProblem(string name, string line)
    {
        if (ShellRegisters.Clip is not { } clip) return Lang.T("Nothing copied yet: clip copies a text for $clip");
        // (change without a text deletes the lines: an empty $clip is not taken for that)
        if (clip.Length == 0 && name == "change") return Lang.T("$clip is empty: change would delete the lines");
        if (!clip.Contains('\n') || IsClipOfRegister(line)) return null;
        return name is "insert" or "change"
            ? null
            : Lang.T("$clip has more than one line: only insert and change take it");
    }

    private bool RunCommand(string name, string argument)
    {
        switch (name)
        {
            case "exit":
            case "quit":
            case "q":
                return false;
            case "pwd":
                Line(Directory);
                break;
            case "ls":
                ListDirectory(argument);
                break;
            case "cd":
                ChangeDirectory(argument);
                break;
            case "up":
                ChangeDirectory("..");
                break;
            case "home":
                ChangeDirectory(string.Empty);
                break;
            case "data":
                // (where it is: next to the program, or in the data of the user)
                ChangeDirectory(Path.GetFullPath(FileIo.GetDataDirectoryName()));
                break;
            case "open":
                Open(argument);
                break;
            case "favorites":
            case "fav":
            case "favs":
                // "fav add 3", "fav remove 3", the lists ("fav lists", "fav new", "fav rename", "fav delete"), else the
                // list ("fav work", "fav use work": another list; "fav main": the main list)
                Favorites(argument);
                break;
            case "history":
            case "hist":
                ListHistory(int.MaxValue, argument);
                break;
            case "recent":
                ListHistory(RecentBox.MaxFiles, argument);
                break;
            case "hotlist":
            case "hot":
                if (Change(argument) is { } addHot) ChangeHotlist(Rest(argument), addHot);
                else ListHotlist();
                break;
            case "peek":
                Peek(argument);
                break;
            case "find":
                Find(argument);
                break;
            case "new":
                NewFile(argument);
                break;
            case "mkdir":
                MakeDirectory(argument);
                break;
            case "rename":
                Rename(argument);
                break;
            case "copy":
                CopyOrMove(argument, false);
                break;
            case "move":
                CopyOrMove(argument, true);
                break;
            case "delete":
            case "del":
                Delete(argument);
                break;
            case "info":
                Info(argument);
                break;
            case "insert":
                Insert(argument);
                break;
            case "shift":
                Shift(argument, false);
                break;
            case "shift!":
                Shift(argument, true);
                break;
            case "lines":
                Lines(argument);
                break;
            case "change":
                ChangeLines(argument);
                break;
            case "format":
                ChangeText(argument, "format");
                break;
            case "trim":
                ChangeText(argument, "trim");
                break;
            case "trim!":
                ChangeText(argument, "trim!");
                break;
            case "set":
                Set(argument);
                break;
            case "reset":
                Reset();
                break;
            case "reload":
                Reload(argument);
                break;
            case "reg":
                Register(argument);
                break;
            case "clip":
                Clip(argument);
                break;
            case "bookmarks":
            case "book":
                ListBookmarks(argument);
                break;
            case "projects":
            case "proj":
                ListProjects();
                break;
            case "project":
                GoToProject(argument);
                break;
            case "log":
                ShowLog(argument);
                break;
            case "version":
                Line(DialogText.ApplicationName + " " + DialogText.ApplicationVersion);
                break;
            case "about":
                About(argument);
                break;
            case "screen":
                // The full screen, in the file browser of this directory (from a file or --run: no screen)
                if (!Terminal)
                {
                    Error(Lang.T("The full screen needs a terminal"));
                    break;
                }

                ScreenDirectory = Directory;
                return false;
            case "clear":
            case "cls":
                // (in a terminal: else there is no screen, and nothing is done)
                ClearScreen?.Invoke();
                break;
            case "again":
                ListAgain();
                break;
            case "help":
            case "h":
                Help(argument);
                break;
            default:
                Error(Lang.F("Unknown command: {0} (help lists the commands)", name));
                break;
        }

        return true;
    }

    // "add ..." (true) or "remove ..." (false) after "fav" or "hot", or null for none of them
    private static bool? Change(string argument) =>
        argument.TrimStart().Split(' ', 2)[0].ToLowerInvariant() switch
        {
            "add" => true,
            "remove" => false,
            _ => null
        };

    // What follows the first word of what follows a command

    private static string Rest(string argument)
    {
        var parts = argument.TrimStart().Split(' ', 2);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }

    #region Places

    // A row that is not in the last list: told with how many rows it has (a list of one row: the file a command worked on)
    private string NoRow(string row) =>
        !_listShown ? Lang.F("No row {0}: no list has been shown", row)
        : LastList.Count == 1 ? Lang.F("No row {0}: the last list has 1 row", row)
        : Lang.F("No row {0}: the last list has {1} rows", row, LastList.Count);

    /// <summary>
    /// The path a command is given: the number of a row of the last list, or a path (not full: from the directory
    /// the shell is in; "~": the home directory). Null (and told) for a number that is not a row
    /// </summary>
    internal Item? Resolve(string argument, bool wantDirectory)
    {
        argument = argument.Trim().Trim('"');
        if (argument.Length > 0 && argument.All(char.IsAsciiDigit) && !Exists(PathOf(argument)))
        {
            if (int.TryParse(argument, out var number) && number >= 1 && number <= LastList.Count)
            {
                var item = LastList[number - 1];
                if (item.Path.Length > 0) return item;
            }

            Error(NoRow(argument));
            return null;
        }

        var path = PathOf(argument);
        return new Item(path, wantDirectory || System.IO.Directory.Exists(path));

        static bool Exists(string path) => File.Exists(path) || System.IO.Directory.Exists(path);
    }

    // A path typed, as a full path: "~" is the home directory, and a path that is not full is taken from the
    // directory the shell is in (on Windows "/docs" from its drive, not from the drive Blyant was started on)
    internal string PathOf(string typed)
    {
        if (typed.Length == 0) return Directory;
        typed = Shell.FromHome(typed);
        try
        {
            // (with a base path, a path from the root without a drive, "/docs" on Windows, is on the drive of the
            // base path: not on the drive Blyant was started on)
            return Path.GetFullPath(typed, Directory);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return typed;
        }
    }

    // "cd notes", "cd 2": a directory; "cd plot.md", "cd 3" on a file (of the favorites, the history, a search ...): the
    // directory of the file, which is then the last list ("open 1"), and where it went is told
    private void ChangeDirectory(string argument)
    {
        var item = argument.Trim().Length == 0 ? new Item(PathOf("~"), true) : Resolve(argument, true);
        if (item == null) return;
        if (!System.IO.Directory.Exists(item.Path) && File.Exists(item.Path) &&
            Path.GetDirectoryName(Path.GetFullPath(item.Path)) is { Length: > 0 } folder)
        {
            GoTo(folder);
            LastList = new List<Item> { new(Path.GetFullPath(item.Path), false, item.At) };
            Line(Lang.F("In {0} (the directory of {1})", Directory, Path.GetFileName(item.Path)));
            return;
        }

        if (!System.IO.Directory.Exists(item.Path))
        {
            Error(Lang.F("Not found: {0}", item.Path));
            return;
        }

        GoTo(item.Path);
    }

    // The directory the shell is in from now on
    private void GoTo(string path)
    {
        Directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (Directory.Length == 0 || !Path.IsPathRooted(Directory)) Directory = Path.GetFullPath(path);
    }

    // "ls", "ls notes", "ls 2": the folders and files of a directory, as the file browser shows them (hidden and
    // system ones left out: on Linux and macOS the names that start with "."); "ls -a": those too
    private void ListDirectory(string argument)
    {
        // "-a": hidden ones too; "-l": the full paths, without the mark of a favorite (for scripts); before the
        // directory, in any order ("ls -a -l notes")
        var (all, full) = (false, false);
        while (argument.TrimStart() is ['-', 'a' or 'l', ..] trimmed &&
               (trimmed.Length == 2 || char.IsWhiteSpace(trimmed[2])))
        {
            if (trimmed[1] == 'a') all = true;
            else full = true;
            argument = trimmed[2..];
        }

        var item = Resolve(argument, true);
        if (item == null) return;
        if (!System.IO.Directory.Exists(item.Path))
        {
            Error(Lang.F("Not found: {0}", item.Path));
            return;
        }

        try
        {
            var info = new DirectoryInfo(item.Path);
            // The folders first, then the files, each by name (letter case alike)
            // (a favorite marked " *", as in the file browser)
            FileFavorites.Load();
            var rows = info.EnumerateDirectories().Where(Shown).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => (new Item(d.FullName, true),
                    full ? Path.TrimEndingDirectorySeparator(d.FullName) + "/" : d.Name + "/" + FavoriteMark(d.FullName)))
                .Concat(info.EnumerateFiles().Where(Shown).OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => (new Item(f.FullName, false), full ? f.FullName : f.Name + FavoriteMark(f.FullName))))
                .ToList();
            Line(item.Path);
            List(rows, Lang.T("(empty)"));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
        }

        bool Shown(FileSystemInfo entry) => all || (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;
    }

    #endregion

    #region Lists

    // "fav", "fav work": the favorites of the open list, or of another. "fav -l", "fav -l work": the full paths.
    // "options": false for "fav use -l" (a list named "-l")
    private void ListFavorites(string argument, bool options = true)
    {
        var full = false;
        if (options) (full, argument) = LongList(argument);
        FileFavorites.Load();
        if (argument.Trim().Length > 0)
        {
            // Another list: used from now on, as ":list name" in the file browser
            if (FavoriteList(argument) is not { } list) return;

            FileFavorites.UseList(list);
            // (kept, as ":list" keeps it: the full screen shows the same list)
            FileIo.SaveSettings();
        }

        Line(Lang.F("List: {0}", ListWord(FileFavorites.CurrentList)));
        // The folders of the list are shown as headings (not numbered)
        var items = new List<Item>();
        // (the folders before each row: a folder with a subfolder first in it, or empty folders, are several)
        var headings = new Dictionary<int, List<string>>();
        foreach (var favorite in FileFavorites.Items)
        {
            if (favorite.IsFolder)
            {
                if (!headings.TryGetValue(items.Count, out var before)) headings[items.Count] = before = new List<string>();
                before.Add(new string(' ', 2 * favorite.Level) + "[" + favorite.Name + "]");
                continue;
            }

            var isDirectory = System.IO.Directory.Exists(favorite.Path);
            var at = favorite.Line > 0 ? (favorite.Line, Math.Max(1, favorite.Column)) : ((int, int)?)null;
            items.Add(new Item(favorite.Path, isDirectory, at));
        }

        // (no mark: they are all favorites)
        var names = Names(items.Select(item => (item.Path, item.IsDirectory)).ToList(), full, false);
        var rows = items.Select((item, i) => (item, names[i])).ToList();

        if (headings.Count == 0)
        {
            List(rows, Lang.T("No favorites"));
            return;
        }

        // With folders: the rows with the folder names between them (the numbers are those of the rows)
        LastList = rows.Select(row => row.Item1).ToList();
        var width = rows.Count.ToString().Length;
        for (var i = 0; i <= rows.Count; i++)
        {
            if (headings.TryGetValue(i, out var before)) foreach (var heading in before) Line(heading);
            if (i < rows.Count) Line((i + 1).ToString().PadLeft(width + 2) + "  " + rows[i].Item2);
        }
    }

    // "hist", "recent": the files by name and when they were opened ("-l": the full paths)
    private void ListHistory(int most, string argument)
    {
        if (OnlyLong(argument) is not { } full) return;
        FileHistory.Load();
        var entries = FileHistory.Items.Take(most).ToList();
        var names = Names(entries.Select(entry => (entry.Path, false)).ToList(), full);
        // (the dates lined up after the names; after full paths not: the rows would be wider than the window)
        var width = full || names.Count == 0 ? 0 : names.Max(TextMetrics.WidthOf);
        var rows = entries.Select((entry, i) => (new Item(entry.Path, false),
            Padded(names[i], width) + "  " + entry.Opened.ToString("yyyy-MM-dd HH:mm"))).ToList();
        List(rows, Lang.T("No files opened"));
    }

    private void ListHotlist()
    {
        DirectoryHotlist.Load();
        var rows = DirectoryHotlist.Items.Select(entry =>
            (new Item(entry.Path, true),
                (entry.Name.Length > 0 ? entry.Name + "  " : string.Empty) + Shown(entry.Path, true))).ToList();
        List(rows, Lang.T("No directories in the hotlist"));
    }

    // "book": the bookmarks, by their name and the name of the file or directory ("-l": its full path)
    private void ListBookmarks(string argument)
    {
        if (OnlyLong(argument) is not { } full) return;
        Bookmarks.Load();
        var places = Bookmarks.Places.ToList();
        var names = Names(places.Select(entry =>
            (entry.Target.Kind is JumpKind.File or JumpKind.Directory ? entry.Target.FullPath : "?",
                entry.Target.Kind == JumpKind.Directory)).ToList(), full);
        var rows = places.Select((entry, i) =>
        {
            var target = entry.Target;
            var name = entry.Name.Length > 0 ? entry.Name + "  " : string.Empty;
            return target.Kind switch
            {
                JumpKind.File => (new Item(target.FullPath, false, target.At),
                    name + names[i] + (target.At is { } at ? ":" + at.Line : string.Empty)),
                JumpKind.Directory => (new Item(target.FullPath, true), name + names[i]),
                // A page of Blyant: on the full screen only
                _ => (new Item(string.Empty, false), name + Lang.F("page {0} (full screen only)", target.Target))
            };
        }).ToList();
        List(rows, Lang.T("No bookmarks"));
    }

    private void ListProjects()
    {
        var projects = Projects.All();
        var names = Projects.DisplayNames(projects);
        var rows = projects.Select((project, i) =>
            (new Item(project.Path, true), names[i] + "  " + Shown(project.Path, true))).ToList();
        List(rows, Lang.T("No projects"));
    }

    // "project 2" (the number in the list of projects) or "project novel" (its name): go to its directory
    private void GoToProject(string argument)
    {
        argument = argument.Trim();
        var projects = Projects.All();
        var names = Projects.DisplayNames(projects);
        var index = int.TryParse(argument, out var number) && number >= 1 && number <= projects.Count
            ? number - 1
            : names.FindIndex(name => string.Equals(name, argument, StringComparison.CurrentCultureIgnoreCase));
        if (argument.Length == 0 || index < 0)
        {
            Error(Lang.F("No project {0} (projects lists them)", argument));
            return;
        }

        ChangeDirectory(projects[index].Path);
    }

    private void ShowLog(string argument)
    {
        var count = int.TryParse(argument.Trim(), out var n) && n > 0 ? n : 20;
        foreach (var line in EventLog.All.Skip(Math.Max(0, EventLog.All.Count - count))) Line(line);
    }

    // "about": Blyant, its version and author, and the commands of the shell used the most (the about page of the full
    // screen tells of its keys); "about license": the license, as on the second page of About
    private void About(string argument)
    {
        var what = argument.Trim().ToLowerInvariant();
        if (what.Length > 0 && what != "license")
        {
            Error(Lang.T("Give nothing, or license: about license"));
            return;
        }

        // The columns of the window (0: not a terminal, or one that does not tell its size)
        var width = Terminal && WindowSize?.Invoke() is { Width: > 20 } size ? size.Width - 1 : 0;
        if (what == "license")
        {
            // In a terminal a line wider than the window is broken between words (the paragraphs of the license are
            // one line each); to a file or a program, as it is
            foreach (var line in TextBuffer.Normalize(SoftwareLicense.GetLicenseText()).TrimEnd('\n').Split('\n'))
            {
                if (width == 0 || TextMetrics.WidthOf(line) <= width) Line(line);
                else foreach (var part in ChoiceBox.Wrap(line, width)) Line(part);
            }

            return;
        }

        Line(DialogText.ApplicationName + " " + DialogText.ApplicationVersion + " " + DialogText.ApplicationBy);
        Line();
        Line(Lang.T("The commands used the most:"));
        var rows = new[]
        {
            ("help [command]", Lang.T("List the commands, or tell what one does")),
            ("ls, cd 2, up", Lang.T("List a directory, go into row 2 of it, or up")),
            ("open 3", Lang.T("Edit a file on the full screen (closing it is back here)")),
            ("peek 3, find plot", Lang.T("Show lines of a file; find files by name (-t: by text)")),
            ("recent, fav, hot", Lang.T("The latest files, the favorites, the directory hotlist")),
            ("set language de", Lang.T("Language: en, de, da, nb, sv, es, fr, it, pl, nl or fi")),
            ("screen", Lang.T("Go on on the full screen, in the file browser here")),
            ("exit", Lang.T("Leave Blyant (also quit, q)"))
        };
        CommandRows(rows, "  ");

        Line();
        Line(Lang.T("about license shows the license."));
        Line(Lang.T("The shell tutorial in the user manual shows more."));
    }

    // Rows of a command and what it does: the texts in a column, a text wider than the window (79 columns when it is
    // not known, as to a file) broken between words under its column
    private void CommandRows(IReadOnlyList<(string Command, string Text)> rows, string indent)
    {
        var width = Terminal && WindowSize?.Invoke() is { Width: > 20 } size ? size.Width - 1 : 79;
        var widest = rows.Max(row => row.Command.Length) + 2;
        var room = width - widest - indent.Length;
        foreach (var (command, text) in rows)
        {
            var parts = room >= 20 ? ChoiceBox.Wrap(text, room) : [text];
            Line(indent + command.PadRight(widest) + parts[0]);
            foreach (var part in parts.Skip(1)) Line(new string(' ', indent.Length + widest) + part);
        }
    }

    private void Help(string argument)
    {
        var rows = HelpRows().ToList();
        var name = argument.Trim().ToLowerInvariant();
        if (name.Length > 0)
        {
            // The command, or the long name of a short one ("fav": favorites)
            var full = name switch
            {
                "fav" or "favs" => "favorites",
                "hist" => "history",
                "hot" => "hotlist",
                "book" => "bookmarks",
                "proj" => "projects",
                "h" => "help",
                "q" or "quit" => "exit",
                "del" => "delete",
                "cls" => "clear",
                "$clip" => "clip",
                _ => name
            };
            // (all the rows of the command: "help clip" tells each of its forms)
            // ("trim!", "shift!": with the command)
            var found = rows.Where(r => r.Command.Split(' ')[0].TrimEnd('!') == full.TrimEnd('!')).ToList();
            if (found.Count == 0)
            {
                Error(Lang.F("Unknown command: {0} (help lists the commands)", name));
                return;
            }

            CommandRows(found, string.Empty);
            return;
        }

        CommandRows(rows, "  ");
        Line();
        Line(Lang.T("A number is a row of the last list shown: open 3, cd 2"));
    }

    #endregion

    #region Open

    // "open notes.md", "open 3", "open notes.md +12": the editor of the full screen, and back at the prompt when it is
    // closed. A file that is not there (in a directory that is) is made if you want that. A directory: gone to
    private void Open(string argument)
    {
        // (a name in quotes as it is: "a  b.md" keeps its blanks)
        var words = Words(argument);
        var line = words.Select(word => IsLine(word)
            ? int.TryParse(word[1..], out var n) ? Math.Max(1, n) : int.MaxValue
            : (int?)null).LastOrDefault(n => n != null);
        var name = string.Join(' ', words.Where(word => !IsLine(word)));
        if (name.Length == 0)
        {
            Error(Lang.T("Open what? open notes.md, or open 3 (a row of the last list)"));
            return;
        }

        var item = Resolve(name, false);
        if (item == null) return;
        if (item.IsDirectory)
        {
            ChangeDirectory(item.Path);
            return;
        }

        var result = StartArgument.Resolve(item.Path, Directory, line);
        if (result.Message.Length > 0)
        {
            Error(result.Message);
            return;
        }

        // (before a new file is made: without the editor it would stay empty)
        if (!Terminal)
        {
            Error(Lang.F("{0} is not opened: the editor needs a terminal", result.Path));
            return;
        }

        if (result.AskToCreate)
        {
            if (!ChoiceBox.Confirm(DialogText.NewFile,
                    Lang.F("{0} is not there. Make it?", Path.GetFileName(result.Path)), result.Path, false))
            {
                return;
            }

            try
            {
                // Only a new file: one that has come since it was looked for is not emptied
                FileIo.CreateNewFile(result.Path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Error(e.Message);
                return;
            }
        }

        var at = line is { } l ? (l, 1) : item.At;
        if (EditOnScreen(result.Path, at)) Line(Lang.F("Back from the editor: {0}", result.Path));
        else Line(Lang.F("Back from the editor: {0} (the place jumped to is not shown here)", result.Path));
    }

    // "+12": a line to open the file at
    private static bool IsLine(string word) => word.Length > 1 && word[0] == '+' && word[1..].All(char.IsAsciiDigit);

    // The editor of the full screen, with the file browser of its directory behind it, which closes with it (as when
    // a file is opened from the corkboard). A jump to another place leaves the editor (asking about changes first)
    // and is not made: the shell goes on
    // Returns false if the editor was left for a jump
    private static bool EditOnScreen(string path, (int Line, int Column)? at)
    {
        Shell.OnScreen = true;
        try
        {
            Init.EnterScreen();
            FileExplorer.RunAtFile(path, at, true);
            return true;
        }
        catch (NavigationException)
        {
            Jump.DropPending();
            return false;
        }
        finally
        {
            Shell.OnScreen = false;
            Init.LeaveScreen();
        }
    }

    #endregion
}
