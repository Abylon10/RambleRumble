using Godot;

namespace RambleRumble;

/// <summary>Pulsing marker where the story swarm gathers. World shows it while that quest is active.</summary>
public partial class SwarmBeacon : Node2D
{
    private float _t;

    public override void _Ready()
    {
        var label = new Label
        {
            Text = "SWARM",
            HorizontalAlignment = HorizontalAlignment.Center,
            Size = new Vector2(200, 30),
            Position = new Vector2(-100, -150),
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.6f, 1f));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        label.AddThemeConstantOverride("outline_size", 6);
        AddChild(label);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 3f);
        Shapes.Circle(this, Vector2.Zero, 110f, new Color(0.55f, 0.25f, 0.85f, 0.12f + 0.1f * pulse));
        Shapes.Ring(this, Vector2.Zero, 90f + 30f * pulse, new Color(0.8f, 0.55f, 1f, 0.8f - 0.5f * pulse));
    }
}
