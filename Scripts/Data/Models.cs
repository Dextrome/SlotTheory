using System.Collections.Generic;

namespace SlotTheory.Data;

public record TowerDef(
    string Name,
    float BaseDamage,
    float AttackInterval,
    float Range,
    bool AppliesMark = false,
    int ChainCount = 0,
    float ChainRange = 260f,
    float ChainDamageDecay = 0.6f
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
    int SwiftCount = 0          // fast enemies (240px/s, 1.5× HP) — appear in waves 10-14
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
    int DisplayOrder = 999
);
