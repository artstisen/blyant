namespace Blyant;

/// <summary>
/// The list of the projects in a box on top of the page, to pick one from: Open project on the start page, E (or
/// ":projects") in the file browser and ":projects" in the editor. Below the list is what the selected project
/// holds, and to the right of it the start of the note of the project; "/" filters the list, C picks the color of
/// the project, N opens its note in the editor, I shows information about it, ":" is the command line (":info",
/// ":path", ":name", ":color", ":note", ":help"), "?" the help, Enter picks and Esc closes.
/// </summary>
internal static class ProjectsBox
{
    // The key hints of the list
    private static string Keys => Lang.T("j/k Select | Enter Open | C Color | N Note | I Info | : Command | ? Help");

    /// <summary>
    /// Show the projects and let the user pick one. Without projects a message tells how to make one
    /// </summary>
    /// <returns>The full path of the project picked, or null</returns>
    internal static string? Pick()
    {
        ChoiceBox.Forget();
        var names = Projects.Names();
        if (names.Count == 0)
        {
            ChoiceBox.Message(DialogText.OpenProject, DialogText.NoProjectsYet, Projects.Directory);
            return null;
        }

        // The colors and notes, read once (and again when one is changed)
        var info = names.Select(Info).ToList();
        var extras = new PickListBox.Extras
        {
            RowColor = i => info[i].Color,
            Side = i => info[i].Note,
            Key = (key, i) =>
            {
                switch (char.ToUpperInvariant(key.KeyChar))
                {
                    case 'C':
                        PickColor(names[i]);
                        break;
                    case 'N':
                        WriteNote(names[i]);
                        break;
                    case 'I':
                        ShowInfo(names[i]);
                        break;
                    case '?':
                        ShowHelp();
                        break;
                    case ':':
                        if (RunCommand(names[i])) return PickListBox.KeyUse.Close;
                        break;
                    default:
                        return PickListBox.KeyUse.None;
                }

                info[i] = Info(names[i]);
                return PickListBox.KeyUse.Used;
            }
        };
        var picked = PickListBox.Show(Lang.F("Projects ({0})", names.Count), names,
            i => Projects.Contents(names[i], 5), 0, Keys, extras);
        return picked < 0 ? null : Projects.PathOf(names[picked]);
    }

    private static (ConsoleColor? Color, string Note) Info(string name) =>
        (Projects.ColorOf(name), Projects.NoteSnippet(name));

    // C: the color of the name of the project in the list
    private static void PickColor(string name)
    {
        ChoiceBox.Forget();
        if (!ColorPicker.Pick(DialogText.ColorCaption, (Init.LineWidth - 26) / 2, true, Projects.ColorOf(name), null,
                null, out var color))
        {
            return;
        }

        ShowProblem(Projects.SetColor(name, color), Path.Combine(Projects.PathOf(name), Projects.InfoFileName));
    }

    // N: the note of the project in the editor (a note left empty is removed), and then the list again
    private static void WriteNote(string name)
    {
        var status = Projects.EnsureNote(name);
        if (status != DialogText.Success)
        {
            ShowProblem(status, Projects.NotePath(name));
            return;
        }

        FileExplorer.EditNote(Projects.NotePath(name));
        Projects.RemoveEmptyNote(name);
        Tools.ClearScreen();
    }

    // ":" in the list: a command, on the selected project. Returns true if the list is closed
    private static bool RunCommand(string name)
    {
        var command = CommandLine.Read(CommandContext.Projects);
        if (command == null) return false;
        if (command is "q" or "quit" or "close") return true;
        switch (command)
        {
            case "h":
            case "help":
                ShowHelp();
                break;
            case "info":
                ShowInfo(name);
                break;
            case "path":
            case "name":
                // The full path of the folder, or its name
                var path = Projects.PathOf(name);
                var text = command == "path" ? path : Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                PickListBox.ShowMessage(SystemClipboard.CopyWithMemory(text).Message);
                break;
            case "color":
                PickColor(name);
                break;
            case "note":
                WriteNote(name);
                break;
            default:
                CommandLine.ShowUnknown(command);
                break;
        }

        // (the end of the commands)
        return false;
    }

    // "?" or ":help": the keys of the list (the page behind it is drawn again when the list is closed)
    private static void ShowHelp()
    {
        Tools.WriteEditorHelp(Tools.HelpTopic.Projects);
        Tools.ClearScreen();
    }

    // I or ":info": the project folder (as the info of a directory in the file browser), with its color and note
    private static void ShowInfo(string name)
    {
        ChoiceBox.Forget();
        var path = Projects.PathOf(name);
        List<(string Label, string Value)> rows;
        try
        {
            rows = FileExplorer.InfoRows(name, path, true, 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ChoiceBox.Message(DialogText.ProjectInfo, e.Message, path);
            return;
        }

        // Before the path, which may take several rows
        var color = Projects.ColorOf(name);
        var note = Projects.NoteSnippet(name);
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
}
