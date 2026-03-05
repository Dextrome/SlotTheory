using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Shared visual metadata for modifier UI cards.
/// </summary>
public static class ModifierVisuals
{
    private static readonly Color DamageScaling = new Color(1.00f, 0.60f, 0.20f); // orange
    private static readonly Color Utility       = new Color(0.45f, 0.92f, 1.00f); // cyan
    private static readonly Color Range         = new Color(0.72f, 0.58f, 1.00f); // violet
    private static readonly Color StatusSynergy = new Color(1.00f, 0.36f, 0.80f); // magenta
    private static readonly Color MultiTarget   = new Color(0.48f, 1.00f, 0.76f); // mint-green

    public static Color GetAccent(string modifierId) => modifierId switch
    {
        "momentum" => DamageScaling,
        "overkill" => DamageScaling,
        "focus_lens" => DamageScaling,
        "hair_trigger" => DamageScaling,
        "feedback_loop" => DamageScaling,

        "slow" => Utility,
        "split_shot" => MultiTarget,
        "chain_reaction" => MultiTarget,

        "overreach" => Range,

        "exploit_weakness" => StatusSynergy,
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
