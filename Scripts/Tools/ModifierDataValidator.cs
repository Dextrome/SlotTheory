using System.Collections.Generic;
using System.Globalization;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Tools;

/// <summary>
/// Validates that modifier descriptions match their implementations.
/// Run this to catch tooltip drift from balance changes.
/// </summary>
public static class ModifierDataValidator
{
    private class ModifierExpectation
    {
        public string Name { get; set; } = string.Empty;
        public List<string> RequiredTokens { get; set; } = new();
    }

    private const string MultiplySign = "\u00D7";
    private const string MinusSign = "\u2212";

    public static void ValidateModifierData(Dictionary<string, ModifierDef> modifiers)
    {
        var expectations = BuildExpectations();
        int errorCount = 0;

        foreach (var (id, _) in expectations)
        {
            if (modifiers.ContainsKey(id))
                continue;

            GD.PrintErr($"[VALIDATOR] Missing modifier in Data/modifiers.json: {id}");
            errorCount++;
        }

        foreach (var id in modifiers.Keys)
        {
            if (expectations.ContainsKey(id))
                continue;

            GD.PrintErr($"[VALIDATOR] Missing expectation for modifier: {id}");
            errorCount++;
        }

        foreach (var (id, expectation) in expectations)
        {
            if (!modifiers.TryGetValue(id, out var def))
                continue;

            if (def.Name != expectation.Name)
            {
                GD.PrintErr($"[VALIDATOR] {id}: expected Name '{expectation.Name}', found '{def.Name}'");
                errorCount++;
            }

            foreach (var token in expectation.RequiredTokens)
            {
                if (def.Description.Contains(token))
                    continue;

                GD.PrintErr($"[VALIDATOR] {def.Name}: expected token '{token}' not found");
                GD.PrintErr($"            Description: {def.Description}");
                errorCount++;
            }
        }

        if (errorCount == 0)
        {
            GD.Print("[VALIDATOR] OK All modifier descriptions match implementation");
        }
        else
        {
            GD.PrintErr($"[VALIDATOR] \u2717 {errorCount} mismatch(es) found \u2014 update tooltip!");
        }
    }

    private static Dictionary<string, ModifierExpectation> BuildExpectations()
    {
        float momentumMaxMultiplier = 1f + (Balance.MomentumBonusPerStack * Balance.MomentumMaxStacks);
        float focusLensPercent = (Balance.FocusLensDamageBonus - 1f) * 100f;
        float slowPercent = (1f - Balance.SlowSpeedFactor) * 100f;

        return new Dictionary<string, ModifierExpectation>
        {
            ["momentum"] = new ModifierExpectation
            {
                Name = "Momentum",
                RequiredTokens = new()
                {
                    $"+{FormatInt(Balance.MomentumBonusPerStack * 100f)}%",
                    $"{MultiplySign}{FormatTwoDp(momentumMaxMultiplier)}"
                }
            },
            ["overkill"] = new ModifierExpectation
            {
                Name = "Overkill",
                RequiredTokens = new() { $"{FormatInt(Balance.OverkillSpillEfficiency * 100f)}%" }
            },
            ["exploit_weakness"] = new ModifierExpectation
            {
                Name = "Exploit Weakness",
                RequiredTokens = new() { "+60%" }
            },
            ["focus_lens"] = new ModifierExpectation
            {
                Name = "Focus Lens",
                RequiredTokens = new()
                {
                    $"+{FormatInt(focusLensPercent)}%",
                    $"{MultiplySign}{FormatInt(Balance.FocusLensAttackInterval)}"
                }
            },
            ["slow"] = new ModifierExpectation
            {
                Name = "Chill Shot",
                RequiredTokens = new()
                {
                    $"{MinusSign}{FormatInt(slowPercent)}%",
                    $"{FormatInt(Balance.SlowDuration)} s"
                }
            },
            ["overreach"] = new ModifierExpectation
            {
                Name = "Overreach",
                RequiredTokens = new() { "+40%", $"{MinusSign}20%" }
            },
            ["hair_trigger"] = new ModifierExpectation
            {
                Name = "Hair Trigger",
                RequiredTokens = new()
                {
                    $"+{FormatInt((Balance.HairTriggerAttackSpeed - 1f) * 100f)}%",
                    $"{MinusSign}{FormatInt((1f - Balance.HairTriggerRangeFactor) * 100f)}%"
                }
            },
            ["split_shot"] = new ModifierExpectation
            {
                Name = "Split Shot",
                RequiredTokens = new() { $"{FormatInt(Balance.SplitShotDamageRatio * 100f)}%" }
            },
            ["feedback_loop"] = new ModifierExpectation
            {
                Name = "Feedback Loop",
                RequiredTokens = new() { $"{FormatInt(Balance.FeedbackLoopCooldownReduction * 100f)}%" }
            },
            ["chain_reaction"] = new ModifierExpectation
            {
                Name = "Chain Reaction",
                RequiredTokens = new() { "55%" }
            }
        };
    }

    private static string FormatInt(float value)
    {
        return value.ToString("0", CultureInfo.InvariantCulture);
    }

    private static string FormatTwoDp(float value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
