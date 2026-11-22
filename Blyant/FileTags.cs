using System.Text;

namespace Blyant;

/// <summary>
/// The tags of files, for the shell ("tag add notes.md draft", "tag draft"): a file can have several tags, and a tag
/// several files. Kept in "tags.txt" in the data directory, a line for each file: the path, a tab, and the tags
/// separated by blanks, in the order they were given (the one given last at the end). A tag is letters, digits, "-"
/// and "_", kept in small letters. The tags follow a file that is renamed or moved, go with a copy, and leave a file
/// that is deleted
/// </summary>
internal static class FileTags
{
    internal const string FileName = "tags.txt";

    // The longest name of a tag
    internal const int MaxNameLength = 40;

    // The files and their tags, in the order they were first tagged
    private static readonly List<(string Path, List<string> Tags)> Entries = new();

    // The file could not be read: it is not written over (it may only be locked for a moment)
    private static bool _unreadable;

    /// <summary>
    /// The file with the tags. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    /// <summary>
    /// A name a tag can have: letters, digits, "-" and "_", at most 40 of them, starting with a letter or a digit (a
    /// word that starts with "-" is an option: "tag -l")
    /// </summary>
    internal static bool IsName(string name) =>
        name.Length is > 0 and <= MaxNameLength && char.IsLetterOrDigit(name[0]) &&
        name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    /// <summary>
    /// A file that can have tags: a path without a tab or a line break, which would break the line of tags.txt (on Linux
    /// and macOS a name can have them)
    /// </summary>
    internal static bool CanHaveTags(string path) => path.IndexOfAny(['\t', '\n', '\r']) < 0;

    /// <summary>
    /// Read the tags from the file
    /// </summary>
    internal static void Load()
    {
        Entries.Clear();
        _unreadable = false;
        try
        {
            if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _unreadable = true;
        }
    }

    /// <summary>
    /// The tags from the text of the file (a line that is not a path and tags is left out). The path as it is: on Linux
    /// and macOS a name can end in a blank. A file found by its path in a dictionary, so a long list is read quickly
    /// </summary>
    internal static void Parse(string text)
    {
        Entries.Clear();
        var index = new Dictionary<string, int>(FileOperations.NameComparer);
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            var path = line[..tab];
            if (!Path.IsPathFullyQualified(path)) continue;
            foreach (var word in line[(tab + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IsName(word)) continue;
                if (!index.TryGetValue(path, out var at))
                {
                    at = Entries.Count;
                    index[path] = at;
                    Entries.Add((path, new List<string>()));
                }

                var tag = word.ToLowerInvariant();
                Entries[at].Tags.Remove(tag);
                Entries[at].Tags.Add(tag);
            }
        }
    }

    /// <summary>
    /// The text of the file
    /// </summary>
    internal static string Serialize() =>
        // (a path with a tab or a line break: not written, as the line would be broken)
        string.Concat(Entries.Where(entry => entry.Tags.Count > 0 && CanHaveTags(entry.Path))
            .Select(entry => entry.Path + "\t" + string.Join(' ', entry.Tags) + "\n"));

    /// <summary>
    /// Write the tags to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory is not null) FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize()));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// No tags (a new Blyant, or a test)
    /// </summary>
    internal static void Clear()
    {
        Entries.Clear();
        _unreadable = false;
    }

    private static int IndexOf(string path) =>
        Entries.FindIndex(entry => FileOperations.NameComparer.Equals(entry.Path, path));

    /// <summary>
    /// Give a file a tag (a tag it has already is moved to the end: it is the one given last). False when the file had
    /// it as the last already
    /// </summary>
    internal static bool Add(string path, string tag)
    {
        tag = tag.ToLowerInvariant();
        path = Path.GetFullPath(path);
        var index = IndexOf(path);
        if (index < 0)
        {
            Entries.Add((path, new List<string> { tag }));
            return true;
        }

        var tags = Entries[index].Tags;
        if (tags.Count > 0 && tags[^1] == tag) return false;
        tags.Remove(tag);
        tags.Add(tag);
        return true;
    }

    /// <summary>
    /// Take a tag off a file (null: all its tags). False when it did not have it
    /// </summary>
    internal static bool Remove(string path, string? tag)
    {
        var index = IndexOf(Path.GetFullPath(path));
        if (index < 0) return false;
        var tags = Entries[index].Tags;
        var removed = tag is null ? tags.Count > 0 : tags.Remove(tag.ToLowerInvariant());
        if (tag is null || tags.Count == 0) Entries.RemoveAt(index);
        return removed;
    }

    /// <summary>
    /// The tags of a file, the one given last at the end (none: an empty list)
    /// </summary>
    internal static IReadOnlyList<string> TagsOf(string path)
    {
        var index = IndexOf(Path.GetFullPath(path));
        return index < 0 ? Array.Empty<string>() : Entries[index].Tags.ToList();
    }

    /// <summary>
    /// The files with a tag, in the order they were first tagged
    /// </summary>
    internal static List<string> FilesWith(string tag)
    {
        tag = tag.ToLowerInvariant();
        return Entries.Where(entry => entry.Tags.Contains(tag)).Select(entry => entry.Path).ToList();
    }

    /// <summary>
    /// The files that have tags, in the order they were first tagged
    /// </summary>
    internal static List<(string Path, IReadOnlyList<string> Tags)> Tagged() =>
        Entries.Where(entry => entry.Tags.Count > 0).Select(entry => (entry.Path, (IReadOnlyList<string>)entry.Tags.ToList()))
            .ToList();

    /// <summary>
    /// The tags there are, by name, and how many files have each
    /// </summary>
    internal static List<(string Tag, int Count)> All() =>
        Entries.SelectMany(entry => entry.Tags).GroupBy(tag => tag)
            .Select(group => (group.Key, group.Count())).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Give the files of a tag another name (a file with both keeps one). How many files it was given to
    /// </summary>
    internal static int Rename(string tag, string name)
    {
        tag = tag.ToLowerInvariant();
        name = name.ToLowerInvariant();
        // (the same name: nothing to change; the files keep the tag)
        if (tag == name) return FilesWith(tag).Count;
        var count = 0;
        foreach (var (_, tags) in Entries)
        {
            var at = tags.IndexOf(tag);
            if (at < 0) continue;
            count++;
            if (tags.Contains(name)) tags.RemoveAt(at);
            else tags[at] = name;
        }

        return count;
    }

    /// <summary>
    /// Take a tag off all files (null: all tags). How many files had it
    /// </summary>
    internal static int ClearTag(string? tag)
    {
        var count = tag is null ? Entries.Count(entry => entry.Tags.Count > 0) : FilesWith(tag).Count;
        if (tag is null)
        {
            Entries.Clear();
            return count;
        }

        foreach (var (_, tags) in Entries) tags.Remove(tag.ToLowerInvariant());
        Entries.RemoveAll(entry => entry.Tags.Count == 0);
        return count;
    }

    /// <summary>
    /// A file or directory has a new path: the tags of the file, or of the files in it, follow it
    /// </summary>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        for (var i = 0; i < Entries.Count; i++)
        {
            if (FileOperations.FollowedPath(Entries[i].Path, oldPath, newPath) is not { } path) continue;
            Entries[i] = (path, Entries[i].Tags);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// A file or directory was copied: the copy, or the files in it, get the tags of the files copied
    /// </summary>
    internal static bool Copied(string fromPath, string toPath)
    {
        var copies = Entries.Select(entry => (Copy: FileOperations.FollowedPath(entry.Path, fromPath, toPath), entry.Tags))
            .Where(copy => copy.Copy is not null).ToList();
        foreach (var (copy, tags) in copies)
        {
            foreach (var tag in tags) Add(copy!, tag);
        }

        return copies.Count > 0;
    }

    /// <summary>
    /// Files or directories were deleted: the files, and the files in them, have no tags any more
    /// </summary>
    internal static bool PathsDeleted(IEnumerable<string> paths)
    {
        var gone = paths.ToList();
        return Entries.RemoveAll(entry =>
            gone.Any(path => FileOperations.SamePath(entry.Path, path) || FileOperations.IsInside(entry.Path, path))) > 0;
    }

    /// <summary>
    /// Read the tags, let them follow a file or directory with a new path, and save them if that changed them
    /// </summary>
    internal static void Follow(string oldPath, string newPath)
    {
        Load();
        if (PathChanged(oldPath, newPath)) Saved();
    }

    /// <summary>
    /// Read the tags, give a copy the tags of what was copied, and save them if that changed them
    /// </summary>
    internal static void FollowCopy(string fromPath, string toPath)
    {
        Load();
        if (Copied(fromPath, toPath)) Saved();
    }

    /// <summary>
    /// Read the tags, take them off what was deleted, and save them if that changed them
    /// </summary>
    internal static void FollowDelete(IEnumerable<string> paths)
    {
        Load();
        if (PathsDeleted(paths)) Saved();
    }

    // The tags saved after a file operation (which tells nothing of the tags): when they cannot be (tags.txt could not
    // be read, or written), the log says so, as the tags then do not follow the file
    private static void Saved()
    {
        var result = Save();
        if (result != DialogText.Success) EventLog.Add(DialogText.LogFile + "Tags not saved: " + FilePath + ": " + result);
    }
}
