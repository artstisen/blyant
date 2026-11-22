using System.Text.RegularExpressions;

namespace Blyant.Tests;

/// <summary>
/// The coding style (CLAUDE.md, the rules of CSharp Coding Kit): "is null" and "is not null", never "== null" or
/// "!= null" (a pure check of the reference, which an operator == cannot change); no #region; and in Blyant the types
/// internal, and the classes sealed (or static or abstract). The source is read, without the comments and the texts
/// in quotes. The copies of Blyant Validator in Format follow the same rules (they are changed in Blyant Validator,
/// and copied with tools/formatters.py, which makes their types internal)
/// </summary>
public class StyleTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Blyant.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    // The source files of a project, without what the build makes
    private static IEnumerable<string> SourceFiles(string project)
    {
        var root = Path.Combine(Root(), project);
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Where(path =>
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            return !relative.StartsWith("bin/", StringComparison.Ordinal) &&
                   !relative.StartsWith("obj/", StringComparison.Ordinal);
        });
    }

    // A line of code without the texts in quotes (a character, a string, also $"..." and @"...") and a comment after it
    private static readonly Regex Quoted = new(@"'(?:\\.|[^'\\])'|@""(?:[^""]|"""")*""|\$?""(?:\\.|[^""\\])*""");

    private static string CodeOf(string line)
    {
        var code = Quoted.Replace(line, "\"\"");
        var comment = code.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? code[..comment] : code;
    }

    // The lines of code of the files with a match, as "file:line: text" (the lines inside a raw text """ ... """ are
    // left out)
    private static List<string> Found(IEnumerable<string> files, Regex pattern)
    {
        var found = new List<string>();
        foreach (var path in files)
        {
            var inRaw = false;
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = Regex.Matches(lines[i], "\"\"\"").Count;
                if (inRaw || raw > 0)
                {
                    if (raw % 2 == 1) inRaw = !inRaw;
                    continue;
                }

                if (pattern.IsMatch(CodeOf(lines[i]))) found.Add(Path.GetFileName(path) + ":" + (i + 1) + ": " + lines[i].Trim());
            }
        }

        return found;
    }

    [Fact]
    public void NullIsCheckedWithIsNull_NotWithAnOperator()
    {
        var found = Found(SourceFiles("Blyant").Concat(SourceFiles("Blyant.Tests")), new Regex(@"[!=]=\s*null\b|\bnull\s*[!=]="));
        Assert.True(found.Count == 0, "Write is null or is not null:\n" + string.Join("\n", found));
    }

    [Fact]
    public void ThereAreNoRegions()
    {
        var found = Found(SourceFiles("Blyant").Concat(SourceFiles("Blyant.Tests")), new Regex(@"^\s*#(end)?region\b"));
        Assert.True(found.Count == 0, "Leave out #region:\n" + string.Join("\n", found));
    }

    [Fact]
    public void TheTypesOfBlyant_AreInternal_AndTheClassesSealed()
    {
        var files = SourceFiles("Blyant").ToList();
        var open = Found(files, new Regex(@"^public\s+(static\s+|sealed\s+|abstract\s+|partial\s+)*(class|record|struct|enum|interface)\b"));
        Assert.True(open.Count == 0, "Make the type internal:\n" + string.Join("\n", open));
        // (EngineNotAvailableException, a copy of Blyant Validator, is a class that other exceptions derive from there)
        var unsealed = Found(files, new Regex(@"^(internal\s+)?(partial\s+)?class\b(?!\s+EngineNotAvailableException\b)"));
        Assert.True(unsealed.Count == 0, "Make the class sealed (or static, or abstract):\n" + string.Join("\n", unsealed));
    }
}
