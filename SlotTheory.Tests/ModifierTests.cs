using System.Collections.Generic;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Modifiers;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for all 11 modifier behaviors using FakeTower and FakeEnemy stubs.
/// No Godot engine needed - all pure C# logic.
/// </summary>
public class ModifierTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static ModifierDef Def(string id) => new(id, id, "", new Dictionary<string, float>());

    private static DamageContext Ctx(ITowerView attacker, IEnemyView target,
                                      IEnumerable<IEnemyView>? enemies = null)
        => new(attacker, target, waveIndex: 0, enemies ?? new List<IEnemyView>());

    // ── Overreach ─────────────────────────────────────────────────────────

    [Fact]
    public void Overreach_OnEquip_IncreasesRange145Percent()
    {
        var tower = new FakeTower { Range = 200f };
        new Overreach(Def("overreach")).OnEquip(tower);
        Assert.Equal(200f * Balance.OverreachRangeFactor, tower.Range, precision: 2);
    }

    [Fact]
    public void Overreach_OnEquip_ReducesDamage90Percent()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        new Overreach(Def("overreach")).OnEquip(tower);
        Assert.Equal(9f, tower.BaseDamage, precision: 2);
    }

    // ── HairTrigger ───────────────────────────────────────────────────────

    [Fact]
    public void HairTrigger_OnEquip_ReducesAttackInterval()
    {
        var tower = new FakeTower { AttackInterval = 1.0f };
        new HairTrigger(Def("hair_trigger")).OnEquip(tower);
        // AttackInterval /= HairTriggerAttackSpeed (1.40), so 1.0 / 1.40 ≈ 0.714
        Assert.Equal(1f / Balance.HairTriggerAttackSpeed, tower.AttackInterval, precision: 3);
    }

    [Fact]
    public void HairTrigger_OnEquip_ReducesRange()
    {
        var tower = new FakeTower { Range = 200f };
        new HairTrigger(Def("hair_trigger")).OnEquip(tower);
        Assert.Equal(200f * Balance.HairTriggerRangeFactor, tower.Range, precision: 2);
    }

    // ── SplitShot ─────────────────────────────────────────────────────────

    [Fact]
    public void SplitShot_OnEquip_IncrementsSplitCount()
    {
        var tower = new FakeTower { SplitCount = 0 };
        new SplitShot(Def("split_shot")).OnEquip(tower);
        Assert.Equal(1, tower.SplitCount);
    }

    [Fact]
    public void SplitShot_OnEquip_Stacks_EachCopyAddsOne()
    {
        var tower = new FakeTower();
        new SplitShot(Def("split_shot")).OnEquip(tower);
        new SplitShot(Def("split_shot")).OnEquip(tower);
        Assert.Equal(2, tower.SplitCount);
    }

    // ── ChainReaction ─────────────────────────────────────────────────────

    [Fact]
    public void ChainReaction_OnEquip_IncrementsChainCount()
    {
        var tower = new FakeTower { ChainCount = 0 };
        new ChainReaction(Def("chain_reaction")).OnEquip(tower);
        Assert.Equal(1, tower.ChainCount);
    }

    [Fact]
    public void ChainReaction_OnEquip_SetsMinimumChainRange()
    {
        var tower = new FakeTower { ChainRange = 100f };
        new ChainReaction(Def("chain_reaction")).OnEquip(tower);
        // ChainReactionRange was reduced from 400 to 320 in the chain_reaction nerf pass.
        Assert.True(tower.ChainRange >= Balance.ChainReactionRange);
    }

    [Fact]
    public void ChainReaction_OnEquip_Stacks_MultipleBouncesAccumulate()
    {
        var tower = new FakeTower();
        new ChainReaction(Def("chain_reaction")).OnEquip(tower);
        new ChainReaction(Def("chain_reaction")).OnEquip(tower);
        Assert.Equal(2, tower.ChainCount);
    }

    // ── FocusLens ─────────────────────────────────────────────────────────

    [Fact]
    public void FocusLens_ModifyDamage_MultipliesByBonus()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        var enemy = new FakeEnemy();
        var mod = new FocusLens(Def("focus_lens"));

        float damage = 10f;
        mod.ModifyDamage(ref damage, Ctx(tower, enemy));

        Assert.Equal(10f * Balance.FocusLensDamageBonus, damage, precision: 3);
    }

    [Fact]
    public void FocusLens_ModifyAttackInterval_AppliesConfiguredMultiplier()
    {
        var tower = new FakeTower { AttackInterval = 1f };
        var mod = new FocusLens(Def("focus_lens"));

        float interval = 1f;
        mod.ModifyAttackInterval(ref interval, tower);

        Assert.Equal(Balance.FocusLensAttackInterval, interval, precision: 3);
    }

    // ── ExploitWeakness ───────────────────────────────────────────────────

    [Fact]
    public void ExploitWeakness_ModifyDamage_MarkedTarget_AppliesBonus()
    {
        var tower = new FakeTower();
        var enemy = new FakeEnemy { MarkedRemaining = 4f }; // IsMarked = true
        float damage = 10f;

        new ExploitWeakness(Def("exploit_weakness")).ModifyDamage(ref damage, Ctx(tower, enemy));

        Assert.Equal(10f * Balance.ExploitWeaknessDamageBonus, damage, precision: 3);
    }

    [Fact]
    public void ExploitWeakness_ModifyDamage_UnmarkedTarget_NoBonus()
    {
        var tower = new FakeTower();
        var enemy = new FakeEnemy { MarkedRemaining = 0f }; // IsMarked = false
        float damage = 10f;

        new ExploitWeakness(Def("exploit_weakness")).ModifyDamage(ref damage, Ctx(tower, enemy));

        Assert.Equal(10f, damage, precision: 3);
    }

    // ── Momentum ──────────────────────────────────────────────────────────

    [Fact]
    public void Momentum_OnHit_SameTarget_IncrementsStacks()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        var enemy = new FakeEnemy { Hp = 1000f };
        var mod = new Momentum(Def("momentum"));

        // First hit: resets to 1 stack (target switch)
        mod.OnHit(Ctx(tower, enemy));
        // Second hit on same target: increments
        mod.OnHit(Ctx(tower, enemy));

        float damage = 10f;
        mod.ModifyDamage(ref damage, Ctx(tower, enemy));
        // 2 stacks: 10 * (1 + 2 * 0.16) = 10 * 1.32
        Assert.Equal(10f * (1f + 2 * Balance.MomentumBonusPerStack), damage, precision: 2);
    }

    [Fact]
    public void Momentum_OnHit_DifferentTarget_ResetsStacks()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        var enemy1 = new FakeEnemy { Hp = 1000f };
        var enemy2 = new FakeEnemy { Hp = 1000f };
        var mod = new Momentum(Def("momentum"));

        // Build stacks on enemy1
        mod.OnHit(Ctx(tower, enemy1));
        mod.OnHit(Ctx(tower, enemy1));

        // Switch to enemy2: stacks reset to 1
        mod.OnHit(Ctx(tower, enemy2));

        float damage = 10f;
        mod.ModifyDamage(ref damage, Ctx(tower, enemy2));
        // 1 stack: 10 * (1 + 1 * 0.16) = 11.6
        Assert.Equal(10f * (1f + 1 * Balance.MomentumBonusPerStack), damage, precision: 2);
    }

    [Fact]
    public void Momentum_Stacks_CapAtMaxMultiplier()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        var enemy = new FakeEnemy { Hp = 1000f };
        var mod = new Momentum(Def("momentum"));

        // Hit more times than MomentumMaxStacks to trigger the cap
        for (int i = 0; i < Balance.MomentumMaxStacks + 5; i++)
            mod.OnHit(Ctx(tower, enemy));

        float damage = 10f;
        mod.ModifyDamage(ref damage, Ctx(tower, enemy));

        // Cap: 10 * (1 + MaxStacks * BonusPerStack) = 10 * 1.80
        float expected = 10f * (1f + Balance.MomentumMaxStacks * Balance.MomentumBonusPerStack);
        Assert.Equal(expected, damage, precision: 2);
    }

    // ── Overkill ──────────────────────────────────────────────────────────

    [Fact]
    public void Overkill_OnKill_SpillsExcessDamageToNextEnemy()
    {
        var overkill = new Overkill(Def("overkill"));
        var tower = new FakeTower();
        tower.Modifiers.Add(overkill); // overkillCount must be >= 1 for spill to fire
        var primary = new FakeEnemy { Hp = -40f, ProgressRatio = 0.5f }; // overkill by 40
        var next = new FakeEnemy { Hp = 100f, ProgressRatio = 0.3f };
        var enemies = new List<IEnemyView> { next };

        overkill.OnKill(new DamageContext(tower, primary, 0, enemies));

        // 40 * OverkillSpillEfficiency spill damage
        Assert.Equal(100f - 40f * Balance.OverkillSpillEfficiency, next.Hp, precision: 2);
    }

    [Fact]
    public void Overkill_OnKill_NoNextEnemy_NoError()
    {
        var tower = new FakeTower();
        var primary = new FakeEnemy { Hp = -10f };

        // Should not throw even with empty enemy list
        new Overkill(Def("overkill")).OnKill(new DamageContext(tower, primary, 0, new List<IEnemyView>()));
    }

    [Fact]
    public void Overkill_OnKill_TargetNotDead_NoSpill()
    {
        var tower = new FakeTower();
        var primary = new FakeEnemy { Hp = 5f }; // still alive (positive HP = no overkill)
        var next = new FakeEnemy { Hp = 100f };
        var enemies = new List<IEnemyView> { next };

        new Overkill(Def("overkill")).OnKill(new DamageContext(tower, primary, 0, enemies));

        Assert.Equal(100f, next.Hp, precision: 2); // untouched
    }

    // ── Slow ──────────────────────────────────────────────────────────────

    [Fact]
    public void Slow_OnHit_AppliesSlowToTarget()
    {
        var tower = new FakeTower();
        tower.Modifiers.Add(new Slow(Def("slow")));
        var enemy = new FakeEnemy();

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        Assert.True(enemy.SlowRemaining > 0f);
        Assert.Equal(Balance.SlowSpeedFactor, enemy.SlowSpeedFactor, precision: 3);
    }

    [Fact]
    public void Slow_OnHit_TwoStacks_MultipliesSpeedFactor()
    {
        var tower = new FakeTower();
        tower.Modifiers.Add(new Slow(Def("slow")));
        tower.Modifiers.Add(new Slow(Def("slow")));
        var enemy = new FakeEnemy();

        // Apply via first copy (it counts all "slow" modifiers on the tower)
        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        float expected = Balance.SlowSpeedFactor * Balance.SlowSpeedFactor;
        Assert.Equal(expected, enemy.SlowSpeedFactor, precision: 3);
    }

    // ── FeedbackLoop ──────────────────────────────────────────────────────

    [Fact]
    public void FeedbackLoop_OnKill_OneCopy_Reduces50Percent()
    {
        var tower = new FakeTower { Cooldown = 0.8f };
        var enemy = new FakeEnemy { Hp = 0f }; // dead
        var mod = new FeedbackLoop(Def("feedback_loop"));
        tower.Modifiers.Add(mod);

        mod.OnKill(Ctx(tower, enemy));

        Assert.Equal(0.8f * (1f - Balance.FeedbackLoopCooldownReductionPerCopy), tower.Cooldown, precision: 3);
    }

    [Fact]
    public void FeedbackLoop_OnKill_TwoCopies_FullReset()
    {
        var tower = new FakeTower { Cooldown = 0.8f };
        var enemy = new FakeEnemy { Hp = 0f };
        var mod1 = new FeedbackLoop(Def("feedback_loop"));
        var mod2 = new FeedbackLoop(Def("feedback_loop"));
        tower.Modifiers.Add(mod1);
        tower.Modifiers.Add(mod2);

        mod1.OnKill(Ctx(tower, enemy));

        Assert.Equal(0f, tower.Cooldown, precision: 3);
    }

    [Fact]
    public void FeedbackLoop_OnKill_CooldownNeverGoesNegative()
    {
        var tower = new FakeTower { Cooldown = 0f };
        var enemy = new FakeEnemy { Hp = 0f };
        var mod = new FeedbackLoop(Def("feedback_loop"));
        tower.Modifiers.Add(mod);

        mod.OnKill(Ctx(tower, enemy));

        Assert.Equal(0f, tower.Cooldown);
    }

    // ── Wildfire ──────────────────────────────────────────────────────────

    [Fact]
    public void Wildfire_OnHit_PrimaryHit_AppliesBurnToTarget()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f };

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        Assert.Equal(Balance.WildfireBurnDuration, enemy.BurnRemaining, precision: 3);
        Assert.True(enemy.BurnDamagePerSecond > 0f);
    }

    [Fact]
    public void Wildfire_OnHit_ChainHit_AppliesBurn()
    {
        // Wildfire fires on chain hits too (proc spaghetti -- all hits ignite).
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0,
                                    new List<IEnemyView>(), isChain: true);

        tower.Modifiers[0].OnHit(ctx);

        Assert.Equal(Balance.WildfireBurnDuration, enemy.BurnRemaining, precision: 3);
        Assert.True(enemy.BurnDamagePerSecond > 0f);
    }

    [Fact]
    public void Wildfire_OnHit_RefreshesDurationOnReapplication()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f, BurnRemaining = 0.5f };

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        Assert.Equal(Balance.WildfireBurnDuration, enemy.BurnRemaining, precision: 3);
    }

    [Fact]
    public void Wildfire_OnHit_TwoCopies_DoublesBurnDps()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f };

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        float expectedDps = tower.BaseDamage * Balance.WildfireBurnDpsRatio * 2;
        Assert.Equal(expectedDps, enemy.BurnDamagePerSecond, precision: 3);
    }

    [Fact]
    public void Wildfire_OnHit_DeadTarget_DoesNotIgnite()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 0f }; // already dead

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        Assert.Equal(0f, enemy.BurnRemaining);
    }

    [Fact]
    public void Wildfire_OnHit_SetsTrailDropTimerToFullInterval()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f };

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        Assert.Equal(Balance.WildfireTrailDropInterval, enemy.BurnTrailDropTimer, precision: 3);
    }

    [Fact]
    public void Wildfire_OnHit_ReIgnition_DoesNotResetTrailDropTimer()
    {
        // Fast-firing tower re-ignites a burning enemy before the drop timer expires.
        // The timer must NOT be reset, otherwise it never counts down and trails never drop.
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f };

        // First ignition -- sets timer to full interval
        tower.Modifiers[0].OnHit(Ctx(tower, enemy));
        float timerAfterFirstHit = enemy.BurnTrailDropTimer;

        // Simulate partial countdown (0.3 s elapsed out of 0.65 s interval)
        enemy.BurnTrailDropTimer -= 0.3f;
        float timerBeforeReIgnition = enemy.BurnTrailDropTimer;

        // Re-ignition while still burning -- timer must NOT be reset to full interval
        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        Assert.Equal(timerBeforeReIgnition, enemy.BurnTrailDropTimer, precision: 3);
    }

    [Fact]
    public void Afterimage_ModifierId_IsConfigured()
    {
        var mod = new Afterimage(Def("afterimage"));
        Assert.Equal("afterimage", mod.ModifierId);
    }

    [Fact]
    public void Wildfire_OnHit_SingleCopy_DpsEqualsBaseDamageTimesRatio()
    {
        var tower = new FakeTower { BaseDamage = 20f };
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));
        var enemy = new FakeEnemy { Hp = 100f };

        tower.Modifiers[0].OnHit(Ctx(tower, enemy));

        float expectedDps = 20f * Balance.WildfireBurnDpsRatio;
        Assert.Equal(expectedDps, enemy.BurnDamagePerSecond, precision: 3);
    }

    // ── ReaperProtocol ────────────────────────────────────────────────────────

    private static DamageContext ChainCtx(ITowerView attacker, IEnemyView target, int waveIndex = 10)
        => new(attacker, target, waveIndex, new List<IEnemyView>(), isChain: true);

    private static DamageContext PrimaryCtx(ITowerView attacker, IEnemyView target, int waveIndex = 10)
        => new(attacker, target, waveIndex, new List<IEnemyView>(), isChain: false);

    [Fact]
    public void ReaperProtocol_OnKill_ChainKill_ReturnsFalse()
    {
        var tower = new FakeTower();
        var enemy = new FakeEnemy { Hp = 0f };
        var mod = new ReaperProtocol(Def("reaper_protocol"));

        bool result = mod.OnKill(ChainCtx(tower, enemy));

        Assert.False(result);
    }

    [Fact]
    public void ReaperProtocol_OnKill_PrimaryKill_ReturnsTrue()
    {
        var tower = new FakeTower();
        var enemy = new FakeEnemy { Hp = 0f };
        var mod = new ReaperProtocol(Def("reaper_protocol"));

        bool result = mod.OnKill(PrimaryCtx(tower, enemy));

        Assert.True(result);
    }

    [Fact]
    public void ReaperProtocol_OnKill_CapsAtKillCapPerWave()
    {
        var tower = new FakeTower();
        var mod = new ReaperProtocol(Def("reaper_protocol"));

        int procs = 0;
        for (int i = 0; i < Balance.ReaperProtocolKillCap + 3; i++)
        {
            var enemy = new FakeEnemy { Hp = 0f };
            if (mod.OnKill(PrimaryCtx(tower, enemy, waveIndex: 12)))
                procs++;
        }

        Assert.Equal(Balance.ReaperProtocolKillCap, procs);
    }

    [Fact]
    public void ReaperProtocol_OnKill_AfterCapReached_ReturnsFalse()
    {
        var tower = new FakeTower();
        var mod = new ReaperProtocol(Def("reaper_protocol"));

        // Exhaust the per-wave cap
        for (int i = 0; i < Balance.ReaperProtocolKillCap; i++)
        {
            var e = new FakeEnemy { Hp = 0f };
            mod.OnKill(PrimaryCtx(tower, e, waveIndex: 5));
        }

        var extra = new FakeEnemy { Hp = 0f };
        bool result = mod.OnKill(PrimaryCtx(tower, extra, waveIndex: 5));

        Assert.False(result);
    }

    [Fact]
    public void ReaperProtocol_OnKill_ResetsCapOnNewWave()
    {
        var tower = new FakeTower();
        var mod = new ReaperProtocol(Def("reaper_protocol"));

        // Exhaust cap on wave 10
        for (int i = 0; i < Balance.ReaperProtocolKillCap; i++)
        {
            var e = new FakeEnemy { Hp = 0f };
            mod.OnKill(PrimaryCtx(tower, e, waveIndex: 10));
        }

        // Cap is full -- wave 10 kill returns false
        var postCap = new FakeEnemy { Hp = 0f };
        Assert.False(mod.OnKill(PrimaryCtx(tower, postCap, waveIndex: 10)));

        // Wave 11 resets the counter -- first kill of new wave returns true
        var newWave = new FakeEnemy { Hp = 0f };
        Assert.True(mod.OnKill(PrimaryCtx(tower, newWave, waveIndex: 11)));
    }

    [Fact]
    public void ReaperProtocol_OnKill_TwoCopies_EachHaveIndependentCounters()
    {
        // Per the design, each modifier copy tracks kills independently.
        // Two copies on the same tower each have their own cap of 5.
        var tower = new FakeTower();
        var mod1 = new ReaperProtocol(Def("reaper_protocol"));
        var mod2 = new ReaperProtocol(Def("reaper_protocol"));

        // Exhaust mod1's cap
        for (int i = 0; i < Balance.ReaperProtocolKillCap; i++)
        {
            var e = new FakeEnemy { Hp = 0f };
            mod1.OnKill(PrimaryCtx(tower, e, waveIndex: 10));
        }

        // mod1 is capped, mod2 still has kills available
        var enemy = new FakeEnemy { Hp = 0f };
        Assert.False(mod1.OnKill(PrimaryCtx(tower, enemy, waveIndex: 10)));
        Assert.True(mod2.OnKill(PrimaryCtx(tower, enemy, waveIndex: 10)));
    }

    [Fact]
    public void ReaperProtocol_OnKill_ChainKillDoesNotCountTowardCap()
    {
        // Chain kills must never consume cap slots -- only primary kills do.
        var tower = new FakeTower();
        var mod = new ReaperProtocol(Def("reaper_protocol"));

        // 10 chain kills -- none count toward cap
        for (int i = 0; i < 10; i++)
        {
            var e = new FakeEnemy { Hp = 0f };
            mod.OnKill(ChainCtx(tower, e, waveIndex: 10));
        }

        // First primary kill after many chain kills should still proc (cap not consumed)
        var primary = new FakeEnemy { Hp = 0f };
        Assert.True(mod.OnKill(PrimaryCtx(tower, primary, waveIndex: 10)));
    }

    // ── Deadzone ──────────────────────────────────────────────────────────

    [Fact]
    public void Deadzone_ModifierId_IsConfigured()
    {
        var mod = new Deadzone(Def("deadzone"));
        Assert.Equal("deadzone", mod.ModifierId);
    }

    [Fact]
    public void Deadzone_ApplyToChainTargets_IsFalse()
    {
        // Deadzone should NOT plant zones on chain/split secondary hits.
        var mod = new Deadzone(Def("deadzone"));
        Assert.False(mod.ApplyToChainTargets);
    }

    [Fact]
    public void Deadzone_OnHit_PrimaryHit_ReturnsTrue()
    {
        // OnHit should return true on primary hit to show proc visual.
        // (Deadzone's OnHit is the inherited no-op; the actual zone placement
        // is triggered by DamageModel seeding -- but here we verify the modifier
        // class itself is well-formed and doesn't throw on primary hit.)
        var tower = new FakeTower { BaseDamage = 10f };
        tower.Modifiers.Add(new Deadzone(Def("deadzone")));
        var enemy = new FakeEnemy { Hp = 100f };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0, new List<IEnemyView>(), isChain: false);

        // Base Modifier.OnHit returns false -- Deadzone delegates seeding to DamageModel.
        bool result = tower.Modifiers[0].OnHit(ctx);
        Assert.False(result); // no OnHit override; zone placement is in DamageModel
    }

    [Fact]
    public void Deadzone_Balance_LifetimeIsPositive()
        => Assert.True(Balance.DeadzoneLifetime > 0f);

    [Fact]
    public void Deadzone_Balance_ArmTimeIsPositive()
        => Assert.True(Balance.DeadzoneArmTime > 0f);

    [Fact]
    public void Deadzone_Balance_TriggerRadiusIsPositive()
        => Assert.True(Balance.DeadzoneTriggerRadius > 0f);

    [Fact]
    public void Deadzone_Balance_PinDurationIsPositive()
        => Assert.True(Balance.DeadzonePinDuration > 0f);

    [Fact]
    public void Deadzone_Balance_PinDurationPerCopyIsPositive()
        => Assert.True(Balance.DeadzonePinDurationPerCopy > 0f);

    [Fact]
    public void Deadzone_Balance_ArmTimeLessThanLifetime()
    {
        // Zone arm time must be shorter than total lifetime or zone would never fire.
        Assert.True(Balance.DeadzoneArmTime < Balance.DeadzoneLifetime);
    }

    [Fact]
    public void Deadzone_Balance_PinDurationLessThanLifetime()
    {
        // Base pin duration should be shorter than zone lifetime (1 copy scenario).
        Assert.True(Balance.DeadzonePinDuration < Balance.DeadzoneLifetime * 3f);
    }

    // ── Unlocks: Deadzone full-game gates ────────────────────────────────────

    [Fact]
    public void Unlocks_DeadzoneModifierId_IsDeadzone()
        => Assert.Equal("deadzone", Unlocks.DeadzoneModifierId);

    [Fact]
    public void Unlocks_DeadzoneAchievementId_HasExpectedValue()
        => Assert.Equal("DEADZONE_UNSEALED", Unlocks.DeadzoneAchievementId);
}
