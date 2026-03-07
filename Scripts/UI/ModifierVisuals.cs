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

    // High-contrast colorblind-safe palette — each color is a clear hue-shift from its normal counterpart
    private static readonly Color CB_DamageScaling = new Color(1.00f, 0.92f, 0.10f); // bright yellow   (was orange)
    private static readonly Color CB_Utility       = new Color(0.10f, 0.50f, 1.00f); // cobalt blue      (was cyan)
    private static readonly Color CB_Range         = new Color(0.95f, 0.95f, 1.00f); // near-white       (was violet)
    private static readonly Color CB_StatusSynergy = new Color(1.00f, 0.50f, 0.00f); // pure orange      (was magenta)
    private static readonly Color CB_MultiTarget   = new Color(0.75f, 0.25f, 1.00f); // vivid purple     (was mint-green)

    public static Color GetAccent(string modifierId)
    {
        bool cb = SlotTheory.Core.SettingsManager.Instance?.ColorblindMode ?? false;
        return GetAccentInternal(modifierId, cb);
    }

    private static Color GetAccentInternal(string modifierId, bool colorblind) => modifierId switch
    {
        "momentum"      => colorblind ? CB_DamageScaling : DamageScaling,
        "overkill"      => colorblind ? CB_DamageScaling : DamageScaling,
        "focus_lens"    => colorblind ? CB_DamageScaling : DamageScaling,
        "hair_trigger"  => colorblind ? CB_DamageScaling : DamageScaling,
        "feedback_loop" => colorblind ? CB_DamageScaling : DamageScaling,

        "slow"           => colorblind ? CB_Utility : Utility,
        "split_shot"     => colorblind ? CB_MultiTarget : MultiTarget,
        "chain_reaction" => colorblind ? CB_MultiTarget : MultiTarget,

        "overreach" => colorblind ? CB_Range : Range,

        "exploit_weakness" => colorblind ? CB_StatusSynergy : StatusSynergy,
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
