using System.Collections.Generic;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Modifiers;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for all 10 modifier behaviors using FakeTower and FakeEnemy stubs.
/// No Godot engine needed — all pure C# logic.
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
    public void Overreach_OnEquip_IncreasesRange140Percent()
    {
        var tower = new FakeTower { Range = 200f };
        new Overreach(Def("overreach")).OnEquip(tower);
        Assert.Equal(280f, tower.Range, precision: 2);
    }

    [Fact]
    public void Overreach_OnEquip_ReducesDamage80Percent()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        new Overreach(Def("overreach")).OnEquip(tower);
        Assert.Equal(8f, tower.BaseDamage, precision: 2);
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
        Assert.True(tower.ChainRange >= 400f);
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
    public void FocusLens_ModifyAttackInterval_DoublesInterval()
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
        var tower = new FakeTower();
        var primary = new FakeEnemy { Hp = -40f, ProgressRatio = 0.5f }; // overkill by 40
        var next = new FakeEnemy { Hp = 100f, ProgressRatio = 0.3f };
        var enemies = new List<IEnemyView> { next };

        new Overkill(Def("overkill")).OnKill(new DamageContext(tower, primary, 0, enemies));

        // 40 * 0.60 = 24 spill damage
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
    public void FeedbackLoop_OnKill_ReducesCooldownBy25Percent()
    {
        var tower = new FakeTower { Cooldown = 0.8f };
        var enemy = new FakeEnemy { Hp = 0f }; // dead
        var mod = new FeedbackLoop(Def("feedback_loop"));

        mod.OnKill(Ctx(tower, enemy));

        Assert.Equal(0.8f * (1f - Balance.FeedbackLoopCooldownReduction), tower.Cooldown, precision: 3);
    }

    [Fact]
    public void FeedbackLoop_OnKill_CooldownNeverGoesNegative()
    {
        var tower = new FakeTower { Cooldown = 0f };
        var enemy = new FakeEnemy { Hp = 0f };
        var mod = new FeedbackLoop(Def("feedback_loop"));

        mod.OnKill(Ctx(tower, enemy));

        Assert.Equal(0f, tower.Cooldown);
    }
}
