namespace Blyant;

/// <summary>
/// The parts of Blyant that can be turned off on the Modules page of Configuration: the projects (setting
/// "moduleprojects"), the corkboard ("moduleboard") and the Jump menu with the bookmarks ("modulejump"). A module turned off has its doors closed: it is not on the
/// start page, in the Jump menu, in the command box above the command line or in the help, its keys tell that it is
/// off, and its commands do too. Nothing of it is deleted: the projects and the boards come back as they were when it
/// is turned on again
/// </summary>
internal static class Modules
{
    /// <summary>
    /// The projects: New project and Open project, the list of projects (E, ":projects"), and its tab of the Jump
    /// menu
    /// </summary>
    internal static bool Projects
    {
        get => _projects;
        set
        {
            _projects = value;
            // The help leaves out the rows of a module that is off
            Tools.ClearEditorHelpData();
        }
    }

    private static bool _projects = true;

    /// <summary>
    /// The corkboard: its page (":board"), pinning files (":pin"), and the boards of the projects
    /// </summary>
    internal static bool Board
    {
        get => _board;
        set
        {
            _board = value;
            Tools.ClearEditorHelpData();
        }
    }

    private static bool _board = true;

    /// <summary>
    /// The Jump menu: Ctrl+O and ":jump" (also ":jump name"), and the bookmarks, which are kept in it (":book",
    /// ":bookmark"). The pages it goes to have their own commands (":board", ":favorites", ":log" ...), which stay
    /// </summary>
    internal static bool Jump
    {
        get => _jump;
        set
        {
            _jump = value;
            Tools.ClearEditorHelpData();
        }
    }

    private static bool _jump = true;

    /// <summary>
    /// What is told when the projects are asked for while they are off
    /// </summary>
    internal static string ProjectsOff => Lang.T("Projects are turned off (Configuration: Modules)");

    /// <summary>
    /// What is told when the corkboard is asked for while it is off
    /// </summary>
    internal static string BoardOff => Lang.T("The corkboard is turned off (Configuration: Modules)");

    /// <summary>
    /// What is told when the Jump menu or a bookmark is asked for while it is off
    /// </summary>
    internal static string JumpOff => Lang.T("The Jump menu is turned off (Configuration: Modules)");

    /// <summary>
    /// The Jump menu is off: told in a box (Ctrl+O, ":jump", ":book"), and true
    /// </summary>
    internal static bool JumpTurnedOff()
    {
        if (Jump) return false;
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.CaptionsJump, JumpOff, string.Empty);
        return true;
    }

    /// <summary>
    /// True for a command of the Jump menu (":jump", ":book") that is refused where it is typed while the Jump menu
    /// is off (CommandLine.Read), also ":jump name": the pages have their own commands
    /// </summary>
    internal static bool RefusesJumpCommand(string name) =>
        !Jump && name is "jump" or "ju" or "jmp" or "book";

    // The commands of each module (also their short forms), left out of the command box and Tab while it is off.
    // Typed whole, they are still run, and tell that the module is off
    private static readonly string[] ProjectCommands = { "project", "projects", "newproject", "proj", "pro" };
    private static readonly string[] BoardCommands = { "board", "corkboard", "pin", "tofile" };
    private static readonly string[] JumpCommands = { "jump", "ju", "jmp", "book", "bookmark", "bookmark!" };

    /// <summary>
    /// True for a command of a module that is off
    /// </summary>
    internal static bool Hides(string command) =>
        (!Projects && ProjectCommands.Contains(command)) || (!Board && BoardCommands.Contains(command)) ||
        (!Jump && JumpCommands.Contains(command));

    // The rows of the help (Tools.WriteEditorHelp) that are about a module, by what they say in the language in use.
    // ModulesTests checks that each is a text of the help
    internal static string[] ProjectHelp => new[]
    {
        Lang.T("New project: a folder where you pick"),
        Lang.T("Open project: pick one in the list of projects"),
        Lang.T("In the list: the color of the project"),
        Lang.T("In the list: the note of the project, in the editor"),
        Lang.T("In the list: the corkboard of the project"),
        Lang.T("New project (asks for the name if not given)"),
        Lang.T("Open project: the list of projects"),
        Lang.T("The projects: Enter shows one in this pane"),
        Lang.T("Add the directory shown to the projects"),
        Lang.T("The projects: Enter shows one in the file browser"),
        Lang.T("Open a project in the file browser")
    };

    internal static string[] BoardHelp => new[]
    {
        Lang.T("The corkboard: files pinned as cards"),
        Lang.T("Pin the selected file or folder to the corkboard"),
        Lang.T("Pin the file being edited to the corkboard"),
        Lang.T("In the list: the corkboard of the project"),
        Lang.T("The corkboard of the project (shown at the right)")
    };

    internal static string[] JumpHelp => new[]
    {
        Lang.T("The Jump menu: files, directories and pages"),
        Lang.T("Open the Jump menu (from most places)"),
        Lang.T("The Jump menu (asks to save changes before leaving)"),
        Lang.T("Go to a page of Blyant (Tab lists the names)"),
        Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)"),
        Lang.T("The number is shown at the right of the bookmark"),
        Lang.T("Bookmark the place of the cursor (Jump menu)"),
        Lang.T("Bookmark the selected file or directory (Jump menu)"),
        Lang.T("... with a name: :book name jumps to it"),
        Lang.T("Move the bookmark with the name here")
    };

    /// <summary>
    /// True for a row of the help that is about a module that is off: a row of its own part of the help (the list
    /// of projects, the corkboard), or a row elsewhere that leads to it
    /// </summary>
    internal static bool HidesHelp(string section, string description)
    {
        if (!Projects && (section == DialogText.HelpSectionProjects || ProjectHelp.Contains(description)))
        {
            return true;
        }

        if (!Board && (section == DialogText.HelpSectionBoard || BoardHelp.Contains(description))) return true;
        return !Jump && (section == DialogText.HelpSectionJump || JumpHelp.Contains(description));
    }
}
