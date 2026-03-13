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
    float PrimaryShare,
    float SecondaryShare,
    float TertiaryShare,
    float SurgePower,
    float AugmentStrength,
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

public readonly record struct GlobalSurgeTriggerInfo(float MeterAfter, int UniqueContributors, string EffectId, string EffectName);

public sealed class SpectacleSystem
{
    private readonly struct ContributionSample
    {
        public float Time { get; }
        public string ModId { get; }
        public float Value { get; }

        public ContributionSample(float time, string modId, float value)
        {
            Time = time;
            ModId = modId;
            Value = value;
        }
    }

    private readonly struct SurgeContribution
    {
        public float Time { get; }
        public ITowerView Tower { get; }

        public SurgeContribution(float time, ITowerView tower)
        {
            Time = time;
            Tower = tower;
        }
    }

    private sealed class TowerState
    {
        public float Meter;
        public float SurgeCooldown;
        public float InactivityTime;
        public float Pulse;
        public float PulseHold;
        public string LoadoutSignature = string.Empty;
        public bool RolesLocked;
        public string LockedPrimary = string.Empty;
        public string LockedSecondary = string.Empty;
        public string LockedTertiary = string.Empty;
        public readonly Dictionary<string, float> Tokens = new(StringComparer.Ordinal);
        public readonly Dictionary<string, float> ContributionWindow = new(StringComparer.Ordinal);
        public readonly Queue<ContributionSample> ContributionSamples = new();
        public readonly Queue<float> ShotTimes = new();

        public void Clear()
        {
            Meter = 0f;
            SurgeCooldown = 0f;
            InactivityTime = 0f;
            Pulse = 0f;
            PulseHold = 0f;
            RolesLocked = false;
            LockedPrimary = string.Empty;
            LockedSecondary = string.Empty;
            LockedTertiary = string.Empty;
            Tokens.Clear();
            ContributionWindow.Clear();
            ContributionSamples.Clear();
            ShotTimes.Clear();
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
    };

    private readonly Dictionary<ITowerView, TowerState> _towerStates = new();
    private readonly Queue<SurgeContribution> _surgeContributions = new();
    private float _time;
    private float _globalMeter;

    public event Action<SpectacleTriggerInfo>? OnSurgeTriggered;
    public event Action<GlobalSurgeTriggerInfo>? OnGlobalTriggered;

    public float GlobalMeter => _globalMeter;

    public void Reset()
    {
        _towerStates.Clear();
        _surgeContributions.Clear();
        _time = 0f;
        _globalMeter = 0f;
    }

    public void RemoveTower(ITowerView tower)
    {
        _towerStates.Remove(tower);
        if (_surgeContributions.Count == 0)
            return;

        int count = _surgeContributions.Count;
        for (int i = 0; i < count; i++)
        {
            var contribution = _surgeContributions.Dequeue();
            if (!ReferenceEquals(contribution.Tower, tower))
                _surgeContributions.Enqueue(contribution);
        }
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
            RegenerateTokens(state, delta);
            state.InactivityTime += delta;
            if (state.InactivityTime >= SpectacleDefinitions.ResolveInactivityGraceSeconds() && state.Meter > 0f)
                state.Meter = Max(0f, state.Meter - SpectacleDefinitions.ResolveInactivityDecayPerSecond() * delta);

            PruneContributionWindow(state);
        }

        PruneGlobalContributions();
    }

    public SpectacleVisualState GetVisualState(ITowerView tower)
    {
        if (!_towerStates.TryGetValue(tower, out var state))
            return new SpectacleVisualState(0f, 0f, string.Empty);

        var signature = ResolveSignature(state, tower, useLockedRoles: true);
        return new SpectacleVisualState(
            MeterNormalized: Clamp(state.Meter / SpectacleDefinitions.ResolveSurgeThreshold(), 0f, 1f),
            Pulse: state.Pulse,
            PrimaryModId: signature.PrimaryModId);
    }

    public SpectacleSignature PreviewSignature(ITowerView tower)
    {
        var state = EnsureTowerState(tower);
        return ResolveSignature(state, tower, useLockedRoles: false);
    }

    public void RegisterShotFired(ITowerView tower)
    {
        if (tower == null) return;
        var state = EnsureTowerState(tower);
        EnsureTokenBuckets(state);

        state.ShotTimes.Enqueue(_time);
        while (state.ShotTimes.Count > 0 && state.ShotTimes.Peek() < _time - 1f)
            state.ShotTimes.Dequeue();

        if (CountCopies(tower, SpectacleDefinitions.HairTrigger) <= 0)
            return;

        float expectedShotsPerSecond = 1f / Max(0.001f, tower.AttackInterval);
        float streakNorm = Clamp(state.ShotTimes.Count / Max(0.001f, expectedShotsPerSecond), 0f, 2f);
        float eventScalar = SpectacleDefinitions.HairTriggerEventScalar(streakNorm);
        float estimatedHitDamage = Max(1f, tower.GetEffectiveDamageForPreview());
        RegisterProcInternal(tower, state, SpectacleDefinitions.HairTrigger, eventScalar, estimatedHitDamage);
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
        EnsureTokenBuckets(state);
        RegisterProcInternal(tower, state, modId, eventScalar, eventDamage);
    }

    private void RegisterProcInternal(ITowerView tower, TowerState state, string modId, float eventScalar, float eventDamage)
    {
        if (!float.IsFinite(eventScalar) || eventScalar <= 0f)
            return;
        if (!float.IsFinite(eventDamage))
            eventDamage = -1f;

        int copies = CountCopies(tower, modId);
        if (copies <= 0)
            return;

        int uniqueCount = CountUniqueSupportedMods(tower);
        if (uniqueCount <= 0)
            return;

        if (!state.Tokens.TryGetValue(modId, out float tokens))
            tokens = SpectacleDefinitions.GetTokenConfig(modId).Cap;

        float gate = Clamp(tokens, 0f, 1f);
        state.Tokens[modId] = Max(0f, tokens - 1f);

        if (gate <= 0.0001f)
            return;

        float gain = SpectacleDefinitions.GetBaseGain(modId)
            * eventScalar
            * SpectacleDefinitions.GetCopyMultiplier(copies)
            * gate
            * SpectacleDefinitions.GetDiversityMultiplier(uniqueCount)
            * SpectacleDefinitions.ResolveMeterGainScale()
            * SpectacleDefinitions.ResolveDamageMeterMultiplier(eventDamage);

        if (!float.IsFinite(gain) || gain <= 0.0001f)
            return;

        state.InactivityTime = 0f;
        state.Meter = Clamp(state.Meter + gain, 0f, SpectacleDefinitions.ResolveSurgeThreshold());
        AddContribution(state, modId, gain);

        if (!state.RolesLocked && state.Meter >= SpectacleDefinitions.ResolveRoleLockMeterThreshold())
            LockRoles(state, tower);

        TryTriggerEvents(tower, state);
    }

    private void TryTriggerEvents(ITowerView tower, TowerState state)
    {
        float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold();
        float globalThreshold = SpectacleDefinitions.ResolveGlobalThreshold();
        if (state.Meter >= surgeThreshold && state.SurgeCooldown <= 0f)
        {
            var signature = ResolveSignature(state, tower, useLockedRoles: true);
            state.Meter = SpectacleDefinitions.ResolveSurgeMeterAfterTrigger();
            state.SurgeCooldown = SpectacleDefinitions.ResolveSurgeCooldownSeconds();
            state.Pulse = Max(state.Pulse, 1.0f);
            state.PulseHold = Max(state.PulseHold, 0.42f);
            state.RolesLocked = false;
            state.LockedPrimary = string.Empty;
            state.LockedSecondary = string.Empty;
            state.LockedTertiary = string.Empty;

            _globalMeter = Clamp(_globalMeter + SpectacleDefinitions.ResolveGlobalMeterPerSurge(), 0f, globalThreshold);
            _surgeContributions.Enqueue(new SurgeContribution(_time, tower));
            PruneGlobalContributions();

            OnSurgeTriggered?.Invoke(new SpectacleTriggerInfo(tower, IsSurge: true, signature, state.Meter));

            if (_globalMeter >= globalThreshold)
            {
                // If the meter is full, fire on this surge instead of waiting on contributor gating.
                int uniqueContributors = Math.Max(1, CountUniqueGlobalContributors());
                _globalMeter = SpectacleDefinitions.ResolveGlobalMeterAfterTrigger();
                OnGlobalTriggered?.Invoke(new GlobalSurgeTriggerInfo(
                    MeterAfter: _globalMeter,
                    UniqueContributors: uniqueContributors,
                    EffectId: "G_SPECTACLE_CATHARSIS",
                    EffectName: "Catastrophe"));
            }

            return;
        }
    }

    private void LockRoles(TowerState state, ITowerView tower)
    {
        var signature = ResolveSignature(state, tower, useLockedRoles: false);
        if (string.IsNullOrEmpty(signature.PrimaryModId))
            return;

        state.RolesLocked = true;
        state.LockedPrimary = signature.PrimaryModId;
        state.LockedSecondary = signature.SecondaryModId;
        state.LockedTertiary = signature.TertiaryModId;
    }

    private SpectacleSignature ResolveSignature(TowerState state, ITowerView tower, bool useLockedRoles)
    {
        var counts = BuildCopyCountMap(tower);
        if (counts.Count == 0)
        {
            return new SpectacleSignature(
                Mode: SpectacleMode.Single,
                PrimaryModId: string.Empty,
                SecondaryModId: string.Empty,
                TertiaryModId: string.Empty,
                PrimaryShare: 0f,
                SecondaryShare: 0f,
                TertiaryShare: 0f,
                SurgePower: 1f,
                AugmentStrength: 0f,
                EffectId: string.Empty,
                EffectName: string.Empty,
                ComboEffectId: string.Empty,
                ComboEffectName: string.Empty,
                AugmentEffectId: string.Empty,
                AugmentName: string.Empty);
        }

        string r1;
        string r2;
        string r3;
        if (useLockedRoles && state.RolesLocked && !string.IsNullOrEmpty(state.LockedPrimary))
        {
            r1 = state.LockedPrimary;
            r2 = state.LockedSecondary;
            r3 = state.LockedTertiary;
        }
        else
        {
            var ranked = counts.Keys
                .OrderByDescending(modId => 100f * counts[modId] + 0.01f * state.ContributionWindow.GetValueOrDefault(modId, 0f))
                .ThenBy(modId => CanonicalRank(modId))
                .ThenBy(modId => modId, StringComparer.Ordinal)
                .ToList();

            r1 = ranked.Count > 0 ? ranked[0] : string.Empty;
            r2 = ranked.Count > 1 ? ranked[1] : string.Empty;
            r3 = ranked.Count > 2 ? ranked[2] : string.Empty;
        }

        int unique = counts.Count;
        int n = tower.Modifiers.Count;
        if (n <= 0) n = 1;

        float w1 = counts.TryGetValue(r1, out int c1) ? c1 / (float)n : 0f;
        float w2 = counts.TryGetValue(r2, out int c2) ? c2 / (float)n : 0f;
        float w3 = counts.TryGetValue(r3, out int c3) ? c3 / (float)n : 0f;

        SpectacleMode mode = unique switch
        {
            <= 1 => SpectacleMode.Single,
            2 => SpectacleMode.Combo,
            _ => SpectacleMode.Triad,
        };

        float copyBoost = 1f;
        if (!string.IsNullOrEmpty(r1) && counts.TryGetValue(r1, out int count1))
            copyBoost += 0.28f * (count1 - 1);
        if (!string.IsNullOrEmpty(r2) && counts.TryGetValue(r2, out int count2))
            copyBoost += 0.16f * (count2 - 1);
        if (!string.IsNullOrEmpty(r3) && counts.TryGetValue(r3, out int count3))
            copyBoost += 0.10f * (count3 - 1);

        float surgePower = SpectacleDefinitions.GetModeBase(mode) * copyBoost;
        float augmentStrength = mode == SpectacleMode.Triad ? w3 * surgePower : 0f;

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
            PrimaryShare: w1,
            SecondaryShare: w2,
            TertiaryShare: w3,
            SurgePower: surgePower,
            AugmentStrength: augmentStrength,
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
            EnsureTokenBuckets(state);
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

    private static Dictionary<string, int> BuildCopyCountMap(ITowerView tower)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var mod in tower.Modifiers)
        {
            string id = SpectacleDefinitions.NormalizeModId(mod.ModifierId);
            if (!SpectacleDefinitions.IsSupported(id))
                continue;

            map.TryGetValue(id, out int count);
            map[id] = count + 1;
        }
        return map;
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

    private static int CountUniqueSupportedMods(ITowerView tower)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mod in tower.Modifiers)
        {
            string id = SpectacleDefinitions.NormalizeModId(mod.ModifierId);
            if (SpectacleDefinitions.IsSupported(id))
                set.Add(id);
        }
        return set.Count;
    }

    private static void EnsureTokenBuckets(TowerState state)
    {
        foreach (string modId in SpectacleDefinitions.SupportedModIds)
        {
            if (!state.Tokens.ContainsKey(modId))
                state.Tokens[modId] = SpectacleDefinitions.GetTokenConfig(modId).Cap;
        }
    }

    private static void RegenerateTokens(TowerState state, float delta)
    {
        var ids = state.Tokens.Keys.ToArray();
        foreach (string modId in ids)
        {
            var cfg = SpectacleDefinitions.GetTokenConfig(modId);
            float next = state.Tokens[modId] + cfg.RegenPerSecond * delta;
            state.Tokens[modId] = Clamp(next, 0f, cfg.Cap);
        }
    }

    private void AddContribution(TowerState state, string modId, float value)
    {
        state.ContributionSamples.Enqueue(new ContributionSample(_time, modId, value));
        state.ContributionWindow.TryGetValue(modId, out float current);
        state.ContributionWindow[modId] = current + value;
        PruneContributionWindow(state);
    }

    private void PruneContributionWindow(TowerState state)
    {
        float cutoff = _time - SpectacleDefinitions.ResolveContributionWindowSeconds();
        while (state.ContributionSamples.Count > 0 && state.ContributionSamples.Peek().Time < cutoff)
        {
            var sample = state.ContributionSamples.Dequeue();
            if (!state.ContributionWindow.TryGetValue(sample.ModId, out float current))
                continue;

            float next = current - sample.Value;
            if (next <= 0.0001f)
                state.ContributionWindow.Remove(sample.ModId);
            else
                state.ContributionWindow[sample.ModId] = next;
        }
    }

    private void PruneGlobalContributions()
    {
        float cutoff = _time - SpectacleDefinitions.ResolveGlobalContributionWindowSeconds();
        while (_surgeContributions.Count > 0 && _surgeContributions.Peek().Time < cutoff)
            _surgeContributions.Dequeue();
    }

    private int CountUniqueGlobalContributors()
    {
        if (_surgeContributions.Count == 0)
            return 0;

        var set = new HashSet<ITowerView>();
        foreach (var c in _surgeContributions)
            set.Add(c.Tower);
        return set.Count;
    }

    private static float Clamp(float value, float min, float max) => MathF.Min(max, MathF.Max(min, value));
    private static float Max(float a, float b) => a > b ? a : b;
}
