using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Stylized combo glyph burst so surge skins are clearly distinct in combat.
/// </summary>
public partial class SpectacleSkinGlyph : Node2D
{
	private ComboExplosionSkin _skin = ComboExplosionSkin.Default;
	private Color _color = new Color(0.90f, 0.96f, 1.00f, 1f);
	private float _duration = 0.52f;
	private float _life;
	private float _scale = 1f;

	public void Initialize(ComboExplosionSkin skin, Color color, float durationSec = 0.52f, float scale = 1f)
	{
		_skin = skin;
		_color = color;
		_duration = Mathf.Clamp(durationSec, 0.12f, 1.4f);
		_scale = Mathf.Clamp(scale, 0.4f, 2.4f);
	}

	public override void _Process(double delta)
	{
		_life += (float)delta;
		if (_life >= _duration)
		{
			QueueFree();
			return;
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		float t = Mathf.Clamp(_life / _duration, 0f, 1f);
		float fade = 1f - t;
		float ease = 1f - Mathf.Pow(1f - t, 2f);
		float baseRadius = Mathf.Lerp(20f, 78f, ease) * _scale;
		Color core = new Color(_color.R, _color.G, _color.B, 0.50f * fade);
		Color hot = new Color(1f, 1f, 1f, 0.30f * fade);

		switch (_skin)
		{
			case ComboExplosionSkin.ChillShatter:
				DrawArc(Vector2.Zero, baseRadius, 0f, Mathf.Tau, 56, core, 2.6f);
				for (int i = 0; i < 6; i++)
				{
					float a = i * Mathf.Tau / 6f + t * 0.12f;
					Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					Vector2 p0 = dir * (baseRadius * 0.22f);
					Vector2 p1 = dir * baseRadius;
					DrawLine(p0, p1, core, 2.0f);
					Vector2 branch = new Vector2(-dir.Y, dir.X) * (baseRadius * 0.13f);
					DrawLine(p1 - dir * (baseRadius * 0.18f), p1 - dir * (baseRadius * 0.08f) + branch, hot, 1.5f);
					DrawLine(p1 - dir * (baseRadius * 0.18f), p1 - dir * (baseRadius * 0.08f) - branch, hot, 1.5f);
				}
				break;

			case ComboExplosionSkin.ChainArc:
				for (int i = 0; i < 4; i++)
				{
					float a = i * Mathf.Tau / 4f + t * 0.25f;
					Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
					Vector2 p0 = dir * (baseRadius * 0.12f);
					Vector2 p1 = dir * (baseRadius * 0.52f);
					Vector2 p2 = dir * baseRadius;
					Vector2 n = new Vector2(-dir.Y, dir.X);
					DrawLine(p0, p1 + n * (baseRadius * 0.12f), core, 2.2f);
					DrawLine(p1 + n * (baseRadius * 0.12f), p2 - n * (baseRadius * 0.10f), core, 2.2f);
				}
				DrawCircle(Vector2.Zero, baseRadius * 0.16f, hot);
				break;

			case ComboExplosionSkin.SplitShrapnel:
				for (int i = 0; i < 3; i++)
				{
					float mid = -0.62f + i * 0.62f;
					float half = 0.22f + 0.03f * i;
					float a0 = mid - half;
					float a1 = mid + half;
					Vector2 v0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * baseRadius;
					Vector2 v1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * baseRadius;
					DrawLine(Vector2.Zero, v0, core, 2.0f);
					DrawLine(Vector2.Zero, v1, core, 2.0f);
					DrawLine(v0, v1, hot, 1.5f);
				}
				DrawCircle(Vector2.Zero, baseRadius * 0.14f, core);
				break;

			case ComboExplosionSkin.FocusImplosion:
				{
					float innerR = Mathf.Lerp(baseRadius * 0.92f, baseRadius * 0.22f, ease);
					for (int i = 0; i < 8; i++)
					{
						float a = i * Mathf.Tau / 8f;
						Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
						DrawLine(dir * baseRadius, dir * innerR, core, 1.8f);
					}
					DrawCircle(Vector2.Zero, baseRadius * 0.16f + ease * 8f, hot);
				}
				break;

			default:
				DrawArc(Vector2.Zero, baseRadius, 0f, Mathf.Tau, 48, core, 2.0f);
				break;
		}
	}
}
