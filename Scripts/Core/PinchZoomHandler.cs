using System.Collections.Generic;
using Godot;

/// <summary>
/// Pinch-to-zoom + single-finger pan for a target Control node on mobile.
/// Zoom: two-finger pinch (manual tracking, works on all Android).
/// Pan: shifting PivotOffset while zoomed - no layout/offset manipulation,
///      no gray edges, no anchor-system conflicts.
/// </summary>
public partial class PinchZoomHandler : Node
{
	private readonly Control _target;

	private float _baseScale;
	private float _minScale;
	private float _maxScale;

	// Pan offset in screen space (accumulated drag delta)
	private Vector2 _panOffset = Vector2.Zero;

	// Two-finger pinch
	private readonly Dictionary<int, Vector2> _activeTouches = new();
	private float _pinchStartDist = -1f;
	private float _pinchStartScale = 1f;

	// Single-finger pan (only when zoomed in)
	private bool _panActive = false;

	public PinchZoomHandler(Control target)
	{
		_target = target;
	}

	public override void _Ready()
	{
		if (!MobileOptimization.IsMobile())
		{
			SetProcessInput(false);
			return;
		}
		_baseScale = Mathf.Max(_target.Scale.X, MobileOptimization.GetUIScale());
		_minScale = _baseScale * 0.55f;
		_maxScale = _baseScale * 2.0f;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMagnifyGesture magnify)
		{
			ApplyScale(_target.Scale.X * magnify.Factor);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event is InputEventScreenTouch touch)
		{
			if (touch.Pressed)
			{
				_activeTouches[touch.Index] = touch.Position;
				if (_activeTouches.Count == 2)
				{
					BeginPinch();
					_panActive = false;
				}
				else if (_activeTouches.Count == 1)
				{
					_panActive = IsZoomedIn();
				}
			}
			else
			{
				_activeTouches.Remove(touch.Index);
				_pinchStartDist = -1f;
				if (_activeTouches.Count == 0)
					_panActive = false;
				else if (_activeTouches.Count == 1)
					_panActive = IsZoomedIn(); // remaining finger can pan after pinch ends
			}
		}
		else if (@event is InputEventScreenDrag drag)
		{
			if (!_activeTouches.ContainsKey(drag.Index)) return;
			_activeTouches[drag.Index] = drag.Position;

			if (_activeTouches.Count == 2 && _pinchStartDist > 0f)
			{
				var pts = new List<Vector2>(_activeTouches.Values);
				float dist = pts[0].DistanceTo(pts[1]);
				ApplyScale(_pinchStartScale * (dist / _pinchStartDist));
				GetViewport().SetInputAsHandled();
			}
			else if (_activeTouches.Count == 1 && _panActive)
			{
				_panOffset += drag.Relative;
				ClampPan();
				ApplyPan();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private bool IsZoomedIn() => _target.Scale.X > _baseScale * 1.05f;

	private void BeginPinch()
	{
		var pts = new List<Vector2>(_activeTouches.Values);
		_pinchStartDist = pts[0].DistanceTo(pts[1]);
		_pinchStartScale = _target.Scale.X;
	}

	private void ApplyScale(float raw)
	{
		float s = Mathf.Clamp(raw, _minScale, _maxScale);
		_target.PivotOffset = _target.Size * 0.5f;
		_target.Scale = new Vector2(s, s);

		if (!IsZoomedIn())
		{
			_panOffset = Vector2.Zero;
			// Reset pivot to center
			_target.PivotOffset = _target.Size * 0.5f;
		}
		else
		{
			// Re-apply pan so pivot stays consistent after zoom change
			ClampPan();
			ApplyPan();
		}
	}

	/// <summary>
	/// Pan by shifting PivotOffset. For a control zoomed at scale S from its center,
	/// shifting the pivot by delta/-(S-1) in local space produces a delta shift in screen space.
	/// </summary>
	private void ApplyPan()
	{
		float s = _target.Scale.X;
		if (s <= 1f) return;
		var center = _target.Size * 0.5f;
		_target.PivotOffset = center - _panOffset / (s - 1f);
	}

	private void ClampPan()
	{
		float s = _target.Scale.X;
		if (s <= 1f) return;
		// PivotOffset must stay in [0, size] to avoid showing outside content bounds
		// pivot = center - panOffset / (s - 1)
		// panOffset = center*(s-1) - pivot*(s-1); valid when pivot in [0, size]
		// => panOffset in [-(size/2)*(s-1), (size/2)*(s-1)]
		var sz = _target.Size;
		float maxX = sz.X * 0.5f * (s - 1f);
		float maxY = sz.Y * 0.5f * (s - 1f);
		_panOffset.X = Mathf.Clamp(_panOffset.X, -maxX, maxX);
		_panOffset.Y = Mathf.Clamp(_panOffset.Y, -maxY, maxY);
	}
}
