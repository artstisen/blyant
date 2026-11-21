using System.Globalization;
using System.Text;

namespace Blyant;

/// <summary>
/// The windows of peek kept for the shell, in "peeks.txt" in the data directory: those saved with "peek save" (by a
/// name, or without one), to come back to with "peek back", and the window of the last peek when a shell typed in at a
/// terminal was left, so the next shell goes on from it. A window is a file, its first line and how many lines: never
/// the text of the file. A line for each: the name ("*" for the last peek, "" for the one saved without a name), a
/// tab, the first line, a tab, the number of lines, a tab and the path. The windows follow a file that is renamed or
/// moved
/// </summary>
internal static class PeekMarks
{
    internal const string FileName = "peeks.txt";

    // The longest name of a saved peek
    internal const int MaxNameLength = 40;

    // The name of the window of the last peek in the file
    private const string LastName = "*";

    /// <summary>
    /// A window of peek: the file, the first line and how many lines
    /// </summary>
    internal sealed record Window(string Path, int First, int Count);

    // The windows saved, in the order they were first saved
    private static readonly List<(string Name, Window Window)> Entries = new();

    // The window of the last peek of the shell left last
    private static Window? _last;

    // The file could not be read: it is not written over (it may only be locked for a moment)
    private static bool _unreadable;

    /// <summary>
    /// The file of the windows. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    /// <summary>
    /// A name a saved peek can have: a letter first, then letters, digits, "-" and "_", at most 40 (a number is a row
    /// of "peek saved")
    /// </summary>
    internal static bool IsName(string name) =>
        name.Length is > 0 and <= MaxNameLength && char.IsLetter(name[0]) &&
        name.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    /// <summary>
    /// The windows saved, in order ("" is the one saved without a name)
    /// </summary>
    internal static IReadOnlyList<(string Name, Window Window)> Saved => Entries.ToList();

    /// <summary>
    /// The window of the last peek of the shell left last (null: none)
    /// </summary>
    internal static Window? Last => _last;

    /// <summary>
    /// Read the windows from the file
    /// </summary>
    internal static void Load()
    {
        Entries.Clear();
        _last = null;
        _unreadable = false;
        try
        {
            if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _unreadable = true;
            // English: the log (the path, never what is in it)
            EventLog.Add(DialogText.LogFile + "Peeks not read: " + FilePath + ": " + e.Message);
        }
    }

    /// <summary>
    /// The windows from the text of the file (a line that cannot be read is left out)
    /// </summary>
    internal static void Parse(string text)
    {
        Entries.Clear();
        _last = null;
        foreach (var line in text.Split('\n'))
        {
            var parts = line.TrimEnd('\r').Split('\t', 4);
            if (parts.Length != 4 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var first) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count) || first < 1 ||
                count < 1 || !Path.IsPathFullyQualified(parts[3]))
            {
                continue;
            }

            var window = new Window(parts[3], first, count);
            if (parts[0] == LastName) _last = window;
            else if (parts[0].Length == 0 || IsName(parts[0])) Put(parts[0], window);
        }
    }

    // The text of the file
    private static string Serialize()
    {
        var all = Entries.Select(entry => (entry.Name, entry.Window)).ToList();
        if (_last != null) all.Add((LastName, _last));
        return string.Concat(all.Where(entry => Keepable(entry.Window.Path)).Select(entry =>
            entry.Name + "\t" + entry.Window.First.ToString(CultureInfo.InvariantCulture) + "\t" +
            entry.Window.Count.ToString(CultureInfo.InvariantCulture) + "\t" + entry.Window.Path + "\n"));
    }

    // A path that fits on a line of the file (Linux and macOS allow a tab or a line break in a name)
    private static bool Keepable(string path) => path.IndexOfAny(['\t', '\n', '\r']) < 0;

    /// <summary>
    /// Write the windows to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory != null && !Directory.Exists(directory)) FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize()));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    // A window saved by a name: in the place of one of that name (letter case alike), or at the end
    private static void Put(string name, Window window)
    {
        var at = Entries.FindIndex(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) Entries[at] = (Entries[at].Name.Length == 0 ? name : name.ToLowerInvariant(), window);
        else Entries.Add((name.ToLowerInvariant(), window));
    }

    /// <summary>
    /// Save a window by a name ("" for the one without a name): read the file first, and write it
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Set(string name, Window window)
    {
        Load();
        if (!Keepable(window.Path)) return DialogText.ListNotRead;
        Put(name, window);
        return Save();
    }

    /// <summary>
    /// The window saved by a name ("" for the one without a name), from the file as it is now; null: none
    /// </summary>
    internal static Window? Get(string name)
    {
        Load();
        return Entries.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)).Window;
    }

    /// <summary>
    /// Delete the window saved by a name: false when there is none (nothing written)
    /// </summary>
    internal static bool Remove(string name, out string result)
    {
        Load();
        result = DialogText.Success;
        if (Entries.RemoveAll(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)) == 0) return false;
        result = Save();
        return true;
    }

    /// <summary>
    /// Keep the window of the last peek of a shell left (null: none), written when it changed
    /// </summary>
    internal static void SetLast(Window? window)
    {
        Load();
        if (window != null && !Keepable(window.Path)) window = null;
        if (_last == window) return;
        _last = window;
        var result = Save();
        // English: the log (the path, never what is in it)
        if (result != DialogText.Success) EventLog.Add(DialogText.LogFile + "Peeks not saved: " + FilePath + ": " + result);
    }

    /// <summary>
    /// A file or directory was renamed or moved: the windows of it (or of a file in it) follow it. Written when one did
    /// </summary>
    internal static void Follow(string oldPath, string newPath)
    {
        Load();
        var changed = false;
        for (var i = 0; i < Entries.Count; i++)
        {
            if (FileOperations.FollowedPath(Entries[i].Window.Path, oldPath, newPath) is not { } path) continue;
            Entries[i] = (Entries[i].Name, Entries[i].Window with { Path = path });
            changed = true;
        }

        if (_last != null && FileOperations.FollowedPath(_last.Path, oldPath, newPath) is { } lastPath)
        {
            _last = _last with { Path = lastPath };
            changed = true;
        }

        if (!changed) return;
        var result = Save();
        // English: the log (the path, never what is in it)
        if (result != DialogText.Success) EventLog.Add(DialogText.LogFile + "Peeks not saved: " + FilePath + ": " + result);
    }

    /// <summary>
    /// No windows (a test)
    /// </summary>
    internal static void Clear()
    {
        Entries.Clear();
        _last = null;
        _unreadable = false;
    }
}
