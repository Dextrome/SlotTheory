using System;
using System.Collections.Generic;
using Godot;

namespace SlotTheory.Core;

public enum DecorationType { Tree, Rock }
public record DecorationData(Vector2 Pos, DecorationType Type);
public record MapLayout(Vector2[] PathWaypoints, Vector2[] SlotPositions, bool[,] PathGrid, DecorationData[] Decorations);

public static class MapGenerator
{
	public const int COLS = 8, ROWS = 5, CELL_W = 160, CELL_H = 128, GRID_Y = 80;

	public static MapLayout Generate(int seed)
	{
		var rng = new Random(seed);
		var waypoints    = GeneratePathWaypoints(rng, out var pathGrid);
		var slots        = PlaceSlots(rng, pathGrid);
		var decorations  = PlaceDecorations(rng, pathGrid, slots);
		return new MapLayout(waypoints, slots, pathGrid, decorations);
	}

	public static Vector2 CellCenter(int col, int row)
		=> new(col * CELL_W + CELL_W / 2f, GRID_Y + row * CELL_H + CELL_H / 2f);

	// ── Path generation ─────────────────────────────────────────────────

	private static Vector2[] GeneratePathWaypoints(Random rng, out bool[,] pathGrid)
	{
		pathGrid = new bool[COLS, ROWS];

		// Fixed 3-leg snake; pick random turning rows/cols.
		// c1 capped at 3 and c2 capped at 5 so col 6 is never a vertical turn leg —
		// that guarantees zones 2 and 5 (cols 6-7) always contain grass cells.
		int r0 = rng.Next(2, 5);           // [2, 4]  — first turn row (down)
		int c1 = rng.Next(2, 4);           // [2, 3]  — first turn col (right)
		int r1 = rng.Next(0, r0 - 1);      // [0, r0-2] — second turn row (up)
		int c2 = rng.Next(c1 + 2, 6);      // [c1+2, 5] — second turn col (right)
		int r2 = rng.Next(r1 + 1, 5);      // [r1+1, 4] — third turn row (down)

		// Mark path cells
		MarkVertical(pathGrid,   0,  0,  r0);   // col 0 down
		MarkHorizontal(pathGrid, 0,  c1, r0);   // row r0 right
		MarkVertical(pathGrid,   c1, r1, r0);   // col c1 up
		MarkHorizontal(pathGrid, c1, c2, r1);   // row r1 right
		MarkVertical(pathGrid,   c2, r1, r2);   // col c2 down
		MarkHorizontal(pathGrid, c2, 7,  r2);   // row r2 right
		MarkVertical(pathGrid,   7,  0,  r2);   // col 7 up to exit

		// Resolve world-space X/Y for each turning point
		float cx0  = CellCenter(0,  0).X;   // 80
		float cxC1 = CellCenter(c1, 0).X;
		float cxC2 = CellCenter(c2, 0).X;
		float cx7  = CellCenter(7,  0).X;   // 1200

		float cyR0 = CellCenter(0, r0).Y;
		float cyR1 = CellCenter(0, r1).Y;
		float cyR2 = CellCenter(0, r2).Y;

		return new Vector2[]
		{
			new(cx0,  50),      // entry above col 0
			new(cx0,  cyR0),    // bottom of first vertical
			new(cxC1, cyR0),    // right end of first horizontal
			new(cxC1, cyR1),    // top of second vertical
			new(cxC2, cyR1),    // right end of second horizontal
			new(cxC2, cyR2),    // bottom of third vertical
			new(cx7,  cyR2),    // right end of third horizontal
			new(cx7,  50),      // exit above col 7
		};
	}

	// ── Slot placement ──────────────────────────────────────────────────

	private static readonly (int minCol, int maxCol, int minRow, int maxRow)[] Zones =
	{
		(0, 2, 0, 2),   // 0: top-left
		(3, 5, 0, 2),   // 1: top-center
		(6, 7, 0, 2),   // 2: top-right
		(0, 2, 3, 4),   // 3: bottom-left
		(3, 5, 3, 4),   // 4: bottom-center
		(6, 7, 3, 4),   // 5: bottom-right
	};

	private static Vector2[] PlaceSlots(Random rng, bool[,] pathGrid)
	{
		var result    = new Vector2[6];
		var usedCells = new HashSet<(int, int)>();

		for (int z = 0; z < 6; z++)
		{
			var (minCol, maxCol, minRow, maxRow) = Zones[z];

			// Prefer grass cells that are directly adjacent to the path (dist == 1):
			// towers placed here are always within range.  Fall back to any grass cell
			// in the zone, then to any unused cell if the zone is entirely path.
			var adjacent = new List<(int col, int row)>();
			var grassOnly = new List<(int col, int row)>();

			for (int c = minCol; c <= maxCol; c++)
			{
				for (int r = minRow; r <= maxRow; r++)
				{
					if (pathGrid[c, r]) continue;
					if (usedCells.Contains((c, r))) continue;

					if (IsAdjacentToPath(pathGrid, c, r))
						adjacent.Add((c, r));
					else
						grassOnly.Add((c, r));
				}
			}

			(int col, int row) chosen;
			if (adjacent.Count > 0)
				chosen = adjacent[rng.Next(adjacent.Count)];
			else if (grassOnly.Count > 0)
				chosen = grassOnly[rng.Next(grassOnly.Count)];
			else
			{
				// Last resort: any unused cell in zone, even if on path
				var any = new List<(int, int)>();
				for (int c = minCol; c <= maxCol; c++)
					for (int r = minRow; r <= maxRow; r++)
						if (!usedCells.Contains((c, r)))
							any.Add((c, r));
				chosen = any.Count > 0 ? any[rng.Next(any.Count)] : (minCol, minRow);
			}

			usedCells.Add(chosen);
			result[z] = CellCenter(chosen.col, chosen.row);
		}

		return result;
	}

	private static bool IsAdjacentToPath(bool[,] pathGrid, int col, int row)
		=> (col > 0        && pathGrid[col - 1, row]) ||
		   (col < COLS - 1 && pathGrid[col + 1, row]) ||
		   (row > 0        && pathGrid[col, row - 1]) ||
		   (row < ROWS - 1 && pathGrid[col, row + 1]);

	// ── Decoration placement ────────────────────────────────────────────

	private static DecorationData[] PlaceDecorations(Random rng, bool[,] pathGrid, Vector2[] slotPositions)
	{
		// Mark which cells hold a slot so we leave them clear
		var slotCells = new HashSet<(int, int)>();
		foreach (var s in slotPositions)
		{
			int sc = (int)(s.X / CELL_W);
			int sr = (int)((s.Y - GRID_Y) / CELL_H);
			slotCells.Add((Math.Clamp(sc, 0, COLS - 1), Math.Clamp(sr, 0, ROWS - 1)));
		}

		var result = new List<DecorationData>();
		const float margin = 22f;

		for (int c = 0; c < COLS; c++)
		for (int r = 0; r < ROWS; r++)
		{
			if (pathGrid[c, r]) continue;
			if (slotCells.Contains((c, r))) continue;
			if (rng.NextDouble() > 0.55) continue;   // ~55 % of grass cells get a decoration

			float x = c * CELL_W + margin + (float)(rng.NextDouble() * (CELL_W - 2 * margin));
			float y = GRID_Y + r * CELL_H + margin + (float)(rng.NextDouble() * (CELL_H - 2 * margin));
			var type = rng.NextDouble() > 0.30 ? DecorationType.Tree : DecorationType.Rock;
			result.Add(new DecorationData(new Vector2(x, y), type));
		}

		return result.ToArray();
	}

	// ── Helpers ─────────────────────────────────────────────────────────

	private static void MarkVertical(bool[,] pathGrid, int col, int fromRow, int toRow)
	{
		int lo = Math.Min(fromRow, toRow);
		int hi = Math.Max(fromRow, toRow);
		for (int r = lo; r <= hi; r++)
			pathGrid[col, r] = true;
	}

	private static void MarkHorizontal(bool[,] pathGrid, int fromCol, int toCol, int row)
	{
		int lo = Math.Min(fromCol, toCol);
		int hi = Math.Max(fromCol, toCol);
		for (int c = lo; c <= hi; c++)
			pathGrid[c, row] = true;
	}
}
