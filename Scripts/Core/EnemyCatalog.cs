using System;
using System.Collections.Generic;

namespace SlotTheory.Core;

public readonly record struct EnemyControlProfile(
    float UndertowPullMultiplier,
    float AccordionCompressionMultiplier);

public static class EnemyCatalog
{
    public const string BasicWalkerId = "basic_walker";
    public const string ArmoredWalkerId = "armored_walker";
    public const string SwiftWalkerId = "swift_walker";
    public const string SplitterWalkerId = "splitter_walker";
    public const string SplitterShardId = "splitter_shard";
    public const string ReverseWalkerId = "reverse_walker";
    public const string ShieldDroneId = "shield_drone";
    public const string AnchorWalkerId = "anchor_walker";
    public const string NullDroneId = "null_drone";
    public const string LancerWalkerId = "lancer_walker";
    public const string VeilWalkerId = "veil_walker";

    private static readonly string[] WaveSpecialEnemyIds =
    {
        ArmoredWalkerId,
        SwiftWalkerId,
        SplitterWalkerId,
        ReverseWalkerId,
        ShieldDroneId,
        AnchorWalkerId,
        NullDroneId,
        LancerWalkerId,
        VeilWalkerId,
    };

    private static readonly HashSet<string> FullGameOnlyIds = new(StringComparer.Ordinal)
    {
        ReverseWalkerId,
        ShieldDroneId,
        AnchorWalkerId,
        NullDroneId,
        LancerWalkerId,
        VeilWalkerId,
    };

    public static IReadOnlyList<string> GetWaveSpecialEnemyIds() => WaveSpecialEnemyIds;
    public static bool IsFullGameOnly(string enemyId) => FullGameOnlyIds.Contains(enemyId ?? string.Empty);

    public static string GetDisplayName(string enemyId) => enemyId switch
    {
        ArmoredWalkerId => "Armored Walker",
        SwiftWalkerId => "Swift Walker",
        SplitterWalkerId => "Splitter Walker",
        SplitterShardId => "Splitter Shard",
        ReverseWalkerId => "Reverse Walker",
        ShieldDroneId => "Shield Drone",
        AnchorWalkerId => "Anchor Walker",
        NullDroneId => "Null Drone",
        LancerWalkerId => "Lancer Walker",
        VeilWalkerId => "Veil Walker",
        _ => "Basic Walker",
    };

    public static int GetLeakCost(string enemyId) => enemyId switch
    {
        ArmoredWalkerId => 2,
        AnchorWalkerId => 2,
        SplitterWalkerId => 3,
        _ => 1,
    };

    public static float GetBaseSpeed(string enemyId) => enemyId switch
    {
        ArmoredWalkerId => Balance.TankyEnemySpeed,
        SwiftWalkerId => Balance.SwiftEnemySpeed,
        SplitterWalkerId => Balance.SplitterSpeed,
        SplitterShardId => Balance.SplitterShardSpeed,
        ReverseWalkerId => Balance.ReverseWalkerSpeed,
        ShieldDroneId => Balance.ShieldDroneSpeed,
        AnchorWalkerId => Balance.AnchorWalkerSpeed,
        NullDroneId => Balance.NullDroneSpeed,
        LancerWalkerId => Balance.LancerWalkerSpeed,
        VeilWalkerId => Balance.VeilWalkerSpeed,
        _ => Balance.BaseEnemySpeed,
    };

    public static float GetBaseHpMultiplier(string enemyId) => enemyId switch
    {
        ArmoredWalkerId => Balance.TankyHpMultiplier,
        SwiftWalkerId => Balance.SwiftHpMultiplier,
        SplitterWalkerId => Balance.SplitterHpMultiplier,
        SplitterShardId => Balance.SplitterShardHpMultiplier,
        ReverseWalkerId => Balance.ReverseWalkerHpMultiplier,
        ShieldDroneId => Balance.ShieldDroneHpMultiplier,
        AnchorWalkerId => Balance.AnchorWalkerHpMultiplier,
        NullDroneId => Balance.NullDroneHpMultiplier,
        LancerWalkerId => Balance.LancerWalkerHpMultiplier,
        VeilWalkerId => Balance.VeilWalkerHpMultiplier,
        _ => 1f,
    };

    public static EnemyControlProfile GetControlProfile(string enemyId) => enemyId switch
    {
        ArmoredWalkerId => new EnemyControlProfile(
            UndertowPullMultiplier: Balance.UndertowArmoredResistanceMultiplier,
            AccordionCompressionMultiplier: 1f),
        ReverseWalkerId or ShieldDroneId or SplitterWalkerId => new EnemyControlProfile(
            UndertowPullMultiplier: Balance.UndertowHeavyResistanceMultiplier,
            AccordionCompressionMultiplier: 1f),
        AnchorWalkerId => new EnemyControlProfile(
            UndertowPullMultiplier: Balance.UndertowAnchorResistanceMultiplier,
            AccordionCompressionMultiplier: Balance.AccordionAnchorCompressionMultiplier),
        _ => new EnemyControlProfile(
            UndertowPullMultiplier: 1f,
            AccordionCompressionMultiplier: 1f),
    };

    public static float ResolveLancerDashDistance(bool isPinned, bool isSlowed, float slowSpeedFactor)
    {
        if (isPinned)
            return 0f;

        float dash = 0.5f * (Balance.LancerWalkerDashDistanceMin + Balance.LancerWalkerDashDistanceMax);
        if (isSlowed && slowSpeedFactor <= Balance.LancerWalkerDashSuppressionThreshold)
            dash *= Balance.LancerWalkerDashSuppressedMultiplier;

        return dash >= Balance.LancerWalkerMinEffectiveDash ? dash : 0f;
    }

    public static bool TryConsumeVeilShell(ref bool shellActive, ref float refreshRemaining, ref float incomingDamage)
    {
        if (!shellActive || incomingDamage <= 0f)
            return false;

        incomingDamage *= (1f - Balance.VeilWalkerShellDamageReduction);
        shellActive = false;
        refreshRemaining = Balance.VeilWalkerShellRefreshDelay;
        return true;
    }

    public static void NotifyVeilHitTaken(ref bool shellActive, ref float refreshRemaining, float damageDealt)
    {
        if (damageDealt <= 0f)
            return;

        shellActive = false;
        refreshRemaining = Balance.VeilWalkerShellRefreshDelay;
    }

    public static void AdvanceVeilRefresh(ref bool shellActive, ref float refreshRemaining, float delta)
    {
        if (shellActive || refreshRemaining <= 0f || delta <= 0f)
            return;

        refreshRemaining = MathF.Max(0f, refreshRemaining - delta);
        if (refreshRemaining <= 0f)
            shellActive = true;
    }
}
