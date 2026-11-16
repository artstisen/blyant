using System.Globalization;

namespace Blyant;

/// <summary>
/// Simple calculations, for "calc" in the shell: + - * /, % (the remainder), ^ (a power), parentheses and a minus or
/// plus in front of a number, with * / % before + -, and ^ before them ("-2 ^ 2" is -4; "2 ^ 3 ^ 2" is 2 ^ 9).
/// Calculated in decimal, so 0.1 + 0.2 is 0.3 exactly. A number takes "." or "," as the decimal point (there are no
/// thousands separators); the result is written with "." and at most 10 decimals, without zeros at the end. The
/// expression is read here, never run as code. Does not touch the console, so it can be tested
/// </summary>
internal static class Calculator
{
    /// <summary>
    /// The most decimals in a result (10 / 3 is 3.3333333333)
    /// </summary>
    internal const int MaxDecimals = 10;

    /// <summary>
    /// The most parentheses and signs inside each other ("((((1))))", "---1", "2 ^ 2 ^ 2"): more is told, as the
    /// reader would otherwise run out of stack, which ends Blyant
    /// </summary>
    internal const int MaxDepth = 200;

    // A whole exponent up to this is calculated exactly, by multiplying (a larger one by double)
    private const long MaxWholeExponent = 1_000_000_000;

    /// <summary>
    /// The result of an expression as text ("1642.5"), or the error that stopped it (null when there is a result)
    /// </summary>
    internal static (string? Result, string? Error) Calculate(string expression)
    {
        if (expression.Trim().Length == 0) return (null, Lang.T("Give a calculation: calc 2 + 3 * 4"));
        var reader = new Reader(expression);
        try
        {
            var value = reader.Sum();
            reader.SkipBlanks();
            if (!reader.AtEnd) return (null, reader.Unexpected());
            return (Format(value), null);
        }
        catch (CalcException e)
        {
            return (null, e.Message);
        }
        catch (DivideByZeroException)
        {
            return (null, Lang.T("Division by zero"));
        }
        catch (OverflowException)
        {
            return (null, Lang.T("The number is too large"));
        }
    }

    /// <summary>
    /// A value as a result: rounded to MaxDecimals, "." as the decimal point, no zeros at the end ("2.5", "14")
    /// </summary>
    internal static string Format(decimal value)
    {
        var rounded = Math.Round(value, MaxDecimals, MidpointRounding.AwayFromZero);
        // ("-0" is 0)
        if (rounded == 0) return "0";
        return rounded.ToString("0." + new string('#', MaxDecimals), CultureInfo.InvariantCulture);
    }

    // An error to tell (in the language in use)
    private sealed class CalcException(string message) : Exception(message);

    // Reads an expression from the left: Sum is + and -, Product * / %, Signed a minus or plus in front, Power ^, and
    // Primary a number or a part in parentheses
    private sealed class Reader(string text)
    {
        private int _at;
        private int _depth;

        internal bool AtEnd => _at >= text.Length;

        internal void SkipBlanks()
        {
            while (_at < text.Length && char.IsWhiteSpace(text[_at])) _at++;
        }

        // The next sign (after blanks), if it is one of these: taken
        private bool Take(char sign)
        {
            SkipBlanks();
            if (_at >= text.Length || text[_at] != sign) return false;
            _at++;
            return true;
        }

        internal decimal Sum()
        {
            var value = Product();
            while (true)
            {
                if (Take('+')) value += Product();
                else if (Take('-')) value -= Product();
                else return value;
            }
        }

        private decimal Product()
        {
            var value = Signed();
            while (true)
            {
                if (Take('*')) value *= Signed();
                else if (Take('/')) value /= Signed();
                else if (Take('%')) value %= Signed();
                else return value;
            }
        }

        // (every parenthesis, sign in front and power goes through here: the depth is counted here)
        private decimal Signed()
        {
            if (++_depth > MaxDepth)
            {
                throw new CalcException(Lang.F("Too many parentheses or signs inside each other (at most {0})",
                    MaxDepth));
            }

            try
            {
                if (Take('-')) return -Signed();
                if (Take('+')) return Signed();
                return Power();
            }
            finally
            {
                _depth--;
            }
        }

        // (the power before the sign in front: "-2 ^ 2" is -4; from the right: "2 ^ 3 ^ 2" is 2 ^ 9)
        private decimal Power()
        {
            var value = Primary();
            return Take('^') ? Raise(value, Signed()) : value;
        }

        private decimal Primary()
        {
            SkipBlanks();
            if (AtEnd) throw new CalcException(Lang.T("A number is missing at the end"));
            if (Take('('))
            {
                var value = Sum();
                if (!Take(')'))
                {
                    SkipBlanks();
                    throw new CalcException(AtEnd ? Lang.T("A ) is missing") : Unexpected());
                }

                return value;
            }

            var start = _at;
            var point = false;
            while (_at < text.Length && (char.IsAsciiDigit(text[_at]) || (!point && text[_at] is '.' or ',')))
            {
                if (text[_at] is '.' or ',') point = true;
                _at++;
            }

            var number = text[start.._at].Replace(',', '.');
            if (number.Length == 0 || number == ".")
            {
                _at = start;
                throw new CalcException(Unexpected());
            }

            return decimal.Parse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        // What is not understood where the reader is: the characters up to the next blank or sign, and the place
        internal string Unexpected()
        {
            var end = _at;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && (end == _at || !"+-*/%^()".Contains(text[end])))
            {
                end++;
            }

            // (from the start of the word it is in: "12x" at the x is "12x", position 3)
            var start = _at;
            while (start > 0 && char.IsLetterOrDigit(text[start - 1]) && char.IsLetterOrDigit(text[_at])) start--;
            return Lang.F("Not understood: {0} (position {1})", text[start..end], _at + 1);
        }

        // A power: a whole exponent exactly (by multiplying), another by double (as near as it comes). 0 to a
        // negative power is a division by zero
        private static decimal Raise(decimal value, decimal exponent)
        {
            if (value == 0 && exponent < 0) throw new DivideByZeroException();
            if (exponent == decimal.Truncate(exponent) && Math.Abs(exponent) <= MaxWholeExponent)
            {
                // (a negative exponent as a positive one of 1 / value: 0.5 ^ -90 is 2 ^ 90, exactly; 2 ^ -100 comes
                // out as 0, not as a power of 2 too large first)
                var factor = exponent < 0 ? 1m / value : value;
                var result = 1m;
                var n = (long)Math.Abs(exponent);
                while (n > 0)
                {
                    if ((n & 1) == 1) result *= factor;
                    n >>= 1;
                    if (n > 0) factor *= factor;
                }

                return result;
            }

            var power = Math.Pow((double)value, (double)exponent);
            if (double.IsNaN(power)) throw new CalcException(Lang.T("No result: a negative number to a power that is not whole"));
            if (double.IsInfinity(power)) throw new OverflowException();
            return (decimal)power;
        }
    }
}
