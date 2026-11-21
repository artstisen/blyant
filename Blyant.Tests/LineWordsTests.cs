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
    [InlineData("the (well-known) end", 1, 2, "title", "", "The (Well-known) end")]
    [InlineData("NASA and iPhone", 1, 3, "title", "", "NASA And IPhone")]
    public void Change_ChangesTheWords_AndLeavesWhatIsAroundThem(string line, int first, int last, string action,
        string text, string result)
    {
        Assert.Equal(result, LineWords.Change(line, first, last, action, text));
    }

    [Theory]
    [InlineData("a b c", 2, 2, "a c")]
    [InlineData("a b c", 1, 1, "b c")]
    [InlineData("a b c", 3, 3, "a b")]
    [InlineData("that I want to change, with", 5, 5, "that I want to, with")]
    [InlineData("This is the end.", 4, 4, "This is the.")]
    [InlineData("Read the guide (see chapter 3) now.", 4, 4, "Read the guide (chapter 3) now.")]
    [InlineData("**very bold** text", 1, 1, "**bold** text")]
    [InlineData("a (word) b", 2, 2, "a b")]
    [InlineData("say \"why\" now", 2, 2, "say now")]
    [InlineData("a\tb\tc", 2, 2, "a\tc")]
    [InlineData("a  b", 2, 2, "a")]
    [InlineData("- item", 1, 1, "-")]
    [InlineData("  indented word", 1, 1, "  word")]
    [InlineData("keep **bold** and", 2, 2, "keep and")]
    [InlineData("with a few commands...", 3, 4, "with a...")]
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
        Assert.Equal("#1:1 This  #2:6 is  #3:9 a", rows[0]);
        Assert.All(rows, row => Assert.True(row.Length <= 30, row));
        Assert.Equal("#1:1 This  #2:6 is  #3:9 a  #4:11 line  #5:16 of  #6:19 text  #7:25 that  #8:30 I  #9:32 want  #10:37 to  #11:40 change",
            string.Join("  ", rows));
        Assert.Empty(LineWords.Numbered("-- **", 30));
    }

    private static List<string> BetweenBlanks(string line) =>
        LineWords.Find(line, true).Select(word => line.Substring(word.Start, word.Length)).ToList();

    [Fact]
    public void BetweenBlanks_AWordIsAllBetweenBlanks_WithPunctuationAndMarks()
    {
        Assert.Equal(new[] { "Keep", "**bold**", "and", "a/b,", "\"why?\"", "—", "here." },
            BetweenBlanks("  Keep **bold** and a/b, \"why?\" — here.  "));
        Assert.Empty(BetweenBlanks("   "));
    }

    [Theory]
    [InlineData("a line of text, that", 4, 4, "replace", "note:", "a line of note: that")]
    [InlineData("a few commands...", 3, 3, "replace", "commands.", "a few commands.")]
    [InlineData("keep **bold** here", 2, 2, "title", "", "keep **Bold** here")]
    [InlineData("say \"why?\" now", 2, 2, "upper", "", "say \"WHY?\" now")]
    [InlineData("see a/b, here", 2, 2, "delete", "", "see here")]
    [InlineData("one two", 1, 1, "after", "and a half", "one and a half two")]
    public void BetweenBlanks_Change_TakesTheWholePart(string line, int first, int last, string action, string text,
        string result)
    {
        Assert.Equal(result, LineWords.Change(line, first, last, action, text, true));
    }

    [Fact]
    public void Title_CapitalizesTheFirstLetter_AfterMarks()
    {
        Assert.Equal("**Bold**", LineWords.Title("**bold**"));
        Assert.Equal("**BOLD**", LineWords.Title("**bOLD**"));
        Assert.Equal("\"Why?\"", LineWords.Title("\"why?\""));
        Assert.Equal("--", LineWords.Title("--"));
        Assert.Equal("THE", LineWords.Title("tHE"));
    }

    [Fact]
    public void AWord_RunsOnOverAccentsAndMarks_AndLettersOutsideTheBasicRange()
    {
        // "café" with the accent as a character of its own (as in files from macOS)
        var decomposed = "cafe\u0301 au lait";
        Assert.Equal(new[] { "cafe\u0301", "au", "lait" }, WordsOf(decomposed));
        Assert.Equal("tea au lait", LineWords.Change(decomposed, 1, 1, "replace", "tea"));
        // Hindi: the vowel signs and the virama are part of the word
        Assert.Single(WordsOf("हिन्दी"));
        // A letter outside the basic range (two chars)
        Assert.Equal(new[] { "a\U0001D49Cb" }, WordsOf("a\U0001D49Cb"));
    }

    [Fact]
    public void TitleLine_LeavesLinksCodeAndWebAddressesAsTheyAre()
    {
        Assert.Equal("See [Docs](https://Example.com/API) And NASA",
            LineWords.TitleLine("See [docs](https://Example.com/API) and NASA"));
        Assert.Equal("Use `myVar` Here", LineWords.TitleLine("use `myVar` here"));
        Assert.Equal("Go To https://x.io/path Now", LineWords.TitleLine("go to https://x.io/path now"));
        Assert.Equal("Mail <me@x.io> Today", LineWords.TitleLine("mail <me@x.io> today"));
    }
}
