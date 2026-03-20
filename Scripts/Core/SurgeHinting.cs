using System;

namespace SlotTheory.Core;

public enum SurgeHintId
{
    CombatFills,
    TowerReady,
    GlobalContribution,
    GlobalActivate,
    ComboUnlock
}

public sealed class SurgeHintRunTelemetry
{
    public int TowersSurged { get; set; }
    public int GlobalsBecameReady { get; set; }
    public int GlobalsActivated { get; set; }
    public float GlobalReadyUnusedSeconds { get; set; }
    public bool LostWithGlobalReadyUnused { get; set; }
    public int ComboTowersBuiltThisRun { get; set; }
    public int ComboTowerSurgesThisRun { get; set; }
    public int QuickGlobalActivationsWithin10s { get; set; }

    public void Reset()
    {
        TowersSurged = 0;
        GlobalsBecameReady = 0;
        GlobalsActivated = 0;
        GlobalReadyUnusedSeconds = 0f;
        LostWithGlobalReadyUnused = false;
        ComboTowersBuiltThisRun = 0;
        ComboTowerSurgesThisRun = 0;
        QuickGlobalActivationsWithin10s = 0;
    }
}

public sealed class SurgeHintProfileState
{
    public int CombatFillsLifetimeShows { get; set; }
    public int TowerReadyLifetimeShows { get; set; }
    public int GlobalContributionLifetimeShows { get; set; }
    public int GlobalActivateLifetimeShows { get; set; }
    public int ComboUnlockLifetimeShows { get; set; }

    public bool CombatFillsRetired { get; set; }
    public bool TowerReadyRetired { get; set; }
    public bool GlobalContributionRetired { get; set; }
    public bool GlobalActivateRetired { get; set; }
    public bool ComboUnlockRetired { get; set; }

    public int GlobalActivationsTotal { get; set; }
    public int GlobalActivationRuns { get; set; }
    public int WinsWithGlobalActivation { get; set; }
    public int ComboTowersBuiltTotal { get; set; }
    public int QuickGlobalActivationsTotal { get; set; }

    public string LastPostLossTipId { get; set; } = string.Empty;
    public int LastPostLossTipRepeatCount { get; set; }

    public int GetLifetimeShows(SurgeHintId id) => id switch
    {
        SurgeHintId.CombatFills => CombatFillsLifetimeShows,
        SurgeHintId.TowerReady => TowerReadyLifetimeShows,
        SurgeHintId.GlobalContribution => GlobalContributionLifetimeShows,
        SurgeHintId.GlobalActivate => GlobalActivateLifetimeShows,
        SurgeHintId.ComboUnlock => ComboUnlockLifetimeShows,
        _ => 0
    };

    public bool IsRetired(SurgeHintId id) => id switch
    {
        SurgeHintId.CombatFills => CombatFillsRetired,
        SurgeHintId.TowerReady => TowerReadyRetired,
        SurgeHintId.GlobalContribution => GlobalContributionRetired,
        SurgeHintId.GlobalActivate => GlobalActivateRetired,
        SurgeHintId.ComboUnlock => ComboUnlockRetired,
        _ => false
    };

    public void IncrementLifetimeShows(SurgeHintId id)
    {
        switch (id)
        {
            case SurgeHintId.CombatFills:
                CombatFillsLifetimeShows++;
                break;
            case SurgeHintId.TowerReady:
                TowerReadyLifetimeShows++;
                break;
            case SurgeHintId.GlobalContribution:
                GlobalContributionLifetimeShows++;
                break;
            case SurgeHintId.GlobalActivate:
                GlobalActivateLifetimeShows++;
                break;
            case SurgeHintId.ComboUnlock:
                ComboUnlockLifetimeShows++;
                break;
        }
    }

    public void SetRetired(SurgeHintId id, bool retired)
    {
        switch (id)
        {
            case SurgeHintId.CombatFills:
                CombatFillsRetired = retired;
                break;
            case SurgeHintId.TowerReady:
                TowerReadyRetired = retired;
                break;
            case SurgeHintId.GlobalContribution:
                GlobalContributionRetired = retired;
                break;
            case SurgeHintId.GlobalActivate:
                GlobalActivateRetired = retired;
                break;
            case SurgeHintId.ComboUnlock:
                ComboUnlockRetired = retired;
                break;
        }
    }

    public void Reset()
    {
        CombatFillsLifetimeShows = 0;
        TowerReadyLifetimeShows = 0;
        GlobalContributionLifetimeShows = 0;
        GlobalActivateLifetimeShows = 0;
        ComboUnlockLifetimeShows = 0;
        CombatFillsRetired = false;
        TowerReadyRetired = false;
        GlobalContributionRetired = false;
        GlobalActivateRetired = false;
        ComboUnlockRetired = false;
        GlobalActivationsTotal = 0;
        GlobalActivationRuns = 0;
        WinsWithGlobalActivation = 0;
        ComboTowersBuiltTotal = 0;
        QuickGlobalActivationsTotal = 0;
        LastPostLossTipId = string.Empty;
        LastPostLossTipRepeatCount = 0;
    }
}

public sealed class SurgeHintRuntimeState
{
    private readonly System.Collections.Generic.Dictionary<SurgeHintId, int> _runShows = new();
    private readonly System.Collections.Generic.Dictionary<SurgeHintId, float> _nextAllowedTime = new();

    public int GetRunShows(SurgeHintId id) => _runShows.TryGetValue(id, out int count) ? count : 0;

    public float GetNextAllowedTime(SurgeHintId id) => _nextAllowedTime.TryGetValue(id, out float nextAt) ? nextAt : 0f;

    public bool CanShow(SurgeHintId id, float playTime, int runCap)
    {
        if (GetRunShows(id) >= Math.Max(1, runCap))
            return false;
        return playTime >= GetNextAllowedTime(id);
    }

    public void MarkShown(SurgeHintId id, float playTime, float cooldownSeconds)
    {
        _runShows[id] = GetRunShows(id) + 1;
        _nextAllowedTime[id] = Math.Max(0f, playTime) + Math.Max(0f, cooldownSeconds);
    }

    public void Reset()
    {
        _runShows.Clear();
        _nextAllowedTime.Clear();
    }
}

public readonly record struct SurgePostLossTip(string Id, string Text);

public static class SurgeHintAdvisor
{
    public static int GetLifetimeCap(SurgeHintId id) => id switch
    {
        SurgeHintId.CombatFills => 10,
        SurgeHintId.TowerReady => 14,
        SurgeHintId.GlobalContribution => 12,
        SurgeHintId.GlobalActivate => 10,
        SurgeHintId.ComboUnlock => 8,
        _ => 1
    };

    public static int GetRunCap(SurgeHintId id) => id switch
    {
        SurgeHintId.CombatFills => 1,
        SurgeHintId.TowerReady => 2,
        SurgeHintId.GlobalContribution => 2,
        SurgeHintId.GlobalActivate => 2,
        SurgeHintId.ComboUnlock => 1,
        _ => 1
    };

    public static float GetRunCooldownSeconds(SurgeHintId id) => id switch
    {
        SurgeHintId.CombatFills => 18f,
        SurgeHintId.TowerReady => 10f,
        SurgeHintId.GlobalContribution => 10f,
        SurgeHintId.GlobalActivate => 12f,
        SurgeHintId.ComboUnlock => 20f,
        _ => 12f
    };

    public static bool ShouldShowMicroHint(
        SurgeHintId id,
        SurgeHintProfileState profile,
        SurgeHintRuntimeState runtime,
        float playTime)
    {
        if (profile.IsRetired(id))
        {
            // Cap migration: when caps increase, allow previously cap-retired hints
            // to resume if they are still below the new ceiling.
            int shown = profile.GetLifetimeShows(id);
            if (shown > 0 && shown < GetLifetimeCap(id))
                profile.SetRetired(id, false);
            else
                return false;
        }

        if (profile.GetLifetimeShows(id) >= GetLifetimeCap(id))
        {
            profile.SetRetired(id, true);
            return false;
        }

        return runtime.CanShow(id, playTime, GetRunCap(id));
    }

    public static void RecordMicroHintShown(
        SurgeHintId id,
        SurgeHintProfileState profile,
        SurgeHintRuntimeState runtime,
        float playTime)
    {
        profile.IncrementLifetimeShows(id);
        runtime.MarkShown(id, playTime, GetRunCooldownSeconds(id));

        if (profile.GetLifetimeShows(id) >= GetLifetimeCap(id))
            profile.SetRetired(id, true);
    }

    public static void ApplyRunOutcome(SurgeHintProfileState profile, SurgeHintRunTelemetry run, bool won)
    {
        if (run.GlobalsActivated > 0)
        {
            profile.GlobalActivationsTotal += run.GlobalsActivated;
            profile.GlobalActivationRuns += 1;
            if (won)
                profile.WinsWithGlobalActivation += 1;
        }

        profile.ComboTowersBuiltTotal += run.ComboTowersBuiltThisRun;
        profile.QuickGlobalActivationsTotal += run.QuickGlobalActivationsWithin10s;

        if (profile.GlobalActivationsTotal >= 2 || profile.WinsWithGlobalActivation >= 1)
        {
            profile.SetRetired(SurgeHintId.CombatFills, true);
            profile.SetRetired(SurgeHintId.GlobalContribution, true);
        }

        if (profile.GlobalActivationsTotal >= 2 || profile.QuickGlobalActivationsTotal >= 2 || profile.WinsWithGlobalActivation >= 1)
            profile.SetRetired(SurgeHintId.TowerReady, true);

        if (profile.GlobalActivationsTotal >= 2 && profile.GlobalActivationRuns >= 2)
            profile.SetRetired(SurgeHintId.GlobalActivate, true);

        if (profile.ComboTowersBuiltTotal >= 3 || run.ComboTowerSurgesThisRun >= 2)
            profile.SetRetired(SurgeHintId.ComboUnlock, true);

        foreach (SurgeHintId id in Enum.GetValues<SurgeHintId>())
        {
            if (profile.GetLifetimeShows(id) >= GetLifetimeCap(id))
                profile.SetRetired(id, true);
        }
    }

    public static SurgePostLossTip? SelectPostLossTip(SurgeHintRunTelemetry run, SurgeHintProfileState profile)
    {
        var candidates = new[]
        {
            new SurgePostLossTip(
                "global_unused",
                "Tip: Global Surge was ready this run - click the bar to trigger it."),
            new SurgePostLossTip(
                "full_towers_power",
                "Tip: Tower surges fill your Global Surge bar."),
            new SurgePostLossTip(
                "combat_fills",
                "Tip: Combat fills each tower's Surge ring."),
            new SurgePostLossTip(
                "combo_unlock",
                "Tip: 2+ mods unlock stronger combo surges."),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            bool relevant = candidates[i].Id switch
            {
                "global_unused" => run.LostWithGlobalReadyUnused
                    || (run.GlobalsBecameReady > run.GlobalsActivated && run.GlobalReadyUnusedSeconds >= 3f),
                "full_towers_power" => run.TowersSurged >= 2 && run.GlobalsActivated == 0,
                "combat_fills" => run.TowersSurged <= 0,
                "combo_unlock" => run.ComboTowersBuiltThisRun <= 0 && profile.ComboTowersBuiltTotal <= 0,
                _ => false
            };

            if (!relevant)
                continue;

            bool repeatedTooMuch = string.Equals(profile.LastPostLossTipId, candidates[i].Id, StringComparison.Ordinal)
                && profile.LastPostLossTipRepeatCount >= 2;
            if (repeatedTooMuch)
                continue;

            return candidates[i];
        }

        return null;
    }

    public static void RecordPostLossTipDisplayed(SurgeHintProfileState profile, string tipId)
    {
        if (string.IsNullOrWhiteSpace(tipId))
            return;

        if (string.Equals(profile.LastPostLossTipId, tipId, StringComparison.Ordinal))
        {
            profile.LastPostLossTipRepeatCount = Math.Clamp(profile.LastPostLossTipRepeatCount + 1, 1, 99);
        }
        else
        {
            profile.LastPostLossTipId = tipId;
            profile.LastPostLossTipRepeatCount = 1;
        }
    }
}
