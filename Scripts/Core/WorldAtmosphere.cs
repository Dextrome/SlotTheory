using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Shader-backed atmospheric backdrop that sits behind map geometry.
/// </summary>
public partial class WorldAtmosphere : Node2D
{
    private const float BgHalfExtent = 4096f;

    private ShaderMaterial? _shaderMaterial;
    private bool _hasShader;
    private float _time;
    private bool _reducedMotion;

    public override void _Ready()
    {
        var shader = GD.Load<Shader>("res://Assets/Shaders/world_atmosphere.gdshader");
        if (shader != null)
        {
            _shaderMaterial = new ShaderMaterial { Shader = shader };
            Material = _shaderMaterial;
            _hasShader = true;
        }

        SyncReducedMotion(forceApply: true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        bool motionChanged = SyncReducedMotion();

        if (!_reducedMotion)
        {
            float mobileSpeedMul = MobileOptimization.IsMobile() ? 0.68f : 1f;
            _time += (float)delta * mobileSpeedMul;
            ApplyShaderParams();
            QueueRedraw();
            return;
        }

        if (motionChanged)
        {
            ApplyShaderParams();
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(
            new Rect2(-BgHalfExtent, -BgHalfExtent, BgHalfExtent * 2f, BgHalfExtent * 2f),
            _hasShader ? Colors.White : new Color(0.03f, 0.01f, 0.09f)
        );
    }

    private bool SyncReducedMotion(bool forceApply = false)
    {
        bool reducedMotion = SettingsManager.Instance?.ReducedMotion ?? false;
        bool changed = reducedMotion != _reducedMotion || forceApply;
        _reducedMotion = reducedMotion;
        if (changed)
            ApplyShaderParams();
        return changed;
    }

    private void ApplyShaderParams()
    {
        if (_shaderMaterial == null)
            return;

        _shaderMaterial.SetShaderParameter("u_time", _time);
        _shaderMaterial.SetShaderParameter("u_motion_scale", _reducedMotion ? 0f : 1f);
        _shaderMaterial.SetShaderParameter("u_mobile_soften", MobileOptimization.IsMobile() ? 1f : 0f);
    }
}
