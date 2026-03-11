using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Lightweight per-frame counters for layered enemy rendering diagnostics.
/// </summary>
public static class EnemyRenderDebugCounters
{
    private static ulong _frameId;
    private static ulong _bloomBudgetFrameId;

    public static int BodyPassCalls { get; private set; }
    public static int DamagePassCalls { get; private set; }
    public static int EmissivePassCalls { get; private set; }
    public static int BloomPassCalls { get; private set; }
    public static int BloomPrimitives { get; private set; }
    public static int BloomFallbackCalls { get; private set; }
    public static int BloomFallbackPrimitives { get; private set; }
    public static int BloomBudgetCap { get; private set; }
    public static int BloomBudgetUsed { get; private set; }
    public static int BloomBudgetRejected { get; private set; }

    private static void EnsureFrame()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _frameId) return;

        _frameId = frame;
        BodyPassCalls = 0;
        DamagePassCalls = 0;
        EmissivePassCalls = 0;
        BloomPassCalls = 0;
        BloomPrimitives = 0;
        BloomFallbackCalls = 0;
        BloomFallbackPrimitives = 0;
        BloomBudgetCap = 0;
        BloomBudgetUsed = 0;
        BloomBudgetRejected = 0;
        _bloomBudgetFrameId = 0;
    }

    private static void EnsureBloomBudget(int frameBudgetCap)
    {
        EnsureFrame();
        if (_bloomBudgetFrameId == _frameId)
            return;

        _bloomBudgetFrameId = _frameId;
        BloomBudgetCap = Mathf.Max(0, frameBudgetCap);
        BloomBudgetUsed = 0;
        BloomBudgetRejected = 0;
    }

    public static void RegisterBodyPass()
    {
        EnsureFrame();
        BodyPassCalls++;
    }

    public static void RegisterDamagePass()
    {
        EnsureFrame();
        DamagePassCalls++;
    }

    public static void RegisterEmissivePass()
    {
        EnsureFrame();
        EmissivePassCalls++;
    }

    public static void RegisterBloomPass(int primitives)
    {
        EnsureFrame();
        BloomPassCalls++;
        BloomPrimitives += primitives;
    }

    public static bool TryReserveBloom(int primitives, int frameBudgetCap)
    {
        int safePrimitives = Mathf.Max(0, primitives);
        EnsureBloomBudget(frameBudgetCap);

        if (safePrimitives == 0)
            return true;

        if (BloomBudgetCap <= 0 || BloomBudgetUsed + safePrimitives > BloomBudgetCap)
        {
            BloomBudgetRejected += safePrimitives;
            return false;
        }

        BloomBudgetUsed += safePrimitives;
        return true;
    }

    public static void RegisterBloomFallback(int primitives)
    {
        EnsureFrame();
        BloomFallbackCalls++;
        BloomFallbackPrimitives += Mathf.Max(0, primitives);
    }
}
