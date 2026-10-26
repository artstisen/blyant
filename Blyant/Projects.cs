using System.Text;

namespace Blyant;

/// <summary>
/// The projects: a project is a directory with the files of a piece of writing in it, anywhere. The projects are kept
/// in a list, projects.txt in the data directory: a new project is made where the user picks, and a directory that is
/// there already can be added. A project is known by its full path.
/// This class does not touch the console, so it can be tested.
/// </summary>
internal static class Projects
{
    /// <summary>
    /// A project: the full path of its directory
    /// </summary>
    internal sealed record Project(string Path)
    {
        internal string Name => System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path));

        /// <summary>
        /// The directory is not there (moved, renamed or deleted elsewhere)
        /// </summary>
        internal bool Missing => !System.IO.Directory.Exists(Path);
    }

    private static readonly StringComparer NameOrder = StringComparer.CurrentCultureIgnoreCase;

    /// <summary>
    /// The projects, in alphabetical order (also those that are missing)
    /// </summary>
    internal static List<Project> All()
    {
        return AddedPaths().Select(path => new Project(path)).OrderBy(project => project.Name, NameOrder)
            .ThenBy(project => project.Path, NameOrder).ToList();
    }

    /// <summary>
    /// The names to show for projects: the name of the directory, and where two have the same name, the name of
    /// the directory it is in after it ("Novel (Writing)")
    /// </summary>
    internal static List<string> DisplayNames(IReadOnlyList<Project> projects)
    {
        return projects.Select(project =>
        {
            if (projects.Count(other => string.Equals(other.Name, project.Name, StringComparison.OrdinalIgnoreCase)) < 2)
            {
                return project.Name;
            }

            var parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(
                System.IO.Path.TrimEndingDirectorySeparator(project.Path)) ?? string.Empty);
            return parent.Length > 0 ? project.Name + " (" + parent + ")" : project.Name;
        }).ToList();
    }

    /// <summary>
    /// What is wrong with a name for a new project in a directory (not a name of a directory, or one of that name
    /// is there), or null if it can be made
    /// </summary>
    internal static string? NameProblem(string parent, string name)
    {
        name = name.Trim();
        if (Tools.FileNameProblem(name) is { } problem) return problem;
        var path = Path.Combine(parent, name);
        if (System.IO.Directory.Exists(path) || File.Exists(path)) return DialogText.NameExists;
        // Not only in upper or lower case (on Linux both can be made, but not on Windows and macOS, where the
        // projects may be synced to)
        try
        {
            if (System.IO.Directory.Exists(parent) && new DirectoryInfo(parent).EnumerateFileSystemInfos()
                    .Any(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return DialogText.NameExists;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // It is found when the directory is made
        }

        return null;
    }

    /// <summary>
    /// Make a new project: its directory in a directory (made too, if it is not there), added to the projects
    /// </summary>
    /// <param name="parent">The directory to make it in</param>
    /// <param name="name">The name of the project</param>
    /// <param name="path">The full path of the project made</param>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Create(string parent, string name, out string path)
    {
        name = name.Trim();
        path = string.Empty;
        if (NameProblem(parent, name) is { } problem) return problem;
        try
        {
            path = Path.GetFullPath(Path.Combine(parent, name));
            FileIo.CreateDirectory(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException
                                      or ArgumentException)
        {
            return e.Message;
        }

        return Add(path);
    }

    #region The list of the projects

    /// <summary>
    /// The list of the projects: a path on each line (in the data directory)
    /// </summary>
    internal const string ListFileName = "projects.txt"; // English: the name of a file

    /// <summary>
    /// The list file, for tests
    /// </summary>
    internal static string? ListPathOverride { get; set; }

    internal static string ListPath => ListPathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), ListFileName);

    // True when the list was there but could not be read the last time: then it is not written (that would leave
    // only the projects changed in it)
    private static bool _unreadable;

    /// <summary>
    /// The paths of the projects (as they are in the list; nothing if it cannot be read)
    /// </summary>
    internal static List<string> AddedPaths()
    {
        _unreadable = false;
        try
        {
            if (!File.Exists(ListPath)) return new List<string>();
            return File.ReadAllText(ListPath, Encoding.UTF8).Replace("\r", string.Empty).Split('\n')
                .Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
                .Select(FileOperations.ListedPath)
                .Where(path => Path.IsPathFullyQualified(path))
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _unreadable = true;
            return new List<string>();
        }
    }

    private static string SaveAdded(IEnumerable<string> paths)
    {
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            // English: read by people in any language
            var text = "# Blyant: the projects (a folder on each line)\n" +
                       string.Concat(paths.Select(path => path + "\n"));
            var directory = Path.GetDirectoryName(ListPath);
            if (directory != null) FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(ListPath, new UTF8Encoding(false).GetBytes(text));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Why a directory cannot be added to the projects, or null if it can: it is not there, or it is a project
    /// already
    /// </summary>
    internal static string? AddProblem(string path)
    {
        if (!System.IO.Directory.Exists(path)) return DialogText.DirectoryNotFound;
        return AddedPaths().Any(added => FileOperations.SamePath(added, path)) ? DialogText.ProjectAlready : null;
    }

    /// <summary>
    /// Add a directory to the projects
    /// </summary>
    /// <returns>DialogText.Success or why it was not added</returns>
    internal static string Add(string path)
    {
        if (AddProblem(path) is { } problem) return problem;
        var paths = AddedPaths();
        paths.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        return SaveAdded(paths);
    }

    /// <summary>
    /// Take a project off the list (the directory itself stays as it is)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Remove(string path)
    {
        var paths = AddedPaths();
        var kept = paths.Where(added => !FileOperations.SamePath(added, path)).ToList();
        return kept.Count == paths.Count ? DialogText.Success : SaveAdded(kept);
    }

    /// <summary>
    /// A directory has a new path (renamed or moved in the file browser): the projects follow it, also those
    /// inside it
    /// </summary>
    internal static void PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        var paths = AddedPaths().Select(path =>
        {
            if (FileOperations.SamePath(path, oldPath))
            {
                changed = true;
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(newPath));
            }

            if (!FileOperations.IsInside(path, oldPath)) return path;
            changed = true;
            return Path.Combine(newPath, Path.GetRelativePath(oldPath, path));
        }).ToList();
        if (changed) SaveAdded(paths);
    }

    #endregion

    #region The files of a project

    /// <summary>
    /// The file in a project with its color (the line "color=DarkCyan"), so it goes with the folder when it is
    /// moved, copied or synced. It is made when a color is set, and removed when the color is
    /// </summary>
    internal const string InfoFileName = ".blyant"; // English: the name of a file

    /// <summary>
    /// The note of a project: a Markdown file in it, written in the editor (N in the list of projects)
    /// </summary>
    internal const string NoteFileName = ".blyant-note.md"; // English: the name of a file

    // As much of a note as is read for the list of projects
    private const int SnippetLength = 2000;

    /// <summary>
    /// The color of a project (null if it has none, or its file cannot be read)
    /// </summary>
    internal static ConsoleColor? ColorOf(string project)
    {
        ConsoleColor? color = null;
        foreach (var line in InfoLines(project))
        {
            if (line.StartsWith("color=", StringComparison.Ordinal) &&
                Enum.TryParse<ConsoleColor>(line[6..].Trim(), true, out var parsed) && Enum.IsDefined(parsed))
            {
                color = parsed;
            }
        }

        return color;
    }

    /// <summary>
    /// Set the color of a project (null: no color). Other lines of the file (of a later version) are kept
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string SetColor(string project, ConsoleColor? color)
    {
        try
        {
            // A file that cannot be read is not written over (its other lines would be lost)
            var kept = ReadInfoLines(project).Where(line => line.Length > 0 && !line.StartsWith('#') &&
                                                            !line.StartsWith("color=", StringComparison.Ordinal)).ToList();
            var path = Path.Combine(project, InfoFileName);
            if (color == null && kept.Count == 0)
            {
                FileIo.DeleteFile(path);
                return DialogText.Success;
            }

            var lines = new List<string> { "# Blyant: the settings of this project" }; // English
            if (color != null) lines.Add("color=" + color);
            lines.AddRange(kept);
            // LF on all systems, so the file is the same wherever it is synced to. Written safely, and a file that
            // is hidden (on Windows) stays so
            FileIo.WriteAllBytesSafe(path, new UTF8Encoding(false).GetBytes(string.Join("\n", lines) + "\n"));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
        }
    }

    // The lines of the file of a project (none if it cannot be read)
    private static string[] InfoLines(string project)
    {
        try
        {
            return ReadInfoLines(project);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // The lines of the file of a project (CRLF also, as it may have been written on another system)
    private static string[] ReadInfoLines(string project)
    {
        var path = Path.Combine(project, InfoFileName);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r", string.Empty).Split('\n') : [];
    }

    /// <summary>
    /// The full path of the note of a project
    /// </summary>
    internal static string NotePath(string project) => Path.Combine(project, NoteFileName);

    /// <summary>
    /// The start of the note of a project, for the list of projects: its words in one line (empty if it has no
    /// note, or it cannot be read)
    /// </summary>
    internal static string NoteSnippet(string project)
    {
        try
        {
            var path = NotePath(project);
            if (!File.Exists(path)) return string.Empty;
            using var reader = new StreamReader(path);
            var buffer = new char[SnippetLength];
            var count = reader.ReadBlock(buffer, 0, buffer.Length);
            // Not half a character at the end (a surrogate pair cut by the length)
            if (count > 0 && char.IsHighSurrogate(buffer[count - 1])) count--;
            var text = new string(buffer, 0, count);
            // Without the marks of Markdown headings and quotes ("# Plan" is "Plan")
            var lines = text.Replace("\r", string.Empty).Split('\n').Select(line => line.TrimStart().TrimStart('#', '>'));
            return string.Join(" ", string.Join(" ", lines).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Make the note of a project if it has none, to write it in the editor
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string EnsureNote(string project)
    {
        try
        {
            var path = NotePath(project);
            if (!File.Exists(path)) FileIo.WriteAllBytesSafe(path, []);
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Remove the note of a project when nothing is written in it (after the editor is closed)
    /// </summary>
    internal static void RemoveEmptyNote(string project)
    {
        try
        {
            var path = NotePath(project);
            if (File.Exists(path) && new FileInfo(path).Length < SnippetLength &&
                File.ReadAllText(path).Trim().Length == 0)
            {
                FileIo.DeleteFile(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // It stays
        }
    }

    /// <summary>
    /// What a project holds, for the list of projects: the names of its files and directories (directories with
    /// "/" after them), the directories first, at most a number of them
    /// </summary>
    internal static string Contents(string project, int count)
    {
        try
        {
            var info = new DirectoryInfo(project);
            var directories = info.EnumerateDirectories().Where(directory => !directory.Name.StartsWith('.'))
                .Select(directory => directory.Name + "/").OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase);
            var files = info.EnumerateFiles().Where(file => !file.Name.StartsWith('.'))
                .Select(file => file.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase);
            var all = directories.Concat(files).ToList();
            if (all.Count == 0) return Lang.T("(empty)");
            var shown = all.Take(count).ToList();
            if (all.Count > count) shown[^1] = Lang.F("... and {0} more", all.Count - count + 1);
            return string.Join("\n", shown);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    #endregion
}
