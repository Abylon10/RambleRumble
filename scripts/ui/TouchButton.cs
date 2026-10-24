using System;
using Godot;

namespace RambleRumble.UI;

/// <summary>
/// Round action button that supports multitouch (unlike Button, which only
/// sees the first finger through mouse emulation). <see cref="Held"/> stays
/// true while pressed so the HUD can repeat actions like the basic attack.
/// </summary>
public partial class TouchButton : Control
{
    public event Action? Tapped;

    public string Label { get; set; } = "";
    public Color Tint { get; set; } = Colors.White;
    /// <summary>Drawn inside the circle instead of the label when set.</summary>
    public Texture2D? Icon { get; set; }
    /// <summary>1.0 = just used, 0.0 = ready. Drawn as a dark sweep.</summary>
    public float CooldownRatio { get; set; }
    public bool Held { get; private set; }

    private int _touchIndex = -1;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || @event is not InputEventScreenTouch touch)
            return;
        var center = GetGlobalRect().GetCenter();
        if (touch.Pressed && _touchIndex == -1 && touch.Position.DistanceTo(center) <= Size.X * 0.5f)
        {
            _touchIndex = touch.Index;
            Held = true;
            Tapped?.Invoke();
            GetViewport().SetInputAsHandled();
        }
        else if (!touch.Pressed && touch.Index == _touchIndex)
        {
            _touchIndex = -1;
            Held = false;
        }
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var center = Size * 0.5f;
        float radius = Size.X * 0.5f;
        DrawCircle(center, radius, new Color(0, 0, 0, Held ? 0.6f : 0.45f));
        DrawArc(center, radius - 2, 0f, Mathf.Tau, 48, Tint, 4f);
        if (Icon != null)
        {
            float side = radius * 1.25f;
            var iconRect = new Rect2(center - new Vector2(side, side) / 2, new Vector2(side, side));
            DrawTextureRect(Icon, iconRect, false);
        }
        if (CooldownRatio > 0f)
        {
            const int steps = 32;
            var points = new Vector2[steps + 2];
            points[0] = center;
            for (int i = 0; i <= steps; i++)
            {
                float angle = -Mathf.Pi / 2 + Mathf.Tau * CooldownRatio * i / steps;
                points[i + 1] = center + Vector2.FromAngle(angle) * (radius - 4);
            }
            DrawColoredPolygon(points, new Color(0, 0, 0, 0.55f));
        }
        if (Icon != null)
            return;
        int fontSize = Size.X > 110 ? 22 : 17;
        DrawString(ThemeDB.FallbackFont, new Vector2(0, center.Y + fontSize * 0.35f), Label, HorizontalAlignment.Center, Size.X, fontSize, Colors.White);
    }
}
