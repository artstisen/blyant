using System.Text;
using System.Text.RegularExpressions;

namespace Blyant;

/// <summary>
/// The registers of the shell: named values ("docs", "note") that a command line uses with $name ("copy plot.md
/// $docs"). A value is a text or a path. They are the shell's own (not the registers of the editor), and kept in the
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
        foreach (var line in TextBuffer.Normalize(text).Split('\n'))
        {
            if (line.Trim().Length == 0) continue;
            var tab = line.IndexOf('\t');
            if (tab <= 0 || !IsName(line[..tab]))
            {
                Kept.Add(line);
                continue;
            }

            Values[line[..tab].ToLowerInvariant()] = line[(tab + 1)..];
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
            var text = string.Concat(Values.Select(pair => pair.Key + "\t" + pair.Value + "\n")) +
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
    /// The value of a register, or null
    /// </summary>
    internal static string? Get(string name) => Values.TryGetValue(name.ToLowerInvariant(), out var value) ? value : null;

    /// <summary>
    /// Give a register a value (one line: a line break or a tab becomes a blank)
    /// </summary>
    internal static void Set(string name, string value) =>
        Values[name.ToLowerInvariant()] = value.Replace("\r", string.Empty).Replace('\n', ' ').Replace('\t', ' ');

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
