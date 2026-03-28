using System;
using System.Collections.Generic;
using System.Linq;
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
		var slots       = PlaceSlots(rng, pathGrid, waypoints);
		var decorations = PlaceDecorations(rng, pathGrid, slots);
		return new MapLayout(waypoints, slots, pathGrid, decorations);
	}

	public static string DescribeSeed(int seed)
	{
		var layout = Generate(seed);
		return DescribeLayout(layout.PathWaypoints, seed);
	}

	public static string DescribeLayout(Vector2[] waypoints, int seed = 0)
	{
		if (waypoints.Length < 2) return "Serpent Grid";

		float length = 0f;
		int leftTurns = 0;
		int rightTurns = 0;
		int turnCount = 0;

		for (int i = 1; i < waypoints.Length; i++)
			length += waypoints[i - 1].DistanceTo(waypoints[i]);

		for (int i = 1; i < waypoints.Length - 1; i++)
		{
			var a = (waypoints[i] - waypoints[i - 1]).Normalized();
			var b = (waypoints[i + 1] - waypoints[i]).Normalized();
			float cross = a.Cross(b);
			if (Mathf.Abs(cross) < 0.02f) continue;
			turnCount++;
			if (cross > 0f) leftTurns++;
			else rightTurns++;
		}

		string baseName = "Serpent Grid";
		if (length >= 3200f || turnCount >= 7)
			baseName = "Long Hook";
		else if (Mathf.Abs(leftTurns - rightTurns) <= 1 && turnCount >= 5)
			baseName = "Split Coil";
		else if (rightTurns > leftTurns + 1)
			baseName = "Split Coil";

		// Deterministic tiny variation to avoid repeated naming monotony.
		int variant = Mathf.Abs(seed ^ (int)length ^ (leftTurns << 4) ^ rightTurns) % 4;
		return baseName switch
		{
			"Long Hook" when variant == 1 => "Long Hook Prime",
			"Split Coil" when variant == 1 => "Split Coil Array",
			"Serpent Grid" when variant == 1 => "Serpent Grid Delta",
			_ => baseName,
		};
	}

	public static Vector2 CellCenter(int col, int row)
		=> new(col * CELL_W + CELL_W / 2f, GRID_Y + row * CELL_H + CELL_H / 2f);

	// ── Path generation ──────────────────────────────────────────────────

	private static Vector2[] GeneratePathWaypoints(Random rng, out bool[,] pathGrid)
	{
		var canonicalGrid = new bool[COLS, ROWS];
		Vector2[] canonical = rng.Next(100) switch
		{
			< 24 => GenerateZigzag(rng, canonicalGrid),
			< 45 => GenerateTopFirstZigzag(rng, canonicalGrid),
			< 64 => GenerateDualLoopZigzag(rng, canonicalGrid),
			< 83 => GenerateSShape(rng, canonicalGrid),
			_    => GenerateWShape(rng, canonicalGrid),
		};

		// Blend mirrored/normal variants to widen visible route families.
		bool mirrorX = rng.Next(2) == 1;
		bool mirrorY = rng.Next(3) == 0;
		pathGrid = mirrorX || mirrorY
			? TransformGrid(canonicalGrid, mirrorX, mirrorY)
			: canonicalGrid;
		var transformed = mirrorX || mirrorY
			? TransformWaypoints(canonical, mirrorX, mirrorY)
			: canonical;

		// Occasionally reverse lane direction so shared cell layouts still play differently.
		if (rng.Next(4) == 0)
			transformed = ReverseWaypoints(transformed);

		return transformed;
	}

	/// <summary>
	/// Zigzag path with column variation.
	/// Randomized knobs:
	/// - midRow in [2,3]
	/// - returnCol in [1,2] (where path comes back up)
	/// - exitCol = returnCol + 5 (6 or 7) to keep top sweep length stable.
	/// This gives multiple visible layouts while preserving overall difficulty.
	/// </summary>
	private static Vector2[] GenerateZigzag(Random rng, bool[,] pathGrid)
	{
		int midRow = rng.Next(2, 4);   // [2, 3]
		int returnCol = rng.Next(1, 3); // [1, 2]
		int exitCol = returnCol + 5;    // 6 or 7

		// Mark path cells.
		MarkVertical  (pathGrid, 0,         0,        midRow);   // col 0: entry vertical
		MarkHorizontal(pathGrid, 0,         7,        midRow);   // mid-row sweep
		MarkVertical  (pathGrid, 7,         midRow,   ROWS - 1); // col 7: down to bottom
		MarkHorizontal(pathGrid, returnCol, 7,        ROWS - 1); // bottom sweep
		MarkVertical  (pathGrid, returnCol, 0,        ROWS - 1); // return column: up to top
		MarkHorizontal(pathGrid, returnCol, exitCol,  0);        // top sweep cells

		float cx0 = CellCenter(0,        0).X;
		float cxR = CellCenter(returnCol, 0).X;
		float cx7 = CellCenter(7,        0).X;
		float cxE = CellCenter(exitCol,  0).X;
		float cy0 = CellCenter(0,        0).Y;
		float cyM = CellCenter(0,        midRow).Y;
		float cyB = CellCenter(0,        ROWS - 1).Y;

		return new Vector2[]
		{
			new(cx0, 50),    // entry above grid
			new(cx0, cyM),   // bottom of entry vertical
			new(cx7, cyM),   // end of mid-row horizontal
			new(cx7, cyB),   // bottom of col 7
			new(cxR, cyB),   // end of bottom horizontal
			new(cxR, cy0),   // top of return column
			new(cxE, cy0),   // top sweep to exit column
			new(cxE, 50),    // exit above grid
		};
	}

	/// <summary>
	/// Variant with an early top loop and bottom finish.
	/// Visibly different from GenerateZigzag while keeping similar flow.
	/// </summary>
	private static Vector2[] GenerateTopFirstZigzag(Random rng, bool[,] pathGrid)
	{
		int midRow = rng.Next(2, 4);    // [2, 3]
		int returnCol = rng.Next(1, 3); // [1, 2]
		int exitCol = returnCol + 5;    // 6 or 7

		MarkVertical  (pathGrid, 0,         0,        midRow);   // entry vertical
		MarkHorizontal(pathGrid, 0,         7,        midRow);   // mid sweep
		MarkVertical  (pathGrid, 7,         0,        midRow);   // rise to top
		MarkHorizontal(pathGrid, returnCol, 7,        0);        // top sweep
		MarkVertical  (pathGrid, returnCol, 0,        ROWS - 1); // drop to bottom
		MarkHorizontal(pathGrid, returnCol, exitCol,  ROWS - 1); // bottom sweep cells
		MarkVertical  (pathGrid, exitCol,   0,        ROWS - 1); // exit column back up

		float cx0 = CellCenter(0,         0).X;
		float cxR = CellCenter(returnCol, 0).X;
		float cx7 = CellCenter(7,         0).X;
		float cxE = CellCenter(exitCol,   0).X;
		float cy0 = CellCenter(0,         0).Y;
		float cyM = CellCenter(0,         midRow).Y;
		float cyB = CellCenter(0,         ROWS - 1).Y;

		return new Vector2[]
		{
			new(cx0, 50),    // entry
			new(cx0, cyM),
			new(cx7, cyM),
			new(cx7, cy0),
			new(cxR, cy0),
			new(cxR, cyB),
			new(cxE, cyB),   // bottom sweep to exit column
			new(cxE, cy0),
			new(cxE, 50),    // exit
		};
	}

	/// <summary>
	/// Variant with top loop and bottom sweep before final ascent.
	/// </summary>
	private static Vector2[] GenerateDualLoopZigzag(Random rng, bool[,] pathGrid)
	{
		int midRow = rng.Next(2, 4);    // [2, 3]
		int returnCol = rng.Next(1, 3); // [1, 2]
		int exitCol = returnCol + 5;    // 6 or 7
		int loopExitCol = rng.Next(returnCol + 1, exitCol); // [returnCol+1, exitCol-1]

		MarkVertical  (pathGrid, 0,         0,        midRow);   // entry vertical
		MarkHorizontal(pathGrid, 0,         7,        midRow);   // mid sweep
		MarkVertical  (pathGrid, 7,         0,        midRow);   // rise to top
		MarkHorizontal(pathGrid, returnCol, 7,        0);        // top sweep
		MarkVertical  (pathGrid, returnCol, 0,        ROWS - 1); // drop to bottom
		MarkHorizontal(pathGrid, returnCol, exitCol,  ROWS - 1); // bottom sweep
		MarkVertical  (pathGrid, exitCol,   0,        ROWS - 1); // rise to top for second loop
		MarkHorizontal(pathGrid, loopExitCol, exitCol, 0);       // final top exit leg

		float cx0 = CellCenter(0,         0).X;
		float cxR = CellCenter(returnCol, 0).X;
		float cx7 = CellCenter(7,         0).X;
		float cxE = CellCenter(exitCol,   0).X;
		float cxL = CellCenter(loopExitCol, 0).X;
		float cy0 = CellCenter(0,         0).Y;
		float cyM = CellCenter(0,         midRow).Y;
		float cyB = CellCenter(0,         ROWS - 1).Y;

		return new Vector2[]
		{
			new(cx0, 50),    // entry
			new(cx0, cyM),
			new(cx7, cyM),
			new(cx7, cy0),
			new(cxR, cy0),
			new(cxR, cyB),
			new(cxE, cyB),
			new(cxE, cy0),
			new(cxL, cy0),   // final top leg before exit
			new(cxL, 50),    // exit
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
		int c3 = rng.Next(Math.Max(c2 + 1, 5), 6); // always 5 - ensures zone 5 (bottom-right) has col 5 vertical path adjacent

		int r0 = rng.Next(3, 5);              // [3, 4] - low
		int r1 = rng.Next(0, 2);              // [0, 1] - high
		int r2 = rng.Next(3, 5);              // [3, 4] - low
		int r3 = rng.Next(0, 2);              // [0, 1] - high

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

	// Score range: slightly above arc emitter range (333px) to capture bend multi-leg coverage.
	private const float SlotScoreRange     = 340f;
	// Sample spacing along path for coverage scoring.
	private const float ScoreSampleSpacing = 30f;

	private enum SlotPlacementProfile
	{
		Coverage,
		Balanced,
		Wild,
	}

	private readonly record struct SlotCandidate(
		int Col,
		int Row,
		Vector2 Position,
		int CoverageScore,
		bool AdjacentToPath,
		int ZoneIndex
	);

	/// <summary>
	/// Score a cell by weighted path coverage within SlotScoreRange.
	/// Waypoints (bends) count 5× - slots near bends cover enemies from both approaching legs.
	/// </summary>
	private static int ScoreCell(Vector2 cellCenter, Vector2[] waypoints)
	{
		int count = 0;
		for (int i = 1; i < waypoints.Length - 1; i++)
			if (cellCenter.DistanceTo(waypoints[i]) <= SlotScoreRange)
				count += 5;

		for (int i = 0; i < waypoints.Length - 1; i++)
		{
			var a = waypoints[i];
			var b = waypoints[i + 1];
			float segLen = a.DistanceTo(b);
			if (segLen < 1f) continue;
			int steps = Math.Max(1, (int)(segLen / ScoreSampleSpacing));
			for (int s = 0; s <= steps; s++)
			{
				var sample = a.Lerp(b, (float)s / steps);
				if (cellCenter.DistanceTo(sample) <= SlotScoreRange)
					count++;
			}
		}
		return count;
	}

	// Six spatial zones covering the full grid - guarantees one slot per map area.
	// Zones: top-left | top-center | top-right | bottom-left | bottom-center | bottom-right
	private static readonly (int minCol, int maxCol, int minRow, int maxRow)[] Zones =
	{
		(0, 2, 0, 2),   // 0: top-left
		(3, 5, 0, 2),   // 1: top-center
		(6, 7, 0, 2),   // 2: top-right
		(0, 2, 3, 4),   // 3: bottom-left
		(3, 5, 3, 4),   // 4: bottom-center
		(6, 7, 3, 4),   // 5: bottom-right
	};

	private static Vector2[] PlaceSlots(Random rng, bool[,] pathGrid, Vector2[] waypoints)
	{
		const int NumSlots = 6;

		var profile = (SlotPlacementProfile)rng.Next(0, 3);
		int targetAdjacentCount = profile switch
		{
			SlotPlacementProfile.Coverage => 5,
			SlotPlacementProfile.Balanced => 4 + rng.Next(0, 2),
			_                             => 3 + rng.Next(0, 3),
		};

		var allCandidates = BuildSlotCandidates(pathGrid, waypoints);
		var selected = new List<SlotCandidate>(NumSlots);
		var used = new HashSet<(int Col, int Row)>();

		// Pass 1: take one per zone where possible, but in random zone order each run.
		var zoneOrder = Enumerable.Range(0, Zones.Length)
			.OrderBy(_ => rng.Next())
			.ToArray();
		foreach (int zone in zoneOrder)
		{
			if (selected.Count >= NumSlots) break;

			var zoneCandidates = allCandidates.Where(c => c.ZoneIndex == zone && !used.Contains((c.Col, c.Row)));
			var picked = PickCandidate(zoneCandidates, selected, profile, targetAdjacentCount, rng);
			if (picked == null) continue;

			selected.Add(picked.Value);
			used.Add((picked.Value.Col, picked.Value.Row));
		}

		// Pass 2: fill remaining slots globally with spacing-aware weighted picks.
		while (selected.Count < NumSlots)
		{
			var remaining = allCandidates.Where(c => !used.Contains((c.Col, c.Row)));
			var picked = PickCandidate(remaining, selected, profile, targetAdjacentCount, rng);
			if (picked == null) break;

			selected.Add(picked.Value);
			used.Add((picked.Value.Col, picked.Value.Row));
		}

		// Hard safety net: include any non-path cell not already used (should be rare).
		if (selected.Count < NumSlots)
		{
			for (int c = 0; c < COLS && selected.Count < NumSlots; c++)
			for (int r = 0; r < ROWS && selected.Count < NumSlots; r++)
			{
				if (pathGrid[c, r] || used.Contains((c, r)))
					continue;

				var fallback = new SlotCandidate(
					c,
					r,
					CellCenter(c, r),
					ScoreCell(CellCenter(c, r), waypoints),
					IsAdjacentToPath(pathGrid, c, r),
					GetZoneIndex(c, r));

				selected.Add(fallback);
				used.Add((c, r));
			}
		}

		return selected
			.Take(NumSlots)
			.OrderBy(s => s.Position.X)
			.ThenBy(s => s.Position.Y)
			.Select(s => s.Position)
			.ToArray();
	}

	private static List<SlotCandidate> BuildSlotCandidates(bool[,] pathGrid, Vector2[] waypoints)
	{
		var candidates = new List<SlotCandidate>(COLS * ROWS);
		for (int c = 0; c < COLS; c++)
		for (int r = 0; r < ROWS; r++)
		{
			if (pathGrid[c, r]) continue;

			var pos = CellCenter(c, r);
			candidates.Add(new SlotCandidate(
				c,
				r,
				pos,
				ScoreCell(pos, waypoints),
				IsAdjacentToPath(pathGrid, c, r),
				GetZoneIndex(c, r)));
		}

		return candidates;
	}

	private static SlotCandidate? PickCandidate(
		IEnumerable<SlotCandidate> candidates,
		IReadOnlyList<SlotCandidate> alreadyPlaced,
		SlotPlacementProfile profile,
		int targetAdjacentCount,
		Random rng)
	{
		var ranked = candidates
			.Select(c => (Candidate: c, Score: ScoreSlotCandidate(c, alreadyPlaced, profile, targetAdjacentCount, rng)))
			.OrderByDescending(x => x.Score)
			.ToArray();

		if (ranked.Length == 0)
			return null;

		// Pick among the strongest few to preserve quality while introducing variety.
		int topPool = Math.Min(profile == SlotPlacementProfile.Wild ? 4 : 3, ranked.Length);
		int idx = rng.Next(topPool);
		return ranked[idx].Candidate;
	}

	private static float ScoreSlotCandidate(
		SlotCandidate candidate,
		IReadOnlyList<SlotCandidate> alreadyPlaced,
		SlotPlacementProfile profile,
		int targetAdjacentCount,
		Random rng)
	{
		float spacing = MinDistanceToPlaced(candidate.Position, alreadyPlaced);
		float spacingScore = Mathf.Clamp(spacing / 140f, 0f, 3f);

		int adjacentPlaced = alreadyPlaced.Count(s => s.AdjacentToPath);
		int adjacentNeed = Math.Max(0, targetAdjacentCount - adjacentPlaced);
		float adjacencyBias = 0f;
		if (adjacentNeed > 0)
			adjacencyBias = candidate.AdjacentToPath ? 8f : -3f;
		else if (candidate.AdjacentToPath)
			adjacencyBias = 2f;

		float profileScore = profile switch
		{
			SlotPlacementProfile.Coverage => candidate.CoverageScore * 1.35f + (candidate.AdjacentToPath ? 5f : -1f),
			SlotPlacementProfile.Balanced => candidate.CoverageScore * 1.10f + spacingScore * 4f,
			_                             => candidate.CoverageScore * 0.85f + spacingScore * 6f,
		};

		float randomness = profile switch
		{
			SlotPlacementProfile.Coverage => rng.Next(0, 6),
			SlotPlacementProfile.Balanced => rng.Next(-1, 9),
			_                             => rng.Next(-3, 14),
		};

		return profileScore + adjacencyBias + randomness;
	}

	private static float MinDistanceToPlaced(Vector2 pos, IReadOnlyList<SlotCandidate> placed)
	{
		if (placed.Count == 0)
			return 999f;

		float min = float.MaxValue;
		foreach (var slot in placed)
			min = MathF.Min(min, pos.DistanceTo(slot.Position));
		return min;
	}

	private static int GetZoneIndex(int col, int row)
	{
		for (int i = 0; i < Zones.Length; i++)
		{
			var zone = Zones[i];
			if (col >= zone.minCol && col <= zone.maxCol && row >= zone.minRow && row <= zone.maxRow)
				return i;
		}
		return -1;
	}

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

	private static bool[,] TransformGrid(bool[,] source, bool mirrorX, bool mirrorY)
	{
		var result = new bool[COLS, ROWS];
		for (int c = 0; c < COLS; c++)
		for (int r = 0; r < ROWS; r++)
		{
			if (!source[c, r]) continue;
			int tc = mirrorX ? (COLS - 1 - c) : c;
			int tr = mirrorY ? (ROWS - 1 - r) : r;
			result[tc, tr] = true;
		}
		return result;
	}

	private static Vector2[] TransformWaypoints(Vector2[] source, bool mirrorX, bool mirrorY)
	{
		var result = new Vector2[source.Length];
		for (int i = 0; i < source.Length; i++)
		{
			float x = source[i].X;
			float y = source[i].Y;
			if (mirrorX) x = COLS * CELL_W - x;
			if (mirrorY) y = GRID_Y + ROWS * CELL_H + GRID_Y - y;
			result[i] = new Vector2(x, y);
		}
		return result;
	}

	private static Vector2[] ReverseWaypoints(Vector2[] source)
	{
		var result = new Vector2[source.Length];
		for (int i = 0; i < source.Length; i++)
			result[i] = source[source.Length - 1 - i];
		return result;
	}

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
