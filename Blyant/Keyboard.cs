namespace Blyant;

internal static class Keyboard
{
    private static ConsoleKeyInfo _c;

    #region Navigation keys

    public static ConsoleKeyInfo ToNavigationKey(ConsoleKeyInfo key)
    {
        var arrow = key.KeyChar switch
        {
            'h' => ConsoleKey.LeftArrow,
            'j' => ConsoleKey.DownArrow,
            'k' => ConsoleKey.UpArrow,
            'l' => ConsoleKey.RightArrow,
            _ => ConsoleKey.None
        };
        return arrow == ConsoleKey.None ? key : new ConsoleKeyInfo('\0', arrow, false, false, false);
    }

    /// <summary>
    /// True if the key is CTRL + a letter (and not a character typed with AltGr, which is reported as CTRL+ALT)
    /// </summary>
    public static bool IsControl(ConsoleKeyInfo key, ConsoleKey letter)
    {
        return key.Key == letter && (key.Modifiers & ConsoleModifiers.Control) != 0 &&
               (key.Modifiers & ConsoleModifiers.Alt) == 0;
    }

    #endregion

    #region The key of a page

    // A key to be read next, handed back by a page that closed before it could use it (a peek closed by Esc, with
    // the key typed right after it, see TextArea.ReadKey)
    private static ConsoleKeyInfo? _pending;

    /// <summary>
    /// The key a page gets when it must be drawn again (Blyant goes on after Ctrl+Z and "fg", and the shell wrote on
    /// the screen). It does nothing on a page: the page is drawn again as after any key
    /// </summary>
    internal static readonly ConsoleKeyInfo RedrawKey = new('\0', ConsoleKey.F24, false, false, false);

    internal static bool IsRedraw(ConsoleKeyInfo key) => key.Key == ConsoleKey.F24 && key.KeyChar == '\0';

    /// <summary>
    /// Keep a key to be read next by the page that is shown now
    /// </summary>
    internal static void PushBack(ConsoleKeyInfo key) => _pending = key;

    /// <summary>
    /// The key handed back, if there is one (it is taken)
    /// </summary>
    internal static ConsoleKeyInfo? TakePending()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }

    /// <summary>
    /// The next key of a page: a key handed back first. On Linux and macOS the keyboard is looked at now and then
    /// while waiting, so RedrawKey comes when Blyant goes on after Ctrl+Z (Windows has no Ctrl+Z of a terminal)
    /// </summary>
    internal static ConsoleKeyInfo ReadKey()
    {
        if (TakePending() is { } pending) return pending;
        if (OperatingSystem.IsWindows()) return Console.ReadKey(true);
        while (true)
        {
            if (Tools.TakeContinued()) return RedrawKey;
            try
            {
                if (Console.KeyAvailable) return Console.ReadKey(true);
            }
            catch (InvalidOperationException)
            {
                // Keys cannot be asked about: wait for one
                return Console.ReadKey(true);
            }

            Thread.Sleep(KeyWaitMilliseconds);
        }
    }

    // How long a page waits between looking at the keyboard (Linux, macOS; as the editor)
    private const int KeyWaitMilliseconds = 15;

    #endregion

    #region Await keypress
    public static void AwaitAnyKey()
    {
        Console.Write(DialogText.HitAnyKeyToContinue);
        _c = Console.ReadKey(true);
    }


    public static bool AwaitAnyKeyOrEscape()
    {
        Console.Write(DialogText.HitAnyKeyOrEscape);
        _c = Console.ReadKey(true);
        if (_c.Key == ConsoleKey.Escape)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static ConsoleKeyInfo AwaitAnyKeyWithInstructions(string[] text)
    {
        Tools.WriteCenterScreen(text);
        _c = Console.ReadKey(true);
        return _c;
    }
    #endregion
}

class Shortcut
{
    private string _key;
    private string _description;
    private string _divider;
    private bool _isHeadline;
    private const char Margin = ' ';
    public Shortcut(string key, string description, bool isHeadline)
    {
        _key = key;
        _description = description;
        _divider = new string('.', Math.Max(1, 20 - key.Length));
        _isHeadline = isHeadline;
    }

    public bool IsHeadline => _isHeadline;

    /// <summary>
    /// The keys (or commands) of the row
    /// </summary>
    public string Key => _key;

    /// <summary>
    /// The key and description as one text (for searching)
    /// </summary>
    public string Text => _key + " " + _description;

    public string GetDescription()
    {
        return _description;
    }

    public void WriteShortcut(bool newLine, bool useMargin)
    {
        // In the colors of the quick guide: the headings in the color of headings, the keys in the accent color
        if (_isHeadline)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
            if (useMargin)
            {
                Console.Write(new string(Margin, 1) + _description);
            }
            else
            {
                Console.Write(_description);
            }
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        else
        {
            if (useMargin) Console.Write(new string(Margin, 4));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            Console.Write(_key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(_divider);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // A long description (in another language) is cut at the edge of the window
            Console.Write(Tools.FitLine(" " + _description, Console.CursorLeft));
        }
        if (newLine)
        {
            Console.Write("\n");
        }
    }

}