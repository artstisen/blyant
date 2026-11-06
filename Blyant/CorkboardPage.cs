namespace Blyant;

/// <summary>
/// The page of the corkboard (Corkboard): the cards of the open board drawn where they are placed, the selected one on
/// top, and below them the peek of the selected card when it is on. A card is selected with Tab, its number and
/// Enter, or the keys of a direction (the nearest card that way), and opened with Enter (a note is edited). V moves
/// it, T and E put it on top of the others or below them, C colors it, X gives it a title, D takes it off the board,
/// N pins a file, F a folder, A adds a note, B a badge (a small round mark with an "i" and a note, to mark a place),
/// I shows information, P the peek. Space marks cards: V, C, T, E and D work on the marked ones and the selected one
/// together. | adds a divider (a line from top to bottom, behind the cards, moved left and right), and M changes what
/// the keys of the directions and Tab select: cards and badges (at first), all, cards, badges or dividers (setting
/// "boardmode"). ":list" shows the
/// boards, ":arrange" lays the cards out in an order, and ":clear" takes them all off. Opened with ":board" (":board name": another
/// board) from every command line, ":jump board", the Jump menu, the Start tab, and B in the list of projects (the
/// board of a project)
/// </summary>
internal static class CorkboardPage
{
    private const int BoardTop = 2; // Below the top bar and its line
    private const int PeekRows = 6; // The peek: its line and the text below it

    /// <summary>
    /// The peek of the selected card is shown below the board (setting "boardpeek", on at first)
    /// </summary>
    internal static bool PeekOn { get; set; } = true;

    /// <summary>
    /// What the keys of the directions and Tab select (setting "boardmode")
    /// </summary>
    internal enum SelectMode
    {
        Both, // Cards and badges: all but the dividers
        All,
        Cards, // Of files, folders and notes
        Badges,
        Dividers
    }

    internal static SelectMode Mode { get; set; } = SelectMode.Both;

    /// <summary>
    /// The modes as they are written (":mode cards", the setting; English: the words of commands), in the order M
    /// goes through them
    /// </summary>
    internal static readonly string[] ModeWords = { "both", "all", "cards", "badges", "dividers" };

    internal static string ModeWord => ModeWords[(int)Mode];

    /// <summary>
    /// The mode written (null: not one)
    /// </summary>
    internal static SelectMode? ModeOf(string word)
    {
        var at = Array.IndexOf(ModeWords, word.Trim().ToLowerInvariant());
        return at < 0 ? null : (SelectMode)at;
    }

    private static string ModeName(SelectMode mode) => mode switch
    {
        SelectMode.Both => Lang.T("Cards and badges"),
        SelectMode.Cards => Lang.T("Cards"),
        SelectMode.Badges => Lang.T("Badges"),
        SelectMode.Dividers => Lang.T("Dividers"),
        _ => Lang.T("All")
    };

    /// <summary>
    /// A card is one the mode selects
    /// </summary>
    internal static bool InMode(Corkboard.Card card) => Mode switch
    {
        SelectMode.Cards => !card.IsBadge && !card.IsDivider,
        SelectMode.Badges => card.IsBadge,
        SelectMode.Dividers => card.IsDivider,
        SelectMode.Both => !card.IsDivider,
        _ => true
    };

    private static bool _open; // The page is open (it is not opened again on top of itself)
    private const int NoCard = 0; // No card is selected (a card's number is above 0, a divider's below)
    private static int _selected = NoCard; // The number of the selected card, badge or divider
    private static bool _moving; // The selected card is moved with the keys of the directions
    private static string _typed = string.Empty; // The number typed to select a card
    private static string _filter = string.Empty; // Only the cards with it in their names or paths are not dimmed
    // The marked cards (while the board is open): their numbers, and what each is (a file, a note, a badge), so a card
    // that has the number of a marked one since the board was read again is not marked
    private static readonly Dictionary<int, string> _marks = new();
    private static string _message = string.Empty;

    /// <summary>
    /// Show the corkboard until it is closed
    /// </summary>
    internal static void Show()
    {
        if (_open) return;
        _open = true;
        try
        {
            Run();
        }
        finally
        {
            _open = false;
            _moving = false;
            _typed = string.Empty;
            // (kept for a card opened: the page is shown again when its file or folder is closed)
            if (!Jump.BackToBoard) _marks.Clear();
        }
    }

    /// <summary>
    /// ":board name": the board to show (upper and lower case alike; "main" is the main board). A name that is not
    /// a board is told on the board shown
    /// </summary>
    internal static void Choose(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        if (Corkboard.FindByName(name) is not { } found)
        {
            _message = Lang.F("No board named {0}", name);
            return;
        }

        OpenBoard(found);
    }

    /// <summary>
    /// B in the list of projects: the board of a project (made when a card is put on it), shown until it is closed
    /// </summary>
    internal static void ShowProjectBoard(string project)
    {
        OpenBoard(Path.GetFullPath(project));
        Show();
    }

    // A board is shown with its last card selected, without a filter
    private static void OpenBoard(string key)
    {
        Corkboard.UseBoard(key);
        FileIo.SaveSettings();
        _filter = string.Empty;
        _marks.Clear();
        SelectLast();
    }

    // The card on top selected (of those the mode selects, if there are any; a divider when there is nothing else)
    private static void SelectLast()
    {
        var cards = Corkboard.Cards;
        _selected = (cards.LastOrDefault(card => !card.IsDivider && InMode(card)) ?? cards.LastOrDefault(InMode) ??
                     cards.LastOrDefault(card => !card.IsDivider) ?? cards.LastOrDefault())?.Number ?? NoCard;
    }

    private static Corkboard.Card? Selected => Corkboard.Numbered(_selected);

    /// <summary>
    /// The marks of the cards are not kept: the page was left for a card's file or folder, and not gone back to
    /// </summary>
    internal static void ForgetMarks() => _marks.Clear();

    // What a card is, as its mark keeps it: its file or folder, or a note or a badge (English: not shown)
    private static string MarkOf(Corkboard.Card card) =>
        card.IsDivider ? "\tdivider" : card.IsBadge ? "\tbadge" : card.IsNote ? "\tnote" : card.Path;

    private static bool IsMarked(Corkboard.Card card) =>
        _marks.TryGetValue(card.Number, out var mark) && mark == MarkOf(card);

    // A card that is gone (removed, or the board read again after it was changed elsewhere) is no longer marked
    private static void ForgetGoneMarks()
    {
        foreach (var number in _marks.Keys.ToList())
        {
            if (Corkboard.Numbered(number) is not { } card || !IsMarked(card)) _marks.Remove(number);
        }
    }

    /// <summary>
    /// The cards that keys work on: the marked ones and the selected one, in the order they are drawn (only the
    /// selected one when none is marked)
    /// </summary>
    private static List<Corkboard.Card> Group() =>
        Corkboard.Cards.Where(card => card.Number == _selected || IsMarked(card)).ToList();

    /// <summary>
    /// The height of the board: less with the peek below it
    /// </summary>
    internal static int Height => Math.Max(Corkboard.CardHeight, Corkboard.BoardHeight - (PeekOn ? PeekRows : 0));

    // The cards the filter leaves (all without a filter), in the order they are drawn
    private static List<Corkboard.Card> Matching() => Corkboard.Cards.Where(Matches).ToList();

    // (a divider is not dimmed: it tiles the board)
    private static bool Matches(Corkboard.Card card) => _filter.Length == 0 || card.IsDivider ||
                                                        card.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                                                        card.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                                                        card.Text.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private static void Run()
    {
        Corkboard.Load();
        if (Selected == null) SelectLast();
        while (true)
        {
            Init.ApplyAutoSize();
            ForgetGoneMarks();
            Draw();
            var key = Keyboard.ReadKey();
            var message = _message;
            _message = string.Empty;
            if (Keyboard.IsRedraw(key))
            {
                _message = message;
                continue;
            }

            if (_moving)
            {
                Move(key);
                continue;
            }

            if (Jump.IsKey(key))
            {
                Jump.Open();
                Corkboard.Reload();
                continue;
            }

            if (RecentBox.IsKey(key))
            {
                RecentBox.Open();
                continue;
            }

            // A number typed, to select the card with it (Enter)
            if (key.KeyChar is >= '0' and <= '9' && _typed.Length < 4 && (_typed.Length > 0 || key.KeyChar != '0'))
            {
                _typed += key.KeyChar;
                continue;
            }

            if (key.Key == ConsoleKey.Escape && (_typed.Length > 0 || _marks.Count > 0 || _filter.Length > 0))
            {
                // Esc takes the number typed away first, then the marks, then the filter
                if (_typed.Length > 0) _typed = string.Empty;
                else if (_marks.Count > 0) _marks.Clear();
                else _filter = string.Empty;
                continue;
            }

            if (key.Key == ConsoleKey.Enter && _typed.Length > 0)
            {
                var number = int.Parse(_typed);
                _typed = string.Empty;
                if (Corkboard.Numbered(number) != null) _selected = number;
                else _message = Lang.F("No card {0}", number);
                continue;
            }

            _typed = string.Empty;
            if (key.Key is ConsoleKey.Escape || key.KeyChar is 'q' or 'Q' or '-') break;
            if (!HandleKey(key)) break;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    // A key of the page; false closes it
    private static bool HandleKey(ConsoleKeyInfo key)
    {
        var card = Selected;
        var navigation = Keyboard.ToNavigationKey(key).Key;
        if (key.Key == ConsoleKey.Tab)
        {
            SelectNext((key.Modifiers & ConsoleModifiers.Shift) != 0);
            return true;
        }

        if (navigation is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or ConsoleKey.UpArrow or ConsoleKey.DownArrow &&
            (key.Modifiers & ConsoleModifiers.Shift) == 0 && !char.IsUpper(key.KeyChar))
        {
            // The nearest card that way
            if (card == null) return true;
            var (columns, rows) = Direction(navigation);
            if (Corkboard.Nearest(card, columns, rows, Corkboard.BoardWidth, Height, InMode) is { } next)
            {
                _selected = next.Number;
            }
            return true;
        }

        switch (key.KeyChar)
        {
            case '?':
                Tools.WriteEditorHelp(Tools.HelpTopic.Board);
                return true;
            case '|':
                AddDivider();
                return true;
            case ':':
                return RunCommand();
            case '/':
                _filter = CommandLine.Read('/', false) ?? string.Empty;
                if (Matching().Count > 0 && (card == null || !Matches(card))) _selected = Matching()[^1].Number;
                return true;
            case 'u':
                Undo();
                return true;
        }

        if ((key.Modifiers & ConsoleModifiers.Control) != 0) return true;
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                Open(card);
                break;
            case ConsoleKey.V:
                if (card != null) _moving = true;
                break;
            case ConsoleKey.Spacebar:
                if (card != null && !_marks.Remove(card.Number)) _marks[card.Number] = MarkOf(card);
                break;
            case ConsoleKey.T:
            case ConsoleKey.E:
                if (card == null) break;
                Corkboard.ToFront(Group(), key.Key == ConsoleKey.T);
                _message = key.Key == ConsoleKey.T ? Lang.T("On top of the other cards") : Lang.T("Below the other cards");
                SaveCards();
                break;
            case ConsoleKey.C:
                PickColor(card);
                break;
            case ConsoleKey.X:
                Rename(card, string.Empty);
                break;
            case ConsoleKey.D:
                Remove(card);
                break;
            case ConsoleKey.N:
                PinFile();
                break;
            case ConsoleKey.F:
                PinFolder();
                break;
            case ConsoleKey.A:
                AddNote();
                break;
            case ConsoleKey.B:
                AddBadge();
                break;
            case ConsoleKey.M:
                ChangeMode(string.Empty);
                break;
            case ConsoleKey.I:
                ShowInfo(card);
                break;
            case ConsoleKey.P:
                TogglePeek();
                break;
        }

        return true;
    }

    private static (int Columns, int Rows) Direction(ConsoleKey navigation) => navigation switch
    {
        ConsoleKey.LeftArrow => (-1, 0),
        ConsoleKey.RightArrow => (1, 0),
        ConsoleKey.UpArrow => (0, -1),
        _ => (0, 1)
    };

    // Tab and Shift+Tab: the next/previous card by number (of those the filter leaves and the mode selects), going
    // round; the dividers after the cards, from the left
    private static void SelectNext(bool back)
    {
        var numbers = Matching().Where(InMode).OrderBy(card => card.IsDivider)
            .ThenBy(card => card.IsDivider ? card.X : card.Number).Select(card => card.Number).ToList();
        if (numbers.Count == 0) return;
        var at = numbers.IndexOf(_selected);
        _selected = at < 0 ? numbers[0] : numbers[(at + (back ? numbers.Count - 1 : 1)) % numbers.Count];
    }

    // V: the selected card (and the marked ones, together) is moved with the keys of the directions (H J K L: five
    // steps), until Esc or Enter
    private static void Move(ConsoleKeyInfo key)
    {
        var card = Selected;
        if (card == null || key.Key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.V)
        {
            _moving = false;
            SaveCards();
            return;
        }

        if (MoveOf(key) is not { } move) return;
        Corkboard.Move(Group(), move.Columns, move.Rows, Corkboard.BoardWidth, Height);
    }

    /// <summary>
    /// The columns and rows a key moves a card (V): h j k l and the arrow keys one step, H J K L (also with Caps Lock)
    /// and Shift with an arrow key five steps. Null for another key
    /// </summary>
    internal static (int Columns, int Rows)? MoveOf(ConsoleKeyInfo key)
    {
        var upper = char.IsUpper(key.KeyChar);
        // (only h j k l are directions: H J K L are those, five steps at a time)
        var navigation = Keyboard.ToNavigationKey(upper
            ? new ConsoleKeyInfo(char.ToLowerInvariant(key.KeyChar), key.Key, false, false, false)
            : key).Key;
        if (navigation is not (ConsoleKey.LeftArrow or ConsoleKey.RightArrow or ConsoleKey.UpArrow or ConsoleKey.DownArrow))
        {
            return null;
        }

        var steps = upper || (key.Modifiers & ConsoleModifiers.Shift) != 0 ? 5 : 1;
        var (columns, rows) = Direction(navigation);
        return (columns * steps, rows * steps);
    }

    // Enter: open the file of the card in the editor, or its folder in the file browser (as a jump: the page is
    // left, and shown again when the editor or the file browser is closed, with the marks), or edit the text of a
    // note
    private static void Open(Corkboard.Card? card)
    {
        if (card == null || card.IsDivider) return;
        if (card.IsNote)
        {
            EditNote(card);
            return;
        }

        if (card.IsFolder)
        {
            if (Directory.Exists(card.Path)) Jump.ToPlace(new JumpTarget(JumpKind.Directory, card.Path), null, true);
            else _message = DialogText.DirectoryNotFound;
            return;
        }

        if (!File.Exists(card.Path))
        {
            _message = DialogText.FileNotFound;
            return;
        }

        Jump.ToPlace(new JumpTarget(JumpKind.File, card.Path), null, true);
    }

    // A or ":note": a new note card, its text written in the editor at once
    private static void AddNote()
    {
        var card = Corkboard.AddNote(string.Empty, Corkboard.BoardWidth, Height);
        var board = Corkboard.CurrentBoard;
        _selected = card.Number;
        SaveCards();
        try
        {
            EditNote(card);
        }
        finally
        {
            // A new note left without text is not kept, also when the editor was left by a jump
            if (Corkboard.ChangeCard(board, card.Number, kept => kept.IsNote && kept.Text.Length == 0 &&
                                                                 Corkboard.Discard(kept)) == DialogText.Success &&
                Selected == null)
            {
                SelectLast();
            }
        }
    }

    // B or ":badge": a new badge, its note written in the editor at once. It is kept without a note too: its place
    // and color can mark something by themselves
    private static void AddBadge()
    {
        var card = Corkboard.AddBadge(string.Empty, Corkboard.BoardWidth, Height);
        _selected = card.Number;
        SaveCards();
        EditNote(card);
    }

    // | or ":divider": a new divider, in the column right of the selected card (between it and the next, as the
    // cards are laid a column apart), or in the middle of the board
    private static void AddDivider()
    {
        var width = Corkboard.BoardWidth;
        var column = width / 2;
        if (Selected is { IsDivider: false } card && Corkboard.Shown(card, width, Height).X + card.Width is var right &&
            right < width)
        {
            column = right;
        }

        _selected = Corkboard.AddDivider(column, width).Number;
        _message = Lang.T("Divider added: V and h/l move it");
        SaveCards();
    }

    // M or ":mode name": what the keys select, the next mode or the one named (kept in the settings)
    private static void ChangeMode(string word)
    {
        if (word.Trim().Length == 0)
        {
            Mode = (SelectMode)(((int)Mode + 1) % ModeWords.Length);
        }
        else if (ModeOf(word) is { } mode)
        {
            Mode = mode;
        }
        else
        {
            _message = Lang.T(":mode cards, :mode badges, :mode dividers or :mode all");
            return;
        }

        _message = Lang.F("Select: {0}", ModeName(Mode));
        FileIo.SaveSettings();
    }

    // The text of a note in the editor (its first line is the title of the card, unless it has one of its own). It
    // is kept on the board and card it was written for, by number: the cards may be read again while the editor is
    // open, and another board opened (":board" in the editor)
    private static void EditNote(Corkboard.Card card)
    {
        var board = Corkboard.CurrentBoard;
        var error = FileExplorer.EditListNote(card.Text,
            NotePanes.NoteFileName("board-note", board + "/" + card.Number), Lang.T("Corkboard"),
            written =>
            {
                var text = Notes.Clean(written);
                var result = Corkboard.ChangeCard(board, card.Number, kept =>
                {
                    if (!kept.IsNote) return false;
                    kept.Text = text;
                    return true;
                });
                // (not there, or saving it failed: told why)
                if (result == null || result == string.Empty)
                {
                    _message = card.IsBadge
                        ? Lang.T("The badge is no longer on the board: the note was not kept")
                        : Lang.T("The card is no longer on the board: the note was not kept");
                }
                else if (result != DialogText.Success)
                {
                    _message = result;
                }
            });
        if (error != null) _message = error;
    }

    // I or ":info": what the card is, and its file
    private static void ShowInfo(Corkboard.Card? card)
    {
        if (card == null) return;
        if (card.IsDivider)
        {
            ChoiceBox.Forget();
            ChoiceBox.ShowRows(Lang.T("Divider"), new[]
            {
                (Lang.T("Column:"), (Corkboard.Shown(card, Corkboard.BoardWidth, Height).X + 1).ToString())
            });
            return;
        }

        if (card.IsFolder)
        {
            _message = Directory.Exists(card.Path)
                ? FileExplorer.ShowPlaceInfo(card.Path, true)
                : DialogText.DirectoryNotFound;
            return;
        }

        if (!card.IsNote)
        {
            if (!File.Exists(card.Path))
            {
                _message = DialogText.FileNotFound;
                return;
            }

            _message = FileExplorer.ShowPlaceInfo(card.Path, false);
            return;
        }

        ChoiceBox.Forget();
        ChoiceBox.ShowRows(card.IsBadge ? Lang.T("Badge") : Lang.T("Note"), new[]
        {
            (DialogText.InfoName, card.Name), (Lang.T("Words:"), Tools.WordCount(card.Text).ToString()),
            (Lang.T("Lines:"), Tools.LineCount(card.Text).ToString())
        });
    }

    // P or ":peek": the peek of the selected card below the board on or off (kept in the settings)
    private static void TogglePeek()
    {
        PeekOn = !PeekOn;
        FileIo.SaveSettings();
    }

    private static void PickColor(Corkboard.Card? card)
    {
        if (card == null) return;
        if (!ColorPicker.Pick(DialogText.ColorCaption, (Init.LineWidth - 26) / 2, true, card.Color, null, null,
                out var picked))
        {
            return;
        }

        // (the marked cards too)
        foreach (var colored in Group()) colored.Color = picked;
        SaveCards();
    }

    // X or ":rename title": a title of its own for the card (the file keeps its name)
    private static void Rename(Corkboard.Card? card, string title)
    {
        if (card == null) return;
        if (card.IsBadge)
        {
            _message = Lang.T("A badge has no title: Enter writes its note");
            return;
        }

        if (card.IsDivider)
        {
            _message = Lang.T("A divider has no title");
            return;
        }

        if (title.Trim().Length == 0)
        {
            ChoiceBox.Forget();
            title = ChoiceBox.AskText(Lang.T("Card title"), DialogText.TypeNameAndPressEnter, DialogText.NameColon,
                card.Name) ?? string.Empty;
            if (title.Length == 0) return;
        }

        Corkboard.Rename(card, title);
        SaveCards();
    }

    // D or ":remove": take the card off the board, after asking (u puts it back), or the marked ones with it
    private static void Remove(Corkboard.Card? card)
    {
        if (card == null) return;
        ChoiceBox.Forget();
        var group = Group();
        if (group.Count > 1)
        {
            // The marked cards, with the selected one
            var files = group.Any(marked => marked.HasPlace) ? Lang.T("The files stay where they are.") : string.Empty;
            if (!ChoiceBox.Confirm(Lang.T("Remove cards"), Lang.F("Take {0} cards off the corkboard?", group.Count),
                    files, false))
            {
                return;
            }

            var count = Corkboard.Remove(group);
            _marks.Clear();
            SelectLast();
            _message = Lang.F("Cards taken off: {0} (u puts them back)", count);
            SaveCards();
            return;
        }

        var note = !card.HasPlace ? string.Empty
            : card.IsFolder ? Lang.T("The folder stays where it is.")
            : Lang.T("The file stays where it is.");
        var asked = card.IsDivider
            ? ChoiceBox.Confirm(Lang.T("Remove divider"), Lang.T("Take the divider off the corkboard?"), note, false)
            : card.IsBadge
            ? ChoiceBox.Confirm(Lang.T("Remove badge"), Lang.F("Take badge {0} off the corkboard?", card.Number), note,
                false)
            : ChoiceBox.Confirm(Lang.T("Remove card"), Lang.F("Take card {0} off the corkboard?", card.Number), note,
                false);
        if (!asked)
        {
            return;
        }

        Corkboard.Remove(card);
        SelectLast();
        _message = Lang.F("Removed: {0} (u puts it back)", card.Name);
        SaveCards();
    }

    // u: the card removed last back, or the cards back where they were before they were arranged
    private static void Undo()
    {
        switch (Corkboard.Undo())
        {
            case Corkboard.Undone.Card:
                _selected = Corkboard.UndoneCard!.Number;
                _message = Lang.F("Put back: {0}", Corkboard.UndoneCard.Name);
                break;
            case Corkboard.Undone.Places:
                _message = Lang.T("The cards are back in their places");
                break;
            case Corkboard.Undone.Cards:
                SelectLast();
                _message = Lang.F("Cards put back: {0}", Corkboard.UndoneCount);
                break;
            default:
                _message = Lang.T("Nothing to put back");
                return;
        }

        SaveCards();
    }

    // ":arrange number" (name, color): the cards in rows in that order (u puts them back)
    private static void Arrange(string order)
    {
        if (!Corkboard.Arrange(order.Trim().ToLowerInvariant(), Corkboard.BoardWidth, Height))
        {
            _message = Lang.T(":arrange number, :arrange name or :arrange color");
            return;
        }

        _message = Lang.T("Arranged (u puts the cards back)");
        SaveCards();
    }

    // ":clear": take all the cards off the board, after asking in a box that a word typed by mistake cannot answer (u
    // puts them back)
    private static void Clear()
    {
        var count = Corkboard.Cards.Count;
        if (count == 0)
        {
            _message = Lang.T("The board has no cards");
            return;
        }

        Draw();
        ChoiceBox.Forget();
        // (the cards, the badges and the dividers counted apart, as on the line above the keys)
        var badges = Corkboard.Cards.Count(card => card.IsBadge);
        var dividers = Corkboard.Cards.Count(card => card.IsDivider);
        var question = badges + dividers > 0
            ? Lang.F("Take all the cards ({0}), badges ({1}) and dividers ({2}) off the board?",
                count - badges - dividers, badges, dividers)
            : Lang.F("Take all the cards ({0}) off the board?", count);
        var files = Corkboard.Cards.Any(card => card.HasPlace) ? Lang.T("The files stay where they are.") : string.Empty;
        if (!ChoiceBox.ConfirmGuarded(Lang.T("Clear board"), question, files))
        {
            return;
        }

        Corkboard.Clear();
        _marks.Clear();
        _selected = NoCard;
        _message = Lang.F("Cards taken off: {0} (u puts them back)", count);
        SaveCards();
    }

    // F or ":folder": pin a folder picked in the file browser
    private static void PinFolder()
    {
        var path = FileExplorer.PickDirectory(Lang.T("Pin a folder to the corkboard"));
        Corkboard.Reload();
        if (path == null) return;
        _message = Corkboard.PinWithMessage(path, Height);
        if (Corkboard.CardOf(path) is { } card) _selected = card.Number;
    }

    // N or ":new": pin a file picked in the file browser
    private static void PinFile()
    {
        var path = FileExplorer.PickFile(Lang.T("Pin a file to the corkboard"));
        Corkboard.Reload();
        if (path == null) return;
        _message = Corkboard.PinWithMessage(path, Height);
        if (Corkboard.CardOf(path) is { } card) _selected = card.Number;
    }

    private static void SaveCards()
    {
        var status = Corkboard.Save();
        if (status != DialogText.Success) _message = status;
    }

    // ":" on the page: a command. False if the page is closed
    private static bool RunCommand()
    {
        var command = CommandLine.Read(CommandContext.Board);
        if (command == null) return true;
        var name = CommandLine.Name(command);
        var argument = CommandLine.LastArgument;
        switch (name)
        {
            case "q":
            case "quit":
            case "close":
                return false;
            case "h":
            case "help":
                Tools.WriteEditorHelp(Tools.HelpTopic.Board);
                break;
            case "jump":
            case "ju":
            case "jmp":
                Jump.ToPage(CommandLine.Argument(command));
                Corkboard.Reload();
                break;
            case "book":
                Jump.ToBookmark(CommandLine.Argument(command));
                Corkboard.Reload();
                break;
            case "recent":
                RecentBox.Open();
                break;
            case "new":
                PinFile();
                break;
            case "folder":
                PinFolder();
                break;
            case "clear":
                Clear();
                break;
            case "note":
                AddNote();
                break;
            case "badge":
                AddBadge();
                break;
            case "divider":
                AddDivider();
                break;
            case "mode":
                ChangeMode(argument);
                break;
            case "info":
                ShowInfo(Selected);
                break;
            case "peek":
                TogglePeek();
                break;
            case "list":
            case "board":
            case "corkboard":
                ShowBoards(argument);
                break;
            case "arrange":
                Arrange(argument);
                break;
            case "color":
                PickColor(Selected);
                break;
            case "rename":
                Rename(Selected, argument);
                break;
            case "remove":
                Remove(Selected);
                break;
            case "u":
            case "undo":
                Undo();
                break;
            case "top":
            case "bottom":
                if (Selected == null) break;
                Corkboard.ToFront(Group(), name == "top");
                SaveCards();
                break;
            case "missing":
                ShowMissing();
                break;
            case "prune":
                Prune();
                break;
            default:
                CommandLine.ShowUnknown(command);
                break;
        }

        // (the end of the commands)
        return true;
    }

    #region Boards

    private static string BoardName(string key) =>
        key.Length == 0 ? Lang.T("Main board")
        : Corkboard.IsProjectBoard(key) ? Lang.F("{0} (project)", Corkboard.NameOf(key))
        : key;

    // ":list": the boards in a box (the projects with boards after the others), to open one, add one, rename one or
    // delete one. ":list name" opens one at once
    private static void ShowBoards(string argument)
    {
        if (argument.Trim().Length > 0)
        {
            if (Corkboard.FindByName(argument) is { } found) OpenBoard(found);
            else _message = Lang.F("No board named {0}", argument.Trim());
            return;
        }

        Draw();
        while (true)
        {
            var boards = Corkboard.Boards.Names().Prepend(string.Empty).Concat(Corkboard.ProjectBoards()).ToList();
            // (the open board of a project without cards yet is there too)
            if (!boards.Contains(Corkboard.CurrentBoard)) boards.Add(Corkboard.CurrentBoard);
            string? open = null;
            var picked = PickListBox.Show(Lang.T("Boards"), boards.Select(BoardName).ToList(),
                row => Corkboard.BoardPreview(boards[row]), Math.Max(0, boards.IndexOf(Corkboard.CurrentBoard)),
                Lang.T("j/k Select | Enter Open | N New | R Rename | D Delete | Esc Close"), new PickListBox.Extras
                {
                    Key = (key, row) =>
                    {
                        if ((key.Modifiers & ConsoleModifiers.Control) != 0 || row < 0) return PickListBox.KeyUse.None;
                        switch (key.Key)
                        {
                            case ConsoleKey.N:
                                open = NewBoard();
                                return open != null ? PickListBox.KeyUse.Close : PickListBox.KeyUse.Used;
                            case ConsoleKey.R:
                                return RenameBoard(boards[row]);
                            case ConsoleKey.D:
                                return DeleteBoard(boards[row]);
                            default:
                                return PickListBox.KeyUse.None;
                        }
                    }
                });
            if (picked == PickListBox.Reload) continue;
            if (open != null) OpenBoard(open);
            else if (picked >= 0 && boards[picked] != Corkboard.CurrentBoard) OpenBoard(boards[picked]);
            if (open != null || picked >= 0) _message = Lang.F("Board: {0}", BoardName(Corkboard.CurrentBoard));
            return;
        }
    }

    // The box for a new board: the name of the board made, or null
    private static string? NewBoard()
    {
        ChoiceBox.Forget();
        var name = ChoiceBox.AskText(Lang.T("New board"), DialogText.TypeNameAndPressEnter, DialogText.NameColon,
            string.Empty);
        if (name == null) return null;
        var result = Corkboard.NewBoard(name);
        if (result == DialogText.Success) return name.Trim();
        ChoiceBox.Message(Lang.T("New board"), result, string.Empty);
        return null;
    }

    private static PickListBox.KeyUse RenameBoard(string name)
    {
        ChoiceBox.Forget();
        if (name.Length == 0)
        {
            ChoiceBox.Message(Lang.T("Rename board"), Lang.T("The main board keeps its name"), string.Empty);
            return PickListBox.KeyUse.Used;
        }

        if (Corkboard.IsProjectBoard(name))
        {
            ChoiceBox.Message(Lang.T("Rename board"), Lang.T("The board of a project has the name of its folder"),
                string.Empty);
            return PickListBox.KeyUse.Used;
        }

        var newName = ChoiceBox.AskText(Lang.T("Rename board"), DialogText.TypeNameAndPressEnter, DialogText.NameColon,
            name);
        if (newName == null) return PickListBox.KeyUse.Used;
        var result = Corkboard.RenameBoard(name, newName);
        if (result == DialogText.Success)
        {
            FileIo.SaveSettings();
            return PickListBox.KeyUse.Reload;
        }

        ChoiceBox.Message(Lang.T("Rename board"), result, string.Empty);
        return PickListBox.KeyUse.Used;
    }

    private static PickListBox.KeyUse DeleteBoard(string name)
    {
        ChoiceBox.Forget();
        if (name.Length == 0)
        {
            ChoiceBox.Message(Lang.T("Delete board"), Lang.T("The main board cannot be deleted"), string.Empty);
            return PickListBox.KeyUse.Used;
        }

        if (!ChoiceBox.ConfirmGuarded(Lang.T("Delete board"), Lang.F("Delete the board {0}?", BoardName(name)),
                Lang.T("The files stay where they are.")))
        {
            return PickListBox.KeyUse.Used;
        }

        var wasOpen = Corkboard.CurrentBoard == name;
        var path = Corkboard.BoardFile(name);
        var existed = File.Exists(path);
        var result = Corkboard.DeleteBoard(name, out var trashedPath);
        // (deleted for good it is logged where it is deleted)
        if (FileOperations.DeleteToTrash && existed)
        {
            EventLog.Add(DialogText.LogFile + "Delete (to the trash): " + path +
                         (trashedPath.Length > 0 ? " -> " + trashedPath : string.Empty) +
                         (result == DialogText.Success ? string.Empty : " - FAILED: " + result));
        }

        if (result != DialogText.Success)
        {
            ChoiceBox.Message(Lang.T("Delete board"), result, string.Empty);
            return PickListBox.KeyUse.Used;
        }

        // (the main board is open now: Corkboard.DeleteBoard opened it)
        if (wasOpen)
        {
            FileIo.SaveSettings();
            _filter = string.Empty;
            SelectLast();
        }

        return PickListBox.KeyUse.Reload;
    }

    #endregion

    // The cards whose file or folder is not there
    private static List<Corkboard.Card> Missing(MissingPlaces.Checker check) =>
        Corkboard.Cards.Where(card => IsMissing(card, check)).ToList();

    private static bool IsMissing(Corkboard.Card card, MissingPlaces.Checker check) =>
        card.HasPlace && check.IsMissing(card.Path, !card.IsFolder);

    // ":missing": the cards whose file or folder is gone, to select one
    private static void ShowMissing()
    {
        var check = new MissingPlaces.Checker();
        var missing = Missing(check);
        if (missing.Count == 0)
        {
            _message = MissingPlaces.NothingMissing;
            return;
        }

        Draw();
        var picked = MissingPlaces.Pick(missing.Select(card => (card.Name, card.Path, check.OnAbsentDrive(card.Path)))
            .ToList());
        if (picked >= 0) _selected = missing[picked].Number;
    }

    // ":prune": take the cards whose file or folder is gone off the board, after asking. Those on a drive that is not connected
    // stay
    private static void Prune()
    {
        var check = new MissingPlaces.Checker();
        var (prune, kept) = MissingPlaces.Sort(Missing(check), card => check.OnAbsentDrive(card.Path));
        if (prune.Count == 0)
        {
            _message = MissingPlaces.NothingToPrune(kept.Count);
            return;
        }

        Draw();
        if (!MissingPlaces.Confirm(Lang.T("Remove card"), prune.Count, kept.Count, 0, false)) return;
        var count = Corkboard.RemoveMany(prune);
        if (Selected == null) SelectLast();
        _message = Lang.F("Cards removed: {0}", count);
        SaveCards();
    }

    #region Drawing

    /// <summary>
    /// The title in the top bar: the board (null: the main board) and the mode, which is always shown whole. Where
    /// they do not fit in the room, "Corkboard" is left out before the name of the board, and then the name is cut
    /// </summary>
    internal static string TopBarTitle(string? board, string mode, int room)
    {
        var tail = " | " + mode;
        var whole = (board == null ? Lang.T("Corkboard") : Lang.F("{0}: {1}", Lang.T("Corkboard"), board)) + tail;
        if (board == null || TextMetrics.WidthOf(whole) <= room) return whole;
        var left = room - TextMetrics.WidthOf(tail);
        if (left < 4) return whole; // (no room for a name: cut at the end as before)
        return Tools.TruncateString(board, left, true) + tail;
    }

    private static void Draw()
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        // The board, and what the keys select
        Menu.WriteModeLabel(TopBarTitle(Corkboard.CurrentBoard.Length > 0 ? Corkboard.NameOf(Corkboard.CurrentBoard) : null,
            ModeName(Mode), Menu.CaptionRoom), string.Empty, 0);
        Menu.WriteNameInTopBar();
        // A ruler of the columns of the board, as above the logo of the logo editor (to line the cards up)
        Tools.SetCursorEx(1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(Tools.Ruler(Init.LineWidth - 1));

        var width = Corkboard.BoardWidth;
        var check = new MissingPlaces.Checker();
        var selected = Selected;
        // The dividers first, behind the cards; the column of the selected one shown on the ruler
        foreach (var divider in Corkboard.Cards.Where(card => card.IsDivider))
        {
            DrawDivider(divider, divider == selected, width);
        }

        foreach (var card in Corkboard.Cards.Where(card => !card.IsDivider && card != selected)
                     .Append(selected is { IsDivider: false } ? selected : null))
        {
            if (card != null) DrawCard(card, card == selected, IsMissing(card, check), width, Height);
        }

        if (Corkboard.Cards.Count == 0) DrawFirstKeys(width);

        if (PeekOn) DrawPeek(selected);
        DrawStatus();
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Menu.WriteKeyHints(_moving
            ? Lang.T("hjkl Move | HJKL Move 5 | Enter/Esc Done")
            : Lang.T("Tab/hjkl Select | M Mode | Enter Open | Space Mark | V Move | A Note | B Badge | N New | C Color | X Title | I Info | P Peek | Esc Close"));
    }

    // A divider: a line from the top of the board to the bottom at its column (a marked one with a "*" at its top), and
    // the column of the selected one highlighted on the ruler
    private static void DrawDivider(Corkboard.Card divider, bool selected, int width)
    {
        var (x, _) = Corkboard.Shown(divider, width, Height);
        if (selected)
        {
            Tools.SetCursorEx(1, x);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
            Console.Write(Tools.Ruler(x + 1)[x]);
        }

        for (var row = 0; row < Height; row++)
        {
            Tools.SetCursorEx(BoardTop + row, x);
            if (row == 0 && IsMarked(divider)) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            else if (selected && _moving) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            else if (selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            else if (divider.Color is { } color) ConsoleColors.SetTextColor(color);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(row == 0 && IsMarked(divider) ? '*' : '|');
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // An empty board: the keys to start with, in a box in its middle (or a line, on a board too low for it)
    private static void DrawFirstKeys(int width)
    {
        // English: the keys
        var rows = new (string Keys, string Text)[]
        {
            ("N F", Lang.T("Pin a file / a folder")),
            ("A B |", Lang.T("Add a note / a badge / a divider")),
            ("Tab hjkl", Lang.T("Select")),
            ("M", Lang.T("Select cards, badges, dividers or all")),
            ("Space V", Lang.T("Mark / Move (hjkl, HJKL: 5 steps)")),
            ("T E", Lang.T("On top of / below the others")),
            ("C X D u", Lang.T("Color / Title / Remove / Put back")),
            ("?", Lang.T("All keys"))
        };
        if (Height < rows.Length + 2 || width < 40)
        {
            Tools.SetCursorEx(BoardTop + 1, 2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Tools.TruncateString(Lang.T("No cards yet: :pin a file in the file browser or the editor, or N here"),
                width - 4, true));
            return;
        }

        const int keysWidth = 10;
        var inner = Math.Min(width - 4, 58);
        var left = (width - inner - 2) / 2;
        var top = BoardTop + (Height - rows.Length - 2) / 2;
        var title = Tools.TruncateString(" " + Lang.T("An empty corkboard") + " ", inner - 2, true);
        Tools.SetCursorEx(top, left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(AsciiStyles.GetTopCorner() + "-" + title +
                      new string('-', Math.Max(0, inner - 1 - TextMetrics.WidthOf(title))) + AsciiStyles.GetTopCorner());
        for (var i = 0; i < rows.Length; i++)
        {
            Tools.SetCursorEx(top + 1 + i, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write("| ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            Console.Write(rows[i].Keys.PadRight(keysWidth));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var text = Tools.TruncateString(rows[i].Text, inner - keysWidth - 2, true);
            Console.Write(text + new string(' ', Math.Max(0, inner - keysWidth - 2 - TextMetrics.WidthOf(text))));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" |");
        }

        Tools.SetCursorEx(top + 1 + rows.Length, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawCard(Corkboard.Card card, bool selected, bool missing, int width, int height)
    {
        var (x, y) = Corkboard.Shown(card, width, height);
        var marked = IsMarked(card);
        if (card.IsBadge) DrawBadgeAt(card, BoardTop + y, x, Init.LineWidth, selected, !Matches(card), marked);
        else DrawCardAt(card, BoardTop + y, x, Init.LineWidth, selected, missing || !Matches(card), missing, marked,
            selected && _moving ? (x, y) : null);
    }

    /// <summary>
    /// Where a card is, as it is shown on the selected one: its column on the ruler and its row on the board, both
    /// counted from 1
    /// </summary>
    internal static string PlaceText(int x, int y) => (x + 1) + "," + (y + 1);

    /// <summary>
    /// The top edge of the card being moved: its place at the right end ("-- 26,4 -"), where it fits
    /// </summary>
    internal static string TopEdge(Corkboard.Card card, int inner, (int X, int Y) place, bool selected = false)
    {
        var edge = TopEdge(card, inner, selected);
        var text = " " + PlaceText(place.X, place.Y) + " ";
        // (the start of the edge stays: a folder's tab)
        if (text.Length + FolderTab.Length + 1 > inner) return edge;
        return edge[..(inner - text.Length - 1)] + text + edge[^1];
    }

    /// <summary>
    /// The top edge of a badge, between its corners: its number on it ("###" for one too long for it, rather than
    /// a part that could be the number of another card)
    /// </summary>
    internal static string BadgeEdge(int number, bool selected = false)
    {
        var digits = number.ToString();
        var line = selected ? ":" : "-";
        return digits.Length switch
        {
            1 => line + digits + line,
            2 => digits + line,
            3 => digits,
            _ => "###"
        };
    }

    // A badge at a place on the screen, cut at a column: small, with its number on the top edge and an "i" (English:
    // the same in all languages), its corners in the style of the boxes. A marked one has a "*" at its top left
    private static void DrawBadgeAt(Corkboard.Card card, int row, int column, int end, bool selected, bool dim,
        bool marked)
    {
        void Frame()
        {
            if (selected && _moving) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            else if (selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            else if (dim || card.Color == null) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else ConsoleColors.SetTextColor(card.Color.Value);
        }

        // Writes what fits before the end, from a column
        void Put(int at, string text)
        {
            if (at >= end) return;
            Tools.SetCursorEx(row, at);
            Console.Write(text.Length > end - at ? text[..(end - at)] : text);
        }

        var top = AsciiStyles.GetTopCorner().ToString();
        var bottom = AsciiStyles.GetBottomCorner().ToString();
        if (marked) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        else Frame();
        Put(column, marked ? "*" : top);
        ConsoleColors.SetColors(selected ? ConsoleColors.ColorPreset.SelectorInverted : ConsoleColors.ColorPreset.Divider);
        Put(column + 1, BadgeEdge(card.Number, selected));
        Frame();
        Put(column + 4, top);
        row++;
        // (the selected badge is edged in ":", as the selected card)
        var side = selected ? ":" : "|";
        Put(column, side + " ");
        if (dim) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        else if (card.Color is { } color) ConsoleColors.SetTextColor(color);
        else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Put(column + 2, "i"); // English: the mark of a badge
        Frame();
        Put(column + 3, " " + side);
        row++;
        Put(column, bottom + (selected ? ":::" : "---") + bottom);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The tab at the start of the top edge of a folder's card
    private const string FolderTab = "--<>";

    // The gaps in the edges of a note
    private const string NoteGap = " - - - ";

    // The edge of a note: a line with gaps in it ("--- - - - ---"), from a column of the edge (left as a line where
    // they do not fit)
    private static string Dashed(int inner, int at)
    {
        var edge = new string('-', Math.Max(0, inner));
        if (at < 1 || at + NoteGap.Length >= inner) return edge;
        return edge[..at] + NoteGap + edge[(at + NoteGap.Length)..];
    }

    /// <summary>
    /// The top edge of a card, between its corners: a note's a line with gaps near its right end ("--- - - - ----"), a file's is a line ("---"), and a
    /// folder's has a tab at its start ("--<>--"). The selected card's is of ":" (a folder keeping its tab), to be seen
    /// among cards of many colors
    /// </summary>
    internal static string TopEdge(Corkboard.Card card, int inner, bool selected = false)
    {
        var line = selected ? ':' : '-';
        if (card.IsFolder)
            return (FolderTab.Replace('-', line) + new string(line, Math.Max(0, inner - FolderTab.Length)))[..Math.Max(0, inner)];
        return card.IsNote && !selected ? Dashed(inner, inner - NoteGap.Length - 4) : new string(line, Math.Max(0, inner));
    }

    /// <summary>
    /// The bottom edge of a card, between its corners: a note's a line with gaps near its left end ("--- - - - ----"), a file's and a folder's a line, the
    /// selected card's of ":"
    /// </summary>
    internal static string BottomEdge(Corkboard.Card card, int inner, bool selected = false) =>
        selected ? new string(':', inner) : card.IsNote ? Dashed(inner, 3) : new string('-', inner);

    /// <summary>
    /// The sides of a card: ":" for the selected card, "|" for the others
    /// </summary>
    internal static string Side(Corkboard.Card card, bool selected = false) => selected ? ":" : "|";

    // A card at a place on the screen, cut at a column (the part from it on is not drawn). A marked one has a "*" at
    // its top left
    private static void DrawCardAt(Corkboard.Card card, int row, int column, int end, bool selected, bool dim,
        bool missing, bool marked = false, (int X, int Y)? place = null)
    {
        var inner = Corkboard.CardWidth - 2;
        var at = column;

        // Writes what fits before the end
        void Put(string text)
        {
            var room = end - at;
            if (room <= 0) return;
            var part = TextMetrics.WidthOf(text) > room ? Tools.TruncateString(text, room, false) : text;
            Console.Write(part);
            at += TextMetrics.WidthOf(part);
        }

        void Frame()
        {
            if (selected && _moving) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            else if (selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            else if (dim || card.Color == null) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else ConsoleColors.SetTextColor(card.Color.Value);
        }

        if (column >= end) return;
        var top = AsciiStyles.GetTopCorner();
        // (the selected card shows its place on its top edge)
        var edge = place is { } shownAt ? TopEdge(card, inner, shownAt, selected) : TopEdge(card, inner, selected);
        var side = Side(card, selected);
        Tools.SetCursorEx(row, column);
        if (marked)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Put("*");
            Frame();
            Put(edge + top);
        }
        else
        {
            Frame();
            Put(top + edge + top);
        }

        Tools.SetCursorEx(row + 1, column);
        at = column;
        Put(side);
        // The number (highlighted on the selected card), and the name
        var number = " " + card.Number + " ";
        ConsoleColors.SetColors(selected ? ConsoleColors.ColorPreset.SelectorInverted : ConsoleColors.ColorPreset.Divider);
        Put(number);
        if (dim) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        else if (card.Color is { } color) ConsoleColors.SetTextColor(color);
        else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        var name = Tools.TruncateString(" " + card.Name + (missing ? " " + DialogText.FavoriteMissing : string.Empty),
            inner - TextMetrics.WidthOf(number), true);
        Put(name + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(number) - TextMetrics.WidthOf(name))));
        Frame();
        Put(side);
        var bottom = AsciiStyles.GetBottomCorner();
        Tools.SetCursorEx(row + 2, column);
        at = column;
        Put(bottom + BottomEdge(card, inner, selected) + bottom);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The cards of a board beside the list of projects: its name (with how many cards it has), and the cards by
    /// their numbers laid on each other, each a step to the right and down, as many as there is room for (cut at the
    /// right side). Null cards: the project has no board. Not shown: the column is left empty (no project, or one
    /// that is missing)
    /// </summary>
    internal static void DrawSnapshot(List<Corkboard.Card>? cards, int top, int left, int width, int height,
        bool shown = true)
    {
        var blank = new string(' ', Math.Max(0, width));
        for (var row = 0; row < height; row++)
        {
            Tools.SetCursorEx(top + row, left);
            Console.Write(blank);
        }

        if (!shown) return;
        Tools.SetCursorEx(top, left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        var label = cards == null ? Lang.T("No corkboard: B makes one")
            : Lang.F("Corkboard ({0})", cards.Count);
        Console.Write(Tools.TruncateString(label, width, true));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (cards == null) return;
        for (var i = 0; i < cards.Count; i++)
        {
            var row = top + 1 + i * 2;
            if (row + Corkboard.CardHeight > top + height) break;
            if (cards[i].IsBadge) DrawBadgeAt(cards[i], row, left + i * 2, left + width, false, false, false);
            else DrawCardAt(cards[i], row, left + i * 2, left + width, false, false, false);
        }
    }

    // The peek: the start of the selected card's file (or the text of a note), below the board
    private static void DrawPeek(Corkboard.Card? card)
    {
        var row = Init.LineCount - 2 - PeekRows;
        Tools.SetCursorEx(row, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        var label = Tools.TruncateString("-- " + Lang.T("Peek") + (card != null ? ": " + card.Name : string.Empty) + " ",
            Init.LineWidth - 1, true);
        Console.Write(label + new string('-', Math.Max(0, Init.LineWidth - 1 - TextMetrics.WidthOf(label))));
        if (card == null) return;
        var lines = card.IsDivider ? new List<string>()
            : card.IsNote ? card.Text.Split('\n').ToList()
            : NotePanes.PeekPlace(card.Path, PeekRows - 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        for (var i = 0; i < PeekRows - 1 && i < lines.Count; i++)
        {
            Tools.SetCursorEx(row + 1 + i, 1);
            Console.Write(Tools.TruncateString(lines[i].Replace('\t', ' '), Init.LineWidth - 3, true));
        }
    }

    // The place of a badge being moved (as the place on the top edge of a card: a badge has no room for it)
    private static string BadgePlace(Corkboard.Card badge)
    {
        var (x, y) = Corkboard.Shown(badge, Corkboard.BoardWidth, Height);
        return Lang.F("Badge at {0}", PlaceText(x, y));
    }

    // The line above the keys: a message, the number typed or the filter, and how many cards there are
    private static void DrawStatus()
    {
        var row = Init.LineCount - 2;
        Tools.SetCursorEx(row, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth - 1));
        var text = _message.Length > 0 ? _message
            : _typed.Length > 0 ? Lang.F("Card: {0}", _typed)
            : _filter.Length > 0 ? Lang.F("Filter: {0}", _filter)
            : _moving && Group() is { Count: > 1 } group ? Lang.F("Moving: {0} cards", group.Count)
            : _moving && Selected is { IsBadge: true } badge ? BadgePlace(badge)
            : _moving && Selected is { IsDivider: true } divider
                ? Lang.F("Divider at {0}", Corkboard.Shown(divider, Corkboard.BoardWidth, Height).X + 1)
            : _moving && Selected is { } moving ? Lang.F("Moving: {0}", moving.Name)
            : _marks.Count > 0 ? Lang.F("Marked: {0}", _marks.Count)
            : string.Empty;
        // How many cards there are, and badges and dividers (when there are any)
        var badges = Corkboard.Cards.Count(card => card.IsBadge);
        var dividers = Corkboard.Cards.Count(card => card.IsDivider);
        var count = " " + Lang.F("Cards: {0}", Corkboard.Cards.Count - badges - dividers) +
                    (badges > 0 ? " | " + Lang.F("Badges: {0}", badges) : string.Empty) +
                    (dividers > 0 ? " | " + Lang.F("Dividers: {0}", dividers) : string.Empty) + " ";
        if (text.Length > 0)
        {
            Tools.SetCursorEx(row, 2);
            ConsoleColors.SetColors(_message.Length > 0
                ? ConsoleColors.ColorPreset.AttentionInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + Tools.TruncateString(text, Init.LineWidth - TextMetrics.WidthOf(count) - 10, true) + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(row, Math.Max(0, Init.LineWidth - 3 - TextMetrics.WidthOf(count)));
        Console.Write(count);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
