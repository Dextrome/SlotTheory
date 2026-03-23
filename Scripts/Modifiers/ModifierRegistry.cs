using System;
using System.Collections.Generic;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>Maps modifier IDs (from JSON) to concrete modifier instances.</summary>
public static class ModifierRegistry
{
    private static readonly Dictionary<string, Func<ModifierDef, Modifier>> _factories = new()
    {
        ["momentum"]         = def => new Momentum(def),
        ["overkill"]         = def => new Overkill(def),
        ["exploit_weakness"] = def => new ExploitWeakness(def),
        ["focus_lens"]       = def => new FocusLens(def),
        ["slow"]             = def => new Slow(def),
        ["overreach"]        = def => new Overreach(def),
        ["hair_trigger"]     = def => new HairTrigger(def),
        ["split_shot"]       = def => new SplitShot(def),
        ["feedback_loop"]    = def => new FeedbackLoop(def),
        ["chain_reaction"]   = def => new ChainReaction(def),
        ["blast_core"]       = def => new BlastCore(def),
        ["wildfire"]         = def => new Wildfire(def),
        ["reaper_protocol"]  = def => new ReaperProtocol(def),
    };

    public static Modifier Create(string modifierId)
    {
        var def = DataLoader.GetModifierDef(modifierId);
        if (_factories.TryGetValue(modifierId, out var factory))
            return factory(def);
        throw new InvalidOperationException($"Unknown modifier id: '{modifierId}'");
    }
}
