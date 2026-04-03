using Godot;
using System;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// A small energy pip that flies from a surging tower's screen position
/// to the Global Surge HUD bar, then fires a callback on arrival.
/// Hosted in a CanvasLayer so it renders in screen/viewport coordinates,
/// bridging world-space tower positions to the HUD bar.
/// </summary>
public partial class SurgePip : Node2D
{
	public enum SurgePipGlyph
	{
		Orb,
		Diamond,
		Square,
	}

	// Config
	private Vector2 _start;
	private Vector2 _target;
	private Color _color;
	private float _lingerSec;
	private float _travelSec;
	private float _arcHeight;
	private Action? _onArrival;
	private SurgePipGlyph _glyph = SurgePipGlyph.Orb;
	private float _coreScale = 1f;
	private float _glowScale = 1f;

	// State
	private float _elapsed;
	private bool _done;

	/// <summary>
	/// Must be called immediately after adding the node to the tree.
	/// </summary>
	/// <param name="screenStart">Tower screen position (viewport pixels).</param>
	/// <param name="screenTarget">Surge bar center in screen coordinates.</param>
	/// <param name="color">Dominant mod accent color.</param>
	/// <param name="lingerSec">Seconds to dwell at source before flying.</param>
	/// <param name="travelSec">Flight duration in seconds.</param>
	/// <param name="arcHeight">Upward arc offset in screen pixels (positive = up).</param>
	/// <param name="onArrival">Called once when the pip reaches the bar.</param>
	/// <param name="glyph">Readability shape so color is not the only cue.</param>
	/// <param name="coreScale">Scale multiplier for the core shape.</param>
	/// <param name="glowScale">Scale multiplier for outer glow.</param>
	public void Initialize(
		Vector2 screenStart,
		Vector2 screenTarget,
		Color color,
		float lingerSec,
		float travelSec,
		float arcHeight,
		Action? onArrival,
		SurgePipGlyph glyph = SurgePipGlyph.Orb,
		float coreScale = 1f,
		float glowScale = 1f)
	{
		_start     = screenStart;
		_target    = screenTarget;
		_color     = color;
		_lingerSec = lingerSec;
		_travelSec = travelSec;
		_arcHeight = arcHeight;
		_onArrival = onArrival;
		_glyph = glyph;
		_coreScale = Mathf.Clamp(coreScale, 0.55f, 1.75f);
		_glowScale = Mathf.Clamp(glowScale, 0.55f, 1.90f);
		Position   = screenStart;
	}

	public override void _Process(double delta)
	{
		if (_done) return;
		_elapsed += (float)delta;

		if (_elapsed < _lingerSec)
		{
			// Linger phase: stay at source, pulse visually
			QueueRedraw();
			return;
		}

		float t = Mathf.Clamp((_elapsed - _lingerSec) / _travelSec, 0f, 1f);
		if (t >= 1f)
		{
			if (!_done)
			{
				_done = true;
				_onArrival?.Invoke();
				QueueFree();
			}
			return;
		}

		// Quadratic bezier arc: source → arced control point → HUD bar target
		// Control point sits above the midpoint so the pip traces a clean upward arc.
		Vector2 ctrl = (_start + _target) * 0.5f + new Vector2(0f, -_arcHeight);
		float mt = 1f - t;
		Position = mt * mt * _start + 2f * mt * t * ctrl + t * t * _target;
		QueueRedraw();
	}

	public override void _Draw()
	{
		float travelT = _elapsed <= _lingerSec
			? 0f
			: Mathf.Clamp((_elapsed - _lingerSec) / _travelSec, 0f, 1f);

		bool lingering = _elapsed <= _lingerSec && _lingerSec > 0f;

		// Fade out over the last 20% of travel so it melts into the bar
		float alpha = travelT > 0.80f ? Mathf.InverseLerp(1f, 0.80f, travelT) : 1f;

		// Scale: breathe bigger during linger, gently shrink during travel
		float scale;
		if (lingering)
		{
			float pt = _elapsed / _lingerSec;
			// Start at 1.3×, ease to 1.5× at midpoint, settle back to 1.35× -- held-charge feel
			scale = 1.3f + 0.20f * Mathf.Sin(pt * Mathf.Pi);
		}
		else
		{
			scale = Mathf.Lerp(1.35f, 0.52f, travelT);
		}

		float core = Balance.SurgePipCoreRadius * scale * _coreScale;
		float glow = Balance.SurgePipGlowRadius * scale * _glowScale;

		if (lingering)
		{
			float pt = _elapsed / _lingerSec;
			// Linger: wide bloom + bright saturated core to read clearly as "charged and waiting"
			float bloom = glow * (1.8f + 0.4f * Mathf.Sin(pt * Mathf.Pi * 2f));
			DrawCircle(Vector2.Zero, bloom,
				new Color(_color.R, _color.G, _color.B, 0.18f));
			DrawCircle(Vector2.Zero, glow * 1.1f,
				new Color(_color.R, _color.G, _color.B, 0.50f));
			DrawCoreShape(core * 1.55f, new Color(_color.R, _color.G, _color.B, 0.78f));
			DrawCoreShape(core, new Color(_color.R, _color.G, _color.B, 1.00f));
			// Pure white center flash -- clearly "energy charging"
			DrawCircle(Vector2.Zero, core * 0.48f,
				new Color(1f, 1f, 1f, 1.00f));
		}
		else
		{
			// Travel: shrinking dot, fades to merge with bar
			DrawCircle(Vector2.Zero, glow,
				new Color(_color.R, _color.G, _color.B, 0.22f * alpha));
			DrawCoreShape(core * 1.55f, new Color(_color.R, _color.G, _color.B, 0.50f * alpha));
			DrawCoreShape(core, new Color(_color.R, _color.G, _color.B, 0.90f * alpha));
			DrawCircle(Vector2.Zero, core * 0.40f,
				new Color(1f, 1f, 1f, 0.88f * alpha));
		}
	}

	private void DrawCoreShape(float radius, Color color)
	{
		switch (_glyph)
		{
			case SurgePipGlyph.Diamond:
			{
				Vector2[] points =
				{
					new Vector2(0f, -radius),
					new Vector2(radius, 0f),
					new Vector2(0f, radius),
					new Vector2(-radius, 0f),
				};
				DrawColoredPolygon(points, color);
				break;
			}
			case SurgePipGlyph.Square:
			{
				float side = radius * 0.95f;
				DrawRect(new Rect2(-side, -side, side * 2f, side * 2f), color);
				break;
			}
			default:
				DrawCircle(Vector2.Zero, radius, color);
				break;
		}
	}
}
