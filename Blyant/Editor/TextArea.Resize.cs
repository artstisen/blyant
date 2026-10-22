using System.Runtime.InteropServices;

namespace Blyant;

/// <summary>
/// The editor follows the size of the terminal window while it waits for a key: when the size has stayed the same
/// for a moment (a window dragged larger passes many sizes), the editor takes the dimensions now in use (they follow
/// the window with automatic size, else the user is asked when the window is too small) and is drawn again. Keys
/// that are waiting (typing, pasting) are read at once. On Windows the input of the console is waited for (a key
/// ends the wait at once), and the size is asked for some times a second; elsewhere there is a short pause between
/// the questions (they take no time there: .NET keeps the size, and is told when it changes). Ctrl+C (cancelled in Init) is told about here: it does not close the editor. Only the editor of a file
/// or of the settings follows the window: a peek and a field of a box are drawn again by the page they are on
/// </summary>
public partial class TextArea
{
    // How long to wait between the questions for a key and the size of the window (on Windows: at most, as a key
    // ends the wait)
    private const int KeyWaitMilliseconds = 15;
    private const int WindowsWaitMilliseconds = 100;

    // How long the window keeps its new size before the editor follows it
    private const int SettleMilliseconds = 150;

    // The dimensions the editor is laid out for (Init.LineWidth, Init.LineCount), and the size of the window then
    private int _lineWidth;
    private int _lineCount;
    private (int Width, int Height) _windowSize;

    // The editor of a file or of the settings, on the whole screen
    private bool FollowsWindow => _peek == null && !_singleLine && _saveMode is SaveAsFile or SaveAsSettings;

    // When the editor opens: the dimensions it is laid out for (a Ctrl+C from before is forgotten)
    private void StartFollowingWindow()
    {
        _lineWidth = Init.LineWidth;
        _lineCount = Init.LineCount;
        _windowSize = WindowSize();
        Tools.TakeCancelKey();
    }

    // The size of the terminal window (the last one seen, if it is not known)
    private (int Width, int Height) WindowSize()
    {
        try
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }
        catch
        {
            return _windowSize;
        }
    }

    // The next key. Meanwhile the editor follows the size of the window, and tells how to close it after Ctrl+C
    private ConsoleKeyInfo ReadKey()
    {
        // (also between keys typed without a pause: a copy at least now and then)
        RecoveryTick();
        if (!FollowsWindow) return Console.ReadKey(true);
        var seen = _windowSize; // The new size, and when it was seen first
        var seenAt = 0L;
        while (true)
        {
            try
            {
                if (Console.KeyAvailable) break;
            }
            catch
            {
                // Keys cannot be asked about (input from a file): wait for one
                break;
            }

            if (Tools.TakeCancelKey())
            {
                _engine.SetMessage(DialogText.CancelKeyHint, false);
                Draw();
            }

            var size = WindowSize();
            if (size == _windowSize)
            {
                seenAt = 0;
            }
            else if (seenAt == 0 || size != seen)
            {
                seen = size;
                seenAt = Environment.TickCount64;
            }
            else if (Environment.TickCount64 - seenAt >= SettleMilliseconds)
            {
                seenAt = 0;
                FitToWindow();
            }

            // A recovery copy when a pause has come after a change
            RecoveryTick();
            WaitForInput();
        }

        return Console.ReadKey(true);
    }

    // Until input may have come, or a while has passed
    private static void WaitForInput()
    {
        if (OperatingSystem.IsWindows())
        {
            var input = GetStdHandle(StdInputHandle);
            if (input != IntPtr.Zero && input != new IntPtr(-1) &&
                WaitForSingleObject(input, WindowsWaitMilliseconds) != WaitFailed)
            {
                return;
            }
        }

        Thread.Sleep(KeyWaitMilliseconds);
    }

    private const int StdInputHandle = -10;
    private const uint WaitFailed = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    // Ctrl+C read as a key (not as the signal)
    private static bool IsCancelKey(ConsoleKeyInfo key) =>
        key.KeyChar == '\u0003' ||
        (key.Key == ConsoleKey.C && (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == ConsoleModifiers.Control);

    // The window was resized (or the text could not be drawn in it): the editor takes the dimensions now in use,
    // and is drawn again, also when they stay the same (the terminal may have moved or cleared what was shown)
    private void FitToWindow()
    {
        _windowSize = WindowSize();
        if (Init.AutoSize) Init.ApplyAutoSize();
        else if (Tools.IsWindowTooSmall()) Tools.WindowTooSmall();
        var widthChange = Init.LineWidth - _lineWidth;
        var heightChange = Init.LineCount - _lineCount;
        _lineWidth = Init.LineWidth;
        _lineCount = Init.LineCount;
        if (widthChange != 0 || heightChange != 0) Resize(widthChange, heightChange);
        RedrawAll();
        // (What could not be drawn is drawn now)
        Tools.TakeRedrawRequest();
    }

    // The text area is made wider or narrower, and higher or lower. The text is wrapped in the new width (the line
    // numbers are found again), and the cursor stays in view. A split keeps its panes on their sides, each with
    // half the width as when it was split
    private void Resize(int widthChange, int heightChange)
    {
        _height = Math.Max(1, _height + heightChange);
        _shownRows = new string?[_height];
        _shownCells = new int[_height];
        _engine.SetHeight(_height);
        if (_otherPane == null)
        {
            SetPlace(_left, Math.Max(2, _width + widthChange));
            return;
        }

        _wholeWidth = Math.Max(5, _wholeWidth + widthChange);
        var leftWidth = (_wholeWidth - 1) / 2;
        var rightWidth = _wholeWidth - leftWidth - 1;
        var rightLeft = _wholeLeft + leftWidth + 1;
        var focusAtLeft = _left < _otherPane.Left;
        SetPlace(focusAtLeft ? _wholeLeft : rightLeft, focusAtLeft ? leftWidth : rightWidth);
        _otherPane.Left = focusAtLeft ? rightLeft : _wholeLeft;
        _otherPane.Width = focusAtLeft ? rightWidth : leftWidth;
        _otherPane.TextWidth = _otherPane.Width - 1;
        _otherPane.Gutter = -1;
        _otherPane.ShownRows = new string?[_height];
        _otherPane.ShownCells = new int[_height];
        _otherPane.Engine.SetHeight(_height);
    }
}
