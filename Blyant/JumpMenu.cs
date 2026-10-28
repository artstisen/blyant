namespace Blyant;

/// <summary>
/// The Jump menu: a box on top of the page with the tabs Bookmarks (places kept with B or ":bookmark"), Recent (the
/// jump history), Projects (the list of projects), File browser (with its lists History, Favorites and Hotlist as sub-tabs) and Blyant (the pages
/// of Blyant). Tab and Shift+Tab go from tab to tab, h and l through all the lists in a row (the
/// sub-tabs too); B bookmarks the selected place, and the Bookmarks tab has keys to arrange, rename and remove them. A place is picked with the
/// selection arrow (j/k, gg/G) and Enter; h/l go from tab to tab, "/" filters the list of the tab, I shows
/// information about the selected place and D takes a place out of the jump history. Only names are shown, not
/// paths
/// </summary>
internal static class JumpMenu
{
    // The lists, in the order h and l go through them
    internal enum Tab
    {
        Bookmarks,
        Jumps, // Recent: the jump history
        Projects, // The projects
        History, // The file history of the file browser
        Favorites,
        Hotlist,
        Configuration // The pages of Blyant
    }

    // The tabs at the top: each has one list, or (the file browser) several as sub-tabs
    internal enum Group
    {
        Bookmarks,
        Recent,
        Projects,
        FileBrowser,
        Blyant
    }

    internal static Group GroupOf(Tab tab) => tab switch
    {
        Tab.Bookmarks => Group.Bookmarks,
        Tab.Jumps => Group.Recent,
        Tab.Projects => Group.Projects,
        Tab.History or Tab.Favorites or Tab.Hotlist => Group.FileBrowser,
        _ => Group.Blyant
    };

    /// <summary>
    /// Tab and Shift+Tab: the first list of the next (or the previous) tab at the top, going round
    /// </summary>
    internal static int NextGroup(IReadOnlyList<Tab> tabs, int tab, bool back)
    {
        var groups = tabs.Select(GroupOf).Distinct().ToList();
        var next = groups[TabLayout.Next(groups.IndexOf(GroupOf(tabs[tab])), groups.Count, back)];
        return tabs.ToList().FindIndex(list => GroupOf(list) == next);
    }

    // In the language in use
    private static string GroupName(Group group) => group switch
    {
        Group.Bookmarks => Lang.T("Bookmarks"),
        Group.Recent => Lang.T("Recent"),
        Group.Projects => Lang.T("Projects"),
        Group.FileBrowser => DialogText.HelpSectionFiles,
        _ => DialogText.ApplicationName
    };

    // The short name of a tab, used when the names do not all fit in a row (a long language)
    private static string ShortGroupName(Group group) => group switch
    {
        Group.FileBrowser => DialogText.CaptionsFiles,
        _ => GroupName(group)
    };

    // The cells a row of tabs takes (as TabLayout.SubTabStarts places them)
    private static int TabRowWidth(IEnumerable<string> names) => names.Sum(name => TextMetrics.WidthOf(name) + 4);

    // The bookmarks: j k move the selected one while this is on (V)
    private static bool _moving;

    // A count typed in front of a key (5j, 5gg)
    private static readonly ListCount Count = new();

    /// <summary>
    /// The panes below the bookmarks (the note and the peek of the selected bookmark or folder) are shown (setting
    /// "bookmarkpanes")
    /// </summary>
    internal static bool BookmarkPanes { get; set; } = true;

    /// <summary>
    /// The peek is shown in the panes below the bookmarks (setting "bookmarkpeek")
    /// </summary>
    internal static bool BookmarkPeek { get; set; } = true;

    /// <summary>
    /// The rows of the Bookmarks tab are numbered, as 12gg counts them (setting "bookmarknumbers", off unless turned
    /// on)
    /// </summary>
    internal static bool BookmarkNumbers { get; set; }

    // The rows of a tab are numbered: the bookmarks, and the history, the favorites and the hotlist as in the file
    // browser (the same settings)
    private static bool NumbersOn(Tab tab) => tab switch
    {
        Tab.Bookmarks => BookmarkNumbers,
        Tab.History => FileExplorer.HistoryNumbers,
        Tab.Favorites => FileExplorer.FavoriteNumbers,
        Tab.Hotlist => FileExplorer.HotlistNumbers,
        _ => false
    };

    // ":number" and ":nonumber" on a tab: false on a tab without numbers
    private static bool SetNumbers(Tab tab, bool on)
    {
        switch (tab)
        {
            case Tab.Bookmarks: BookmarkNumbers = on; break;
            case Tab.History: FileExplorer.HistoryNumbers = on; break;
            case Tab.Favorites: FileExplorer.FavoriteNumbers = on; break;
            case Tab.Hotlist: FileExplorer.HotlistNumbers = on; break;
            default: return false;
        }

        return true;
    }

    // The start of the name of the file a note of a bookmark is written in, in the editor (in the data directory,
    // removed after)
    private const string BookmarkNoteFile = "bookmark-note"; // English: the name of a file

    // The place a command (":book 3", ":new") or W jumps to, and that the Bookmarks tab is to be shown (":book")
    private static JumpTarget? _jumpTo;
    private static bool _showBookmarks;
    private static string _message = string.Empty;

    // The bookmarks marked with Space (several are moved to a folder, to the top or the end, or removed at once)
    private static readonly HashSet<Bookmarks.Entry> Marks = new();

    // The bookmark (or folder) to select when the rows are made again (after it was moved, added or put back)
    private static Bookmarks.Entry? _select;

    // The color of a file or directory that is a favorite with a color of its own
    private static ConsoleColor? FavoriteColor(JumpTarget target) => target.Kind is JumpKind.File or JumpKind.Directory
        ? FileFavorites.ColorOf(target.Target)
        : null;

    // A place in the list of a tab, and when it was jumped to or opened (DateTime.MinValue: not known). A file or
    // directory that is a favorite with a color of its own is shown in it. On the Bookmarks tab a row is a bookmark
    // or a folder, and the color of a bookmark is used before that of a favorite
    private sealed record Row(JumpTarget Target, DateTime Time)
    {
        // The name shown, if it is not the name of the place (a directory of the hotlist with a name of its own)
        internal string? Label { get; init; }

        internal string Name => Label ?? Target.Name;

        internal ConsoleColor? Color { get; init; } = FavoriteColor(Target);

        // The bookmark or folder of a row of the Bookmarks tab
        internal Bookmarks.Entry? Bookmark { get; init; }

        internal bool IsFolder => Bookmark?.IsFolder == true;
    }

    // The list the menu was on when it was closed: it is shown again the next time it is opened (while Blyant
    // runs; it is not saved)
    private static Tab _lastTab = Tab.Bookmarks;

    /// <summary>
    /// Open the menu on a list the next time it is shown (":book" alone: the bookmarks)
    /// </summary>
    internal static void ShowNextOn(Tab tab)
    {
        _lastTab = tab;
    }

    // ":book 3" on a row of the Bookmarks tab: its number among all the bookmarks (also when a filter shows some, and
    // the bookmarks in closed folders count); nothing for a folder
    private static string BookCommand(Row row) =>
        row.Bookmark is { IsFolder: false } entry ? Bookmarks.CommandOf(entry) : string.Empty;

    private const int MarkWidth = 4; // "FILE", "DIR", "PAGE"

    /// <summary>
    /// The lists that are shown, in the order h and l go through them
    /// </summary>
    internal static List<Tab> Tabs() => Enum.GetValues<Tab>().ToList();

    // The name of a sub-tab, in the language in use
    private static string TabName(Tab tab) => tab switch
    {
        Tab.History => Lang.T("History"),
        Tab.Favorites => Lang.T("Favorites"),
        Tab.Hotlist => DialogText.CaptionsHotlist,
        _ => GroupName(GroupOf(tab))
    };

    // The places of a tab
    private static List<Row> RowsOf(Tab tab)
    {
        switch (tab)
        {
            case Tab.Bookmarks:
                return BookmarkRows(string.Empty);
            case Tab.Jumps:
                JumpHistory.Load();
                return JumpHistory.Items.Select(entry => new Row(entry.Target, entry.Jumped)).ToList();
            case Tab.Projects:
                // In the color of the project (else of a favorite)
                return Projects.All().Select(project =>
                {
                    var target = new JumpTarget(JumpKind.Directory, project.Path);
                    return new Row(target, DateTime.MinValue)
                    {
                        Color = Projects.ColorOf(project.Path) ?? FavoriteColor(target)
                    };
                }).ToList();
            case Tab.History:
                FileHistory.Load();
                return FileHistory.Items.Select(entry => new Row(new JumpTarget(JumpKind.File, entry.Path), entry.Opened))
                    .ToList();
            case Tab.Favorites:
                FileFavorites.Load();
                return FileFavorites.Items.Where(favorite => !favorite.IsFolder)
                    .Select(favorite => new Row(new JumpTarget(
                        Directory.Exists(favorite.Path) ? JumpKind.Directory : JumpKind.File, favorite.Path),
                        DateTime.MinValue)).ToList();
            case Tab.Hotlist:
                DirectoryHotlist.Load();
                return DirectoryHotlist.Items.Select(entry =>
                    new Row(new JumpTarget(JumpKind.Directory, entry.Path), DateTime.MinValue)
                    {
                        Label = entry.Name.Length > 0 ? entry.Name : null
                    }).ToList();
            default:
                return JumpPages.Ids.Select(id => new Row(new JumpTarget(JumpKind.Page, id), DateTime.MinValue))
                    .ToList();
        }
    }

    // The rows of the Bookmarks tab (as the list is now; it is read when the menu opens). Without a filter: the folders,
    // and the bookmarks in the open ones two spaces in. With a filter: the bookmarks and folders with the text in their
    // names or notes, without the folders they are in
    private static List<Row> BookmarkRows(string filter)
    {
        var rows = new List<Row>();
        for (var index = 0; index < Bookmarks.Items.Count; index++)
        {
            var entry = Bookmarks.Items[index];
            // Two blank spaces in for each folder it is in
            var indent = new string(' ', 2 * entry.Level);
            var found = filter.Length == 0 || entry.Shown.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        entry.Note.Contains(filter, StringComparison.OrdinalIgnoreCase);
            if (entry.IsFolder)
            {
                if (!found || filter.Length == 0 && !Bookmarks.IsShown(index)) continue;
                var count = Bookmarks.FolderCount(index);
                rows.Add(new Row(entry.Target, DateTime.MinValue)
                {
                    Bookmark = entry,
                    Label = (filter.Length == 0 ? indent : string.Empty) + (entry.Collapsed ? "[+] " : "[-] ") +
                            entry.Name + " (" + count + ")",
                    Color = entry.Color
                });
                continue;
            }

            if (!found || filter.Length == 0 && !Bookmarks.IsShown(index)) continue;
            rows.Add(new Row(entry.Target, DateTime.MinValue)
            {
                Bookmark = entry,
                Label = (filter.Length == 0 ? indent : string.Empty) + entry.Shown,
                Color = entry.Color ?? FavoriteColor(entry.Target)
            });
        }

        return rows;
    }

    /// <summary>
    /// A name shortened to a width with "..." in the middle, so its start and its end (the extension) are seen
    /// </summary>
    internal static string ShortName(string name, int width)
    {
        // Counted in terminal cells: a wide character takes two, and none is split. For the screen: control
        // characters are blank spaces
        name = TextMetrics.Printable(name);
        if (TextMetrics.WidthOf(name) <= width) return name;
        if (width <= 3) return TextMetrics.StartPart(name, Math.Max(0, width));
        var end = (width - 3) / 2;
        var start = width - 3 - end;
        return TextMetrics.StartPart(name, start) + "..." + TextMetrics.EndPart(name, end);
    }

    // The kind of a place, in short (English, as the kinds of file in the file browser)
    private static string Mark(JumpTarget target) => target.Kind switch
    {
        JumpKind.Directory => "DIR",
        JumpKind.Page => "PAGE",
        _ => "FILE"
    };

    // The name shown for a place: with a mark when it is not there any more
    private static string RowName(Row row) =>
        row.IsFolder || row.Target.Exists ? row.Name : row.Name + " " + DialogText.FavoriteMissing;

    // When a place was jumped to or opened: the time today, else the date
    private static string When(DateTime time) =>
        time == DateTime.MinValue ? string.Empty
        : time.Date == DateTime.Today ? time.ToString("HH:mm")
        : time.ToString("yyyy-MM-dd");

    /// <summary>
    /// Show the Jump menu until a place is picked (Enter) or it is closed (Esc)
    /// </summary>
    /// <returns>The place picked, or null</returns>
    internal static JumpTarget? Pick()
    {
        // In the color of text in frames and boxes
        using var frameText = ConsoleColors.FrameTextScope();
        var tabs = Tabs();
        // The favorites, for the colors of the names
        FileFavorites.Load();
        var filters = new Dictionary<Tab, string>();
        var tab = Math.Max(0, tabs.IndexOf(_lastTab));
        var selected = 0;
        var scroll = 0;
        // The whole menu is drawn (false: only what the selection moving changed)
        var full = true;
        _moving = false;
        _message = string.Empty;
        _select = null;
        _jumpTo = null;
        _showBookmarks = false;
        Marks.Clear();
        // The bookmarks are read once: the rows refer to them (marks, moving)
        Bookmarks.Load();
        while (true)
        {
            var current = tabs[tab];
            _lastTab = current;
            var filter = filters.GetValueOrDefault(current, string.Empty);
            var rows = current == Tab.Bookmarks
                ? BookmarkRows(filter)
                : RowsOf(current).Where(row => filter.Length == 0 ||
                    row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (_select != null)
            {
                // The bookmark or folder that was moved, added or put back
                var found = rows.FindIndex(row => ReferenceEquals(row.Bookmark, _select));
                if (found >= 0) selected = found;
                _select = null;
            }

            selected = Math.Clamp(selected, 0, Math.Max(0, rows.Count - 1));
            // Follow the size of the terminal window (all is drawn again when it has changed). The page below the
            // menu was drawn for the size before: the screen is cleared, and the page is drawn when the menu closes
            if (Init.ApplyAutoSize())
            {
                Tools.ClearScreen();
                full = true;
            }
            var visible = Draw(tabs, tab, filter, rows, selected, ref scroll, full);
            // (the keys that only move the selection set it to false)
            full = true;
            var message = _message;
            _message = _moving ? DialogText.Moving : string.Empty;
            var key = Keyboard.ReadKey();
            // The window was resized (or Blyant went on after Ctrl+Z): all is drawn again, with the message
            if (Keyboard.IsRedraw(key))
            {
                _message = message;
                continue;
            }

            // A count in front of a key: 5j 5k (5 rows), 5gg 5G (row 5), shown next to the filter. Esc only drops it
            if (Count.Add(key))
            {
                _message = Count.Shown;
                full = false;
                continue;
            }

            var count = Count.Take();
            if (count != null && key.Key == ConsoleKey.Escape)
            {
                full = false;
                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (_moving)
            {
                // Moving the selected bookmark or folder: j k move it (into and out of folders; with a count, that
                // many steps), and Esc, Enter or V end it
                if (navigation is ConsoleKey.DownArrow or ConsoleKey.UpArrow && rows.Count > 0 &&
                    rows[selected].Bookmark is { } moved)
                {
                    var steps = 0;
                    while (steps < (count ?? 1) && MoveBookmark(moved, navigation == ConsoleKey.UpArrow)) steps++;
                    if (steps > 0) SaveBookmarks();
                    _select = moved;
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.V)
                {
                    _moving = false;
                    _message = string.Empty;
                }

                continue;
            }

            if (Jump.IsKey(key)) return null;
            if (key.Key == ConsoleKey.Tab)
            {
                // The next (Shift: the previous) tab at the top
                tab = NextGroup(tabs, tab, (key.Modifiers & ConsoleModifiers.Shift) != 0);
                Marks.Clear();
                selected = 0;
                scroll = 0;
                continue;
            }

            if (key.Key is ConsoleKey.Escape || key.KeyChar is 'q' or 'Q')
            {
                // Esc takes the marks away, then the filter, before it closes the menu
                if (Marks.Count > 0 && key.Key == ConsoleKey.Escape && current == Tab.Bookmarks)
                {
                    Marks.Clear();
                    continue;
                }

                if (filter.Length > 0 && key.Key == ConsoleKey.Escape)
                {
                    filters[current] = string.Empty;
                    continue;
                }

                return null;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (rows.Count == 0) continue;
                if (rows[selected].Bookmark is { IsFolder: true } folder)
                {
                    // A folder is opened or closed
                    folder.Collapsed = !folder.Collapsed;
                    SaveBookmarks();
                    continue;
                }

                var target = rows[selected].Target;
                if (target.Exists) return target;
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.CaptionsJump, DialogText.FileNotFound, target.FullPath);
                continue;
            }

            switch (navigation)
            {
                case ConsoleKey.DownArrow:
                    selected = Math.Min(selected + (count ?? 1), Math.Max(0, rows.Count - 1));
                    full = false;
                    continue;
                case ConsoleKey.UpArrow:
                    selected = Math.Max(selected - (count ?? 1), 0);
                    full = false;
                    continue;
                case ConsoleKey.RightArrow:
                    tab = TabLayout.Next(tab, tabs.Count, false);
                    Marks.Clear();
                    selected = 0;
                    scroll = 0;
                    continue;
                case ConsoleKey.LeftArrow:
                    tab = TabLayout.Next(tab, tabs.Count, true);
                    Marks.Clear();
                    selected = 0;
                    scroll = 0;
                    continue;
            }

            switch (key.KeyChar)
            {
                case 'G':
                    selected = ListCount.Row(count, rows.Count, false);
                    full = false;
                    break;
                case 'g':
                    if (Console.ReadKey(true).KeyChar == 'g') selected = ListCount.Row(count, rows.Count, true);
                    full = false;
                    break;
                case '/':
                    // Only show the places with a text in their names
                    filters[current] = CommandLine.Read('/', false) ?? string.Empty;
                    selected = 0;
                    scroll = 0;
                    break;
                case 'i':
                case 'I':
                    if (rows.Count > 0 && !rows[selected].IsFolder) ShowInfo(current, rows[selected]);
                    break;
                case 'b':
                case 'B':
                    // Keep the selected place as a bookmark (to the list read when the menu was opened, so a
                    // bookmark removed can still be put back with u; the marks are left)
                    if (rows.Count == 0 || rows[selected].IsFolder) break;
                    _message = Bookmarks.AddWithMessage(rows[selected].Target, false);
                    Marks.Clear();
                    break;
                case ':':
                    if (RunBookmarkCommand(current, rows, selected, filter))
                    {
                        // Closed, or a place to jump to (":book 3", ":new")
                        var jump = _jumpTo;
                        _jumpTo = null;
                        return jump;
                    }

                    if (_showBookmarks)
                    {
                        // ":book": the bookmarks
                        _showBookmarks = false;
                        tab = tabs.IndexOf(Tab.Bookmarks);
                        selected = 0;
                        scroll = 0;
                    }

                    break;
                case 'w':
                case 'W':
                    // A new file, bookmarked below the selected bookmark, opened in the editor (a jump)
                    if (current == Tab.Bookmarks && NewBookmarkFile(rows, selected, string.Empty) is { } made) return made;
                    break;
                case 'n':
                case 'N':
                    if (current == Tab.Bookmarks && rows.Count > 0) EditBookmarkNote(rows[selected]);
                    break;
                case 'v':
                case 'V':
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    if (filter.Length > 0)
                    {
                        _message = DialogText.BookmarkMoveFiltered;
                        break;
                    }

                    _moving = true;
                    _message = DialogText.Moving;
                    break;
                case ' ':
                    // Mark (or unmark) the bookmark and go to the next
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    if (rows[selected].Bookmark is not { IsFolder: false } marked)
                    {
                        _message = DialogText.BookmarkMarkFolder;
                        break;
                    }

                    if (!Marks.Remove(marked)) Marks.Add(marked);
                    selected = Math.Min(selected + 1, rows.Count - 1);
                    break;
                case 'm':
                case 'M':
                    if (current == Tab.Bookmarks) MoveBookmarksToFolder(rows, selected);
                    break;
                case 't':
                case 'T':
                case 'e':
                case 'E':
                    // The marked bookmarks (or the selected bookmark or folder) first or last (in a folder: of it)
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    MoveBookmarksToEnd(rows, selected, key.KeyChar is 't' or 'T');
                    break;
                case 'a':
                case 'A':
                    if (current == Tab.Bookmarks) NewBookmarkFolder(rows, selected, filter, string.Empty);
                    break;
                case 's':
                case 'S':
                    if (current == Tab.Bookmarks) NewBookmarkSubfolder(rows, selected, filter, string.Empty);
                    break;
                case 'x':
                case 'X':
                    if (current == Tab.Bookmarks && rows.Count > 0) RenameBookmark(rows[selected], string.Empty);
                    break;
                case 'c':
                case 'C':
                    if (current == Tab.Bookmarks && rows.Count > 0) PickBookmarkColor(rows[selected]);
                    break;
                case 'u':
                case 'U':
                    if (current == Tab.Bookmarks) UndoBookmarkRemove();
                    break;
                case 'd':
                case 'D':
                    if (current == Tab.Bookmarks && rows.Count > 0)
                    {
                        RemoveBookmarks(rows, selected);
                        break;
                    }

                    // A place is taken out of the jump history
                    if (current != Tab.Jumps || rows.Count == 0) break;
                    JumpHistory.Load();
                    var index = JumpHistory.Items.ToList().FindIndex(entry => entry.Target.SameAs(rows[selected].Target));
                    JumpHistory.RemoveAt(index);
                    var status = JumpHistory.Save();
                    if (status != DialogText.Success)
                    {
                        ChoiceBox.Forget();
                        ChoiceBox.Message(DialogText.CaptionsJump, status, string.Empty);
                    }

                    break;
                case '?':
                    Tools.WriteEditorHelp(Tools.HelpTopic.Jump);
                    Tools.ClearScreen();
                    break;
            }

            if (key.Key == ConsoleKey.PageDown) selected = Math.Min(selected + visible, Math.Max(0, rows.Count - 1));
            if (key.Key == ConsoleKey.PageUp) selected = Math.Max(selected - visible, 0);
        }
    }

    // What the menu showed when it was drawn last: when only the selection has moved since (the same rows, and the list
    // has not scrolled), only what changed is drawn again
    private sealed record Shown(int Top, int Left, int Width, int Tab, string Filter, int Rows, int Scroll,
        int Selected, bool Moving, int Marks, int LineWidth, int LineCount);

    private static Shown? _shown;

    // Draw the menu: the box, its tabs, the filter and the places of the tab. With full false and only the selection
    // moved: the rows it moved from and to, the row of the filter (a count, a message) and the panes. Returns how many
    // places are seen
    private static int Draw(List<Tab> tabs, int tab, string filter, List<Row> rows, int selected, ref int scroll,
        bool full = true)
    {
        // Down to the row above the bottom line, so it covers a box shown before it
        var height = Math.Max(8, Init.LineCount - 5);
        // The places, and below them on the Bookmarks tab the panes with the note and the peek of the selected one
        var visible = Math.Max(1, height - 5);
        var paneRows = tabs[tab] == Tab.Bookmarks ? NotePanes.Rows(BookmarkPanes, visible, 10) : 0;
        if (paneRows > 0) visible -= paneRows + 1;
        scroll = Math.Clamp(scroll, Math.Max(0, selected - visible + 1), selected);
        var before = _shown;
        var partial = !full && before != null && before.Tab == tab && before.Filter == filter &&
                      before.Rows == rows.Count && before.Scroll == scroll && before.Moving == _moving &&
                      before.Marks == Marks.Count && before.LineWidth == Init.LineWidth &&
                      before.LineCount == Init.LineCount;
        int top, left, width;
        if (partial)
        {
            (top, left, width) = (before!.Top, before.Left, before.Width);
        }
        else
        {
            ChoiceBox.Forget();
            var inner = Math.Max(30, Init.LineWidth - 6);
            (top, left, width, _) = ChoiceBox.DrawFrame(DialogText.CaptionsJump, inner, height);
        }

        _shown = new Shown(top, left, width, tab, filter, rows.Count, scroll, selected, _moving, Marks.Count,
            Init.LineWidth, Init.LineCount);

        // The tabs at the top, and below them the lists of the tab shown (its sub-tabs), each row with the keys that
        // change it at the end
        if (!partial)
        {
            var group = GroupOf(tabs[tab]);
            var groups = tabs.Select(GroupOf).Distinct().ToList();
            // The names of the tabs, or their short names if they do not fit next to the keys
            var groupNames = groups.Select(GroupName).ToList();
            if (TabRowWidth(groupNames) > width - 7) groupNames = groups.Select(ShortGroupName).ToList();
            DrawTabRow(top + 1, left, width, groupNames, groups.IndexOf(group), "Tab"); // English
            var lists = tabs.Where(list => GroupOf(list) == group).ToList();
            DrawTabRow(top + 2, left, width, lists.Count > 1 ? lists.Select(TabName).ToList() : new List<string>(),
                lists.IndexOf(tabs[tab]), "h/l"); // English: the keys
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.TabFrame);
            Tools.SetCursorEx(top + 3, left + 2);
            Console.Write(new string('-', Math.Max(0, width - 2)));
        }
        else
        {
            // The row of the filter is written again (the count or the message on it may have changed)
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.SetCursorEx(top + 4, left + 1);
            Console.Write(new string(' ', width));
        }

        // The filter
        Tools.SetCursorEx(top + 4, left + 3);
        if (filter.Length == 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Lang.T("/ Filter"));
        }
        else
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(Tools.TruncateString(Lang.F("Filter: {0}", filter), width - 4, true));
        }

        // A message (a bookmark added ...), or what the list is
        var room = left + width - 1 - Console.CursorLeft - 2;
        var note = _message.Length > 0 ? _message
            : tabs[tab] == Tab.Jumps ? Lang.T("Only jumps made in this menu, the latest first")
            : tabs[tab] == Tab.Bookmarks && rows.Count == 0 && filter.Length == 0 ? DialogText.BookmarksEmpty
            : tabs[tab] == Tab.Projects && rows.Count == 0 && filter.Length == 0 ? DialogText.NoProjectsYet
            : string.Empty;
        if (note.Length > 0 && room > 10)
        {
            if (_message.Length > 0)
            {
                // A message: highlighted, with a blank space on each side in the highlight
                Console.Write(" ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write(" " + Tools.TruncateString(note, room - 2, true) + " ");
            }
            else
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write("  " + Tools.TruncateString(note, room, true));
            }
        }

        // At the right end of the row (when there is room): that ":" gives the commands
        var commands = Lang.T(": Commands");
        var commandsColumn = left + width - 2 - TextMetrics.WidthOf(commands);
        if (Console.CursorLeft + 2 <= commandsColumn)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(top + 4, commandsColumn);
            Console.Write(commands);
        }

        // The columns, with lines between them and their names in the divider line: the name, the command that
        // jumps to a bookmark (Bookmarks) or when it was jumped to or opened (Recent, History), the directory the
        // place is in, and its kind. The name is as wide as the longest (at most half the box), so the command is
        // next to it, also in a wide window; the directory takes the room that is left
        var lastLabel = tabs[tab] == Tab.Bookmarks ? Lang.T("Command")
            : tabs[tab] is Tab.Jumps or Tab.History ? Lang.T("When")
            : string.Empty;
        var lasts = rows.Select(row => tabs[tab] == Tab.Bookmarks ? BookCommand(row) : When(row.Time)).ToList();
        var typeLabel = Lang.T("Type");
        var markWidth = Math.Max(MarkWidth, TextMetrics.WidthOf(typeLabel));
        // The numbers of the rows (when they are on for the list of the tab): right-aligned, as wide as the largest,
        // and a blank space; the pointer and the names follow
        var numbers = ListNumbers.Width(NumbersOn(tabs[tab]), rows.Count);
        var nameColumn = left + 4 + numbers;
        var nameWidth = Math.Clamp(rows.Count == 0 ? 0 : rows.Max(row => TextMetrics.WidthOf(RowName(row))),
            TextMetrics.WidthOf(DialogText.ColumnName) + 2, Math.Max(10, width / 2 - 8 - numbers));
        var nameLine = nameColumn + nameWidth + 1;
        var lastWidth = lastLabel.Length == 0 ? 0
            : Math.Max(TextMetrics.WidthOf(lastLabel) + 2, lasts.Count == 0 ? 0 : lasts.Max(TextMetrics.WidthOf));
        var lastLine = lastLabel.Length == 0 ? nameLine : nameLine + 2 + lastWidth + 1;
        // (two cells of room after the kind, so its name is not pressed against the edge of the menu)
        var typeColumn = left + width - markWidth - 3;
        var typeLine = typeColumn - 2;
        var pathColumn = lastLine + 2;
        var pathWidth = typeLine - 1 - pathColumn;
        // (no room for the directory: the kind follows at once)
        if (pathWidth < 8)
        {
            pathWidth = 0;
            typeLine = lastLine;
        }

        var lines = new List<int> { nameLine };
        if (lastLabel.Length > 0) lines.Add(lastLine);
        if (pathWidth > 0) lines.Add(typeLine);
        var labels = new List<(int, string)> { (nameColumn - 3, DialogText.ColumnName) };
        if (lastLabel.Length > 0) labels.Add((nameLine, lastLabel));
        if (pathWidth > 0) labels.Add((lastLine, DialogText.ColumnPath));
        labels.Add((typeLine, typeLabel));
        if (!partial)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(top + 5, left + 2);
            Console.Write(new string('-', Math.Max(0, width - 2)));
            if (rows.Count > 0) FileExplorer.DrawColumnLabels(top + 5, labels, lines);
        }

        var first = top + 6;
        if (rows.Count == 0 && !partial)
        {
            Tools.SetCursorEx(first, left + 4);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Tools.TruncateString(Lang.T("Nothing to show"), width - 5, true));
        }

        // The lines between the columns go down to the end of the list (to the panes, or the bottom of the box), also
        // past the last row
        if (rows.Count > 0 && !partial)
        {
            for (var line = rows.Count - scroll; line < visible; line++)
            {
                foreach (var column in lines) FileExplorer.DrawColumnLine(first + line, column);
            }
        }

        for (var line = 0; line < visible && scroll + line < rows.Count; line++)
        {
            var row = rows[scroll + line];
            var index = scroll + line;
            // Only the rows the selection moved from and to
            if (partial && index != selected && index != before!.Selected) continue;
            if (numbers > 0)
            {
                // The number of the row (as 12gg counts the rows)
                Tools.SetCursorEx(first + line, left + 1);
                ListNumbers.Write(index, numbers, index == selected);
            }

            Tools.SetCursorEx(first + line, left + 1 + numbers);
            if (index == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(index == selected ? (_moving ? "<>" : "->") : "  ");

            // A marked bookmark has a "+" in front
            if (row.Bookmark != null && Marks.Contains(row.Bookmark))
            {
                Tools.SetCursorEx(first + line, left + 3 + numbers);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write("+");
            }

            // The name, in the color of the bookmark or of a favorite (a place that is not there any more is dimmed
            // and marked). A folder is shown with its name and the number of bookmarks in it
            var exists = row.IsFolder || row.Target.Exists;
            var name = ShortName(RowName(row), nameWidth);
            Tools.SetCursorEx(first + line, nameColumn);
            // The bookmark that is moved is highlighted, from the blank space before it (as in the other lists)
            if (_moving && index == selected)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Tools.SetCursorEx(first + line, left + 3 + numbers);
                Console.Write(" ");
            }
            else if (row.IsFolder && row.Color == null) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
            else if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else if (row.Color is { } color) ConsoleColors.SetTextColor(color);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(name);

            // The lines between the columns, the command (or when), the directory and the kind (a folder of
            // bookmarks: only its name)
            foreach (var column in lines) FileExplorer.DrawColumnLine(first + line, column);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            if (lastLabel.Length > 0)
            {
                Tools.SetCursorEx(first + line, nameLine + 2);
                Console.Write(lasts[index]);
            }

            if (row.IsFolder) continue;
            if (pathWidth > 0 && Path.GetDirectoryName(row.Target.FullPath) is { Length: > 0 } directory)
            {
                Tools.SetCursorEx(first + line, pathColumn);
                Console.Write(TextMetrics.KeepEnd(directory, pathWidth));
            }

            Tools.SetCursorEx(first + line, typeLine + 2);
            Console.Write(Mark(row.Target));
        }

        if (paneRows > 0)
        {
            var entry = rows.Count > 0 ? rows[selected].Bookmark : null;
            NotePanes.Draw(first + visible, left + 2, width - 2, paneRows, lines, entry?.Note,
                BookmarkPeek ? entry == null ? new List<string>() : PeekOf(entry, paneRows) : null);
        }

        DrawPosition(top + height + 1, left, width, rows.Count == 0 ? 0 : selected + 1, rows.Count);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (partial) return visible;
        // With bookmarks marked: what can be done with them
        ChoiceBox.WriteHints(tabs[tab] != Tab.Bookmarks ? DialogText.JumpKeys
            : Marks.Count > 0 ? DialogText.FavoritesMarkedKeys
            : DialogText.JumpBookmarkKeys);
        return visible;
    }

    // The number of the selected place and how many there are (" 5/8 "), at the right of the bottom line of the box, as
    // below the lists of the file browser. The end of the line is written again first, so a shorter number leaves
    // nothing behind
    private static void DrawPosition(int row, int left, int width, int number, int count)
    {
        var position = " " + number + "/" + count + " ";
        var start = Math.Max(left + 1, left + width - 2 - TextMetrics.WidthOf(position) - 6);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
        Tools.SetCursorEx(row, start);
        Console.Write(new string('-', left + width + 1 - start));
        Tools.SetCursorEx(row, left + width - 1 - TextMetrics.WidthOf(position));
        Console.Write(position);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // A row of tabs (the names, the one shown highlighted) with the keys that change them at its end
    private static void DrawTabRow(int row, int left, int width, List<string> names, int shown, string keys)
    {
        var starts = TabLayout.SubTabStarts(names);
        for (var i = 0; i < names.Count; i++)
        {
            Tools.SetCursorEx(row, left + 2 + starts[i] - 1);
            if (i == shown) ConsoleColors.SetTabColors();
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.OtherTabs);
            Console.Write(" " + names[i] + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.TabFrame);
        Tools.SetCursorEx(row, left + width - keys.Length - 1);
        Console.Write(keys);
    }

    // The place of a bookmark or folder in the whole list
    private static int IndexOf(Bookmarks.Entry entry) => Bookmarks.Items.ToList().IndexOf(entry);

    // What the peek shows of a bookmark or folder: the top of a file (a bookmark made in the editor: from its line),
    // what is in a directory or a folder, or what a page of Blyant is
    private static List<string> PeekOf(Bookmarks.Entry entry, int rows)
    {
        if (entry.IsFolder)
        {
            return NotePanes.PeekFolder(Bookmarks.InFolder(IndexOf(entry)).Select(item =>
                new string(' ', 2 * (item.Level - entry.Level - 1)) +
                (item.IsFolder ? "[" + item.Name + "]"
                    : item.Shown + (item.Target.Kind == JumpKind.Directory ? "/" : ""))).ToList(), rows);
        }

        return entry.Target.Kind == JumpKind.Page
            ? new List<string> { Lang.T("A page of Blyant: Enter opens it") }
            : NotePanes.PeekPlace(entry.Target.FullPath, rows, entry.Target.At?.Line ?? 1);
    }

    // ":panes" and ":peek": the panes below the bookmarks, or the peek in them, on or off (kept in the settings)
    private static void ToggleBookmarkPanes(bool peek)
    {
        var panes = BookmarkPanes;
        var peeking = BookmarkPeek;
        _message = FileExplorer.TogglePanes(peek, ref panes, ref peeking);
        BookmarkPanes = panes;
        BookmarkPeek = peeking;
        FileIo.SaveSettings();
    }

    // N or ":note": the note of the selected bookmark or folder, written in the editor (kept in bookmarks.txt)
    private static void EditBookmarkNote(Row row)
    {
        if (row.Bookmark is not { } entry) return;
        var index = IndexOf(entry);
        var parent = Bookmarks.Parent(index);
        var parentName = parent < 0 ? string.Empty : Bookmarks.Items[parent].Name;
        var key = entry.IsFolder
            ? "[" + parentName + "]" + entry.Level + "[" + entry.Name + "]"
            : JumpHistory.KindName(entry.Target.Kind) + "\t" + entry.Target.Target;
        var error = FileExplorer.EditListNote(entry.Note, NotePanes.NoteFileName(BookmarkNoteFile, key),
            DialogText.CaptionsJump, written => KeepBookmarkNote(entry, index, parentName, written));
        if (error != null) _message = error;
        // (the editor covered the page: the menu is drawn on an empty page, as after the help)
        Tools.ClearScreen();
    }

    // The note written in the editor, given to its bookmark or folder and saved
    private static void KeepBookmarkNote(Bookmarks.Entry entry, int index, string parentName, string written)
    {
        // The list may have been read again while the editor was open (the Jump menu was opened in it)
        if (!Bookmarks.Items.Contains(entry))
        {
            var same = FindSameBookmark(entry, index, parentName);
            if (same == null)
            {
                _message = Lang.T("The bookmark is no longer in the list: the note was not kept");
                return;
            }

            entry = same;
        }

        var before = entry.Note;
        Bookmarks.SetNote(entry, written);
        if (entry.Note == before) return;
        SaveBookmarks();
        if (_message.Length == 0) _message = entry.Note.Length == 0 ? Lang.T("Note removed") : Lang.T("Note kept");
        _select = entry;
    }

    /// <summary>
    /// A bookmark or folder in the list as it was read again: the bookmark of the same place, or the folder of the
    /// same name and level in a folder of the same name. With more such folders, the one at the place it had (none if
    /// it is not there)
    /// </summary>
    internal static Bookmarks.Entry? FindSameBookmark(Bookmarks.Entry entry, int index, string parentName)
    {
        var items = Bookmarks.Items;
        if (!entry.IsFolder) return items.FirstOrDefault(item => !item.IsFolder && item.Target.SameAs(entry.Target));
        var found = new List<int>();
        for (var i = 0; i < items.Count; i++)
        {
            var parent = Bookmarks.Parent(i);
            if (items[i].IsFolder && items[i].Level == entry.Level && items[i].Name == entry.Name &&
                (parent < 0 ? string.Empty : items[parent].Name) == parentName)
            {
                found.Add(i);
            }
        }

        if (found.Count == 1) return items[found[0]];
        return found.Contains(index) ? items[index] : null;
    }

    /// <summary>
    /// The folder a new file made from the bookmarks starts in: the directory of the selected file, the selected
    /// directory, or for a folder the one of the first file or directory in it (null: none, a page of Blyant, or it is
    /// not there; the folder the file browser was in last is used)
    /// </summary>
    internal static string? FolderForNewFile(Bookmarks.Entry? entry)
    {
        if (entry is { IsFolder: true })
        {
            entry = Bookmarks.InFolder(IndexOf(entry))
                .FirstOrDefault(item => !item.IsFolder && item.Target.Kind != JumpKind.Page);
        }

        if (entry == null || entry.Target.Kind == JumpKind.Page) return null;
        var path = entry.Target.FullPath;
        if (Directory.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path);
        return File.Exists(path) && directory != null && Directory.Exists(directory) ? directory : null;
    }

    // W or ":new name": a new file, made with the box of the start page (the name, with ":new name" filled in, and Tab
    // for another folder), in the folder of the selected bookmark to start with. It is bookmarked below the selected
    // bookmark (first in a folder that is selected), and is the place to jump to (it is opened in the editor). Returns
    // it, or null
    private static JumpTarget? NewBookmarkFile(List<Row> rows, int selected, string name)
    {
        var entry = rows.Count > 0 ? rows[selected].Bookmark : null;
        ChoiceBox.Forget();
        var path = StartMenu.AskNewFile(DialogText.NewFile, name, FolderForNewFile(entry));
        ChoiceBox.Forget();
        if (path == null) return null;
        if (path.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0)
        {
            // (it could not be kept in bookmarks.txt)
            _message = Lang.T("A path with a tab or a line break is not kept");
            return null;
        }
        try
        {
            // Only a new file: one that was made in the meantime is not emptied (it is logged, or why it failed)
            FileIo.CreateNewFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _message = e.Message;
            return null;
        }

        var target = new JumpTarget(JumpKind.File, path);
        Bookmarks.AddBelow(target, entry == null ? -1 : IndexOf(entry));
        Marks.Clear();
        SaveBookmarks();
        return target;
    }

    // The bookmarks (and folders) shown, in their order, and if they are in a folder: a move is seen when one of them
    // changes place among them or goes into or out of a folder
    private static List<(Bookmarks.Entry Entry, int Level)> ShownOrder() => BookmarkRows(string.Empty)
        .Select(row => (row.Bookmark!, row.Bookmark!.Level)).ToList();

    // j k while moving: one place up or down among the rows shown, into and out of folders. Bookmarks that are not
    // shown (of a mode that is not used, or in a closed folder) are passed over
    // Returns false if it could not move (it is saved by the caller)
    private static bool MoveBookmark(Bookmarks.Entry entry, bool up)
    {
        // A folder moves closed (Move closes it): closed before the rows are compared, so its first step is seen. It
        // stays open if it cannot move
        var collapsed = entry.Collapsed;
        if (entry.IsFolder) entry.Collapsed = true;
        var before = ShownOrder();
        var moved = false;
        for (var step = 0; step < Bookmarks.Items.Count + 2; step++)
        {
            if (!Bookmarks.Move(IndexOf(entry), up)) break;
            moved = true;
            if (!ShownOrder().SequenceEqual(before)) break;
        }

        if (!moved) entry.Collapsed = collapsed;
        return moved;
    }

    // The marked bookmarks, or the selected one (not a folder)
    private static List<Bookmarks.Entry> MarkedOrSelected(List<Row> rows, int selected)
    {
        if (Marks.Count > 0) return Bookmarks.Items.Where(Marks.Contains).ToList();
        return rows.Count > 0 && rows[selected].Bookmark is { IsFolder: false } entry
            ? new List<Bookmarks.Entry> { entry }
            : new List<Bookmarks.Entry>();
    }

    // M or ":move": the marked bookmarks (or the selected one) to a folder picked from a list: one of the folders,
    // a new folder, or out of the folders
    private static void MoveBookmarksToFolder(List<Row> rows, int selected)
    {
        var items = MarkedOrSelected(rows, selected);
        if (items.Count == 0)
        {
            _message = DialogText.BookmarkMarkFolder;
            return;
        }

        var folders = Bookmarks.Items.Where(item => item.IsFolder).ToList();
        var choices = new List<string> { DialogText.FavoriteNewFolder + "...", DialogText.FavoriteNoFolder };
        // A subfolder below its folder, further in
        choices.AddRange(folders.Select(folder => new string(' ', 3 * folder.Level) + "[" + folder.Name + "]"));
        var caption = items.Count == 1 ? DialogText.FavoriteMoveOne : Lang.F("Move {0} bookmarks to folder", items.Count);
        ChoiceBox.Forget();
        var choice = FileExplorer.PickFromList(caption, choices, folders.Count > 0 ? 2 : 0);
        ChoiceBox.Forget();
        if (choice < 0) return;
        Bookmarks.Entry? folder = null;
        if (choice == 0)
        {
            // A new folder, where the first of them is
            ChoiceBox.Forget();
            var name = ChoiceBox.AskText(DialogText.FavoriteNewFolder, DialogText.TypeNameAndPressEnter,
                DialogText.NameColon, string.Empty);
            if (name == null) return;
            var index = Bookmarks.AddFolder(name, IndexOf(items[0]));
            if (index < 0)
            {
                _message = DialogText.FavoriteFolderName;
                return;
            }

            folder = Bookmarks.Items[index];
        }
        else if (choice >= 2)
        {
            folder = folders[choice - 2];
        }

        if (Bookmarks.MoveToFolder(items, folder) < 0) return;
        Marks.Clear();
        _select = folder ?? items[0];
        var where = folder == null ? DialogText.FavoriteNoFolder : folder.Name;
        _message = items.Count == 1 ? DialogText.FavoriteMovedOne + where
            : Lang.F("{0} bookmarks moved to: {1}", items.Count, where);
        SaveBookmarks();
    }

    // T E, ":top" ":bottom": the marked bookmarks (or the selected bookmark or folder) to the top or the end (a
    // bookmark in a folder: of its folder)
    private static void MoveBookmarksToEnd(List<Row> rows, int selected, bool top)
    {
        var items = MarkedOrSelected(rows, selected);
        if (items.Count == 0 && rows.Count > 0 && rows[selected].Bookmark is { } entry) items.Add(entry);
        if (items.Count == 0) return;
        Bookmarks.MoveToEnd(items, top);
        Marks.Clear();
        _select = items[0];
        _message = top ? Lang.T("Moved to the top") : Lang.T("Moved to the end");
        SaveBookmarks();
    }

    // A or ":folder name": a new folder in front of the selected bookmark (or its folder)
    private static void NewBookmarkFolder(List<Row> rows, int selected, string filter, string name)
    {
        if (filter.Length > 0)
        {
            _message = DialogText.BookmarkMoveFiltered;
            return;
        }

        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            var asked = ChoiceBox.AskText(DialogText.FavoriteNewFolder, DialogText.TypeNameAndPressEnter,
                DialogText.NameColon, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var before = rows.Count > 0 && rows[selected].Bookmark is { } entry ? IndexOf(entry) : -1;
        var index = Bookmarks.AddFolder(name, before);
        if (index < 0)
        {
            _message = DialogText.FavoriteFolderName;
            return;
        }

        _select = Bookmarks.Items[index];
        SaveBookmarks();
    }

    // S or ":subfolder name": a new subfolder in the folder of the selected bookmark (first in it when the folder is
    // selected, else in front of the bookmark or of its subfolder)
    private static void NewBookmarkSubfolder(List<Row> rows, int selected, string filter, string name)
    {
        if (filter.Length > 0)
        {
            _message = DialogText.BookmarkMoveFiltered;
            return;
        }

        if (rows.Count == 0 || rows[selected].Bookmark is not { } entry || entry is { IsFolder: false, Level: 0 })
        {
            _message = Lang.T("Select a folder (or a bookmark in one) to add a subfolder to it");
            return;
        }

        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            var asked = ChoiceBox.AskText(Lang.T("New subfolder"), DialogText.TypeNameAndPressEnter,
                DialogText.NameColon, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var index = Bookmarks.AddSubfolder(name, IndexOf(entry));
        if (index < 0)
        {
            _message = DialogText.FavoriteFolderName;
            return;
        }

        _select = Bookmarks.Items[index];
        SaveBookmarks();
    }

    // X or ":rename name": the name the bookmark is shown with (empty: the name of the place), or the name of a
    // folder
    private static void RenameBookmark(Row row, string name)
    {
        if (row.Bookmark is not { } entry) return;
        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            var asked = entry.IsFolder
                ? ChoiceBox.AskText(DialogText.FavoriteRenameFolder, DialogText.TypeNameAndPressEnter,
                    DialogText.NameColon, entry.Name)
                : ChoiceBox.AskText(DialogText.BookmarkRename, DialogText.BookmarkRenameNote, DialogText.NameColon,
                    entry.Shown);
            if (asked == null) return;
            name = asked;
        }

        if (!Bookmarks.Rename(IndexOf(entry), name))
        {
            // A folder with a name that cannot be used, or a bookmark with the name of another (or only digits)
            var given = name.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            _message = entry.IsFolder ? DialogText.FavoriteFolderName
                : Bookmarks.NameProblem(given, entry) ?? DialogText.FavoriteFolderName;
            return;
        }

        SaveBookmarks();
    }

    // C or ":color": the text color of the bookmark or folder (it is used before the color of a favorite)
    private static void PickBookmarkColor(Row row)
    {
        if (row.Bookmark is not { } entry) return;
        ChoiceBox.Forget();
        if (!ColorPicker.Pick(DialogText.ColorCaption, (Init.LineWidth - 26) / 2, true, entry.Color, null, null,
                out var color))
        {
            return;
        }

        Bookmarks.SetColor(IndexOf(entry), color);
        SaveBookmarks();
    }

    // D or ":remove": the marked bookmarks, or the selected bookmark, or a folder with its bookmarks (u puts them back)
    private static void RemoveBookmarks(List<Row> rows, int selected)
    {
        if (Marks.Count > 0)
        {
            var marked = Bookmarks.Items.Where(Marks.Contains).ToList();
            var notes = marked.Count(item => item.Note.Length > 0);
            ChoiceBox.Forget();
            if ((marked.Count > 1 || notes > 0) && !ChoiceBox.Confirm(Lang.T("Remove bookmarks"),
                    Lang.F("Remove the {0} marked bookmarks from the list?", marked.Count),
                    (notes > 0 ? Lang.T("Their notes are removed with them.") + " " : string.Empty) +
                    DialogText.BookmarksClearNote, true))
            {
                return;
            }

            var count = Bookmarks.RemoveMany(marked);
            Marks.Clear();
            _message = Lang.F("{0} removed from the bookmarks (u: Undo)", count);
            SaveBookmarks();
            return;
        }

        if (rows.Count == 0 || rows[selected].Bookmark is not { } entry) return;
        var index = IndexOf(entry);
        if (entry.IsFolder)
        {
            var count = Bookmarks.FolderCount(index);
            var subfolders = Bookmarks.SubfolderCount(index);
            ChoiceBox.Forget();
            if ((count > 0 || subfolders > 0 || entry.Note.Length > 0) && !ChoiceBox.Confirm(
                    DialogText.FavoriteDeleteFolder,
                    DialogText.DeleteNamed.Replace("{0}", Tools.TruncateString(entry.Name, 40, true)),
                    (entry.Note.Length > 0 ? Lang.T("Its note is removed with it.") + " " : string.Empty) +
                    (count == 0 && subfolders == 0 ? string.Empty
                    : subfolders > 0
                        ? Lang.F("In it: {0} bookmarks and {1} subfolders, removed from the list too (the places stay)",
                            count, subfolders)
                        : count == 1 ? Lang.T("The bookmark in it is removed from the list too (the place stays)")
                        : Lang.F("The {0} bookmarks in it are removed from the list too (the places stay)", count)),
                    true))
            {
                return;
            }

            Bookmarks.RemoveAt(index);
            _message = Lang.F("Removed the folder: {0}", entry.Name);
            SaveBookmarks();
            return;
        }

        if (entry.Note.Length > 0)
        {
            ChoiceBox.Forget();
            if (!ChoiceBox.Confirm(Lang.T("Remove bookmark"),
                    Lang.F("Remove \"{0}\" from the bookmarks?", Tools.TruncateString(entry.Shown, 40, true)),
                    Lang.T("Its note is removed with it."), false))
            {
                return;
            }
        }

        Bookmarks.RemoveAt(index);
        SaveBookmarks();
        _message = Lang.F("Removed from the bookmarks: {0} (u puts it back)", entry.Shown);
    }

    // u or ":undo": put back what was removed last
    private static void UndoBookmarkRemove()
    {
        var index = Bookmarks.UndoRemove();
        if (index < 0)
        {
            _message = DialogText.FavoriteNothingToUndo;
            return;
        }

        var entry = Bookmarks.Items[index];
        // Shown, also when it is in a closed folder
        Bookmarks.Reveal(index);
        _select = entry;
        SaveBookmarks();
        _message = Lang.F("Back in the bookmarks: {0}", entry.Shown);
    }

    // ":path" and ":name": the full path of the place, or the name of the file or directory itself, to the clipboard
    // as set and to the string memory (as in the file browser). A page of Blyant and a folder of bookmarks have
    // neither
    private static void CopyPathOrName(Row row, bool path)
    {
        var target = row.Target;
        if (row.IsFolder || target.Kind is not (JumpKind.File or JumpKind.Directory))
        {
            _message = Lang.T("A page of Blyant or a folder has no path or file name");
            return;
        }

        var full = target.FullPath;
        var text = path ? full : Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        if (text.Length == 0) text = full;
        _message = SystemClipboard.CopyWithMemory(text).Message;
    }

    // ":clearbook": empty the list, after asking
    private static void ClearBookmarks()
    {
        if (Bookmarks.Items.Count == 0) return;
        ChoiceBox.Forget();
        if (!ChoiceBox.Confirm(Lang.T("Bookmarks"),
                Lang.F("Remove all {0} bookmarks?", Bookmarks.Places.Count), DialogText.BookmarksClearNote, true))
        {
            return;
        }

        Bookmarks.Clear();
        Marks.Clear();
        SaveBookmarks();
        _message = DialogText.BookmarksCleared;
    }

    // ":import favorites": the favorites of the file browser copied into the bookmarks, after asking with what it
    // will do (the favorites stay; places that are bookmarks already are left out, folders of the same name merged)
    private static void ImportFavorites(string argument)
    {
        if (!argument.Trim().Equals("favorites", StringComparison.OrdinalIgnoreCase))
        {
            _message = Lang.T(":import favorites  copies the favorites of the file browser here");
            return;
        }

        FileFavorites.Load();
        var favorites = FileFavorites.Items;
        if (!favorites.Any(favorite => !favorite.IsFolder))
        {
            _message = Lang.T("There are no favorites to copy");
            return;
        }

        var plan = Bookmarks.ImportFavorites(favorites, false);
        if (plan.Added == 0 && plan.Notes == 0)
        {
            _message = Lang.T("All the favorites are bookmarks already");
            return;
        }

        ChoiceBox.Forget();
        // What will be done, a sentence for each that there is
        var sentences = new List<string>();
        if (plan.Added > 0) sentences.Add(Lang.F("{0} files and directories are added.", plan.Added));
        if (plan.NewFolders > 0) sentences.Add(Lang.F("New folders: {0}.", plan.NewFolders));
        if (plan.MergedFolders > 0) sentences.Add(Lang.F("Added to folders of the same name: {0}.", plan.MergedFolders));
        if (plan.Skipped > 0) sentences.Add(Lang.F("{0} are bookmarks already, and are left out.", plan.Skipped));
        if (plan.Notes > 0) sentences.Add(Lang.F("Notes copied: {0} (a note a bookmark has is kept).", plan.Notes));
        var note = string.Join(" ", sentences);
        if (!ChoiceBox.Confirm(Lang.T("Bookmarks"), Lang.T("Copy the favorites to the bookmarks?"), note, false)) return;
        var done = Bookmarks.ImportFavorites(favorites, true);
        Marks.Clear();
        SaveBookmarks();
        EventLog.Add(DialogText.LogFile + "Favorites copied to the bookmarks: " + done.Added + " added, " +
                     done.Skipped + " left out");
        if (_message.Length == 0) _message = Lang.F("{0} favorites copied to the bookmarks", done.Added);
    }

    // ":expand" ":collapse": open or close all the folders
    private static void SetBookmarkFolders(bool collapsed)
    {
        if (Bookmarks.SetAllCollapsed(collapsed) == 0)
        {
            _message = DialogText.FavoritesNoFolders;
            return;
        }

        _message = collapsed ? DialogText.FavoritesCollapsed : DialogText.FavoritesExpanded;
        SaveBookmarks();
    }

    // ":" in the Jump menu: a command. The commands on the bookmarks are run on the Bookmarks tab. Returns true if
    // the menu is closed
    private static bool RunBookmarkCommand(Tab current, List<Row> rows, int selected, string filter)
    {
        var command = CommandLine.Read(CommandContext.Jump);
        if (command == null) return false;
        // A name can follow the command
        if (CommandLine.Name(command) is "folder" or "subfolder" or "rename" or "import" or "new")
        {
            command = CommandLine.Name(command);
        }

        if (CommandLine.Name(command) == "book")
        {
            // ":book 3": jump to bookmark 3 (on every tab); ":book" alone: the bookmarks
            return JumpToBookmark(CommandLine.Argument(command));
        }
        if (command is "q" or "quit" or "close") return true;
        if (command is "h" or "help")
        {
            Tools.WriteEditorHelp(Tools.HelpTopic.Jump);
            Tools.ClearScreen();
            return false;
        }

        if (command is "info")
        {
            if (rows.Count > 0 && !rows[selected].IsFolder) ShowInfo(current, rows[selected]);
            return false;
        }

        if (command is "path" or "name")
        {
            // On every tab: the full path, or the name of the file or directory itself (not a name given to it)
            if (rows.Count > 0) CopyPathOrName(rows[selected], command == "path");
            return false;
        }

        if (!Commands.IsKnown(CommandContext.Jump, command))
        {
            CommandLine.ShowUnknown(command);
            return false;
        }

        if (command is "number" or "nonumber")
        {
            // The numbers of the rows of the list of the tab on or off (kept in the settings, and the same in the
            // file browser)
            if (SetNumbers(current, command == "number"))
            {
                _message = NumbersOn(current) ? DialogText.LineNumbersOn : DialogText.LineNumbersOff;
                FileIo.SaveSettings();
            }
            else
            {
                _message = Lang.T("Line numbers: Bookmarks, History, Favorites, Hotlist");
            }

            return false;
        }

        if (current != Tab.Bookmarks)
        {
            _message = Lang.T("This command is used on the Bookmarks tab");
            return false;
        }

        var row = rows.Count > 0 ? rows[selected] : null;
        switch (command)
        {
            case "folder":
                NewBookmarkFolder(rows, selected, filter, CommandLine.LastArgument);
                break;
            case "subfolder":
                NewBookmarkSubfolder(rows, selected, filter, CommandLine.LastArgument);
                break;
            case "new":
                _jumpTo = NewBookmarkFile(rows, selected, CommandLine.LastArgument);
                if (_jumpTo != null) return true;
                break;
            case "note":
                if (row != null) EditBookmarkNote(row);
                break;
            case "panes":
            case "peek":
                ToggleBookmarkPanes(command == "peek");
                break;
            case "rename":
                if (row != null) RenameBookmark(row, CommandLine.LastArgument);
                break;
            case "color":
                if (row != null) PickBookmarkColor(row);
                break;
            case "remove":
                RemoveBookmarks(rows, selected);
                break;
            case "move":
                MoveBookmarksToFolder(rows, selected);
                break;
            case "top":
            case "bottom":
                if (row != null) MoveBookmarksToEnd(rows, selected, command == "top");
                break;
            case "u":
            case "undo":
                UndoBookmarkRemove();
                break;
            case "expand":
            case "collapse":
                SetBookmarkFolders(command == "collapse");
                break;
            case "clearbook":
                ClearBookmarks();
                break;
            case "import":
                ImportFavorites(CommandLine.LastArgument);
                break;
        }

        // (the end of the commands)
        return false;
    }

    // ":book 3" in the menu: the bookmark is the place to jump to (true: the menu closes). ":book" alone shows the
    // Bookmarks tab; a number that is not a bookmark, or a place that is gone, is told
    private static bool JumpToBookmark(string argument)
    {
        argument = argument.Trim();
        if (argument.Length == 0)
        {
            _showBookmarks = true;
            return false;
        }

        var places = Bookmarks.Places;
        if (!int.TryParse(argument, out var number) || number < 1 || number > places.Count)
        {
            _message = places.Count == 0
                ? DialogText.BookmarksEmpty
                : Lang.F("No bookmark {0} (the bookmarks have 1 to {1})", argument, places.Count);
            return false;
        }

        var target = places[number - 1].Target;
        if (!target.Exists)
        {
            _message = DialogText.FileNotFound;
            return false;
        }

        _jumpTo = target;
        return true;
    }

    private static void SaveBookmarks()
    {
        var status = Bookmarks.Save();
        if (status != DialogText.Success) _message = status;
    }

    // Information about a place: its name, kind and path, whether it is there, and when it was jumped to or opened
    private static void ShowInfo(Tab tab, Row row)
    {
        var target = row.Target;
        var rows = new List<(string Label, string Value)>
        {
            (DialogText.InfoName, target.Name),
            (DialogText.InfoType, target.Kind switch
            {
                JumpKind.Directory => DialogText.InfoTypeDirectory,
                JumpKind.Page => Lang.T("Page of Blyant"),
                _ => Lang.T("File")
            })
        };
        if (target.Kind != JumpKind.Page)
        {
            rows.Add((DialogText.InfoPath, target.FullPath));
            rows.Add((Lang.T("Found:"), target.Exists ? DialogText.ChoiceYes : DialogText.ChoiceNo));
        }

        if (row.Time != DateTime.MinValue)
        {
            rows.Add((tab == Tab.Jumps ? Lang.T("Jumped to:") : Lang.T("Opened:"),
                row.Time.ToString("yyyy-MM-dd HH:mm")));
        }

        ChoiceBox.Forget();
        ChoiceBox.ShowRows(DialogText.CaptionsJumpInfo, rows);
    }
}
