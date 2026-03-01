using Godot;
using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Core;

public enum GamePhase { Boot, Draft, Wave, Win, Loss }

public partial class GameController : Node
{
    public static GameController Instance { get; private set; } = null!;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;

    private RunState _runState = null!;
    private DraftSystem _draftSystem = null!;
    private WaveSystem _waveSystem = null!;
    private CombatSim _combatSim = null!;

    public override void _Ready()
    {
        Instance = this;
        DataLoader.LoadAll();

        _runState = new RunState();
        _draftSystem = new DraftSystem();
        _waveSystem = new WaveSystem();
        _combatSim = new CombatSim(_runState);

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
        // TODO: pass options to DraftPanel UI
        GD.Print($"Wave {_runState.WaveIndex + 1} draft. Options: {options.Count}");
    }

    public void OnDraftConfirmed()
    {
        StartWavePhase();
    }

    private void StartWavePhase()
    {
        CurrentPhase = GamePhase.Wave;
        _waveSystem.LoadWave(_runState.WaveIndex, _runState);
        GD.Print($"Wave {_runState.WaveIndex + 1} started.");
    }
}
