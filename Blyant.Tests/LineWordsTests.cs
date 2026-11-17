namespace Blyant.Tests;

/// <summary>
/// The words of a line for "words" and "word" in the shell: what a word is, and the changes made to them
/// </summary>
public class LineWordsTests
{
    private static List<string> WordsOf(string line) =>
        LineWords.Find(line).Select(word => line.Substring(word.Start, word.Length)).ToList();

    [Fact]
    public void AWord_IsLettersAndDigits_WithApostrophesAndHyphensInside()
    {
        Assert.Equal(new[] { "This", "is", "text", "don't", "well-known", "it’s", "2026" },
            WordsOf("This is **text**, don't (well-known) it’s - 2026!"));
        // Not a word: a mark, a dash alone, a hyphen at the end
        Assert.Equal(new[] { "end" }, WordsOf("— - * end-"));
        Assert.Equal(new[] { "Æble", "øl" }, WordsOf("Æble, øl."));
        Assert.Empty(WordsOf("  --  "));
    }

    [Theory]
    [InlineData("I want to go.", 2, 2, "replace", "wish", "I wish to go.")]
    [InlineData("text, that", 1, 1, "replace", "note", "note, that")]
    [InlineData("with a few commands", 2, 4, "replace", "for now", "with for now")]
    [InlineData("with a few", 3, 3, "before", "handful of", "with a handful of few")]
    [InlineData("a line of text, that", 4, 4, "after", "or two", "a line of text or two, that")]
    [InlineData("keep **bold** here", 2, 2, "upper", "", "keep **BOLD** here")]
    [InlineData("SHOUT here", 1, 1, "lower", "", "shout here")]
    [InlineData("this is a line", 1, 4, "title", "", "This Is A Line")]
    [InlineData("tHE (well-known) end", 1, 2, "title", "", "The (Well-known) end")]
    public void Change_ChangesTheWords_AndLeavesWhatIsAroundThem(string line, int first, int last, string action,
        string text, string result)
    {
        Assert.Equal(result, LineWords.Change(line, first, last, action, text));
    }

    [Theory]
    [InlineData("a b c", 2, 2, "a c")]
    [InlineData("a b c", 1, 1, "b c")]
    [InlineData("a b c", 3, 3, "a b")]
    [InlineData("that I want to change, with", 5, 5, "that I want to with")]
    [InlineData("keep **bold** and", 2, 2, "keep and")]
    [InlineData("with a few commands...", 3, 4, "with a")]
    [InlineData("see a/b here", 2, 2, "see /b here")]
    [InlineData("one", 1, 1, "")]
    public void Delete_TakesTheWordsWithWhatIsAroundThem_AndOneBlank(string line, int first, int last, string result)
    {
        Assert.Equal(result, LineWords.Change(line, first, last, "delete", string.Empty));
    }

    [Fact]
    public void TitleLine_AndNumbered_InRowsNoWiderThanTheWidth()
    {
        Assert.Equal("# My Notes On The Storm", LineWords.TitleLine("# my notes on the storm"));
        var rows = LineWords.Numbered("This is a line of text, that I want to change", 30);
        Assert.Equal("1 This  2 is  3 a  4 line", rows[0]);
        Assert.All(rows, row => Assert.True(row.Length <= 30, row));
        Assert.Equal("1 This  2 is  3 a  4 line  5 of  6 text  7 that  8 I  9 want  10 to  11 change",
            string.Join("  ", rows));
        Assert.Empty(LineWords.Numbered("-- **", 30));
    }
}
