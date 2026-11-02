namespace Blyant.Tests;

/// <summary>
/// Several lists of favorites: one is open at a time, and a file that is renamed, moved or deleted is followed in all
/// of them
/// </summary>
[Collection("Static state")]
public class FavoriteListsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-favorite-lists-" + Guid.NewGuid().ToString("N"));
    private readonly bool _toTrash = FileOperations.DeleteToTrash;

    public FavoriteListsTests()
    {
        Directory.CreateDirectory(_directory);
        FileFavorites.FilePathOverride = Path.Combine(_directory, FileFavorites.FileName);
        FileFavorites.CurrentList = string.Empty;
        FileFavorites.Load();
        FileOperations.DeleteToTrash = false;
    }

    public void Dispose()
    {
        FileFavorites.CurrentList = string.Empty;
        FileFavorites.FilePathOverride = null;
        FileFavorites.Parse(string.Empty);
        FileOperations.DeleteToTrash = _toTrash;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.Combine(_directory, name);

    private static string Names() => string.Join(" ", FileFavorites.Items.Select(item => Path.GetFileName(item.Path)));

    // A list with these files
    private void MakeList(string name, params string[] files)
    {
        Assert.Equal(DialogText.Success, FileFavorites.NewList(name));
        FileFavorites.UseList(name);
        foreach (var file in files) FileFavorites.Toggle(In(file));
        Assert.Equal(DialogText.Success, FileFavorites.Save());
    }

    [Fact]
    public void TheMainList_IsTheFileItAlwaysWas()
    {
        Assert.Equal(In(FileFavorites.FileName), FileFavorites.FilePath);
        Assert.Empty(FileFavorites.ListNames());
    }

    [Fact]
    public void AList_IsAFileOfItsOwn_AndOneIsOpen()
    {
        FileFavorites.Toggle(In("main.md"));
        FileFavorites.Save();
        MakeList("Work", "report.md");
        Assert.Equal(Path.Combine(_directory, "favorites", "Work.txt"), FileFavorites.FilePath);
        Assert.Equal("report.md", Names());
        FileFavorites.UseList(string.Empty);
        Assert.Equal("main.md", Names());
        FileFavorites.UseList("Work");
        Assert.Equal("report.md", Names());
        Assert.Equal(new[] { "Work" }, FileFavorites.ListNames());
    }

    [Fact]
    public void AListIsFoundByItsName_InAnyLetterCase()
    {
        MakeList("Work");
        Assert.Equal("Work", FileFavorites.FindList("WORK"));
        Assert.Equal(string.Empty, FileFavorites.FindList("main"));
        Assert.Equal(string.Empty, FileFavorites.FindList(" Main "));
        Assert.Null(FileFavorites.FindList("Home"));
        Assert.Equal(new[] { FileFavorites.MainListWord, "Work" }, FileFavorites.ListWords());
    }

    [Theory]
    [InlineData("Work", true)]
    [InlineData("Book 2", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("main", false)]
    [InlineData("MAIN", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("..", false)]
    [InlineData(".hidden", false)]
    [InlineData("a:b", false)]
    [InlineData("a name that is too long for the top bar", false)]
    public void ANameCanBeUsed_OrNot(string name, bool valid)
    {
        Assert.Equal(valid, FileFavorites.IsValidListName(name));
    }

    [Fact]
    public void ANameThatIsTaken_IsRefused()
    {
        MakeList("Work");
        Assert.NotEqual(DialogText.Success, FileFavorites.NewList("work"));
        Assert.Single(FileFavorites.ListNames());
    }

    [Fact]
    public void ANameThatIsTooLong_IsToldAsThat()
    {
        Lang.Set(Lang.English);
        var tooLong = new string('x', 25);
        Assert.Equal("Name too long (at most 24 characters)", FileFavorites.NewList(tooLong));
        Assert.Equal(DialogText.Success, FileFavorites.NewList(new string('x', 24)));
        Assert.Equal("Name too long (at most 24 characters)", FileFavorites.RenameList(new string('x', 24), tooLong));
        // Another name that cannot be used says so
        Assert.Equal("That name cannot be used for a list", FileFavorites.NewList("a/b"));
    }

    [Fact]
    public void ARename_KeepsTheListOpen()
    {
        MakeList("Work", "report.md");
        MakeList("Home");
        FileFavorites.UseList("Work");
        Assert.NotEqual(DialogText.Success, FileFavorites.RenameList("Work", "home"));
        Assert.Equal(DialogText.Success, FileFavorites.RenameList("Work", "Job"));
        Assert.Equal("Job", FileFavorites.CurrentList);
        Assert.Equal(new[] { "Home", "Job" }, FileFavorites.ListNames());
        FileFavorites.Load();
        Assert.Equal("report.md", Names());
        // Only another way of writing the name
        Assert.Equal(DialogText.Success, FileFavorites.RenameList("Job", "JOB"));
        Assert.Equal("JOB", FileFavorites.CurrentList);
    }

    [Fact]
    public void DeletingTheOpenList_OpensTheMainList()
    {
        FileFavorites.Toggle(In("main.md"));
        FileFavorites.Save();
        MakeList("Work", "report.md");
        Assert.Equal(DialogText.Success, FileFavorites.DeleteList("Work", out _));
        Assert.Equal(string.Empty, FileFavorites.CurrentList);
        Assert.Equal("main.md", Names());
        Assert.Empty(FileFavorites.ListNames());
    }

    [Fact]
    public void AListThatIsGone_LeavesTheMainListOpen()
    {
        MakeList("Work", "report.md");
        File.Delete(FileFavorites.FilePath);
        FileFavorites.Load();
        Assert.Equal(string.Empty, FileFavorites.CurrentList);
    }

    [Fact]
    public void ARenamedFile_IsFollowedInAllTheLists()
    {
        FileFavorites.Toggle(In("draft.md"));
        FileFavorites.Save();
        MakeList("Work", "draft.md", "report.md");
        MakeList("Home", "other.md");
        // The list that is open is told; the others are saved
        Assert.False(FileFavorites.PathChanged(In("draft.md"), In("final.md")));
        Assert.Equal("other.md", Names());
        FileFavorites.UseList("Work");
        Assert.Equal("final.md report.md", Names());
        FileFavorites.UseList(string.Empty);
        Assert.Equal("final.md", Names());
        // In the list that is open too: it is saved by the one who asked
        Assert.True(FileFavorites.PathChanged(In("final.md"), In("last.md")));
        Assert.Equal("last.md", Names());
        FileFavorites.UseList("Work");
        Assert.Equal("last.md report.md", Names());
    }

    [Fact]
    public void WhatIsInADirectory_IsFollowedInAllTheLists()
    {
        MakeList("Work", Path.Combine("book", "ch1.md"));
        FileFavorites.UseList(string.Empty);
        FileFavorites.PathChanged(In("book"), In("novel"));
        FileFavorites.UseList("Work");
        Assert.Equal(In(Path.Combine("novel", "ch1.md")), FileFavorites.Items[0].Path);
    }

    [Fact]
    public void ADeletedFile_LeavesAllTheLists()
    {
        MakeList("Work", "gone.md", "report.md");
        MakeList("Home", "gone.md");
        Assert.True(FileFavorites.PathDeleted(In("gone.md")));
        Assert.Equal(string.Empty, Names());
        FileFavorites.UseList("Work");
        Assert.Equal("report.md", Names());
    }

    [Fact]
    public void ThePartsOfAnotherList_AreKept()
    {
        MakeList("Work", "a.md", "b.md");
        FileFavorites.AddFolder("Folder", -1);
        FileFavorites.SetNote(FileFavorites.Items[0], "A note");
        FileFavorites.Save();
        var before = File.ReadAllText(FileFavorites.FilePath);
        FileFavorites.UseList(string.Empty);
        FileFavorites.PathChanged(In("a.md"), In("c.md"));
        Assert.Equal(before.Replace(In("a.md"), In("c.md")), File.ReadAllText(Path.Combine(_directory, "favorites", "Work.txt")));
    }

    [Fact]
    public void AListThatChangesNothing_IsNotWritten()
    {
        MakeList("Work", "report.md");
        var path = FileFavorites.FilePath;
        File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        FileFavorites.UseList(string.Empty);
        FileFavorites.PathChanged(In("other.md"), In("another.md"));
        Assert.Equal(2020, File.GetLastWriteTimeUtc(path).Year);
    }

    [Fact]
    public void ThePreview_ShowsWhatIsInAList_AndLeavesTheOpenListAlone()
    {
        FileFavorites.Toggle(In("main.md"));
        FileFavorites.Save();
        MakeList("Work", "report.md");
        FileFavorites.UseList(string.Empty);
        Assert.Equal("report.md", FileFavorites.ListPreview("Work"));
        Assert.Equal("main.md", FileFavorites.ListPreview(string.Empty));
        Assert.Equal("main.md", Names());
    }

    [UnixFact]
    public void TheLetterCaseOfAName_IsTheOneOfTheFile()
    {
        MakeList("Work");
        // ("work" is another file name here, but the same list)
        Assert.Equal("Work", FileFavorites.FindList("work"));
        Assert.NotEqual(DialogText.Success, FileFavorites.NewList("work"));
    }
}
