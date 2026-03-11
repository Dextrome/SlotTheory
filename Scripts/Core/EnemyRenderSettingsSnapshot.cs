using Godot;
using System.Collections.Generic;

namespace SlotTheory.Core;

/// <summary>
/// Serializable subset of display settings that controls layered enemy rendering.
/// Kept separate for deterministic unit-test roundtrips.
/// </summary>
public readonly struct EnemyRenderSettingsSnapshot
{
    private const string DisplaySection = "display";

    public bool PostFxEnabled { get; }
    public bool LayeredEnabled { get; }
    public bool EmissiveEnabled { get; }
    public bool DamageMaterialEnabled { get; }
    public bool BloomEnabled { get; }
    public bool DevModeEnabled { get; }

    public EnemyRenderSettingsSnapshot(
        bool postFxEnabled,
        bool layeredEnabled,
        bool emissiveEnabled,
        bool damageMaterialEnabled,
        bool bloomEnabled,
        bool devModeEnabled)
    {
        PostFxEnabled = postFxEnabled;
        LayeredEnabled = layeredEnabled;
        EmissiveEnabled = emissiveEnabled;
        DamageMaterialEnabled = damageMaterialEnabled;
        BloomEnabled = bloomEnabled;
        DevModeEnabled = devModeEnabled;
    }

    public static EnemyRenderSettingsSnapshot ReadFrom(ConfigFile cfg, bool defaultBloomEnabled)
        => new(
            postFxEnabled: (bool)cfg.GetValue(DisplaySection, "post_fx", true),
            layeredEnabled: (bool)cfg.GetValue(DisplaySection, "enemy_layered", true),
            emissiveEnabled: (bool)cfg.GetValue(DisplaySection, "enemy_emissive", true),
            damageMaterialEnabled: (bool)cfg.GetValue(DisplaySection, "enemy_damage_material", true),
            bloomEnabled: (bool)cfg.GetValue(DisplaySection, "enemy_bloom", defaultBloomEnabled),
            devModeEnabled: (bool)cfg.GetValue(DisplaySection, "dev_mode", false));

    public static EnemyRenderSettingsSnapshot ReadFrom(
        IReadOnlyDictionary<string, bool> values,
        bool defaultBloomEnabled)
    {
        bool Read(string key, bool defaultValue)
            => values.TryGetValue(key, out bool value) ? value : defaultValue;

        return new EnemyRenderSettingsSnapshot(
            postFxEnabled: Read("post_fx", true),
            layeredEnabled: Read("enemy_layered", true),
            emissiveEnabled: Read("enemy_emissive", true),
            damageMaterialEnabled: Read("enemy_damage_material", true),
            bloomEnabled: Read("enemy_bloom", defaultBloomEnabled),
            devModeEnabled: Read("dev_mode", false));
    }

    public void WriteTo(ConfigFile cfg)
    {
        cfg.SetValue(DisplaySection, "post_fx", PostFxEnabled);
        cfg.SetValue(DisplaySection, "enemy_layered", LayeredEnabled);
        cfg.SetValue(DisplaySection, "enemy_emissive", EmissiveEnabled);
        cfg.SetValue(DisplaySection, "enemy_damage_material", DamageMaterialEnabled);
        cfg.SetValue(DisplaySection, "enemy_bloom", BloomEnabled);
        cfg.SetValue(DisplaySection, "dev_mode", DevModeEnabled);
    }

    public Dictionary<string, bool> ToDictionary()
        => new()
        {
            ["post_fx"] = PostFxEnabled,
            ["enemy_layered"] = LayeredEnabled,
            ["enemy_emissive"] = EmissiveEnabled,
            ["enemy_damage_material"] = DamageMaterialEnabled,
            ["enemy_bloom"] = BloomEnabled,
            ["dev_mode"] = DevModeEnabled,
        };
}
