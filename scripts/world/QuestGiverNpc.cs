using Godot;

namespace RambleRumble;

/// <summary>
/// Garrick, the aging instructor who gives the story quests. Shows "!" when a
/// quest is waiting to be accepted and "?" when one is ready to hand in.
/// World handles talking.
/// </summary>
public partial class QuestGiverNpc : Node2D
{
    private static readonly Color Robe = new(0.55f, 0.30f, 0.22f);
    private static readonly Color Skin = new(0.95f, 0.80f, 0.65f);
    private static readonly Color Hair = new(0.92f, 0.92f, 0.90f);

    private Label _marker = null!;
    private float _bob;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        var name = MakeLabel(Quests.GiverName, 16, Colors.White);
        name.Position = new Vector2(-100, -78);
        AddChild(name);
        _marker = MakeLabel("!", 34, new Color(1f, 0.85f, 0.2f));
        _marker.Position = new Vector2(-100, -122);
        AddChild(_marker);
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, Size = new Vector2(200, 30) };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    public override void _Process(double delta)
    {
        _bob += (float)delta * 3f;
        bool offering = Data.HasStarter && Data.CurrentQuest != null && !Data.QuestAccepted;
        _marker.Visible = offering || Data.QuestReady;
        _marker.Text = Data.QuestReady ? "?" : "!";
        _marker.Position = new Vector2(-100, -122 + Mathf.Sin(_bob) * 4f);
    }

    public override void _Draw()
    {
        Shapes.Ellipse(this, new Vector2(0, 22), new Vector2(20, 7), new Color(0, 0, 0, 0.3f));
        // Tunic, head, grey hair and beard; his old sword.
        DrawColoredPolygon(new[] { new Vector2(-18, 22), new Vector2(-10, -18), new Vector2(10, -18), new Vector2(18, 22) }, Robe);
        Shapes.Circle(this, new Vector2(0, -28), 13f, Skin);
        Shapes.Circle(this, new Vector2(0, -36), 12f, Hair);
        Shapes.Ellipse(this, new Vector2(0, -16), new Vector2(9, 10), Hair);
        Shapes.Circle(this, new Vector2(-5, -28), 2f, Colors.Black);
        Shapes.Circle(this, new Vector2(5, -28), 2f, Colors.Black);
        DrawLine(new Vector2(24, -44), new Vector2(24, 14), new Color(0.80f, 0.82f, 0.85f), 5f);
        DrawLine(new Vector2(16, 6), new Vector2(32, 6), new Color(0.50f, 0.34f, 0.20f), 4f);
    }
}
