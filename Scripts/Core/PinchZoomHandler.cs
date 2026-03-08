using System.Collections.Generic;
using Godot;

/// <summary>
/// Pinch-to-zoom + single-finger pan for a target Control node on mobile.
/// Zoom: two-finger pinch (manual tracking, works on all Android).
/// Pan: single-finger drag while zoomed in, via Control offset adjustments
///      so the background stays static — no gray edges.
/// </summary>
public partial class PinchZoomHandler : Node
{
	private readonly Control _target;

	private float _baseScale;
	private float _minScale;
	private float _maxScale;

	// Initial Control offsets captured at _Ready so pan can be applied on top
	private float _initOffsetLeft;
	private float _initOffsetTop;
	private float _initOffsetRight;
	private float _initOffsetBottom;
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

		// Capture initial offsets — handles cases like MapSelect's OffsetBottom = -72
		_initOffsetLeft   = _target.OffsetLeft;
		_initOffsetTop    = _target.OffsetTop;
		_initOffsetRight  = _target.OffsetRight;
		_initOffsetBottom = _target.OffsetBottom;
	}

	public override void _Input(InputEvent @event)
	{
		// Platform pinch gesture (desktop trackpads, some Android)
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
				// Two-finger zoom
				var pts = new List<Vector2>(_activeTouches.Values);
				float dist = pts[0].DistanceTo(pts[1]);
				ApplyScale(_pinchStartScale * (dist / _pinchStartDist));
				GetViewport().SetInputAsHandled();
			}
			else if (_activeTouches.Count == 1 && _panActive)
			{
				// Single-finger pan while zoomed in
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
			ApplyPan();
		}
	}

	/// <summary>
	/// Shifts all four Control offsets by equal amounts — moves the control
	/// without changing its size, and without touching the CanvasLayer.
	/// </summary>
	private void ApplyPan()
	{
		_target.OffsetLeft   = _initOffsetLeft   + _panOffset.X;
		_target.OffsetRight  = _initOffsetRight  + _panOffset.X;
		_target.OffsetTop    = _initOffsetTop    + _panOffset.Y;
		_target.OffsetBottom = _initOffsetBottom + _panOffset.Y;
	}

	private void ClampPan()
	{
		float zoomFactor = _target.Scale.X / _baseScale;
		var vpSize = GetViewport().GetVisibleRect().Size;
		float maxX = vpSize.X * (zoomFactor - 1f) * 0.5f;
		float maxY = vpSize.Y * (zoomFactor - 1f) * 0.5f;
		_panOffset.X = Mathf.Clamp(_panOffset.X, -maxX, maxX);
		_panOffset.Y = Mathf.Clamp(_panOffset.Y, -maxY, maxY);
	}
}
