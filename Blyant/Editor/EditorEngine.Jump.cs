namespace Blyant;

/// <summary>
/// The jump to a word on the screen (Enter in NORMAL and VISUAL mode, as mini.jump2d for Neovim): every word that
/// starts on the rows shown is a spot, and the spots are given labels made of the letters a to z. The letters are
/// spread evenly over the spots in their order on the screen, and when there are more spots than letters, the spots
/// that share a letter are given a second one (and a third, if needed) the same way. Typing a letter keeps the spots
/// whose label starts with it; when one is left, the cursor goes there. Enter goes to the first spot left, and Esc
/// cancels. A jump is not made in a macro (the labels depend on what is on the screen), and Enter there, with a count
/// or after an operator, still goes down to the first character of a line
/// </summary>
internal sealed partial class EditorEngine
{
    /// <summary>
    /// The letters of the labels
    /// </summary>
    internal const string JumpLetters = "abcdefghijklmnopqrstuvwxyz";

    // The spots left and their whole labels while a jump is made (null: no jump), and the letters typed so far
    private List<(TextPos Pos, string Label)>? _jumpSpots;
    private string _jumpTyped = string.Empty;

    /// <summary>
    /// True while a jump is made: the keys are the letters of the labels
    /// </summary>
    internal bool IsJumping => _jumpSpots != null;

    /// <summary>
    /// The labels of the spots on a line while a jump is made: the char index where the word starts, and the letters
    /// still to be typed for it (the first is the next to type)
    /// </summary>
    internal IEnumerable<(int Col, string Label)> JumpLabels(int line)
    {
        if (_jumpSpots == null) yield break;
        foreach (var (pos, label) in _jumpSpots)
        {
            if (pos.Line == line) yield return (pos.Col, label[_jumpTyped.Length..]);
        }
    }

    /// <summary>
    /// The labels of a number of spots, in their order: the letters are spread evenly over the spots (the first
    /// letters get one spot more when they do not divide evenly), and the spots that share a letter are labeled
    /// again, after it, the same way. A single spot has an empty label
    /// </summary>
    internal static List<string> JumpLabelsFor(int count, string letters = JumpLetters)
    {
        var labels = new string[count];
        Label(0, count, string.Empty);
        return labels.ToList();

        void Label(int first, int spots, string start)
        {
            if (spots == 1)
            {
                labels[first] = start;
                return;
            }

            var groups = Math.Min(spots, letters.Length);
            var size = spots / groups;
            var more = spots % groups;
            for (var group = 0; group < groups; group++)
            {
                var inGroup = size + (group < more ? 1 : 0);
                Label(first, inGroup, start + letters[group]);
                first += inGroup;
            }
        }
    }

    // Enter: the labels are shown at the words on the screen (a single word is jumped to at once)
    private void StartJump()
    {
        var spots = JumpSpots();
        if (spots.Count == 0)
        {
            SetError(Lang.T("No word to jump to on the screen"));
            return;
        }

        if (spots.Count == 1)
        {
            RunMotion(new Motion(spots[0]));
            return;
        }

        _jumpSpots = spots.Zip(JumpLabelsFor(spots.Count)).ToList();
        _jumpTyped = string.Empty;
        ShowJumpHint();
    }

    private void ShowJumpHint() => SetMessage(Lang.T("Type the letters shown at a word (Esc: cancel)"), false);

    private void EndJump()
    {
        _jumpSpots = null;
        _jumpTyped = string.Empty;
    }

    // A key while a jump is made: a letter of the labels, Enter (the first spot left) or Esc. Other keys do nothing
    private void HandleJumpKey(ConsoleKeyInfo key)
    {
        if (IsEscape(key))
        {
            EndJump();
            return;
        }

        TextPos? target = null;
        if (IsEnter(key))
        {
            target = _jumpSpots![0].Pos;
        }
        else if (IsText(key))
        {
            var typed = _jumpTyped + char.ToLowerInvariant(key.KeyChar);
            var left = _jumpSpots!.Where(spot => spot.Label.StartsWith(typed, StringComparison.Ordinal)).ToList();
            if (left.Count == 1)
            {
                target = left[0].Pos;
            }
            else if (left.Count > 1)
            {
                _jumpSpots = left;
                _jumpTyped = typed;
            }
        }

        if (target is { } pos)
        {
            EndJump();
            RunMotion(new Motion(pos));
            return;
        }

        ShowJumpHint();
    }

    // The starts of the words on the rows shown, from the top (not the one under the cursor). Without word wrap, only
    // those in the part of the lines shown
    private List<TextPos> JumpSpots()
    {
        var spots = new List<TextPos>();
        var rows = Math.Min(TopRow + Height, Layout.TotalRows());
        for (var row = TopRow; row < rows; row++)
        {
            var (line, segment) = Layout.LineAtRow(row);
            var text = Buffer[line];
            var end = Layout.SegmentEnd(line, segment);
            for (var col = Layout.SegmentStart(line, segment); col < end;
                 col += Math.Max(1, TextMetrics.ElementLength(text, col)))
            {
                if (!IsWordStart(text, col)) continue;
                if (!Wrap)
                {
                    var cell = TextMetrics.WidthOfRange(text, 0, col);
                    if (cell < LeftCell || cell >= LeftCell + Layout.Width) continue;
                }

                var pos = new TextPos(line, col);
                if (pos != Cursor) spots.Add(pos);
            }
        }

        return spots;
    }

    // A word starts at a letter or digit that does not follow one (an apostrophe between letters is in the word:
    // "don't" is one word)
    internal static bool IsWordStart(string text, int col)
    {
        if (!char.IsLetterOrDigit(text, col)) return false;
        if (col == 0) return true;
        var before = col - TextMetrics.PreviousElementLength(text, col);
        if (char.IsLetterOrDigit(text, before)) return false;
        if (text[before] is not ('\'' or '’') || before == 0) return true;
        var beforeApostrophe = before - TextMetrics.PreviousElementLength(text, before);
        return !char.IsLetterOrDigit(text, beforeApostrophe);
    }
}
