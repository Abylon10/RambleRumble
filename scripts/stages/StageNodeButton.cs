using System;
using System.Collections.Generic;
using Godot;

namespace RambleRumble;

/// <summary>
/// A numbered stage marker placed over the painted chapter map: dark with a
/// cream number, glowing blue for the next stage to play, red with a flame for
/// the boss, grey with a lock until unlocked.
/// </summary>
public partial class StageNodeButton : Control
{
    public event Action? Pressed;

    public StageDef Stage { get; init; } = null!;

    private bool _selected;

    public override void _Ready()
    {
        Size = CustomMinimumSize = new Vector2(64, 64);
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = Stage.Title;
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Pressed?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var data = PlayerData.Instance;
        bool unlocked = data.IsStageUnlocked(Stage);
        bool current = unlocked && data.StageStars.GetValueOrDefault(Stage.Id) == 0;
        var centre = Size / 2f;
        float radius = Stage.IsBoss ? 22f : 18f;

        var cream = new Color(0.96f, 0.90f, 0.76f);
        var glow = Stage.IsBoss ? new Color(1f, 0.25f, 0.2f) : new Color(0.3f, 0.65f, 1f);
        if (current || Stage.IsBoss)
        {
            Shapes.Circle(this, centre, radius + 12, new Color(glow, 0.22f));
            Shapes.Circle(this, centre, radius + 7, new Color(glow, 0.35f));
        }
        if (Stage.IsBoss)
        {
            // A little flame crown above the boss marker.
            var top = centre - new Vector2(0, radius + 3);
            DrawColoredPolygon(new[] { top + new Vector2(-7, 4), top + new Vector2(0, -12), top + new Vector2(7, 4) }, new Color(1f, 0.35f, 0.2f));
        }
        Shapes.Circle(this, centre + new Vector2(0, 3), radius + 3, new Color(0, 0, 0, 0.35f));
        Shapes.Circle(this, centre, radius + 3, _selected ? Colors.White : unlocked ? cream : new Color(0.55f, 0.55f, 0.55f));
        var fill = !unlocked ? new Color(0.22f, 0.22f, 0.24f)
            : current ? new Color(0.10f, 0.30f, 0.70f)
            : Stage.IsBoss ? new Color(0.45f, 0.10f, 0.10f)
            : new Color(0.16f, 0.12f, 0.10f);
        Shapes.Circle(this, centre, radius, fill);

        var font = ThemeDB.FallbackFont;
        if (!unlocked)
        {
            var grey = new Color(0.75f, 0.75f, 0.75f);
            DrawRect(new Rect2(centre + new Vector2(-7, -1), new Vector2(14, 11)), grey);
            DrawArc(centre + new Vector2(0, -2), 5, Mathf.Pi, Mathf.Tau, 10, grey, 2.5f);
            return;
        }
        string label = Stage.Number.ToString();
        int size = Stage.IsBoss ? 22 : 20;
        var at = new Vector2(0, centre.Y + size * 0.36f);
        DrawStringOutline(font, at, label, HorizontalAlignment.Center, Size.X, size, 4, new Color(0, 0, 0, 0.7f));
        DrawString(font, at, label, HorizontalAlignment.Center, Size.X, size, cream);
    }
}
