namespace Blyant;

/// <summary>
/// Hints on the Start tab of the start page, in a box to the right of the menu: one feature of Blyant at a time,
/// the next each time Blyant starts and shows the start page (setting "hintindex"). X on the Start tab hides them,
/// and Configuration (System) shows them again (setting "hints"). While they are hidden the hint does not change,
/// and nothing is written for it
/// </summary>
internal static class Hints
{
    /// <summary>
    /// True to show the hints (setting "hints")
    /// </summary>
    internal static bool Enabled { get; set; } = true;

    /// <summary>
    /// The hint shown (setting "hintindex"; -1 before the first start, so the first one is shown first)
    /// </summary>
    internal static int Index { get; set; } = -1;

    /// <summary>
    /// The hints, in the language in use (keys and commands stay as they are typed)
    /// </summary>
    internal static string[] All() =>
    [
        Lang.T("Type : on any page to see its commands, narrowed as you type. Tab completes them, and ? shows " +
               "all the keys of the page."),
        Lang.T("Ctrl+O opens the Jump menu from anywhere: bookmarks, recent places, projects, the lists of the " +
               "file browser and the pages of Blyant."),
        Lang.T("Ctrl+K or :recent shows the latest files you opened, on almost any page. In the editor, Ctrl+K " +
               "and Enter go back to the file you were in before."),
        Lang.T("B in the Jump menu bookmarks the selected place, and :bookmark does it in the editor and the file " +
               "browser. Then :book 3 jumps to bookmark 3 from almost any page."),
        Lang.T("In the file browser, P keeps a file or folder as a favorite, and F shows them. Group them in " +
               "folders and give them colors; :import favorites in the Jump menu copies them to the bookmarks."),
        Lang.T("In a Markdown or Typst file, type /bold, /link or /h2 and press Tab. Select text and type " +
               ":surround /bold to put the marks around it. Your own completions go in completions.txt."),
        Lang.T("While you write, a recovery copy is kept. If Blyant or the terminal stops, the text is offered " +
               "when you open the file again, or under Recover unsaved text."),
        Lang.T("D in the file browser moves a file to the trash, after asking. :trash goes there, and M moves a " +
               "file back where you want it."),
        Lang.T(":split shows two panes of a text (Tab goes from one to the other), and :diff compares the text " +
               "with the file as it is saved."),
        Lang.T(":split name shows another file at the right, read only: notes or an outline beside your text. " +
               ":view picks it in the file browser, and Tab goes into it."),
        Lang.T("V in the file browser peeks at the selected file in the other pane, without opening it, and j and " +
               "k go on to the next file."),
        Lang.T("A project can have a note: N in the list of projects opens it in the editor. Keep plans, things " +
               "to do and ideas for the project there.")
    ];

    /// <summary>
    /// The hint shown, and its number (from 1)
    /// </summary>
    internal static (string Text, int Number) Current()
    {
        var all = All();
        // (before the first start, -1: the first)
        var index = Math.Max(0, Index) % all.Length;
        return (all[index], index + 1);
    }

    // The hint has gone on in this session
    private static bool _advanced;

    /// <summary>
    /// The start page is shown: the first time in a session, the next hint (so one is not passed over when Blyant
    /// starts in a file or the file browser)
    /// </summary>
    internal static void AdvanceOnce()
    {
        if (_advanced) return;
        _advanced = true;
        Advance();
    }

    /// <summary>
    /// The next hint, saved with the settings. Not while the hints are hidden, and not when the settings file could
    /// not be read (then it is not written: it would be replaced with the settings in use)
    /// </summary>
    internal static void Advance()
    {
        if (!Enabled || FileIo.SettingsUnread) return;
        Index = (Math.Max(-1, Index) + 1) % All().Length;
        FileIo.SaveSettings();
    }
}
