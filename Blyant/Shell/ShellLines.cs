namespace Blyant;

/// <summary>
/// "lines": lines of a file copied to another file, or cut ("cut": moved there), without opening them. The lines and
/// the place are those of shift. What is written is never logged, only the paths and the lines
/// </summary>
internal sealed partial class ShellSession
{
    // "lines notes.md 4-6 > plan.md bottom", "lines notes.md +1 14 > plan.md top cut", "lines todo.md "Buy milk" >
    // done.md Done+": the lines (as shift takes them, or "+1 14": 14 lines from line 1, as peek takes them) put in the
    // other file at the place (as shift: top, bottom, +12 before line 12, under a heading, Heading+ at the end of its
    // section). "copy" (the default) or "cut" last, not in quotes (a heading "Cut" is written in quotes): cut takes the
    // lines out of their file after they are in the other, so a failure leaves them in both, never in neither. Not
    // asked: no line is lost. As insert and shift: each file keeps its line endings and byte order mark, only files in
    // UTF-8 and not ones Blyant uses, and the other file is then the last list, at the lines put in
    private void CopyLines(string argument)
    {
        var words = Words(argument);
        var arrow = words.IndexOf(">");
        if (arrow is < 2 or > 3 || words.Count < arrow + 3 || (arrow == 3 && !words[1].StartsWith('+')))
        {
            Error(Lang.T("Give the lines and where to: lines notes.md 4-6 > plan.md bottom (cut: move them)"));
            return;
        }

        // The place, and "copy" or "cut" after it
        var place = AfterWords(argument, arrow + 2).Trim();
        var cut = false;
        var blank = place.LastIndexOfAny([' ', '\t']);
        if (blank > 0 && place[(blank + 1)..].ToLowerInvariant() is "copy" or "cut")
        {
            cut = place[(blank + 1)..].Equals("cut", StringComparison.OrdinalIgnoreCase);
            place = place[..blank].Trim();
        }

        place = place.Trim('"').Trim();
        if (EditableFile(words[0]) is not { } source) return;
        if (EditableFile(words[arrow + 1]) is not { } target) return;
        if (FileOperations.SamePath(LinkedPath(source.Item.Path), LinkedPath(target.Item.Path)))
        {
            Error(Lang.T("The lines are in that file already: shift moves lines in a file"));
            return;
        }

        var lines = LinesOfText(source.File.Text);
        if (PickedLines(words[1], arrow == 3 ? words[2] : null, lines, source.Item.Path) is not { } picked) return;
        var targetLines = LinesOfText(target.File.Text);
        if (PlaceIn(place, targetLines, target.File.Text, target.Item.Path, null) is not { } position) return;

        var taken = picked.Select(n => lines[n - 1]).ToList();
        var (text, at) = WithLinesAt(target.File.Text, taken, position);
        var result = target.File.Save(text);
        // (the paths and the lines, never the text)
        var what = source.Item.Path + ":" + Numbers(picked) + " to " + target.Item.Path + ":" + at;
        FileExplorer.LogFileOperation(cut ? "Cut lines" : "Copy lines", what, result);
        if (result != DialogText.Success)
        {
            Error(result);
            return;
        }

        LastList = new List<Item> { new(target.Item.Path, false, (at, 1)) };
        var now = LinesOfText(text);
        var width = (at + taken.Count - 1).ToString().Length;
        for (var i = at; i < at + taken.Count; i++) Line(i.ToString().PadLeft(width + 2) + "  " + now[i - 1]);
        Line(taken.Count == 1
            ? Lang.F("{0}: line {1} put in", target.Item.Path, at)
            : Lang.F("{0}: lines {1}-{2} put in", target.Item.Path, at, at + taken.Count - 1));
        if (!cut) return;

        // Cut: out of their file, as it was read (changed since: left there)
        if (Changed(source.Item.Path, source.File))
        {
            Error(Lang.F("{0} was changed meanwhile: the lines are copied, not taken out", source.Item.Path));
            return;
        }

        var left = WithoutLines(source.File.Text, picked);
        var removed = source.File.Save(left);
        FileExplorer.LogFileOperation("Take out lines", source.Item.Path + ":" + Numbers(picked), removed);
        if (removed != DialogText.Success)
        {
            Error(Lang.F("{0}: the lines are copied, but not taken out: {1}", source.Item.Path, removed));
            return;
        }

        Line(picked.Count == 1
            ? Lang.F("{0}: line {1} taken out", source.Item.Path, picked[0])
            : Lang.F("{0}: {1} lines taken out", source.Item.Path, picked.Count));
    }

    // The file a path is (a link: the file it links to, as the file is saved there), to tell the same file under
    // two names
    private static string LinkedPath(string path)
    {
        var full = Path.GetFullPath(path);
        try
        {
            return new FileInfo(full).ResolveLinkTarget(true)?.FullName ?? full;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return full;
        }
    }

    // The numbers of lines for the log ("4-6" for 4, 5 and 6; "4,7")
    internal static string Numbers(List<int> lines) =>
        lines.Count > 1 && lines[^1] - lines[0] == lines.Count - 1
            ? lines[0] + "-" + lines[^1]
            : string.Join(',', lines);

    /// <summary>
    /// A text with lines put in before its line "position" (from 0; the number of lines: after the last), and the
    /// line they start at. The lines end in "\n"; a text that ends with a line break keeps it, and one that does not
    /// gets none after lines put in at the end
    /// </summary>
    internal static (string Text, int Line) WithLinesAt(string text, IReadOnlyList<string> added, int position)
    {
        text = TextBuffer.Normalize(text);
        if (text.Length == 0) return (string.Join("\n", added) + "\n", 1);
        var endsWithBreak = text.EndsWith('\n');
        var lines = LinesOfText(text);
        var index = Math.Clamp(position, 0, lines.Count);
        lines.InsertRange(index, added);
        return (string.Join("\n", lines) + (endsWithBreak ? "\n" : string.Empty), index + 1);
    }

    /// <summary>
    /// A text without some of its lines (from 1). A text that ends with a line break keeps it; none left: empty
    /// </summary>
    internal static string WithoutLines(string text, IReadOnlyCollection<int> taken)
    {
        text = TextBuffer.Normalize(text);
        var endsWithBreak = text.EndsWith('\n');
        var rest = LinesOfText(text).Where((_, i) => !taken.Contains(i + 1)).ToList();
        if (rest.Count == 0) return string.Empty;
        return string.Join("\n", rest) + (endsWithBreak ? "\n" : string.Empty);
    }
}
