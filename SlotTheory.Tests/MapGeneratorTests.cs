using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public class MapGeneratorTests
{
    [Fact]
    public void Generate_RandomSeeds_AlwaysProducesSixUniqueNonPathSlots()
    {
        for (int seed = 0; seed < 400; seed++)
        {
            var layout = MapGenerator.Generate(seed);
            string slotDump = string.Join(", ", layout.SlotPositions.Select(s => $"({s.X:0.##},{s.Y:0.##})"));

            Assert.Equal(6, layout.SlotPositions.Length);

            var unique = new HashSet<(int Col, int Row)>();
            foreach (var slot in layout.SlotPositions)
            {
                int col = (int)(slot.X / MapGenerator.CELL_W);
                int row = (int)((slot.Y - MapGenerator.GRID_Y) / MapGenerator.CELL_H);

                Assert.InRange(col, 0, MapGenerator.COLS - 1);
                Assert.InRange(row, 0, MapGenerator.ROWS - 1);
                Assert.False(layout.PathGrid[col, row], $"Seed {seed}: slot placed on path at ({col}, {row}). Slots: {slotDump}");
                Assert.True(unique.Add((col, row)), $"Seed {seed}: duplicate slot at ({col}, {row}). Slots: {slotDump}");
            }
        }
    }

    [Fact]
    public void Generate_RandomSeeds_PathHasNoImmediateBacktrackTriples()
    {
        for (int seed = 0; seed < 400; seed++)
        {
            var layout = MapGenerator.Generate(seed);
            var path = layout.PathWaypoints;
            string pathDump = string.Join(" -> ", path.Select(p => $"({p.X:0.##},{p.Y:0.##})"));

            for (int i = 2; i < path.Length; i++)
            {
                bool pingPong = path[i].DistanceTo(path[i - 2]) < 0.01f;
                Assert.False(
                    pingPong,
                    $"Seed {seed}: immediate backtrack at path index {i}. Path: {pathDump}");
            }
        }
    }

    [Fact]
    public void Generate_RandomSeeds_UsesMultiplePathFamilies()
    {
        var waypointCounts = new HashSet<int>();

        for (int seed = 0; seed < 400; seed++)
        {
            var layout = MapGenerator.Generate(seed);
            waypointCounts.Add(layout.PathWaypoints.Length);
        }

        Assert.True(
            waypointCounts.Count >= 3,
            $"Expected at least 3 different waypoint counts, got {waypointCounts.Count}: {string.Join(", ", waypointCounts.OrderBy(v => v))}");
    }

    [Fact]
    public void Generate_RandomSeeds_SlotRowPatternsHaveStrongVariety()
    {
        var rowPatterns = new HashSet<string>();

        for (int seed = 0; seed < 400; seed++)
        {
            var layout = MapGenerator.Generate(seed);
            var orderedRows = layout.SlotPositions
                .Select(slot =>
                {
                    int col = (int)(slot.X / MapGenerator.CELL_W);
                    int row = (int)((slot.Y - MapGenerator.GRID_Y) / MapGenerator.CELL_H);
                    return (col, row);
                })
                .OrderBy(p => p.col)
                .ThenBy(p => p.row)
                .Select(p => p.row);

            rowPatterns.Add(string.Join(",", orderedRows));
        }

        Assert.True(
            rowPatterns.Count >= 12,
            $"Expected at least 12 distinct slot-row patterns, got {rowPatterns.Count}.");
    }
}
