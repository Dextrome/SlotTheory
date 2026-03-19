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
    int SwiftCount = 0,         // fast enemies (240px/s, 1.5× HP) - appear in waves 10-14
    int SplitterCount = 0       // splitter enemies (1.8× HP, 90px/s) - split into 2 shards on death
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
    WaveConfig[]? TutorialWaves = null
);

// ── Campaign ──────────────────────────────────────────────────────────────────

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
