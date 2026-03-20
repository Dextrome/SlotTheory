using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Stacks optional screen-space effects over every scene.
///   Layer 128 - Base filter   : fake bloom, scanlines, saturation
///   Layer 130 - VHS Glitch    : scan-band displacement + channel bleed + grain
///   Layer 131 - Phosphor Grid : screen-door pixel matrix + RGB sub-pixel tint
/// </summary>
public partial class ScreenFilter : Node
{
    public static ScreenFilter? Instance { get; private set; }

    private ColorRect _baseRect    = null!;
    private ColorRect _vhsRect     = null!;
    private ColorRect _phosphorRect = null!;

    private ShaderMaterial _baseMat     = null!;
    private ShaderMaterial _vhsMat      = null!;
    private ShaderMaterial _phosphorMat = null!;

    private float _time = 0f;

    public override void _Ready()
    {
        Instance = this;
        var sm = SettingsManager.Instance;

        _baseRect     = MakeLayer(128, BaseShader(),     sm?.ScreenFilterEnabled ?? true,  out _baseMat);
        _vhsRect      = MakeLayer(130, VhsShader(),      sm?.VhsGlitchEnabled    ?? false, out _vhsMat);
        _phosphorRect = MakeLayer(131, PhosphorShader(), sm?.PhosphorGridEnabled  ?? false, out _phosphorMat);

        SettingsManager.ScreenFilterChanged  += on => _baseRect.Visible     = on;
        SettingsManager.VhsGlitchChanged     += on => _vhsRect.Visible      = on;
        SettingsManager.PhosphorGridChanged  += on => _phosphorRect.Visible = on;
    }

    /// <summary>
    /// Briefly forces the VHS glitch on for <paramref name="duration"/> seconds,
    /// then restores visibility to whatever the persistent setting says.
    /// Safe to call even when VHS is toggled off in settings.
    /// </summary>
    public void FlashVhs(float duration = 0.45f)
    {
        _vhsRect.Visible = true;
        var tween = CreateTween();
        tween.TweenInterval(duration);
        tween.TweenCallback(Callable.From(() =>
        {
            _vhsRect.Visible = SettingsManager.Instance?.VhsGlitchEnabled ?? false;
        }));
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _baseMat.SetShaderParameter("u_time", _time);
        _vhsMat.SetShaderParameter("u_time",  _time);
    }

    // ── Layer factory ────────────────────────────────────────────────────────

    private ColorRect MakeLayer(int layer, string code, bool visible, out ShaderMaterial mat)
    {
        var cl = new CanvasLayer { Layer = layer };
        AddChild(cl);
        var anchor = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        cl.AddChild(anchor);

        var rect = new ColorRect();
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        rect.Visible = visible;

        var shader = new Shader { Code = code };
        mat = new ShaderMaterial { Shader = shader };
        rect.Material = mat;
        anchor.AddChild(rect);
        return rect;
    }

    // ── Shaders ──────────────────────────────────────────────────────────────

    private static string BaseShader() => @"
shader_type canvas_item;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float bloom_strength   : hint_range(0.0, 1.0)    = 0.22;
uniform float bloom_threshold  : hint_range(0.0, 1.0)    = 0.30;
uniform float scanline_opacity : hint_range(0.0, 0.5)    = 0.15;
uniform float saturation       : hint_range(0.5, 2.0)    = 1.18;
uniform float brightness       : hint_range(0.8, 1.3)    = 1.03;
uniform float u_time           : hint_range(0.0, 1000.0) = 0.0;
void fragment() {
    vec2 uv = SCREEN_UV;
    vec3 color = texture(SCREEN_TEXTURE, uv).rgb;
    vec3 blurred = texture(SCREEN_TEXTURE, uv, 3.5).rgb;
    float blurred_lum = dot(blurred, vec3(0.299, 0.587, 0.114));
    float bright_mask = max(0.0, blurred_lum - bloom_threshold) / (1.0 - bloom_threshold);
    color += blurred * bright_mask * bloom_strength;
    float line = step(2.0, mod(floor(uv.y / SCREEN_PIXEL_SIZE.y), 3.0));
    color *= 1.0 - scanline_opacity * line;
    float lum = dot(color, vec3(0.299, 0.587, 0.114));
    color = mix(vec3(lum), color, saturation);
    color *= brightness;
    float shimmer = 1.0 + sin(u_time * 0.7 + uv.y * 220.0) * 0.002;
    color *= shimmer;
    COLOR = vec4(color, 1.0);
}";

    private static string VhsShader() => @"
shader_type canvas_item;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear;
uniform float intensity : hint_range(0.0, 1.0)    = 0.7;
uniform float u_time    : hint_range(0.0, 1000.0) = 0.0;

float rand(vec2 co) {
    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}
void fragment() {
    vec2 uv = SCREEN_UV;
    // Horizontal scan-band displacement
    float band_y    = floor(uv.y * 90.0);
    float band_t    = floor(u_time * 7.0);
    float band_roll = rand(vec2(band_y, band_t));
    float active    = step(0.91, band_roll);
    float dx        = (rand(vec2(band_y + 1.3, band_t)) - 0.5) * 0.022 * active * intensity;
    // Per-channel horizontal bleed
    float r = texture(SCREEN_TEXTURE, vec2(uv.x + dx + 0.003 * intensity, uv.y)).r;
    float g = texture(SCREEN_TEXTURE, vec2(uv.x + dx,                     uv.y)).g;
    float b = texture(SCREEN_TEXTURE, vec2(uv.x + dx - 0.003 * intensity, uv.y)).b;
    // Film grain
    float grain = (rand(uv + fract(u_time * 0.17)) - 0.5) * 0.07 * intensity;
    vec3 color = vec3(r, g, b) + grain;
    // Occasional full-width bright noise bar
    float bar_t    = floor(u_time * 1.5);
    float bar_y    = rand(vec2(bar_t, 3.7));
    float bar_h    = 0.012;
    float bar_mask = step(abs(uv.y - bar_y), bar_h) * rand(vec2(bar_t, 9.1)) * 0.4 * intensity;
    color += bar_mask;
    COLOR = vec4(color, 1.0);
}";

    private static string PhosphorShader() => @"
shader_type canvas_item;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_nearest;
uniform float grid_opacity : hint_range(0.0, 1.0) = 0.45;
uniform float pixel_scale  : hint_range(1.0, 4.0) = 2.0;
void fragment() {
    vec2 uv = SCREEN_UV;
    vec3 color = texture(SCREEN_TEXTURE, uv).rgb;
    // Screen-door grid between virtual pixels
    vec2 cell = fract(uv / (SCREEN_PIXEL_SIZE * pixel_scale));
    float border = max(
        step(1.0 - 0.3 / pixel_scale, cell.x),
        step(1.0 - 0.3 / pixel_scale, cell.y)
    );
    color *= 1.0 - border * grid_opacity;
    // RGB phosphor sub-pixel column tint (R / G / B cycling every pixel)
    float col = mod(floor(uv.x / SCREEN_PIXEL_SIZE.x), 3.0);
    vec3 mask = vec3(
        0.85 + 0.15 * step(col, 0.5),
        0.85 + 0.15 * (1.0 - abs(sign(col - 1.0))),
        0.85 + 0.15 * step(1.5, col)
    );
    color *= mask;
    COLOR = vec4(color, 1.0);
}";
}
