using System.Text;

namespace Blyant;

internal static class Init
{
    #region Output dimensions
    // Min/Max line values
    // Standard line count (row count)
    internal const byte MinLineCount = 23; // Default
    private const byte MaxLineCount = 255; // Default
    // Standard line width (column count)
    internal const byte MinLineWidth = 80; // Default
    private const byte MaxLineWidth = 255; // Default
    internal static byte LineWidth { get; private set; }
    internal static byte LineCount { get; private set; }

    // Set "artificial buffer size" according to general guidelines (80x23)
    // -the actual console buffer is not touched but must be set manually by the user (cross platform compat)
    // -this buffer is used to calculate the output width and height across the entire application.
    internal static void SetDefaultOutputDimensions()
    {
        LineWidth = MinLineWidth;
        LineCount = MinLineCount;
    }

  
    /// <summary>
    /// Use the size of the terminal window as the output dimensions (setting "autosize", on unless turned off)
    /// </summary>
    internal static bool AutoSize { get; set; } = true;

    /// <summary>
    /// Set the output dimensions from the size of the terminal window if AutoSize is on.
    /// One row and one column are left unused, as writing in the last column of the window moves
    /// the cursor to the next line (and in the bottom right corner makes some terminals scroll).
    /// </summary>
    /// <returns>True if the dimensions were changed</returns>
    internal static bool ApplyAutoSize()
    {
        if (!AutoSize) return false;
        int width, height;
        try
        {
            width = Console.WindowWidth - 1;
            height = Console.WindowHeight - 1;
        }
        catch
        {
            // The size of the window is not available: keep the dimensions
            return false;
        }

        width = Math.Clamp(width, MinLineWidth, MaxLineWidth);
        height = Math.Clamp(height, MinLineCount, MaxLineCount);
        if (width == LineWidth && height == LineCount) return false;
        LineWidth = (byte)width;
        LineCount = (byte)height;
        return true;
    }

    internal static void SetLineWidth(byte width)
    {
        LineWidth = width is >= MinLineWidth and <= MaxLineWidth ? width : MinLineWidth;

    }

    internal static void SetLineCount(byte count)
    {
        LineCount = count is >= MinLineCount and <= MaxLineCount ? count : MinLineCount;
    }

    internal static void IncreaseLineCount()
    {
        if (LineCount + 1 <= MaxLineCount)
        {
            LineCount += 1;
        }
    }

    internal static void DecreaseLineCount()
    {
        if (LineCount - 1 >= MinLineCount)
        {
            LineCount -= 1;
        }
    }
    
    internal static void IncreaseLineWidth()
    {
        if (LineWidth + 1 <= MaxLineWidth)
        {
            LineWidth += 1;
        }
    }

    internal static void DecreaseLineWidth()
    {
        if (LineWidth - 1 >= MinLineWidth)
        {
            LineWidth -= 1;
        }
    }


    private const char TempOutputCornerChar = '+';

    internal static bool TestOutputDimensions()
    {
        try
        {
            Tools.ClearScreen();
            // Top left corner
            Tools.CursorTopLeft();
            Console.Write(TempOutputCornerChar);

            // Top right corner
            Tools.CursorTopLeft();
            Tools.CursorRight(LineWidth - 1);
            Console.Write(TempOutputCornerChar);

            // Bottom left corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Console.Write(TempOutputCornerChar);

            // Bottom right corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Tools.CursorRight(LineWidth - 1);
            Console.Write(TempOutputCornerChar);

            return true;
        }
        catch
        {
            SetDefaultOutputDimensions();  // Must be set first to display the message correctly
            Menu.StatusMessage(DialogText.ErrorBufferSize, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
            FileIo.SaveSettings();
            return false;
        }
    }


    internal static void DisplayOutputDimensions()
    {
        try
        {
            Tools.ClearScreen();
            // Top left corner
            Tools.CursorTopLeft();
            Console.Write(TempOutputCornerChar + "--");
            Tools.CursorReturn();
            Tools.CursorDown();
            Console.Write("|");

            // Top right corner
            Tools.CursorTopLeft();
            Tools.CursorRight(LineWidth - 3);
            Console.Write("--" + TempOutputCornerChar);
            Tools.CursorReturn();
            Tools.CursorDown();
            Tools.CursorRight(LineWidth - 1);
            Console.Write("|");

            // Bottom left corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Console.Write(TempOutputCornerChar + "--");
            Tools.CursorReturn();
            Tools.CursorUp(1);
            Console.Write("|");

            // Bottom right corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Tools.CursorRight(LineWidth - 3);
            Console.Write("--" + TempOutputCornerChar);
            Tools.CursorReturn();
            Tools.CursorRight(LineWidth - 1);
            Tools.CursorUp(1);
            Console.Write("|");

            // Display center message
            Tools.CursorTopLeft();
            Tools.CursorDown(2);
            Tools.CursorRight(3);
            Console.Write(LineWidth + "x" + LineCount);
        }
        catch
        {
            DimensionsExceptionCaught = true;
            SetDefaultOutputDimensions(); // Must be set first to display the message correctly
            Menu.StatusMessage(DialogText.ErrorBufferSize, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
            FileIo.SaveSettings();
        }
    }

    internal static string GetOutputDimensions()
    {
        return "(" + LineWidth + "x" + LineCount + ")";
    }

    #endregion


    #region Console init sequence


    internal static bool DimensionsExceptionCaught { get; set; }
    internal static bool DataDirectoryCreated { get; set; }
    internal static bool UsingDefaultSettings { get; set; }
    private static int _initLines; // The number of lines logged by the initialization that is running

    public static void RunInitialization()
    {
        _initLines = 0;
        // Hide the cursor
        Tools.HideCursor();
        DimensionsExceptionCaught = false;
        DataDirectoryCreated = false;
        UsingDefaultSettings = false;
        Console.Clear();
        Console.WriteLine();
        WriteProgressBar(1); // progress
        Log(DialogText.ApplicationName + " " + DialogText.ApplicationVersion, true);
        WriteProgressBar(1); // progress
        Log(DialogText.InitInitializing, true);
        WriteProgressBar(2); // progress
        WriteProgressBar(3); // progress
        WriteProgressBar(4); // progress
        // Set title
        Console.Title = DialogText.ApplicationName;
        WriteProgressBar(5); // progress
        WriteProgressBar(6); // progress
        Log(DialogText.InitSetEncoding, true);
        WriteProgressBar(7); // progress
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Log(DialogText.InitSetDefaults, true);
        AsciiStyles.ResetAsciiStyle();
        Logo.Animation = Logo.AnimationSpeed.Fast;
        AutoSize = true;
        StartMenu.Mode = StartMenu.StartMode.Select;
        StartMenu.StartInFavorites = false;
        Projects.Directory = Projects.DefaultDirectory;
        Projects.DeleteToTrash = true;
        ConsoleColors.Monochrome = false;
        SyntaxHighlighter.ColorFiles = true;
        Completions.Enabled = false;
        FileExplorer.DualPane = true;
        TextArea.LineNumbers = false;
        Logo.AnimateNext(); // Sweep the logo when the start page is first shown
        WriteProgressBar(8); // progress
        // Set default colors
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
        WriteProgressBar(9); // progress
        // Ctrl+C does not close Blyant (text that is not saved would be lost): the signal is cancelled, and the
        // editor tells how to close it. Ctrl+C stays a signal (not a key, as TreatControlCAsInput would make it), so
        // the terminal still copies a selection with it
        Console.CancelKeyPress += (_, e) =>
        {
            if (e.SpecialKey != ConsoleSpecialKey.ControlC) return;
            e.Cancel = true;
            Tools.NoteCancelKey();
        };
        // Set default buffer-size
        Log(DialogText.InitDimensions, true);
        WriteProgressBar(10); // progress
        SetDefaultOutputDimensions();
        WriteProgressBar(11); // progress
        // Check default data folder path (writing permissions etc.)
        Log(DialogText.InitDirectoryCheck, true);
        WriteProgressBar(12); // progress
        if (!FileIo.CheckDataDirectory())
        {
            Log(DialogText.InitDirectoryCheckError + FileIo.DirectoryCheckErrorMessage, true);
            Keyboard.AwaitAnyKey();
        }
        else
        {
            if (DataDirectoryCreated)
            {
                Log(DialogText.InitOk + " -" + DialogText.DirectoryCreated, true);
            }
            else
            {
                Log(DialogText.InitOk, true);
            }

            // The log is written to log.txt in the data directory from now on (with what is logged so far)
            EventLog.StartFile(Path.Combine(FileIo.GetDataDirectoryName(), EventLog.FileName));
        }
        WriteProgressBar(13); // progress
        Log(DialogText.InitLoadingSettings, true);
        WriteProgressBar(14); // progress
        // Load settings
        if (!FileIo.LoadSettings())
        {
            Log(DialogText.InitFailed, true);
            Log(DialogText.InitLoadingSettingsError, true);
            // The settings file is not written now: when a setting is changed, a copy of it is kept first
            // (settings.ini.bad)
            Log("(using the default settings)", true);
            Keyboard.AwaitAnyKey();
        }
        else
        {
            if (UsingDefaultSettings)
            {
                Log(DialogText.InitOk + " (" + DialogText.DefaultSettings + ")", true);
                FileIo.SaveSettings(); // Save a fresh settings file
            }
            else
            {
                Log(DialogText.InitOk, true);
            }
        }
        WriteProgressBar(15); // progress
        // Update output dimensions if set by user
        ApplyAutoSize();
        Log(DialogText.InitUpdateDimensions, true);
        WriteProgressBar(16); // progress
        Log("Target: " + LineWidth + "x" + LineCount, true);
        WriteProgressBar(17); // progress
        if (TestOutputDimensions() && !DimensionsExceptionCaught)
        {
            Log(DialogText.InitOk, true);
        }
        else
        {
            Log(DialogText.InitFailed, true);
        }
        WriteAllInitOutput();
        WriteProgressBar(18); // progress
        Log(DialogText.InitCompleted, true);
    }

    /// <summary>
    /// Add a line to the log
    /// </summary>
    /// <param name="text">The text</param>
    /// <param name="writeOutput">True to write it on the screen too (during the initialization)</param>
    internal static void Log(string text, bool writeOutput)
    {
        EventLog.Add(text);
        _initLines++;
        if (writeOutput)
        {
            Console.WriteLine(Tools.TruncateString(text, 75, true));
        }
    }

    // Show what the initialization has logged
    private static void WriteAllInitOutput()
    {
        Tools.ClearScreen();
        foreach (var line in EventLog.All.Skip(Math.Max(0, EventLog.Count - _initLines)))
        {
            Console.WriteLine(Tools.TruncateString(line, 75, true));
        }
    }

    /// <summary>
    /// Show the log
    /// </summary>
    internal static void WriteLog()
    {
        EventLog.Show();
    }

    private static void WriteProgressBar(int value)
    {
        Tools.SaveCursorPosition(0);
        Tools.CursorTopLeft();
        Console.Write(Tools.GetProgressBar(value, 18));
        Tools.LoadCursorPosition(0);
    }

    #endregion
}