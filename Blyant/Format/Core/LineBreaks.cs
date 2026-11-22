// Copied from Blyant Validator 0.6.3 (src/Blyant.Validator.Core/LineBreaks.cs) by tools/formatters.py: not changed by hand in Blyant.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
namespace Blyant.Format.Core;

/// <summary>
/// The line breaks of a document: CR LF, LF and CR. Not the other breaks of Unicode that string.ReplaceLineEndings
/// takes (NEL, FF, U+2028, U+2029): in a document they are text, and changing them would change what it means.
/// </summary>
internal static class LineBreaks
{
    /// <summary>The text with each line break written as <paramref name="newline"/>.</summary>
    public static string Normalize(string text, string newline) =>
        text.Contains('\n') || text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", newline) : text;

    /// <summary>The number of line breaks in the text (CR LF counts once).</summary>
    public static int Count(string text)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            if (text[i] is '\n' or '\r') count++;
        }

        return count;
    }

    /// <summary>The lines of the text, without the line breaks (a break at the end gives an empty last line).</summary>
    public static string[] Split(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
}
