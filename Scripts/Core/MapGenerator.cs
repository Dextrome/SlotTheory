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
		var rng         = new Random(seed);
		var waypoints   = GeneratePathWaypoints(rng, out var pathGrid);
		var slots       = PlaceSlots(rng, pathGrid);
		var decorations = PlaceDecorations(rng, pathGrid, slots);
		return new MapLayout(waypoints, slots, pathGrid, decorations);
	}

	public static Vector2 CellCenter(int col, int row)
		=> new(col * CELL_W + CELL_W / 2f, GRID_Y + row * CELL_H + CELL_H / 2f);

	// ── Path generation — dispatch to shape variant ──────────────────────

	private static Vector2[] GeneratePathWaypoints(Random rng, out bool[,] pathGrid)
	{
		pathGrid = new bool[COLS, ROWS];
		return rng.Next(3) switch
		{
			0 => GenerateUShape(rng, pathGrid),
			1 => GenerateSShape(rng, pathGrid),
			_ => GenerateWShape(rng, pathGrid),
		};
	}

	/// <summary>
	/// U-shape: one horizontal leg.
	/// entry → col 0 DOWN → full row right → col 7 UP → exit
	/// </summary>
	private static Vector2[] GenerateUShape(Random rng, bool[,] pathGrid)
	{
		int r0 = rng.Next(2, 4);   // [2, 3] — capped at 3 so zone 1 (top-centre) always has path-adjacent cells

		MarkVertical  (pathGrid, 0, 0, r0);
		MarkHorizontal(pathGrid, 0, 7, r0);
		MarkVertical  (pathGrid, 7, 0, r0);

		float cx0  = CellCenter(0, 0).X;
		float cx7  = CellCenter(7, 0).X;
		float cyR0 = CellCenter(0, r0).Y;

		return new Vector2[]
		{
			new(cx0, 50),
			new(cx0, cyR0),
			new(cx7, cyR0),
			new(cx7, 50),
		};
	}

	/// <summary>
	/// S-shape: three horizontal legs (original design).
	/// entry → DOWN → right → UP → right → DOWN → right → col 7 UP → exit
	/// </summary>
	private static Vector2[] GenerateSShape(Random rng, bool[,] pathGrid)
	{
		// c1 ≤ 3, c2 ≤ 5 so col 6 is never a turn leg → zones 2 and 5 always have grass.
		int r0 = rng.Next(2, 5);           // [2, 4]
		int c1 = rng.Next(2, 4);           // [2, 3]
		int r1 = rng.Next(0, r0 - 1);     // [0, r0-2]
		int c2 = rng.Next(c1 + 2, 6);     // [c1+2, 5]
		int r2 = rng.Next(r1 + 1, 5);     // [r1+1, 4]

		MarkVertical  (pathGrid, 0,  0,  r0);
		MarkHorizontal(pathGrid, 0,  c1, r0);
		MarkVertical  (pathGrid, c1, r1, r0);
		MarkHorizontal(pathGrid, c1, c2, r1);
		MarkVertical  (pathGrid, c2, r1, r2);
		MarkHorizontal(pathGrid, c2, 7,  r2);
		MarkVertical  (pathGrid, 7,  0,  r2);

		float cx0  = CellCenter(0,  0).X;
		float cxC1 = CellCenter(c1, 0).X;
		float cxC2 = CellCenter(c2, 0).X;
		float cx7  = CellCenter(7,  0).X;

		float cyR0 = CellCenter(0, r0).Y;
		float cyR1 = CellCenter(0, r1).Y;
		float cyR2 = CellCenter(0, r2).Y;

		return new Vector2[]
		{
			new(cx0,  50),
			new(cx0,  cyR0),
			new(cxC1, cyR0),
			new(cxC1, cyR1),
			new(cxC2, cyR1),
			new(cxC2, cyR2),
			new(cx7,  cyR2),
			new(cx7,  50),
		};
	}

	/// <summary>
	/// W-shape: four horizontal legs.
	/// entry → DOWN → right → UP → right → DOWN → right → UP → right → col 7 UP → exit
	/// Creates a deeper zigzag with a longer path.
	/// </summary>
	private static Vector2[] GenerateWShape(Random rng, bool[,] pathGrid)
	{
		int c1 = rng.Next(2, 4);              // [2, 3]
		int c2 = rng.Next(c1 + 1, 5);        // [c1+1, 4]
		int c3 = rng.Next(Math.Max(c2 + 1, 5), 6); // always 5 — ensures zone 5 (bottom-right) has col 5 vertical path adjacent

		int r0 = rng.Next(3, 5);              // [3, 4] — low
		int r1 = rng.Next(0, 2);              // [0, 1] — high
		int r2 = rng.Next(3, 5);              // [3, 4] — low
		int r3 = rng.Next(0, 2);              // [0, 1] — high

		MarkVertical  (pathGrid, 0,  0,  r0);
		MarkHorizontal(pathGrid, 0,  c1, r0);
		MarkVertical  (pathGrid, c1, r1, r0);
		MarkHorizontal(pathGrid, c1, c2, r1);
		MarkVertical  (pathGrid, c2, r1, r2);
		MarkHorizontal(pathGrid, c2, c3, r2);
		MarkVertical  (pathGrid, c3, r3, r2);
		MarkHorizontal(pathGrid, c3, 7,  r3);
		MarkVertical  (pathGrid, 7,  0,  r3);

		float cx0  = CellCenter(0,  0).X;
		float cxC1 = CellCenter(c1, 0).X;
		float cxC2 = CellCenter(c2, 0).X;
		float cxC3 = CellCenter(c3, 0).X;
		float cx7  = CellCenter(7,  0).X;

		float cyR0 = CellCenter(0, r0).Y;
		float cyR1 = CellCenter(0, r1).Y;
		float cyR2 = CellCenter(0, r2).Y;
		float cyR3 = CellCenter(0, r3).Y;

		return new Vector2[]
		{
			new(cx0,  50),
			new(cx0,  cyR0),
			new(cxC1, cyR0),
			new(cxC1, cyR1),
			new(cxC2, cyR1),
			new(cxC2, cyR2),
			new(cxC3, cyR2),
			new(cxC3, cyR3),
			new(cx7,  cyR3),
			new(cx7,  50),
		};
	}

	// ── Slot placement ───────────────────────────────────────────────────

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

			var adjacent  = new List<(int col, int row)>();
			var grassOnly = new List<(int col, int row)>();

			for (int c = minCol; c <= maxCol; c++)
			for (int r = minRow; r <= maxRow; r++)
			{
				if (pathGrid[c, r]) continue;
				if (usedCells.Contains((c, r))) continue;

				if (IsAdjacentToPath(pathGrid, c, r))
					adjacent.Add((c, r));
				else
					grassOnly.Add((c, r));
			}

			(int col, int row) chosen;
			if (adjacent.Count > 0)
				chosen = adjacent[rng.Next(adjacent.Count)];
			else if (grassOnly.Count > 0)
			{
				// Pick whichever grass cell is closest to any path cell (maximises chance of being in tower range)
				float bestDist = float.MaxValue;
				var   bestCells = new List<(int col, int row)>();
				foreach (var (gc, gr) in grassOnly)
				{
					float minD = float.MaxValue;
					for (int pc = 0; pc < COLS; pc++)
					for (int pr = 0; pr < ROWS; pr++)
					{
						if (!pathGrid[pc, pr]) continue;
						float d = (CellCenter(gc, gr) - CellCenter(pc, pr)).Length();
						if (d < minD) minD = d;
					}
					if      (minD < bestDist - 0.5f) { bestDist = minD; bestCells.Clear(); bestCells.Add((gc, gr)); }
					else if (minD <= bestDist + 0.5f)  bestCells.Add((gc, gr));
				}
				chosen = bestCells.Count > 0 ? bestCells[rng.Next(bestCells.Count)] : grassOnly[rng.Next(grassOnly.Count)];
			}
			else
			{
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

	// ── Decoration placement ─────────────────────────────────────────────

	private static DecorationData[] PlaceDecorations(Random rng, bool[,] pathGrid, Vector2[] slotPositions)
	{
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
			if (rng.NextDouble() > 0.55) continue;

			float x    = c * CELL_W + margin + (float)(rng.NextDouble() * (CELL_W - 2 * margin));
			float y    = GRID_Y + r * CELL_H + margin + (float)(rng.NextDouble() * (CELL_H - 2 * margin));
			var   type = rng.NextDouble() > 0.30 ? DecorationType.Tree : DecorationType.Rock;
			result.Add(new DecorationData(new Vector2(x, y), type));
		}

		return result.ToArray();
	}

	// ── Adjacency check ──────────────────────────────────────────────────

	private static bool IsAdjacentToPath(bool[,] pathGrid, int col, int row)
		=> (col > 0        && pathGrid[col - 1, row]) ||
		   (col < COLS - 1 && pathGrid[col + 1, row]) ||
		   (row > 0        && pathGrid[col, row - 1]) ||
		   (row < ROWS - 1 && pathGrid[col, row + 1]);

	// ── Helpers ──────────────────────────────────────────────────────────

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
