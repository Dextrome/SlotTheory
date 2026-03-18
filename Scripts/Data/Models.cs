using System.Collections.Generic;

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
    WaveConfig[]? TutorialWaves = null
);
