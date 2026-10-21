namespace Blyant;

/// <summary>
/// The list of the projects in a box on top of the page, to pick one from: Open project on the start page, E (or
/// ":projects") in the file browser and ":projects" in the editor. Below the list is what the selected project
/// holds; "/" filters the list, Enter picks and Esc closes.
/// </summary>
internal static class ProjectsBox
{
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
            ChoiceBox.Message(DialogText.OpenProject, Lang.T("No projects yet: New project on the start page makes one"),
                Projects.Directory);
            return null;
        }

        var picked = PickListBox.Show(Lang.F("Projects ({0})", names.Count), names,
            i => Projects.Contents(names[i], 5), 0, Lang.T("j/k Select | Enter Open | / Filter | Esc Close"));
        return picked < 0 ? null : Projects.PathOf(names[picked]);
    }
}
