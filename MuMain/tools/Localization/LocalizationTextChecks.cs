using System.Text.RegularExpressions;

namespace MuMain.Tools.Localization;

internal static partial class LocalizationTextChecks
{
    public static bool LooksCorruptedZhCn(string value) => IsMojibakeHardFail(value);

    public static bool ContainsSuspiciousLegacyText(string value)
    {
        // Half-width kana can be valid Japanese; this is a review warning, not a failure.
        return value.Any(character => (uint)character >= 0xFF65 && (uint)character <= 0xFF9F);
    }

    /// <summary>
    /// Hard-fail corruption markers:
    /// 1) the U+FFFD replacement character,
    /// 2) four or more consecutive question marks,
    /// 3) a run of at least four consecutive Latin-1 Supplement characters (U+0080..U+00FF) that
    ///    also contains a Latin-1 symbol code point (U+00A0..U+00BF, U+00D7 or U+00F7).
    /// The third pattern is the signature of Korean cp949/cp1252 mojibake residue. Legitimate
    /// European accented text (a-umlaut, o-umlaut, u-umlaut, a-tilde, c-cedilla, a-acute, the
    /// all-capital run, "acao") is either isolated letters or pure letter runs without those
    /// symbol code points, so it is deliberately not flagged.
    /// </summary>
    public static bool IsMojibakeHardFail(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Contains('\uFFFD') || RepeatedQuestionMarks().IsMatch(value))
        {
            return true;
        }

        var run = 0;
        var runHasSymbol = false;
        foreach (var character in value)
        {
            var code = (uint)character;
            if (code >= 0x0080 && code <= 0x00FF)
            {
                run++;
                if ((code >= 0x00A0 && code <= 0x00BF) || code == 0x00D7 || code == 0x00F7)
                {
                    runHasSymbol = true;
                }
            }
            else
            {
                if (run >= 4 && runHasSymbol)
                {
                    return true;
                }

                run = 0;
                runHasSymbol = false;
            }
        }

        return run >= 4 && runHasSymbol;
    }

    public static int CountLogicalLineBreaks(string value)
    {
        var count = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\r')
            {
                if (index + 1 < value.Length && value[index + 1] == '\n')
                {
                    index++;
                }

                count++;
            }
            else if (value[index] == '\n')
            {
                count++;
            }
            else if (value[index] == '\\' && index + 1 < value.Length && value[index + 1] == 'n')
            {
                index++;
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"\?{4,}", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedQuestionMarks();
}
