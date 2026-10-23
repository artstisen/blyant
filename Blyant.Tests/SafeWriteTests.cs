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

    // The ways of writing that empty a file before the new content is in it
    private static readonly Regex Unsafe = new(
        @"\bFile\.(WriteAllText|WriteAllBytes|WriteAllLines|Create|CreateText|OpenWrite)\s*\(|" +
        @"FileMode\.(Create|Truncate|OpenOrCreate)\b|new\s+StreamWriter\s*\(");

    [Fact]
    public void FilesAreNotEmptiedBeforeTheyAreWritten()
    {
        var root = SourceDirectory();
        var found = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // FileIo.CanWriteIn makes a file that is deleted when it is closed, to test the directory
                if (line.Contains("FileOptions.DeleteOnClose")) continue;
                if (Unsafe.IsMatch(line)) found.Add(Path.GetRelativePath(root, path) + ":" + (i + 1) + ": " + line.Trim());
            }
        }

        Assert.True(found.Count == 0, "Use FileIo.WriteAllBytesSafe (or FileMode.CreateNew for a new file):\n" +
                                      string.Join("\n", found));
    }

    // The ways of changing files and directories that are only used in FileIo (which logs them), in
    // FileOperations (the operations of the file browser, which logs them as a whole) and in EventLog (the log file)
    private static readonly Regex Unlogged = new(
        @"\b(File|Directory)\.(Delete|Move|Copy|CreateDirectory|CreateSymbolicLink|AppendAllText|AppendAllLines|" +
        @"SetAttributes)\s*\(|FileMode\.CreateNew|\.CopyTo\s*\(|\.MoveTo\s*\(");

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
