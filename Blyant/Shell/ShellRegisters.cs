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
    /// The lines of the file that were not registers when it was read last (no name, no tab, or not a name a register
    /// can have): skipped, and told by "reg"
    /// </summary>
    internal static int Skipped { get; private set; }

    // The file as it was when it was read or written last (its time and size), to tell when it was changed by hand
    private static (DateTime Time, long Size)? _stamp;

    /// <summary>
    /// Read the registers from their file (none: no registers)
    /// </summary>
    internal static void Load()
    {
        Values.Clear();
        Skipped = 0;
        _stamp = Stamp();
        string text;
        try
        {
            if (!File.Exists(FilePath)) return;
            text = File.ReadAllText(FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var line in TextBuffer.Normalize(text).Split('\n'))
        {
            if (line.Trim().Length == 0) continue;
            var tab = line.IndexOf('\t');
            if (tab <= 0 || !IsName(line[..tab]))
            {
                Skipped++;
                continue;
            }

            Values[line[..tab].ToLowerInvariant()] = line[(tab + 1)..];
        }
    }

    /// <summary>
    /// Read the registers again when their file has been changed since (edited by hand, or by another Blyant): before
    /// each command of the shell. Every change made in the shell is written at once, so none is lost
    /// </summary>
    /// <returns>True if they were read again</returns>
    internal static bool Refresh()
    {
        if (Stamp() == _stamp) return false;
        Load();
        return true;
    }

    // The time and size of the file (null: it is not there, or cannot be looked at)
    private static (DateTime Time, long Size)? Stamp()
    {
        try
        {
            var file = new FileInfo(FilePath);
            return file.Exists ? (file.LastWriteTimeUtc, file.Length) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
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
            var text = string.Concat(Values.Select(pair => pair.Key + "\t" + pair.Value + "\n"));
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(text));
            // (written here: not read again for it; the lines skipped are gone from the file)
            _stamp = Stamp();
            Skipped = 0;
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
    internal static void Clear()
    {
        Values.Clear();
        Skipped = 0;
    }

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
