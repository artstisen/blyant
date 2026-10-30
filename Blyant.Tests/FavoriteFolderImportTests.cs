namespace Blyant.Tests;

/// <summary>
/// ":import folder" in the favorites: the files of a directory as a folder of favorites, also those of the folders
/// below it (at any depth, all in the one folder), hidden files left out, favorites that are there kept, and the new
/// files of the same directory added to its folder
/// </summary>
[Collection("Static state")]
public class FavoriteFolderImportTests : IDisposable
{
    private readonly string _directory;
    private readonly string _book;

    public FavoriteFolderImportTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blyant-import-folder-" + Guid.NewGuid().ToString("N"));
        _book = Path.Combine(_directory, "Book");
        Directory.CreateDirectory(_book);
        FileFavorites.FilePathOverride = Path.Combine(_directory, FileFavorites.FileName);
        FileFavorites.Parse(string.Empty);
        // The book: its own files, a hidden file and folder, chapters with drafts below them, notes, an empty folder
        foreach (var name in new[]
                 {
                     "b.txt", "a.md", ".hidden.md", Path.Combine(".git", "config"), Path.Combine("chapters", "c2.md"),
                     Path.Combine("chapters", "c1.md"), Path.Combine("chapters", "drafts", "old.md"),
                     Path.Combine("chapters", "drafts", "early", "first", "start.md"), Path.Combine("notes", "n.md")
                 })
        {
            var path = Path.Combine(_book, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
        }

        Directory.CreateDirectory(Path.Combine(_book, "empty"));
    }

    public void Dispose()
    {
        FileFavorites.FilePathOverride = null;
        FileFavorites.Parse(string.Empty);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    // The list as text: folders in brackets, a "." in front for each folder an item is in
    private static string Show() => string.Join(" ", FileFavorites.Items.Select(item =>
        new string('.', item.Level) + (item.IsFolder ? "[" + item.Name + "]" : Path.GetFileName(item.Path))));

    [Fact]
    public void TheFilesOfAFolder_AreReadByName_WithoutHiddenOnes()
    {
        var own = FileFavorites.ReadFolder(_book, false)!;
        Assert.Equal("Book", own.Name);
        Assert.Equal(new[] { "a.md", "b.txt" }, own.Files.Select(Path.GetFileName));
        // Its own files, then those of each folder below it, at any depth (the files of a folder together); .git is
        // left out
        var all = FileFavorites.ReadFolder(_book, true)!;
        Assert.Equal(new[] { "a.md", "b.txt", "c1.md", "c2.md", "old.md", "start.md", "n.md" },
            all.Files.Select(Path.GetFileName));
        Assert.Equal(7, all.Count);
        // More files than wanted: none
        Assert.Null(FileFavorites.ReadFolder(_book, true, 5));
        Assert.NotNull(FileFavorites.ReadFolder(_book, false, 2));
        Assert.Null(FileFavorites.ReadFolder(Path.Combine(_directory, "not there"), false));
    }

    [Fact]
    public void AFolder_IsAdded_WithItsSubfolders_AndAgainOnlyItsNewFiles()
    {
        // A favorite there already stays where it is
        FileFavorites.Toggle(Path.Combine(_book, "b.txt"));
        var all = FileFavorites.ReadFolder(_book, true)!;
        var plan = FileFavorites.ImportFolder(all, false);
        Assert.Equal((6, 1, 1), (plan.Added, plan.Skipped, plan.NewFolders));
        // (only counted: the list is as it was)
        Assert.Equal("b.txt", Show());
        FileFavorites.ImportFolder(all, true);
        // All in the one folder
        Assert.Equal("b.txt [Book] .a.md .c1.md .c2.md .old.md .start.md .n.md", Show());
        Assert.Equal(1, FileFavorites.FolderIndex("book"));
        // The same folder again, with a new file deep down: only that is added, to the folder there
        File.WriteAllText(Path.Combine(_book, "chapters", "drafts", "c3.md"), "x");
        var again = FileFavorites.ImportFolder(FileFavorites.ReadFolder(_book, true)!, true);
        Assert.Equal((1, 0, 1), (again.Added, again.NewFolders, again.MergedFolders));
        Assert.Equal("b.txt [Book] .a.md .c1.md .c2.md .old.md .start.md .n.md .c3.md", Show());
    }

    [UnixFact]
    public void ALinkToAnotherFolder_IsNotFollowed()
    {
        // (a link to the folder itself would go round in a circle)
        Directory.CreateSymbolicLink(Path.Combine(_book, "loop"), _book);
        Assert.Equal(7, FileFavorites.ReadFolder(_book, true)!.Count);
    }

    [Fact]
    public void ItsOwnFilesOnly_AndANameWithBrackets()
    {
        FileFavorites.ImportFolder(FileFavorites.ReadFolder(_book, false)!, true);
        Assert.Equal("[Book] .a.md .b.txt", Show());
        // Brackets mark a folder in the file: they are changed in the name
        Assert.Equal("Book (draft)", FileFavorites.FolderNameOf(Path.Combine(_directory, "Book [draft]")));
        Assert.True(FileFavorites.IsValidFolderName(FileFavorites.FolderNameOf(Path.Combine(_directory, "Book [draft]"))));
    }
}
