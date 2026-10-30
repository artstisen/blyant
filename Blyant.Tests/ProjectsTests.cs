namespace Blyant.Tests;

/// <summary>
/// The projects (folders anywhere, kept in a list), the name of a new file (Markdown without an extension), and
/// ":projects" in the editor
/// </summary>
[Collection("Static state")]
public class ProjectsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-projects-" + Guid.NewGuid().ToString("N"));

    public ProjectsTests()
    {
        Projects.ListPathOverride = Path.Combine(_directory, "data", Projects.ListFileName);
    }

    public void Dispose()
    {
        Projects.ListPathOverride = null;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(params string[] parts) => Path.Combine(new[] { _directory }.Concat(parts).ToArray());

    // The folder the projects of a test are made in (as one the user picks)
    private string Writing => In("Writing");

    // A project made in the folder Writing
    private string Make(string name)
    {
        Assert.Equal(DialogText.Success, Projects.Create(Writing, name, out var path));
        return path;
    }

    private static List<string> NamesOf(IEnumerable<Projects.Project> projects) =>
        projects.Select(project => project.Name).ToList();

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
    public void ANewFile_GetsTheKindSet_WhenItsNameHasNoExtension()
    {
        try
        {
            FileOperations.DefaultExtension = ".txt";
            Assert.Equal("notes.txt", FileOperations.WithDefaultExtension("notes"));
            Assert.Equal("page.md", FileOperations.WithDefaultExtension("page.md"));
            Assert.Equal(".gitignore", FileOperations.WithDefaultExtension(".gitignore"));
            Assert.Equal(".html", FileOperations.ParseFileType("HTML"));
            Assert.Equal(".ini", FileOperations.ParseFileType(".ini"));
            Assert.Null(FileOperations.ParseFileType("docx"));
            Assert.Equal("C#", FileOperations.FileTypeName(".cs"));
            Assert.Equal(".typ", FileOperations.ParseFileType("typ"));
            Assert.Equal("Typst", FileOperations.FileTypeName(".typ"));
            Assert.Equal("typst.typ", FileOperations.WithDefaultExtension("typst.typ"));
            Assert.Equal(11, FileOperations.FileTypes.Count);
            // Typst comes after Markdown
            Assert.Equal(new[] { ".md", ".typ", ".txt" }, FileOperations.FileTypes.Take(3).Select(type => type.Extension));
        }
        finally
        {
            FileOperations.DefaultExtension = FileOperations.MarkdownExtension;
        }
    }

    [Fact]
    public void ANewProject_IsMadeInTheFolderPicked_WhichIsMadeToo_AndIsOnTheList()
    {
        Assert.Empty(Projects.All());
        Assert.Equal(DialogText.Success, Projects.Create(Writing, " Novel ", out var path));
        Assert.Equal(Path.Combine(Writing, "Novel"), path);
        Assert.True(Directory.Exists(path));
        Assert.Equal(new[] { path }, Projects.ListedPaths());
        // Not twice, and not with a name that cannot be used
        Assert.Equal(DialogText.NameExists, Projects.Create(Writing, "Novel", out _));
        Assert.NotEqual(DialogText.Success, Projects.Create(Writing, "a/b", out _));
        Assert.Null(Projects.NameProblem(Writing, "Short stories"));
        // Made somewhere else: on the list too
        Assert.Equal(DialogText.Success, Projects.Create(In("Poetry"), "Songs", out var songs));
        Assert.Equal(new[] { path, songs }, Projects.ListedPaths());
    }

    [Fact]
    public void TheProjects_AreInAlphabeticalOrder_WhereverTheyAre()
    {
        foreach (var path in new[] { In("x", "b"), In("y", "A"), In("z", "c") })
        {
            Directory.CreateDirectory(path);
            Assert.Equal(DialogText.Success, Projects.Add(path));
        }

        Assert.Equal(new[] { "A", "b", "c" }, NamesOf(Projects.All()));
    }

    [Fact]
    public void AFolderThatIsThere_IsAdded_AndTakenOffTheListAgain()
    {
        Make("Novel");
        var essays = In("Elsewhere", "Essays");
        Directory.CreateDirectory(essays);
        Assert.Equal(DialogText.Success, Projects.Add(essays));
        Assert.Equal(new[] { "Essays", "Novel" }, NamesOf(Projects.All()));
        // Not twice, and not a folder that is not there
        Assert.Equal(DialogText.ProjectAlready, Projects.Add(essays + Path.DirectorySeparatorChar));
        Assert.Equal(DialogText.ProjectAlready, Projects.Add(Path.Combine(Writing, "Novel")));
        Assert.Equal(DialogText.DirectoryNotFound, Projects.Add(In("nowhere")));
        // Taken off the list: the folder stays
        Assert.Equal(DialogText.Success, Projects.Remove(essays));
        Assert.Equal(new[] { "Novel" }, NamesOf(Projects.All()));
        Assert.True(Directory.Exists(essays));
    }

    [Fact]
    public void Projects_FollowTheirFolder_AndOneThatIsGoneIsMissing()
    {
        var essays = In("Elsewhere", "Essays");
        Directory.CreateDirectory(essays);
        Projects.Add(essays);
        // The folder it is in is renamed
        Projects.PathChanged(In("Elsewhere"), In("Moved"));
        Assert.Equal(new[] { In("Moved", "Essays") }, Projects.ListedPaths());
        // It is renamed itself
        Projects.PathChanged(In("Moved", "Essays"), In("Moved", "Articles"));
        Assert.Equal(new[] { In("Moved", "Articles") }, Projects.ListedPaths());
        // Not there: missing, but still on the list
        Assert.True(Projects.All().Single().Missing);
    }

    [Fact]
    public void ProjectsOfTheSameName_AreToldApart_ByTheFolderTheyAreIn()
    {
        Make("Novel");
        var other = In("Old", "Novel");
        Directory.CreateDirectory(other);
        Projects.Add(other);
        var all = Projects.All();
        Assert.Equal(new[] { "Novel (Old)", "Novel (Writing)" }, Projects.DisplayNames(all).OrderBy(n => n));
    }

    [Fact]
    public void TheListOfProjects_IsAFileOfPaths()
    {
        var a = In("a");
        Directory.CreateDirectory(a);
        Projects.Add(a);
        var text = File.ReadAllText(Projects.ListPath);
        Assert.StartsWith("# ", text);
        Assert.Contains("\n" + a + "\n", text);
        // Lines written by hand: blank lines and comments are skipped, and a path that is not a full path
        File.WriteAllText(Projects.ListPath, "# mine\r\n\r\n" + a + "\r\nrelative\\path\r\n");
        Assert.Equal(new[] { a }, Projects.ListedPaths());
    }

    [Fact]
    public void WhatAProjectHolds_DirectoriesFirst_AndHowManyMore()
    {
        var path = Make("Book");
        Assert.Equal(Lang.T("(empty)"), Projects.Contents(path, 5));
        Directory.CreateDirectory(Path.Combine(path, "notes"));
        foreach (var name in new[] { "c.md", "a.md", "b.md" }) File.WriteAllText(Path.Combine(path, name), "x");
        Assert.Equal("notes/\na.md\nb.md\nc.md", Projects.Contents(path, 5));
        Assert.Equal("notes/\n" + Lang.F("... and {0} more", 3), Projects.Contents(path, 2));
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
        var path = Make("Novel");
        var file = Path.Combine(path, Projects.InfoFileName);
        Assert.Null(Projects.ColorOf(path));
        Assert.Equal(DialogText.Success, Projects.SetColor(path, ConsoleColor.DarkCyan));
        Assert.Equal(ConsoleColor.DarkCyan, Projects.ColorOf(path));
        // LF, also on Windows
        Assert.DoesNotContain("\r", File.ReadAllText(file));
        Assert.Contains("color=DarkCyan\n", File.ReadAllText(file));
        Assert.Equal(DialogText.Success, Projects.SetColor(path, null));
        Assert.Null(Projects.ColorOf(path));
        Assert.False(File.Exists(file));
        // Not among what the project holds
        Projects.SetColor(path, ConsoleColor.Red);
        Assert.Equal(Lang.T("(empty)"), Projects.Contents(path, 5));
    }

    [Fact]
    public void TheFileOfAProject_WrittenElsewhere_IsRead_AndItsOtherLinesAreKept()
    {
        var path = Make("Novel");
        var file = Path.Combine(path, Projects.InfoFileName);
        File.WriteAllText(file, "# comment\r\ncolor=blue\r\nlater=1\r\n");
        Assert.Equal(ConsoleColor.Blue, Projects.ColorOf(path));
        Projects.SetColor(path, null);
        Assert.Contains("later=1", File.ReadAllText(file));
        Assert.Null(Projects.ColorOf(path));
        // Not a color
        File.WriteAllText(file, "color=Plaid\ncolor=99\n");
        Assert.Null(Projects.ColorOf(path));
    }

    [Fact]
    public void TheNoteOfAProject_IsAMarkdownFile_ShownAsItsWordsInALine()
    {
        var path = Make("Novel");
        Assert.Equal(string.Empty, Projects.NoteSnippet(path));
        Assert.Equal(DialogText.Success, Projects.EnsureNote(path));
        Assert.Equal(Path.Combine(path, Projects.NoteFileName), Projects.NotePath(path));
        // Left empty: removed
        Projects.RemoveEmptyNote(path);
        Assert.False(File.Exists(Projects.NotePath(path)));

        File.WriteAllText(Projects.NotePath(path), "# Plan\r\n\r\n> Send   chapters 1-3\n\tto the editor.\n");
        Assert.Equal("Plan Send chapters 1-3 to the editor.", Projects.NoteSnippet(path));
        Projects.RemoveEmptyNote(path);
        Assert.True(File.Exists(Projects.NotePath(path)));
        // Making it again keeps what is written
        Projects.EnsureNote(path);
        Assert.StartsWith("# Plan", File.ReadAllText(Projects.NotePath(path)));
        Assert.Equal(Lang.T("(empty)"), Projects.Contents(path, 5));
    }

    [Fact]
    public void ANewProject_IsNotNamedAsAnother_InOtherUpperOrLowerCase()
    {
        // On Linux both could be made, but not where the folder may be synced to (Windows, macOS)
        Make("Novel");
        Assert.Equal(DialogText.NameExists, Projects.NameProblem(Writing, "novel"));
        Assert.Equal(DialogText.NameExists, Projects.NameProblem(Writing, "NOVEL"));
        Assert.Null(Projects.NameProblem(Writing, "Novels"));
    }

    [Fact]
    public void ANewFile_CanBeMadeInThePlacesSaved_EachOnce_AndOnlyThoseThatAreThere()
    {
        var saved = (FileExplorer.StartDirectory, FileExplorer.StartDirectory2, FileExplorer.PickPlace);
        try
        {
            var one = In("One");
            var two = In("Two");
            var picked = In("Picked", "Essays");
            foreach (var directory in new[] { one, two, picked }) Directory.CreateDirectory(directory);
            FileExplorer.StartDirectory = one;
            FileExplorer.StartDirectory2 = two;
            FileExplorer.PickPlace = picked;
            Assert.Equal(new[] { one, two, picked }, StartMenu.NewFilePlaces().Select(place => place.Directory));
            // The same place twice is there once, and a folder that is not there is left out
            FileExplorer.StartDirectory2 = one + Path.DirectorySeparatorChar;
            FileExplorer.PickPlace = In("Gone");
            Assert.Equal(new[] { one }, StartMenu.NewFilePlaces().Select(place => place.Directory));
            // Where the picking was canceled (ending with a separator): that directory
            FileExplorer.PickPlace = two + Path.DirectorySeparatorChar;
            Assert.Equal(new[] { one, two }, StartMenu.NewFilePlaces().Select(place => place.Directory));
        }
        finally
        {
            (FileExplorer.StartDirectory, FileExplorer.StartDirectory2, FileExplorer.PickPlace) = saved;
        }
    }
}
