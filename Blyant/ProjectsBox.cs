namespace Blyant;

/// <summary>
/// The list of the projects in a box on top of the page, to pick one from: Open project on the start page, E (or
/// ":projects") in the file browser and ":projects" in the editor. Below the list is where the selected project is
/// and what it holds, and to the right of it the start of its note. P makes a new project (in a folder the user
/// picks), A adds a folder, D takes one off the list, C picks the color, N opens the note in the editor, I shows
/// information, "/" filters the list, ":" is the command line, "?" the help, Enter picks and Esc closes. This is
/// also where new projects are made from (New project on the start page)
/// </summary>
internal static class ProjectsBox
{
    // The key hints of the list
    private static string Keys() =>
        Lang.T("j/k Select | Enter Open | P New | A Add | D Remove | C Color | N Note | ? Help");

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
            var projects = Projects.All();
            var names = Projects.DisplayNames(projects);
            // A missing one is told
            names = names.Select((name, i) => projects[i].Missing ? Lang.F("{0} (missing)", name) : name).ToList();
            // The colors and notes, read once (and again when one is changed)
            var info = projects.Select(project => Info(project.Path)).ToList();
            string? result = null;
            var extras = new PickListBox.Extras
            {
                RowColor = i => info[i].Color,
                Side = i => info[i].Note,
                EmptyText = Lang.T("No projects yet: P makes a new one, A adds a folder"),
                // Below the list: where the project is and what it holds, and the start of its note
                PreviewLabel = Lang.T("Files"),
                SideLabel = Lang.T("Note"),
                Key = (key, i) =>
                {
                    var project = i >= 0 ? projects[i] : null;
                    switch (char.ToUpperInvariant(key.KeyChar))
                    {
                        case 'P':
                            result = NewProject(string.Empty);
                            return result != null ? PickListBox.KeyUse.Close : PickListBox.KeyUse.Used;
                        case 'A':
                            if (AddFolder() is { } added)
                            {
                                select = added;
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

                            // Drawn again: the command line (and its list of commands) covered the box
                            return PickListBox.KeyUse.Used;
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
            var picked = PickListBox.Show(caption, names, i => Preview(projects[i]), selected, Keys(), extras);
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

    #region New and added projects

    /// <summary>
    /// Make a new project: its folder, in a folder the user picks in the file browser (it starts where a folder was
    /// picked last). The name is asked for if it is not given
    /// </summary>
    /// <returns>The full path of the project made, or null</returns>
    internal static string? NewProject(string name)
    {
        ChoiceBox.Forget();
        var parent = FileExplorer.PickDirectory(Lang.T("Where to make the project"));
        Tools.ClearScreen();
        if (parent == null) return null;
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

    // D: a project off the list (asked first; the folder itself stays). Returns true if the list changed
    private static bool RemoveProject(Projects.Project project)
    {
        ChoiceBox.Forget();
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
                _commandResult = NewProject(argument);
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

    // I or ":info": the project folder (as the info of a directory in the file browser), with its color and note
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
