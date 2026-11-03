namespace Blyant;

/// <summary>
/// Lists of something kept as files with names, besides a main one (the favorites, the boards of the corkboard): the
/// main list is its file in the data directory, and the others are files in a directory next to it, with the names
/// of the lists. Names, finding a list by its name, and making, renaming and deleting the files of the lists. It does
/// not touch the console, so it can be tested
/// </summary>
internal sealed class NamedLists
{
    private const string Extension = ".txt";

    /// <summary>
    /// The longest name of a list: it fits in the top bar, after the caption
    /// </summary>
    internal const int MaxName = 20;

    /// <summary>
    /// What stands for the main list in a command (":list main"; English: as the names of commands)
    /// </summary>
    internal const string MainWord = "main";

    private readonly Func<string> _mainPath;
    private readonly string _directoryName;

    /// <param name="mainPath">The file of the main list</param>
    /// <param name="directoryName">The directory of the other lists, next to the file of the main list</param>
    internal NamedLists(Func<string> mainPath, string directoryName)
    {
        _mainPath = mainPath;
        _directoryName = directoryName;
    }

    private string Directory =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_mainPath())) ?? string.Empty, _directoryName);

    /// <summary>
    /// The file of a list (empty: the main list)
    /// </summary>
    internal string PathOf(string name) => name.Length == 0 ? _mainPath() : Path.Combine(Directory, name + Extension);

    /// <summary>
    /// The names of the lists besides the main list, in alphabetical order
    /// </summary>
    internal List<string> Names()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return new List<string>();
            return System.IO.Directory.GetFiles(Directory, "*" + Extension).Select(Path.GetFileNameWithoutExtension)
                .OfType<string>().Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// The words that can follow a command that opens a list: the main list and the names of the others
    /// </summary>
    internal IEnumerable<string> Words() => Names().Prepend(MainWord);

    /// <summary>
    /// The list with a name (upper and lower case alike): its name as it is written, empty for the main list, and
    /// null if there is none
    /// </summary>
    internal string? Find(string name)
    {
        name = name.Trim();
        if (name.Equals(MainWord, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return Names().FirstOrDefault(list => list.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A name a list can have: not too long, usable as a file name on all systems, and not the word for the main list
    /// </summary>
    internal static bool IsValidName(string name)
    {
        name = name.Trim();
        return name.Length is > 0 and <= MaxName && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0 &&
               !name.StartsWith('.') && !name.EndsWith('.') &&
               !name.Equals(MainWord, StringComparison.OrdinalIgnoreCase);
    }

    // Why a list cannot have a name (null: it can)
    private static string? NameProblem(string name)
    {
        if (name.Length > MaxName) return Lang.F("Name too long (at most {0} characters)", MaxName);
        return IsValidName(name) ? null : Lang.T("That name cannot be used for a list");
    }

    /// <summary>
    /// Make a new, empty list
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal string New(string name)
    {
        name = name.Trim();
        if (NameProblem(name) is { } problem) return problem;
        if (Find(name) != null) return Lang.T("There is a list of that name");
        try
        {
            FileIo.CreateDirectory(Directory);
            FileIo.CreateNewFile(PathOf(name));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Give a list another name (its file is renamed)
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal string Rename(string name, string newName)
    {
        newName = newName.Trim();
        if (NameProblem(newName) is { } problem) return problem;
        // (another way of writing the name is not another list)
        if (Find(newName) is { } taken && !taken.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return Lang.T("There is a list of that name");
        }

        try
        {
            if (name != newName) FileIo.MoveFile(PathOf(name), PathOf(newName), false);
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Delete a list: to the trash, unless that is turned off
    /// </summary>
    /// <param name="name">Its name (not the main list)</param>
    /// <param name="trashedPath">Where it is in the trash (empty: it is not there)</param>
    /// <returns>DialogText.Success or a message</returns>
    internal string Delete(string name, out string trashedPath)
    {
        trashedPath = string.Empty;
        try
        {
            if (FileOperations.DeleteToTrash)
            {
                return FileOperations.MoveToTrash(PathOf(name), FileOperations.TrashDirectory, out trashedPath);
            }

            FileIo.DeleteFile(PathOf(name));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }
}
