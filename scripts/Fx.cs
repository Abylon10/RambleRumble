using Godot;

namespace RambleRumble;

public enum FxKind
{
    Ring,
    Circle,
    Line,
    Text,
}

/// <summary>
/// Short-lived visual effect: an expanding ring, a filled circle, a line, or
/// floating text. Placeholder art until real effects arrive.
/// </summary>
public partial class Fx : Node2D
{
    public FxKind Kind { get; set; } = FxKind.Ring;
    public Color Tint { get; set; } = Colors.White;
    public float Radius { get; set; } = 40f;
    public Vector2 To { get; set; }
    public string Text { get; set; } = "";
    public float Duration { get; set; } = 0.3f;

    private float _t;

    public static Fx Spawn(Node parent, Vector2 position, FxKind kind, Color? tint = null,
        float radius = 40f, float duration = 0.3f, string text = "", Vector2 to = default)
    {
        var fx = new Fx
        {
            Kind = kind,
            Tint = tint ?? Colors.White,
            Radius = radius,
            Duration = duration,
            Text = text,
            To = to,
            Position = position,
            ZIndex = 10,
        };
        parent.AddChild(fx);
        return fx;
    }

    public static Fx FloatingText(Node parent, Vector2 position, string text, Color tint, float duration = 0.6f) =>
        Spawn(parent, position, FxKind.Text, tint, duration: duration, text: text);

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (Kind == FxKind.Text)
            Position += new Vector2(0, -45f * (float)delta);
        QueueRedraw();
        if (_t >= Duration)
            QueueFree();
    }

    public override void _Draw()
    {
        float fade = Mathf.Clamp(1f - _t / Duration, 0f, 1f);
        var color = new Color(Tint, fade);
        switch (Kind)
        {
            case FxKind.Ring:
                DrawArc(Vector2.Zero, Radius * (0.6f + 0.4f * (1f - fade)), 0f, Mathf.Tau, 40, color, 5f);
                break;
            case FxKind.Circle:
                DrawCircle(Vector2.Zero, Radius, new Color(Tint, fade * 0.55f));
                break;
            case FxKind.Line:
                DrawLine(Vector2.Zero, To, color, 8f);
                break;
            case FxKind.Text:
                DrawString(ThemeDB.FallbackFont, new Vector2(-60, 0), Text, HorizontalAlignment.Center, 120, 20, color);
                break;
        }
    }
}
