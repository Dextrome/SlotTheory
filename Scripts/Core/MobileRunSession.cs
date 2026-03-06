using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using SlotTheory.Core.Leaderboards;

namespace SlotTheory.Core;

public sealed class MobileRunSlotSnapshot
{
	public int SlotIndex { get; set; }
	public string TowerId { get; set; } = "";
	public List<string> ModifierIds { get; set; } = new();
}

public sealed class MobileRunSnapshot
{
	public int Version { get; set; } = 1;
	public long SavedAtUnixSeconds { get; set; } = 0;
	public string Phase { get; set; } = "draft"; // "draft" | "wave"
	public string MapId { get; set; } = LeaderboardKey.RandomMapId;
	public int RngSeed { get; set; } = 0;
	public int WaveIndex { get; set; } = 0;
	public int Lives { get; set; } = Balance.StartingLives;
	public int TotalKills { get; set; } = 0;
	public int TotalDamageDealt { get; set; } = 0;
	public float TotalPlayTime { get; set; } = 0f;
	public List<MobileRunSlotSnapshot> Slots { get; set; } = new();
}

public static class MobileRunSession
{
	private const string SavePath = "user://mobile_run_session.json";
	private const int CurrentVersion = 1;
	private const long MaxSnapshotAgeSeconds = 12 * 60 * 60; // 12h
	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		WriteIndented = false
	};

	public static bool HasSnapshot()
	{
		if (!FileAccess.FileExists(SavePath))
			return false;
		return TryLoad(out _);
	}

	public static void Save(GamePhase phase, RunState runState)
	{
		if (!IsActiveRunPhase(phase))
		{
			Clear();
			return;
		}

		try
		{
			var snapshot = new MobileRunSnapshot
			{
				Version = CurrentVersion,
				SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				Phase = phase == GamePhase.Wave ? "wave" : "draft",
				MapId = string.IsNullOrWhiteSpace(runState.SelectedMapId) ? LeaderboardKey.RandomMapId : runState.SelectedMapId!,
				RngSeed = runState.RngSeed,
				WaveIndex = runState.WaveIndex,
				Lives = runState.Lives,
				TotalKills = runState.TotalKills,
				TotalDamageDealt = runState.TotalDamageDealt,
				TotalPlayTime = runState.TotalPlayTime,
				Slots = CaptureSlots(runState)
			};

			string json = JsonSerializer.Serialize(snapshot, JsonOpts);
			using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
			file.StoreString(json);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MobileSession] Failed to save snapshot: {ex.Message}");
		}
	}

	public static bool TryLoad(out MobileRunSnapshot snapshot)
	{
		snapshot = new MobileRunSnapshot();
		if (!FileAccess.FileExists(SavePath))
			return false;

		try
		{
			using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
			var json = file.GetAsText();
			var loaded = JsonSerializer.Deserialize<MobileRunSnapshot>(json, JsonOpts);
			if (loaded == null || !IsValid(loaded))
			{
				Clear();
				return false;
			}

			long age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - loaded.SavedAtUnixSeconds;
			if (age > MaxSnapshotAgeSeconds)
			{
				Clear();
				return false;
			}

			snapshot = loaded;
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[MobileSession] Failed to load snapshot: {ex.Message}");
			Clear();
			return false;
		}
	}

	public static void Clear()
	{
		if (!FileAccess.FileExists(SavePath))
			return;

		var err = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
		if (err != Error.Ok && err != Error.FileNotFound)
			GD.PrintErr($"[MobileSession] Failed to clear snapshot: {err}");
	}

	public static bool IsActiveRunPhase(GamePhase phase)
	{
		return phase == GamePhase.Draft || phase == GamePhase.Wave;
	}

	private static List<MobileRunSlotSnapshot> CaptureSlots(RunState runState)
	{
		var slots = new List<MobileRunSlotSnapshot>();
		for (int i = 0; i < runState.Slots.Length; i++)
		{
			var tower = runState.Slots[i].Tower;
			if (tower == null)
				continue;

			slots.Add(new MobileRunSlotSnapshot
			{
				SlotIndex = i,
				TowerId = tower.TowerId ?? "",
				ModifierIds = tower.Modifiers
					.Select(m => m.ModifierId)
					.Where(id => !string.IsNullOrWhiteSpace(id))
					.Take(Balance.MaxModifiersPerTower)
					.ToList()
			});
		}
		return slots;
	}

	private static bool IsValid(MobileRunSnapshot snapshot)
	{
		if (snapshot.Version != CurrentVersion)
			return false;
		if (snapshot.SavedAtUnixSeconds <= 0)
			return false;
		if (snapshot.Phase != "draft" && snapshot.Phase != "wave")
			return false;
		if (snapshot.WaveIndex < 0 || snapshot.WaveIndex >= Balance.TotalWaves)
			return false;
		if (snapshot.Lives < 0 || snapshot.Lives > 999)
			return false;
		if (snapshot.TotalKills < 0 || snapshot.TotalDamageDealt < 0 || snapshot.TotalPlayTime < 0f)
			return false;
		if (string.IsNullOrWhiteSpace(snapshot.MapId))
			return false;

		foreach (var slot in snapshot.Slots)
		{
			if (slot.SlotIndex < 0 || slot.SlotIndex >= Balance.SlotCount)
				return false;
			if (string.IsNullOrWhiteSpace(slot.TowerId))
				return false;
			if (slot.ModifierIds.Count > Balance.MaxModifiersPerTower)
				return false;
		}

		return true;
	}
}
