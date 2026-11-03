using System.Text;

namespace Blyant;

/// <summary>
/// The corkboard: files pinned as cards, and notes as cards, placed freely on a page (CorkboardPage) to plan a story
/// or keep the files worked on in view. Each card has a number that stays with it, a place (its column and row on the
/// board), a color and a title of its own if it is given one. A note card has a text instead of a file (its title is
/// its first line). The cards are drawn in their order, so a card later in the list covers the ones before it where
/// they overlap.
///
/// There can be several boards: the main board is the file "board.txt" in the data directory, and the others are
/// files of the same kind in the directory "boards" next to it, with their names. One board is open at a time (setting
/// "boardname"). The cards of all the boards follow a file that is renamed or moved.
///
/// A board is kept as a line for each card: its number, its place ("column,row"), its color (or nothing), its title
/// (or nothing) and the path of its file (nothing for a note), with a tab character between them. The text of a note
/// is on the line after it, after a "#" (as the notes of the favorites). It does not touch the console, so it can be
/// tested
/// </summary>
internal static class Corkboard
{
    internal const string FileName = "board.txt";

    /// <summary>
    /// The size of a card on the screen, with its frame
    /// </summary>
    internal const int CardWidth = 24;

    internal const int CardHeight = 3;

    internal sealed class Card
    {
        internal int Number;
        internal int X; // The column of its left side on the board
        internal int Y; // The row of its top on the board
        internal ConsoleColor? Color;
        internal string Title = string.Empty; // A title of its own (empty: the name of its file, or a note's first line)
        internal string Path = string.Empty; // Empty: a note
        internal string Text = string.Empty; // The text of a note

        /// <summary>
        /// A card with a text of its own instead of a file
        /// </summary>
        internal bool IsNote => Path.Length == 0;

        /// <summary>
        /// What the card shows: its title, or else its own name
        /// </summary>
        internal string Name => Title.Length > 0 ? Title : OwnName;

        /// <summary>
        /// The name of the card without a title: the name of its file without the extension, or the first line of
        /// its text (a tab in it as blank space, so the card keeps its frame)
        /// </summary>
        internal string OwnName => (!IsNote ? System.IO.Path.GetFileNameWithoutExtension(Path)
            : Text.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0) ?? Lang.T("Note"))
            .Replace('\t', ' ');
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

    internal static string FilePath => Boards.PathOf(CurrentBoard);

    /// <summary>
    /// The name of the board that is open (setting "boardname"; empty: the main board). Its cards are read when it
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

    // What u puts back: the card removed last and where it was in the order, or the places of the cards before they
    // were arranged
    private static (Card Card, int Index)? _removed;
    private static List<(Card Card, int X, int Y)>? _arranged;

    /// <summary>
    /// Read the cards of the open board from its file
    /// </summary>
    internal static void Load()
    {
        _cards.Clear();
        _removed = null;
        _arranged = null;
        _unreadable = false;
        // A board that is gone (deleted outside Blyant): the main board
        if (_currentBoard.Length > 0 && !File.Exists(FilePath)) _currentBoard = string.Empty;
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
    /// Read the cards again if their file was changed (by another page while this one was left): what u puts back is
    /// kept when it was not
    /// </summary>
    internal static void Reload()
    {
        try
        {
            var text = File.Exists(FilePath) ? File.ReadAllText(FilePath, Encoding.UTF8) : string.Empty;
            if (!_unreadable && text.Replace("\r", string.Empty) == Serialize()) return;
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
    /// <param name="name">Its name (empty: the main board)</param>
    internal static void UseBoard(string name)
    {
        _currentBoard = name;
        Load();
    }

    internal static void Parse(string text)
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
            var parts = line.Split('\t');
            if (parts.Length < 5 || !int.TryParse(parts[0], out var number) || number < 1) continue;
            var place = parts[1].Split(',');
            if (place.Length != 2 || !int.TryParse(place[0], out var x) || !int.TryParse(place[1], out var y)) continue;
            if (_cards.Any(card => card.Number == number)) continue;
            ConsoleColor? color = Enum.TryParse<ConsoleColor>(parts[2].Trim(), true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : null;
            last = new Card
            {
                Number = number, X = Math.Max(0, x), Y = Math.Max(0, y), Color = color, Title = parts[3].Trim(),
                Path = FileOperations.ListedPath(parts[4])
            };
            _cards.Add(last);
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var card in _cards)
        {
            text.Append(card.Number).Append('\t').Append(card.X).Append(',').Append(card.Y).Append('\t')
                .Append(card.Color?.ToString() ?? string.Empty).Append('\t').Append(card.Title).Append('\t')
                .Append(card.Path).Append('\n');
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
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(FilePath));
            if (directory != null) FileIo.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize()));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    // Do something with a board in the place of the one that is open, which stays as it is. A board that cannot be
    // read is left alone
    private static void WithBoard(string name, Action action)
    {
        var open = _cards;
        _cards = new List<Card>();
        try
        {
            if (File.Exists(Boards.PathOf(name))) Parse(File.ReadAllText(Boards.PathOf(name), Encoding.UTF8));
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            EventLog.Add(DialogText.LogFile + "Corkboard: " + Boards.PathOf(name) + " - FAILED: " + e.Message);
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
    /// <returns>False if the card is not there, or nothing was changed or saved</returns>
    internal static bool ChangeCard(string board, int number, Func<Card, bool> change)
    {
        if (board == _currentBoard)
        {
            return Numbered(number) is { } card && change(card) && Save() == DialogText.Success;
        }

        var changed = false;
        WithBoard(board, () =>
        {
            if (Numbered(number) is not { } card || !change(card)) return;
            FileIo.WriteAllBytesSafe(Boards.PathOf(board), new UTF8Encoding(false).GetBytes(Serialize()));
            changed = true;
        });
        return changed;
    }

    /// <summary>
    /// What is on a board, a line for each card, to show it in the box of boards
    /// </summary>
    internal static string BoardPreview(string name)
    {
        var lines = new List<string>();
        WithBoard(name, () => lines.AddRange(_cards.OrderBy(card => card.Number)
            .Select(card => card.Number + "  " + card.Name)));
        return string.Join("\n", lines);
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
    internal static string DeleteBoard(string name, out string trashedPath)
    {
        var result = Boards.Delete(name, out trashedPath);
        if (result == DialogText.Success && _currentBoard == name) UseBoard(string.Empty);
        return result;
    }

    /// <summary>
    /// The card of a file, or null if it is not pinned
    /// </summary>
    internal static Card? CardOf(string path) =>
        _cards.FirstOrDefault(card => !card.IsNote && FileOperations.SamePath(card.Path, path));

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
    /// Pin a file: a new card at the first free place on a board of a size, on top of the others
    /// </summary>
    internal static Card Add(string path, int width, int height)
    {
        var (x, y) = FreePlace(width, height);
        var card = new Card { Number = FreeNumber(), X = x, Y = y, Path = System.IO.Path.GetFullPath(path) };
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
    /// A place for a new card: the first one, row by row, where it covers no other card (with a column between
    /// cards), or else a step from the last card
    /// </summary>
    internal static (int X, int Y) FreePlace(int width, int height)
    {
        for (var y = 0; y + CardHeight <= Math.Max(CardHeight, height); y += CardHeight)
        {
            for (var x = 0; x + CardWidth <= Math.Max(CardWidth, width); x += CardWidth + 1)
            {
                if (!_cards.Any(card => Overlap(card, x, y))) return (x, y);
            }
        }

        // The board is full: on top of the others, a step from the last one
        var last = _cards.Count > 0 ? _cards[^1] : null;
        return last == null
            ? (0, 0)
            : (Math.Min(last.X + 2, Math.Max(0, width - CardWidth)), Math.Min(last.Y + 1, Math.Max(0, height - CardHeight)));
    }

    private static bool Overlap(Card card, int x, int y) =>
        x < card.X + CardWidth + 1 && card.X < x + CardWidth + 1 && y < card.Y + CardHeight && card.Y < y + CardHeight;

    /// <summary>
    /// Where a card is shown on a board of a size: inside it (a card placed on a larger window is drawn at the edge
    /// of a smaller one, and keeps its place)
    /// </summary>
    internal static (int X, int Y) Shown(Card card, int width, int height) =>
        (Math.Clamp(card.X, 0, Math.Max(0, width - CardWidth)), Math.Clamp(card.Y, 0, Math.Max(0, height - CardHeight)));

    /// <summary>
    /// Move a card by columns and rows, inside a board of a size
    /// </summary>
    internal static void Move(Card card, int columns, int rows, int width, int height)
    {
        var (x, y) = Shown(card, width, height);
        card.X = Math.Clamp(x + columns, 0, Math.Max(0, width - CardWidth));
        card.Y = Math.Clamp(y + rows, 0, Math.Max(0, height - CardHeight));
    }

    /// <summary>
    /// Put a card on top of the others (true), or below them
    /// </summary>
    internal static void ToFront(Card card, bool front)
    {
        if (!_cards.Remove(card)) return;
        if (front) _cards.Add(card);
        else _cards.Insert(0, card);
    }

    /// <summary>
    /// Take a card off the board (its file stays). It can be put back with Undo
    /// </summary>
    internal static void Remove(Card card)
    {
        var index = _cards.IndexOf(card);
        if (index < 0) return;
        _cards.RemoveAt(index);
        _removed = (card, index);
        _arranged = null;
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
        _arranged = null;
        return _cards.RemoveAll(gone.Contains);
    }

    /// <summary>
    /// The ways the cards can be arranged (":arrange number", English: as the words of commands)
    /// </summary>
    internal static readonly string[] Orders = { "number", "name", "color" };

    /// <summary>
    /// Arrange the cards in rows from the top left, in an order: by number, by name (upper and lower case alike,
    /// numbers by their value: "Chapter 2" before "Chapter 10"), or by color (in the order of the colors, the cards
    /// without one last; by number within a color). The order they are drawn in stays. It can be undone with Undo
    /// </summary>
    /// <returns>False if the order is not known</returns>
    internal static bool Arrange(string order, int width, int height)
    {
        IEnumerable<Card> sorted = order switch
        {
            "number" => _cards.OrderBy(card => card.Number),
            "name" => _cards.OrderBy(card => card.Name, NaturalOrder.Instance).ThenBy(card => card.Number),
            "color" => _cards.OrderBy(card => card.Color == null ? int.MaxValue : (int)card.Color.Value)
                .ThenBy(card => card.Number),
            _ => Array.Empty<Card>()
        };
        if (!Orders.Contains(order)) return false;
        _arranged = _cards.Select(card => (card, card.X, card.Y)).ToList();
        _removed = null;
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
        Places // The places of the cards before they were arranged
    }

    /// <summary>
    /// The card Undo put back
    /// </summary>
    internal static Card? UndoneCard { get; private set; }

    /// <summary>
    /// Put back what was done last: the card removed, where it was, or the places of the cards before they were
    /// arranged
    /// </summary>
    internal static Undone Undo()
    {
        UndoneCard = null;
        if (_arranged is { } places)
        {
            foreach (var (card, x, y) in places)
            {
                card.X = x;
                card.Y = y;
            }

            _arranged = null;
            return Undone.Places;
        }

        if (_removed is not { } removed || _cards.Any(card => card.Number == removed.Card.Number)) return Undone.Nothing;
        _cards.Insert(Math.Min(removed.Index, _cards.Count), removed.Card);
        _removed = null;
        UndoneCard = removed.Card;
        return Undone.Card;
    }

    /// <summary>
    /// Give a card a title of its own (one with tabs or line breaks has them as blank space). Its own name, or an empty
    /// title, takes the title away
    /// </summary>
    internal static void Rename(Card card, string title)
    {
        title = string.Join(' ', title.Split('\t', '\r', '\n')).Trim();
        // (the name it has without one: no title of its own, so it follows its file or text)
        card.Title = title == card.OwnName ? string.Empty : title;
    }

    /// <summary>
    /// The card nearest to another in a direction (columns and rows: -1, 0 or 1), or null if there is none that way.
    /// A card straight that way is nearer than one as far off to the side
    /// </summary>
    internal static Card? Nearest(Card from, int columns, int rows, int width, int height)
    {
        var (fx, fy) = Shown(from, width, height);
        Card? best = null;
        var bestScore = double.MaxValue;
        foreach (var card in _cards)
        {
            if (card == from) continue;
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
        foreach (var card in _cards.Where(card => !card.IsNote))
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
        Load();
        foreach (var name in Boards.Names().Prepend(string.Empty).Where(name => name != _currentBoard))
        {
            WithBoard(name, () =>
            {
                if (FollowHere(oldPath, newPath))
                {
                    FileIo.WriteAllBytesSafe(Boards.PathOf(name), new UTF8Encoding(false).GetBytes(Serialize()));
                }
            });
        }

        if (!FollowHere(oldPath, newPath)) return;
        var status = Save();
        if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "Corkboard: " + status);
    }

    /// <summary>
    /// A path the file of the board can keep: a card is a line, with tabs between its parts (Linux and macOS allow
    /// both in names)
    /// </summary>
    internal static bool CanKeep(string path) => path.IndexOfAny(new[] { '\t', '\r', '\n' }) < 0;

    /// <summary>
    /// ":pin": pin a file to the open board, at a free place on the board as the window is now
    /// </summary>
    /// <returns>The message that tells what was done (with the number of the card)</returns>
    /// <param name="path">The file</param>
    /// <param name="height">The height of the board on the page (less with the peek below it)</param>
    internal static string PinWithMessage(string path, int height)
    {
        if (!CanKeep(path)) return Lang.T("A path with a tab or a line break is not kept");
        Reload();
        if (CardOf(path) is { } there) return Lang.F("Already on the corkboard ({0}): {1}", there.Number, there.Name);
        var card = Add(path, BoardWidth, height);
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
