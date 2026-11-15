using System.Text;
using System.Text.RegularExpressions;

namespace Blyant;

/// <summary>
/// The registers of the shell: named values ("docs", "note") that a command line uses with $name ("copy plot.md
/// $docs"). A value is a text, a path, or lines (collected with "reg notes += $peek"). They are the shell's own (not the registers of the editor), and kept in the
/// data directory between sessions (registers.txt: a line "name", a tab, and the value). A name starts with a letter,
/// and has letters, digits and "_" (letter case alike: kept in lowercase). This class does not touch the console
/// </summary>
internal static partial class ShellRegisters
{
    internal const string FileName = "registers.txt";

    /// <summary>
    /// The file of the registers, for tests (null: in the data directory)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    private static string FilePath => FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    private static readonly SortedDictionary<string, string> Values = new(StringComparer.Ordinal);

    /// <summary>
    /// The registers, by name
    /// </summary>
    internal static IReadOnlyDictionary<string, string> All => Values;

    /// <summary>
    /// The name of $clip: the text the shell's "clip" copied last. It is not a register a user can set, and is kept in
    /// Blyant only (not in the file, and not read from the clipboard of the system)
    /// </summary>
    internal const string ClipName = "clip";

    /// <summary>
    /// $clip: the text "clip" copied last in this Blyant (null: nothing yet). It may have more than one line
    /// </summary>
    internal static string? Clip { get; set; }

    /// <summary>
    /// True for the name of $clip (letter case alike)
    /// </summary>
    internal static bool IsClip(string name) => name.Equals(ClipName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The names set by Blyant, not registers: $clip (the text clip copied last), $peek (the lines peek showed last), $pwd (the directory the shell is
    /// in), $file (the file the last command worked on), $date, $time and $now. They cannot be set or taken away, and
    /// a line of one of them in the file is kept as a line that is not a register
    /// </summary>
    internal static readonly string[] Reserved = { ClipName, PeekName, "pwd", "file", "date", "time", "now" };

    /// <summary>
    /// The name of $peek: the lines the last peek showed
    /// </summary>
    internal const string PeekName = "peek";

    /// <summary>
    /// $peek: the lines the last peek of this Blyant showed, as they were then (null: none yet). It may have more than
    /// one line
    /// </summary>
    internal static string? Peek { get; set; }

    /// <summary>
    /// True for a name set by Blyant (letter case alike)
    /// </summary>
    internal static bool IsReserved(string name) => Reserved.Contains(name, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The directory the shell is in, for $pwd (set by the shell that runs)
    /// </summary>
    internal static Func<string>? DirectorySource { get; set; }

    /// <summary>
    /// The file the last command worked on, for $file (null: none yet; set by the shell that runs)
    /// </summary>
    internal static Func<string?>? FileSource { get; set; }

    /// <summary>
    /// The time now, for $date, $time and $now (another for tests)
    /// </summary>
    internal static Func<DateTime> Clock { get; set; } = () => DateTime.Now;

    // The value of a name set by Blyant (null: none now: nothing copied, no file yet)
    private static string? ReservedValue(string name) => name.ToLowerInvariant() switch
    {
        ClipName => Clip,
        PeekName => Peek,
        "pwd" => DirectorySource?.Invoke(),
        "file" => FileSource?.Invoke(),
        // (as :ts in the editor: ISO, the same in every language)
        "date" => Clock().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        "time" => Clock().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        _ => Clock().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
    };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex NamePattern();

    /// <summary>
    /// True for a name a register can have ("docs", "draft_2"; not "2", "my docs")
    /// </summary>
    internal static bool IsName(string name) => NamePattern().IsMatch(name);

    /// <summary>
    /// The lines of the file that are not registers (no name, no tab, or not a name a register can have): told by
    /// "reg", and kept in the file when it is written, so a line mistyped by hand can be put right
    /// </summary>
    internal static int Skipped => Kept.Count;

    private static readonly List<string> Kept = new();

    // The text of the file as it was read or written last ("": no file), to tell when it was changed by hand. Null: it
    // could not be read, so it is not written over until it has been read
    private static string? _text;

    /// <summary>
    /// Read the registers from their file (none: no registers)
    /// </summary>
    internal static void Load()
    {
        if (Read() is { } text)
        {
            Take(text);
            return;
        }

        Values.Clear();
        Kept.Clear();
        _text = null;
    }

    /// <summary>
    /// Read the registers again when their file has been changed since (edited by hand, or by another Blyant): before
    /// each command of the shell. The text of the file is compared (a time of a file can be too coarse: 2 seconds on
    /// FAT drives). A file that cannot be read now (another program saving it) leaves them as they are. Every change
    /// made in the shell is written at once, so none is lost
    /// </summary>
    /// <returns>True if they were read again</returns>
    internal static bool Refresh()
    {
        if (Read() is not { } text || text == _text) return false;
        Take(text);
        return true;
    }

    // The text of the file ("": it is not there), or null when it cannot be read
    private static string? Read()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : string.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    // The registers of a text of the file (the lines that are not registers kept as they are)
    private static void Take(string text)
    {
        Values.Clear();
        Kept.Clear();
        _text = text;
        // (the register a line starting with a tab goes on: a register of several lines)
        string? current = null;
        foreach (var line in TextBuffer.Normalize(text).Split('\n'))
        {
            if (current != null && line.StartsWith('\t'))
            {
                Values[current] += "\n" + line[1..];
                continue;
            }

            current = null;
            if (line.Trim().Length == 0) continue;
            var tab = line.IndexOf('\t');
            // (a line "clip", "pwd" ... is kept as it is: those are set by Blyant)
            if (tab <= 0 || !IsName(line[..tab]) || IsReserved(line[..tab]))
            {
                Kept.Add(line);
                continue;
            }

            current = line[..tab].ToLowerInvariant();
            Values[current] = line[(tab + 1)..];
        }
    }

    /// <summary>
    /// Write the registers to their file (as a whole, safely)
    /// </summary>
    /// <returns>DialogText.Success or what went wrong</returns>
    internal static string Save()
    {
        try
        {
            // A file that could not be read is not written over: its registers would be lost
            if (_text == null)
            {
                Load();
                if (_text == null) return Lang.F("{0} could not be read: the registers are not written", FileName);
            }

            // The registers, and after them the lines that are not registers, as they were
            // (a register of several lines: its next lines start with a tab)
            var text = string.Concat(Values.Select(pair => pair.Key + "\t" + pair.Value.Replace("\n", "\n\t") + "\n")) +
                       string.Concat(Kept.Select(line => line + "\n"));
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(text));
            // (written here: not read again for it)
            _text = text;
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The value of a register, or of a name set by Blyant ("clip", "pwd" ...), or null
    /// </summary>
    internal static string? Get(string name) =>
        IsReserved(name) ? ReservedValue(name) : Values.TryGetValue(name.ToLowerInvariant(), out var value) ? value : null;

    /// <summary>
    /// Give a register a value: a text, or lines (kept in the file as lines that start with a tab after the first)
    /// </summary>
    internal static void Set(string name, string value) =>
        Values[name.ToLowerInvariant()] = TextBuffer.Normalize(value);

    /// <summary>
    /// Add a text to the end of a register, as lines after those it has (a register that is not there: made)
    /// </summary>
    internal static void Add(string name, string value) =>
        Set(name, Values.TryGetValue(name.ToLowerInvariant(), out var before) ? before + "\n" + value : value);

    /// <summary>
    /// Take a register away (false: there is none of that name)
    /// </summary>
    internal static bool Remove(string name) => Values.Remove(name.ToLowerInvariant());

    /// <summary>
    /// Take all the registers away
    /// </summary>
    internal static void Clear() => Values.Clear();

    /// <summary>
    /// A command line with $name put in for the value of each register of that name ("$$" for a "$"; a $name that is
    /// not a register is left as typed, so "$5" stays). In the first words (paths), a value with a blank in it, in a
    /// word that is not in quotes, puts the word in quotes, so it is still one word ("copy plot.md $docs"); after them
    /// the text is the value as it is (the text of "insert": no quotes in it)
    /// </summary>
    /// <param name="line">The command line</param>
    /// <param name="words">The words (the command first) in which a value with blanks is quoted; after them, as it is</param>
    internal static string Expand(string line, int words)
    {
        if (!line.Contains('$')) return line;
        var result = new StringBuilder();
        var i = 0;
        var count = 0;
        while (i < line.Length)
        {
            // The blank between words, as it is
            var start = i;
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            result.Append(line, start, i - start);
            if (i >= line.Length) break;
            if (count >= words)
            {
                // The rest: the values as they are
                result.Append(Replace(line[i..]));
                break;
            }

            // A word (with its parts in quotes)
            start = i;
            var quoted = false;
            while (i < line.Length && (quoted || !char.IsWhiteSpace(line[i])))
            {
                if (line[i] == '"') quoted = !quoted;
                i++;
            }

            var word = line[start..i];
            var replaced = Replace(word);
            var hasQuote = word.Contains('"');
            result.Append(!hasQuote && replaced != word && replaced.Any(char.IsWhiteSpace) ? "\"" + replaced + "\"" : replaced);
            count++;
        }

        return result.ToString();
    }

    /// <summary>
    /// True if a command line has $name in it (as Expand finds it: not "$$name", and not "$names")
    /// </summary>
    internal static bool Mentions(string line, string name)
    {
        for (var i = 0; i + 1 < line.Length; i++)
        {
            if (line[i] != '$') continue;
            if (line[i + 1] == '$')
            {
                i++;
                continue;
            }

            if (!char.IsAsciiLetter(line[i + 1])) continue;
            var end = i + 1;
            while (end < line.Length && (char.IsAsciiLetterOrDigit(line[end]) || line[end] == '_')) end++;
            if (line[(i + 1)..end].Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
            i = end - 1;
        }

        return false;
    }

    // $name for the value of the register (a name not of a register: as typed), and $$ for $
    private static string Replace(string text)
    {
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '$' || i + 1 >= text.Length)
            {
                result.Append(text[i]);
                continue;
            }

            if (text[i + 1] == '$')
            {
                result.Append('$');
                i++;
                continue;
            }

            var end = i + 1;
            if (!char.IsAsciiLetter(text[end]))
            {
                result.Append('$');
                continue;
            }

            while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_')) end++;
            if (Get(text[(i + 1)..end]) is { } value)
            {
                result.Append(value);
                i = end - 1;
            }
            else
            {
                result.Append('$');
            }
        }

        return result.ToString();
    }
}
