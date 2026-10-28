using System.Text;

namespace Blyant;

/// <summary>
/// The splits: a file that was closed with another file shown read only beside it (":view", ":split name") is opened
/// with it again, at the place it was left at. Kept in "splits.txt" in the data directory, the newest first, one on
/// each line: the file, a tab character, the file beside it, a tab character, and the line and column in it
/// ("12:5"). A file closed without one is taken out of the list. At most MaxCount are kept; files that are renamed or
/// moved are followed. ":splits" shows them to open one again (SplitsBox). This class does not touch the console.
/// </summary>
internal static class Splits
{
    internal const string FileName = "splits.txt";
    internal const int MaxCount = 100;

    /// <summary>
    /// A file, the file read only beside it, and where in it the cursor was (from 1)
    /// </summary>
    internal sealed record Entry(string Left, string Right, int Line, int Column);

    private static readonly List<Entry> List = new();

    internal static IReadOnlyList<Entry> Items => List;

    /// <summary>
    /// The file with the list. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    // The file was there, but could not be read: it is not written over
    private static bool _unreadable;

    /// <summary>
    /// Read the list from the file
    /// </summary>
    internal static void Load()
    {
        List.Clear();
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

    internal static void Parse(string text)
    {
        List.Clear();
        foreach (var rawLine in text.Split('\n'))
        {
            var parts = rawLine.TrimEnd('\r').Split('\t');
            if (parts.Length < 2) continue;
            var left = FileOperations.ListedPath(parts[0]);
            var right = FileOperations.ListedPath(parts[1]);
            if (left.Length == 0 || right.Length == 0 || Get(left) != null) continue;
            var (line, column) = (1, 1);
            if (parts.Length > 2 && parts[2].Split(':') is [var l, var c] && int.TryParse(l, out var readLine) &&
                int.TryParse(c, out var readColumn) && readLine > 0 && readColumn > 0)
            {
                (line, column) = (readLine, readColumn);
            }

            List.Add(new Entry(left, right, line, column));
            if (List.Count == MaxCount) break;
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            text.Append(entry.Left).Append('\t').Append(entry.Right).Append('\t').Append(entry.Line).Append(':')
                .Append(entry.Column).Append('\n');
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

    /// <summary>
    /// The file shown beside a file when it was closed, or null
    /// </summary>
    internal static Entry? Get(string left) => List.Find(entry => FileOperations.SamePath(entry.Left, left));

    /// <summary>
    /// A file was closed with another beside it: first in the list (one for each file)
    /// </summary>
    /// <returns>True if the list changed</returns>
    internal static bool Set(string left, string right, int line, int column)
    {
        var entry = new Entry(Path.GetFullPath(left), Path.GetFullPath(right), Math.Max(1, line), Math.Max(1, column));
        // (a tab or a line break would break the line of the file)
        if ((entry.Left + entry.Right).IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0) return false;
        if (List.Count > 0 && List[0] == entry) return false;
        List.RemoveAll(other => FileOperations.SamePath(other.Left, left));
        List.Insert(0, entry);
        if (List.Count > MaxCount) List.RemoveRange(MaxCount, List.Count - MaxCount);
        return true;
    }

    /// <summary>
    /// A file was closed without a file beside it, or the split is taken out of the list
    /// </summary>
    /// <returns>True if it was in the list</returns>
    internal static bool Remove(string left) => List.RemoveAll(entry => FileOperations.SamePath(entry.Left, left)) > 0;

    /// <summary>
    /// A file or directory has a new path (renamed or moved): the splits of it (and of what is in it) follow it
    /// </summary>
    /// <returns>True if the list changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        for (var i = 0; i < List.Count; i++)
        {
            var left = CaretPosition.NewPath(List[i].Left, oldPath, newPath);
            var right = CaretPosition.NewPath(List[i].Right, oldPath, newPath);
            if (left == null && right == null) continue;
            List[i] = List[i] with { Left = left ?? List[i].Left, Right = right ?? List[i].Right };
            changed = true;
        }

        return changed;
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

/// <summary>
/// ":splits": the files that were closed with a file read only beside them, the newest first, in a box. Enter opens
/// the one picked (the file, with the other beside it again); D takes one out of the list (the files stay)
/// </summary>
internal static class SplitsBox
{
    /// <returns>The split picked, or null</returns>
    internal static Splits.Entry? Pick()
    {
        while (true)
        {
            Splits.Load();
            var items = Splits.Items.ToList();
            ChoiceBox.Forget();
            if (items.Count == 0)
            {
                ChoiceBox.Message(Lang.T("Splits"), Lang.T("No splits yet: close a file with another beside it (:view)"),
                    string.Empty);
                return null;
            }

            string Name(string path) =>
                Path.GetFileName(path) + (File.Exists(path) ? string.Empty : " " + DialogText.FavoriteMissing);

            var rows = items.Select(entry => Name(entry.Left) + "  |  " + Name(entry.Right)).ToList();
            var picked = PickListBox.Show(Lang.T("Splits"), rows,
                i => items[i].Left + "\n" + items[i].Right + ":" + items[i].Line, 0,
                Lang.T("j/k Select | Enter Open | D Remove | / Filter | Esc Close"), new PickListBox.Extras
                {
                    Key = (key, i) =>
                    {
                        if (key.Key != ConsoleKey.D || i < 0 || (key.Modifiers & ConsoleModifiers.Control) != 0)
                        {
                            return PickListBox.KeyUse.None;
                        }

                        if (Splits.Remove(items[i].Left) && Splits.Save() is var status &&
                            status != DialogText.Success)
                        {
                            PickListBox.ShowMessage(status);
                            return PickListBox.KeyUse.Used;
                        }

                        return PickListBox.KeyUse.Reload;
                    }
                });
            if (picked == PickListBox.Reload) continue;
            return picked >= 0 ? items[picked] : null;
        }
    }
}
