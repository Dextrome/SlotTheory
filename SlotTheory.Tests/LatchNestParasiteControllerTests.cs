using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Modifiers;
using Xunit;

namespace SlotTheory.Tests;

public class LatchNestParasiteControllerTests
{
    private static ModifierDef Def(string id) => new(id, id, string.Empty, new Dictionary<string, float>());

    [Fact]
    public void AttachStylePrimaryHit_UsesIsChainFalse_AndCanTriggerBlastCoreSplash()
    {
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 20f };
        tower.Modifiers.Add(new BlastCore(Def("blast_core")));

        var host = new FakeEnemy { Hp = 200f, GlobalPosition = Godot.Vector2.Zero };
        var nearby = new FakeEnemy { Hp = 200f, GlobalPosition = new Godot.Vector2(48f, 0f) };
        var enemies = new List<IEnemyView> { host, nearby };

        var ctx = new DamageContext(tower, host, waveIndex: 0, enemies, state: null, isChain: false);
        DamageModel.Apply(ctx);

        Assert.False(ctx.IsChain);
        Assert.True(nearby.Hp < 200f);
    }

    [Fact]
    public void ParasiteTicks_AreSecondaryHits_AndProcBlastCoreAndWildfireOnHit()
    {
        // Parasite ticks use isChain=true but Blast Core and Wildfire now fire on all hits
        // (proc spaghetti is desired). Nearby enemy within blast radius takes splash damage;
        // host receives the burn from Wildfire.
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 18f };
        tower.Modifiers.Add(new BlastCore(Def("blast_core")));
        tower.Modifiers.Add(new Wildfire(Def("wildfire")));

        var host = new FakeEnemy { Hp = 220f, GlobalPosition = Godot.Vector2.Zero };
        var nearby = new FakeEnemy { Hp = 220f, GlobalPosition = new Godot.Vector2(52f, 0f) };
        var enemies = new List<FakeEnemy> { host, nearby };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        bool attached = controller.TryAttach(
            tower, host, durationSeconds: 1.0f, tickIntervalSeconds: 0.10f,
            maxActivePerTower: Balance.LatchNestMaxActiveParasitesPerTower,
            maxPerHost: Balance.LatchNestMaxParasitesPerHost,
            out _, out _);
        Assert.True(attached);

        LatchParasiteTickEvent<FakeEnemy>? tickEvent = null;
        controller.Tick(0.11f, waveIndex: 3, enemies, state: null, tickDamageMultiplier: 1f, onTick: evt => tickEvent = evt);

        Assert.NotNull(tickEvent);
        Assert.True(tickEvent!.Value.Context.IsChain);
        // Blast Core fires: nearby takes splash from host's position (52px < BlastCoreRadius 140px)
        float expectedSplash = 18f * Balance.BlastCoreDamageRatio;
        Assert.Equal(220f - expectedSplash, nearby.Hp, precision: 1);
        // Wildfire fires: host gains burn
        Assert.Equal(Balance.WildfireBurnDuration, host.BurnRemaining, precision: 3);
    }

    [Fact]
    public void ParasiteTickKill_DoesNotSpillOverkillDamage()
    {
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 40f };
        tower.Modifiers.Add(new Overkill(Def("overkill")));

        var host = new FakeEnemy { Hp = 10f, ProgressRatio = 0.7f };
        var next = new FakeEnemy { Hp = 90f, ProgressRatio = 0.5f };
        var enemies = new List<FakeEnemy> { host, next };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(
            tower, host, 1.0f, 0.10f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));

        controller.Tick(0.11f, waveIndex: 1, enemies, state: null, tickDamageMultiplier: 1f);

        Assert.Equal(90f, next.Hp, 3);
    }

    [Fact]
    public void ParasiteTickKill_DoesNotProcReaperProtocolPrimaryOnlyKill()
    {
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 30f };
        var reaper = new ReaperProtocol(Def("reaper_protocol"));
        tower.Modifiers.Add(reaper);

        var host = new FakeEnemy { Hp = 8f };
        var enemies = new List<FakeEnemy> { host };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(
            tower, host, 1.0f, 0.10f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));

        LatchParasiteTickEvent<FakeEnemy>? tickEvent = null;
        controller.Tick(0.11f, waveIndex: 5, enemies, state: null, tickDamageMultiplier: 1f, onTick: evt => tickEvent = evt);

        Assert.NotNull(tickEvent);
        Assert.True(tickEvent!.Value.Context.Target.Hp <= 0f);
        Assert.False(reaper.OnKill(tickEvent.Value.Context));
    }

    [Fact]
    public void Momentum_RampsAcrossRepeatedParasiteTicksOnSameHost()
    {
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 10f };
        tower.Modifiers.Add(new Momentum(Def("momentum")));

        var host = new FakeEnemy { Hp = 1000f };
        var enemies = new List<FakeEnemy> { host };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(
            tower, host, 2.0f, 0.15f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));

        var damages = new List<float>();
        for (int i = 0; i < 6; i++)
        {
            controller.Tick(0.16f, waveIndex: 2, enemies, state: null, tickDamageMultiplier: 1f, onTick: evt =>
            {
                damages.Add(evt.Context.FinalDamage);
            });
        }

        Assert.True(damages.Count >= 3);
        Assert.True(damages[^1] > damages[0], $"Expected momentum growth, first={damages[0]:F2}, last={damages[^1]:F2}");
    }

    [Fact]
    public void ParasiteTicks_NaturallyApplyChillAndExploitWeakness()
    {
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 10f };
        tower.Modifiers.Add(new Slow(Def("slow")));
        tower.Modifiers.Add(new ExploitWeakness(Def("exploit_weakness")));

        var host = new FakeEnemy { Hp = 200f, MarkedRemaining = 4f };
        var enemies = new List<FakeEnemy> { host };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(
            tower, host, 1.0f, 0.10f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));

        LatchParasiteTickEvent<FakeEnemy>? tickEvent = null;
        controller.Tick(0.11f, waveIndex: 8, enemies, state: null, tickDamageMultiplier: 1f, onTick: evt => tickEvent = evt);

        Assert.NotNull(tickEvent);
        Assert.True(host.SlowRemaining > 0f);
        Assert.Equal(Balance.SlowSpeedFactor, host.SlowSpeedFactor, 3);
        float expected = 10f * Balance.ExploitWeaknessDamageBonus * (1f + Balance.MarkedDamageBonus);
        Assert.Equal(expected, tickEvent!.Value.Context.FinalDamage, 2);
    }

    [Fact]
    public void PerHostAndPerTowerParasiteCaps_AreRespected()
    {
        var tower = new FakeTower { TowerId = "latch_nest" };
        var hostA = new FakeEnemy { Hp = 100f };
        var hostB = new FakeEnemy { Hp = 100f };
        var hostC = new FakeEnemy { Hp = 100f };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(tower, hostA, 2f, 0.2f, maxActivePerTower: 3, maxPerHost: 2, out _, out _));
        Assert.True(controller.TryAttach(tower, hostA, 2f, 0.2f, maxActivePerTower: 3, maxPerHost: 2, out _, out _));
        Assert.False(controller.TryAttach(tower, hostA, 2f, 0.2f, maxActivePerTower: 3, maxPerHost: 2, out _, out _));

        Assert.True(controller.TryAttach(tower, hostB, 2f, 0.2f, maxActivePerTower: 3, maxPerHost: 2, out _, out _));
        Assert.False(controller.TryAttach(tower, hostC, 2f, 0.2f, maxActivePerTower: 3, maxPerHost: 2, out _, out _));
    }

    [Fact]
    public void Parasites_CleanUpOnHostDeath_AndOnControllerClear()
    {
        var tower = new FakeTower { TowerId = "latch_nest" };
        var host = new FakeEnemy { Hp = 100f };
        var enemies = new List<FakeEnemy> { host };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(
            tower, host, 2f, 0.2f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));

        var detachReasons = new List<LatchParasiteDetachReason>();
        host.Hp = 0f;
        controller.Tick(0.21f, waveIndex: 1, enemies, state: null, tickDamageMultiplier: 1f, onDetach: evt =>
        {
            detachReasons.Add(evt.Reason);
        });

        Assert.Contains(LatchParasiteDetachReason.HostDead, detachReasons);
        Assert.Equal(0, controller.ActiveCount);

        Assert.True(controller.TryAttach(
            tower, new FakeEnemy { Hp = 100f }, 2f, 0.2f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));
        controller.Clear(evt => detachReasons.Add(evt.Reason));
        Assert.Contains(LatchParasiteDetachReason.Cleared, detachReasons);
        Assert.Equal(0, controller.ActiveCount);
    }

    [Fact]
    public void ParasiteTicks_WorkWithoutRunStateOrVisualDependencies()
    {
        var tower = new FakeTower { TowerId = "latch_nest", BaseDamage = 9f };
        var host = new FakeEnemy { Hp = 100f };
        var enemies = new List<FakeEnemy> { host };
        var controller = new LatchNestParasiteController<FakeEnemy>();

        Assert.True(controller.TryAttach(
            tower, host, 1f, 0.2f,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out _, out _));

        int ticks = controller.Tick(0.21f, waveIndex: 0, enemies, state: null, tickDamageMultiplier: 1f);
        Assert.True(ticks >= 1);
        Assert.True(host.Hp < 100f);
    }
}
