# Map System Refactor Plan

**Status**: Planning Phase  
**Priority**: High (affects replayability perception)  
**Scope**: Replace procedural-only system with curated maps + random option

---

## Problem Statement

Current procedural map generation:
- Single algorithm for all runs
- Limited path variety (3-leg snake always)
- Slot placement feels random/disconnected
- Reduces sense of deliberate tower placement

Goal: **Intentional map design** with **familiar layouts** that players can learn and strategize on.

---

## Target Architecture

```
MapSystem (new)
├── MapExecution (selects/loads map)
├── Map (abstract / interface)
│   ├── Path definition (fixed waypoints)
│   ├── Slot zones (6 fixed positions)
│   └── Visual hints (decorative obstacles, landmarks)
├── HandCraftedMap : Map (new class)
│   └── Loads from JSON
└── ProceduralMap : Map (existing, refactored)
    └── Generates randomized layout
```

---

## Data Structure Changes

### New: `maps.json`

```json
{
  "maps": [
    {
      "id": "arena_classic",
      "name": "Arena Classic",
      "description": "Balanced 3-lane snake. Clean sightlines.",
      "displayOrder": 0,
      "path": [
        { "x": 320, "y": 80 },
        { "x": 480, "y": 80 },
        { "x": 640, "y": 80 },
        { "x": 640, "y": 208 },
        { "x": 480, "y": 208 },
        { "x": 320, "y": 208 },
        { "x": 320, "y": 336 },
        { "x": 480, "y": 336 },
        { "x": 640, "y": 336 }
      ],
      "slots": [
        { "id": 0, "zoneRow": 0, "zoneCol": 0, "x": 160, "y": 80 },
        { "id": 1, "zoneRow": 0, "zoneCol": 1, "x": 800, "y": 80 },
        { "id": 2, "zoneRow": 1, "zoneCol": 0, "x": 160, "y": 208 },
        { "id": 3, "zoneRow": 1, "zoneCol": 1, "x": 800, "y": 208 },
        { "id": 4, "zoneRow": 2, "zoneCol": 0, "x": 160, "y": 336 },
        { "id": 5, "zoneRow": 2, "zoneCol": 1, "x": 800, "y": 336 }
      ],
      "landmarks": [
        {
          "type": "rock_cluster",
          "x": 1000,
          "y": 150,
          "radius": 80,
          "comment": "Flavor. Does not block path or slots."
        }
      ]
    },
    {
      "id": "gauntlet",
      "name": "Gauntlet",
      "description": "Narrow path forces early engagement. High-skill.",
      "path": [
        { "x": 480, "y": 80 },
        { "x": 480, "y": 208 },
        { "x": 480, "y": 336 },
        { "x": 480, "y": 464 },
        { "x": 480, "y": 592 }
      ],
      "slots": [
        { "id": 0, "x": 160, "y": 80 },
        { "id": 1, "x": 800, "y": 80 },
        { "id": 2, "x": 160, "y": 208 },
        { "id": 3, "x": 800, "y": 208 },
        { "id": 4, "x": 160, "y": 336 },
        { "id": 5, "x": 800, "y": 336 }
      ]
    }
  ],
  "random": {
    "id": "random_map",
    "name": "Random Map",
    "description": "Procedurally generated.",
    "isRandom": true
  }
}
```

---

## Class Changes

### Current (to be refactored)

```csharp
public partial class GameController : Node
{
    private MapGenerator _mapGen;
    
    private void _Ready()
    {
        var map = _mapGen.GenerateMap(runState.RngSeed);  // Only option
        PlaceSlots(map);
    }
}
```

### New Design

```csharp
// Abstract base
public abstract class Map
{
    public string Id { get; protected set; }
    public string Name { get; protected set; }
    public Vector2[] Path { get; protected set; }  // 9 waypoints
    public SlotDefinition[] Slots { get; protected set; }  // 6 slots
}

// Hand-crafted maps
public class HandCraftedMap : Map
{
    public static HandCraftedMap LoadFromDef(MapDef def)
    {
        var map = new HandCraftedMap
        {
            Id = def.Id,
            Name = def.Name,
            Path = def.Path.Select(p => new Vector2(p.X, p.Y)).ToArray(),
            Slots = def.Slots.Select((s, i) => new SlotDefinition
            {
                SlotIndex = i,
                Position = new Vector2(s.X, s.Y)
            }).ToArray()
        };
        return map;
    }
}

// Procedural option
public class ProceduralMap : Map
{
    public static ProceduralMap Generate(ulong seed)
    {
        var rng = new System.Random((int)seed);
        // Existing MapGenerator logic, refactored into here
        
        var map = new ProceduralMap
        {
            Id = "random",
            Name = "Random Map",
            Path = GeneratePath(rng),
            Slots = GenerateSlots(rng)
        };
        return map;
    }
}

// Map selection
public partial class GameController : Node
{
    private Map _currentMap;
    
    private void _Ready()
    {
        string mapId = runState.SelectedMapId ?? "random";
        
        if (mapId == "random")
            _currentMap = ProceduralMap.Generate(runState.RngSeed);
        else
            _currentMap = HandCraftedMap.LoadFromDef(DataLoader.GetMapDef(mapId));
        
        PlaceSlots(_currentMap);
    }
}
```

---

## RunState Addition

```csharp
public class RunState
{
    public string SelectedMapId { get; set; } = null;  // null = random
    
    // ... existing fields
}
```

---

## DraftPanel / UI Changes

### Option 1: Simple (No changes to current flow)
- Always show "Random Map" before Draft starts
- No UI selection; map is chosen at run start
- Feels natural, no extra screens needed

### Option 2: Map Picker (Future)
- Full-screen map selector before draft
- Shows 3-4 map thumbnails
- Slight wait time before draft

**Initial Implementation**: Option 1 (minimal UI disruption)

---

## DataLoader Changes

```csharp
public static class DataLoader
{
    private static List<MapDef> _mapDefinitions = new();
    
    public static void Load()
    {
        // Existing tower/modifier/wave loading
        // + Maps
        var mapFile = FileAccess.GetFileAsText("res://Data/maps.json");
        var maps = Json.Parse(mapFile);
        _mapDefinitions = ParseMaps(maps);
    }
    
    public static MapDef GetMapDef(string mapId) =>
        _mapDefinitions.FirstOrDefault(m => m.Id == mapId);
}

// New DTO
public class MapDef
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Vector2D[] Path { get; set; }  // Simple DTO with X, Y
    public SlotDef[] Slots { get; set; }
    public bool IsRandom { get; set; }
}

public class Vector2D { public float X, Y; }
public class SlotDef { public int Id; public float X, Y; }
```

---

## Implementation Steps

### Phase 1: Data Layer (1-2 hours)

- [ ] Create `maps.json` with 2-3 hand-crafted maps
  - Arena Classic (existing snake, tuned)
  - Gauntlet (narrow center path)
  - (Optional: Sprawl — wide, distributed slots)
- [ ] Add `MapDef`, `Vector2D`, `SlotDef` models to `Models.cs`
- [ ] Update `DataLoader.cs` to load maps
- [ ] Test JSON parsing

### Phase 2: Refactor MapGenerator → ProceduralMap (2-3 hours)

- [ ] Extract `MapGenerator.cs` logic into `ProceduralMap` class
- [ ] Create abstract `Map` base class
- [ ] Implement `HandCraftedMap.LoadFromDef()`
- [ ] Ensure existing procedural logic works in new structure
- [ ] Verify bot mode still works

### Phase 3: Integration (1 hour)

- [ ] Update `RunState` to include `SelectedMapId`
- [ ] Update `GameController._Ready()` to select map
- [ ] Update bot runner to support map selection
- [ ] Test both hand-crafted and random maps in gameplay

### Phase 4: Polish & Testing (1 hour)

- [ ] Visually verify slot placement on each map
- [ ] Ensure range circles and tooltips work with new coordinates
- [ ] Run 10 quick games on each map
- [ ] Test edge cases (slot near path, clustering)

---

## Hand-Crafted Map Ideas

### Map 1: Arena Classic
- **Path**: 3-leg horizontal snake (existing design, refined)
- **Slots**: 2 wide zones on left/right, staggered
- **Feel**: Balanced, clean sightlines
- **Difficulty**: Medium (standard)

### Map 2: Gauntlet
- **Path**: Straight vertical lane down center
- **Slots**: Heavy clustering on sides forces early placement
- **Feel**: Intense, forces early engagement
- **Difficulty**: Hard (enemies can't dodge)

### Map 3: Sprawl (Optional)
- **Path**: Wide zigzag across full width
- **Slots**: Distributed in corners and edges
- **Feel**: Tactical, spread-out placement
- **Difficulty**: Easy (more engagement distance)

---

## Procedural Map Option

Existing `MapGenerator.Generate()` becomes:

```csharp
public static ProceduralMap Generate(ulong seed)
{
    var rng = new System.Random((int)seed);
    
    var path = GenerateSnakePath(rng);
    var slots = GenerateSlotsAdjacent(rng, path);
    
    return new ProceduralMap
    {
        Id = "random",
        Name = "Random Map",
        Path = path,
        Slots = slots
    };
}
```

No behavioral changes — just architectural wrapper.

---

## Migration Checklist

- [ ] Backward compatibility: Does `RngSeed` still work?
- [ ] Export: Does Windows export still run maps correctly?
- [ ] Bot mode: Does BotRunner select maps properly?
- [ ] Edge cases: Slots outside grid? Path validation?
- [ ] Performance: JSON loading fast enough?

---

## Testing Plan

### Unit Tests (Once infrastructure exists)
- Maps.json parsing
- Slot coordinate validity
- Path waypoint count

### Manual Testing
- Visual: Each map renders without overlaps
- Gameplay: Defeat waves on each map
- Tower placement: Can place in all 6 slots
- Targeting: Range circles work correctly
- Bot: 5-run bot playtest on each map

### Balance Review (After implementation)
- Do some maps trivialize certain tower combos?
- Do slots feel equally viable?
- Is random map difficulty comparable to hand-crafted?

---

## Timeline

**Total Estimate**: ~6-8 hours (can be split across sessions)

- Phase 1: ~2 hours
- Phase 2: ~2-3 hours  
- Phase 3: ~1 hour
- Phase 4: ~1 hour
- Buffer: ~1-2 hours

---

## Success Criteria

✅ Hand-crafted maps load and render correctly  
✅ All 6 slots functional on each map  
✅ Visual feedback (range circles, tooltips) works  
✅ Procedural "random" option still available  
✅ 20-wave wins possible on all maps  
✅ Bot mode runs on each map without errors  

---

## Future Expansion (Out of Scope)

- Map unlock system (played N runs before new map available)
- Map difficulty ratings (Easy / Medium / Hard)
- Seasonal map rotations
- Player feedback: "I like Gauntlet, give me more narrow maps"
- Map-specific modifiers or mutators

