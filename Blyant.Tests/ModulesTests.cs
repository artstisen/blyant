namespace Blyant.Tests;

/// <summary>
/// The modules that can be turned off (Configuration: Modules): the projects and the corkboard. Their doors are
/// closed, and nothing of them is lost
/// </summary>
[Collection("Static state")]
public class ModulesTests : IDisposable
{
    public void Dispose()
    {
        Modules.Projects = true;
        Modules.Board = true;
        Modules.Jump = true;
    }

    [Fact]
    public void TheCommandsOfAModuleThatIsOff_AreNotOffered()
    {
        Modules.Board = false;
        Assert.DoesNotContain("board", Commands.Names(CommandContext.Files));
        Assert.DoesNotContain("pin", Commands.Names(CommandContext.Editor));
        // Still commands: typed whole, they tell that the corkboard is off
        Assert.Contains("board", Commands.All(CommandContext.Files));
        Assert.True(Commands.IsKnown(CommandContext.Editor, "pin"));
        Assert.Contains("projects", Commands.Names(CommandContext.Start));

        Modules.Projects = false;
        Assert.DoesNotContain("projects", Commands.Names(CommandContext.Start));
        Assert.DoesNotContain("project", Commands.Names(CommandContext.Files));
        Assert.Contains("favorites", Commands.Names(CommandContext.Start));

        Modules.Projects = true;
        Modules.Board = true;
        Assert.Contains("board", Commands.Names(CommandContext.Files));
        Assert.Contains("projects", Commands.Names(CommandContext.Start));
    }

    [Fact]
    public void TheJumpMenu_HasNoProjectsTab_WhileTheyAreOff()
    {
        Assert.Contains(JumpMenu.Tab.Projects, JumpMenu.Tabs());
        Modules.Projects = false;
        Assert.DoesNotContain(JumpMenu.Tab.Projects, JumpMenu.Tabs());
        Assert.Contains(JumpMenu.Tab.Bookmarks, JumpMenu.Tabs());
    }

    [Fact]
    public void TheHelpOfAModuleThatIsOff_IsLeftOut()
    {
        Assert.False(Modules.HidesHelp(DialogText.HelpSectionBoard, "x"));
        Modules.Board = false;
        // Its own part of the help, and the rows elsewhere that lead to it
        Assert.True(Modules.HidesHelp(DialogText.HelpSectionBoard, "x"));
        Assert.True(Modules.HidesHelp(DialogText.HelpSectionFiles, Lang.T("The corkboard: files pinned as cards")));
        Assert.False(Modules.HidesHelp(DialogText.HelpSectionProjects, "x"));
        Modules.Projects = false;
        Assert.True(Modules.HidesHelp(DialogText.HelpSectionProjects, "x"));
        Assert.True(Modules.HidesHelp(DialogText.HelpSectionStart, Lang.T("New project: a folder where you pick")));
        Assert.False(Modules.HidesHelp(DialogText.HelpSectionStart, Lang.T("Exit Blyant (without asking)")));
    }

    [Fact]
    public void TheJumpMenuOff_TakesTheBookmarksWithIt_ButNotThePages()
    {
        Assert.False(Modules.RefusesJumpCommand("jump"));
        Modules.Jump = false;
        Assert.True(Modules.RefusesJumpCommand("jump"));
        Assert.True(Modules.RefusesJumpCommand("ju"));
        Assert.True(Modules.RefusesJumpCommand("book"));
        // The pages have their own commands, which stay
        Assert.False(Modules.RefusesJumpCommand("board"));
        Assert.False(Modules.RefusesJumpCommand("favorites"));
        Assert.DoesNotContain("jump", Commands.Names(CommandContext.Files));
        Assert.DoesNotContain("bookmark", Commands.Names(CommandContext.Editor));
        Assert.Contains("board", Commands.Names(CommandContext.Files));
        Assert.Contains("log", Commands.Names(CommandContext.Editor));
        // No bookmark is added
        Assert.Equal(Modules.JumpOff,
            Bookmarks.AddWithMessage(new JumpTarget(JumpKind.Directory, Path.GetTempPath()), false));
        Assert.True(Modules.HidesHelp(DialogText.HelpSectionJump, "x"));
        Assert.True(Modules.HidesHelp(DialogText.HelpSectionFiles,
            Lang.T("Jump to bookmark 3 (:book alone: the bookmarks)")));
    }

    [Fact]
    public void TheEditor_TellsTheMenuFromAPage()
    {
        // Ctrl+O and ":jump" ask for the Jump menu; ":board" and the lists only go to a page
        var editor = new EditorEngine("text", 40, 10, false);
        Assert.Equal(EditorRequest.Jump, editor.Type(":jump logos<CR>"));
        Assert.True(editor.JumpMenuAsked);
        Assert.Equal(EditorRequest.Jump, editor.Type(":board<CR>"));
        Assert.False(editor.JumpMenuAsked);
        Assert.Equal(EditorRequest.Jump, editor.Type(":history<CR>"));
        Assert.False(editor.JumpMenuAsked);
    }

    [Fact]
    public void TheGuidesOfTheModulesThatAreOff_AreNotOnTheHelpTab()
    {
        Assert.Equal(QuickGuide.All, QuickGuide.Shown);
        Modules.Board = false;
        Modules.Jump = false;
        Assert.Equal(QuickGuide.All.Length - 2, QuickGuide.Shown.Length);
        Assert.Contains(QuickGuide.Guide.Projects, QuickGuide.Shown);
        Assert.DoesNotContain(QuickGuide.Guide.Board, QuickGuide.Shown);
        Assert.DoesNotContain(QuickGuide.Guide.Jump, QuickGuide.Shown);
    }

    [Fact]
    public void TheRowsOfTheModules_AreRowsOfTheHelp()
    {
        // Each text is that of a row of the help (Tools.cs), so a row changed there is not left in the help by mistake
        var help = File.ReadAllText(Path.Combine(SourceDirectory(), "Tools.cs"));
        foreach (var text in Modules.ProjectHelp.Concat(Modules.BoardHelp).Concat(Modules.JumpHelp))
        {
            Assert.Contains("Lang.T(\"" + text + "\")", help);
        }
    }

    // The folder of the source of Blyant (found from the folder of the tests)
    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Blyant.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "Blyant");
    }
}
