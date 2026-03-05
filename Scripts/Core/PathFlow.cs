using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Draws animated chevrons that scroll along the enemy path to show movement direction.
/// Added as a child of _mapVisuals so it renders on top of the road.
/// </summary>
public partial class PathFlow : Node2D
{
    private Vector2[] _waypoints = System.Array.Empty<Vector2>();
    private float     _phase;
    private float _surgeTimeRemaining = 0f;
    private float _surgeTotal = 1f;

    private const float Speed   = 36f;   // px/s
    private const float Spacing = 48f;   // px between chevrons
    private const float Size    =  7f;   // chevron arm length
    private const float Arm     = 2.3f;  // half-angle of chevron arms (radians)

    public void Initialize(Vector2[] waypoints) => _waypoints = waypoints;

    public void TriggerSurge(float duration = 1.0f)
    {
        _surgeTotal = Mathf.Max(0.05f, duration);
        _surgeTimeRemaining = Mathf.Max(_surgeTimeRemaining, duration);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_surgeTimeRemaining > 0f)
            _surgeTimeRemaining = Mathf.Max(0f, _surgeTimeRemaining - dt);

        float speedMul = _surgeTimeRemaining > 0f ? 1.85f : 1f;
        _phase = (_phase + Speed * speedMul * dt) % Spacing;
        QueueRedraw();
    }

    public override void _Draw()
    {
        for (int i = 0; i + 1 < _waypoints.Length; i++)
            DrawSegment(_waypoints[i], _waypoints[i + 1]);
    }

    private void DrawSegment(Vector2 a, Vector2 b)
    {
        float segLen = a.DistanceTo(b);
        if (segLen < 1f) return;

        var   dir   = (b - a).Normalized();
        float angle = dir.Angle();
        float surgeT = _surgeTimeRemaining <= 0f ? 0f : (_surgeTimeRemaining / _surgeTotal);
        float surgeAmp = 1f + 0.9f * surgeT;
        var color = new Color(0.00f, 0.94f, 1.00f, 0.40f * surgeAmp);
        float width = 1.5f * (1f + 0.32f * surgeT);

        float t = _phase;
        while (t < segLen)
        {
            Vector2 pos = a + dir * t;
            DrawChevron(pos, angle, color, width);
            t += Spacing;
        }
    }

    private void DrawChevron(Vector2 pos, float angle, Color color, float width)
    {
        var tip  = pos + new Vector2(Mathf.Cos(angle),       Mathf.Sin(angle))       * Size * 0.55f;
        var left = pos + new Vector2(Mathf.Cos(angle + Arm), Mathf.Sin(angle + Arm)) * Size;
        var rght = pos + new Vector2(Mathf.Cos(angle - Arm), Mathf.Sin(angle - Arm)) * Size;
        DrawLine(left, tip, color, width);
        DrawLine(rght, tip, color, width);
    }
}
