namespace Blyant.Tests;

/// <summary>
/// The projects (folders in the projects folder), the name of a new file (Markdown without an extension), and
/// ":projects" in the editor
/// </summary>
[Collection("Static state")]
public class ProjectsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-projects-" + Guid.NewGuid().ToString("N"));
    private readonly string _saved = Projects.Directory;

    public ProjectsTests()
    {
        Projects.Directory = Path.Combine(_directory, "Projects");
    }

    public void Dispose()
    {
        Projects.Directory = _saved;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Theory]
    [InlineData("chapter", "chapter.md")]
    [InlineData(" chapter 1 ", "chapter 1.md")]
    [InlineData("index.html", "index.html")]
    [InlineData("notes.MD", "notes.MD")]
    [InlineData("v1.2 draft", "v1.2 draft")] // ".2 draft" is its extension
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("name.", "name.")] // Not a name to use: refused as it is
    [InlineData("", "")]
    public void ANewFileWithoutAnExtension_IsAMarkdownFile(string name, string expected)
    {
        Assert.Equal(expected, FileOperations.WithDefaultExtension(name));
    }

    [Fact]
    public void AProject_IsMadeInTheProjectsFolder_WhichIsMadeToo()
    {
        Assert.Empty(Projects.Names());
        Assert.Equal(DialogText.Success, Projects.Create(" Novel ", out var path));
        Assert.Equal(Path.Combine(Projects.Directory, "Novel"), path);
        Assert.True(Directory.Exists(path));
        // Not twice, and not with a name that cannot be used
        Assert.Equal(DialogText.NameExists, Projects.Create("Novel", out _));
        Assert.NotEqual(DialogText.Success, Projects.Create("a/b", out _));
        Assert.Null(Projects.NameProblem("Short stories"));
    }

    [Fact]
    public void TheProjects_AreTheFoldersInTheProjectsFolder_InAlphabeticalOrder()
    {
        foreach (var name in new[] { "b", "A", ".hidden", "c" }) Directory.CreateDirectory(Path.Combine(Projects.Directory, name));
        File.WriteAllText(Path.Combine(Projects.Directory, "a file.md"), "x");
        Assert.Equal(new[] { "A", "b", "c" }, Projects.Names());
    }

    [Fact]
    public void WhatAProjectHolds_DirectoriesFirst_AndHowManyMore()
    {
        Projects.Create("Book", out var path);
        Assert.Equal(Lang.T("(empty)"), Projects.Contents("Book", 5));
        Directory.CreateDirectory(Path.Combine(path, "notes"));
        foreach (var name in new[] { "c.md", "a.md", "b.md" }) File.WriteAllText(Path.Combine(path, name), "x");
        Assert.Equal("notes/\na.md\nb.md\nc.md", Projects.Contents("Book", 5));
        Assert.Equal("notes/\n" + Lang.F("... and {0} more", 3), Projects.Contents("Book", 2));
    }

    [Fact]
    public void Projects_InTheEditor_AsksTheHostToShowThem()
    {
        var editor = new EditorEngine("text", 40, 10, false);
        Assert.Equal(EditorRequest.Projects, editor.Type(":projects<CR>"));
        Assert.Equal(EditorRequest.Projects, editor.Type(":proj<CR>"));
        Assert.Equal(EditorRequest.Projects, editor.Type(":pro<CR>"));
        Assert.Equal("text", editor.Buffer.GetText());
        // Not in a field of a box
        Assert.NotEqual(EditorRequest.Projects, new EditorEngine("x", 40, 1, true).Type(":projects<CR>"));
    }

    [Fact]
    public void TheColorOfAProject_IsKeptInTheProject_AndTheFileGoesWithTheLastColor()
    {
        Projects.Create("Novel", out var path);
        var file = Path.Combine(path, Projects.InfoFileName);
        Assert.Null(Projects.ColorOf("Novel"));
        Assert.Equal(DialogText.Success, Projects.SetColor("Novel", ConsoleColor.DarkCyan));
        Assert.Equal(ConsoleColor.DarkCyan, Projects.ColorOf("Novel"));
        // LF, also on Windows
        Assert.DoesNotContain("\r", File.ReadAllText(file));
        Assert.Contains("color=DarkCyan\n", File.ReadAllText(file));
        Assert.Equal(DialogText.Success, Projects.SetColor("Novel", null));
        Assert.Null(Projects.ColorOf("Novel"));
        Assert.False(File.Exists(file));
        // Not among what the project holds
        Projects.SetColor("Novel", ConsoleColor.Red);
        Assert.Equal(Lang.T("(empty)"), Projects.Contents("Novel", 5));
    }

    [Fact]
    public void TheFileOfAProject_WrittenElsewhere_IsRead_AndItsOtherLinesAreKept()
    {
        Projects.Create("Novel", out var path);
        var file = Path.Combine(path, Projects.InfoFileName);
        File.WriteAllText(file, "# comment\r\ncolor=blue\r\nlater=1\r\n");
        Assert.Equal(ConsoleColor.Blue, Projects.ColorOf("Novel"));
        Projects.SetColor("Novel", null);
        Assert.Contains("later=1", File.ReadAllText(file));
        Assert.Null(Projects.ColorOf("Novel"));
        // Not a color
        File.WriteAllText(file, "color=Plaid\ncolor=99\n");
        Assert.Null(Projects.ColorOf("Novel"));
    }

    [Fact]
    public void TheNoteOfAProject_IsAMarkdownFile_ShownAsItsWordsInALine()
    {
        Projects.Create("Novel", out var path);
        Assert.Equal(string.Empty, Projects.NoteSnippet("Novel"));
        Assert.Equal(DialogText.Success, Projects.EnsureNote("Novel"));
        Assert.Equal(Path.Combine(path, Projects.NoteFileName), Projects.NotePath("Novel"));
        // Left empty: removed
        Projects.RemoveEmptyNote("Novel");
        Assert.False(File.Exists(Projects.NotePath("Novel")));

        File.WriteAllText(Projects.NotePath("Novel"), "# Plan\r\n\r\n> Send   chapters 1-3\n\tto the editor.\n");
        Assert.Equal("Plan Send chapters 1-3 to the editor.", Projects.NoteSnippet("Novel"));
        Projects.RemoveEmptyNote("Novel");
        Assert.True(File.Exists(Projects.NotePath("Novel")));
        // Making it again keeps what is written
        Projects.EnsureNote("Novel");
        Assert.StartsWith("# Plan", File.ReadAllText(Projects.NotePath("Novel")));
        Assert.Equal(Lang.T("(empty)"), Projects.Contents("Novel", 5));
    }

    [Fact]
    public void ANewProject_IsNotNamedAsAnother_InOtherUpperOrLowerCase()
    {
        // On Linux both could be made, but not where the folder may be synced to (Windows, macOS)
        Projects.Create("Novel", out _);
        Assert.Equal(DialogText.NameExists, Projects.NameProblem("novel"));
        Assert.Equal(DialogText.NameExists, Projects.NameProblem("NOVEL"));
        Assert.Null(Projects.NameProblem("Novels"));
    }

    [Fact]
    public void TheProjectsFolderOfTheSettings_IsAFullPathOfThisSystem()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, Projects.DirectoryFromSetting("~"));
        Assert.Equal(Path.Combine(home, "Writing"), Projects.DirectoryFromSetting(" ~/Writing "));
        Assert.Equal(_directory, Projects.DirectoryFromSetting(_directory));
        Assert.Null(Projects.DirectoryFromSetting(""));
        Assert.Null(Projects.DirectoryFromSetting("Writing"));
        // Written on another system
        Assert.Null(Projects.DirectoryFromSetting(OperatingSystem.IsWindows() ? "/home/me/Writing" : @"C:\Users\me\Writing"));
    }
}
