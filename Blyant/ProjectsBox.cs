namespace Blyant;

/// <summary>
/// The list of the projects in a box on top of the page, to pick one from: Open project on the start page, E (or
/// ":projects") in the file browser and ":projects" in the editor. With projects both in the projects folder and
/// added from elsewhere, the list has three tabs (Tab goes between them): All, Added and Projects folder. Below the
/// list is where the selected project is and what it holds, and to the right of it the start of its note. P makes
/// a new project, A adds a folder, D takes an added one off the list, C picks the color, N opens the note in the
/// editor, I shows information, "/" filters the list, ":" is the command line, "?" the help, Enter picks and Esc
/// closes. This is also where new projects are made from (New project on the start page), and where the user is
/// asked where the projects go, the first time
/// </summary>
internal static class ProjectsBox
{
    private enum Tab
    {
        All,
        Added,
        Folder
    }

    // The tab shown last (the list opens on it again)
    private static Tab _tab = Tab.All;

    private static string TabName(Tab tab) => tab switch
    {
        Tab.Added => Lang.T("Added"),
        Tab.Folder => Lang.T("Projects folder"),
        _ => Lang.T("All")
    };

    // The tabs: all three when there are projects both in the projects folder and added, else one (not shown)
    private static List<Tab> Tabs()
    {
        var added = Projects.AddedPaths().Count > 0;
        if (Projects.HasFolder && added) return new List<Tab> { Tab.All, Tab.Added, Tab.Folder };
        if (Projects.HasFolder) return new List<Tab> { Tab.Folder };
        return new List<Tab> { added ? Tab.Added : Tab.All };
    }

    private static List<Projects.Project> ProjectsOf(Tab tab) => tab switch
    {
        Tab.Added => Projects.Added(),
        Tab.Folder => Projects.InFolder(),
        _ => Projects.All()
    };

    // The key hints of the list (with tabs: Tab too)
    private static string Keys(bool tabs) => tabs
        ? Lang.T("j/k Select | Enter Open | Tab Next tab | P New | A Add | D Remove | ? Help")
        : Lang.T("j/k Select | Enter Open | P New | A Add | D Remove | C Color | N Note | ? Help");

    /// <summary>
    /// Show the projects and let the user pick one (or make one, or add one)
    /// </summary>
    /// <returns>The full path of the project picked, or null</returns>
    internal static string? Pick()
    {
        string? select = null;
        while (true)
        {
            ChoiceBox.Forget();
            var tabs = Tabs();
            if (!tabs.Contains(_tab)) _tab = tabs[0];
            var projects = ProjectsOf(_tab);
            var names = Projects.DisplayNames(projects);
            // In All: the added ones are told
            if (_tab == Tab.All && tabs.Count > 1)
            {
                names = names.Select((name, i) => projects[i].Added ? Lang.F("{0} (added)", name) : name).ToList();
            }

            // A missing one is told
            names = names.Select((name, i) => projects[i].Missing ? Lang.F("{0} (missing)", name) : name).ToList();
            // The colors and notes, read once (and again when one is changed)
            var info = projects.Select(project => Info(project.Path)).ToList();
            string? result = null;
            var extras = new PickListBox.Extras
            {
                RowColor = i => info[i].Color,
                Side = i => info[i].Note,
                Tabs = tabs.Count > 1 ? tabs.Select(TabName).ToList() : null,
                ActiveTab = Math.Max(0, tabs.IndexOf(_tab)),
                EmptyText = Lang.T("No projects yet: P makes a new one, A adds a folder"),
                // Below the list: where the project is and what it holds, and the start of its note
                PreviewLabel = Lang.T("Files"),
                SideLabel = Lang.T("Note"),
                Key = (key, i) =>
                {
                    var project = i >= 0 ? projects[i] : null;
                    if (key.Key == ConsoleKey.Tab && tabs.Count > 1)
                    {
                        var back = (key.Modifiers & ConsoleModifiers.Shift) != 0;
                        _tab = tabs[(tabs.IndexOf(_tab) + (back ? tabs.Count - 1 : 1)) % tabs.Count];
                        select = project?.Path;
                        return PickListBox.KeyUse.Reload;
                    }

                    switch (char.ToUpperInvariant(key.KeyChar))
                    {
                        case 'P':
                            result = NewProject(string.Empty, _tab switch
                            {
                                Tab.Added => Place.Elsewhere,
                                Tab.Folder => Place.Folder,
                                _ => Place.Default
                            });
                            return result != null ? PickListBox.KeyUse.Close : PickListBox.KeyUse.Used;
                        case 'A':
                            if (AddFolder() is { } added)
                            {
                                select = added;
                                if (_tab == Tab.Folder) _tab = Tab.Added;
                                return PickListBox.KeyUse.Reload;
                            }

                            return PickListBox.KeyUse.Used;
                        case 'D':
                            if (project != null && RemoveProject(project))
                            {
                                select = null;
                                return PickListBox.KeyUse.Reload;
                            }

                            return PickListBox.KeyUse.Used;
                        case '?':
                            ShowHelp();
                            return PickListBox.KeyUse.Used;
                        case ':':
                            switch (RunCommand(project))
                            {
                                case CommandResult.Close:
                                    return PickListBox.KeyUse.Close;
                                case CommandResult.Reload:
                                    return PickListBox.KeyUse.Reload;
                                case CommandResult.Opened when _commandResult != null:
                                    result = _commandResult;
                                    return PickListBox.KeyUse.Close;
                            }

                            break;
                    }

                    if (project == null) return PickListBox.KeyUse.None;
                    switch (char.ToUpperInvariant(key.KeyChar))
                    {
                        case 'C':
                            PickColor(project.Path);
                            break;
                        case 'N':
                            WriteNote(project.Path);
                            break;
                        case 'I':
                            ShowInfo(project);
                            break;
                        case ':':
                            break;
                        default:
                            return PickListBox.KeyUse.None;
                    }

                    info[i] = Info(project.Path);
                    return PickListBox.KeyUse.Used;
                }
            };
            var caption = Lang.F("Projects ({0})", projects.Count);
            var selected = select == null ? 0 : Math.Max(0, projects.FindIndex(p => FileOperations.SamePath(p.Path, select)));
            var picked = PickListBox.Show(caption, names, i => Preview(projects[i]), selected, Keys(tabs.Count > 1),
                extras);
            if (result != null) return result;
            if (picked == PickListBox.Reload) continue;
            if (picked < 0) return null;
            if (!projects[picked].Missing) return projects[picked].Path;
            // Missing: told, and the list again
            ChoiceBox.Forget();
            ChoiceBox.Message(DialogText.OpenProject, DialogText.ProjectMissing, projects[picked].Path);
            select = projects[picked].Path;
        }
    }

    // Below the list: where the project is (the end of the path, in the left half of the box), and what it holds
    private static string Preview(Projects.Project project)
    {
        var path = TextMetrics.KeepEnd(project.Path, Math.Max(10, (Init.LineWidth - 10) / 2 - 4));
        return path + "\n" + (project.Missing ? DialogText.ProjectMissing : Projects.Contents(project.Path, 4));
    }

    private static (ConsoleColor? Color, string Note) Info(string project) =>
        (Projects.ColorOf(project), Projects.NoteSnippet(project));

    #region The projects folder

    /// <summary>
    /// Where the projects go: the projects folder suggested (in Documents), another one picked in the file
    /// browser, or none (the place of each new project is asked for). Asked the first time a project is made, and
    /// in Configuration. The settings are saved
    /// </summary>
    /// <returns>True if the user decided (false: canceled)</returns>
    internal static bool AskProjectsFolder()
    {
        ChoiceBox.Forget();
        var current = Projects.HasFolder ? Projects.Directory : Projects.AskEachTime ? Lang.T("(ask each time)") : Lang.T("(not set)");
        var choice = ChoiceBox.Pick(Lang.T("Projects folder"), Lang.T("Where should your projects be kept?"),
            Lang.F("Now: {0}", current),
            new[]
            {
                Lang.F("In Documents: {0}", Projects.DefaultDirectory), Lang.T("Choose another folder..."),
                Lang.T("No projects folder: ask where each time"), DialogText.ConvertCancel
            },
            new[] { "1", "2", "3", "Esc" }, Projects.HasFolder ? 1 : 0, false);
        switch (choice)
        {
            case 0:
                Projects.Directory = Projects.DefaultDirectory;
                Projects.AskEachTime = false;
                break;
            case 1:
                ChoiceBox.Forget();
                var picked = FileExplorer.PickDirectory(Lang.T("Projects folder"));
                Tools.ClearScreen();
                if (picked == null) return false;
                Projects.Directory = picked;
                Projects.AskEachTime = false;
                break;
            case 2:
                Projects.Directory = string.Empty;
                Projects.AskEachTime = true;
                break;
            default:
                return false;
        }

        EventLog.Add(DialogText.LogFile + "Projects folder: " +
                     (Projects.HasFolder ? Projects.Directory : "(none: asked each time)"));
        FileIo.SaveSettings();
        return true;
    }

    #endregion

    #region New and added projects

    /// <summary>
    /// Where a new project is made
    /// </summary>
    internal enum Place
    {
        Default, // In the projects folder, or where the user picks if there is none (asked first, the first time)
        Folder, // In the projects folder
        Elsewhere // Where the user picks (it is added to the projects)
    }

    /// <summary>
    /// Make a new project: its folder, in the projects folder or where the user picks. The name is asked for if it
    /// is not given. The first time, the user is asked where the projects go
    /// </summary>
    /// <returns>The full path of the project made, or null</returns>
    internal static string? NewProject(string name, Place place)
    {
        ChoiceBox.Forget();
        if (place == Place.Default && !Projects.IsDecided && !AskProjectsFolder()) return null;
        if (place == Place.Folder && !Projects.HasFolder) place = Place.Default;
        string parent;
        if (place != Place.Elsewhere && Projects.HasFolder)
        {
            parent = Projects.Directory;
        }
        else
        {
            ChoiceBox.Forget();
            var picked = FileExplorer.PickDirectory(Lang.T("Where to make the project"));
            Tools.ClearScreen();
            if (picked == null) return null;
            parent = picked;
        }

        ChoiceBox.Forget();
        name = name.Trim();
        if (name.Length == 0)
        {
            var asked = ChoiceBox.AskText(DialogText.NewProject, Lang.F("The project is a folder in {0}", parent),
                DialogText.ProjectNameColon, string.Empty);
            if (asked == null) return null;
            name = asked;
        }

        var status = Projects.Create(parent, name, out var path);
        EventLog.Add(DialogText.LogFile + "New project: " + (path.Length > 0 ? path : name) +
                     (status == DialogText.Success ? string.Empty : " (" + status + ")"));
        if (status != DialogText.Success)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.NewProject, status, name);
            return null;
        }

        return path;
    }

    // A: a folder picked in the file browser is added to the projects. Returns its path (null: not added)
    private static string? AddFolder()
    {
        ChoiceBox.Forget();
        var picked = FileExplorer.PickDirectory(Lang.T("Add a folder to the projects"));
        Tools.ClearScreen();
        if (picked == null) return null;
        return AddFolder(picked);
    }

    /// <summary>
    /// Add a folder to the projects, or tell why it cannot be
    /// </summary>
    /// <returns>Its path, or null if it was not added</returns>
    internal static string? AddFolder(string path)
    {
        var status = Projects.Add(path);
        EventLog.Add(DialogText.LogFile + "Project added: " + path +
                     (status == DialogText.Success ? string.Empty : " (" + status + ")"));
        if (status == DialogText.Success) return Path.GetFullPath(path);
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.AddProject, status, path);
        return null;
    }

    // D: an added project off the list (asked first; the folder itself stays). One in the projects folder is not
    // removed here. Returns true if the list changed
    private static bool RemoveProject(Projects.Project project)
    {
        ChoiceBox.Forget();
        if (!project.Added)
        {
            ChoiceBox.Message(DialogText.OpenProject, Lang.T("This project is in the projects folder"),
                Lang.T("It is deleted in the file browser, as other folders are"));
            return false;
        }

        if (!ChoiceBox.ConfirmGuarded(Lang.T("Remove from the projects"),
                Lang.F("Take \"{0}\" off the list of projects?", Tools.TruncateString(project.Name, 40, true)),
                Lang.T("The folder and its files stay as they are")))
        {
            return false;
        }

        var status = Projects.Remove(project.Path);
        EventLog.Add(DialogText.LogFile + "Project removed from the list: " + project.Path +
                     (status == DialogText.Success ? string.Empty : " (" + status + ")"));
        if (status == DialogText.Success) return true;
        ChoiceBox.Message(DialogText.OpenProject, status, project.Path);
        return false;
    }

    #endregion

    #region Keys of a project

    // C: the color of the name of the project in the list
    private static void PickColor(string project)
    {
        ChoiceBox.Forget();
        if (!ColorPicker.Pick(DialogText.ColorCaption, (Init.LineWidth - 26) / 2, true, Projects.ColorOf(project), null,
                null, out var color))
        {
            return;
        }

        ShowProblem(Projects.SetColor(project, color), Path.Combine(project, Projects.InfoFileName));
    }

    // N: the note of the project in the editor (a note left empty is removed), and then the list again
    private static void WriteNote(string project)
    {
        var status = Projects.EnsureNote(project);
        if (status != DialogText.Success)
        {
            ShowProblem(status, Projects.NotePath(project));
            return;
        }

        FileExplorer.EditNote(Projects.NotePath(project));
        Projects.RemoveEmptyNote(project);
        Tools.ClearScreen();
    }

    private enum CommandResult
    {
        None,
        Close, // The list is closed
        Reload, // The list is made again (a project added or removed)
        Opened // A project was made: _commandResult
    }

    private static string? _commandResult;

    // ":" in the list: a command, on the selected project (null: the list is empty)
    private static CommandResult RunCommand(Projects.Project? project)
    {
        _commandResult = null;
        var command = CommandLine.Read(CommandContext.Projects);
        if (command == null) return CommandResult.None;
        var name = CommandLine.Name(command).ToLowerInvariant();
        var argument = command.Length > name.Length ? command[name.Length..].Trim() : string.Empty;
        if (name is "q" or "quit" or "close") return CommandResult.Close;
        switch (name)
        {
            case "h":
            case "help":
                ShowHelp();
                return CommandResult.None;
            case "new":
                _commandResult = NewProject(argument, Place.Default);
                return _commandResult != null ? CommandResult.Opened : CommandResult.None;
            case "add":
                return AddFolder() != null ? CommandResult.Reload : CommandResult.None;
        }

        if (project == null)
        {
            if (Commands.IsKnown(CommandContext.Projects, name))
            {
                PickListBox.ShowMessage(Lang.T("No projects yet: P makes a new one, A adds a folder"));
            }
            else
            {
                CommandLine.ShowUnknown(command);
            }

            return CommandResult.None;
        }

        switch (name)
        {
            case "info":
                ShowInfo(project);
                break;
            case "path":
            case "name":
                // The full path of the folder, or its name
                PickListBox.ShowMessage(SystemClipboard.CopyWithMemory(name == "path" ? project.Path : project.Name)
                    .Message);
                break;
            case "color":
                PickColor(project.Path);
                break;
            case "note":
                WriteNote(project.Path);
                break;
            case "remove":
                return RemoveProject(project) ? CommandResult.Reload : CommandResult.None;
            default:
                CommandLine.ShowUnknown(command);
                break;
        }

        // (the end of the commands)
        return CommandResult.None;
    }

    // "?" or ":help": the keys of the list (the page behind it is drawn again when the list is closed)
    private static void ShowHelp()
    {
        Tools.WriteEditorHelp(Tools.HelpTopic.Projects);
        Tools.ClearScreen();
    }

    // I or ":info": the project folder (as the info of a directory in the file browser), with its color and note,
    // and whether it was added or is in the projects folder
    private static void ShowInfo(Projects.Project project)
    {
        ChoiceBox.Forget();
        List<(string Label, string Value)> rows;
        try
        {
            rows = FileExplorer.InfoRows(project.Name, project.Path, true, 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ChoiceBox.Message(DialogText.ProjectInfo, e.Message, project.Path);
            return;
        }

        // Before the path, which may take several rows
        var color = Projects.ColorOf(project.Path);
        var note = Projects.NoteSnippet(project.Path);
        rows.Insert(rows.Count - 1, (Lang.T("Color:"), color == null ? DialogText.ColorDefault : ColorPicker.Name(color.Value)));
        rows.Insert(rows.Count - 1, (Lang.T("Note:"), note.Length == 0 ? Lang.T("(none)") : Tools.TruncateString(note, 160, true)));
        rows.Insert(rows.Count - 1, (Lang.T("Project:"),
            project.Added ? Lang.T("Added (from anywhere)") : Lang.T("In the projects folder")));
        ChoiceBox.ShowRows(DialogText.ProjectInfo, rows);
    }

    private static void ShowProblem(string result, string path)
    {
        if (result == DialogText.Success) return;
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.OpenProject, result, path);
    }

    #endregion
}
