using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Pure motion profile calculations for enemy visuals.
/// Keeps behavior testable outside of rendering code.
/// </summary>
public static class EnemyVisualProfile
{
    public readonly struct Sample
    {
        public float BobOffsetPx { get; }
        public float JitterX { get; }
        public float JitterY { get; }
        public float ThrustPulse { get; }
        public float BodyTiltRad { get; }
        public float NearDeathPulse { get; }
        public float NearDeathFlicker { get; }

        public float JitterMagnitude => Mathf.Sqrt(JitterX * JitterX + JitterY * JitterY);

        public Sample(
            float bobOffsetPx,
            float jitterX,
            float jitterY,
            float thrustPulse,
            float bodyTiltRad,
            float nearDeathPulse,
            float nearDeathFlicker)
        {
            BobOffsetPx = bobOffsetPx;
            JitterX = jitterX;
            JitterY = jitterY;
            ThrustPulse = thrustPulse;
            BodyTiltRad = bodyTiltRad;
            NearDeathPulse = nearDeathPulse;
            NearDeathFlicker = nearDeathFlicker;
        }
    }

    public const float NearDeathThreshold = 0.22f;

    public static Sample Evaluate(string enemyTypeId, float elapsed, float effectiveSpeed, float hpRatio)
    {
        float speedNorm = Mathf.Clamp(effectiveSpeed / Mathf.Max(1f, Balance.BaseEnemySpeed), 0.35f, 2.6f);
        float nearDeathRaw = Mathf.Clamp((NearDeathThreshold - hpRatio) / NearDeathThreshold, 0f, 1f);
        float nearDeathPulse = nearDeathRaw <= 0f
            ? 0f
            : nearDeathRaw * (0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(elapsed * 15.3f)));
        float nearDeathFlicker = nearDeathRaw <= 0f
            ? 0f
            : nearDeathRaw * (0.5f + 0.5f * Mathf.Sin(elapsed * 39.1f));

        Sample motion = enemyTypeId switch
        {
            "swift_walker"   => EvaluateSwift(elapsed, speedNorm),
            "reverse_walker" => EvaluateReverse(elapsed, speedNorm),
            "armored_walker" => EvaluateArmored(elapsed, speedNorm),
            "splitter_shard" => EvaluateSwift(elapsed, speedNorm),
            "shield_drone"   => EvaluateShieldDrone(elapsed, speedNorm),
            EnemyCatalog.AnchorWalkerId => EvaluateAnchor(elapsed, speedNorm),
            EnemyCatalog.NullDroneId => EvaluateNullDrone(elapsed, speedNorm),
            EnemyCatalog.LancerWalkerId => EvaluateLancer(elapsed, speedNorm),
            EnemyCatalog.VeilWalkerId => EvaluateVeil(elapsed, speedNorm),
            _                => EvaluateBasic(elapsed, speedNorm),
        };

        return new Sample(
            motion.BobOffsetPx,
            motion.JitterX,
            motion.JitterY,
            motion.ThrustPulse,
            motion.BodyTiltRad,
            nearDeathPulse,
            nearDeathFlicker);
    }

    private static Sample EvaluateBasic(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (1.8f + speedNorm * 0.22f)) * 0.95f;
        float driftX = Mathf.Sin(elapsed * 2.2f) * 0.18f;
        float driftY = Mathf.Sin(elapsed * 2.9f) * 0.12f;
        float thrust = 0.22f + speedNorm * 0.28f + 0.08f * Mathf.Sin(elapsed * 6.2f);
        float tilt = 0.022f * Mathf.Sin(elapsed * 2.4f);
        return new Sample(bob, driftX, driftY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateSwift(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (3.2f + speedNorm * 1.1f)) * 0.72f;
        float jitterX = (Mathf.Sin(elapsed * 17.3f) + Mathf.Sin(elapsed * 29.7f) * 0.63f) * 0.38f;
        float jitterY = (Mathf.Sin(elapsed * 22.4f) + Mathf.Sin(elapsed * 32.8f) * 0.47f) * 0.42f;
        float thrust = 0.40f + speedNorm * 0.52f + 0.18f * Mathf.Sin(elapsed * 12.8f);
        float tilt = 0.08f * Mathf.Sin(elapsed * 15.7f) + 0.05f * Mathf.Sin(elapsed * 8.4f);
        return new Sample(bob, jitterX, jitterY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateArmored(float elapsed, float speedNorm)
    {
        float stomp = Mathf.Abs(Mathf.Sin(elapsed * (1.18f + speedNorm * 0.34f)));
        float bob = (stomp * 1.65f) - 0.85f;
        float jitterX = Mathf.Sin(elapsed * 1.45f) * 0.08f;
        float jitterY = Mathf.Sin(elapsed * 1.12f) * 0.10f;
        float thrust = 0.18f + speedNorm * 0.19f + 0.05f * Mathf.Sin(elapsed * 4.2f);
        float tilt = 0.014f * Mathf.Sin(elapsed * 1.5f);
        return new Sample(bob, jitterX, jitterY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateReverse(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (2.5f + speedNorm * 0.45f)) * 1.08f;
        float jitterX = (Mathf.Sin(elapsed * 9.8f) + Mathf.Sin(elapsed * 18.4f) * 0.45f) * 0.34f;
        float jitterY = (Mathf.Sin(elapsed * 12.3f) + Mathf.Sin(elapsed * 21.5f) * 0.41f) * 0.26f;
        float thrust = 0.30f + speedNorm * 0.34f + 0.12f * Mathf.Sin(elapsed * 7.7f);
        float tilt = 0.05f * Mathf.Sin(elapsed * 7.2f) + 0.03f * Mathf.Sin(elapsed * 3.8f);
        return new Sample(bob, jitterX, jitterY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    // Gentle hover - implies a hovering relay rather than ground contact
    private static Sample EvaluateShieldDrone(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (1.3f + speedNorm * 0.18f)) * 1.35f;
        float driftX = Mathf.Sin(elapsed * 1.6f) * 0.22f;
        float driftY = Mathf.Sin(elapsed * 2.1f) * 0.14f;
        float thrust = 0.18f + speedNorm * 0.20f + 0.06f * Mathf.Sin(elapsed * 4.8f);
        float tilt = 0.016f * Mathf.Sin(elapsed * 2.0f);
        return new Sample(bob, driftX, driftY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateAnchor(float elapsed, float speedNorm)
    {
        float stomp = Mathf.Abs(Mathf.Sin(elapsed * (1.05f + speedNorm * 0.25f)));
        float bob = (stomp * 1.40f) - 0.72f;
        float jitterX = Mathf.Sin(elapsed * 1.10f) * 0.06f;
        float jitterY = Mathf.Sin(elapsed * 0.95f) * 0.08f;
        float thrust = 0.14f + speedNorm * 0.16f + 0.03f * Mathf.Sin(elapsed * 3.8f);
        float tilt = 0.012f * Mathf.Sin(elapsed * 1.25f);
        return new Sample(bob, jitterX, jitterY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateNullDrone(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (1.45f + speedNorm * 0.22f)) * 1.25f;
        float driftX = Mathf.Sin(elapsed * 1.9f) * 0.26f;
        float driftY = Mathf.Sin(elapsed * 2.5f) * 0.18f;
        float thrust = 0.20f + speedNorm * 0.22f + 0.08f * Mathf.Sin(elapsed * 5.2f);
        float tilt = 0.018f * Mathf.Sin(elapsed * 2.4f);
        return new Sample(bob, driftX, driftY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateLancer(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (2.9f + speedNorm * 0.95f)) * 0.78f;
        float jitterX = (Mathf.Sin(elapsed * 14.3f) + Mathf.Sin(elapsed * 24.1f) * 0.52f) * 0.30f;
        float jitterY = (Mathf.Sin(elapsed * 18.6f) + Mathf.Sin(elapsed * 27.2f) * 0.44f) * 0.32f;
        float thrust = 0.36f + speedNorm * 0.46f + 0.16f * Mathf.Sin(elapsed * 10.6f);
        float tilt = 0.06f * Mathf.Sin(elapsed * 11.8f) + 0.04f * Mathf.Sin(elapsed * 6.2f);
        return new Sample(bob, jitterX, jitterY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }

    private static Sample EvaluateVeil(float elapsed, float speedNorm)
    {
        float bob = Mathf.Sin(elapsed * (2.0f + speedNorm * 0.36f)) * 1.00f;
        float jitterX = (Mathf.Sin(elapsed * 6.8f) + Mathf.Sin(elapsed * 13.9f) * 0.35f) * 0.20f;
        float jitterY = (Mathf.Sin(elapsed * 8.1f) + Mathf.Sin(elapsed * 15.2f) * 0.31f) * 0.18f;
        float thrust = 0.26f + speedNorm * 0.31f + 0.10f * Mathf.Sin(elapsed * 6.9f);
        float tilt = 0.03f * Mathf.Sin(elapsed * 5.9f) + 0.02f * Mathf.Sin(elapsed * 3.1f);
        return new Sample(bob, jitterX, jitterY, Mathf.Max(0f, thrust), tilt, 0f, 0f);
    }
}
