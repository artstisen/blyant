using System.Text;

namespace Blyant;

/// <summary>
/// The corkboard: files pinned as cards, placed freely on a page (CorkboardPage) to plan a story or keep the files
/// worked on in view. Each card has a number that stays with it, a place (its column and row on the board), a color
/// and a title of its own if it is given one. The cards are drawn in their order, so a card later in the list covers
/// the ones before it where they overlap.
///
/// The cards are kept in the file "board.txt" in the data directory, a line for each: its number, its place
/// ("column,row"), its color (or nothing), its title (or nothing) and the path of its file, with a tab character
/// between them. It does not touch the console, so it can be tested
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
        internal string Title = string.Empty; // A title of its own (empty: the name of its file)
        internal string Path = string.Empty;

        /// <summary>
        /// What the card shows: its title, or the name of its file without the extension
        /// </summary>
        internal string Name => Title.Length > 0 ? Title : System.IO.Path.GetFileNameWithoutExtension(Path);
    }

    private static readonly List<Card> List = new();

    /// <summary>
    /// The cards, in the order they are drawn (the last covers the others)
    /// </summary>
    internal static IReadOnlyList<Card> Cards => List;

    /// <summary>
    /// The file with the cards. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath => FilePathOverride ?? System.IO.Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    // The file was there, but could not be read: it is not written over until it is read
    private static bool _unreadable;

    // The card removed last, and where it was in the order (u puts it back)
    private static (Card Card, int Index)? _removed;

    /// <summary>
    /// Read the cards from the file
    /// </summary>
    internal static void Load()
    {
        List.Clear();
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

    internal static void Parse(string text)
    {
        List.Clear();
        foreach (var rawLine in text.Split('\n'))
        {
            var parts = rawLine.TrimEnd('\r').Split('\t');
            if (parts.Length < 5 || !int.TryParse(parts[0], out var number) || number < 1) continue;
            var place = parts[1].Split(',');
            if (place.Length != 2 || !int.TryParse(place[0], out var x) || !int.TryParse(place[1], out var y)) continue;
            var path = parts[4].Trim();
            if (path.Length == 0 || List.Any(card => card.Number == number)) continue;
            ConsoleColor? color = Enum.TryParse<ConsoleColor>(parts[2].Trim(), true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : null;
            List.Add(new Card
            {
                Number = number, X = Math.Max(0, x), Y = Math.Max(0, y), Color = color, Title = parts[3].Trim(),
                Path = path
            });
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var card in List)
        {
            text.Append(card.Number).Append('\t').Append(card.X).Append(',').Append(card.Y).Append('\t')
                .Append(card.Color?.ToString() ?? string.Empty).Append('\t').Append(card.Title).Append('\t')
                .Append(card.Path).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Write the cards to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize()));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The card of a file, or null if it is not pinned
    /// </summary>
    internal static Card? CardOf(string path) => List.FirstOrDefault(card => FileOperations.SamePath(card.Path, path));

    /// <summary>
    /// The card with a number, or null
    /// </summary>
    internal static Card? Numbered(int number) => List.FirstOrDefault(card => card.Number == number);

    // The lowest number no card has
    private static int FreeNumber()
    {
        var number = 1;
        while (List.Any(card => card.Number == number)) number++;
        return number;
    }

    /// <summary>
    /// Pin a file: a new card at the first free place on a board of a size (the next row of cards when there is
    /// none), at the top of the others
    /// </summary>
    internal static Card Add(string path, int width, int height)
    {
        var (x, y) = FreePlace(width, height);
        var card = new Card { Number = FreeNumber(), X = x, Y = y, Path = Path.GetFullPath(path) };
        List.Add(card);
        return card;
    }

    /// <summary>
    /// A place for a new card: the first one, row by row, where it covers no other card (with a column between
    /// cards), or else one moved a little from the last card
    /// </summary>
    internal static (int X, int Y) FreePlace(int width, int height)
    {
        for (var y = 0; y + CardHeight <= Math.Max(CardHeight, height); y += CardHeight)
        {
            for (var x = 0; x + CardWidth <= Math.Max(CardWidth, width); x += CardWidth + 1)
            {
                if (!List.Any(card => Overlap(card, x, y))) return (x, y);
            }
        }

        // The board is full: on top of the others, a step from the last one
        var last = List.Count > 0 ? List[^1] : null;
        return last == null ? (0, 0) : (Math.Min(last.X + 2, Math.Max(0, width - CardWidth)), Math.Min(last.Y + 1, Math.Max(0, height - CardHeight)));
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
        if (!List.Remove(card)) return;
        if (front) List.Add(card);
        else List.Insert(0, card);
    }

    /// <summary>
    /// Take a card off the board (the file stays). It can be put back with Undo
    /// </summary>
    internal static void Remove(Card card)
    {
        var index = List.IndexOf(card);
        if (index < 0) return;
        List.RemoveAt(index);
        _removed = (card, index);
    }

    /// <summary>
    /// Take several cards off the board (":prune"). They cannot be put back
    /// </summary>
    internal static int RemoveMany(IEnumerable<Card> cards)
    {
        var gone = cards.ToHashSet();
        _removed = null;
        return List.RemoveAll(gone.Contains);
    }

    /// <summary>
    /// Put back the card removed last, where it was. Null if there is none (or its number is taken again)
    /// </summary>
    internal static Card? Undo()
    {
        if (_removed is not { } removed || List.Any(card => card.Number == removed.Card.Number)) return null;
        List.Insert(Math.Min(removed.Index, List.Count), removed.Card);
        _removed = null;
        return removed.Card;
    }

    /// <summary>
    /// Give a card a title of its own (one with tabs or line breaks has them as blank space)
    /// </summary>
    internal static void Rename(Card card, string title)
    {
        card.Title = string.Join(' ', title.Split('\t', '\r', '\n')).Trim();
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
        foreach (var card in List)
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

    /// <summary>
    /// A file or directory was renamed or moved: the cards follow (also those of the files inside a directory). The
    /// list is read and saved here
    /// </summary>
    internal static void Follow(string oldPath, string newPath)
    {
        Load();
        var changed = false;
        foreach (var card in List)
        {
            if (FileOperations.SamePath(card.Path, oldPath))
            {
                card.Path = Path.GetFullPath(newPath);
                changed = true;
            }
            else if (FileOperations.IsInside(card.Path, oldPath))
            {
                card.Path = Path.GetFullPath(Path.Combine(newPath, Path.GetRelativePath(Path.GetFullPath(oldPath),
                    Path.GetFullPath(card.Path))));
                changed = true;
            }
        }

        if (!changed) return;
        var status = Save();
        if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "Corkboard: " + status);
    }

    /// <summary>
    /// ":pin": pin a file to the corkboard, at a free place on the board as the window is now
    /// </summary>
    /// <returns>The message that tells what was done (with the number of the card)</returns>
    internal static string PinWithMessage(string path)
    {
        Load();
        if (CardOf(path) is { } there) return Lang.F("Already on the corkboard ({0}): {1}", there.Number, there.Name);
        var card = Add(path, BoardWidth, BoardHeight);
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
}
