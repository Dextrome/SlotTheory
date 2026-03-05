using System;
using System.Collections.Generic;
using Godot;
using SlotTheory.Data;

namespace SlotTheory.Core;

/// <summary>
/// Abstract base class for all map types. Contains path waypoints and slot positions.
/// </summary>
public abstract class Map
{
    public string Id { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public Vector2[] Path { get; protected set; } = System.Array.Empty<Vector2>();
    public Vector2[] Slots { get; protected set; } = System.Array.Empty<Vector2>();
}

/// <summary>
/// Hand-crafted map loaded from maps.json.
/// </summary>
public class HandCraftedMap : Map
{
    public static HandCraftedMap LoadFromDef(MapDef def)
    {
        var map = new HandCraftedMap
        {
            Id = def.Id,
            Name = def.Name,
            Path = ConvertPath(def.Path),
            Slots = ConvertSlots(def.Slots),
        };
        return map;
    }

    private static Vector2[] ConvertPath(Vector2Def[] pathDefs)
    {
        var result = new Vector2[pathDefs.Length];
        for (int i = 0; i < pathDefs.Length; i++)
            result[i] = new Vector2(pathDefs[i].X, pathDefs[i].Y);
        return result;
    }

    private static Vector2[] ConvertSlots(SlotDef[] slotDefs)
    {
        var result = new Vector2[slotDefs.Length];
        for (int i = 0; i < slotDefs.Length; i++)
            result[i] = new Vector2(slotDefs[i].X, slotDefs[i].Y);
        return result;
    }
}

/// <summary>
/// Procedurally generated map. Uses the existing MapGenerator logic.
/// </summary>
public class ProceduralMap : Map
{
    public bool[,]? PathGrid { get; private set; }
    public DecorationData[] Decorations { get; private set; } = System.Array.Empty<DecorationData>();

    public static ProceduralMap Generate(ulong seed)
    {
        var layout = MapGenerator.Generate((int)seed);
        
        var map = new ProceduralMap
        {
            Id = "random_map",
            Name = MapGenerator.DescribeLayout(layout.PathWaypoints, (int)seed),
            Path = layout.PathWaypoints,
            Slots = layout.SlotPositions,
            PathGrid = layout.PathGrid,
            Decorations = layout.Decorations,
        };
        
        return map;
    }
}
