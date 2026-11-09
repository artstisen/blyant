using System.Text;

namespace Blyant;

/// <summary>
/// The corkboard: files pinned as cards, and notes as cards, placed freely on a page (CorkboardPage) to plan a story
/// or keep the files worked on in view. Each card has a number that stays with it, a place (its column and row on the
/// board), a color and a title of its own if it is given one. A note card has a text instead of a file (its title is
/// its first line). A badge is a small round mark with an "i", and a note: it marks a place on the board (a group of
/// cards), and its note is read in the peek. A divider is a line from the top of the board to the bottom at a column,
/// to tile the board; a line points from card to card, and a frame marks off an area. They are drawn behind the
/// cards, and have no numbers of their own (inside they get numbers below 0 in their order, which are not kept). The
/// cards are drawn in their order, so a card later in the list covers the ones before it where they overlap.
///
/// There can be several boards: the main board is the file "board.txt" in the data directory, and the others are
/// files of the same kind in the directory "boards" next to it, with their names. A project can have a board of its
/// own: the file ".blyant-board.txt" in its folder, where the paths of what is in the project are kept from the
/// folder, so the board goes with the project when it is moved, copied or synced. A board is told by its key: its
/// name (empty: the main board), or the full path of the project (a name cannot have a separator in it). One board is
/// open at a time (setting "boardname"). The cards of all the boards follow a file that is renamed or moved.
///
/// A board is kept as a line for each card: its number, its place ("column,row"), its color (or nothing), its title
/// (or nothing) and the path of its file or folder (nothing for a note; a folder ends with a separator), with a tab
/// character between them; a badge has the word "badge" after them (a version without badges reads it as a note).
/// The text of a note is on the line after it, after a "#" (as the notes of the favorites). A divider is a line of
/// its own: "|", its column and its color (or nothing), with tabs between them. A line: "-", its start ("column,row"),
/// its color, the way it goes ("-", "\", "|" or "/"), its length and its arrowheads ("end", "start", "both" or
/// nothing). A frame: "+", its top left corner, its color, its size ("30x8") and its title. A version without them
/// skips these lines.
/// It does not touch the console, so it can be tested
/// </summary>
internal static class Corkboard
{
    internal const string FileName = "board.txt";

    /// <summary>
    /// The board of a project: a file in its folder (English: the name of a file)
    /// </summary>
    internal const string ProjectFileName = ".blyant-board.txt";

    /// <summary>
    /// The size of a card on the screen, with its frame
    /// </summary>
    internal const int CardWidth = 24;

    internal const int CardHeight = 3;

    /// <summary>
    /// The width of a badge (as high as a card)
    /// </summary>
    internal const int BadgeWidth = 5;

    // The word a badge is kept with, after the parts of its line (English: a word of the file)
    private const string BadgeWord = "badge";

    // The mark a divider's line starts with (English: a part of the file)
    private const string DividerMark = "|";

    // The marks the lines of a line and a frame start with (English: parts of the file)
    private const string LineMark = "-";
    private const string FrameMark = "+";

    /// <summary>
    /// The ways a line goes from its start, as it is turned (r): right, down to the right, down, down to the left. As
    /// they are written in the file of the board (English)
    /// </summary>
    internal static readonly char[] LineMarks = { '-', '\\', '|', '/' };

    /// <summary>
    /// The arrowheads of a line (a), as they are written in the file of the board (English: words of the file)
    /// </summary>
    internal static readonly string[] ArrowWords = { "", "end", "start", "both" };

    /// <summary>
    /// The smallest line, and frame
    /// </summary>
    internal const int ShortestLine = 2;

    internal const int SmallestFrameWidth = 6;
    internal const int SmallestFrameHeight = 3;

    /// <summary>
    /// The longest line, and the widest and highest frame, read from the file of a board (one written by hand or
    /// damaged cannot make a line or frame too large to draw)
    /// </summary>
    internal const int LargestShape = 1000;

    internal sealed class Card
    {
        internal int Number;
        internal int X; // The column of its left side on the board
        internal int Y; // The row of its top on the board
        internal ConsoleColor? Color;
        internal string Title = string.Empty; // A title of its own (empty: the name of its file, or a note's first line)
        internal string Path = string.Empty; // Empty: a note
        internal string Text = string.Empty; // The text of a note
        internal bool IsFolder; // The path is of a folder
        internal bool IsBadge; // A badge: a note shown as a small round mark (it has no title)
        internal bool IsDivider; // A divider: a line from top to bottom at its column (X), behind the cards
        internal bool IsLine; // A line from its start (X, Y), the way it is turned (Turn), Length long, behind the cards
        internal bool IsFrame; // A frame from its top left corner (X, Y), FrameWidth by FrameHeight, behind the cards
        internal int Turn; // The way a line goes: an index of LineMarks
        internal int Length = 6; // The characters of a line
        internal int Arrow; // The arrowheads of a line: an index of ArrowWords
        internal int FrameWidth = 30;
        internal int FrameHeight = 8;

        /// <summary>
        /// A divider, line or frame: drawn behind the cards, without a number of the cards (theirs are below 0)
        /// </summary>
        internal bool IsShape => IsDivider || IsLine || IsFrame;

        /// <summary>
        /// A card with a text of its own instead of a file (a badge too)
        /// </summary>
        internal bool IsNote => Path.Length == 0 && !IsShape;

        /// <summary>
        /// The step from one character of a line to the next, by its turn
        /// </summary>
        internal (int X, int Y) Step => Turn switch
        {
            0 => (1, 0),
            1 => (1, 1),
            2 => (0, 1),
            _ => (-1, 1)
        };

        /// <summary>
        /// The columns from X to the left side of what is drawn (a line turned down to the left goes left of its start)
        /// </summary>
        internal int Left => IsLine ? Math.Min(0, Step.X * (Length - 1)) : 0;

        /// <summary>
        /// Its height on the board (not used for a divider: it goes from the top of the board to the bottom)
        /// </summary>
        internal int Height => IsLine ? Step.Y * (Length - 1) + 1 : IsFrame ? FrameHeight : CardHeight;

        /// <summary>
        /// The card is of a file or folder
        /// </summary>
        internal bool HasPlace => Path.Length > 0;

        /// <summary>
        /// Its width on the board
        /// </summary>
        internal int Width => IsDivider ? 1
            : IsLine ? Math.Abs(Step.X) * (Length - 1) + 1
            : IsFrame ? FrameWidth
            : IsBadge ? BadgeWidth : CardWidth;

        /// <summary>
        /// What the card shows: its title, or else its own name
        /// </summary>
        internal string Name => Title.Length > 0 ? Title : OwnName;

        /// <summary>
        /// The name of the card without a title: the name of its file without the extension, of its folder with a
        /// "/" after it (as in the file browser), or the first line of its text (a tab in it as blank space, so the
        /// card keeps its frame)
        /// </summary>
        internal string OwnName => (IsDivider ? Lang.T("Divider")
            : IsLine ? Lang.T("Drawn line") // (not "Line": that is a line of text)
            : IsFrame ? Lang.T("Frame")
            : IsFolder ? FolderName(Path)
            : !IsNote ? System.IO.Path.GetFileNameWithoutExtension(Path)
            : Text.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ??
              (IsBadge ? Lang.T("Badge") : Lang.T("Note")))
            .Replace('\t', ' ');
    }

    // The name of a folder with "/" after it, or the root of a drive as it is written ("D:\\", "/")
    private static string FolderName(string path)
    {
        var name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(path));
        return name.Length > 0 ? name + "/" : path;
    }

    private static List<Card> _cards = new();

    /// <summary>
    /// The cards of the open board, in the order they are drawn (the last covers the others)
    /// </summary>
    internal static IReadOnlyList<Card> Cards => _cards;

    /// <summary>
    /// The file of the main board. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    private static string MainFilePath =>
        FilePathOverride ?? System.IO.Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    /// <summary>
    /// The boards besides the main board: files in the directory "boards"
    /// </summary>
    internal static readonly NamedLists Boards = new(() => MainFilePath, "boards");

    internal static string FilePath => BoardFile(CurrentBoard);

    /// <summary>
    /// A key that is the board of a project (its full path), not a name
    /// </summary>
    internal static bool IsProjectBoard(string key) => System.IO.Path.IsPathFullyQualified(key);

    /// <summary>
    /// The file of a board
    /// </summary>
    internal static string BoardFile(string key) =>
        IsProjectBoard(key) ? System.IO.Path.Combine(key, ProjectFileName) : Boards.PathOf(key);

    // The folder the paths of a board are kept from (a project), or null: full paths
    private static string? RootOf(string key) => IsProjectBoard(key) ? key : null;

    /// <summary>
    /// The board of a key as it is now (setting "boardname"): a board of that name, or a project folder that is
    /// there. Null if there is none
    /// </summary>
    internal static string? FindBoard(string key) =>
        IsProjectBoard(key) ? Directory.Exists(key) ? System.IO.Path.GetFullPath(key) : null : Boards.Find(key);

    /// <summary>
    /// The projects that have a board, by their full paths (those whose folder is there)
    /// </summary>
    internal static List<string> ProjectBoards() => WithBoardFiles(ProjectKeys());

    // The projects in the list of projects, by their full paths
    private static List<string> ProjectKeys()
    {
        var keys = new List<string>();
        foreach (var project in Projects.ListedPaths())
        {
            try
            {
                keys.Add(System.IO.Path.GetFullPath(project));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                // (a line that is not a path)
            }
        }

        return keys;
    }

    // The keys of projects whose board file is there
    private static List<string> WithBoardFiles(IEnumerable<string> keys) =>
        keys.Where(key => File.Exists(BoardFile(key))).Distinct().ToList();

    /// <summary>
    /// The name a board is shown with: its name, or the name of its project (empty: the main board; a project at the
    /// root of a drive: the root as it is written)
    /// </summary>
    internal static string NameOf(string key)
    {
        if (!IsProjectBoard(key)) return key;
        var name = System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(key));
        return name.Length > 0 ? name : key;
    }

    /// <summary>
    /// ":board name": a board by its name, or the board of a project by the name of its folder. A name written the
    /// same way comes first, of a board or a project, then upper and lower case alike. Null if there is none
    /// </summary>
    internal static string? FindByName(string name)
    {
        name = name.Trim();
        if (name.Equals(NamedLists.MainWord, StringComparison.OrdinalIgnoreCase)) return string.Empty;
        var names = Boards.Names();
        var projects = ProjectBoards();
        foreach (var comparison in new[] { StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase })
        {
            var found = names.FirstOrDefault(board => board.Equals(name, comparison)) ??
                        projects.FirstOrDefault(key => NameOf(key).Equals(name, comparison));
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// The words that can follow ":board": the boards, and the projects with boards
    /// </summary>
    internal static IEnumerable<string> BoardWords() =>
        Boards.Words().Concat(ProjectBoards().Select(NameOf)).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The key of the board that is open (setting "boardname"; empty: the main board). Its cards are read when it
    /// is set, so what is saved never goes to a board it was not read from
    /// </summary>
    internal static string CurrentBoard
    {
        get => _currentBoard;
        set
        {
            if (_currentBoard == value) return;
            _currentBoard = value;
            Load();
        }
    }

    private static string _currentBoard = string.Empty;

    // The file was there, but could not be read: it is not written over until it is read
    private static bool _unreadable;

    // What u puts back: the cards removed last (one, or those marked) and where they were in the order, the cards
    // taken off by ":clear", or the places of the cards before they were arranged
    private static List<(Card Card, int Index)>? _removed;
    private static List<Card>? _cleared;
    private static List<(Card Card, int X, int Y)>? _arranged;

    // The note made a file last (W, ":tofile"), with its text: u makes it a note again
    private static (Card Card, string Text)? _converted;

    /// <summary>
    /// The folder of the project of the open board, or null when it is not the board of a project
    /// </summary>
    internal static string? ProjectFolder => IsProjectBoard(_currentBoard) ? _currentBoard : null;

    /// <summary>
    /// The name a note is given as a file (W, ":tofile"), without an extension: its title, or else its first line
    /// that is not blank, without the marks of a Markdown heading in front, and without the characters a file name
    /// cannot have on any system. At most 60 characters; "Note" when nothing is left
    /// </summary>
    internal static string FileNameOf(Card card)
    {
        var name = card.Name.Trim().TrimStart('#', ' ', '\t').Trim();
        var kept = new string(name.Where(c => !char.IsControl(c) && "<>:\"/\\|?*".IndexOf(c) < 0).ToArray());
        kept = string.Join(' ', kept.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (kept.Length > 60) kept = kept[..60];
        kept = kept.Trim().TrimEnd('.', ' ');
        return kept.Length > 0 ? kept : "Note";
    }

    /// <summary>
    /// A note becomes the card of a file (its text was written to the file): it keeps its place, number, color and
    /// title. u makes it a note again, with its text (the file stays)
    /// </summary>
    internal static void ToFile(Card card, string path)
    {
        _converted = (card, card.Text);
        _removed = null;
        _cleared = null;
        _arranged = null;
        card.Path = path;
        card.Text = string.Empty;
    }

    /// <summary>
    /// Read the cards of the open board from its file
    /// </summary>
    internal static void Load()
    {
        _cards.Clear();
        _removed = null;
        _cleared = null;
        _arranged = null;
        _converted = null;
        _unreadable = false;
        // A board that is gone (deleted outside Blyant, or its project folder): the main board. A project without a
        // board yet has an empty one
        if (IsProjectBoard(_currentBoard) ? !Directory.Exists(_currentBoard)
                : _currentBoard.Length > 0 && !File.Exists(FilePath))
        {
            _currentBoard = string.Empty;
        }

        try
        {
            if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8), RootOf(_currentBoard));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _unreadable = true;
        }
    }

    /// <summary>
    /// Read the cards again if their file was changed (by another page while this one was left): what u puts back is
    /// kept when it was not
    /// </summary>
    internal static void Reload()
    {
        try
        {
            var text = File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : string.Empty;
            if (!_unreadable && text.Replace("\r", string.Empty) == Serialize(RootOf(_currentBoard))) return;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Read below, as it can be
        }

        Load();
    }

    /// <summary>
    /// Open a board
    /// </summary>
    /// <param name="key">Its name (empty: the main board), or the full path of a project</param>
    internal static void UseBoard(string key)
    {
        _currentBoard = key;
        Load();
    }

    /// <summary>
    /// Read cards from the text of a board
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="root">The folder of a project, the paths of what is in it are kept from (null: none)</param>
    internal static void Parse(string text, string? root = null)
    {
        _cards.Clear();
        Card? last = null; // The card a note is of
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (Notes.IsNote(line.TrimStart(' '), out var note))
            {
                if (last is { IsNote: true }) last.Text = note;
                continue;
            }

            last = null;
            if (line.StartsWith(DividerMark + "\t", StringComparison.Ordinal))
            {
                ReadDivider(line);
                continue;
            }

            if (line.StartsWith(LineMark + "\t", StringComparison.Ordinal) ||
                line.StartsWith(FrameMark + "\t", StringComparison.Ordinal))
            {
                ReadShape(line);
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 5 || !int.TryParse(parts[0], out var number) || number < 1) continue;
            var place = parts[1].Split(',');
            if (place.Length != 2 || !int.TryParse(place[0], out var x) || !int.TryParse(place[1], out var y)) continue;
            if (_cards.Any(card => card.Number == number)) continue;
            ConsoleColor? color = Enum.TryParse<ConsoleColor>(parts[2].Trim(), true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : null;
            var (path, isFolder) = ReadPath(parts[4], root);
            var isBadge = path.Length == 0 && !isFolder && parts.Length > 5 &&
                          parts[5].Trim().Equals(BadgeWord, StringComparison.OrdinalIgnoreCase);
            last = new Card
            {
                Number = number, X = Math.Max(0, x), Y = Math.Max(0, y), Color = color,
                Title = isBadge ? string.Empty : parts[3].Trim(), Path = path, IsFolder = isFolder, IsBadge = isBadge
            };
            _cards.Add(last);
        }
    }

    // A divider's line: "|", its column, and its color (or nothing)
    private static void ReadDivider(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var column)) return;
        ConsoleColor? color = parts.Length > 2 && Enum.TryParse<ConsoleColor>(parts[2].Trim(), true, out var parsed) &&
                              Enum.IsDefined(parsed)
            ? parsed
            : null;
        _cards.Add(new Card { Number = FreeShapeNumber(), X = Math.Max(0, column), Color = color, IsDivider = true });
    }

    // A line's line: "-", its start, its color, the way it goes (a mark of LineMarks), its length and its arrowheads
    // (a word of ArrowWords). A frame's: "+", its top left corner, its color, its size ("30x8") and its title
    private static void ReadShape(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 4) return;
        var place = parts[1].Split(',');
        if (place.Length != 2 || !int.TryParse(place[0], out var x) || !int.TryParse(place[1], out var y)) return;
        ConsoleColor? color = Enum.TryParse<ConsoleColor>(parts[2].Trim(), true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
        var card = new Card { Number = FreeShapeNumber(), X = Math.Max(0, x), Y = Math.Max(0, y), Color = color };
        if (parts[0] == LineMark)
        {
            var turn = Array.IndexOf(LineMarks, parts[3].Trim().FirstOrDefault());
            if (turn < 0 || parts.Length < 5 || !int.TryParse(parts[4], out var length)) return;
            card.IsLine = true;
            card.Turn = turn;
            card.Length = Math.Clamp(length, ShortestLine, LargestShape);
            card.Arrow = parts.Length > 5 ? Math.Max(0, Array.IndexOf(ArrowWords, parts[5].Trim().ToLowerInvariant())) : 0;
            // (a line to the left of its start is kept from where it is: X is its start)
            card.X = x;
        }
        else
        {
            var size = parts[3].Split('x');
            if (size.Length != 2 || !int.TryParse(size[0], out var frameWidth) || !int.TryParse(size[1], out var frameHeight))
            {
                return;
            }

            card.IsFrame = true;
            card.FrameWidth = Math.Clamp(frameWidth, SmallestFrameWidth, LargestShape);
            card.FrameHeight = Math.Clamp(frameHeight, SmallestFrameHeight, LargestShape);
            card.Title = parts.Length > 4 ? parts[4].Trim() : string.Empty;
        }

        _cards.Add(card);
    }

    // A path as it is kept: a folder ends with a separator, and in a project, what is in it is kept from its folder
    // with "/" (the same on all systems). It is read back as a full path
    private static (string Path, bool IsFolder) ReadPath(string written, string? root)
    {
        var trimmed = written.TrimEnd();
        var isFolder = trimmed.EndsWith('/') || (OperatingSystem.IsWindows() && trimmed.EndsWith('\\'));
        // (the separator after a folder; the root of a drive is written with its own: "D:\\", "/")
        if (isFolder) written = System.IO.Path.GetPathRoot(trimmed) == trimmed ? trimmed : trimmed[..^1];
        if (written.Trim().Length == 0) return (isFolder ? root ?? string.Empty : string.Empty, isFolder);
        if (root != null && !System.IO.Path.IsPathRooted(written.Trim()))
        {
            try
            {
                // ("\\" is a character of a name on Linux and macOS: Blyant writes "/" between the parts)
                written = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, written));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                // Kept as it is (a card of a file that is not there)
            }
        }

        return (FileOperations.ListedPath(written), isFolder);
    }

    private static string WritePath(Card card, string? root)
    {
        if (card.IsNote) return string.Empty;
        var path = card.Path;
        var relative = root != null && (FileOperations.IsInside(path, root) || FileOperations.SamePath(path, root));
        if (relative)
        {
            path = System.IO.Path.GetRelativePath(root!, path);
            if (OperatingSystem.IsWindows()) path = path.Replace('\\', '/');
        }

        if (!card.IsFolder || (!relative && System.IO.Path.GetPathRoot(path) == path)) return path;
        return path + (relative ? '/' : System.IO.Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// The text of the board that is read, as it is kept
    /// </summary>
    /// <param name="root">The folder of a project, the paths of what is in it are kept from (null: full paths)</param>
    internal static string Serialize(string? root = null)
    {
        var text = new StringBuilder();
        foreach (var card in _cards)
        {
            if (card.IsDivider)
            {
                text.Append(DividerMark).Append('\t').Append(card.X).Append('\t')
                    .Append(card.Color?.ToString() ?? string.Empty).Append('\n');
                continue;
            }

            if (card.IsLine || card.IsFrame)
            {
                text.Append(card.IsLine ? LineMark : FrameMark).Append('\t').Append(card.X).Append(',').Append(card.Y)
                    .Append('\t').Append(card.Color?.ToString() ?? string.Empty).Append('\t');
                if (card.IsLine)
                {
                    text.Append(LineMarks[card.Turn]).Append('\t').Append(card.Length).Append('\t')
                        .Append(ArrowWords[card.Arrow]);
                }
                else
                {
                    text.Append(card.FrameWidth).Append('x').Append(card.FrameHeight).Append('\t').Append(card.Title);
                }

                text.Append('\n');
                continue;
            }

            text.Append(card.Number).Append('\t').Append(card.X).Append(',').Append(card.Y).Append('\t')
                .Append(card.Color?.ToString() ?? string.Empty).Append('\t').Append(card.Title).Append('\t')
                .Append(WritePath(card, root)).Append(card.IsBadge ? "\t" + BadgeWord : string.Empty).Append('\n');
            if (card.IsNote && card.Text.Length > 0) text.Append(Notes.Line(0, card.Text));
        }

        return text.ToString();
    }

    /// <summary>
    /// Write the cards of the open board to its file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        NumberShapes();
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            // (the folder of a project is not made again: it was moved or deleted)
            if (IsProjectBoard(_currentBoard))
            {
                if (!Directory.Exists(_currentBoard)) return DialogText.ProjectMissing;
            }
            else if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(FilePath)) is { } directory)
            {
                FileIo.CreateDirectory(directory);
            }

            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize(RootOf(_currentBoard))));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    // Do something with a board in the place of the one that is open, which stays as it is. A board that cannot be
    // read is left alone
    private static void WithBoard(string key, Action action)
    {
        var open = _cards;
        _cards = new List<Card>();
        try
        {
            if (File.Exists(BoardFile(key))) Parse(File.ReadAllText(BoardFile(key), Encoding.UTF8), RootOf(key));
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EventLog.Add(DialogText.LogFile + "Corkboard: " + BoardFile(key) + " - FAILED: " + e.Message);
        }
        finally
        {
            _cards = open;
        }
    }

    /// <summary>
    /// Change a card on a board, the open one or another (as the cards may have been read again since the card was
    /// taken: a note written in the editor, where the board can be changed). The change is given the card with the
    /// number, and returns true if it changed it; then the board is saved
    /// </summary>
    /// <returns>DialogText.Success, null if the card is not there, empty if nothing was changed, or why it could not be
    /// saved</returns>
    internal static string? ChangeCard(string board, int number, Func<Card, bool> change)
    {
        if (board == _currentBoard)
        {
            if (Numbered(number) is not { } card) return null;
            return change(card) ? Save() : string.Empty;
        }

        string? result = null;
        WithBoard(board, () =>
        {
            if (Numbered(number) is not { } card) return;
            if (!change(card))
            {
                result = string.Empty;
                return;
            }

            try
            {
                WriteBoard(board);
                result = DialogText.Success;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                result = e.Message;
            }
        });
        return result;
    }

    // Write the cards read (WithBoard) to the file of a board
    private static void WriteBoard(string key)
    {
        FileIo.WriteAllBytesSafe(BoardFile(key), new UTF8Encoding(false).GetBytes(Serialize(RootOf(key))));
    }

    /// <summary>
    /// What is on a board, a line for each card, to show it in the box of boards
    /// </summary>
    internal static string BoardPreview(string key)
    {
        var lines = new List<string>();
        WithBoard(key, () => lines.AddRange(_cards.Where(card => !card.IsShape).OrderBy(card => card.Number)
            .Select(card => card.Number + "  " + card.Name)));
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The cards of a board by their numbers (to show them beside the list of projects), or null if it has no file
    /// </summary>
    internal static List<Card>? Snapshot(string key)
    {
        if (!File.Exists(BoardFile(key))) return null;
        var cards = new List<Card>();
        // (the dividers, lines and frames are about the board: they are not laid on the cards)
        WithBoard(key, () => cards.AddRange(_cards.Where(card => !card.IsShape).OrderBy(card => card.Number)));
        return cards;
    }

    /// <summary>
    /// Make a new, empty board
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal static string NewBoard(string name) => Boards.New(name);

    /// <summary>
    /// Give a board another name. The open board stays open
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal static string RenameBoard(string name, string newName)
    {
        var result = Boards.Rename(name, newName);
        if (result == DialogText.Success && _currentBoard == name) _currentBoard = newName.Trim();
        return result;
    }

    /// <summary>
    /// Delete a board: to the trash, unless that is turned off. The files of its cards stay where they are. If it
    /// is open, the main board is opened
    /// </summary>
    /// <returns>DialogText.Success or a message</returns>
    internal static string DeleteBoard(string key, out string trashedPath)
    {
        trashedPath = string.Empty;
        // (the board of a project without cards yet has no file)
        var result = !IsProjectBoard(key) ? Boards.Delete(key, out trashedPath)
            : File.Exists(BoardFile(key)) ? NamedLists.DeleteFile(BoardFile(key), out trashedPath)
            : DialogText.Success;
        if (result == DialogText.Success && _currentBoard == key) UseBoard(string.Empty);
        return result;
    }

    /// <summary>
    /// The card of a file or folder, or null if it is not pinned
    /// </summary>
    internal static Card? CardOf(string path) =>
        _cards.FirstOrDefault(card => card.HasPlace && FileOperations.SamePath(card.Path, path));

    /// <summary>
    /// The card with a number, or null
    /// </summary>
    internal static Card? Numbered(int number) => _cards.FirstOrDefault(card => card.Number == number);

    // The lowest number no card has
    private static int FreeNumber()
    {
        var number = 1;
        while (_cards.Any(card => card.Number == number)) number++;
        return number;
    }

    /// <summary>
    /// Number the dividers, lines and frames in their order (-1, -2 ...), as they are numbered when the board is read:
    /// a new one is put first, so it would else have another number after the board is read again (and the selection
    /// or a mark kept by its number would go to another one)
    /// </summary>
    internal static void NumberShapes()
    {
        var number = -1;
        foreach (var card in _cards.Where(card => card.IsShape)) card.Number = number--;
    }

    // The number a divider, line or frame gets inside (below 0, so it is not a card's number, and not kept)
    private static int FreeShapeNumber()
    {
        var number = -1;
        while (_cards.Any(card => card.Number == number)) number--;
        return number;
    }

    /// <summary>
    /// A new divider at a column of a board of a width, below the cards
    /// </summary>
    internal static Card AddDivider(int column, int width)
    {
        var card = new Card
        {
            Number = FreeShapeNumber(), X = Math.Clamp(column, 0, Math.Max(0, width - 1)), IsDivider = true
        };
        _cards.Insert(0, card);
        return card;
    }

    /// <summary>
    /// A new line from a place, to the right with an arrowhead at its end, on a board of a size, below the cards
    /// </summary>
    internal static Card AddLine(int x, int y, int width, int height)
    {
        var card = new Card { Number = FreeShapeNumber(), X = x, Y = y, IsLine = true, Length = 6, Arrow = 1 };
        card.Length = Math.Clamp(card.Length, ShortestLine, Math.Max(ShortestLine, width - Math.Max(0, x)));
        (card.X, card.Y) = Shown(card, width, height);
        _cards.Insert(0, card);
        return card;
    }

    /// <summary>
    /// A new frame at a place, of a size, on a board of a size, below the cards
    /// </summary>
    internal static Card AddFrame(int x, int y, int frameWidth, int frameHeight, int width, int height)
    {
        var card = new Card
        {
            Number = FreeShapeNumber(), X = x, Y = y, IsFrame = true,
            FrameWidth = Math.Clamp(frameWidth, SmallestFrameWidth, Math.Max(SmallestFrameWidth, width)),
            FrameHeight = Math.Clamp(frameHeight, SmallestFrameHeight, Math.Max(SmallestFrameHeight, height))
        };
        (card.X, card.Y) = Shown(card, width, height);
        _cards.Insert(0, card);
        return card;
    }

    /// <summary>
    /// The place and size of a frame around cards (a column of room at their sides and a row above and below them),
    /// inside a board of a size. Null if there are no cards (dividers, lines and frames are not framed)
    /// </summary>
    internal static (int X, int Y, int Width, int Height)? FrameAround(IEnumerable<Card> cards, int width, int height)
    {
        var placed = cards.Where(card => !card.IsShape).Select(card => (Card: card, Place: Shown(card, width, height)))
            .ToList();
        if (placed.Count == 0) return null;
        var left = Math.Max(0, placed.Min(pair => pair.Place.X) - 2);
        var top = Math.Max(0, placed.Min(pair => pair.Place.Y) - 1);
        var right = Math.Min(width - 1, placed.Max(pair => pair.Place.X + pair.Card.Width) + 1);
        var bottom = Math.Min(height - 1, placed.Max(pair => pair.Place.Y + CardHeight));
        return (left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// Turn a line around its start (r: one step, -1: back), on a board of a size: it is shortened to fit, and moved
    /// in where it would go off the board
    /// </summary>
    internal static void TurnLine(Card line, int steps, int width, int height)
    {
        if (!line.IsLine) return;
        line.Turn = ((line.Turn + steps) % LineMarks.Length + LineMarks.Length) % LineMarks.Length;
        Lengthen(line, 0, width, height);
    }

    /// <summary>
    /// Make a line longer or shorter (+ -), on a board of a size: at least ShortestLine, and as long as fits on it
    /// </summary>
    internal static void Lengthen(Card line, int by, int width, int height)
    {
        if (!line.IsLine) return;
        var longest = line.Turn switch
        {
            0 => width,
            2 => height,
            _ => Math.Min(width, height)
        };
        line.Length = Math.Clamp(line.Length + by, ShortestLine, Math.Max(ShortestLine, longest));
        (line.X, line.Y) = Shown(line, width, height);
    }

    /// <summary>
    /// The next arrowheads of a line (a): none, at its end, at its start, at both ends
    /// </summary>
    internal static void NextArrow(Card line)
    {
        if (line.IsLine) line.Arrow = (line.Arrow + 1) % ArrowWords.Length;
    }

    /// <summary>
    /// Make a frame wider or narrower, and higher or lower (its top left corner stays), inside a board of a size
    /// </summary>
    internal static void Resize(Card frame, int columns, int rows, int width, int height)
    {
        if (!frame.IsFrame) return;
        var (x, y) = Shown(frame, width, height);
        frame.FrameWidth = Math.Clamp(frame.FrameWidth + columns, SmallestFrameWidth, Math.Max(SmallestFrameWidth, width - x));
        frame.FrameHeight = Math.Clamp(frame.FrameHeight + rows, SmallestFrameHeight,
            Math.Max(SmallestFrameHeight, height - y));
        (frame.X, frame.Y) = Shown(frame, width, height);
    }

    /// <summary>
    /// Pin a file or folder: a new card at the first free place on a board of a size, on top of the others
    /// </summary>
    internal static Card Add(string path, int width, int height, bool isFolder = false)
    {
        var (x, y) = FreePlace(width, height);
        var card = new Card
        {
            Number = FreeNumber(), X = x, Y = y, Path = System.IO.Path.GetFullPath(path), IsFolder = isFolder
        };
        _cards.Add(card);
        return card;
    }

    /// <summary>
    /// A new note card, with a text, at the first free place on a board of a size, on top of the others
    /// </summary>
    internal static Card AddNote(string text, int width, int height)
    {
        var (x, y) = FreePlace(width, height);
        var card = new Card { Number = FreeNumber(), X = x, Y = y, Text = Notes.Clean(text) };
        _cards.Add(card);
        return card;
    }

    /// <summary>
    /// A new badge, with a note, at the first free place on a board of a size, on top of the others
    /// </summary>
    internal static Card AddBadge(string text, int width, int height)
    {
        var (x, y) = FreePlace(width, height, BadgeWidth);
        var card = new Card { Number = FreeNumber(), X = x, Y = y, Text = Notes.Clean(text), IsBadge = true };
        _cards.Add(card);
        return card;
    }

    /// <summary>
    /// A place for a new card of a width (a card's, or a badge's): the first one, row by row, where it covers no other
    /// card (with a column between cards), or else a step from the last card. Cards are placed in columns of their
    /// width, a badge in the first column free (right after a card)
    /// </summary>
    internal static (int X, int Y) FreePlace(int width, int height, int cardWidth = CardWidth)
    {
        var step = cardWidth == CardWidth ? CardWidth + 1 : 1;
        for (var y = 0; y + CardHeight <= Math.Max(CardHeight, height); y += CardHeight)
        {
            for (var x = 0; x + cardWidth <= Math.Max(cardWidth, width); x += step)
            {
                if (!_cards.Any(card => !card.IsShape && Overlap(card, x, y, cardWidth))) return (x, y);
            }
        }

        // The board is full: on top of the others, a step from the last one (a new card can be laid over a divider,
        // a line or a frame)
        var last = _cards.LastOrDefault(card => !card.IsShape);
        return last == null
            ? (0, 0)
            : (Math.Min(last.X + 2, Math.Max(0, width - cardWidth)), Math.Min(last.Y + 1, Math.Max(0, height - CardHeight)));
    }

    private static bool Overlap(Card card, int x, int y, int width) =>
        x < card.X + card.Width + 1 && card.X < x + width + 1 && y < card.Y + CardHeight && card.Y < y + CardHeight;

    /// <summary>
    /// Where a card is shown on a board of a size: inside it (a card placed on a larger window is drawn at the edge
    /// of a smaller one, and keeps its place)
    /// </summary>
    internal static (int X, int Y) Shown(Card card, int width, int height) =>
        (Math.Clamp(card.X, -card.Left, Math.Max(-card.Left, width - card.Width - card.Left)),
            card.IsDivider ? 0 : Math.Clamp(card.Y, 0, Math.Max(0, height - card.Height)));

    /// <summary>
    /// Move several cards together by columns and rows, inside a board of a size: they keep their places to each
    /// other, so at the edge of the board they all stop. A divider only goes left and right
    /// </summary>
    internal static void Move(IReadOnlyCollection<Card> cards, int columns, int rows, int width, int height)
    {
        if (cards.Count == 0) return;
        foreach (var card in cards) (card.X, card.Y) = Shown(card, width, height);
        columns = Math.Clamp(columns, -cards.Min(card => card.X + card.Left),
            Math.Max(0, cards.Min(card => width - card.Width - card.Left - card.X)));
        var placed = cards.Where(card => !card.IsDivider).ToList();
        rows = placed.Count == 0
            ? 0
            : Math.Clamp(rows, -placed.Min(card => card.Y),
                Math.Max(0, placed.Min(card => height - card.Height - card.Y)));
        foreach (var card in cards)
        {
            card.X += columns;
            if (!card.IsDivider) card.Y += rows;
        }
    }

    /// <summary>
    /// Move a card by columns and rows, inside a board of a size
    /// </summary>
    internal static void Move(Card card, int columns, int rows, int width, int height) =>
        Move(new[] { card }, columns, rows, width, height);

    /// <summary>
    /// Put cards on top of the others (true), or below them, in the order they had among themselves
    /// </summary>
    internal static void ToFront(IReadOnlyCollection<Card> cards, bool front)
    {
        var moving = _cards.Where(cards.Contains).ToList();
        _cards.RemoveAll(moving.Contains);
        if (front) _cards.AddRange(moving);
        else _cards.InsertRange(0, moving);
    }

    /// <summary>
    /// Take a card off the board (its file stays). It can be put back with Undo
    /// </summary>
    internal static void Remove(Card card) => Remove(new[] { card });

    /// <summary>
    /// Take several cards off the board (their files stay). Undo puts them all back, where they were
    /// </summary>
    /// <returns>How many were taken off</returns>
    internal static int Remove(IReadOnlyCollection<Card> cards)
    {
        var removed = _cards.Select((card, index) => (Card: card, Index: index))
            .Where(pair => cards.Contains(pair.Card)).ToList();
        if (removed.Count == 0) return 0;
        _cards.RemoveAll(card => cards.Contains(card));
        _removed = removed;
        _cleared = null;
        _arranged = null;
        _converted = null;
        return removed.Count;
    }

    // Cards put back that have the number of a card added since get a new number (the lowest free one; a divider's,
    // line's or frame's below 0)
    private static void Renumber(IEnumerable<Card> putBack, List<Card> taken)
    {
        foreach (var card in putBack.Where(card => taken.Any(other => other.Number == card.Number)))
        {
            var step = card.IsShape ? -1 : 1;
            card.Number = step;
            while (_cards.Any(other => other != card && other.Number == card.Number)) card.Number += step;
        }
    }

    /// <summary>
    /// ":clear": take all the cards off the board (their files stay). They can be put back with Undo
    /// </summary>
    /// <returns>How many were taken off</returns>
    internal static int Clear()
    {
        if (_cards.Count == 0) return 0;
        _cleared = _cards.ToList();
        _removed = null;
        _arranged = null;
        _converted = null;
        _cards.Clear();
        return _cleared.Count;
    }

    /// <summary>
    /// Take a card off the board that is not to be put back (a new note left empty)
    /// </summary>
    /// <returns>False if it was not on the board</returns>
    internal static bool Discard(Card card) => _cards.Remove(card);

    /// <summary>
    /// Take several cards off the board (":prune"). They cannot be put back
    /// </summary>
    internal static int RemoveMany(IEnumerable<Card> cards)
    {
        var gone = cards.ToHashSet();
        _removed = null;
        _cleared = null;
        _arranged = null;
        _converted = null;
        return _cards.RemoveAll(gone.Contains);
    }

    /// <summary>
    /// The ways the cards can be arranged (":arrange number", English: as the words of commands)
    /// </summary>
    internal static readonly string[] Orders = { "number", "name", "color" };

    /// <summary>
    /// Arrange the cards in rows from the top left, in an order: by number, by name (upper and lower case alike,
    /// numbers by their value: "Chapter 2" before "Chapter 10"), or by color (in the order of the colors, the cards
    /// without one last; by number within a color). The badges, dividers, lines and frames stay where they are:
    /// they mark places. The order they are drawn in stays. It can be undone with Undo
    /// </summary>
    /// <returns>False if the order is not known</returns>
    internal static bool Arrange(string order, int width, int height)
    {
        var cards = _cards.Where(card => !card.IsBadge && !card.IsShape).ToList();
        IEnumerable<Card> sorted = order switch
        {
            "number" => cards.OrderBy(card => card.Number),
            "name" => cards.OrderBy(card => card.Name, NaturalOrder.Instance).ThenBy(card => card.Number),
            "color" => cards.OrderBy(card => card.Color == null ? int.MaxValue : (int)card.Color.Value)
                .ThenBy(card => card.Number),
            _ => Array.Empty<Card>()
        };
        if (!Orders.Contains(order)) return false;
        _arranged = cards.Select(card => (card, card.X, card.Y)).ToList();
        _converted = null;
        _removed = null;
        _cleared = null;
        var perRow = Math.Max(1, (Math.Max(CardWidth, width) + 1) / (CardWidth + 1));
        var rows = Math.Max(1, Math.Max(CardHeight, height) / CardHeight);
        var i = 0;
        foreach (var card in sorted.ToList())
        {
            // (more than the board holds: on from the top left again, a step aside)
            var round = i / (perRow * rows);
            card.X = Math.Min(i % perRow * (CardWidth + 1) + round * 2, Math.Max(0, width - CardWidth));
            card.Y = Math.Min(i / perRow % rows * CardHeight + round, Math.Max(0, height - CardHeight));
            i++;
        }

        return true;
    }

    /// <summary>
    /// What Undo put back
    /// </summary>
    internal enum Undone
    {
        Nothing,
        Card, // The card removed last (UndoneCard)
        Cards, // The cards removed together, or taken off by ":clear" (as many as UndoneCount)
        Places, // The places of the cards before they were arranged
        Note // The card of a file made from a note is a note again (UndoneCard)
    }

    /// <summary>
    /// The card Undo put back
    /// </summary>
    internal static Card? UndoneCard { get; private set; }

    /// <summary>
    /// How many cards Undo put back
    /// </summary>
    internal static int UndoneCount { get; private set; }

    /// <summary>
    /// Put back what was done last: the cards removed (one or several), where they were, the cards taken off by
    /// ":clear" (with those added since on top of them), or the places of the cards before they were arranged. A card
    /// added since keeps its number: one put back that had it gets a new one
    /// </summary>
    internal static Undone Undo()
    {
        UndoneCard = null;
        UndoneCount = 0;
        if (_converted is { } done)
        {
            var (converted, text) = done;
            _converted = null;
            if (!_cards.Contains(converted)) return Undone.Nothing;
            converted.Path = string.Empty;
            converted.IsFolder = false;
            converted.Text = text;
            UndoneCard = converted;
            return Undone.Note;
        }

        if (_cleared is { } cleared)
        {
            var taken = _cards.ToList();
            _cards.InsertRange(0, cleared);
            Renumber(cleared, taken);

            _cleared = null;
            UndoneCount = cleared.Count;
            return Undone.Cards;
        }

        if (_arranged is { } places)
        {
            foreach (var (card, x, y) in places)
            {
                card.X = x;
                card.Y = y;
            }

            _arranged = null;
        _converted = null;
            return Undone.Places;
        }

        if (_removed is not { } removed) return Undone.Nothing;
        // (in the order they were in, so each goes back to its place)
        var before = _cards.ToList();
        foreach (var (card, index) in removed) _cards.Insert(Math.Min(index, _cards.Count), card);
        Renumber(removed.Select(pair => pair.Card), before);
        _removed = null;
        if (removed.Count > 1)
        {
            UndoneCount = removed.Count;
            return Undone.Cards;
        }

        UndoneCard = removed[0].Card;
        return Undone.Card;
    }

    /// <summary>
    /// Give a card a title of its own (one with tabs or line breaks has them as blank space). Its own name, or an empty
    /// title, takes the title away
    /// </summary>
    internal static void Rename(Card card, string title)
    {
        if (card.IsBadge || card.IsDivider || card.IsLine) return; // (a badge, a divider and a line have no title)
        title = string.Join(' ', title.Split('\t', '\r', '\n')).Trim();
        // (the name it has without one: no title of its own, so it follows its file or text)
        card.Title = title == card.OwnName ? string.Empty : title;
    }

    /// <summary>
    /// The card nearest to another in a direction (columns and rows: -1, 0 or 1), or null if there is none that way.
    /// A card in the same row (for left and right) or column (for up and down) comes first, so the keys go along a
    /// row as it is read; at its end, a card straight that way is nearer than one as far off to the side. A divider
    /// is in every row, and only left and right lead to it or from it. Only the cards it takes are gone to (the
    /// selection modes of the page; null: all)
    /// </summary>
    internal static Card? Nearest(Card from, int columns, int rows, int width, int height, Func<Card, bool>? takes = null)
    {
        if (columns == 0 && from.IsDivider) return null;
        var (fx, fy) = Shown(from, width, height);
        bool Skipped(Card card) => card == from || (takes != null && !takes(card)) || (columns == 0 && card.IsDivider);
        // The cards in the same row (h, l) or column (j, k) first: those beside the card, sharing at least two of its
        // three rows (or half the columns of the narrower: a badge under a card), so a card laid a little lower is in
        // the row and one a row lower is not.
        // The nearest that way, and of two as near, the one less to the side
        Card? inLine = null;
        (int Along, int Aside) inLineDistance = (int.MaxValue, int.MaxValue);
        foreach (var card in _cards)
        {
            if (Skipped(card)) continue;
            var (x, y) = Shown(card, width, height);
            var beside = columns != 0
                ? card.IsDivider || from.IsDivider || Math.Min(y, fy) + CardHeight - Math.Max(y, fy) >= (CardHeight + 1) / 2
                : Math.Min(x + card.Width, fx + from.Width) - Math.Max(x, fx) >= Math.Min(card.Width, from.Width) / 2;
            var along = columns != 0 ? (x - fx) * columns : (y - fy) * rows;
            if (!beside || along <= 0) continue;
            var distance = (along, columns != 0 ? Math.Abs(y - fy) : Math.Abs(x - fx));
            if (distance.CompareTo(inLineDistance) >= 0) continue;
            inLine = card;
            inLineDistance = distance;
        }

        if (inLine != null) return inLine;

        // Else (at the end of a row or column) the nearest that way in another
        Card? best = null;
        var bestScore = double.MaxValue;
        foreach (var card in _cards)
        {
            if (Skipped(card)) continue;
            var (x, y) = Shown(card, width, height);
            // (the distance in rows counts as much as three columns: a row on the screen is about that high)
            var along = columns != 0 ? (x - fx) * columns : (y - fy) * rows * 3.0;
            var aside = columns != 0 ? Math.Abs(y - fy) * 3.0 : Math.Abs(x - fx);
            if (along <= 0) continue;
            var score = along + aside * 2;
            if (score >= bestScore) continue;
            best = card;
            bestScore = score;
        }

        return best;
    }

    // The paths of the cards follow a file or directory that was renamed or moved. True if one did
    private static bool FollowHere(string oldPath, string newPath)
    {
        var changed = false;
        if (!CanKeep(newPath)) return false;
        foreach (var card in _cards.Where(card => card.HasPlace))
        {
            if (FileOperations.SamePath(card.Path, oldPath))
            {
                card.Path = System.IO.Path.GetFullPath(newPath);
                changed = true;
            }
            else if (FileOperations.IsInside(card.Path, oldPath))
            {
                card.Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(newPath,
                    System.IO.Path.GetRelativePath(System.IO.Path.GetFullPath(oldPath),
                        System.IO.Path.GetFullPath(card.Path))));
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// A file or directory was renamed or moved: the cards of all the boards follow (also those of the files inside a
    /// directory). The boards are read and saved here
    /// </summary>
    internal static void Follow(string oldPath, string newPath)
    {
        // The board of a project that was moved is in its new place (the paths in it are kept from the project, so
        // those inside it need no change)
        _currentBoard = Followed(_currentBoard, oldPath, newPath);
        Load();
        // (the list of projects may not have followed yet: their boards are looked for where they are now)
        var others = Boards.Names().Prepend(string.Empty)
            .Concat(WithBoardFiles(ProjectKeys().Select(key => Followed(key, oldPath, newPath))))
            .Where(key => key != _currentBoard).Distinct();
        foreach (var key in others)
        {
            WithBoard(key, () =>
            {
                if (FollowHere(oldPath, newPath)) WriteBoard(key);
            });
        }

        if (!FollowHere(oldPath, newPath)) return;
        var status = Save();
        if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "Corkboard: " + status);
    }

    // The key of a board after a file or directory was renamed or moved: the board of a project in it follows
    private static string Followed(string key, string oldPath, string newPath)
    {
        if (!IsProjectBoard(key) || !CanKeep(newPath)) return key;
        if (FileOperations.SamePath(key, oldPath)) return System.IO.Path.GetFullPath(newPath);
        if (!FileOperations.IsInside(key, oldPath)) return key;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(newPath,
            System.IO.Path.GetRelativePath(System.IO.Path.GetFullPath(oldPath), System.IO.Path.GetFullPath(key))));
    }

    /// <summary>
    /// A path the file of the board can keep: a card is a line, with tabs between its parts (Linux and macOS allow
    /// both in names)
    /// </summary>
    internal static bool CanKeep(string path) => path.IndexOfAny(new[] { '\t', '\r', '\n' }) < 0;

    /// <summary>
    /// ":pin": pin a file or folder to the open board, at a free place on the board as the window is now
    /// </summary>
    /// <returns>The message that tells what was done (with the number of the card)</returns>
    /// <param name="path">The file or folder</param>
    /// <param name="height">The height of the board on the page (less with the peek below it)</param>
    internal static string PinWithMessage(string path, int height)
    {
        if (!CanKeep(path)) return Lang.T("A path with a tab or a line break is not kept");
        Reload();
        if (CardOf(path) is { } there) return Lang.F("Already on the corkboard ({0}): {1}", there.Number, there.Name);
        var card = Add(path, BoardWidth, height, Directory.Exists(path));
        var status = Save();
        return status == DialogText.Success
            ? Lang.F("Pinned to the corkboard ({0}): {1}", card.Number, card.Name)
            : status;
    }

    /// <summary>
    /// The size of the board on the screen: all of it but the top bar and its line, and the two rows at the bottom
    /// </summary>
    internal static int BoardWidth => Init.LineWidth - 1;

    internal static int BoardHeight => Math.Max(CardHeight, Init.LineCount - 4);

    /// <summary>
    /// Names in the order a person reads them: upper and lower case alike, and numbers by their value
    /// ("Chapter 2" before "Chapter 10")
    /// </summary>
    internal sealed class NaturalOrder : IComparer<string>
    {
        internal static readonly NaturalOrder Instance = new();

        public int Compare(string? x, string? y)
        {
            x ??= string.Empty;
            y ??= string.Empty;
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var startX = i;
                    var startY = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    var numberX = x[startX..i].TrimStart('0');
                    var numberY = y[startY..j].TrimStart('0');
                    if (numberX.Length != numberY.Length) return numberX.Length.CompareTo(numberY.Length);
                    var digits = string.CompareOrdinal(numberX, numberY);
                    if (digits != 0) return digits;
                    continue;
                }

                var letters = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (letters != 0) return letters;
                i++;
                j++;
            }

            return (x.Length - i).CompareTo(y.Length - j);
        }
    }
}
