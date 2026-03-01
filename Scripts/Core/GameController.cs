using Godot;
using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.UI;

namespace SlotTheory.Core;

public enum GamePhase { Boot, Draft, Wave, Win, Loss }

public partial class GameController : Node
{
	public static GameController Instance { get; private set; } = null!;

	public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;

	[Export] public PackedScene? EnemyScene { get; set; }
	[Export] public Path2D? LanePath { get; set; }

	private RunState _runState = null!;
	private DraftSystem _draftSystem = null!;
	private WaveSystem _waveSystem = null!;
	private CombatSim _combatSim = null!;
	private DraftPanel _draftPanel = null!;
	private Node2D[] _slotNodes = new Node2D[Balance.SlotCount];

	public override void _Ready()
	{
		Instance = this;
		DataLoader.LoadAll();

		_runState = new RunState();
		_draftSystem = new DraftSystem();
		_waveSystem = new WaveSystem();
		_combatSim = new CombatSim(_runState)
		{
			EnemyScene = EnemyScene,
			LanePath = LanePath,
		};
		_draftPanel = GetNode<DraftPanel>("../DraftPanel");

		SetupLane();
		SetupSlots();

		GD.Print("Slot Theory booted.");
		StartDraftPhase();
	}

	public override void _Process(double delta)
	{
		if (CurrentPhase != GamePhase.Wave) return;

		var result = _combatSim.Step((float)delta, _runState, _waveSystem);

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			GD.Print("Run lost.");
			return;
		}

		if (result == WaveResult.WaveComplete)
		{
			_runState.WaveIndex++;
			if (_runState.WaveIndex >= Balance.TotalWaves)
			{
				CurrentPhase = GamePhase.Win;
				GD.Print("Run won!");
			}
			else
			{
				StartDraftPhase();
			}
		}
	}

	public void StartDraftPhase()
	{
		CurrentPhase = GamePhase.Draft;
		var options = _draftSystem.GenerateOptions(_runState);
		GD.Print($"Wave {_runState.WaveIndex + 1} draft. Options: {options.Count}");
		_draftPanel.Show(options, _runState.WaveIndex + 1);
	}

	public RunState GetRunState() => _runState;

	/// <summary>Called by DraftPanel after the player picks an option.</summary>
	public void OnDraftPick(DraftOption option, int targetSlotIndex)
	{
		if (option.Type == DraftOptionType.Tower)
		{
			var freeIdx = System.Array.FindIndex(_runState.Slots, s => s.Tower == null);
			if (freeIdx >= 0)
				PlaceTower(option.Id, freeIdx);
		}
		else
		{
			var tower = _runState.Slots[targetSlotIndex].Tower;
			if (tower != null)
				_draftSystem.ApplyModifier(option.Id, tower);
		}
		StartWavePhase();
	}

	/// <summary>Place a tower by ID into a slot. Called by draft UI later.</summary>
	public void PlaceTower(string towerId, int slotIndex)
	{
		if (_runState.Slots[slotIndex].Tower != null) return;

		var def = DataLoader.GetTowerDef(towerId);
		var tower = new TowerInstance
		{
			TowerId        = towerId,
			BaseDamage     = def.BaseDamage,
			AttackInterval = def.AttackInterval,
			Range          = def.Range,
			AppliesMark    = def.AppliesMark,
			ProjectileColor = towerId switch
			{
				"rapid_shooter" => new Color(0.3f, 0.9f, 1.0f),  // cyan
				"heavy_cannon"  => new Color(1.0f, 0.55f, 0.0f), // orange
				"marker_tower"  => new Color(0.75f, 0.3f, 1.0f), // purple
				_               => Colors.Yellow,
			},
		};

		// Tower visual — blue square
		tower.AddChild(new ColorRect
		{
			Color        = new Color(0.2f, 0.5f, 1.0f),
			OffsetLeft   = -15f,
			OffsetTop    = -15f,
			OffsetRight  =  15f,
			OffsetBottom =  15f,
		});

		_slotNodes[slotIndex].AddChild(tower);
		_runState.Slots[slotIndex].Tower = tower;
		GD.Print($"Placed {def.Name} in slot {slotIndex}");
	}

	private void SetupSlots()
	{
		var slotsNode = GetNode<Node2D>("../World/Slots");
		for (int i = 0; i < Balance.SlotCount; i++)
		{
			_slotNodes[i] = slotsNode.GetNode<Node2D>($"Slot{i}");

			// Empty slot visual — dark gray square
			_slotNodes[i].AddChild(new ColorRect
			{
				Color        = new Color(0.25f, 0.25f, 0.25f, 0.5f),
				OffsetLeft   = -20f,
				OffsetTop    = -20f,
				OffsetRight  =  20f,
				OffsetBottom =  20f,
			});
		}
	}

	private void SetupLane()
	{
		if (LanePath == null) return;
		if (LanePath.Curve != null && LanePath.Curve.PointCount > 0) return;

		var curve = new Curve2D();
		curve.AddPoint(new Vector2(50,  300));
		curve.AddPoint(new Vector2(400, 200));
		curve.AddPoint(new Vector2(750, 400));
		curve.AddPoint(new Vector2(1150, 300));
		LanePath.Curve = curve;
	}

	private void StartWavePhase()
	{
		CurrentPhase = GamePhase.Wave;
		_waveSystem.LoadWave(_runState.WaveIndex, _runState);
		_combatSim.ResetForWave();
		GD.Print($"Wave {_runState.WaveIndex + 1} started.");
	}
}
