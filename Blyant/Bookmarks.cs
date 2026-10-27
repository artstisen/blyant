using System.Text;

namespace Blyant;

/// <summary>
/// The bookmarks of the Jump menu: places kept to jump to (a file, a directory or a page of Blyant),
/// added with B on any list of the Jump menu, or with ":bookmark" in the editor and the file browser. They are in
/// the order they were added, which can be changed on the Bookmarks tab. A bookmark can be given a name and a text
/// color of its own, and bookmarks can be grouped in folders (as the favorites of the file browser). The list has
/// no limit; ":import favorites" copies the favorites of the file browser into it (ImportFavorites), and
/// ":import bookmarks" in the favorites copies it there (FileFavorites.ImportBookmarks).
///
/// The bookmarks are numbered 1, 2, 3 ... in the order of the list (folders are not counted, and the bookmarks in a
/// closed folder are): ":book 3" jumps to the third.
///
/// The list is kept in the file "bookmarks.txt" in the data directory, one bookmark on each line: the kind ("file",
/// "dir" or "page", as in jump.txt), a tab character, the place, and after other tab characters the name it was
/// given and its color (if any), and the line and column of a bookmark made in the editor ("120:5"). A folder has its name in brackets ("[Work]"), its color and "collapsed" if it is
/// closed; the bookmarks in a folder are the lines after it that start with a blank space. A folder line that starts
/// with a blank space in a folder is a subfolder, and the bookmarks in it start with two. A note of a bookmark or a
/// folder is on the line after it, after a "#" (Notes; an older Blyant leaves these lines out). (The lines of data
/// files, kept by Marbles, are left out.)
///
/// This class does not touch the console: the list is shown by JumpMenu.
/// </summary>
internal static class Bookmarks
{
    internal const string FileName = "bookmarks.txt";
    private const string CollapsedMark = "collapsed";

    /// <summary>
    /// A bookmark (a place, the name it was given and its color), or a folder (its name and color)
    /// </summary>
    internal sealed class Entry : IFolderItem
    {
        // The place a folder does not have
        private static readonly JumpTarget NoPlace = new(JumpKind.Page, string.Empty);

        internal Entry(JumpTarget target, string name = "")
        {
            Target = target;
            Name = name;
        }

        /// <summary>
        /// A folder with a name
        /// </summary>
        internal static Entry Folder(string name) => new(NoPlace, name) { IsFolder = true };

        internal JumpTarget Target { get; set; }

        /// <summary>
        /// The name given to a bookmark (empty: the name of the place is shown), or the name of a folder
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The text color of the bookmark or folder (null: none of its own)
        /// </summary>
        public ConsoleColor? Color { get; set; }

        public bool IsFolder { get; init; }
        public int Level { get; set; }
        public bool Collapsed { get; set; }

        /// <summary>
        /// A bookmark in a folder (or a subfolder in a folder)
        /// </summary>
        internal bool InFolder
        {
            get => Level > 0;
            init => Level = value ? 1 : 0;
        }

        /// <summary>
        /// The note of the bookmark or folder (empty: none)
        /// </summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// The name shown: the one given, or the name of the place, with the line of a bookmark made in the editor
        /// ("chapter.md:120"; a folder: its name)
        /// </summary>
        internal string Shown => IsFolder || Name.Length > 0 ? Name
            : Target.At is { } at ? Target.Name + ":" + at.Line
            : Target.Name;
    }

    // The list, with its folders (moving, removing and putting back are done by it)
    private static readonly FolderList<Entry> Folders = new();
    private static List<Entry> List => Folders.Items;

    /// <summary>
    /// The bookmarks and folders, in the order of the list. The bookmarks of a folder follow it
    /// </summary>
    internal static IReadOnlyList<Entry> Items => List;

    /// <summary>
    /// The bookmarks without the folders, in the order of the list: bookmark 1 is the first (":book 1")
    /// </summary>
    internal static List<Entry> Places => List.Where(entry => !entry.IsFolder).ToList();

    /// <summary>
    /// The number of a bookmark (from 1; 0 if it is not a bookmark of the list)
    /// </summary>
    internal static int NumberOf(Entry entry) => Places.IndexOf(entry) + 1;

    /// <summary>
    /// The file with the list. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    /// <summary>
    /// Read the list from the file
    /// </summary>
    internal static void Load()
    {
        List.Clear();
        Folders.ForgetRemoved();
        _unreadable = false;
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

    private static ConsoleColor? ParseColor(string text) =>
        Enum.TryParse<ConsoleColor>(text.Trim(), true, out var color) && Enum.IsDefined(color) ? color : null;

    // The line and column of a bookmark made in the editor ("120:5"), or null
    private static (int Line, int Column)? ParseAt(string text)
    {
        var parts = text.Trim().Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out var line) && int.TryParse(parts[1], out var column) &&
               line > 0 && column > 0
            ? (line, column)
            : null;
    }

    internal static void Parse(string text)
    {
        List.Clear();
        // What was removed before is not put back into a list read again
        Folders.ForgetRemoved();
        var levels = new FolderList<Entry>.LevelReader();
        Entry? last = null; // The bookmark or folder a note is of
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var spaces = FolderList<Entry>.LevelReader.Spaces(line);
            line = line.TrimStart(' ');
            if (Notes.IsNote(line, out var note))
            {
                if (last != null) last.Note = note;
                continue;
            }

            last = null;
            var parts = line.Split('\t');
            var close = parts[0].IndexOf(']');
            if (parts[0].StartsWith('[') && close > 1)
            {
                // A folder (or a subfolder): its name, color, and if it is closed
                var name = parts[0][1..close].Trim();
                if (!IsValidFolderName(name))
                {
                    // Left out, and what follows it is not put in the folder above it
                    levels.Add(0, false);
                    continue;
                }

                var folder = Entry.Folder(name);
                folder.Level = levels.LevelOf(spaces, true);
                levels.Add(folder.Level, true);
                folder.Color = parts.Length > 1 ? ParseColor(parts[1]) : null;
                folder.Collapsed = parts.Skip(2)
                    .Any(part => part.Trim().Equals(CollapsedMark, StringComparison.OrdinalIgnoreCase));
                List.Add(folder);
                last = folder;
                continue;
            }

            if (parts.Length < 2 || JumpHistory.ParseKind(parts[0]) is not { } kind) continue;
            var place = kind is JumpKind.File or JumpKind.Directory ? FileOperations.ListedPath(parts[1]) : parts[1].Trim();
            if (place.Length == 0) continue;
            var target = new JumpTarget(kind, place) { At = kind == JumpKind.File && parts.Length > 4 ? ParseAt(parts[4]) : null };
            if (Contains(target)) continue;
            // The bookmarks of a folder follow it directly
            var level = levels.LevelOf(spaces, false);
            levels.Add(level, false);
            last = new Entry(target, parts.Length > 2 ? parts[2].Trim() : string.Empty)
            {
                Color = parts.Length > 3 ? ParseColor(parts[3]) : null,
                Level = level
            };
            List.Add(last);
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            text.Append(' ', entry.Level);
            var color = entry.Color?.ToString() ?? string.Empty;
            if (entry.IsFolder)
            {
                text.Append('[').Append(entry.Name).Append(']').Append('\t').Append(color);
                if (entry.Collapsed) text.Append('\t').Append(CollapsedMark);
            }
            else
            {
                text.Append(JumpHistory.KindName(entry.Target.Kind)).Append('\t').Append(entry.Target.Target);
                var at = entry.Target.At is { } place ? place.Line + ":" + place.Column : string.Empty;
                if (entry.Name.Length > 0 || color.Length > 0 || at.Length > 0) text.Append('\t').Append(entry.Name);
                if (color.Length > 0 || at.Length > 0) text.Append('\t').Append(color);
                if (at.Length > 0) text.Append('\t').Append(at);
            }

            text.Append('\n');
            if (entry.Note.Length > 0) text.Append(Notes.Line(entry.Level, entry.Note));
        }

        return text.ToString();
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

    internal static bool Contains(JumpTarget target) =>
        List.Exists(entry => !entry.IsFolder && SameBookmark(entry.Target, target));

    // The same bookmark: the same place, and the same line in a file (a bookmark of a file, and bookmarks at other
    // lines of it, are others)
    private static bool SameBookmark(JumpTarget one, JumpTarget other) =>
        one.SameAs(other) && one.At?.Line == other.At?.Line;

    // The bookmark of a place, or null
    private static Entry? Find(JumpTarget target) =>
        List.Find(entry => !entry.IsFolder && SameBookmark(entry.Target, target));

    /// <summary>
    /// What adding a place did
    /// </summary>
    internal enum AddResult
    {
        Added,
        AlreadyThere,
        NotKept // A tab or a line break in its path
    }

    private static readonly char[] LineBreaking = { '\t', '\r', '\n' };

    /// <summary>
    /// Copy the favorites of the file browser into the bookmarks: a file as a bookmark of a file, a directory of a
    /// directory, with their folders, colors and notes (see FolderList.Import). The favorites stay as they are; one
    /// whose file is gone is copied too
    /// </summary>
    /// <param name="favorites">The favorites, in their order (a folder followed by those in it)</param>
    /// <param name="apply">False to only count what would be done</param>
    internal static ImportResult ImportFavorites(IReadOnlyList<FileFavorites.Favorite> favorites, bool apply) =>
        Folders.Import(favorites, apply, favorite =>
        {
            var path = Path.GetFullPath(favorite.Path);
            if (path.IndexOfAny(LineBreaking) >= 0) return null;
            // A favorite that is gone keeps its kind as far as it can be told: a name without an extension is
            // taken for a directory only when it is there
            return new Entry(new JumpTarget(Directory.Exists(path) ? JumpKind.Directory : JumpKind.File, path));
        }, Entry.Folder, (one, other) => SameBookmark(one.Target, other.Target));

    /// <summary>
    /// Add a place at the end of the list, outside the folders (a place that is there already stays where it is)
    /// </summary>
    internal static AddResult Add(JumpTarget target)
    {
        if (target.Kind is JumpKind.File or JumpKind.Directory)
        {
            target = target with { Target = Path.GetFullPath(target.Target) };
        }

        if (Contains(target)) return AddResult.AlreadyThere;
        // (a tab or a line break in it would break the line of the file)
        if (target.Target.IndexOfAny(LineBreaking) >= 0 || target.Name.IndexOfAny(LineBreaking) >= 0)
        {
            return AddResult.NotKept;
        }

        List.Add(new Entry(target));
        return AddResult.Added;
    }

    /// <summary>
    /// Add a place below an item of the list: first in it when the item is a folder, else after it, in its folder (-1:
    /// at the end of the list)
    /// </summary>
    /// <returns>The place of the bookmark in the list, or -1 if it is a bookmark already (or cannot be kept)</returns>
    internal static int AddBelow(JumpTarget target, int at)
    {
        if (target.Kind is JumpKind.File or JumpKind.Directory)
        {
            target = target with { Target = Path.GetFullPath(target.Target) };
        }

        if (Contains(target) || target.Target.IndexOfAny(LineBreaking) >= 0) return -1;
        return Folders.AddBelow(new Entry(target), at);
    }

    /// <summary>
    /// Give a bookmark or folder a note (blank: no note)
    /// </summary>
    internal static void SetNote(Entry entry, string note)
    {
        entry.Note = Notes.Clean(note);
    }

    /// <summary>
    /// The bookmarks and subfolders in a folder (or subfolder), in their order
    /// </summary>
    internal static List<Entry> InFolder(int index)
    {
        if (index < 0 || index >= List.Count || !List[index].IsFolder) return new List<Entry>();
        return List.GetRange(index + 1, Folders.FolderEnd(index) - index);
    }

    /// <summary>
    /// Remove a bookmark, or a folder with the bookmarks in it (UndoRemove puts it back)
    /// </summary>
    internal static void RemoveAt(int index) => Folders.RemoveAt(index);

    /// <summary>
    /// Remove several bookmarks (folders are not removed this way)
    /// </summary>
    /// <returns>The number removed</returns>
    internal static int RemoveMany(ICollection<Entry> items) => Folders.RemoveMany(items);

    /// <summary>
    /// Put back what was removed last, where it was (a place that is a bookmark again is not)
    /// </summary>
    /// <returns>The place in the list of the first that was put back, or -1 if there is nothing to put back</returns>
    internal static int UndoRemove() => Folders.UndoRemove(entry => !Contains(entry.Target));

    /// <summary>
    /// Move a bookmark or a folder one place up or down (into and out of folders, see FolderList.Move)
    /// </summary>
    /// <returns>False if it is at the end of the list</returns>
    internal static bool Move(int index, bool up) => Folders.Move(index, up);

    /// <summary>
    /// Move bookmarks (or folders) to the top or the end of the list (a bookmark in a folder: of its folder)
    /// </summary>
    internal static int MoveToEnd(ICollection<Entry> items, bool top) => Folders.MoveToEnd(items, top);

    /// <summary>
    /// Move bookmarks into a folder (last in it), or out of their folders (null: last in the list)
    /// </summary>
    /// <returns>The place in the list of the first of them, or -1 if nothing was moved</returns>
    internal static int MoveToFolder(ICollection<Entry> items, Entry? folder) => Folders.MoveToFolder(items, folder);

    /// <summary>
    /// Give a bookmark the name it is shown with (empty: the name of the place), or a folder a new name
    /// </summary>
    /// <returns>False if a folder cannot have the name</returns>
    internal static bool Rename(int index, string name)
    {
        if (index < 0 || index >= List.Count) return false;
        // A tab or a line break would end the name in the file
        name = name.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        var entry = List[index];
        if (entry.IsFolder)
        {
            if (!IsValidFolderName(name)) return false;
            entry.Name = name;
            return true;
        }

        entry.Name = name == entry.Target.Name ? string.Empty : name;
        return true;
    }

    internal static void SetColor(int index, ConsoleColor? color)
    {
        if (index >= 0 && index < List.Count) List[index].Color = color;
    }

    #region Folders

    /// <summary>
    /// A name that can be used for a folder (the brackets mark a folder in the file)
    /// </summary>
    internal static bool IsValidFolderName(string name) => FileFavorites.IsValidFolderName(name);

    /// <summary>
    /// Add a folder in front of an item of the list (in front of its folder, if it is in one)
    /// </summary>
    /// <returns>The place of the folder in the list, or -1 if the name cannot be used</returns>
    internal static int AddFolder(string name, int before)
    {
        if (!IsValidFolderName(name)) return -1;
        return Folders.AddFolder(Entry.Folder(name.Trim()), before);
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
        var index = Folders.AddSubfolder(Entry.Folder(name.Trim()), at);
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
    /// Open or close all the folders
    /// </summary>
    /// <returns>The number of folders</returns>
    internal static int SetAllCollapsed(bool collapsed) => Folders.SetAllCollapsed(collapsed);

    /// <summary>
    /// The number of bookmarks in a folder, with those in its subfolders
    /// </summary>
    internal static int FolderCount(int index) => Folders.FolderCount(index);

    #endregion

    /// <summary>
    /// Empty the list (the folders too)
    /// </summary>
    internal static void Clear()
    {
        List.Clear();
        Folders.ForgetRemoved();
    }

    /// <summary>
    /// A file or directory has a new path (renamed or moved): the bookmarks of it (and of what is in it) follow it
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        foreach (var entry in List)
        {
            if (entry.IsFolder || entry.Target.Followed(oldPath, newPath) is not { } target) continue;
            entry.Target = target;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Add a place to the bookmarks (as they are in the file) and save them
    /// </summary>
    /// <param name="target">The place</param>
    /// <param name="reload">False in the Jump menu, which has read the list already: what can be undone there (a
    /// bookmark removed) is kept</param>
    /// <returns>The message that tells what happened</returns>
    internal static string AddWithMessage(JumpTarget target, bool reload = true)
    {
        if (reload) Load();
        var result = Add(target);
        if (result == AddResult.NotKept) return Lang.T("A path with a tab or a line break is not kept");
        // With its number, for ":book 14", and its name as the Jump menu shows it
        var entry = Find(target with
        {
            Target = target.Kind == JumpKind.Page ? target.Target : Path.GetFullPath(target.Target)
        });
        var number = entry == null ? 0 : NumberOf(entry);
        var name = entry?.Shown ?? target.Name;
        if (result == AddResult.AlreadyThere) return Lang.F("Already bookmarked ({0}): {1}", number, name);
        var status = Save();
        return status == DialogText.Success ? Lang.F("Bookmarked ({0}): {1}", number, name) : status;
    }

    /// <summary>
    /// Load the list, follow a file or directory with a new path, and save it if it changed
    /// </summary>
    internal static void Follow(string oldPath, string newPath)
    {
        Load();
        if (PathChanged(oldPath, newPath)) Save();
    }
}
