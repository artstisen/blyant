namespace Blyant;

/// <summary>
/// The entries of a list (favorites, bookmarks, hotlist, history) whose file or directory is not there any more:
/// ":missing" lists them in a box, and ":prune" takes them out of the list. An entry on a drive that is not
/// connected (a USB drive, a network share) is listed, but not pruned: it is only missing for now
/// </summary>
internal static class MissingPlaces
{
    // The folders drives are mounted in on Linux and macOS, and how many folders down the drive is: /mnt/usb,
    // /Volumes/usb, /media/usb or /media/user/usb, /run/media/user/usb
    private static readonly (string Folder, int Depth)[] MountFolders =
    {
        ("/media/", 2), ("/run/media/", 2), ("/mnt/", 1), ("/Volumes/", 1)
    };

    /// <summary>
    /// True when there is no file or directory at the path
    /// </summary>
    internal static bool IsMissing(string path) => !File.Exists(path) && !Directory.Exists(path);

    /// <summary>
    /// True when the drive of the path is not there: a drive letter or network share that is not connected, or a
    /// drive mounted in /media, /run/media, /mnt or /Volumes that is not mounted
    /// </summary>
    /// <param name="path">The full path</param>
    /// <param name="directoryExists">Tells if a directory is there (for testing; null: the file system)</param>
    internal static bool OnAbsentDrive(string path, Func<string, bool>? directoryExists = null)
    {
        directoryExists ??= Directory.Exists;
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) && root is not ("/" or "\\") && !directoryExists(root)) return true;
        foreach (var (folder, depth) in MountFolders)
        {
            if (!path.StartsWith(folder, StringComparison.Ordinal)) continue;
            var parts = path[folder.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            var place = folder.TrimEnd('/');
            for (var i = 0; i < Math.Min(depth, parts.Length); i++)
            {
                place += "/" + parts[i];
                if (!directoryExists(place)) return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// The missing entries that are pruned (their drive is there), and those kept (on a drive that is not there)
    /// </summary>
    internal static (List<T> Prune, List<T> Kept) Sort<T>(IEnumerable<T> missing, Func<T, string> path,
        Func<string, bool>? directoryExists = null)
    {
        var prune = new List<T>();
        var kept = new List<T>();
        foreach (var entry in missing)
        {
            (OnAbsentDrive(path(entry), directoryExists) ? kept : prune).Add(entry);
        }

        return (prune, kept);
    }

    /// <summary>
    /// ":missing": the missing entries in a box, to pick one to show in the list
    /// </summary>
    /// <param name="missing">The name and full path of each missing entry, in the order of the list</param>
    /// <returns>The number of the entry picked, or -1</returns>
    internal static int Pick(IReadOnlyList<(string Name, string Path)> missing)
    {
        var absent = missing.Select(entry => OnAbsentDrive(entry.Path)).ToList();
        var rows = missing.Select(entry => entry.Name).ToList();
        return PickListBox.Show(Lang.F("Missing ({0})", missing.Count), rows,
            i => missing[i].Path + "\n" + (absent[i]
                ? Lang.T("Its drive is not connected: :prune keeps it")
                : Lang.T(":prune removes it from the list")), 0,
            Lang.T("j/k Select | Enter Show in list | / Filter | Esc Close"));
    }

    /// <summary>
    /// ":prune": ask before the missing entries are taken out of a list
    /// </summary>
    /// <param name="caption">The title of the box (the name of the list)</param>
    /// <param name="count">How many are removed</param>
    /// <param name="kept">How many missing are kept (on a drive that is not connected)</param>
    /// <param name="notes">How many of those removed have notes</param>
    /// <param name="undo">True when u puts them back</param>
    internal static bool Confirm(string caption, int count, int kept, int notes, bool undo)
    {
        var note = Lang.T("Their files and directories are not there any more.");
        if (notes > 0) note += " " + Lang.T("Their notes are removed with them.");
        if (kept > 0) note += " " + Lang.F("{0} on drives that are not connected are kept.", kept);
        if (undo) note += " " + Lang.T("u puts them back.");
        ChoiceBox.Forget();
        return ChoiceBox.Confirm(caption, Lang.F("Remove {0} missing from the list?", count), note, false);
    }

    /// <summary>
    /// The message when nothing is pruned: nothing is missing, or only on drives that are not connected
    /// </summary>
    internal static string NothingToPrune(int kept) => kept == 0
        ? Lang.T("Nothing is missing: all the files and directories are there")
        : Lang.F("Nothing removed: the {0} missing are on drives that are not connected", kept);

    /// <summary>
    /// The message when nothing is missing (":missing")
    /// </summary>
    internal static string NothingMissing => Lang.T("Nothing is missing: all the files and directories are there");
}
