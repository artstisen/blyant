namespace Blyant;

/// <summary>
/// The projects: a project is a directory with the files of a piece of writing in it. The projects are kept in
/// the projects directory (setting "projectsdir"; by default "Blyant" in the Documents folder of the user, or in
/// the home directory where there is none). New project on the start page makes a directory there, and Open
/// project lists the directories there; either opens the project in the file browser.
/// This class does not touch the console, so it can be tested.
/// </summary>
internal static class Projects
{
    private const string DefaultName = "Blyant"; // English: the name of a directory

    /// <summary>
    /// The projects directory used when none is set: "Blyant" in the Documents folder (or the home directory)
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
        return System.IO.Directory.Exists(path) || File.Exists(path) ? DialogText.NameExists : null;
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
            System.IO.Directory.CreateDirectory(path);
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return e.Message;
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
