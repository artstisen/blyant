using System.Text.RegularExpressions;

namespace Blyant;

/// <summary>
/// "gf" in the editor: the name of a file written in the text (as in a table of contents), and the files it can be.
/// It does not touch the console, so it can be tested
/// </summary>
internal static class FileReference
{
    // What a file name in a text stops at: blank space, quotes, brackets and the characters no file can have
    private const string NotInName = "<>|\"*?`'()[]{},;";

    // A Markdown link, [text](target) or [text](<target with spaces>), with a title after the target if any
    private static readonly Regex Link = new(@"\[[^\]]*\]\((?:<([^>]+)>|([^)\s]+))(?:\s+""[^""]*"")?\)");

    // The kinds a name without an extension can be (as written in a table of contents)
    private static readonly string[] Extensions = { ".md", ".typ" };

    /// <summary>
    /// The name of a file at a place in a line: the target of a Markdown link the place is in, or else the word of
    /// the characters a file name has. Empty if there is none
    /// </summary>
    internal static string At(string line, int column)
    {
        foreach (Match link in Link.Matches(line))
        {
            if (column < link.Index || column >= link.Index + link.Length) continue;
            var target = link.Groups[1].Success ? link.Groups[1].Value : link.Groups[2].Value;
            // A place in the file ("#heading") is not part of its name, and a link writes a blank space as %20
            var hash = target.IndexOf('#');
            if (hash >= 0) target = target[..hash];
            try
            {
                target = Uri.UnescapeDataString(target);
            }
            catch (UriFormatException)
            {
                // Written as it is
            }

            return Clean(target);
        }

        if (line.Length == 0) return string.Empty;
        column = Math.Clamp(column, 0, line.Length - 1);
        if (!IsNameChar(line[column])) return string.Empty;
        var start = column;
        while (start > 0 && IsNameChar(line[start - 1])) start--;
        var end = column + 1;
        while (end < line.Length && IsNameChar(line[end])) end++;
        return Clean(line[start..end]);
    }

    private static bool IsNameChar(char c) => !char.IsWhiteSpace(c) && NotInName.IndexOf(c) < 0;

    /// <summary>
    /// A name as written, without what is around it in a text: blank space, quotes, and the marks that end a
    /// sentence ("see notes.md." is notes.md)
    /// </summary>
    internal static string Clean(string name)
    {
        name = name.Replace("\r", string.Empty).Replace("\n", " ").Trim().Trim('`', '"', '\'', '<', '>', '*', '_');
        return name.TrimEnd('.', ',', ';', ':', '!', '?').Trim();
    }

    /// <summary>
    /// The files a name can be. A full path, or a path from the folder of the file being edited, that is there is
    /// the file; else the files Blyant knows (the favorites and the history) whose path ends with the name, upper and
    /// lower case alike. A name without an extension can also be a Markdown or Typst file. The file being edited is
    /// left out
    /// </summary>
    /// <param name="name">The name, as written</param>
    /// <param name="currentFile">The file being edited (null: none)</param>
    /// <param name="known">The files Blyant knows</param>
    /// <param name="isFile">Tells if a file is there</param>
    internal static List<string> Find(string name, string? currentFile, IEnumerable<string> known,
        Func<string, bool> isFile)
    {
        var found = new List<string>();
        name = Clean(name);
        if (name.Length == 0) return found;
        var names = Path.HasExtension(name) ? new[] { name } : Extensions.Select(ext => name + ext).Prepend(name).ToArray();

        void Add(string path)
        {
            try
            {
                path = Path.GetFullPath(path);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
            {
                return;
            }

            if (currentFile != null && FileOperations.SamePath(path, currentFile)) return;
            if (found.Any(other => FileOperations.SamePath(other, path)) || !isFile(path)) return;
            found.Add(path);
        }

        // A full path, or one from the folder of the file being edited
        var folder = currentFile == null ? null : Path.GetDirectoryName(currentFile);
        foreach (var each in names)
        {
            if (Path.IsPathRooted(each)) Add(each);
            else if (folder != null) Add(Path.Combine(folder, each));
        }

        if (found.Count > 0) return found;

        // The files Blyant knows, by the end of their path ("./" and "../" in front of the name are left out)
        foreach (var path in known)
        {
            if (names.Any(each => EndsWith(path, Relative(each)))) Add(path);
        }

        return found;
    }

    // A name without what leads to it from a folder ("../notes/plan.md" is notes/plan.md)
    private static string Relative(string name)
    {
        name = name.Replace('\\', '/');
        while (name.StartsWith("./", StringComparison.Ordinal) || name.StartsWith("../", StringComparison.Ordinal))
        {
            name = name[(name.IndexOf('/') + 1)..];
        }

        return name;
    }

    // True if a path ends with a name, as a whole part of it ("plan.md" ends ".../notes/plan.md", not "myplan.md")
    private static bool EndsWith(string path, string name)
    {
        if (name.Length == 0) return false;
        path = path.Replace('\\', '/');
        return path.Equals(name, StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase);
    }
}
