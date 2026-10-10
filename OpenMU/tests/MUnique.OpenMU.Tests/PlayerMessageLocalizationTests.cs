// <copyright file="PlayerMessageLocalizationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.GameLogic.Properties;
using NUnit.Framework;

/// <summary>
/// Consistency tests for the <see cref="PlayerMessage"/> resources: verifies that every key of the
/// invariant (English) resource also resolves under zh-CN with a matching placeholder contract, and
/// pins the regression that "{player} entered the game." is shown in Chinese for zh accounts.
/// </summary>
[TestFixture]
public class PlayerMessageLocalizationTests
{
    private static readonly CultureInfo ZhCn = CultureInfo.GetCultureInfo("zh-CN");

    /// <summary>
    /// Every invariant resource key must resolve to a non-empty string under zh-CN. A missing entry
    /// would silently fall back to English and regress the Chinese client.
    /// </summary>
    [Test]
    public void AllInvariantKeysResolveUnderZhCn()
    {
        var invariantSet = PlayerMessage.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        Assert.That(invariantSet, Is.Not.Null);
        var set = invariantSet!;

        var missing = new List<string>();
        foreach (DictionaryEntry entry in set)
        {
            var key = entry.Key as string ?? string.Empty;
            if (key.StartsWith(">", StringComparison.Ordinal))
            {
                continue; // skip framework metadata keys (>>mimetypes, >>resheader etc.)
            }

            var translated = PlayerMessage.ResourceManager.GetString(key, ZhCn);
            if (string.IsNullOrEmpty(translated))
            {
                missing.Add(key);
            }
        }

        Assert.That(missing, Is.Empty, "These keys have no zh-CN translation and would fall back to English: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The set of {n} format placeholders must be identical between invariant and zh-CN for every
    /// key, otherwise string.Format would throw or mis-order arguments at runtime.
    /// </summary>
    [Test]
    public void PlaceholderContractMatchesBetweenInvariantAndZhCn()
    {
        var invariantSet = PlayerMessage.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        Assert.That(invariantSet, Is.Not.Null);
        var set = invariantSet!;

        var mismatches = new List<string>();
        foreach (DictionaryEntry entry in set)
        {
            var key = entry.Key as string ?? string.Empty;
            if (key.StartsWith(">", StringComparison.Ordinal))
            {
                continue;
            }

            var invariantValue = entry.Value as string ?? string.Empty;
            var translatedValue = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            if (Placeholders(invariantValue).SequenceEqual(Placeholders(translatedValue)))
            {
                continue;
            }

            mismatches.Add($"{key}: invariant=[{invariantValue}] zhCN=[{translatedValue}]");
        }

        Assert.That(mismatches, Is.Empty, "Placeholder contract mismatch: " + string.Join(" | ", mismatches));
    }

    /// <summary>
    /// Regression: the global "{0} entered the game." notification must be Chinese when resolved with
    /// the zh-CN culture (what a zh account actually gets after the neutral-culture mapping fix).
    /// </summary>
    [Test]
    public void PlayerEnteredGameMessageIsChineseUnderZhCn()
    {
        var message = PlayerMessage.ResourceManager.GetString(nameof(PlayerMessage.PlayerEnteredGameMessage), ZhCn);
        Assert.That(message, Is.EqualTo("{0} 进入了游戏。"));
    }

    /// <summary>
    /// Keys added by the L3c business-localization pass (Castle Siege / Item Registration / Speed Hack / CharInfo).
    /// These were previously hardcoded English literals sent straight to players; they must now exist in both
    /// the invariant (English) and zh-CN resources.
    /// </summary>
    private static readonly string[] NewL3cKeys =
    {
        "CastleSiegeStartsInFormat",
        "CastleSiegeGuildRegistrationOpen",
        "CastleSiegeMarkRegistrationOpen",
        "CastleSiegePreparationsInProgress",
        "CastleSiegeInProgress",
        "ItemRegistrationNpcNotAvailable",
        "ItemRegistrationMissingRequiredItem",
        "ItemRegistrationInventoryMoneyLimitReached",
        "ItemRegistrationCompletedWithRewardFormat",
        "ItemRegistrationTotalAllTimeFormat",
        "ItemRegistrationProgressFormat",
        "SpeedHackWarningDetected",
        "CharInfoAccountNameFormat",
    };

    /// <summary>
    /// Every L3c key must resolve to a non-empty string in both the invariant (English) and zh-CN resources.
    /// </summary>
    [Test]
    public void NewL3cKeysExistInBaseAndZhCn()
    {
        var problems = new List<string>();
        foreach (var key in NewL3cKeys)
        {
            var invariant = PlayerMessage.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
            var translated = PlayerMessage.ResourceManager.GetString(key, ZhCn);
            if (string.IsNullOrEmpty(invariant))
            {
                problems.Add(key + " missing in invariant (base) resource");
            }

            if (string.IsNullOrEmpty(translated))
            {
                problems.Add(key + " missing/empty in zh-CN resource");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>
    /// The zh-CN value of every L3c key must actually be Chinese (contain CJK ideographs), not an
    /// English copy that would defeat the localization.
    /// </summary>
    [Test]
    public void NewL3cKeysZhCnAreChinese()
    {
        var problems = new List<string>();
        foreach (var key in NewL3cKeys)
        {
            var translated = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            if (!Regex.IsMatch(translated, @"\p{IsCJKUnifiedIdeographs}"))
            {
                problems.Add(key + " zh-CN value is not Chinese: [" + translated + "]");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>
    /// The {n} placeholder set must match between invariant and zh-CN for every L3c key, so the same
    /// format arguments the business code passes format correctly in both cultures.
    /// </summary>
    [Test]
    public void NewL3cPlaceholderContractMatches()
    {
        var problems = new List<string>();
        foreach (var key in NewL3cKeys)
        {
            var invariant = PlayerMessage.ResourceManager.GetString(key, CultureInfo.InvariantCulture) ?? string.Empty;
            var translated = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            if (!Placeholders(invariant).SequenceEqual(Placeholders(translated)))
            {
                problems.Add(key + ": invariant=[" + invariant + "] zhCN=[" + translated + "]");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>
    /// Exercises the real lookup path used by PlayerMessageExtensions.GetLocalizedMessage /
    /// ShowLocalizedBlueMessageAsync (ResourceManager.GetString(key, player.Culture) then string.Format).
    /// For the parameterized keys, formatting with representative arguments under zh-CN must yield a
    /// Chinese sentence with the arguments substituted in.
    /// </summary>
    [Test]
    public void NewL3cFormattedOutputUnderZhCnIsChinese()
    {
        var cases = new (string Key, object[] Args)[]
        {
            ("CastleSiegeStartsInFormat", new object[] { 30 }),
            ("ItemRegistrationCompletedWithRewardFormat", new object[] { 5, 5 }),
            ("ItemRegistrationTotalAllTimeFormat", new object[] { 42 }),
            ("ItemRegistrationProgressFormat", new object[] { 3, 5 }),
            ("CharInfoAccountNameFormat", new object[] { "TestAccount" }),
        };

        var problems = new List<string>();
        foreach (var (key, args) in cases)
        {
            var template = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            string rendered;
            try
            {
                rendered = string.Format(template, args);
            }
            catch (FormatException ex)
            {
                problems.Add(key + " format failed: " + ex.Message);
                continue;
            }

            if (!Regex.IsMatch(rendered, @"\p{IsCJKUnifiedIdeographs}"))
            {
                problems.Add(key + " rendered non-Chinese: [" + rendered + "]");
            }

            if (!rendered.Contains(args[0].ToString() ?? string.Empty, StringComparison.Ordinal))
            {
                problems.Add(key + " argument not substituted: [" + rendered + "]");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }
    /// <summary>
    /// Keys added by the L3 server-side English-localization pass (summon-party channel / invasion
    /// announcements / mini-game entrance notifications). These were previously hardcoded English literals
    /// sent straight to players; they must now exist in both the invariant (English) and zh-CN resources.
    /// </summary>
    private static readonly string[] NewL3Keys =
    {
        "SummoningInSecondsFormat",
        "SummoningCanceled",
        "InvasionGoldenStartMessage",
        "InvasionGoldenEndMessage",
        "InvasionRedDragonStartMessage",
        "InvasionRedDragonEndMessage",
        "InvasionWhiteWizardStartMessage",
        "InvasionWhiteWizardEndMessage",
        "MiniGameEntranceOpenMinutesFormat",
        "MiniGameEntranceClosed",
    };

    /// <summary>Every L3 key must resolve to a non-empty string in both invariant (en) and zh-CN resources.</summary>
    [Test]
    public void NewL3KeysExistInBaseAndZhCn()
    {
        var problems = new List<string>();
        foreach (var key in NewL3Keys)
        {
            if (string.IsNullOrEmpty(PlayerMessage.ResourceManager.GetString(key, CultureInfo.InvariantCulture)))
            {
                problems.Add(key + " missing in invariant (base) resource");
            }

            if (string.IsNullOrEmpty(PlayerMessage.ResourceManager.GetString(key, ZhCn)))
            {
                problems.Add(key + " missing/empty in zh-CN resource");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>The zh-CN value of every L3 key must actually be Chinese (contain CJK ideographs).</summary>
    [Test]
    public void NewL3KeysZhCnAreChinese()
    {
        var problems = new List<string>();
        foreach (var key in NewL3Keys)
        {
            var translated = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            if (!Regex.IsMatch(translated, @"\p{IsCJKUnifiedIdeographs}"))
            {
                problems.Add(key + " zh-CN value is not Chinese: [" + translated + "]");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>The {n} placeholder set must match between invariant and zh-CN for every L3 key.</summary>
    [Test]
    public void NewL3PlaceholderContractMatches()
    {
        var problems = new List<string>();
        foreach (var key in NewL3Keys)
        {
            var invariant = PlayerMessage.ResourceManager.GetString(key, CultureInfo.InvariantCulture) ?? string.Empty;
            var translated = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            if (!Placeholders(invariant).SequenceEqual(Placeholders(translated)))
            {
                problems.Add(key + ": invariant=[" + invariant + "] zhCN=[" + translated + "]");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>
    /// Exercises the real formatting path used by PlayerMessageExtensions.GetLocalizedMessage (ResourceManager
    /// + string.Format) for the parameterized L3 keys. Under zh-CN the rendered text must be Chinese with the
    /// arguments substituted in.
    /// </summary>
    [Test]
    public void NewL3FormattedOutputUnderZhCnIsChinese()
    {
        var cases = new (string Key, object[] Args)[]
        {
            ("SummoningInSecondsFormat", new object[] { 5 }),
            ("InvasionGoldenStartMessage", new object[] { "Lorencia" }),
            ("InvasionGoldenEndMessage", new object[] { "Lorencia" }),
            ("InvasionRedDragonStartMessage", new object[] { "Noria" }),
            ("InvasionRedDragonEndMessage", new object[] { "Noria" }),
            ("InvasionWhiteWizardStartMessage", new object[] { "Devias" }),
            ("InvasionWhiteWizardEndMessage", new object[] { "Devias" }),
            ("MiniGameEntranceOpenMinutesFormat", new object[] { 5 }),
        };

        var problems = new List<string>();
        foreach (var (key, args) in cases)
        {
            var template = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
            string rendered;
            try
            {
                rendered = string.Format(template, args);
            }
            catch (FormatException ex)
            {
                problems.Add(key + " format failed: " + ex.Message);
                continue;
            }

            if (!Regex.IsMatch(rendered, @"\p{IsCJKUnifiedIdeographs}"))
            {
                problems.Add(key + " rendered non-Chinese: [" + rendered + "]");
            }

            if (!rendered.Contains(args[0].ToString() ?? string.Empty, StringComparison.Ordinal))
            {
                problems.Add(key + " argument not substituted: [" + rendered + "]");
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    /// <summary>
    /// Real announcement-path check for the built-in invasion plugins: each concrete plugin's protected
    /// StartMessageResourceKey/EndMessageResourceKey (resolved by the production code in
    /// BaseInvasionPlugIn.TrySendStartMessageAsync/TrySendEndMessageAsync) must point at a localized
    /// PlayerMessage key that renders to Chinese under zh-CN and carries a {0} map-name placeholder.
    /// </summary>
    [Test]
    public void BuiltInInvasionPluginsResolveToLocalizedAnnouncementKeys()
    {
        var pluginTypes = new[]
        {
            typeof(GoldenInvasionPlugIn),
            typeof(RedDragonInvasionPlugIn),
            typeof(WhiteWizardInvasionPlugIn),
        };

        var problems = new List<string>();
        foreach (var type in pluginTypes)
        {
            object plugin;
            try
            {
                plugin = Activator.CreateInstance(type)!;
            }
            catch (Exception ex)
            {
                problems.Add(type.Name + " could not be constructed: " + ex.Message);
                continue;
            }

            foreach (var propertyName in new[] { "StartMessageResourceKey", "EndMessageResourceKey" })
            {
                var prop = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
                var key = prop?.GetValue(plugin) as string;
                if (string.IsNullOrEmpty(key))
                {
                    problems.Add(type.Name + "." + propertyName + " resolved to null");
                    continue;
                }

                var template = PlayerMessage.ResourceManager.GetString(key, ZhCn) ?? string.Empty;
                if (!Regex.IsMatch(template, @"\p{IsCJKUnifiedIdeographs}"))
                {
                    problems.Add(type.Name + " " + propertyName + "=[" + key + "] zh-CN not Chinese: [" + template + "]");
                }

                if (!template.Contains("{0}", StringComparison.Ordinal))
                {
                    problems.Add(type.Name + " " + propertyName + "=[" + key + "] missing {0} map-name placeholder");
                }
            }
        }

        Assert.That(problems, Is.Empty, string.Join(" | ", problems));
    }

    private static IEnumerable<string> Placeholders(string value)
    {
        return Regex.Matches(value, @"\{\d+\}").Select(m => m.Value).OrderBy(p => p, StringComparer.Ordinal);
    }
}