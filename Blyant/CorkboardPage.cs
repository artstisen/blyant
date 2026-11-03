namespace Blyant;

/// <summary>
/// The page of the corkboard (Corkboard): the cards drawn where they are placed, the selected one on top. A card
/// is selected with Tab, its number and Enter, or the keys of a direction (the nearest card that way), and opened
/// with Enter. V moves it, T and E put it on top of the others or below them, C colors it, X gives it a title, D
/// takes it off the board and N pins another file. Opened with ":board" from every command line, ":jump board",
/// the Jump menu and the Start tab
/// </summary>
internal static class CorkboardPage
{
    private const int BoardTop = 2; // Below the top bar and its line

    private static bool _open; // The page is open (it is not opened again on top of itself)
    private static int _selected = -1; // The number of the selected card
    private static bool _moving; // The selected card is moved with the keys of the directions
    private static string _typed = string.Empty; // The number typed to select a card
    private static string _filter = string.Empty; // Only the cards with it in their names or paths are not dimmed
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
        }
    }

    private static Corkboard.Card? Selected => Corkboard.Numbered(_selected);

    // The cards the filter leaves (all without a filter), in the order they are drawn
    private static List<Corkboard.Card> Matching() => Corkboard.Cards.Where(Matches).ToList();

    private static bool Matches(Corkboard.Card card) => _filter.Length == 0 ||
                                                        card.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                                                        card.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private static void Run()
    {
        Corkboard.Load();
        if (Selected == null) _selected = Corkboard.Cards.Count > 0 ? Corkboard.Cards[^1].Number : -1;
        while (true)
        {
            Init.ApplyAutoSize();
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
                Corkboard.Load();
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

            if (key.Key == ConsoleKey.Escape && (_typed.Length > 0 || _filter.Length > 0))
            {
                // Esc takes the number typed away first, then the filter
                if (_typed.Length > 0) _typed = string.Empty;
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
            if (Corkboard.Nearest(card, columns, rows, Corkboard.BoardWidth, Corkboard.BoardHeight) is { } next)
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
                if (card != null)
                {
                    _moving = true;
                    // (on top while it is moved, as it is drawn)
                }

                break;
            case ConsoleKey.T:
            case ConsoleKey.E:
                if (card == null) break;
                Corkboard.ToFront(card, key.Key == ConsoleKey.T);
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

    // Tab and Shift+Tab: the next/previous card by number (of those the filter leaves), going round
    private static void SelectNext(bool back)
    {
        var numbers = Matching().Select(card => card.Number).Order().ToList();
        if (numbers.Count == 0) return;
        var at = numbers.IndexOf(_selected);
        _selected = at < 0 ? numbers[0] : numbers[(at + (back ? numbers.Count - 1 : 1)) % numbers.Count];
    }

    // V: the selected card is moved with the keys of the directions (H J K L: five steps), until Esc or Enter
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
        Corkboard.Move(card, move.Columns, move.Rows, Corkboard.BoardWidth, Corkboard.BoardHeight);
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

    // Enter: open the file of the card in the editor (as a jump: the page is left)
    private static void Open(Corkboard.Card? card)
    {
        if (card == null) return;
        if (!File.Exists(card.Path))
        {
            _message = DialogText.FileNotFound;
            return;
        }

        Jump.ToPlace(new JumpTarget(JumpKind.File, card.Path));
    }

    private static void PickColor(Corkboard.Card? card)
    {
        if (card == null) return;
        if (!ColorPicker.Pick(DialogText.ColorCaption, (Init.LineWidth - 26) / 2, true, card.Color, null, null,
                out var picked))
        {
            return;
        }

        card.Color = picked;
        SaveCards();
    }

    // X or ":rename title": a title of its own for the card (the file keeps its name)
    private static void Rename(Corkboard.Card? card, string title)
    {
        if (card == null) return;
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

    // D or ":remove": take the card off the board, after asking (u puts it back)
    private static void Remove(Corkboard.Card? card)
    {
        if (card == null) return;
        ChoiceBox.Forget();
        if (!ChoiceBox.Confirm(Lang.T("Remove card"), Lang.F("Take card {0} off the corkboard?", card.Number),
                Lang.T("The file stays where it is."), false))
        {
            return;
        }

        Corkboard.Remove(card);
        _selected = Corkboard.Cards.Count > 0 ? Corkboard.Cards[^1].Number : -1;
        _message = Lang.F("Removed: {0} (u puts it back)", card.Name);
        SaveCards();
    }

    private static void Undo()
    {
        if (Corkboard.Undo() is not { } card)
        {
            _message = Lang.T("Nothing to put back");
            return;
        }

        _selected = card.Number;
        _message = Lang.F("Put back: {0}", card.Name);
        SaveCards();
    }

    // N or ":new": pin a file picked in the file browser
    private static void PinFile()
    {
        var path = FileExplorer.PickFile(Lang.T("Pin a file to the corkboard"));
        Corkboard.Load();
        if (path == null) return;
        _message = Corkboard.PinWithMessage(path);
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
                Corkboard.Load();
                break;
            case "book":
                Jump.ToBookmark(CommandLine.Argument(command));
                Corkboard.Load();
                break;
            case "recent":
                RecentBox.Open();
                break;
            case "new":
                PinFile();
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
                if (Selected is not { } card) break;
                Corkboard.ToFront(card, name == "top");
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

    // The cards whose file is not there
    private static List<Corkboard.Card> Missing(MissingPlaces.Checker check) =>
        Corkboard.Cards.Where(card => check.IsMissing(card.Path, true)).ToList();

    // ":missing": the cards whose file is gone, to select one
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

    // ":prune": take the cards whose file is gone off the board, after asking. Those on a drive that is not connected
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
        if (Selected == null) _selected = Corkboard.Cards.Count > 0 ? Corkboard.Cards[^1].Number : -1;
        _message = Lang.F("Cards removed: {0}", count);
        SaveCards();
    }

    #region Drawing

    private static void Draw()
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(Lang.T("Corkboard"), string.Empty, 0);
        Menu.WriteNameInTopBar();
        Tools.SetCursorEx(1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth - 1));

        var width = Corkboard.BoardWidth;
        var height = Corkboard.BoardHeight;
        var check = new MissingPlaces.Checker();
        var selected = Selected;
        foreach (var card in Corkboard.Cards.Where(card => card != selected).Append(selected))
        {
            if (card != null) DrawCard(card, card == selected, check.IsMissing(card.Path, true), width, height);
        }

        if (Corkboard.Cards.Count == 0)
        {
            Tools.SetCursorEx(BoardTop + 1, 2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Tools.TruncateString(Lang.T("No cards yet: :pin a file in the file browser or the editor, or N here"),
                width - 4, true));
        }

        DrawStatus();
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Menu.WriteKeyHints(_moving
            ? Lang.T("hjkl Move | HJKL Move 5 | Enter/Esc Done")
            : Lang.T("Tab Select | Enter Open | V Move | T/E Top/Bottom | C Color | X Title | D Remove | N New | Esc Close"));
    }

    private static void DrawCard(Corkboard.Card card, bool selected, bool missing, int width, int height)
    {
        var (x, y) = Corkboard.Shown(card, width, height);
        var inner = Corkboard.CardWidth - 2;
        var dim = missing || !Matches(card);

        void Frame()
        {
            if (selected && _moving) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            else if (selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            else if (dim || card.Color == null) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else ConsoleColors.SetTextColor(card.Color.Value);
        }

        Tools.SetCursorEx(BoardTop + y, x);
        Frame();
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        Tools.SetCursorEx(BoardTop + y + 1, x);
        Console.Write('|');
        // The number (highlighted on the selected card), and the name
        var number = " " + card.Number + " ";
        ConsoleColors.SetColors(selected ? ConsoleColors.ColorPreset.SelectorInverted : ConsoleColors.ColorPreset.Divider);
        Console.Write(number);
        if (dim) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        else if (card.Color is { } color) ConsoleColors.SetTextColor(color);
        else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        var name = Tools.TruncateString(" " + card.Name + (missing ? " " + DialogText.FavoriteMissing : string.Empty),
            inner - TextMetrics.WidthOf(number), true);
        Console.Write(name + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(number) - TextMetrics.WidthOf(name))));
        Frame();
        Console.Write('|');
        Tools.SetCursorEx(BoardTop + y + 2, x);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
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
            : _moving && Selected is { } moving ? Lang.F("Moving: {0}", moving.Name)
            : string.Empty;
        var count = " " + Lang.F("Cards: {0}", Corkboard.Cards.Count) + " ";
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
