using System.Collections.Generic;
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
            GD.PrintErr($"[VALIDATOR] ✗ {errorCount} mismatch(es) found — update tooltip!");
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
                RequiredTokens = new() { $"+{Balance.MomentumBonusPerStack * 100f:0}%", $"×{momentumMaxMultiplier:0.00}" }
            },
            ["overkill"] = new ModifierExpectation
            {
                Name = "Overkill",
                RequiredTokens = new() { $"{Balance.OverkillSpillEfficiency * 100f:0}%" }
            },
            ["exploit_weakness"] = new ModifierExpectation
            {
                Name = "Exploit Weakness",
                RequiredTokens = new() { "+60%" }
            },
            ["focus_lens"] = new ModifierExpectation
            {
                Name = "Focus Lens",
                RequiredTokens = new() { $"+{focusLensPercent:0}%", $"×{Balance.FocusLensAttackInterval:0}" }
            },
            ["slow"] = new ModifierExpectation
            {
                Name = "Chill Shot",
                RequiredTokens = new() { $"−{slowPercent:0}%", $"{Balance.SlowDuration:0} s" }
            },
            ["overreach"] = new ModifierExpectation
            {
                Name = "Overreach",
                RequiredTokens = new() { "+40%", "−20%" }
            },
            ["hair_trigger"] = new ModifierExpectation
            {
                Name = "Hair Trigger",
                RequiredTokens = new() { $"+{(Balance.HairTriggerAttackSpeed - 1f) * 100f:0}%", $"−{(1f - Balance.HairTriggerRangeFactor) * 100f:0}%" }
            },
            ["split_shot"] = new ModifierExpectation
            {
                Name = "Split Shot",
                RequiredTokens = new() { $"{Balance.SplitShotDamageRatio * 100f:0}%" }
            },
            ["feedback_loop"] = new ModifierExpectation
            {
                Name = "Feedback Loop",
                RequiredTokens = new() { $"{Balance.FeedbackLoopCooldownReduction * 100f:0}%" }
            },
            ["chain_reaction"] = new ModifierExpectation
            {
                Name = "Chain Reaction",
                RequiredTokens = new() { "55%" }
            }
        };
    }
}
