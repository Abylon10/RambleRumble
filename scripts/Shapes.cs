using Godot;

namespace RambleRumble;

/// <summary>
/// Fast circles and rings for things redrawn every frame. In Godot 4.7 C#,
/// DrawCircle costs ~130 µs and DrawArc ~85 µs per call, while drawing a
/// texture costs ~1 µs, so these draw a white disc/ring texture tinted and
/// scaled instead. Static art drawn once (maps) can keep using DrawCircle.
/// </summary>
public static class Shapes
{
    private const int TextureSize = 128;
    /// <summary>Ring thickness as a fraction of its radius: thick for small world shapes, thin for big UI circles.</summary>
    private const float RingWidth = 0.14f;
    private const float ThinRingWidth = 0.05f;

    private static ImageTexture? _disc;
    private static ImageTexture? _ring;
    private static ImageTexture? _thinRing;

    public static Texture2D DiscTexture => _disc ??= Build(0f);
    public static Texture2D RingTexture => _ring ??= Build(RingWidth);
    public static Texture2D ThinRingTexture => _thinRing ??= Build(ThinRingWidth);

    public static void Circle(CanvasItem canvas, Vector2 center, float radius, Color color) =>
        Ellipse(canvas, center, new Vector2(radius, radius), color);

    public static void Ellipse(CanvasItem canvas, Vector2 center, Vector2 radii, Color color) =>
        canvas.DrawTextureRect(DiscTexture, new Rect2(center - radii, radii * 2), false, color);

    public static void Ring(CanvasItem canvas, Vector2 center, float radius, Color color, bool thin = false) =>
        canvas.DrawTextureRect(thin ? ThinRingTexture : RingTexture,
            new Rect2(center - new Vector2(radius, radius), new Vector2(radius, radius) * 2), false, color);

    /// <param name="ringWidth">0 for a filled disc, otherwise ring thickness as a fraction of the radius.</param>
    private static ImageTexture Build(float ringWidth)
    {
        var data = new byte[TextureSize * TextureSize * 4];
        float center = (TextureSize - 1) / 2f;
        float outer = TextureSize / 2f - 1f;
        float inner = outer * (1f - ringWidth);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float d = new Vector2(x - center, y - center).Length();
                // One-pixel soft edges keep scaled shapes smooth.
                float alpha = Mathf.Clamp(outer - d + 0.5f, 0f, 1f);
                if (ringWidth > 0f)
                    alpha *= Mathf.Clamp(d - inner + 0.5f, 0f, 1f);
                int i = (y * TextureSize + x) * 4;
                data[i] = data[i + 1] = data[i + 2] = 255;
                data[i + 3] = (byte)(alpha * 255);
            }
        }
        var image = Image.CreateFromData(TextureSize, TextureSize, false, Image.Format.Rgba8, data);
        return ImageTexture.CreateFromImage(image);
    }
}
