using System;
using System.Collections.Generic;
using Godot;

namespace RambleRumble;

/// <summary>A stage circle on the chapter map: number, stars earned, lock, and a bigger skull ring for bosses.</summary>
public partial class StageNodeButton : Control
{
    public event Action? Pressed;

    public StageDef Stage { get; init; } = null!;

    private bool _selected;

    public override void _Ready()
    {
        Size = CustomMinimumSize = new Vector2(96, 104);
        MouseFilter = MouseFilterEnum.Stop;
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
        int stars = data.StageStars.GetValueOrDefault(Stage.Id);
        var centre = new Vector2(Size.X / 2f, 42f);
        float radius = Stage.IsBoss ? 34f : 26f;

        // Locked grey, cleared dark, the next stage to play glowing blue, the boss red.
        bool current = unlocked && stars == 0;
        var fill = !unlocked ? new Color(0.35f, 0.35f, 0.35f)
            : current ? new Color(0.20f, 0.45f, 0.90f)
            : new Color(0.25f, 0.20f, 0.15f);
        if (Stage.IsBoss)
            fill = !unlocked ? new Color(0.45f, 0.22f, 0.20f) : current ? new Color(0.85f, 0.20f, 0.18f) : new Color(0.50f, 0.18f, 0.16f);
        if (current)
            Shapes.Circle(this, centre, radius + 14, new Color(fill, 0.35f));

        Shapes.Ellipse(this, centre + new Vector2(0, radius * 0.75f), new Vector2(radius, radius * 0.35f), new Color(0, 0, 0, 0.3f));
        Shapes.Circle(this, centre, radius + 5, _selected ? Colors.White : new Color(0.95f, 0.85f, 0.65f));
        Shapes.Circle(this, centre, radius, fill);
        Shapes.Circle(this, centre + new Vector2(-radius * 0.3f, -radius * 0.35f), radius * 0.35f, new Color(1, 1, 1, 0.18f));

        var font = ThemeDB.FallbackFont;
        if (!unlocked)
        {
            // Padlock.
            DrawRect(new Rect2(centre + new Vector2(-9, -2), new Vector2(18, 14)), new Color(0.85f, 0.85f, 0.85f));
            DrawArc(centre + new Vector2(0, -3), 6, Mathf.Pi, Mathf.Tau, 10, new Color(0.85f, 0.85f, 0.85f), 3f);
        }
        else
        {
            string label = Stage.IsBoss ? "BOSS" : Stage.Number.ToString();
            int size = Stage.IsBoss ? 16 : 22;
            DrawStringOutline(font, new Vector2(0, centre.Y + size * 0.35f), label, HorizontalAlignment.Center, Size.X, size, 4, new Color(0, 0, 0, 0.6f));
            DrawString(font, new Vector2(0, centre.Y + size * 0.35f), label, HorizontalAlignment.Center, Size.X, size, Colors.White);
        }

        // Three stars under the circle.
        for (int k = 0; k < 3; k++)
        {
            var at = new Vector2(Size.X / 2f + (k - 1) * 16f, centre.Y + radius + 14f);
            DrawStar(at, k < stars ? new Color(1f, 0.85f, 0.25f) : new Color(0, 0, 0, 0.35f));
        }
    }

    private void DrawStar(Vector2 at, Color colour)
    {
        var points = new Vector2[10];
        for (int i = 0; i < 10; i++)
            points[i] = at + Vector2.FromAngle(-Mathf.Pi / 2 + i * Mathf.Pi / 5) * (i % 2 == 0 ? 7f : 3f);
        DrawColoredPolygon(points, colour);
    }
}
