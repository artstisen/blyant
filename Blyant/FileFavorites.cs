using System.Text;

namespace Blyant;

/// <summary>
/// The favorites of the file browser: files and directories marked by the user, in an order and with text
/// colors set by the user. Favorites can be grouped in folders, and folders hold subfolders (one level of them),
/// which only exist in the list (not on disk). A favorite or a folder can have a note, which is kept in the list
/// too (never next to the files, which may be in a repository).
///
/// The list is kept in the file "favorites.txt" in the data directory, with one line for each favorite:
/// the color (or nothing), a tab character and the path. A folder has its name in brackets instead of a
/// path ("[Work]", followed by a tab character and "collapsed" if it is closed), and the favorites in a
/// folder are the lines after it that start with a blank space. A folder line that starts with a blank space in a
/// folder is a subfolder, and the favorites in it start with two. A note is on the line after its favorite or
/// folder, after a "#" (the line breaks in it written as \n, a backslash as \\ and a tab as \t).
///
/// There can be more lists than this main list: each is a file of the same kind in the directory "favorites" next
/// to it, with the name of the list. One list is open at a time (setting "favoritelist"). The favorites of all the
/// lists follow a file that is renamed, moved or deleted.
/// </summary>
internal static class FileFavorites
{
    internal const string FileName = "favorites.txt";
    private const string CollapsedMark = "collapsed";
    private const string ListsDirectoryName = "favorites";
    private const string ListExtension = ".txt";
    private const int MaxListName = 20; // The name of a list fits in the top bar, after the caption

    /// <summary>
    /// What stands for the main list in ":list main" (English: as the names of commands)
    /// </summary>
    internal const string MainListWord = "main";

    internal sealed class Favorite : IFolderItem
    {
        internal string Path = string.Empty;
        public ConsoleColor? Color { get; set; }
        public bool IsFolder { get; init; }
        public string Name { get; set; } = string.Empty; // The name of a folder
        public bool Collapsed { get; set; } // A folder that is closed: the favorites in it are not shown
        public int Level { get; set; } // 0: not in a folder, 1: in a folder (a subfolder), 2: in a subfolder

        // A favorite in a folder (or a subfolder in a folder)
        internal bool InFolder
        {
            get => Level > 0;
            init => Level = value ? 1 : 0;
        }

        public string Note { get; set; } = string.Empty; // The note of the favorite or folder (empty: none)
        internal int Line; // Where the cursor was when the file was closed in the editor (from 1; 0: not known)
        internal int Column;
        internal string Split = string.Empty; // The other pane of a split, as written ("|40:1"; empty: not split)
    }

    // The list, with its folders (moving, removing and putting back are done by it)
    private static FolderList<Favorite> Folders = new();
    private static List<Favorite> List => Folders.Items;

    /// <summary>
    /// The favorites and folders in the order set by the user. The favorites of a folder follow it
    /// </summary>
    internal static IReadOnlyList<Favorite> Items => List;

    /// <summary>
    /// The file with the list. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath => ListPath(CurrentList);

    /// <summary>
    /// The name of the list that is open (setting "favoritelist"; empty: the main list). The favorites of another list
    /// are read when it is set, so what is saved never goes to a list it was not read from
    /// </summary>
    internal static string CurrentList
    {
        get => _currentList;
        set
        {
            if (_currentList == value) return;
            _currentList = value;
            Load();
        }
    }

    private static string _currentList = string.Empty;

    private static string MainFilePath => FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    private static string ListsDirectory =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(MainFilePath)) ?? string.Empty, ListsDirectoryName);

    /// <summary>
    /// The file of a list (empty: the main list)
    /// </summary>
    internal static string ListPath(string name) =>
        name.Length == 0 ? MainFilePath : Path.Combine(ListsDirectory, name + ListExtension);

    /// <summary>
    /// The names of the lists besides the main list, in alphabetical order
    /// </summary>
    internal static List<string> ListNames()
    {
        try
        {
            if (!Directory.Exists(ListsDirectory)) return new List<string>();
            return Directory.GetFiles(ListsDirectory, "*" + ListExtension).Select(Path.GetFileNameWithoutExtension)
                .OfType<string>().Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// The words that can follow ":list": the main list and the names of the others
    /// </summary>
    internal static IEnumerable<string> ListWords() => ListNames().Prepend(MainListWord);

    /// <summary>
    /// The list with a name (upper and lower case alike): its name as it is written, empty for the main list, and
    /// null if there is none
    /// </summary>
    internal static string? FindList(string name)
    {
        name = name.Trim();
        if (name.Equals(MainListWord, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return ListNames().FirstOrDefault(list => list.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A name a list can have: not too long, usable as a file name on all systems, and not the word for the main list
    /// </summary>
    internal static bool IsValidListName(string name)
    {
        name = name.Trim();
        return name.Length is > 0 and <= MaxListName && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0 &&
               !name.StartsWith('.') && !name.EndsWith('.') &&
               !name.Equals(MainListWord, StringComparison.OrdinalIgnoreCase);
    }

    // Why a list cannot have a name (null: it can)
    private static string? ListNameProblem(string name)
    {
        if (name.Length > MaxListName) return Lang.F("Name too long (at most {0} characters)", MaxListName);
        return IsValidListName(name) ? null : Lang.T("That name cannot be used for a list");
    }

    // Do something with a list in the place of the one that is open, which stays as it is. A list that cannot be read
    // is left alone
    private static void WithList(string name, Action action)
    {
        var open = Folders;
        Folders = new FolderList<Favorite>();
        try
        {
            if (File.Exists(ListPath(name))) Parse(File.ReadAllText(ListPath(name), Encoding.UTF8));
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EventLog.Add(DialogText.LogFile + "Favorites: " + ListPath(name) + " - FAILED: " + e.Message);
        }
        finally
        {
            Folders = open;
        }
    }

    // A change to the favorites in all the lists but the one that is open
    private static void InOtherLists(Func<bool> change)
    {
        foreach (var name in ListNames().Prepend(string.Empty).Where(name => name != CurrentList))
        {
            WithList(name, () =>
            {
                if (change()) FileIo.WriteAllBytesSafe(ListPath(name), new UTF8Encoding(false).GetBytes(Serialize()));
            });
        }
    }

    /// <summary>
    /// The files and directories of all the lists (the one that is open first)
    /// </summary>
    internal static List<string> PathsInAllLists()
    {
        var paths = List.Where(item => !item.IsFolder).Select(item => item.Path).ToList();
        foreach (var name in ListNames().Prepend(string.Empty).Where(name => name != CurrentList))
        {
            WithList(name, () => paths.AddRange(List.Where(item => !item.IsFolder).Select(item => item.Path)));
        }

        return paths;
    }

    /// <summary>
    /// What is in a list, a line for each favorite or folder, to show it in the box of lists
    /// </summary>
    internal static string ListPreview(string name)
    {
        var lines = new List<string>();
        WithList(name, () => lines.AddRange(List.Select(item =>
            new string(' ', item.Level * 2) + (item.IsFolder ? "[" + item.Name + "]" : Path.GetFileName(item.Path)))));
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Open a list
    /// </summary>
    /// <param name="name">Its name (empty: the main list)</param>
    internal static void UseList(string name)
    {
        _currentList = name;
        Load();
    }

    /// <summary>
    /// Make a new, empty list
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal static string NewList(string name)
    {
        name = name.Trim();
        if (ListNameProblem(name) is { } problem) return problem;
        if (FindList(name) != null) return Lang.T("There is a list of that name");
        try
        {
            FileIo.CreateDirectory(ListsDirectory);
            FileIo.CreateNewFile(ListPath(name));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Give a list another name (its file is renamed). The open list stays open
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal static string RenameList(string name, string newName)
    {
        newName = newName.Trim();
        if (ListNameProblem(newName) is { } problem) return problem;
        // (another way of writing the name is not another list)
        if (FindList(newName) is { } taken && !taken.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return Lang.T("There is a list of that name");
        }

        try
        {
            if (name != newName) FileIo.MoveFile(ListPath(name), ListPath(newName), false);
            if (_currentList == name) _currentList = newName;
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Delete a list: to the trash, unless that is turned off. The files in it stay where they are. If it is open, the
    /// main list is opened
    /// </summary>
    /// <param name="name">Its name (not the main list)</param>
    /// <param name="trashedPath">Where it is in the trash (empty: it is not there)</param>
    /// <returns>DialogText.Success or a message</returns>
    internal static string DeleteList(string name, out string trashedPath)
    {
        trashedPath = string.Empty;
        var result = DialogText.Success;
        try
        {
            if (FileOperations.DeleteToTrash)
            {
                result = FileOperations.MoveToTrash(ListPath(name), FileOperations.TrashDirectory, out trashedPath);
            }
            else
            {
                FileIo.DeleteFile(ListPath(name));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }

        if (result == DialogText.Success && CurrentList == name) UseList(string.Empty);
        return result;
    }

    /// <summary>
    /// Read the list from the file
    /// </summary>
    internal static void Load()
    {
        List.Clear();
        Folders.ForgetRemoved();
        _unreadable = false;
        // A list that is gone (deleted outside Blyant): the main list
        if (_currentList.Length > 0 && !File.Exists(FilePath)) _currentList = string.Empty;
        try
        {
            if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }
        catch
        {
            // A list that cannot be read is empty, and the file is not written over until it is read (it may only be
            // locked for a moment, as by a virus scanner)
            _unreadable = true;
        }
    }

    // The file was there, but could not be read
    private static bool _unreadable;

    internal static void Parse(string text)
    {
        List.Clear();
        Folders.ForgetRemoved();
        var levels = new FolderList<Favorite>.LevelReader();
        Favorite? last = null; // The favorite or folder a note is of
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var spaces = FolderList<Favorite>.LevelReader.Spaces(line);
            line = line.TrimStart(' ');
            if (Notes.IsNote(line, out var note))
            {
                if (last != null) last.Note = note;
                continue;
            }

            last = null;
            var tab = line.IndexOf('\t');
            var written = tab < 0 ? line : line[(tab + 1)..];
            var rest = written.Trim();
            if (rest.Length == 0) continue;
            ConsoleColor? color = null;
            if (tab > 0 && Enum.TryParse<ConsoleColor>(line[..tab].Trim(), true, out var parsed) &&
                Enum.IsDefined(parsed))
            {
                color = parsed;
            }

            var close = rest.IndexOf(']');
            if (rest[0] == '[' && close > 1)
            {
                // A folder, or a subfolder
                var level = levels.LevelOf(spaces, true);
                levels.Add(level, true);
                last = new Favorite
                {
                    IsFolder = true, Name = rest[1..close].Trim(), Color = color, Level = level,
                    Collapsed = rest[(close + 1)..].Contains(CollapsedMark, StringComparison.OrdinalIgnoreCase)
                };
                List.Add(last);
                continue;
            }

            // Where the cursor was, before the path
            var (caretLine, caretColumn, split) = CaretPosition.Take(ref written);
            rest = FileOperations.ListedPath(written);
            if (rest.Length == 0 || Contains(rest)) continue;
            // The favorites of a folder follow it directly
            var at = levels.LevelOf(spaces, false);
            levels.Add(at, false);
            last = new Favorite
            {
                Path = rest, Color = color, Level = at, Line = caretLine, Column = caretColumn, Split = split
            };
            List.Add(last);
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var favorite in List)
        {
            text.Append(' ', favorite.Level);
            text.Append(favorite.Color?.ToString() ?? string.Empty).Append('\t');
            if (favorite.IsFolder)
            {
                text.Append('[').Append(favorite.Name).Append(']');
                if (favorite.Collapsed) text.Append('\t').Append(CollapsedMark);
            }
            else
            {
                text.Append(CaretPosition.Write(favorite.Line, favorite.Column, favorite.Split)).Append(favorite.Path);
            }

            text.Append('\n');
            if (favorite.Note.Length > 0) text.Append(Notes.Line(favorite.Level, favorite.Note));
        }

        return text.ToString();
    }

    /// <summary>
    /// Give a favorite or folder a note (blank: no note). Blank lines at its end are left out
    /// </summary>
    internal static void SetNote(Favorite favorite, string note)
    {
        favorite.Note = Notes.Clean(note);
    }

    /// <summary>
    /// Write the list to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory != null) FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize()));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    private static int IndexOf(string path)
    {
        return List.FindIndex(favorite => !favorite.IsFolder && FileOperations.SamePath(favorite.Path, path));
    }

    internal static bool Contains(string path) => IndexOf(path) >= 0;

    /// <summary>
    /// The place of a favorite or folder in the list (-1: not in it)
    /// </summary>
    internal static int IndexOf(Favorite favorite) => List.IndexOf(favorite);

    /// <summary>
    /// The note of a favorite file or directory (empty: none, or not a favorite)
    /// </summary>
    internal static string NoteOf(string path)
    {
        var index = IndexOf(path);
        return index >= 0 ? List[index].Note : string.Empty;
    }

    /// <summary>
    /// The text color of a favorite file or directory (null: none, or not a favorite)
    /// </summary>
    internal static ConsoleColor? ColorOf(string path)
    {
        var index = IndexOf(path);
        return index >= 0 ? List[index].Color : null;
    }

    /// <summary>
    /// Where the cursor was in a favorite file when it was closed (line and column from 1), or null if not known
    /// </summary>
    internal static (int Line, int Column, string Split)? CaretOf(string path)
    {
        var index = IndexOf(path);
        return index >= 0 && List[index].Line > 0 ? (List[index].Line, List[index].Column, List[index].Split) : null;
    }

    /// <summary>
    /// Keep where the cursor was in a favorite file when it was closed
    /// </summary>
    /// <returns>True if the file is a favorite</returns>
    internal static bool SetCaret(string path, int line, int column, string split = "")
    {
        var index = IndexOf(path);
        if (index < 0) return false;
        List[index].Line = Math.Max(1, line);
        List[index].Column = Math.Max(1, column);
        List[index].Split = split;
        return true;
    }

    /// <summary>
    /// Add a file or directory to the favorites, or remove it if it is one
    /// </summary>
    /// <returns>True if it is a favorite now</returns>
    internal static bool Toggle(string path)
    {
        var index = IndexOf(path);
        if (index >= 0)
        {
            List.RemoveAt(index);
            return false;
        }

        // A path with a line break would break the lines of the file
        if (path.IndexOfAny(new[] { '\r', '\n' }) >= 0) return false;

        List.Add(new Favorite { Path = Path.GetFullPath(path) });
        return true;
    }

    /// <summary>
    /// Add a file or directory to the favorites below an item of the list: first in it when the item is a folder, else
    /// after it, in its folder (-1: at the end of the list)
    /// </summary>
    /// <returns>The place of the favorite in the list, or -1 if it is a favorite already (or its path has a line
    /// break)</returns>
    internal static int AddBelow(string path, int at)
    {
        if (Contains(path) || path.IndexOfAny(new[] { '\r', '\n' }) >= 0) return -1;
        return Folders.AddBelow(new Favorite { Path = Path.GetFullPath(path) }, at);
    }

    /// <summary>
    /// Copy the bookmarks of the Jump menu into the favorites: the bookmarks of files and directories, with their
    /// folders, colors and notes (see FolderList.Import). A bookmark of a page of Blyant is left out (a favorite is a
    /// file or a directory), and the name given to a bookmark is not copied (a favorite shows the name of its file).
    /// The bookmarks stay as they are
    /// </summary>
    /// <param name="bookmarks">The bookmarks, in their order (a folder followed by those in it)</param>
    /// <param name="apply">False to only count what would be done</param>
    internal static ImportResult ImportBookmarks(IReadOnlyList<Bookmarks.Entry> bookmarks, bool apply) =>
        Folders.Import(bookmarks, apply,
            bookmark => bookmark.Target.Kind is JumpKind.File or JumpKind.Directory &&
                        bookmark.Target.Target.IndexOfAny(new[] { '\r', '\n' }) < 0
                ? new Favorite { Path = Path.GetFullPath(bookmark.Target.Target) }
                : null,
            name => new Favorite { IsFolder = true, Name = name },
            (one, other) => FileOperations.SamePath(one.Path, other.Path));

    /// <summary>
    /// The files of a directory, to add to the favorites as a folder (":import folder"): its own text files, and (when
    /// asked for) those of the folders below it, at any depth. TooMany: there are more than the most that are read
    /// (the files are not all read then). Stopped: the reading was stopped. Error: the directory itself could not be
    /// read. Unreadable: the folders below it that could not be read, and are left out. Other: the files that are not
    /// text (as told by their names), and are left out
    /// </summary>
    internal sealed record FolderFiles(string Name, List<string> Files, bool TooMany = false, bool Stopped = false,
        string? Error = null, int Unreadable = 0, int Other = 0)
    {
        internal int Count => Files.Count;
    }

    /// <summary>
    /// The most files a directory can add to the favorites
    /// </summary>
    internal const int FolderImportMax = 2000;

    // The files and folders of a directory that are read: not hidden ones, and not those that cannot be read. (A
    // folder marked System, as a customized folder of Windows, is read: it is no folder of the system)
    private static readonly EnumerationOptions Shown = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden
    };

    // A name that is not hidden: ".git", ".obsidian" and ".DS_Store" are hidden also where they are not marked so
    private static bool IsShown(string path) => !System.IO.Path.GetFileName(path).StartsWith('.');

    // A folder that is a link to another (a symbolic link or a junction), which could lead round in a circle. A
    // folder of OneDrive or Dropbox is marked as a reparse point too, but is no link: it is read
    private static bool IsLink(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).LinkTarget != null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    // The text files of a directory by name (no file is read: their names tell), at most a number of them (more are
    // not read: the list is too long then). The other files are counted. A link to a file that is not there is left
    // out
    private static List<string> FilesIn(string directory, int most, ref int other)
    {
        var files = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*", Shown))
        {
            if (!IsShown(file)) continue;
            if (!SyntaxHighlighter.IsTextFile(file))
            {
                other++;
                continue;
            }

            if (!IsThere(file)) continue;
            files.Add(file);
            if (files.Count >= most) break;
        }

        return files.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // A file, or a link to a file that is there. (File.Exists is true for a link to a file that is not there on
    // Linux and macOS, where it looks at the link itself, and false on Windows: the target is looked at)
    private static bool IsThere(string file)
    {
        try
        {
            var info = new FileInfo(file);
            return info.LinkTarget == null || info.ResolveLinkTarget(true) is { Exists: true };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static List<string> FoldersIn(string directory) =>
        Directory.EnumerateDirectories(directory, "*", Shown).Where(folder => IsShown(folder) && !IsLink(folder))
            .Order(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// The name of a folder for a directory: its name, without the brackets that mark a folder in the file and the
    /// characters that would break its line ("Folder" when nothing is left)
    /// </summary>
    internal static string FolderNameOf(string directory)
    {
        var name = DirectoryHotlist.NameOf(directory).Replace('[', '(').Replace(']', ')')
            .Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        return name.Length > 0 ? name : "Folder"; // English: a name for the file, not shown as a text of Blyant
    }

    /// <summary>
    /// Read the text files of a directory to add to the favorites as a folder: its own files by name, then (with
    /// subfolders) those of each folder below it in turn, at any depth, so the files of a folder stay together. Only
    /// the names are read. Files that are not text, hidden files and folders (also those whose names start with
    /// "."), links to other folders, links to files that are not there, and what cannot be read are left out.
    /// Reading stops when there are more than max
    /// </summary>
    /// <param name="directory">The directory</param>
    /// <param name="subfolders">True to read the files of the folders below it too</param>
    /// <param name="max">The most files</param>
    /// <param name="progress">Called with each folder before it is read, and the number of files found: false stops
    /// the reading</param>
    internal static FolderFiles ReadFolder(string directory, bool subfolders, int max = FolderImportMax,
        Func<string, int, bool>? progress = null)
    {
        var name = FolderNameOf(directory);
        var files = new List<string>();
        var unreadable = 0;
        var other = 0;
        // The folders still to read, the next on top (a loop, not a call for each folder: no limit to the depth)
        var folders = new Stack<string>();
        folders.Push(directory);
        while (folders.Count > 0)
        {
            var folder = folders.Pop();
            if (progress != null && !progress(folder, files.Count)) return new FolderFiles(name, files, Stopped: true);
            try
            {
                files.AddRange(FilesIn(folder, max + 1 - files.Count, ref other));
                if (files.Count > max)
                {
                    return new FolderFiles(name, files, TooMany: true, Unreadable: unreadable, Other: other);
                }

                if (!subfolders) break;
                var below = FoldersIn(folder);
                for (var i = below.Count - 1; i >= 0; i--) folders.Push(below[i]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // The directory itself: nothing is added. A folder below it (gone since, or not to be read): left out
                if (folder == directory) return new FolderFiles(name, new List<string>(), Error: e.Message);
                unreadable++;
            }
        }

        return new FolderFiles(name, files, Unreadable: unreadable, Other: other);
    }

    /// <summary>
    /// Add the text files of a directory to the favorites, all in one folder named after the directory (at the end of the
    /// list, or added to a folder of that name that is there: see FolderList.Import), in the order they were read. A
    /// file that is a favorite already stays where it is, so the same directory added again adds its new files
    /// </summary>
    /// <param name="folder">The files (ReadFolder)</param>
    /// <param name="apply">False to only count what would be done</param>
    internal static ImportResult ImportFolder(FolderFiles folder, bool apply)
    {
        // The favorites there are, by their full paths: each file is looked up once (not compared with each)
        var comparer = FileOperations.NamesIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var there = new HashSet<string>(List.Where(item => !item.IsFolder).Select(item => PlaceKey(item.Path)), comparer);
        var fresh = folder.Files.Where(path => !there.Contains(PlaceKey(path))).ToList();
        var items = new List<Favorite> { new() { IsFolder = true, Name = folder.Name } };
        items.AddRange(fresh.Select(path => new Favorite { Path = path, Level = 1 }));
        var result = Folders.Import(items, apply,
            item => item.Path.IndexOfAny(new[] { '\r', '\n' }) < 0
                ? new Favorite { Path = System.IO.Path.GetFullPath(item.Path) }
                : null,
            name => new Favorite { IsFolder = true, Name = name },
            // (those that are favorites already are left out above)
            (_, _) => false);
        return result with { Skipped = result.Skipped + folder.Files.Count - fresh.Count };
    }

    // A path as it is compared: full, without a separator at the end
    private static string PlaceKey(string path)
    {
        try
        {
            return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
        {
            return path;
        }
    }

    /// <summary>
    /// The place of a folder (not a subfolder) by its name (-1: none)
    /// </summary>
    internal static int FolderIndex(string name) => List.FindIndex(item =>
        item.IsFolder && item.Level == 0 && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Remove a favorite, or a folder with the favorites in it
    /// </summary>
    internal static void RemoveAt(int index) => Folders.RemoveAt(index);

    /// <summary>
    /// Remove several favorites (folders are not removed this way)
    /// </summary>
    /// <returns>The number removed</returns>
    internal static int RemoveMany(ICollection<Favorite> items) => Folders.RemoveMany(items);

    /// <summary>
    /// Put back what was removed last, where it was (a favorite that is a favorite again is not)
    /// </summary>
    /// <returns>The place in the list of the first that was put back, or -1 if there is nothing to put back</returns>
    internal static int UndoRemove() => Folders.UndoRemove(item => !Contains(item.Path));

    /// <summary>
    /// Move favorites into a folder (last in it, in the order they have in the list), or out of their folders
    /// </summary>
    /// <param name="items">The favorites</param>
    /// <param name="folder">The folder, or null to place them last in the list, outside the folders</param>
    /// <returns>The place in the list of the first of them, or -1 if nothing was moved</returns>
    internal static int MoveToFolder(ICollection<Favorite> items, Favorite? folder) =>
        Folders.MoveToFolder(items, folder);

    /// <summary>
    /// Move a folder into another (as a subfolder), or a subfolder out of its folder (null). See FolderList.MoveFolderTo
    /// </summary>
    internal static int MoveFolderTo(Favorite folder, Favorite? into) => Folders.MoveFolderTo(folder, into);

    /// <summary>
    /// Move favorites (or folders, with the favorites in them) to the top or the end: a favorite in a folder to
    /// the top or the end of its folder, the others to the top or the end of the list. Their order is kept
    /// </summary>
    /// <returns>The number moved</returns>
    internal static int MoveToEnd(ICollection<Favorite> items, bool top) => Folders.MoveToEnd(items, top);

    internal static void SetColor(int index, ConsoleColor? color)
    {
        if (index >= 0 && index < List.Count) List[index].Color = color;
    }

    #region Folders

    /// <summary>
    /// A name that can be used for a folder (the brackets mark a folder in the file)
    /// </summary>
    internal static bool IsValidFolderName(string name)
    {
        name = name.Trim();
        return name.Length > 0 && name.IndexOfAny(new[] { '[', ']', '\t', '\n', '\r' }) < 0;
    }

    /// <summary>
    /// Add a folder in front of an item of the list (in front of its folder, if it is in one)
    /// </summary>
    /// <returns>The place of the folder in the list, or -1 if the name cannot be used</returns>
    internal static int AddFolder(string name, int before)
    {
        if (!IsValidFolderName(name)) return -1;
        return Folders.AddFolder(new Favorite { IsFolder = true, Name = name.Trim() }, before);
    }

    /// <summary>
    /// Add a subfolder to the folder of an item: first in it when the item is the folder, else in front of the item
    /// (or of the subfolder it is in)
    /// </summary>
    /// <returns>The place of the subfolder in the list, -1 if the name cannot be used, or -2 if the item is not a
    /// folder or in one</returns>
    internal static int AddSubfolder(string name, int at)
    {
        if (!IsValidFolderName(name)) return -1;
        var index = Folders.AddSubfolder(new Favorite { IsFolder = true, Name = name.Trim() }, at);
        return index < 0 ? -2 : index;
    }

    /// <summary>
    /// The folder or subfolder an item is directly in (-1: none)
    /// </summary>
    internal static int Parent(int index) => Folders.Parent(index);

    /// <summary>
    /// Open the folders an item is in, so that it is shown
    /// </summary>
    internal static void Reveal(int index) => Folders.Reveal(index);

    /// <summary>
    /// An item is shown: none of the folders it is in is closed
    /// </summary>
    internal static bool IsShown(int index) => Folders.IsShown(index);

    /// <summary>
    /// The number of subfolders in a folder
    /// </summary>
    internal static int SubfolderCount(int index) => Folders.SubfolderCount(index);

    /// <summary>
    /// The favorites and subfolders in a folder (or subfolder), in their order
    /// </summary>
    internal static List<Favorite> InFolder(int index)
    {
        if (index < 0 || index >= List.Count || !List[index].IsFolder) return new List<Favorite>();
        return List.GetRange(index + 1, Folders.FolderEnd(index) - index);
    }

    internal static bool RenameFolder(int index, string name)
    {
        if (index < 0 || index >= List.Count || !List[index].IsFolder || !IsValidFolderName(name)) return false;
        List[index].Name = name.Trim();
        return true;
    }

    /// <summary>
    /// Open or close all the folders
    /// </summary>
    /// <returns>The number of folders in the list</returns>
    internal static int SetAllCollapsed(bool collapsed) => Folders.SetAllCollapsed(collapsed);

    /// <summary>
    /// The folder a favorite is in (the item itself if it is not in a folder)
    /// </summary>
    internal static int FolderOf(int index) => Folders.FolderOf(index);

    /// <summary>
    /// The number of favorites in a folder, with those in its subfolders
    /// </summary>
    internal static int FolderCount(int index) => Folders.FolderCount(index);

    #endregion

    /// <summary>
    /// Move a favorite or a folder one place up or down in the list.
    /// A favorite moved onto a folder goes into it, and one moved past the first or last favorite of its
    /// folder leaves the folder. A folder is closed and moves with its favorites, past the favorite or
    /// folder next to it.
    /// </summary>
    /// <returns>False if it is at the end of the list</returns>
    internal static bool Move(int index, bool up) => Folders.Move(index, up);

    /// <summary>
    /// A file or directory was renamed or moved: the favorites follow (also the ones inside a directory), in all the
    /// lists. The other lists are saved here
    /// </summary>
    /// <returns>True if the list that is open was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath) => PathsChanged(new[] { (oldPath, newPath) });

    /// <summary>
    /// Files or directories were moved: as PathChanged, with each list read and written once
    /// </summary>
    /// <returns>True if the list that is open was changed</returns>
    internal static bool PathsChanged(IReadOnlyCollection<(string Old, string New)> moved)
    {
        if (moved.Count == 0) return false;
        // (not stopped by the first that changes something)
        bool Change() => moved.Count(item => ChangePath(item.Old, item.New)) > 0;
        InOtherLists(Change);
        return Change();
    }

    private static bool ChangePath(string oldPath, string newPath)
    {
        var changed = false;
        foreach (var favorite in List)
        {
            if (favorite.IsFolder) continue;
            if (FileOperations.SamePath(favorite.Path, oldPath))
            {
                favorite.Path = Path.GetFullPath(newPath);
                changed = true;
            }
            else if (FileOperations.IsInside(favorite.Path, oldPath))
            {
                var rest = Path.GetRelativePath(Path.GetFullPath(oldPath), Path.GetFullPath(favorite.Path));
                favorite.Path = Path.GetFullPath(Path.Combine(newPath, rest));
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Files or directories were deleted: they are removed from the favorites (with what was inside a directory), in
    /// all the lists, each read and written once. The other lists are saved here
    /// </summary>
    /// <returns>True if the list that is open was changed</returns>
    internal static bool PathsDeleted(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0) return false;
        bool Remove() => paths.Count(RemovePath) > 0;
        InOtherLists(Remove);
        return Remove();
    }

    private static bool RemovePath(string path)
    {
        return List.RemoveAll(favorite => !favorite.IsFolder &&
                                          (FileOperations.SamePath(favorite.Path, path) ||
                                           FileOperations.IsInside(favorite.Path, path))) > 0;
    }
}
