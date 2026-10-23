using System.Text;

namespace Blyant;

/// <summary>
/// The projects: a project is a directory with the files of a piece of writing in it. The projects are kept in
/// the projects directory (setting "projectsdir"; by default "Blyant-Projects" in the Documents folder of the user,
/// or in the home directory where there is none). New project on the start page makes a directory there, and Open
/// project lists the directories there; either opens the project in the file browser.
/// This class does not touch the console, so it can be tested.
/// </summary>
internal static class Projects
{
    // Not "Blyant": that is the name a folder for the program gets, and the program and its data do not belong
    // with the writing
    private const string DefaultName = "Blyant-Projects"; // English: the name of a directory

    /// <summary>
    /// The trash: a directory in the projects directory, hidden from the list of projects (its name starts with
    /// a dot). What is deleted in the file browser is moved here when the setting "deletetotrash" is on
    /// </summary>
    internal const string TrashName = ".trash"; // English: the name of a directory

    /// <summary>
    /// The trash directory (it is made when the first file is moved to it)
    /// </summary>
    internal static string TrashDirectory => Path.GetFullPath(Path.Combine(Directory, TrashName));

    /// <summary>
    /// Deleting in the file browser moves to the trash (setting "deletetotrash", on unless turned off). Off: the
    /// files are deleted at once and cannot be recovered
    /// </summary>
    internal static bool DeleteToTrash { get; set; } = true;

    /// <summary>
    /// The projects directory used when none is set: "Blyant-Projects" in the Documents folder (or the home
    /// directory)
    /// </summary>
    internal static string DefaultDirectory
    {
        get
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolderOption.DoNotVerify);
            if (documents.Length == 0)
            {
                documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile,
                    Environment.SpecialFolderOption.DoNotVerify);
            }

            return Path.Combine(documents.Length > 0 ? documents : AppContext.BaseDirectory, DefaultName);
        }
    }

    /// <summary>
    /// The directory the projects are kept in (setting "projectsdir"). It is made when the first project is
    /// </summary>
    internal static string Directory { get; set; } = DefaultDirectory;

    /// <summary>
    /// The names of the projects: the directories in the projects directory (not the hidden ones), in
    /// alphabetical order. None if the projects directory is not there (or cannot be read)
    /// </summary>
    internal static List<string> Names()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return new List<string>();
            return new DirectoryInfo(Directory).EnumerateDirectories()
                .Where(directory => !directory.Name.StartsWith('.') &&
                                    (directory.Attributes & FileAttributes.Hidden) == 0)
                .Select(directory => directory.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// The full path of a project
    /// </summary>
    internal static string PathOf(string name) => Path.GetFullPath(Path.Combine(Directory, name));

    /// <summary>
    /// What is wrong with a name for a new project (not a name of a directory, or a project or file of that name
    /// is there), or null if it can be made
    /// </summary>
    internal static string? NameProblem(string name)
    {
        name = name.Trim();
        if (Tools.FileNameProblem(name) is { } problem) return problem;
        var path = Path.Combine(Directory, name);
        if (System.IO.Directory.Exists(path) || File.Exists(path)) return DialogText.NameExists;
        // Not only in upper or lower case (on Linux both can be made, but not on Windows and macOS, where the
        // projects may be synced to)
        return Names().Contains(name, StringComparer.OrdinalIgnoreCase) ? DialogText.NameExists : null;
    }

    /// <summary>
    /// The projects directory of the setting "projectsdir": "~" is the home directory, and a path that is not a
    /// full path on this system (one written on another system: "C:\Users\..." on Linux) is not used
    /// </summary>
    /// <returns>The directory, or null to use the default one</returns>
    internal static string? DirectoryFromSetting(string setting)
    {
        setting = setting.Trim();
        if (setting == "~" || setting.StartsWith("~/", StringComparison.Ordinal) ||
            setting.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile,
                Environment.SpecialFolderOption.DoNotVerify);
            if (home.Length == 0) return null;
            setting = setting.Length <= 2 ? home : Path.Combine(home, setting[2..]);
        }

        return setting.Length > 0 && Tools.IsValidPath(setting) && Path.IsPathFullyQualified(setting)
            ? setting
            : null;
    }

    /// <summary>
    /// Make a new project: its directory in the projects directory (which is made too, if it is not there)
    /// </summary>
    /// <param name="name">The name of the project</param>
    /// <param name="path">The full path of the project made</param>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Create(string name, out string path)
    {
        name = name.Trim();
        path = string.Empty;
        if (NameProblem(name) is { } problem) return problem;
        try
        {
            path = PathOf(name);
            FileIo.CreateDirectory(path);
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
        }
    }

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
    internal static ConsoleColor? ColorOf(string name)
    {
        ConsoleColor? color = null;
        foreach (var line in InfoLines(name))
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
    internal static string SetColor(string name, ConsoleColor? color)
    {
        try
        {
            // A file that cannot be read is not written over (its other lines would be lost)
            var kept = ReadInfoLines(name).Where(line => line.Length > 0 && !line.StartsWith('#') &&
                                                         !line.StartsWith("color=", StringComparison.Ordinal)).ToList();
            var path = Path.Combine(PathOf(name), InfoFileName);
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
    private static string[] InfoLines(string name)
    {
        try
        {
            return ReadInfoLines(name);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // The lines of the file of a project (CRLF also, as it may have been written on another system)
    private static string[] ReadInfoLines(string name)
    {
        var path = Path.Combine(PathOf(name), InfoFileName);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r", string.Empty).Split('\n') : [];
    }

    /// <summary>
    /// The full path of the note of a project
    /// </summary>
    internal static string NotePath(string name) => Path.Combine(PathOf(name), NoteFileName);

    /// <summary>
    /// The start of the note of a project, for the list of projects: its words in one line (empty if it has no
    /// note, or it cannot be read)
    /// </summary>
    internal static string NoteSnippet(string name)
    {
        try
        {
            var path = NotePath(name);
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
    internal static string EnsureNote(string name)
    {
        try
        {
            var path = NotePath(name);
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
    internal static void RemoveEmptyNote(string name)
    {
        try
        {
            var path = NotePath(name);
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
    internal static string Contents(string name, int count)
    {
        try
        {
            var info = new DirectoryInfo(PathOf(name));
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
}
