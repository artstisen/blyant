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

    // A place in a file written after its name in a text: "plan.md:12" or "plan.md:12:5" (a line and column)
    private static readonly Regex LineAfter = new(@"^(.+\.[^.\\/:]+):\d+(?::\d+)?$");

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
        return WithoutPlace(Clean(line[start..end]));
    }

    /// <summary>
    /// A name without a place in the file written after it, which is not part of it: "plan.md#intro", "plan.md:12"
    /// </summary>
    internal static string WithoutPlace(string name)
    {
        var hashAt = name.IndexOf('#');
        if (hashAt > 0 && Path.HasExtension(name[..hashAt])) name = name[..hashAt];
        var place = LineAfter.Match(name);
        return place.Success ? place.Groups[1].Value : name;
    }

    private static bool IsNameChar(char c) => !char.IsWhiteSpace(c) && NotInName.IndexOf(c) < 0;

    /// <summary>
    /// A name as written, without what is around it in a text: blank space, quotes, the marks that end a sentence
    /// ("see notes.md." is notes.md), and the marks of emphasis around it ("*notes.md*", "__notes.md__"). Those are
    /// the same at both ends: a name of its own can start with one ("_index.md")
    /// </summary>
    internal static string Clean(string name)
    {
        name = name.Replace("\r", string.Empty).Replace("\n", " ").Trim().Trim('`', '"', '\'', '<', '>');
        name = name.TrimEnd('.', ',', ';', ':', '!', '?');
        while (name.Length > 2 && (name[0] == '*' || name[0] == '_') && name[^1] == name[0]) name = name[1..^1];
        return name.TrimEnd('.', ',', ';', ':', '!', '?').Trim();
    }

    /// <summary>
    /// The files a name can be. A full path, or a path from the folder of the file being edited, that is there is
    /// the file. A path from the top of a site or repository ("/docs/plan.md") is looked for in the top folder above
    /// the file (one that is a project or has ".git" in it), else in the folders above it, the nearest first, and only
    /// then from the root of the drive. Else the files Blyant knows (the favorites and the history) whose path ends
    /// with the name, upper and lower case alike. A name without an extension can also be a Markdown or Typst file.
    /// "\" in a name is a separator on all systems (a name written on Windows). The file being edited is left out
    /// </summary>
    /// <param name="name">The name, as written</param>
    /// <param name="currentFile">The file being edited (null: none)</param>
    /// <param name="known">The files Blyant knows</param>
    /// <param name="isFile">Tells if a file is there</param>
    /// <param name="isTop">Tells if a folder is the top of a site or repository (null: none is)</param>
    internal static List<string> Find(string name, string? currentFile, IEnumerable<string> known,
        Func<string, bool> isFile, Func<string, bool>? isTop = null)
    {
        var found = new List<string>();
        name = Clean(name).Replace('\\', '/');
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

        // A full path, or one from the folder of the file being edited (a path from the top is looked for below)
        var folder = currentFile == null ? null : Path.GetDirectoryName(currentFile);
        var fromTop = names.Where(IsFromTop).ToList();
        foreach (var each in names.Where(each => !IsFromTop(each)))
        {
            if (Path.IsPathRooted(each)) Add(each);
            else if (folder != null) Add(Path.Combine(folder, each));
        }

        if (found.Count > 0) return found;
        if (fromTop.Count > 0)
        {
            // The folders above the file, below the root of the drive, up to the top of a site or repository
            var above = new List<string>();
            for (var each = folder; each != null && Path.GetPathRoot(each) != each; each = Path.GetDirectoryName(each))
            {
                above.Add(each);
                if (isTop?.Invoke(each) == true) break;
            }

            if (above.Count > 0 && isTop?.Invoke(above[^1]) == true)
            {
                foreach (var each in fromTop) Add(Path.Combine(above[^1], each.TrimStart('/')));
                if (found.Count > 0) return found;
            }

            foreach (var place in above)
            {
                foreach (var each in fromTop) Add(Path.Combine(place, each.TrimStart('/')));
            }

            if (found.Count > 0) return found;
            // Last, from the root of the drive (on Linux and macOS a full path is written so too)
            foreach (var each in fromTop) Add(each);
            if (found.Count > 0) return found;
        }

        // The files Blyant knows, by the end of their path ("./" and "../" in front of the name are left out)
        foreach (var path in known)
        {
            if (names.Any(each => EndsWith(path, Relative(each)))) Add(path);
        }

        return found;
    }

    // A path from the top of a site or repository, or of the drive: "/docs/plan.md" (not a network path "//server")
    private static bool IsFromTop(string name) =>
        name.StartsWith('/') && !name.StartsWith("//", StringComparison.Ordinal);

    // A name without what leads to it from a folder ("../notes/plan.md" and "/notes/plan.md" are notes/plan.md)
    private static string Relative(string name)
    {
        name = name.Replace('\\', '/').TrimStart('/');
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
