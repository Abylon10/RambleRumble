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

    // Setters redraw only on change; redrawing every frame re-shaped the label text each time.
    public string Label { get => _label; set => SetAndRedraw(ref _label, value); }
    public Color Tint { get => _tint; set => SetAndRedraw(ref _tint, value); }
    /// <summary>Drawn inside the circle instead of the label when set.</summary>
    public Texture2D? Icon { get => _icon; set => SetAndRedraw(ref _icon, value); }
    /// <summary>1.0 = just used, 0.0 = ready. Drawn as a dark sweep.</summary>
    public float CooldownRatio { get => _cooldownRatio; set => SetAndRedraw(ref _cooldownRatio, value); }
    public bool Held { get => _held; private set => SetAndRedraw(ref _held, value); }

    private string _label = "";
    private Color _tint = Colors.White;
    private Texture2D? _icon;
    private float _cooldownRatio;
    private bool _held;

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

    private void SetAndRedraw<T>(ref T field, T value)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        float radius = Size.X * 0.5f;
        Shapes.Circle(this, center, radius, new Color(0, 0, 0, Held ? 0.6f : 0.45f));
        Shapes.Ring(this, center, radius - 1, Tint, thin: true);
        if (Icon != null)
        {
            float side = radius * 1.25f;
            var iconRect = new Rect2(center - new Vector2(side, side) / 2, new Vector2(side, side));
            DrawTextureRect(Icon, iconRect, false);
        }
        if (CooldownRatio > 0.01f)
        {
            // A thick arc reaching the centre draws the pie. (A pie polygon nearly closes on
            // itself right after a cast and then fails to triangulate.)
            float inner = (radius - 4) / 2f;
            int steps = Mathf.Max(2, (int)(48 * CooldownRatio));
            DrawArc(center, inner, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * CooldownRatio, steps, new Color(0, 0, 0, 0.55f), inner * 2f);
        }
        if (Icon != null)
            return;
        int fontSize = Size.X > 110 ? 22 : 17;
        DrawString(ThemeDB.FallbackFont, new Vector2(0, center.Y + fontSize * 0.35f), Label, HorizontalAlignment.Center, Size.X, fontSize, Colors.White);
    }
}
