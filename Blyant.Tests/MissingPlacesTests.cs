namespace Blyant.Tests;

/// <summary>
/// The entries of the lists whose file or directory is gone (":missing", ":prune"): an entry on a drive that is not
/// connected is missing, but kept by ":prune"
/// </summary>
public class MissingPlacesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "blyant-missing-" + Guid.NewGuid().ToString("N"));

    public MissingPlacesTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void AFileOrDirectoryThatIsThere_IsNotMissing()
    {
        var file = Path.Combine(_directory, "notes.md");
        File.WriteAllText(file, "x");
        Assert.False(MissingPlaces.IsMissing(file));
        Assert.False(MissingPlaces.IsMissing(_directory));
        Assert.True(MissingPlaces.IsMissing(Path.Combine(_directory, "gone.md")));
        Assert.True(MissingPlaces.IsMissing(Path.Combine(_directory, "gone", "deeper")));
        // A folder that is there: the drive is too
        Assert.False(MissingPlaces.OnAbsentDrive(Path.Combine(_directory, "gone.md")));
    }

    [Fact]
    public void ADriveMountedInMediaMntOrVolumes_IsAbsentWhenItsFolderIsGone()
    {
        var there = new HashSet<string> { "/media", "/media/anna", "/mnt", "/Volumes", "/run/media", "/run/media/anna" };
        bool Exists(string path) => there.Contains(path);
        // The drive is not mounted: its folder is not there
        Assert.True(MissingPlaces.OnAbsentDrive("/media/anna/USB/book/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/run/media/anna/USB/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/mnt/backup/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/Volumes/USB/ch1.md", Exists));
        // The directory of the drive itself (as in the hotlist)
        Assert.True(MissingPlaces.OnAbsentDrive("/Volumes/USB", Exists));
        // The drive is there: the file is gone from it
        there.Add("/media/anna/USB");
        there.Add("/Volumes/USB");
        Assert.False(MissingPlaces.OnAbsentDrive("/media/anna/USB/book/ch1.md", Exists));
        Assert.False(MissingPlaces.OnAbsentDrive("/Volumes/USB/ch1.md", Exists));
        // Not on a drive mounted there
        Assert.False(MissingPlaces.OnAbsentDrive("/home/anna/gone/ch1.md", Exists));
    }

    [WindowsFact]
    public void ADriveLetterOrShareThatIsNotThere_IsAbsent()
    {
        var there = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\" };
        bool Exists(string path) => there.Contains(path);
        Assert.True(MissingPlaces.OnAbsentDrive(@"E:\Book\ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive(@"\\server\share\Book\ch1.md", Exists));
        Assert.False(MissingPlaces.OnAbsentDrive(@"C:\Book\ch1.md", Exists));
    }

    [Fact]
    public void Sort_PrunesTheMissing_AndKeepsThoseOnDrivesThatAreNotThere()
    {
        var there = new HashSet<string> { "/media", "/media/anna", "/mnt" };
        var (prune, kept) = MissingPlaces.Sort(
            new[] { "/home/anna/gone.md", "/media/anna/USB/ch1.md", "/mnt/backup/old", "/home/anna/old/" },
            path => path, path => there.Contains(path));
        Assert.Equal(new[] { "/home/anna/gone.md", "/home/anna/old/" }, prune);
        Assert.Equal(new[] { "/media/anna/USB/ch1.md", "/mnt/backup/old" }, kept);
    }
}
