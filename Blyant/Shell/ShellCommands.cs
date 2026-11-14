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
        ("ls [dir|n]", Lang.T("List the folders and files of a directory")),
        ("cd [dir|file|n]", Lang.T("Go to a directory, or that of a file (alone: home)")),
        ("up", Lang.T("Go to the directory above")),
        ("home", Lang.T("Go to your home directory")),
        ("data", Lang.T("Go to the data directory of Blyant (settings, lists, log)")),
        ("open file|n [+line]", Lang.T("Edit a file on the full screen (closing it is back here)")),
        ("peek file|n [lines]", Lang.T("Show the lines of a file (the first 20, 40, 100-120, or +100 30)")),
        ("find [name] [-t text]", Lang.T("Find files here and below, by name (*.md) and by text")),
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
        ("format file|n", Lang.T("Format an HTML, CSS or XML file, as :format does")),
        ("trim file|n", Lang.T("Remove blank space at line ends, as :trim does (trim!)")),
        ("set [name [value]]", Lang.T("Show the settings, or change one")),
        ("reset", Lang.T("Reset all settings to the defaults (asks first)")),
        ("clip [path|name] file|dir|n|.", Lang.T("Copy a path, or a name, to the clipboard")),
        ("clip text file|n", Lang.T("Copy the text of a file to the clipboard")),
        ("clip lines file|n lines", Lang.T("Copy lines of a file, as peek takes them")),
        ("clip $name", Lang.T("Copy the value of a register to the clipboard")),
        ("reg [name [text|.|#n]]", Lang.T("Keep a text or a path in a register: $name puts it in a command")),
        ("reg -d name|all", Lang.T("Take a register away, or all of them")),
        ("favorites [list]", Lang.T("List the favorites (fav), or those of another list")),
        ("favorites add|remove file|n", Lang.T("Add to the favorites, or take off them")),
        ("history", Lang.T("List the files opened (hist)")),
        ("recent", Lang.T("List the latest files")),
        ("hotlist", Lang.T("List the directory hotlist (hot)")),
        ("hotlist add|remove [dir|n]", Lang.T("Add a directory to the hotlist (this one), or take it off")),
        ("bookmarks", Lang.T("List the bookmarks (book)")),
        ("projects", Lang.T("List the projects (proj)")),
        ("project n|name", Lang.T("Go to the directory of a project")),
        ("log [n]", Lang.T("Show the last lines of the log (20)")),
        ("clear", Lang.T("Clear the screen (also cls)")),
        ("screen", Lang.T("Go on on the full screen, in the file browser here")),
        ("version", Lang.T("Show the version of Blyant")),
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
        // By name only (never what follows it: a path, a name, the value of a register)
        EventLog.AddCommand(DialogText.LogShell, line);
        // $name: the value of the register (in the paths a value with blanks is one word; the text of insert, change,
        // reg and set, and what find and project take, as it is)
        // ("clip $note" alone copies the value as it is: not put in first, where it would be taken as a path)
        if (!IsClipOfRegister(line))
        {
            line = ShellRegisters.Expand(line, CommandLine.Name(line).ToLowerInvariant() switch
            {
                "insert" or "change" => 3,
                "reg" or "set" => 2,
                "find" or "project" => 1,
                _ => int.MaxValue
            });
        }

        var name = CommandLine.Name(line).ToLowerInvariant();
        var argument = CommandLine.Argument(line);
        // (the line of the command typed is the first row of its page)
        NewPage();
        return RunCommand(name, argument);
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
                // "fav add 3", "fav remove 3", else the list ("fav work": another list)
                if (Change(argument) is { } addFavorite) ChangeFavorites(Rest(argument), addFavorite);
                else ListFavorites(argument);
                break;
            case "history":
            case "hist":
                ListHistory(int.MaxValue);
                break;
            case "recent":
                ListHistory(RecentBox.MaxFiles);
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
            case "reg":
                Register(argument);
                break;
            case "clip":
                Clip(argument);
                break;
            case "bookmarks":
            case "book":
                ListBookmarks();
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

            Error(!_listShown
                ? Lang.F("No row {0}: no list has been shown", argument)
                : Lang.F("No row {0}: the last list has {1} rows", argument, LastList.Count));
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

    private void ListDirectory(string argument)
    {
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
            var rows = info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => (new Item(d.FullName, true), d.Name + "/"))
                .Concat(info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(f => (new Item(f.FullName, false), f.Name)))
                .ToList();
            Line(item.Path);
            List(rows, Lang.T("(empty)"));
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Error(Lang.F("{0} cannot be read: {1}", item.Path, e.Message));
        }
    }

    #endregion

    #region Lists

    private void ListFavorites(string argument)
    {
        FileFavorites.Load();
        if (argument.Trim().Length > 0)
        {
            // Another list: used from now on, as ":list name" in the file browser
            if (FileFavorites.FindList(argument.Trim()) is not { } list)
            {
                Error(Lang.F("No list of favorites named {0}", argument.Trim()));
                return;
            }

            FileFavorites.UseList(list);
        }

        if (FileFavorites.CurrentList.Length > 0) Line(Lang.F("List: {0}", FileFavorites.CurrentList));
        // The folders of the list are shown as headings (not numbered)
        var rows = new List<(Item, string)>();
        var headings = new Dictionary<int, string>();
        foreach (var favorite in FileFavorites.Items)
        {
            if (favorite.IsFolder)
            {
                headings[rows.Count] = new string(' ', 2 * favorite.Level) + "[" + favorite.Name + "]";
                continue;
            }

            var isDirectory = System.IO.Directory.Exists(favorite.Path);
            var at = favorite.Line > 0 ? (favorite.Line, Math.Max(1, favorite.Column)) : ((int, int)?)null;
            rows.Add((new Item(favorite.Path, isDirectory, at), Shown(favorite.Path, isDirectory)));
        }

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
            if (headings.TryGetValue(i, out var heading)) Line(heading);
            if (i < rows.Count) Line((i + 1).ToString().PadLeft(width + 2) + "  " + rows[i].Item2);
        }
    }

    private void ListHistory(int most)
    {
        FileHistory.Load();
        var rows = FileHistory.Items.Take(most).Select(entry =>
            (new Item(entry.Path, false), Shown(entry.Path, false) + "  " + entry.Opened.ToString("yyyy-MM-dd HH:mm")))
            .ToList();
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

    private void ListBookmarks()
    {
        Bookmarks.Load();
        var rows = Bookmarks.Places.Select(entry =>
        {
            var target = entry.Target;
            var name = entry.Name.Length > 0 ? entry.Name + "  " : string.Empty;
            return target.Kind switch
            {
                JumpKind.File => (new Item(target.FullPath, false, target.At),
                    name + Shown(target.FullPath, false) + (target.At is { } at ? ":" + at.Line : string.Empty)),
                JumpKind.Directory => (new Item(target.FullPath, true), name + Shown(target.FullPath, true)),
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

            var widest = found.Max(r => r.Command.Length) + 2;
            foreach (var (command, text) in found) Line(command.PadRight(widest) + text);
            return;
        }

        var width = rows.Max(r => r.Command.Length) + 2;
        foreach (var (command, text) in rows) Line("  " + command.PadRight(width) + text);
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
