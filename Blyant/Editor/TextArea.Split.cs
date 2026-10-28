namespace Blyant;

/// <summary>
/// A split (":split"): the text in two panes side by side, with a column of "|" between them. The pane with the
/// focus gets the keys and is the one the top row, the mode on the divider (over that pane), the status line and the
/// command line are about; the other is drawn as it is, and follows the changes (EditorEngine.Split.cs). Tab and
/// Shift+Tab in Normal mode change the focus, ":q" closes the pane with the focus and ":only" the other one. The
/// pane with the focus is drawn with the fields of the text area (_engine, _left, _width ...); the other pane keeps
/// its own, and they are swapped when the focus changes.
///
/// ":split name" shows another file at the right instead, read only, to read while writing (an outline, notes): it
/// has a text of its own, and Tab goes into it as into the peek of the file browser (the keys to move, search and
/// yank; Tab or Esc back, Enter opens it in the editor). The text being edited is the only one saved or closed:
/// ":q" closes the file at the right first
/// </summary>
public partial class TextArea
{
    // The other pane of a split: its editor, and where it is and what is shown of it
    private sealed class Pane
    {
        internal EditorEngine Engine = null!;
        internal int Left;
        internal int Width;
        internal int TextWidth;
        internal int Gutter;
        internal string?[] ShownRows = Array.Empty<string?>();
        internal int[] ShownCells = Array.Empty<int>();
    }

    private Pane? _otherPane;

    // The file shown read only in the other pane (":split name"), or null when the split shows this text
    private string? _referencePath;

    // The place of the text area when it is not split
    private int _wholeLeft;
    private int _wholeWidth;

    // ":split": two panes, or one again (as ":only"); ":split name": a file read only in the other pane
    private void Split()
    {
        if (_engine.SplitArgument.Length > 0) ShowReference(_engine.SplitArgument);
        else SplitText();
    }

    // ":view": a file picked in the file browser (starting next to the file being edited) is shown read only at the
    // right, as ":split name" shows it; ":view name" shows that file. Esc in the browser keeps the text as it was
    private void View()
    {
        if (_peek != null || _singleLine) return;
        if (_engine.SplitArgument.Length > 0)
        {
            ShowReference(_engine.SplitArgument);
            return;
        }

        Tools.HideCursor();
        var picked = FileExplorer.PickFile(Lang.T("View beside the text"),
            FileExplorer.CurrentFilePath.Length > 0 ? FileExplorer.CurrentFilePath : null);
        RedrawAll();
        if (picked != null) ShowReference(picked);
    }

    // The text in two panes, or one again (as ":only")
    private void SplitText()
    {
        if (_otherPane != null)
        {
            CloseOtherPane();
            return;
        }

        // (A peek refuses the request, and a field of one line does not make it)
        if (_peek != null || _singleLine) return;

        _wholeLeft = _left;
        _wholeWidth = _width;
        // The pane with the focus at the left, the new one at the right, and a column between them
        var leftWidth = (_wholeWidth - 1) / 2;
        var rightWidth = _wholeWidth - leftWidth - 1;
        SetPlace(_wholeLeft, leftWidth);
        var other = new EditorEngine(_engine, rightWidth - 1, _height);
        _engine.InSplit = true;
        other.InSplit = true;
        other.StartFollowing();
        _otherPane = new Pane
        {
            Engine = other,
            Left = _wholeLeft + leftWidth + 1,
            Width = rightWidth,
            TextWidth = rightWidth - 1,
            Gutter = -1,
            ShownRows = new string?[_height],
            ShownCells = new int[_height]
        };
        RedrawAll();
        _engine.SetMessage(Lang.T("Tab: the other pane, :only: one pane"), false);
    }

    /// <summary>
    /// The other pane of a split, when the editor is closed: the index of its cursor, and whether it is at the
    /// left. Null when not split
    /// </summary>
    internal (int Index, bool OtherLeft)? SplitPlace =>
        _otherPane == null || _referencePath != null ? null : (_otherPane.Engine.GetCursorIndex(), _otherPane.Left < _left);

    // Opened split, as it was when it was closed: the other pane at its cursor, at the left or the right
    private void RestoreSplit(int index, bool otherLeft)
    {
        SplitText();
        if (_otherPane == null) return;
        var other = _otherPane.Engine;
        other.SetCursorIndex(index);
        other.StartFollowing();
        if (otherLeft)
        {
            // The panes change places (each keeps its editor)
            (_left, _otherPane.Left) = (_otherPane.Left, _left);
            (_width, _otherPane.Width) = (_otherPane.Width, _width);
            (_textWidth, _otherPane.TextWidth) = (_otherPane.TextWidth, _textWidth);
            _gutter = -1;
            _otherPane.Gutter = -1;
            RedrawAll();
        }
    }

    // Tab (Shift+Tab): the other pane gets the keys
    private void SwitchPane()
    {
        if (_otherPane == null)
        {
            _engine.SetMessage(Lang.T("One pane: :split shows the text in two"), false);
            return;
        }

        if (_referencePath != null)
        {
            FocusReference();
            return;
        }

        _engine.StartFollowing();
        SwapPanes();
        _engine.StopFollowing();
        _engine.TakeSmallRegister(_otherPane.Engine);
        // The mode is written over this pane, and the cursor is shown in it
        _shownModeLabel = null;
        _shownStatus = null;
    }

    // ":q" in a split (also ":q!" and ZQ; ":wq", ":x" and ZZ save first): the pane with the focus is closed, and the
    // other one shows the text
    private void ClosePane()
    {
        if (_otherPane == null) return;
        // The file at the right is closed first: the text being edited stays
        if (_referencePath != null)
        {
            CloseOtherPane();
            return;
        }

        _engine.StartFollowing();
        SwapPanes();
        _engine.StopFollowing();
        _engine.TakeSmallRegister(_otherPane.Engine);
        CloseOtherPane();
    }

    // A box was drawn on top of the text: in a split it may cover both panes, which are drawn again with the whole
    // screen (with one pane, only the rows it covered)
    private bool _redrawWhole;

    private void Uncover(int firstRow, int rows, int right)
    {
        if (_otherPane != null)
        {
            _redrawWhole = true;
            return;
        }

        for (var r = firstRow; r < firstRow + rows && r < _height; r++)
        {
            _shownRows[r] = null;
            _shownCells[r] = Math.Max(_shownCells[r], right);
        }
    }

    // ":only": the other pane is closed, and the one with the focus takes the whole width
    private void CloseOtherPane()
    {
        if (_otherPane == null)
        {
            _engine.SetMessage(Lang.T("One pane: :split shows the text in two"), false);
            return;
        }

        _otherPane.Engine.Detach();
        _otherPane = null;
        _referencePath = null;
        _engine.InSplit = false;
        SetPlace(_wholeLeft, _wholeWidth);
        RedrawAll();
    }

    // The place of the pane with the focus: the text is wrapped in its width again (the line numbers are found
    // again, UpdateGutter)
    private void SetPlace(int left, int width)
    {
        _left = left;
        _width = width;
        _textWidth = width - 1;
        _gutter = -1;
    }

    // The pane with the focus and the other one change places in the fields
    private void SwapPanes()
    {
        var other = _otherPane!;
        (_engine, other.Engine) = (other.Engine, _engine);
        (_left, other.Left) = (other.Left, _left);
        (_width, other.Width) = (other.Width, _width);
        (_textWidth, other.TextWidth) = (other.TextWidth, _textWidth);
        (_gutter, other.Gutter) = (other.Gutter, _gutter);
        (_shownRows, other.ShownRows) = (other.ShownRows, _shownRows);
        (_shownCells, other.ShownCells) = (other.ShownCells, _shownCells);
    }

    // The rows of the other pane, as they are (no mode, status line or cursor of its own)
    private void DrawOtherPane()
    {
        if (_otherPane == null) return;
        _otherPane.Engine.UpdateFollowingView();
        SwapPanes();
        try
        {
            DrawRows();
        }
        finally
        {
            SwapPanes();
        }
    }

    // After the whole screen is drawn again: the other pane is written again, and the column between the panes
    private void RedrawSplit()
    {
        if (_otherPane == null) return;
        Array.Fill(_otherPane.ShownRows, null);
        Array.Fill(_otherPane.ShownCells, 0);
        // Just before the pane at the right
        var column = Math.Max(_left, _otherPane.Left) - 1;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        for (var r = 0; r < _height; r++)
        {
            Tools.SetCursorEx(_top + r, column);
            Console.Write('|');
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // ":split name": the file read only in the other pane, at the right (a split of the text is closed first). A path
    // is from the directory of the file being edited; the file itself is shown as ":split" shows it
    private void ShowReference(string name)
    {
        if (_peek != null || _singleLine) return;
        var directory = FileExplorer.CurrentFilePath.Length > 0
            ? Path.GetDirectoryName(FileExplorer.CurrentFilePath) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;
        string path;
        TextFile file;
        try
        {
            path = Path.GetFullPath(name, directory);
            if (Directory.Exists(path))
            {
                _engine.SetMessage(Lang.T("A directory: :split takes a file"), true);
                return;
            }

            if (!File.Exists(path))
            {
                _engine.SetMessage(DialogText.FileNotFound + ": " + name, true);
                return;
            }

            file = TextFile.Load(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                      or NotSupportedException)
        {
            _engine.SetMessage(e.Message, true);
            return;
        }

        if (file.Text.Contains('\0'))
        {
            _engine.SetMessage(DialogText.PeekNotText + ": " + name, true);
            return;
        }

        if (_otherPane != null) CloseOtherPane();
        if (_saveMode == SaveAsFile && FileOperations.SamePath(path, FileExplorer.CurrentFilePath))
        {
            // The file being edited: the text in two panes
            SplitText();
            return;
        }

        _wholeLeft = _left;
        _wholeWidth = _width;
        var leftWidth = (_wholeWidth - 1) / 2;
        var rightWidth = _wholeWidth - leftWidth - 1;
        SetPlace(_wholeLeft, leftWidth);
        var text = FileExplorer.PeekText(file.Text);
        var type = SyntaxHighlighter.TypeOfFile(Path.GetFileName(path), text);
        var reference = new EditorEngine(text, rightWidth - 1, _height, false)
        {
            Wrap = WordWrap,
            ReadOnly = true,
            SyntaxDefault = type,
            Syntax = SyntaxHighlighter.ColorFiles ? type : SyntaxType.None
        };
        _engine.InSplit = true;
        _referencePath = path;
        _otherPane = new Pane
        {
            Engine = reference,
            Left = _wholeLeft + leftWidth + 1,
            Width = rightWidth,
            TextWidth = rightWidth - 1,
            Gutter = -1,
            ShownRows = new string?[_height],
            ShownCells = new int[_height]
        };
        RedrawAll();
        _engine.SetMessage(Lang.T("Tab: the file at the right (read only), :only: one pane"), false);
    }

    // Tab with a file at the right: the keys go to it, read only (as the peek of the file browser), until Tab or Esc;
    // the place it was left at is kept. Enter opens it in the editor, at the cursor (asking about changes first)
    private void FocusReference()
    {
        var pane = _otherPane!;
        var path = _referencePath!;
        var text = pane.Engine.Buffer.GetText();
        Tools.HideCursor();
        // The divider tells that the keys go to the file at the right: its name is highlighted, and the mode of
        // the text at the left is not shown
        Tools.SetCursorEx(_top - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        DrawReferenceName(true);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        var peek = new TextArea(pane.Left, _top, pane.Width, _height, text, pane.Engine.GetCursorIndex(), false, false,
            false, SaveAsFile, false, new PeekOptions(pane.Engine.SyntaxDefault, RedrawAll)
            {
                NoWrap = !WordWrap,
                TabCloses = true,
                Hint = Lang.F("{0}: read only, Tab back, Enter opens it", Path.GetFileName(path)),
                ReadOnlyText = Lang.T("Read only: Enter opens the file in the editor")
            });
        _shownModeLabel = null;
        var index = peek.GetCursorIndex();
        pane.Engine.SetCursorIndex(index);
        Tools.HideCursor();
        if (peek.OpenRequested)
        {
            // The file in the editor in place of this one, at the cursor
            Jump.ToPlace(new JumpTarget(JumpKind.File, path) { At = CaretPosition.Of(text, index) }, LeaveForJump);
        }

        RedrawAll();
    }

    // The name of the file read only at the right, on the divider over it: highlighted while it has the keys
    private void DrawReferenceName(bool focused)
    {
        if (_referencePath == null || _otherPane == null) return;
        var name = Tools.TruncateString(" " + Lang.F("{0}, read only", Path.GetFileName(_referencePath)) + " ",
            Math.Max(0, _otherPane.Width - 2), true);
        Tools.SetCursorEx(_top - 1,
            Math.Max(0, _otherPane.Left + _otherPane.Width / 2 - TextMetrics.WidthOf(name) / 2));
        ConsoleColors.SetColors(focused ? ConsoleColors.ColorPreset.EditorTitle : ConsoleColors.ColorPreset.OtherTabs);
        Console.Write(name);
    }

    /// <summary>
    /// The names of the files in the directory of the file being edited, for Tab after ":split "
    /// </summary>
    internal static IEnumerable<string> SplitFileNames()
    {
        try
        {
            var directory = FileExplorer.CurrentFilePath.Length > 0
                ? Path.GetDirectoryName(FileExplorer.CurrentFilePath)
                : Environment.CurrentDirectory;
            if (directory == null) return Array.Empty<string>();
            return new DirectoryInfo(directory).EnumerateFiles()
                .Where(info => (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .Select(info => info.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    // The column the mode is centered on: the middle of the pane with the focus (of the window when not split)
    private int ModeCenter => _otherPane == null ? Init.LineWidth / 2 : _left + _width / 2;
}
