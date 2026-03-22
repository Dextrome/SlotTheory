namespace SlotTheory.Combat;

/// <summary>
/// Pure-logic helper for Accordion Engine formation compression.
/// Operates on a plain float array so it can be unit-tested without any Godot dependencies.
/// <see cref="CombatSim"/> extracts Progress values into a float[], calls Compress(), then
/// writes the results back -- the Godot PathFollow2D GlobalPosition update is handled by the
/// Progress property setter on EnemyInstance, exactly as the Reverse Walker already does.
/// </summary>
public static class AccordionFormation
{
    /// <summary>
    /// Compresses an ascending-sorted array of Progress values toward their median.
    /// Modifies the array in place.
    ///
    /// Steps (matching CombatSim.CompressEnemyFormation):
    ///   1. Find median: element at index count/2 (upper-middle for even counts).
    ///   2. Compress each value toward median by compressionFactor
    ///      (factor=0.25 → 75% spread reduction; factor=0 → full collapse; factor=1 → no change).
    ///   3. Enforce minimum inter-enemy spacing from the leading enemy downward;
    ///      trailing enemies are nudged back if too close.
    ///   4. Clamp all values to >= 0 (safety net for enemies near the lane start).
    /// </summary>
    /// <param name="progressAscending">Progress values sorted ascending (trailing → leading).
    ///   Must have at least 2 elements; returns immediately if count &lt; 2.</param>
    /// <param name="compressionFactor">Fraction of spread to preserve (0 = full collapse, 1 = no change).</param>
    /// <param name="minSpacingPx">Minimum distance in pixels enforced between adjacent enemies.</param>
    public static void Compress(float[] progressAscending, float compressionFactor, float minSpacingPx)
    {
        int count = progressAscending.Length;
        if (count < 2) return;

        float median = progressAscending[count / 2];

        // Step 1: compress toward median
        for (int i = 0; i < count; i++)
            progressAscending[i] = median + (progressAscending[i] - median) * compressionFactor;

        // Step 2: enforce minimum spacing (leading-to-trailing pass)
        for (int i = count - 2; i >= 0; i--)
        {
            float minAllowed = progressAscending[i + 1] - minSpacingPx;
            if (progressAscending[i] > minAllowed)
                progressAscending[i] = minAllowed;
        }

        // Step 3: clamp negatives
        for (int i = 0; i < count; i++)
            if (progressAscending[i] < 0f) progressAscending[i] = 0f;
    }
}
