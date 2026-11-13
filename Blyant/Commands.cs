using System.Text.RegularExpressions;

namespace Blyant;

/// <summary>
/// The places that have a command line, each with its own commands
/// </summary>
internal enum CommandContext
{
    Files, // The file browser
    FilePicker, // The file browser when a file or directory is picked for something
    Favorites, // The favorites of the file browser
    History, // The files opened from the file browser
    Hotlist, // The directory hotlist of the file browser
    Search, // The files found by a search in the file browser
    Editor,
    Start, // The start page: its tabs Start, Configuration and Help
    Jump, // The Jump menu (the commands work on its Bookmarks tab)
    Projects, // The list of projects (the commands work on the selected project)
    Board // The corkboard (the commands work on the selected card)
}

/// <summary>
/// The commands of each place that has a command line. The tables are what Tab completes from on the
/// command line and what the log knows as commands.
/// A command that is added, renamed or removed where the commands are run must be changed here as well:
/// the tests compare the tables with the code that runs the commands (CommandTests).
/// </summary>
internal static class Commands
{
    private sealed class Table
    {
        internal readonly string[] Names; // The commands that are completed to, in the order they are offered
        internal readonly HashSet<string> All; // With the short forms that are not offered

        internal Table(string names, string shortForms)
        {
            Names = Split(names);
            Array.Sort(Names, StringComparer.Ordinal);
            All = new HashSet<string>(Names, StringComparer.Ordinal);
            All.UnionWith(Split(shortForms));
        }

        private static string[] Split(string words)
        {
            return words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    private static readonly Dictionary<CommandContext, Table> Tables = new()
    {
        [CommandContext.Files] = new Table(
            "quit close exit home data help browse file open jump book peek diff cd new info mkdir rename copy duplicate move delete favorite favorites history hotlist hotadd bookmark board pin refresh panes match path name find projects trash recovery project protect number nonumber bookmark! splits recent colorscheme log",
            "q h fav favs hist hot ju jmp proj pro corkboard colo qa"),
        [CommandContext.FilePicker] = new Table(
            "quit close home data help cd number nonumber",
            "q h"),
        [CommandContext.Favorites] = new Table(
            "quit close help jump book new color info remove move top bottom undo folder subfolder note panes peek number nonumber expand collapse rename import goto " +
            "path name recent missing prune list bookmark bookmark! board pin colorscheme log favorites history hotlist browse file open exit",
            "q h u ju jmp corkboard colo favs hist hot qa"),
        [CommandContext.History] = new Table(
            "quit close help jump book info goto favorite remove clear path name number nonumber recent missing prune " +
            "bookmark bookmark! board pin colorscheme log favorites history hotlist browse file open exit",
            "q h fav ju jmp corkboard colo favs hist hot qa"),
        [CommandContext.Hotlist] = new Table(
            "quit close help jump book info remove undo top bottom move rename clear path name number nonumber recent " +
            "missing prune bookmark bookmark! board pin colorscheme log favorites history hotlist browse file open exit",
            "q h u ju jmp corkboard colo favs hist hot qa"),
        [CommandContext.Search] = new Table(
            "quit close help jump book find info goto favorite number nonumber recent bookmark bookmark! board pin colorscheme log favorites history hotlist browse file open exit",
            "q h fav ju jmp corkboard colo favs hist hot qa"),
        [CommandContext.Start] = new Table(
            "new blank recover file open browse favorites project projects start config general layout editor lists modules system jump book splits recent help about " +
            "quick scheme logo snippet completions clipboard log restart reset quit exit board colorscheme history hotlist",
            "favs newproject proj pro ju jmp h guide snip comp q corkboard colo hist hot qa"),
        [CommandContext.Jump] = new Table(
            "quit close help info jump book new note panes peek number nonumber folder subfolder expand collapse rename color remove move " +
            "top bottom undo clearbook import path name recent missing prune board colorscheme log favorites history hotlist browse file open exit",
            "q h u ju jmp corkboard colo favs hist hot qa"),
        [CommandContext.Projects] = new Table(
            "quit close help info path name color note board new add remove colorscheme log favorites history hotlist browse file open exit",
            "q h colo favs hist hot qa"),
        [CommandContext.Board] = new Table(
            "quit close help jump book recent new folder note badge divider line frame mode info peek list board arrange color rename remove clear undo " +
            "top bottom missing prune tofile colorscheme log favorites history hotlist browse file open exit",
            "q h u ju jmp corkboard colo favs hist hot qa"),
        [CommandContext.Editor] = new Table(
            "write saveas quit quit! wq help edit edit! reload reload! undo redo delete yank replace nohlsearch " +
            "spell nospell spellnext spellprev suggest spelladd syntax number nonumber timestamp " +
            "favorite record play macro trim trim! format lineendings bom whitespace mark marks delmarks delmarks! changes bookmark! view splits recent clear registers reglist regclear putall vsel vline block ol ul dup duplicate surround pb pbappend pbread pbclear pbpath clipboard path sort sort! wrap nowrap snip complete diff scrolloff info jump book bookmark yankall rename split only new projects board pin colorscheme log zen favorites history hotlist browse file open exit",
            "w q q! wq! x h e e! u red d y s rep noh set spellsuggest spellgood nu nonu ts reg sn ju jmp fav proj pro sur k sview sv corkboard colo favs hist hot qa")
    };

    /// <summary>
    /// The commands of a place that Tab completes to, in alphabetical order (without the short forms)
    /// </summary>
    internal static IReadOnlyList<string> Names(CommandContext context)
    {
        // (not those of a module turned off: typed whole, they tell that it is off)
        var names = Tables[context].Names;
        return names.Any(Modules.Hides) ? names.Where(name => !Modules.Hides(name)).ToList() : names;
    }

    /// <summary>
    /// All the commands of a place, with the short forms
    /// </summary>
    internal static IReadOnlyCollection<string> All(CommandContext context)
    {
        return Tables[context].All;
    }

    /// <summary>
    /// True if the name is a command (or the short form of one) in the place
    /// </summary>
    internal static bool IsKnown(CommandContext context, string name)
    {
        return Tables[context].All.Contains(name);
    }

    /// <summary>
    /// True if the name is a command somewhere in Blyant
    /// </summary>
    internal static bool IsKnown(string name)
    {
        foreach (var table in Tables.Values)
        {
            if (table.All.Contains(name)) return true;
        }

        return false;
    }
}

/// <summary>
/// Completes the command being typed on a command line when Tab is pressed.
/// Pressing Tab again gives the next command that starts with what was typed, and after the last of them
/// what was typed is shown again. With nothing typed, all the commands of the place are gone through.
/// </summary>
internal sealed class CommandCompletion
{
    private readonly CommandContext _context;
    private string _shown = string.Empty; // What the last completion gave: typing something else starts over
    private string _before = string.Empty; // The lines a command works on, typed in front of it ("%", "'<,'>")
    private string _typed = string.Empty;
    private List<string> _matches = new();
    private int _index = -1; // The command shown (-1: what was typed)
    private bool _active;

    internal CommandCompletion(CommandContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Forget the commands being gone through (when a new command line is opened)
    /// </summary>
    internal void Reset()
    {
        _active = false;
        _index = -1;
        _matches = new List<string>();
    }

    /// <summary>
    /// The text of the command line after Tab was pressed
    /// </summary>
    /// <param name="text">The text of the command line, without the ":"</param>
    /// <param name="forward">False to go back to the previous command (Shift+Tab)</param>
    /// <returns>The text with the command completed, or the text as it was if nothing starts with it</returns>
    internal string Next(string text, bool forward)
    {
        if (!_active || !string.Equals(text, _shown, StringComparison.Ordinal))
        {
            Reset();
            if (!Start(text)) return text;
            _active = true;
        }

        // What was typed is one of the places gone through
        var count = _matches.Count + 1;
        _index = (_index + 1 + (forward ? 1 : count - 1)) % count - 1;
        _shown = _before + (_index < 0 ? _typed : _matches[_index]);
        return _shown;
    }

    /// <summary>
    /// What follows a command, by the name of the command: the words Tab completes to (":play " and the names of the
    /// macros). Set by the host
    /// </summary>
    internal static Dictionary<string, Func<IEnumerable<string>>> Arguments { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The commands that take one name that can have spaces in it (":colorscheme Deep Blue"): what is typed after the
    /// command is completed and listed as a whole, also after a space
    /// </summary>
    internal static bool TakesNameWithSpaces(string command) => SchemeCommands.Contains(command);

    /// <summary>
    /// The names of ":colorscheme" (its short form too), which takes the name of a color scheme
    /// </summary>
    internal static readonly string[] SchemeCommands = { "colorscheme", "colo" };

    // What follows a command that takes other words in one place than in another (English: as the commands)
    private static readonly Dictionary<(CommandContext, string), Func<IEnumerable<string>>> PlaceArguments = new()
    {
        [(CommandContext.Favorites, "import")] = () => new[] { "bookmarks", "folder" },
        [(CommandContext.Jump, "import")] = () => new[] { "favorites" },
        [(CommandContext.Board, "arrange")] = () => Corkboard.Orders,
        [(CommandContext.Board, "list")] = Corkboard.BoardWords,
        [(CommandContext.Board, "mode")] = () => CorkboardPage.ModeWords
    };

    /// <summary>
    /// The words that can follow a command in a place: those of the place, or else those of the command (Arguments).
    /// False if nothing is known to follow it
    /// </summary>
    internal static bool ArgumentsOf(CommandContext context, string name, out Func<IEnumerable<string>> words)
    {
        if (PlaceArguments.TryGetValue((context, name), out var own))
        {
            words = own;
            return true;
        }

        return Arguments.TryGetValue(name, out words!);
    }

    // Find the commands that start with what is typed (or what follows a command, see Arguments)
    private bool Start(string text)
    {
        var space = text.IndexOf(' ');
        // (the name of the command, also after the lines it works on: "'<,'>surround /b")
        var name = space > 0 ? Regex.Match(text[..space], "[a-z!]+$").Value : string.Empty;
        if (name.Length > 0 && ArgumentsOf(_context, name, out var words))
        {
            _before = text[..(space + 1)];
            _typed = text[(space + 1)..];
            if (_typed.Contains(' ') && !TakesNameWithSpaces(name)) return false;
            _matches = words().Where(word => word.StartsWith(_typed, StringComparison.OrdinalIgnoreCase) &&
                                             !string.Equals(word, _typed, StringComparison.OrdinalIgnoreCase)).ToList();
            return _matches.Count > 0;
        }

        // Only the name of a command is completed, not what follows it
        if (text.Contains(' ')) return false;
        var start = text.Length;
        while (start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] == '!')) start--;
        _before = text[..start];
        _typed = text[start..];
        _matches = Commands.Names(_context)
            .Where(name => name.StartsWith(_typed, StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(name, _typed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return _matches.Count > 0;
    }
}
