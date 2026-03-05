using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Shared visual metadata for modifier UI cards.
/// </summary>
public static class ModifierVisuals
{
    public static Color GetAccent(string modifierId) => modifierId switch
    {
        "momentum" => new Color(1.00f, 0.36f, 0.78f),
        "overkill" => new Color(1.00f, 0.55f, 0.20f),
        "exploit_weakness" => new Color(1.00f, 0.22f, 0.38f),
        "focus_lens" => new Color(0.54f, 0.88f, 1.00f),
        "slow" => new Color(0.40f, 0.94f, 1.00f),
        "overreach" => new Color(0.76f, 0.66f, 1.00f),
        "hair_trigger" => new Color(1.00f, 0.30f, 0.70f),
        "split_shot" => new Color(0.58f, 0.94f, 1.00f),
        "feedback_loop" => new Color(0.36f, 1.00f, 0.70f),
        "chain_reaction" => new Color(0.50f, 0.86f, 1.00f),
        _ => new Color(0.80f, 0.80f, 0.95f),
    };

    public static string GetTag(string modifierId) => modifierId switch
    {
        "momentum" => "DAMAGE RAMP",
        "overkill" => "SPILL DAMAGE",
        "exploit_weakness" => "MARK FINISHER",
        "focus_lens" => "BURST",
        "slow" => "CONTROL",
        "overreach" => "RANGE",
        "hair_trigger" => "ATTACK SPEED",
        "split_shot" => "MULTI-HIT",
        "feedback_loop" => "COOLDOWN RESET",
        "chain_reaction" => "CHAINING",
        _ => "MODIFIER",
    };
}
