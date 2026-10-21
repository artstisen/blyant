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
        Assert.Equal("text", editor.Buffer.GetText());
        // Not in a field of a box
        Assert.NotEqual(EditorRequest.Projects, new EditorEngine("x", 40, 1, true).Type(":projects<CR>"));
    }
}
