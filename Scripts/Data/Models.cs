using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlotTheory.Data;

public record TowerDef(
    string Name,
    float BaseDamage,
    float AttackInterval,
    float Range,
    bool AppliesMark = false,
    int SplitCount = 0,
    int ChainCount = 0,
    float ChainRange = 260f,
    float ChainDamageDecay = 0.57f
);

public record ModifierDef(
    string Id,
    string Name,
    string Description,
    Dictionary<string, float>? Params = null
);

public record WaveConfig(
    int EnemyCount,
    float SpawnInterval,
    int TankyCount = 0,
    bool ClumpArmored = false,  // group all armored enemies into one block instead of spreading evenly
    int SwiftCount = 0,         // fast enemies (240px/s, 1.5x HP)
    int SplitterCount = 0,      // splitter enemies (1.8x HP, 90px/s) - split into shards on death
    int ReverseCount = 0,       // reverse walkers (full game): jump backward when hit hard
    int ShieldDroneCount = 0,   // support drones: project 35% damage reduction aura to nearby allies
    int AnchorCount = 0,        // anti-control bricks: strong resistance to progress manipulation
    int NullDroneCount = 0,     // support drones: cleanse mark/slow on nearby allies
    int LancerCount = 0,        // spacing disruptors: short forward dash
    int VeilCount = 0           // anti-burst walkers: refresh shell if not hit for a short window
);

public record WaveAdjustmentFile(
    WaveAdjustmentEntry[] Entries
);

public record WaveAdjustmentEntry(
    string MapId,
    string Difficulty,
    int? Wave = null,               // 1-based wave number; null = apply to all waves
    int EnemyCountDelta = 0,
    float SpawnIntervalDelta = 0f,
    int TankyDelta = 0,
    int SwiftDelta = 0,
    int SplitterDelta = 0,
    int ReverseDelta = 0,
    int ShieldDroneDelta = 0,
    int AnchorDelta = 0,
    int NullDroneDelta = 0,
    int LancerDelta = 0,
    int VeilDelta = 0
);

public record MapEnemyProfileFile(
    MapEnemyProfileEntry[] Profiles
);

public record MapEnemyProfileEntry(
    string MapId,
    MapEnemyProfileEnemy[] Enemies,
    float PackageBlend = 0.75f,          // 0 = keep baseline composition, 1 = full package weighting
    float OffProfileRetention = 0.18f,   // how much non-package baseline types survive before blending
    float DefaultSpecialShare = -1f,     // target non-basic share [0..1], -1 = keep baseline
    MapEnemyProfileBand[]? Bands = null
);

public record MapEnemyProfileEnemy(
    string EnemyId,
    float Weight = 1f,
    string Tier = "core",               // "core" or "spice"
    int MinWave = 1,
    int MaxWave = 20
);

public record MapEnemyProfileBand(
    int FromWave = 1,
    int ToWave = 20,
    float SpecialShare = -1f,           // overrides DefaultSpecialShare in this wave window
    float PackageBlendMultiplier = 1f,
    float CoreWeightMultiplier = 1f,
    float SpiceWeightMultiplier = 1f
);

public record Vector2Def(float X, float Y);

public record SlotDef(int Id, float X, float Y);

public record MapDef(
    string Id,
    string Name,
    string Description,
    Vector2Def[] Path,
    SlotDef[] Slots,
    bool IsRandom = false,
    int DisplayOrder = 999,
    bool IsTutorial = false,
    bool IsFullGame = false,
    WaveConfig[]? TutorialWaves = null,
    bool IsCustom = false
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MandateType
{
    None,
    BannedModifiers,
    BannedTowers,
    LockedSlots,
    EnemyHpBonus,
}

public record MandateDef(
    MandateType  Type,
    string       DisplayText,
    string[]?    BannedIds         = null,
    int          LockedSlotCount   = 0,
    float        EnemyHpMultiplier = 1.0f,
    string[]?    BannedTowerIds    = null,
    string[]?    BannedModifierIds = null
);

public record CampaignStageDef(
    int         StageIndex,
    string      MapId,
    MandateDef  Mandate,
    string      StageName     = "",
    string      StageSubtitle = "",
    string      IntroLine     = "",
    string      ClearStamp    = ""
);

public record CampaignDataRoot(
    CampaignStageDef[] Stages,
    string FinalCompletionText = ""
);
