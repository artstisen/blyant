namespace Blyant.Tests;

/// <summary>
/// The calculator of "calc" in the shell: the order of the signs, decimals, the result as text, and the errors
/// </summary>
public class CalculatorTests
{
    [Theory]
    [InlineData("1250 * 1.25 + 80", "1642.5")]
    [InlineData("2+3*4", "14")]
    [InlineData("  2 + 3 * 4  ", "14")]
    [InlineData("(2 + 3) * 4", "20")]
    [InlineData("((1))", "1")]
    [InlineData("0.1 + 0.2", "0.3")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("10 / 3", "3.3333333333")]
    [InlineData("2 / 3", "0.6666666667")]
    [InlineData("1,5 * 2", "3")]
    [InlineData(".5 + 1.", "1.5")]
    [InlineData("17 % 5", "2")]
    [InlineData("-17 % 5", "-2")]
    [InlineData("7 - 10", "-3")]
    [InlineData("-5", "-5")]
    [InlineData("--5", "5")]
    [InlineData("+5", "5")]
    [InlineData("3 * -2", "-6")]
    [InlineData("2 ^ 10", "1024")]
    [InlineData("2 ^ -2", "0.25")]
    [InlineData("-2 ^ 2", "-4")]
    [InlineData("(-2) ^ 2", "4")]
    [InlineData("2 ^ 3 ^ 2", "512")]
    [InlineData("2 ^ 0.5", "1.4142135624")]
    [InlineData("0 * -1", "0")]
    [InlineData("1 - 1", "0")]
    [InlineData("10 - 2 - 3", "5")]
    [InlineData("100 / 10 / 2", "5")]
    [InlineData("0.00000000001", "0")]
    public void Calculate_GivesTheResult(string expression, string result)
    {
        Assert.Equal((result, (string?)null), Calculator.Calculate(expression));
    }

    [Fact]
    public void Calculate_TellsWhatIsWrong_AndWhere()
    {
        Assert.Equal(Lang.T("Give a calculation: calc 2 + 3 * 4"), Calculator.Calculate("  ").Error);
        Assert.Equal(Lang.T("Division by zero"), Calculator.Calculate("1 / 0").Error);
        Assert.Equal(Lang.T("Division by zero"), Calculator.Calculate("5 % 0").Error);
        Assert.Equal(Lang.T("Division by zero"), Calculator.Calculate("0 ^ -1").Error);
        Assert.Equal(Lang.T("A ) is missing"), Calculator.Calculate("(1 + 2").Error);
        Assert.Equal(Lang.T("A number is missing at the end"), Calculator.Calculate("2 +").Error);
        Assert.Equal(Lang.F("Not understood: {0} (position {1})", "12x", 3), Calculator.Calculate("12x").Error);
        Assert.Equal(Lang.F("Not understood: {0} (position {1})", ")", 2), Calculator.Calculate("2)").Error);
        Assert.Equal(Lang.F("Not understood: {0} (position {1})", "3", 3), Calculator.Calculate("2 3").Error);
        Assert.Equal(Lang.F("Not understood: {0} (position {1})", "*", 5), Calculator.Calculate("2 + * 3").Error);
        Assert.Equal(Lang.F("Not understood: {0} (position {1})", "(1", 4), Calculator.Calculate("(2)(1)").Error);
        Assert.Equal(Lang.T("The number is too large"), Calculator.Calculate("10 ^ 40").Error);
        Assert.Equal(Lang.T("The number is too large"), Calculator.Calculate("79228162514264337593543950335 + 1").Error);
        Assert.Equal(Lang.T("No result: a negative number to a power that is not whole"),
            Calculator.Calculate("(-8) ^ 0.5").Error);
        // Never code: a name is not understood
        Assert.Null(Calculator.Calculate("Math.Max(1, 2)").Result);
    }

    [Fact]
    public void Format_RoundsToTenDecimals_WithAPoint_AndNoZerosAtTheEnd()
    {
        Assert.Equal("2.5", Calculator.Format(2.50m));
        Assert.Equal("0.1234567891", Calculator.Format(0.12345678905m));
        Assert.Equal("-1.5", Calculator.Format(-1.5m));
        Assert.Equal("1000000", Calculator.Format(1000000m));
    }
}
