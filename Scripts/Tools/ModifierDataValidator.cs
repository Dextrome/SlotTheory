using System;
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
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> RequiredTokens { get; set; } = new();
    }

    public static void ValidateModifierData(Dictionary<string, ModifierDef> modifiers)
    {
        var expectations = new List<ModifierExpectation>
        {
            new ModifierExpectation
            {
                Id = "momentum",
                Name = "Momentum",
                RequiredTokens = new() { "+8%", "×1.4" }
            },
            new ModifierExpectation
            {
                Id = "split_shot",
                Name = "Split Shot",
                RequiredTokens = new() { "65%" }
            },
            new ModifierExpectation
            {
                Id = "slow",
                Name = "Chill Shot",
                RequiredTokens = new() { "−30%", "5 s" }
            },
            new ModifierExpectation
            {
                Id = "feedback_loop",
                Name = "Feedback Loop",
                RequiredTokens = new() { "30%" }
            },
            new ModifierExpectation
            {
                Id = "exploit_weakness",
                Name = "Exploit Weakness",
                RequiredTokens = new() { "+50%" }
            },
            new ModifierExpectation
            {
                Id = "overreach",
                Name = "Overreach",
                RequiredTokens = new() { "+50%", "−15%" }
            },
            new ModifierExpectation
            {
                Id = "hair_trigger",
                Name = "Hair Trigger",
                RequiredTokens = new() { "+50%", "−30%" }
            },
            new ModifierExpectation
            {
                Id = "chain_reaction",
                Name = "Chain Reaction",
                RequiredTokens = new() { "60%" }
            }
        };

        int errorCount = 0;
        foreach (var expectation in expectations)
        {
            if (!modifiers.ContainsKey(expectation.Id))
            {
                GD.PrintErr($"[VALIDATOR] Missing modifier: {expectation.Id}");
                errorCount++;
                continue;
            }

            var def = modifiers[expectation.Id];
            foreach (var token in expectation.RequiredTokens)
            {
                if (!def.Description.Contains(token))
                {
                    GD.PrintErr($"[VALIDATOR] {expectation.Name}: expected token '{token}' not found");
                    GD.PrintErr($"            Description: {def.Description}");
                    errorCount++;
                }
            }
        }

        if (errorCount == 0)
        {
            GD.Print("[VALIDATOR] ✓ All modifier descriptions match implementation");
        }
        else
        {
            GD.PrintErr($"[VALIDATOR] ✗ {errorCount} mismatch(es) found — update tooltip!");
        }
    }
}

