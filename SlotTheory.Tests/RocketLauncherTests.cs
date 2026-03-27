using System.Collections.Generic;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Modifiers;
using Xunit;

namespace SlotTheory.Tests;

public class RocketLauncherTests
{
    private sealed class BlastCoreTagOnly : Modifier
    {
        public BlastCoreTagOnly() => ModifierId = "blast_core";
    }

    [Fact]
    public void RocketLauncher_PrimaryHit_AppliesBuiltInSplash()
    {
        var tower = new FakeTower
        {
            TowerId = "rocket_launcher",
            BaseDamage = 40f,
        };
        var primary = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby = new FakeEnemy
        {
            Hp = 100f,
            GlobalPosition = new Vector2(Balance.RocketLauncherSplashRadius - 1f, 0f)
        };
        var outside = new FakeEnemy
        {
            Hp = 100f,
            GlobalPosition = new Vector2(Balance.RocketLauncherSplashRadius + 12f, 0f)
        };

        var enemies = new List<SlotTheory.Entities.IEnemyView> { primary, nearby, outside };
        var ctx = new DamageContext(tower, primary, waveIndex: 0, enemies);
        DamageModel.Apply(ctx);

        Assert.True(primary.Hp < 100f);
        Assert.True(nearby.Hp < 100f);
        Assert.Equal(100f, outside.Hp);
        Assert.True(ctx.SplashDamageDealt > 0f);
    }

    [Fact]
    public void RocketLauncher_BlastCoreCopies_ExpandBuiltInSplashRadius()
    {
        var tower = new FakeTower
        {
            TowerId = "rocket_launcher",
            BaseDamage = 40f,
        };
        tower.Modifiers.Add(new BlastCoreTagOnly());

        var primary = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var inExpandedRing = new FakeEnemy
        {
            Hp = 100f,
            GlobalPosition = new Vector2(Balance.RocketLauncherSplashRadius + 8f, 0f)
        };
        var enemies = new List<SlotTheory.Entities.IEnemyView> { primary, inExpandedRing };

        var ctx = new DamageContext(tower, primary, waveIndex: 0, enemies);
        DamageModel.Apply(ctx);

        Assert.True(inExpandedRing.Hp < 100f);
        Assert.True(ctx.SplashDamageDealt > 0f);
    }

    [Fact]
    public void RocketLauncher_ChainHit_DoesNotTriggerBuiltInSplash()
    {
        var tower = new FakeTower
        {
            TowerId = "rocket_launcher",
            BaseDamage = 40f,
        };
        var primary = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby = new FakeEnemy
        {
            Hp = 100f,
            GlobalPosition = new Vector2(Balance.RocketLauncherSplashRadius - 1f, 0f)
        };
        var enemies = new List<SlotTheory.Entities.IEnemyView> { primary, nearby };

        var ctx = new DamageContext(tower, primary, waveIndex: 0, enemies, isChain: true);
        DamageModel.Apply(ctx);

        Assert.True(primary.Hp < 100f);
        Assert.Equal(100f, nearby.Hp);
        Assert.Equal(0f, ctx.SplashDamageDealt);
    }
}
