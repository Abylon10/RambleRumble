using Godot;

namespace RambleRumble;

/// <summary>Placeholder drawing helpers shared by the Lobby and Wilderness maps.</summary>
public static class MapArt
{
    /// <summary>Centered text with a dark outline so it reads on any ground.</summary>
    public static void Label(CanvasItem canvas, Vector2 center, string text, int size, Color? color = null)
    {
        var font = ThemeDB.FallbackFont;
        var origin = center + new Vector2(-200, 0);
        canvas.DrawStringOutline(font, origin, text, HorizontalAlignment.Center, 400, size, 6, new Color(0, 0, 0, 0.6f));
        canvas.DrawString(font, origin, text, HorizontalAlignment.Center, 400, size, color ?? Colors.White);
    }

    /// <summary>Stone arch with a swirling portal inside.</summary>
    public static void Portal(CanvasItem canvas, Vector2 at, string label)
    {
        var stone = new Color(0.55f, 0.55f, 0.55f);
        canvas.DrawArc(at, 52, Mathf.Pi, Mathf.Tau, 24, stone, 14f);
        canvas.DrawRect(new Rect2(at + new Vector2(-59, 0), new Vector2(14, 46)), stone);
        canvas.DrawRect(new Rect2(at + new Vector2(45, 0), new Vector2(14, 46)), stone);
        canvas.DrawCircle(at + new Vector2(0, 6), 42, new Color(0.30f, 0.55f, 0.95f, 0.85f));
        canvas.DrawArc(at + new Vector2(0, 6), 26, 0f, Mathf.Tau * 0.8f, 24, new Color(0.75f, 0.90f, 1f), 4f);
        Label(canvas, at + new Vector2(0, -72), label, 20);
    }

    public static void Tree(CanvasItem canvas, Vector2 at, float radius, Color dark, Color light)
    {
        canvas.DrawCircle(at + new Vector2(0, radius * 0.5f), radius * 0.9f, new Color(0, 0, 0, 0.15f));
        canvas.DrawCircle(at, radius, dark);
        canvas.DrawCircle(at + new Vector2(-radius * 0.25f, -radius * 0.25f), radius * 0.55f, light);
    }

    public static void Campfire(CanvasItem canvas, Vector2 at, float radius)
    {
        canvas.DrawCircle(at, radius, new Color(1f, 0.6f, 0.2f, 0.10f));
        canvas.DrawArc(at, radius, 0f, Mathf.Tau, 40, new Color(1f, 0.7f, 0.3f, 0.35f), 2f);
        for (int i = 0; i < 6; i++)
            canvas.DrawCircle(at + Vector2.FromAngle(i * Mathf.Tau / 6) * 26, 7, new Color(0.45f, 0.43f, 0.40f));
        canvas.DrawLine(at + new Vector2(-14, 10), at + new Vector2(14, -6), new Color(0.40f, 0.26f, 0.15f), 6f);
        canvas.DrawLine(at + new Vector2(-14, -6), at + new Vector2(14, 10), new Color(0.40f, 0.26f, 0.15f), 6f);
        canvas.DrawColoredPolygon(new[] { at + new Vector2(-12, 4), at + new Vector2(0, -26), at + new Vector2(12, 4) }, new Color(1f, 0.5f, 0.1f));
        canvas.DrawColoredPolygon(new[] { at + new Vector2(-6, 4), at + new Vector2(0, -14), at + new Vector2(6, 4) }, new Color(1f, 0.9f, 0.4f));
    }
}
