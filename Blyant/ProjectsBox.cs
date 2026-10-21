namespace Blyant;

/// <summary>
/// The list of the projects in a box on top of the page, to pick one from: Open project on the start page, E (or
/// ":projects") in the file browser and ":projects" in the editor. Below the list is what the selected project
/// holds, and to the right of it the start of the note of the project; "/" filters the list, C picks the color of
/// the project, N opens its note in the editor, Enter picks and Esc closes.
/// </summary>
internal static class ProjectsBox
{
    // The key hints of the list
    private static string Keys => Lang.T("j/k Select | Enter Open | C Color | N Note | / Filter | Esc Close");

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
                    default:
                        return false;
                }

                info[i] = Info(names[i]);
                return true;
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

    private static void ShowProblem(string result, string path)
    {
        if (result == DialogText.Success) return;
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.OpenProject, result, path);
    }
}
