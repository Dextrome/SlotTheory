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
	// Config
	private Vector2 _start;
	private Vector2 _target;
	private Color _color;
	private float _lingerSec;
	private float _travelSec;
	private float _arcHeight;
	private Action? _onArrival;

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
	public void Initialize(
		Vector2 screenStart,
		Vector2 screenTarget,
		Color color,
		float lingerSec,
		float travelSec,
		float arcHeight,
		Action? onArrival)
	{
		_start     = screenStart;
		_target    = screenTarget;
		_color     = color;
		_lingerSec = lingerSec;
		_travelSec = travelSec;
		_arcHeight = arcHeight;
		_onArrival = onArrival;
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

		float core = Balance.SurgePipCoreRadius * scale;
		float glow = Balance.SurgePipGlowRadius * scale;

		if (lingering)
		{
			float pt = _elapsed / _lingerSec;
			// Linger: wide bloom + bright saturated core to read clearly as "charged and waiting"
			float bloom = glow * (1.8f + 0.4f * Mathf.Sin(pt * Mathf.Pi * 2f));
			DrawCircle(Vector2.Zero, bloom,
				new Color(_color.R, _color.G, _color.B, 0.18f));
			DrawCircle(Vector2.Zero, glow * 1.1f,
				new Color(_color.R, _color.G, _color.B, 0.50f));
			DrawCircle(Vector2.Zero, core * 1.55f,
				new Color(_color.R, _color.G, _color.B, 0.78f));
			DrawCircle(Vector2.Zero, core,
				new Color(_color.R, _color.G, _color.B, 1.00f));
			// Pure white center flash -- clearly "energy charging"
			DrawCircle(Vector2.Zero, core * 0.48f,
				new Color(1f, 1f, 1f, 1.00f));
		}
		else
		{
			// Travel: shrinking dot, fades to merge with bar
			DrawCircle(Vector2.Zero, glow,
				new Color(_color.R, _color.G, _color.B, 0.22f * alpha));
			DrawCircle(Vector2.Zero, core * 1.55f,
				new Color(_color.R, _color.G, _color.B, 0.50f * alpha));
			DrawCircle(Vector2.Zero, core,
				new Color(_color.R, _color.G, _color.B, 0.90f * alpha));
			DrawCircle(Vector2.Zero, core * 0.40f,
				new Color(1f, 1f, 1f, 0.88f * alpha));
		}
	}
}
