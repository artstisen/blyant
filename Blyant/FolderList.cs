namespace Blyant;

/// <summary>
/// An item of a list with folders: a folder (or a subfolder), or an item that can be in the folder above it
/// </summary>
internal interface IFolderItem
{
    bool IsFolder { get; }

    /// <summary>
    /// Where it is: 0 outside the folders, 1 in a folder, 2 in a subfolder. A folder is at 0, a subfolder at 1
    /// </summary>
    int Level { get; set; }

    /// <summary>
    /// A folder that is closed: the items in it are not shown
    /// </summary>
    bool Collapsed { get; set; }

    /// <summary>
    /// The name of a folder
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The text color (null: none of its own)
    /// </summary>
    ConsoleColor? Color { get; set; }

    /// <summary>
    /// The note (empty: none)
    /// </summary>
    string Note { get; set; }
}

/// <summary>
/// What copying one list with folders into another did (or would do: FolderList.Import with apply false)
/// </summary>
/// <param name="Added">The places added</param>
/// <param name="Skipped">The places that were in the list already (left where they are)</param>
/// <param name="NewFolders">The folders made</param>
/// <param name="MergedFolders">The folders added to a folder of the same name</param>
/// <param name="Notes">The notes copied (with the places and folders added, and to the places and folders that were
/// there without a note)</param>
/// <param name="LeftOut">The places the list cannot hold (a bookmark of a page of Blyant in the favorites)</param>
internal sealed record ImportResult(int Added, int Skipped, int NewFolders, int MergedFolders, int Notes, int LeftOut = 0);

/// <summary>
/// A list in the order set by the user, with folders: the items of a folder follow it in the list, at a level one
/// deeper. A folder can hold subfolders (one level of them), and both hold items. The favorites of the file browser
/// and the bookmarks of the Jump menu are kept in such lists. Items are moved into and out of folders, a folder moves
/// with its items, and what was removed last can be put back.
///
/// This class does not touch the console.
/// </summary>
internal sealed class FolderList<T> where T : class, IFolderItem
{
    /// <summary>
    /// The deepest level of an item (in a subfolder). Folders are one level less deep at the most
    /// </summary>
    internal const int MaxLevel = 2;

    /// <summary>
    /// The items and folders, in their order
    /// </summary>
    internal List<T> Items { get; } = new();

    // What was removed last, with the places it had in the list: an item, a folder with its items, or several items
    private List<(int Index, List<T> Items)>? _removed;

    /// <summary>
    /// Remove an item, or a folder with the items (and subfolders) in it
    /// </summary>
    internal void RemoveAt(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        var count = FolderEnd(index) - index + 1;
        // Kept so that it can be put back
        _removed = new List<(int, List<T>)> { (index, Items.GetRange(index, count)) };
        Items.RemoveRange(index, count);
    }

    /// <summary>
    /// Remove several items (folders are not removed this way)
    /// </summary>
    /// <returns>The number removed</returns>
    internal int RemoveMany(ICollection<T> items)
    {
        var removed = new List<(int, List<T>)>();
        for (var i = 0; i < Items.Count; i++)
        {
            if (!Items[i].IsFolder && items.Contains(Items[i])) removed.Add((i, new List<T> { Items[i] }));
        }

        if (removed.Count == 0) return 0;
        Items.RemoveAll(item => !item.IsFolder && items.Contains(item));
        _removed = removed;
        return removed.Count;
    }

    /// <summary>
    /// Nothing can be put back (the list was emptied)
    /// </summary>
    internal void ForgetRemoved()
    {
        _removed = null;
    }

    /// <summary>
    /// Put back what was removed last, where it was
    /// </summary>
    /// <param name="canReturn">Tells if an item can be put back (one that is in the list again is not)</param>
    /// <returns>The place in the list of the first that was put back, or -1 if there is nothing to put back</returns>
    internal int UndoRemove(Func<T, bool> canReturn)
    {
        if (_removed == null) return -1;
        var first = -1;
        // From the top, so that each gets the place it had
        foreach (var (place, removed) in _removed)
        {
            var items = removed.Where(item => item.IsFolder || canReturn(item)).ToList();
            if (items.Count == 0) continue;
            var index = Math.Min(place, Items.Count);
            // An item that was in a folder goes back into it only if the folder is still there, and nothing goes
            // into the middle of a folder that it was not in: else it is put outside the folders, in front of the
            // folder it would have been in
            if (!CanPlace(index, items[0].Level))
            {
                var shift = items[0].Level;
                foreach (var item in items) item.Level -= shift;
                index = index < Items.Count ? FolderOf(index) : index;
            }

            Items.InsertRange(index, items);
            if (first < 0) first = index;
        }

        _removed = null;
        return first;
    }

    /// <summary>
    /// Move items into a folder or subfolder (last in it, in the order they have in the list), or out of their
    /// folders
    /// </summary>
    /// <param name="items">The items</param>
    /// <param name="folder">The folder, or null to place them last in the list, outside the folders</param>
    /// <returns>The place in the list of the first of them, or -1 if nothing was moved</returns>
    internal int MoveToFolder(ICollection<T> items, T? folder)
    {
        if (folder != null && (!folder.IsFolder || !Items.Contains(folder))) return -1;
        var moving = Items.Where(item => !item.IsFolder && items.Contains(item)).ToList();
        if (moving.Count == 0) return -1;
        Items.RemoveAll(item => moving.Contains(item));
        var index = Items.Count;
        if (folder != null)
        {
            var at = Items.IndexOf(folder);
            index = FolderEnd(at) + 1;
            // Opened, so that what was moved into it can be seen
            Open(at);
        }

        foreach (var item in moving) item.Level = folder == null ? 0 : folder.Level + 1;
        Items.InsertRange(index, moving);
        return index;
    }

    /// <summary>
    /// Move items (or folders, with the items in them) to the top or the end: an item in a folder to the top or
    /// the end of its folder (or subfolder), the others to the top or the end of the list. Their order is kept
    /// </summary>
    /// <returns>The number moved</returns>
    internal int MoveToEnd(ICollection<T> items, bool top)
    {
        var moving = Items.Where(items.Contains).ToList();
        // By where they are: the folder they are in (null: the list)
        var groups = moving.GroupBy(item =>
        {
            var parent = Parent(Items.IndexOf(item));
            return parent < 0 ? null : Items[parent];
        }).ToList();
        foreach (var group in groups)
        {
            var blocks = new List<T>();
            foreach (var item in group)
            {
                var index = Items.IndexOf(item);
                var count = FolderEnd(index) - index + 1;
                blocks.AddRange(Items.GetRange(index, count));
                Items.RemoveRange(index, count);
            }

            int at;
            if (group.Key == null)
            {
                at = top ? 0 : Items.Count;
            }
            else
            {
                var folder = Items.IndexOf(group.Key);
                at = top ? folder + 1 : FolderEnd(folder) + 1;
            }

            Items.InsertRange(at, blocks);
        }

        return moving.Count;
    }

    #region Folders

    /// <summary>
    /// Add a folder in front of an item of the list (in front of its folder, if it is in one)
    /// </summary>
    /// <returns>The place of the folder in the list</returns>
    internal int AddFolder(T folder, int before)
    {
        var index = before < 0 || before >= Items.Count ? Items.Count : FolderOf(before);
        folder.Level = 0;
        Items.Insert(index, folder);
        return index;
    }

    /// <summary>
    /// Add an item below another: first in it when that is a folder, else after it, in the folder it is in. With no
    /// item (-1), at the end of the list, outside the folders
    /// </summary>
    /// <returns>The place of the item in the list</returns>
    internal int AddBelow(T item, int at)
    {
        if (at < 0 || at >= Items.Count)
        {
            item.Level = 0;
            Items.Add(item);
            return Items.Count - 1;
        }

        if (Items[at].IsFolder)
        {
            Open(at);
            item.Level = Items[at].Level + 1;
        }
        else
        {
            Open(Parent(at));
            item.Level = Items[at].Level;
        }

        Items.Insert(at + 1, item);
        return at + 1;
    }

    /// <summary>
    /// Add a subfolder to the folder of an item: first in the folder when the item is the folder, else in front of
    /// the item (in front of its subfolder, if it is in one)
    /// </summary>
    /// <returns>The place of the subfolder in the list, or -1 if the item is not a folder or in one</returns>
    internal int AddSubfolder(T folder, int at)
    {
        if (at < 0 || at >= Items.Count || Items[at].Level == 0 && !Items[at].IsFolder) return -1;
        var index = at + 1;
        if (Items[at].Level > 0)
        {
            index = at;
            while (Items[index].Level > 1) index--;
        }

        Open(FolderOf(at));
        folder.Level = 1;
        Items.Insert(index, folder);
        return index;
    }

    /// <summary>
    /// Open or close all the folders (and subfolders)
    /// </summary>
    /// <returns>The number of folders in the list</returns>
    internal int SetAllCollapsed(bool collapsed)
    {
        var folders = 0;
        foreach (var item in Items)
        {
            if (!item.IsFolder) continue;
            item.Collapsed = collapsed;
            folders++;
        }

        return folders;
    }

    /// <summary>
    /// The folder an item is in, at the top of the list (the item itself if it is not in a folder)
    /// </summary>
    internal int FolderOf(int index)
    {
        if (index < 0 || index >= Items.Count) return index;
        while (index > 0 && Items[index].Level > 0) index--;
        return index;
    }

    /// <summary>
    /// The folder or subfolder an item (or a subfolder) is directly in, or -1 if it is not in a folder
    /// </summary>
    internal int Parent(int index)
    {
        if (index < 0 || index >= Items.Count || Items[index].Level == 0) return -1;
        var level = Items[index].Level - 1;
        for (var i = index - 1; i >= 0; i--)
        {
            if (Items[i].IsFolder && Items[i].Level == level) return i;
        }

        return -1;
    }

    /// <summary>
    /// An item is shown: none of the folders it is in is closed
    /// </summary>
    internal bool IsShown(int index)
    {
        for (var parent = Parent(index); parent >= 0; parent = Parent(parent))
        {
            if (Items[parent].Collapsed) return false;
        }

        return true;
    }

    /// <summary>
    /// The number of items in a folder, with those in its subfolders (the subfolders are not counted)
    /// </summary>
    internal int FolderCount(int index)
    {
        if (index < 0 || index >= Items.Count || !Items[index].IsFolder) return 0;
        var count = 0;
        for (var i = index + 1; i <= FolderEnd(index); i++)
        {
            if (!Items[i].IsFolder) count++;
        }

        return count;
    }

    /// <summary>
    /// The number of subfolders in a folder
    /// </summary>
    internal int SubfolderCount(int index)
    {
        if (index < 0 || index >= Items.Count || !Items[index].IsFolder) return 0;
        var count = 0;
        for (var i = index + 1; i <= FolderEnd(index); i++)
        {
            if (Items[i].IsFolder) count++;
        }

        return count;
    }

    /// <summary>
    /// The last item of a folder, with its subfolders (the folder itself if it is empty, or the item if it is not
    /// a folder)
    /// </summary>
    internal int FolderEnd(int index)
    {
        if (!Items[index].IsFolder) return index;
        var level = Items[index].Level;
        var end = index;
        while (end + 1 < Items.Count && Items[end + 1].Level > level) end++;
        return end;
    }

    /// <summary>
    /// Copy the items of another list with folders into this one (the favorites into the bookmarks, and back): a
    /// place as a place, a folder as a folder and a subfolder as a subfolder (with its color, and closed if it is),
    /// in their order, after the items there are. A place that is in the list already is left out, and a folder with
    /// the name of a folder of the list is added to it (at its end), as is a subfolder with the name of a subfolder of
    /// that folder. The notes are copied too, also to a place or folder that is there without a note (a note it has is
    /// kept). The other list stays as it is
    /// </summary>
    /// <param name="from">The items of the other list, in their order (a folder followed by those in it)</param>
    /// <param name="apply">False to only count what would be done (nothing in the list is changed)</param>
    /// <param name="newPlace">A new item for a place of the other list, without its color, level and note (null:
    /// this list cannot hold it)</param>
    /// <param name="newFolder">A new folder with a name</param>
    /// <param name="samePlace">True if two items of this list are of the same place</param>
    internal ImportResult Import<TFrom>(IReadOnlyList<TFrom> from, bool apply, Func<TFrom, T?> newPlace,
        Func<string, T> newFolder, Func<T, T, bool> samePlace) where TFrom : IFolderItem
    {
        // When only counting, the items are put in a copy of the list, and the notes that would be copied are
        // counted without changing the items
        var list = apply ? Items : new List<T>(Items);
        var noted = new HashSet<T>();
        int added = 0, skipped = 0, newFolders = 0, merged = 0, notes = 0, leftOut = 0;

        // The note of an item of the other list, to an item that has none
        void CopyNote(TFrom source, T to)
        {
            if (source.Note.Length == 0 || to.Note.Length > 0 || noted.Contains(to)) return;
            if (apply) to.Note = source.Note;
            else noted.Add(to);
            notes++;
        }

        T? folder = null; // The folder the items of the folder go to
        T? subfolder = null; // And the subfolder those of a subfolder go to

        // The end of a folder of the list (its last item, with its subfolders)
        int End(T of)
        {
            var end = list.IndexOf(of);
            while (end + 1 < list.Count && list[end + 1].Level > of.Level) end++;
            return end;
        }

        foreach (var source in from)
        {
            if (source.IsFolder)
            {
                var sub = source.Level > 0 && folder != null;
                var start = sub ? list.IndexOf(folder!) + 1 : 0;
                var end = sub ? End(folder!) + 1 : list.Count;
                var same = list.GetRange(start, end - start).FirstOrDefault(item =>
                    item.IsFolder && item.Level == (sub ? 1 : 0) &&
                    item.Name.Equals(source.Name, StringComparison.OrdinalIgnoreCase));
                if (same == null)
                {
                    same = newFolder(source.Name);
                    same.Color = source.Color;
                    same.Collapsed = source.Collapsed;
                    same.Level = sub ? 1 : 0;
                    list.Insert(end, same);
                    newFolders++;
                }
                else
                {
                    merged++;
                }

                CopyNote(source, same);
                if (sub)
                {
                    subfolder = same;
                }
                else
                {
                    folder = same;
                    subfolder = null;
                }

                continue;
            }

            if (source.Level < 2) subfolder = null;
            if (source.Level == 0) folder = null;
            if (newPlace(source) is not { } place)
            {
                leftOut++;
                continue;
            }

            // (also one added before: a place that is twice in the other list is added once)
            var there = list.Find(item => !item.IsFolder && samePlace(item, place));
            if (there != null)
            {
                CopyNote(source, there);
                skipped++;
                continue;
            }

            var into = subfolder ?? folder;
            place.Color = source.Color;
            place.Level = into == null ? 0 : into.Level + 1;
            CopyNote(source, place);
            if (into == null)
            {
                list.Add(place);
            }
            else
            {
                // Last in its folder
                list.Insert(End(into) + 1, place);
            }

            added++;
        }

        return new ImportResult(added, skipped, newFolders, merged, notes, leftOut);
    }

    /// <summary>
    /// Open the folders an item is in, so that it is shown
    /// </summary>
    internal void Reveal(int index)
    {
        if (index >= 0 && index < Items.Count) Open(Parent(index));
    }

    // Open a folder, and the folder it is in
    private void Open(int index)
    {
        for (; index >= 0; index = Parent(index))
        {
            if (Items[index].IsFolder) Items[index].Collapsed = false;
        }
    }

    // The deepest level an item that follows an item can have: in the folder it is, or in its own if it is a folder
    private static int Deepest(T item) => item.IsFolder ? item.Level + 1 : item.Level;

    // Something at a level can be put in at a place of the list: it is in a folder that is there, and does not take
    // the items that follow into it
    private bool CanPlace(int index, int level)
    {
        if (index > 0 ? level > Deepest(Items[index - 1]) : level > 0) return false;
        return index >= Items.Count || Items[index].Level <= level;
    }

    #endregion

    /// <summary>
    /// Move an item or a folder one place up or down in the list.
    /// An item moved onto a folder goes into it (and onto a subfolder in a folder, into the subfolder), and one moved
    /// past the first or last item of its folder leaves the folder. A folder is closed and moves with its items, past
    /// the item or folder next to it; a subfolder moves among the items of its folder, and past its first or last into
    /// the folder above or below (if there is one)
    /// </summary>
    /// <returns>False if it is at the end of the list (or a subfolder has no folder to go to)</returns>
    internal bool Move(int index, bool up)
    {
        if (index < 0 || index >= Items.Count) return false;
        var item = Items[index];
        if (item.IsFolder) return MoveFolder(index, up);
        var level = item.Level;
        if (up)
        {
            if (index == 0) return false;
            var previous = Items[index - 1];
            if (previous.IsFolder && previous.Level == level - 1)
            {
                // The first of its folder: out of it, above it
                Swap(index - 1, index);
                item.Level = level - 1;
            }
            else if (Deepest(previous) > level)
            {
                // Into the folder above, as the last in it (from there, into its last subfolder)
                item.Level = level + 1;
                Open(Parent(index));
            }
            else
            {
                Swap(index - 1, index);
            }

            return true;
        }

        if (index + 1 >= Items.Count || Items[index + 1].Level < level)
        {
            if (level == 0) return false;
            // The last of its folder: out of it, below it
            item.Level = level - 1;
            return true;
        }

        var next = Items[index + 1];
        Swap(index, index + 1);
        if (next.IsFolder)
        {
            // Into the folder below, as the first in it
            item.Level = level + 1;
            next.Collapsed = false;
        }

        return true;
    }

    // Move a folder (closed, with its items) past the item or folder next to it. It is not closed if it cannot move
    private bool MoveFolder(int index, bool up)
    {
        var folder = Items[index];
        var end = FolderEnd(index);
        var count = end - index + 1;
        var block = Items.GetRange(index, count);
        if (folder.Level == 0)
        {
            int target;
            if (up)
            {
                if (index == 0) return false;
                target = FolderOf(index - 1);
            }
            else
            {
                if (end == Items.Count - 1) return false;
                // After the item or folder that follows
                target = FolderEnd(end + 1) + 1 - count;
            }

            folder.Collapsed = true;
            Items.RemoveRange(index, count);
            Items.InsertRange(target, block);
            return true;
        }

        // A subfolder
        var parent = Parent(index);
        if (up)
        {
            if (parent == index - 1)
            {
                // The first of its folder: last in the folder above
                if (parent == 0) return false;
                var above = FolderOf(parent - 1);
                if (!Items[above].IsFolder) return false;
                folder.Collapsed = true;
                Items.RemoveRange(index, count);
                Items.InsertRange(FolderEnd(above) + 1, block);
                Open(above);
                return true;
            }

            var before = index - 1;
            while (Items[before].Level > folder.Level) before--;
            folder.Collapsed = true;
            Items.RemoveRange(index, count);
            Items.InsertRange(before, block);
            return true;
        }

        if (end + 1 >= Items.Count || Items[end + 1].Level < folder.Level)
        {
            // The last of its folder: first in the folder below
            if (end + 1 >= Items.Count || !Items[end + 1].IsFolder) return false;
            folder.Collapsed = true;
            Items.RemoveRange(index, count);
            var below = end + 1 - count;
            Items.InsertRange(below + 1, block);
            Open(below);
            return true;
        }

        var after = FolderEnd(end + 1) + 1 - count;
        folder.Collapsed = true;
        Items.RemoveRange(index, count);
        Items.InsertRange(after, block);
        return true;
    }

    private void Swap(int a, int b)
    {
        (Items[a], Items[b]) = (Items[b], Items[a]);
    }

    /// <summary>
    /// Reads the levels of the lines of a list file, where a line in a folder starts with a blank space, and one in a
    /// subfolder with two. A line that is in more folders than there are above it is placed in the ones there are
    /// </summary>
    internal sealed class LevelReader
    {
        private bool _inFolder;
        private bool _inSubfolder;

        /// <summary>
        /// The level of a line with a number of blank spaces in front of it (Add takes it into the list after)
        /// </summary>
        internal int LevelOf(int spaces, bool isFolder)
        {
            if (isFolder) return spaces > 0 && _inFolder ? 1 : 0;
            var level = Math.Min(spaces, MaxLevel);
            if (level == 2 && !_inSubfolder) level = 1;
            if (level == 1 && !_inFolder) level = 0;
            return level;
        }

        /// <summary>
        /// A line at a level was taken into the list
        /// </summary>
        internal void Add(int level, bool isFolder)
        {
            if (isFolder)
            {
                _inFolder = true;
                _inSubfolder = level == 1;
                return;
            }

            if (level < 2) _inSubfolder = false;
            if (level == 0) _inFolder = false;
        }

        /// <summary>
        /// The number of blank spaces a line starts with
        /// </summary>
        internal static int Spaces(string line)
        {
            var spaces = 0;
            while (spaces < line.Length && line[spaces] == ' ') spaces++;
            return spaces;
        }
    }
}
