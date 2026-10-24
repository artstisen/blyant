using System.Text.RegularExpressions;

namespace Blyant.Tests;

/// <summary>
/// Files are written safely (AGENTS.md): a file that is there is replaced with FileIo.WriteAllBytesSafe, a new
/// file is made with FileMode.CreateNew, and a list that only grows is appended to. The source is read for the
/// ways of writing that would empty a file first, so that an interrupted write could leave it empty or half written
/// </summary>
[Collection("Static state")]
public class SafeWriteTests
{
    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Blyant.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "Blyant");
    }

    // The ways of writing that empty a file before the new content is in it (also the Async ones, and those of
    // FileInfo: info.CreateText(), info.OpenWrite())
    private static readonly Regex Unsafe = new(
        @"\bFile\.(WriteAllText|WriteAllBytes|WriteAllLines|Create|CreateText|OpenWrite)(Async)?\s*\(|" +
        @"FileMode\.(Create|Truncate|OpenOrCreate)\b|new\s+StreamWriter\s*\(|\.(CreateText|OpenWrite)\s*\(\s*\)");

    // A stream opened with a mode: the mode is named on the line (and then checked by Unsafe), or it is set in a
    // FileStreamOptions of that name in the file (where "Mode = FileMode..." is checked by Unsafe)
    private static readonly Regex Opened = new(@"\b(File\.Open|new\s+FileStream)\s*\(\s*[^,]+,\s*(\w+)\s*[,)]");

    // File.Create with FileOptions.DeleteOnClose (FileIo.CanWriteIn tests a directory with a file that is gone
    // when it is closed): the only use of File.Create that is allowed
    private static bool IsAllowed(string line, Match match) =>
        match.Value.StartsWith("File.Create(", StringComparison.Ordinal) && line.Contains("FileOptions.DeleteOnClose");

    [Fact]
    public void FilesAreNotEmptiedBeforeTheyAreWritten()
    {
        var root = SourceDirectory();
        var found = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            found.AddRange(UnsafeLines(File.ReadAllText(path))
                .Select(line => Path.GetRelativePath(root, path) + ":" + line));
        }

        Assert.True(found.Count == 0, "Use FileIo.WriteAllBytesSafe (or FileMode.CreateNew for a new file):\n" +
                                      string.Join("\n", found));
    }

    // The lines of a source file that write in a way that empties a file first ("number: line")
    private static List<string> UnsafeLines(string text)
    {
        var found = new List<string>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var where = (i + 1) + ": " + line.Trim();
            if (Unsafe.Matches(line).Any(match => !IsAllowed(line, match))) found.Add(where);
            // A stream opened with a mode that is not named, and not set in FileStreamOptions of the file
            if (Opened.Match(line) is { Success: true } opened && !line.Contains("FileMode.") &&
                !Regex.IsMatch(text, @"\b" + opened.Groups[2].Value + @"\s*=\s*new\s+FileStreamOptions\b"))
            {
                found.Add(where + " (the mode is not seen)");
            }
        }

        return found;
    }

    [Theory]
    [InlineData("File.WriteAllTextAsync(path, text);")]
    [InlineData("await File.WriteAllBytesAsync(path, bytes);")]
    [InlineData("using var writer = info.CreateText();")]
    [InlineData("using var stream = new FileInfo(path).OpenWrite();")]
    [InlineData("using var stream = new FileStream(path, mode);")]
    [InlineData("using var stream = File.Open(path, mode, FileAccess.Write);")]
    [InlineData("var options = new FileStreamOptions { Mode = FileMode.Create };")]
    [InlineData("using (File.Create(path)) { }")]
    public void TheWaysOfWritingThatEmptyAFile_AreSeen(string line)
    {
        Assert.Single(UnsafeLines(line));
    }

    [Theory]
    [InlineData("using var stream = new FileStream(path, FileMode.Open);")]
    [InlineData("using (File.Create(test, 1, FileOptions.DeleteOnClose)) { }")]
    [InlineData("var options = new FileStreamOptions { Mode = FileMode.CreateNew };\nnew FileStream(path, options);")]
    public void TheSafeWays_AreNotSeen(string text)
    {
        Assert.Empty(UnsafeLines(text));
    }

    [Theory]
    [InlineData("File.Replace(from, to, backup);")]
    [InlineData("await File.AppendAllTextAsync(path, text);")]
    [InlineData("using var writer = File.AppendText(path);")]
    [InlineData("new FileInfo(path).Delete();")]
    [InlineData("new DirectoryInfo(path).Delete(true);")]
    [InlineData("info.IsReadOnly = false;")]
    [InlineData("info.Attributes = FileAttributes.Normal;")]
    public void TheOperationsThatAreNotLogged_AreSeen(string line)
    {
        Assert.Matches(Unlogged, line);
    }

    // The ways of changing files and directories that are only used in FileIo (which logs them), in
    // FileOperations (the operations of the file browser, which logs them as a whole) and in EventLog (the log file)
    // (also File.Replace and File.AppendText, the Async ones, and those of FileInfo and DirectoryInfo: Delete(),
    // and setting Attributes or IsReadOnly)
    private static readonly Regex Unlogged = new(
        @"\b(File|Directory)\.(Delete|Move|Copy|Replace|CreateDirectory|CreateSymbolicLink|AppendAllText|" +
        @"AppendAllLines|AppendText|SetAttributes)(Async)?\s*\(|FileMode\.CreateNew|\.CopyTo\s*\(|\.MoveTo\s*\(|" +
        @"\.Delete\s*\(\s*(true|false)?\s*\)|\.(Attributes|IsReadOnly)\s*=[^=]");

    private static readonly string[] LoggingFiles = { "FileIo.cs", "FileOperations.cs", "EventLog.cs" };

    [Fact]
    public void FileOperations_GoThroughFileIo_SoTheyAreLogged()
    {
        var root = SourceDirectory();
        var found = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                LoggingFiles.Contains(Path.GetFileName(path))) continue;
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Unlogged.IsMatch(lines[i]))
                {
                    found.Add(Path.GetRelativePath(root, path) + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }
        }

        Assert.True(found.Count == 0, "Use the methods of FileIo (CreateDirectory, CreateNewFile, DeleteFile, " +
                                      "MoveFile, CopyFile, AppendText), which log what they do:\n" +
                                      string.Join("\n", found));
    }

    [Fact]
    public void FileIo_LogsWhatItDoes_WithThePathOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "blyant_logged_" + Guid.NewGuid().ToString("N"));
        try
        {
            EventLog.Clear();
            var file = Path.Combine(directory, "sub", "list.txt");
            FileIo.CreateDirectory(Path.Combine(directory, "sub"));
            FileIo.WriteAllBytesSafe(file, "secret text"u8.ToArray());
            FileIo.WriteAllBytesSafe(file, "more secret text"u8.ToArray());
            FileIo.AppendText(file, "added secret");
            FileIo.CopyFile(file, file + ".bad", false);
            FileIo.MoveFile(file + ".bad", file + ".old", false);
            FileIo.DeleteFile(file + ".old");
            Assert.ThrowsAny<IOException>(() => FileIo.CreateNewFile(file));
            var log = EventLog.All.Select(line => line[11..]).ToList();
            Assert.Equal(new[]
            {
                "File: New directory: " + directory,
                "File: New directory: " + Path.Combine(directory, "sub"),
                "File: New: " + file,
                "File: Write: " + file,
                "File: Append: " + file,
                "File: Copy: " + file + " -> " + file + ".bad",
                "File: Rename: " + file + ".bad -> " + file + ".old",
                "File: Delete: " + file + ".old"
            }, log.Take(8));
            Assert.StartsWith("File: New: " + file + " - FAILED: ", log[8]);
            // What is written is never logged
            Assert.DoesNotContain(log, line => line.Contains("secret"));
        }
        finally
        {
            EventLog.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SafeWrite_ReplacesTheFile_AndLeavesNothingBehind()
    {
        var directory = Path.Combine(Path.GetTempPath(), "blyant_safe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "chapter.md");
            File.WriteAllText(path, "old text");
            FileIo.WriteAllBytesSafe(path, "new text"u8.ToArray());
            Assert.Equal("new text", File.ReadAllText(path));
            // Only the file: the temporary file it was written to is gone
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SafeWrite_ThatFails_LeavesTheFileAsItWas()
    {
        var directory = Path.Combine(Path.GetTempPath(), "blyant_safe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "chapter.md");
        try
        {
            File.WriteAllText(path, "old text");
            // A read-only file cannot be replaced (on Windows; on Linux and macOS the directory decides that)
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(path, FileAttributes.ReadOnly);
                Assert.ThrowsAny<Exception>(() => FileIo.WriteAllBytesSafe(path, "new text"u8.ToArray()));
                Assert.Equal("old text", File.ReadAllText(path));
                Assert.Single(Directory.GetFiles(directory));
            }
        }
        finally
        {
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(directory, true);
        }
    }
}
