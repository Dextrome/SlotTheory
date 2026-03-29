using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public enum LatchParasiteDetachReason
{
    Expired,
    HostDead,
    TowerRemoved,
    Cleared,
}

public readonly struct LatchParasiteTickEvent<TEnemy> where TEnemy : class, IEnemyView
{
    public ulong ParasiteId { get; }
    public ITowerView Tower { get; }
    public TEnemy Host { get; }
    public int HostSlot { get; }
    public DamageContext Context { get; }

    public LatchParasiteTickEvent(ulong parasiteId, ITowerView tower, TEnemy host, int hostSlot, DamageContext context)
    {
        ParasiteId = parasiteId;
        Tower = tower;
        Host = host;
        HostSlot = hostSlot;
        Context = context;
    }
}

public readonly struct LatchParasiteDetachEvent<TEnemy> where TEnemy : class, IEnemyView
{
    public ulong ParasiteId { get; }
    public ITowerView Tower { get; }
    public TEnemy Host { get; }
    public int HostSlot { get; }
    public LatchParasiteDetachReason Reason { get; }

    public LatchParasiteDetachEvent(ulong parasiteId, ITowerView tower, TEnemy host, int hostSlot, LatchParasiteDetachReason reason)
    {
        ParasiteId = parasiteId;
        Tower = tower;
        Host = host;
        HostSlot = hostSlot;
        Reason = reason;
    }
}

/// <summary>
/// Shared runtime parasite state for Latch Nest.
/// Owns pure combat logic so gameplay is deterministic in both live sim and headless benchmarks.
/// </summary>
public sealed class LatchNestParasiteController<TEnemy> where TEnemy : class, IEnemyView
{
    private sealed class ActiveParasite
    {
        public ulong Id { get; init; }
        public ITowerView Tower { get; init; } = null!;
        public TEnemy Host { get; init; } = null!;
        public float Remaining { get; set; }
        public float TickInterval { get; init; }
        public float TickRemaining { get; set; }
        public int HostSlot { get; init; }
    }

    private readonly List<ActiveParasite> _active = new();
    private ulong _nextId = 1;

    public int ActiveCount => _active.Count;

    public int ActiveCountForTower(ITowerView tower)
        => _active.Count(p => ReferenceEquals(p.Tower, tower));

    public int ActiveCountOnHost(ITowerView tower, TEnemy host)
        => _active.Count(p => ReferenceEquals(p.Tower, tower) && ReferenceEquals(p.Host, host));

    public void ForEachActive(Action<ulong, ITowerView, TEnemy, float, int> visitor)
    {
        foreach (ActiveParasite parasite in _active)
            visitor(parasite.Id, parasite.Tower, parasite.Host, parasite.Remaining, parasite.HostSlot);
    }

    public bool TryAttach(
        ITowerView tower,
        TEnemy host,
        float durationSeconds,
        float tickIntervalSeconds,
        int maxActivePerTower,
        int maxPerHost,
        out ulong parasiteId,
        out int hostSlot)
    {
        parasiteId = 0;
        hostSlot = -1;
        if (tower == null || host == null)
            return false;
        if (host.Hp <= 0f)
            return false;
        if (ActiveCountForTower(tower) >= maxActivePerTower)
            return false;
        if (ActiveCountOnHost(tower, host) >= maxPerHost)
            return false;

        hostSlot = AllocateHostSlot(tower, host, maxPerHost);
        if (hostSlot < 0)
            return false;

        ulong id = _nextId++;
        _active.Add(new ActiveParasite
        {
            Id = id,
            Tower = tower,
            Host = host,
            Remaining = MathF.Max(0.01f, durationSeconds),
            TickInterval = MathF.Max(0.03f, tickIntervalSeconds),
            TickRemaining = MathF.Max(0.03f, tickIntervalSeconds),
            HostSlot = hostSlot,
        });

        parasiteId = id;
        return true;
    }

    public int Tick(
        float delta,
        int waveIndex,
        List<TEnemy> enemies,
        SlotTheory.Core.RunState? state,
        float tickDamageMultiplier,
        Action<LatchParasiteTickEvent<TEnemy>>? onTick = null,
        Action<LatchParasiteDetachEvent<TEnemy>>? onDetach = null)
    {
        if (delta <= 0f || _active.Count == 0)
            return 0;

        int ticks = 0;
        IEnumerable<IEnemyView> enemyViews = enemies.Cast<IEnemyView>();
        float baseScale = MathF.Max(0f, tickDamageMultiplier);

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveParasite parasite = _active[i];

            if (parasite.Host.Hp <= 0f)
            {
                onDetach?.Invoke(new LatchParasiteDetachEvent<TEnemy>(
                    parasite.Id,
                    parasite.Tower,
                    parasite.Host,
                    parasite.HostSlot,
                    LatchParasiteDetachReason.HostDead));
                _active.RemoveAt(i);
                continue;
            }

            parasite.Remaining -= delta;
            parasite.TickRemaining -= delta;

            while (parasite.TickRemaining <= 0f && parasite.Remaining > 0f && parasite.Host.Hp > 0f)
            {
                parasite.TickRemaining += parasite.TickInterval;
                var context = new DamageContext(
                    parasite.Tower,
                    parasite.Host,
                    waveIndex,
                    enemyViews,
                    state,
                    isChain: true,
                    damageOverride: parasite.Tower.BaseDamage * baseScale);
                DamageModel.Apply(context);
                onTick?.Invoke(new LatchParasiteTickEvent<TEnemy>(
                    parasite.Id,
                    parasite.Tower,
                    parasite.Host,
                    parasite.HostSlot,
                    context));
                ticks++;
            }

            if (parasite.Host.Hp <= 0f)
            {
                onDetach?.Invoke(new LatchParasiteDetachEvent<TEnemy>(
                    parasite.Id,
                    parasite.Tower,
                    parasite.Host,
                    parasite.HostSlot,
                    LatchParasiteDetachReason.HostDead));
                _active.RemoveAt(i);
                continue;
            }

            if (parasite.Remaining <= 0f)
            {
                onDetach?.Invoke(new LatchParasiteDetachEvent<TEnemy>(
                    parasite.Id,
                    parasite.Tower,
                    parasite.Host,
                    parasite.HostSlot,
                    LatchParasiteDetachReason.Expired));
                _active.RemoveAt(i);
            }
        }

        return ticks;
    }

    public int RemoveByHost(TEnemy host, Action<LatchParasiteDetachEvent<TEnemy>>? onDetach = null)
    {
        int removed = 0;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveParasite parasite = _active[i];
            if (!ReferenceEquals(parasite.Host, host))
                continue;
            onDetach?.Invoke(new LatchParasiteDetachEvent<TEnemy>(
                parasite.Id,
                parasite.Tower,
                parasite.Host,
                parasite.HostSlot,
                LatchParasiteDetachReason.HostDead));
            _active.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    public int RemoveByTower(ITowerView tower, Action<LatchParasiteDetachEvent<TEnemy>>? onDetach = null)
    {
        int removed = 0;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveParasite parasite = _active[i];
            if (!ReferenceEquals(parasite.Tower, tower))
                continue;
            onDetach?.Invoke(new LatchParasiteDetachEvent<TEnemy>(
                parasite.Id,
                parasite.Tower,
                parasite.Host,
                parasite.HostSlot,
                LatchParasiteDetachReason.TowerRemoved));
            _active.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    public void Clear(Action<LatchParasiteDetachEvent<TEnemy>>? onDetach = null)
    {
        if (onDetach != null)
        {
            foreach (ActiveParasite parasite in _active)
            {
                onDetach(new LatchParasiteDetachEvent<TEnemy>(
                    parasite.Id,
                    parasite.Tower,
                    parasite.Host,
                    parasite.HostSlot,
                    LatchParasiteDetachReason.Cleared));
            }
        }
        _active.Clear();
    }

    private int AllocateHostSlot(ITowerView tower, TEnemy host, int maxPerHost)
    {
        var used = new HashSet<int>();
        foreach (ActiveParasite parasite in _active)
        {
            if (!ReferenceEquals(parasite.Tower, tower) || !ReferenceEquals(parasite.Host, host))
                continue;
            used.Add(parasite.HostSlot);
        }

        for (int i = 0; i < maxPerHost; i++)
        {
            if (!used.Contains(i))
                return i;
        }
        return -1;
    }
}
