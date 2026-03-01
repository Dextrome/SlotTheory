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

    private const float Speed   = 36f;   // px/s
    private const float Spacing = 48f;   // px between chevrons
    private const float Size    =  7f;   // chevron arm length
    private const float Arm     = 2.3f;  // half-angle of chevron arms (radians)

    public void Initialize(Vector2[] waypoints) => _waypoints = waypoints;

    public override void _Process(double delta)
    {
        _phase = (_phase + Speed * (float)delta) % Spacing;
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
        var   color = new Color(1f, 1f, 1f, 0.18f);

        float t = _phase;
        while (t < segLen)
        {
            Vector2 pos = a + dir * t;
            DrawChevron(pos, angle, color);
            t += Spacing;
        }
    }

    private void DrawChevron(Vector2 pos, float angle, Color color)
    {
        var tip  = pos + new Vector2(Mathf.Cos(angle),       Mathf.Sin(angle))       * Size * 0.55f;
        var left = pos + new Vector2(Mathf.Cos(angle + Arm), Mathf.Sin(angle + Arm)) * Size;
        var rght = pos + new Vector2(Mathf.Cos(angle - Arm), Mathf.Sin(angle - Arm)) * Size;
        DrawLine(left, tip, color, 1.5f);
        DrawLine(rght, tip, color, 1.5f);
    }
}
