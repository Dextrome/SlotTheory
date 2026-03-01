using System.Collections.Generic;

namespace SlotTheory.Data;

public record TowerDef(
    string Name,
    float BaseDamage,
    float AttackInterval,
    float Range,
    bool AppliesMark = false
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
    int TankyCount = 0
);
