using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Entities;

namespace SlotTheory.Core;

public readonly record struct SpectacleVisualState(float MeterNormalized, float Pulse, string PrimaryModId);

public readonly record struct SpectacleSignature(
    SpectacleMode Mode,
    string PrimaryModId,
    string SecondaryModId,
    string TertiaryModId,
    float SurgePower,
    string EffectId,
    string EffectName,
    string ComboEffectId,
    string ComboEffectName,
    string AugmentEffectId,
    string AugmentName);

public readonly record struct SpectacleTriggerInfo(
    ITowerView Tower,
    bool IsSurge,
    SpectacleSignature Signature,
    float MeterAfter);

public readonly record struct GlobalSurgeTriggerInfo(float MeterAfter, int UniqueContributors, string EffectId, string EffectName, string[] DominantModIds);

public sealed class SpectacleSystem
{
    private sealed class TowerState
    {
        public float Meter;
        public float SurgeCooldown;
        public float InactivityTime;
        public float Pulse;
        public float PulseHold;
        public string LoadoutSignature = string.Empty;
        public readonly Dictionary<string, float> ModCooldowns = new(StringComparer.Ordinal);

        public void Clear()
        {
            Meter = 0f;
            SurgeCooldown = 0f;
            InactivityTime = 0f;
            Pulse = 0f;
            PulseHold = 0f;
            ModCooldowns.Clear();
        }
    }

    private static readonly string[] CanonicalOrder =
    {
        SpectacleDefinitions.Momentum,
        SpectacleDefinitions.Overkill,
        SpectacleDefinitions.ExploitWeakness,
        SpectacleDefinitions.FocusLens,
        SpectacleDefinitions.ChillShot,
        SpectacleDefinitions.Overreach,
        SpectacleDefinitions.HairTrigger,
        SpectacleDefinitions.SplitShot,
        SpectacleDefinitions.FeedbackLoop,
        SpectacleDefinitions.ChainReaction,
        SpectacleDefinitions.BlastCore,
        SpectacleDefinitions.Wildfire,
        SpectacleDefinitions.Afterimage,
        SpectacleDefinitions.ReaperProtocol,
    };

    private readonly Dictionary<ITowerView, TowerState> _towerStates = new();
    // Cycle-accumulated scores: count how many times each primaryModId led a surge during the current global cycle.
    private readonly Dictionary<string, int> _globalCycleScores = new(StringComparer.Ordinal);
    private readonly HashSet<ITowerView> _globalCycleContributors = new();
    private float _time;
    private float _globalMeter;

    public event Action<SpectacleTriggerInfo>? OnSurgeTriggered;
    public event Action<GlobalSurgeTriggerInfo>? OnGlobalTriggered;
    /// <summary>Fires when the global meter fills and is waiting for player activation. Passes the resolved archetype label.</summary>
    public event Action<string>? OnGlobalSurgeReady;

    public float GlobalMeter => _globalMeter;

    /// <summary>
    /// Sets the global meter to a fraction of the current threshold (clamped 0–0.99).
    /// Used by the tutorial to pre-fill the meter so it fires naturally during play.
    /// </summary>
    public void SetGlobalMeterFraction(float fraction)
    {
        float threshold = SpectacleDefinitions.ResolveGlobalThreshold();
        _globalMeter = threshold * Math.Clamp(fraction, 0f, 0.99f);
    }

    /// <summary>True when the global meter is full and waiting for the player to activate it.</summary>
    public bool IsGlobalSurgeReady { get; private set; } = false;
    private GlobalSurgeTriggerInfo _pendingGlobalSurge;
    private bool _hasPendingGlobalSurge = false;

    /// <summary>
    /// Activates the pending global surge. Call when the player clicks the surge bar (or
    /// immediately for bot mode). Fires OnGlobalTriggered and resets the ready state.
    /// No-op if no pending surge.
    /// </summary>
    public void ActivateGlobalSurge()
    {
        if (!_hasPendingGlobalSurge) return;
        var info = _pendingGlobalSurge;
        _hasPendingGlobalSurge = false;
        IsGlobalSurgeReady = false;
        _globalMeter = info.MeterAfter;
        OnGlobalTriggered?.Invoke(info);
    }

    /// <summary>
    /// Returns the current dominant mod IDs from cycle-accumulated surge scores,
    /// without mutating any state. Safe to call every frame for preview purposes.
    /// </summary>
    public string[] PeekDominantMods() => ResolveDominantGlobalMods();

    public void Reset()
    {
        _towerStates.Clear();
        _globalCycleScores.Clear();
        _globalCycleContributors.Clear();
        _time = 0f;
        _globalMeter = 0f;
    }

    public void RemoveTower(ITowerView tower)
    {
        _towerStates.Remove(tower);
        _globalCycleContributors.Remove(tower);
    }

    public void Update(float delta)
    {
        if (delta <= 0f)
            return;

        _time += delta;

        foreach (var state in _towerStates.Values)
        {
            state.SurgeCooldown = Max(0f, state.SurgeCooldown - delta);
            if (state.PulseHold > 0f)
            {
                state.PulseHold = Max(0f, state.PulseHold - delta);
            }
            else
            {
                state.Pulse = Max(0f, state.Pulse - delta * 0.72f);
            }
            if (state.ModCooldowns.Count > 0)
            {
                var cdKeys = state.ModCooldowns.Keys.ToArray();
                foreach (string k in cdKeys)
                {
                    float next = state.ModCooldowns[k] - delta;
                    if (next <= 0f)
                        state.ModCooldowns.Remove(k);
                    else
                        state.ModCooldowns[k] = next;
                }
            }
            state.InactivityTime += delta;
            if (state.InactivityTime >= SpectacleDefinitions.ResolveInactivityGraceSeconds() && state.Meter > 0f)
                state.Meter = Max(0f, state.Meter - SpectacleDefinitions.ResolveInactivityDecayPerSecond() * delta);

        }

    }

    public SpectacleVisualState GetVisualState(ITowerView tower)
    {
        if (!_towerStates.TryGetValue(tower, out var state))
            return new SpectacleVisualState(0f, 0f, string.Empty);

        var signature = ResolveSignature(tower);
        return new SpectacleVisualState(
            MeterNormalized: Clamp(state.Meter / SpectacleDefinitions.ResolveSurgeThreshold(tower.TowerId), 0f, 1f),
            Pulse: state.Pulse,
            PrimaryModId: signature.PrimaryModId);
    }

    public SpectacleSignature PreviewSignature(ITowerView tower)
    {
        EnsureTowerState(tower);
        return ResolveSignature(tower);
    }

    public void RegisterShotFired(ITowerView tower)
    {
        if (tower == null) return;
        if (CountCopies(tower, SpectacleDefinitions.HairTrigger) <= 0)
            return;
        var state = EnsureTowerState(tower);
        float estimatedHitDamage = Max(1f, tower.GetEffectiveDamageForPreview());
        RegisterProcInternal(tower, state, SpectacleDefinitions.HairTrigger,
            SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.HairTrigger), estimatedHitDamage);
    }

    public void RegisterProc(ITowerView tower, string modifierId, float eventScalar, float eventDamage = -1f)
    {
        if (tower == null || !float.IsFinite(eventScalar) || eventScalar <= 0f) return;
        if (!float.IsFinite(eventDamage))
            eventDamage = -1f;
        string modId = SpectacleDefinitions.NormalizeModId(modifierId);
        if (!SpectacleDefinitions.IsSupported(modId))
            return;

        var state = EnsureTowerState(tower);
        RegisterProcInternal(tower, state, modId, eventScalar, eventDamage);
    }

    private void RegisterProcInternal(ITowerView tower, TowerState state, string modId, float eventScalar, float eventDamage)
    {
        if (!float.IsFinite(eventScalar) || eventScalar <= 0f)
            return;

        int copies = CountCopies(tower, modId);
        if (copies <= 0)
            return;

        // Per-mod-per-tower cooldown gate: prevents the same mod from contributing
        // more than once per ModProcCooldownSeconds on the same tower.
        if (state.ModCooldowns.TryGetValue(modId, out float remaining) && remaining > 0f)
            return;
        state.ModCooldowns[modId] = SpectacleDefinitions.ModProcCooldownSeconds;

        // Meter gain: base gain × event intensity × copy bonus × global scale × per-tower bias.
        float gain = SpectacleDefinitions.GetBaseGain(modId)
            * eventScalar
            * SpectacleDefinitions.GetCopyMultiplier(copies)
            * SpectacleDefinitions.ResolveMeterGainScale()
            * SpectacleDefinitions.ResolveTowerMeterGainMultiplier(tower.TowerId);

        if (!float.IsFinite(gain) || gain <= 0.0001f)
            return;

        state.InactivityTime = 0f;
        float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold(tower.TowerId);
        state.Meter = Clamp(state.Meter + gain, 0f, surgeThreshold);

        TryTriggerEvents(tower, state);
    }

    private void TryTriggerEvents(ITowerView tower, TowerState state)
    {
        float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold(tower.TowerId);
        float globalThreshold = SpectacleDefinitions.ResolveGlobalThreshold();
        if (state.Meter >= surgeThreshold && state.SurgeCooldown <= 0f)
        {
            var signature = ResolveSignature(tower);
            state.Meter = SpectacleDefinitions.ResolveSurgeMeterAfterTrigger();
            state.SurgeCooldown = SpectacleDefinitions.ResolveSurgeCooldownSeconds();
            state.Pulse = Max(state.Pulse, 1.0f);
            state.PulseHold = Max(state.PulseHold, 0.42f);

            _globalMeter = Clamp(_globalMeter + SpectacleDefinitions.ResolveGlobalMeterPerSurge(), 0f, globalThreshold);

            // Accumulate cycle scores: all equipped mods contribute equally to global feel.
            foreach (string modId in new[] { signature.PrimaryModId, signature.SecondaryModId, signature.TertiaryModId })
                if (!string.IsNullOrEmpty(modId))
                    _globalCycleScores[modId] = _globalCycleScores.GetValueOrDefault(modId, 0) + 1;
            _globalCycleContributors.Add(tower);

            OnSurgeTriggered?.Invoke(new SpectacleTriggerInfo(tower, IsSurge: true, signature, state.Meter));

            if (_globalMeter >= globalThreshold && !IsGlobalSurgeReady)
            {
                // Meter is full - arm for player activation rather than firing immediately.
                string[] dominantMods = ResolveDominantGlobalMods();
                _pendingGlobalSurge = new GlobalSurgeTriggerInfo(
                    MeterAfter: SpectacleDefinitions.ResolveGlobalMeterAfterTrigger(),
                    UniqueContributors: Math.Max(1, _globalCycleContributors.Count),
                    EffectId: "G_SPECTACLE_CATHARSIS",
                    EffectName: "Catastrophe",
                    DominantModIds: dominantMods);
                _hasPendingGlobalSurge = true;
                IsGlobalSurgeReady = true;
                OnGlobalSurgeReady?.Invoke(SurgeDifferentiation.ResolveLabel(dominantMods));
                // Reset cycle state so the next cycle starts fresh.
                _globalCycleScores.Clear();
                _globalCycleContributors.Clear();
            }

            return;
        }
    }

    private static SpectacleSignature ResolveSignature(ITowerView tower)
    {
        // All equipped mods contribute equally -- order by canonical rank only.
        var mods = tower.Modifiers
            .Select(m => SpectacleDefinitions.NormalizeModId(m.ModifierId))
            .Where(SpectacleDefinitions.IsSupported)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(CanonicalRank)
            .ThenBy(id => id, StringComparer.Ordinal)
            .ToList();

        if (mods.Count == 0)
        {
            return new SpectacleSignature(
                Mode: SpectacleMode.Single,
                PrimaryModId: string.Empty,
                SecondaryModId: string.Empty,
                TertiaryModId: string.Empty,
                SurgePower: 1f,
                EffectId: string.Empty,
                EffectName: string.Empty,
                ComboEffectId: string.Empty,
                ComboEffectName: string.Empty,
                AugmentEffectId: string.Empty,
                AugmentName: string.Empty);
        }

        string r1 = mods.Count > 0 ? mods[0] : string.Empty;
        string r2 = mods.Count > 1 ? mods[1] : string.Empty;
        string r3 = mods.Count > 2 ? mods[2] : string.Empty;

        SpectacleMode mode = mods.Count switch
        {
            <= 1 => SpectacleMode.Single,
            2 => SpectacleMode.Combo,
            _ => SpectacleMode.Triad,
        };

        float surgePower = SpectacleDefinitions.GetModeBase(mode);

        string effectId;
        string effectName;
        string comboId = string.Empty;
        string comboName = string.Empty;
        string augmentId = string.Empty;
        string augmentName = string.Empty;

        if (mode == SpectacleMode.Single)
        {
            var single = SpectacleDefinitions.GetSingle(r1);
            effectId = single.EffectId;
            effectName = single.Name;
        }
        else if (mode == SpectacleMode.Combo)
        {
            var combo = SpectacleDefinitions.GetCombo(r1, r2);
            effectId = combo.EffectId;
            effectName = combo.Name;
            comboId = combo.EffectId;
            comboName = combo.Name;
        }
        else
        {
            var combo = SpectacleDefinitions.GetCombo(r1, r2);
            var aug = SpectacleDefinitions.GetTriadAugment(r3);
            effectId = $"{combo.EffectId}+{aug.EffectId}";
            effectName = $"{combo.Name} + {aug.Name}";
            comboId = combo.EffectId;
            comboName = combo.Name;
            augmentId = aug.EffectId;
            augmentName = aug.Name;
        }

        return new SpectacleSignature(
            Mode: mode,
            PrimaryModId: r1,
            SecondaryModId: r2,
            TertiaryModId: r3,
            SurgePower: surgePower,
            EffectId: effectId,
            EffectName: effectName,
            ComboEffectId: comboId,
            ComboEffectName: comboName,
            AugmentEffectId: augmentId,
            AugmentName: augmentName);
    }

    private TowerState EnsureTowerState(ITowerView tower)
    {
        if (!_towerStates.TryGetValue(tower, out var state))
        {
            state = new TowerState();
            _towerStates[tower] = state;
        }

        string sig = BuildLoadoutSignature(tower);
        if (!string.Equals(sig, state.LoadoutSignature, StringComparison.Ordinal))
        {
            state.Clear();
            state.LoadoutSignature = sig;
        }

        return state;
    }

    private static string BuildLoadoutSignature(ITowerView tower)
    {
        if (tower.Modifiers.Count == 0)
            return string.Empty;

        var ids = tower.Modifiers
            .Select(m => SpectacleDefinitions.NormalizeModId(m.ModifierId))
            .Where(SpectacleDefinitions.IsSupported)
            .OrderBy(id => CanonicalRank(id))
            .ThenBy(id => id, StringComparer.Ordinal);
        return string.Join(",", ids);
    }

    private static int CanonicalRank(string modId)
    {
        for (int i = 0; i < CanonicalOrder.Length; i++)
        {
            if (string.Equals(CanonicalOrder[i], modId, StringComparison.Ordinal))
                return i;
        }
        return int.MaxValue;
    }

    private static int CountCopies(ITowerView tower, string modId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modId);
        int count = 0;
        foreach (var mod in tower.Modifiers)
        {
            if (string.Equals(SpectacleDefinitions.NormalizeModId(mod.ModifierId), normalized, StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns up to 3 mod IDs ordered by how frequently they led surges during the current global cycle.
    /// The archetype is determined by the full cycle pattern, not a narrow recent window.
    /// </summary>
    private string[] ResolveDominantGlobalMods()
    {
        if (_globalCycleScores.Count == 0)
            return System.Array.Empty<string>();
        return _globalCycleScores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => CanonicalRank(kv.Key))
            .Take(3)
            .Select(kv => kv.Key)
            .ToArray();
    }

    private static float Clamp(float value, float min, float max) => MathF.Min(max, MathF.Max(min, value));
    private static float Max(float a, float b) => a > b ? a : b;
}
