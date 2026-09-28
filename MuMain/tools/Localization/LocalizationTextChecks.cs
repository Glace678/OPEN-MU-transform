using System.Text.RegularExpressions;

namespace MuMain.Tools.Localization;

internal static partial class LocalizationTextChecks
{
    private const string MojibakeCharacters = "¼½¾ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖØÙÚÛÜÝÞßµ¿±";

    public static bool LooksCorruptedZhCn(string value)
    {
        return ContainsReplacementText(value)
            || value.Count(character => MojibakeCharacters.Contains(character)) >= 3;
    }

    public static bool ContainsSuspiciousLegacyText(string value)
    {
        // Half-width kana can be valid Japanese; this is a review warning, not a failure.
        return ContainsReplacementText(value)
            || value.Any(character => character is >= '\uFF65' and <= '\uFF9F');
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

    private static bool ContainsReplacementText(string value)
    {
        return value.Contains('\uFFFD') || RepeatedQuestionMarks().IsMatch(value);
    }

    [GeneratedRegex(@"\?{5,}", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedQuestionMarks();
}
