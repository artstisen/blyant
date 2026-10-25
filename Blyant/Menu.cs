namespace Blyant;

/// <summary>
/// What the pages draw alike: messages, questions, the label of the page at the top left, the name of Blyant in
/// the top row, the top row of the editor and the key hints of the bottom line
/// </summary>
internal static class Menu
{
    public static void StatusMessage(string msg, bool requireKeyPress, bool clearScreen, bool showCaption,
        string caption, string note, bool suppressPause)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (requireKeyPress)
        {
            // A message that waits for a key is shown in a box on top of the page
            ChoiceBox.Message(showCaption && !string.IsNullOrEmpty(caption) ? caption : DialogText.ApplicationName,
                msg, note);
            return;
        }

        // Clear screen
        if (clearScreen)
        {
            Tools.ClearScreen();
            // Show top info
            if (showCaption && !string.IsNullOrEmpty(caption))
            {
                WriteModeLabel(caption, note, 0);
                WriteNameInTopBar();
                Tools.CursorDown(2);
            }
        }

        Tools.CursorReturn();
        // Write message
        Console.Write(" ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(" " + msg + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // Require keypress
        if (requireKeyPress)
        {
            Tools.CursorReturn();
            Tools.CursorDown(2);
            Tools.CursorRight(1);
            Keyboard.AwaitAnyKey();
        }
        else
        {
            if (!suppressPause)
            {
                Thread.Sleep(100);
            }
        }
    }


    /// <summary>
    /// Ask yes or no in a box on top of the page (ChoiceBox)
    /// </summary>
    /// <param name="invertColors">True for something that cannot be undone: the question is shown as a warning
    /// and No is selected first</param>
    public static bool YesNoPrompt(string caption, string question, string note, bool invertColors)
    {
        return ChoiceBox.Confirm(caption, question, note, invertColors);
    }

    // Returns:
    // 1 = Yes
    // 2 = No
    // 3 = Cancel
    /// <summary>
    /// Ask yes, no or cancel in a box on top of the page
    /// </summary>
    /// <returns>1 = Yes, 2 = No, 3 = Cancel (also Esc)</returns>
    public static byte YesNoCancelPrompt(string caption, string question, string note, bool invertColors)
    {
        var choice = ChoiceBox.Pick(caption, question, note,
            new[] { DialogText.ChoiceYes, DialogText.ChoiceNo, DialogText.ChoiceCancel },
            new[] { "Y", "N", "Esc" }, 0, invertColors);
        return choice switch
        {
            0 => 1,
            1 => 2,
            _ => 3
        };
    }


    #region Mode Label

    private static int _tempModeLabelCursorMemoryTop;
    private static int _tempModeLabelCursorMemoryLeft;

    private static void ModeLabelSaveCursor()
    {
        _tempModeLabelCursorMemoryTop = Console.CursorTop;
        _tempModeLabelCursorMemoryLeft = Console.CursorLeft;
    }

    private static void ModeLabelLoadCursor()
    {
        Tools.SetCursorEx(_tempModeLabelCursorMemoryTop, _tempModeLabelCursorMemoryLeft);
    }

    private static string _modeTitleMemory = string.Empty;
    private static string _modeNoteMemory = string.Empty;

    /// <param name="attention">True to show the label in the attention colors (something is in progress,
    /// as copying to a directory being selected)</param>
    public static void WriteModeLabel(string title, string note, int recallMode, bool attention = false)
    {
        // 0 = do nothing
        // 1 = store in memory
        // 2 = recall memory
        if (recallMode == 2 && !string.IsNullOrEmpty(_modeTitleMemory))
        {
            // Recall (used to recall the mode label after using compound/option key)
            title = _modeTitleMemory;
            note = _modeNoteMemory;
        }
        else if (recallMode == 1)
        {
            // Save for recall
            _modeTitleMemory = title;
            _modeNoteMemory = note;
        }

        ModeLabelSaveCursor();
        Tools.CursorTopLeft();
        ConsoleColors.SetColors(attention
            ? ConsoleColors.ColorPreset.AttentionInverted
            : ConsoleColors.ColorPreset.HighlightInverted);
        Console.WriteLine(" " + title.ToUpper() + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (!string.IsNullOrEmpty(note))
        {
            Console.WriteLine(" " + note);
        }

        ModeLabelLoadCursor();
    }

    #endregion

    /// <summary>
    /// The width kept free at the right of the top row of the editor, for the label Unsaved or Read only (the
    /// wider of them, in the language in use)
    /// </summary>
    internal static int EditorLabelWidth =>
        Math.Max(TextMetrics.WidthOf(DialogText.SavIndicator), TextMetrics.WidthOf(DialogText.ReadOnlyLabel));

    /// <summary>
    /// The top row of the editor: the caption (EDITOR, or COMPARE) at the left, then the name of what is edited
    /// (the name of the file) and a reminder after it, if any. The name is cut so the
    /// label Unsaved or Read only fits at the right
    /// </summary>
    internal static void WriteEditorTop(string caption, string title, string reminder)
    {
        Tools.SetCursorEx(0, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Math.Max(0, Init.LineWidth - 1)));
        WriteModeLabel(caption, string.Empty, 0);
        var start = TextMetrics.WidthOf(" " + caption.ToUpper() + " ") + 1;
        var room = Init.LineWidth - start - EditorLabelWidth - 2;
        if (reminder.Length > 0) room -= TextMetrics.WidthOf(reminder) + 1;
        if (room < 1) return;
        Tools.SetCursorEx(0, start);
        Console.Write(TextMetrics.Printable(Tools.TruncateString(title, room, true)));
        if (reminder.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" " + reminder);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
    }

    internal static void WriteNameInTopBar()
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        WriteCenterText(DialogText.ApplicationName);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static string _tempCurrentSaveIndicator = string.Empty;

    /// <summary>
    /// Write a line of keys and what they do ("j/k Select | Esc Close"), with the keys highlighted
    /// </summary>
    internal static void WriteKeyHints(string hints)
    {
        Console.Write(" ");
        // The hints that fit on the line are shown (a longer text in another language can need more room). They can
        // take the line to its end: the last column of the window is not part of it (Init.ApplyAutoSize)
        var room = Init.LineWidth - Console.CursorLeft;
        foreach (var hint in hints.Split(" | "))
        {
            var space = hint.IndexOf(' ');
            var key = " " + (space < 0 ? hint : hint[..space]) + " ";
            // One blank space on each side of the description
            var description = space < 0 ? " " : " " + hint[(space + 1)..] + " ";
            room -= TextMetrics.WidthOf(key + description);
            if (room < 0) break;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHintInverted);
            Console.Write(key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHint);
            Console.Write(description);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    internal static void InitializeSaveIndicator()
    {
        _tempCurrentSaveIndicator = "";
    }

    internal static void IndicateChanges(bool forceDisplay)
    {
        if (DocumentInfo.HasChanged && (_tempCurrentSaveIndicator == "" || forceDisplay))
        {
            // At the right of the top row of the editor
            Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - TextMetrics.WidthOf(DialogText.SavIndicator)));
            _tempCurrentSaveIndicator = DialogText.SavIndicator;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.UnsavedInverted);
            Console.Write(_tempCurrentSaveIndicator);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        else if (!DocumentInfo.HasChanged && _tempCurrentSaveIndicator == DialogText.SavIndicator)
        {
            _tempCurrentSaveIndicator = "";
        }
    }

    internal static void ClearChangeIndicator()
    {
        var width = TextMetrics.WidthOf(DialogText.SavIndicator);
        Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - width));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', width));
    }

    private static void WriteCenterText(string label)
    {
        Tools.SaveCursorPosition(0);
        Tools.CursorTopLeft();
        if (label.Length > 0)
        {
            Tools.CursorReturn();
            Tools.CursorRight(Init.LineWidth / 2);
            Tools.CursorLeft((label.Length / 2) + 1);
            Console.Write(" " + label + " ");
        }

        Tools.LoadCursorPosition(0);
    }
}
