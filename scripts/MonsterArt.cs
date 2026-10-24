using System;
using System.Collections.Generic;
using Godot;

namespace RambleRumble;

/// <summary>
/// Pixel-art monster sprites generated in code: each species is painted pixel
/// by pixel from a body type (slime, mushroom, beast, goblin, bat, imp,
/// golem) and its own colours and details, then shaded and outlined like
/// hand-made pixel art. Two frames each for a simple idle/walk animation.
/// Sprites face right; flip them to face left.
/// </summary>
public static class MonsterArt
{
    /// <summary>Canvas size of a normal sprite in pixels (bosses use <see cref="BossSize"/>).</summary>
    public const int Size = 24;
    public const int BossSize = 40;

    private static readonly Dictionary<(string, int), ImageTexture> Cache = new();

    public static bool IsBossSpecies(string species) => species is "Slime King" or "Crimson Fang" or "Stone Golem";

    public static Texture2D Frame(string species, int frame)
    {
        frame &= 1;
        if (Cache.TryGetValue((species, frame), out var cached))
            return cached;
        var texture = ImageTexture.CreateFromImage(FrameImage(species, frame));
        Cache[(species, frame)] = texture;
        return texture;
    }

    /// <summary>The raw pixels of a frame (also used to preview the art without a GPU).</summary>
    public static Image FrameImage(string species, int frame)
    {
        var canvas = new PixelCanvas(IsBossSpecies(species) ? BossSize : Size);
        Paint(canvas, species, frame & 1);
        canvas.Finish();
        return canvas.ToImage();
    }

    /// <summary>Draws a sprite standing on <paramref name="feet"/>, about <paramref name="height"/> pixels tall on screen.</summary>
    public static void Draw(CanvasItem canvas, string species, int frame, Vector2 feet, float height, bool faceLeft, Color modulate)
    {
        var texture = Frame(species, frame);
        var size = texture.GetSize() * (height / texture.GetHeight());
        var rect = new Rect2(feet - new Vector2(size.X / 2f, size.Y), size);
        if (faceLeft)
            rect = new Rect2(rect.Position + new Vector2(size.X, 0), new Vector2(-size.X, size.Y));
        canvas.DrawTextureRect(texture, rect, false, modulate);
    }

    private static void Paint(PixelCanvas c, string species, int f)
    {
        switch (species)
        {
            case "Slime": Slime(c, f, new Color(0.40f, 0.82f, 0.38f)); break;
            case "Aqua Slime": Slime(c, f, new Color(0.30f, 0.60f, 0.95f)); c.Pixel(9, 8, new Color(0.85f, 0.95f, 1f)); break;
            case "Slime King": Slime(c, f, new Color(0.50f, 0.88f, 0.36f)); Crown(c, f); break;
            case "Shroom": Shroom(c, f); break;
            case "Wolf": Beast(c, f, new Color(0.58f, 0.58f, 0.64f), new Color(0.85f, 0.85f, 0.88f), tusks: false, veins: false); break;
            case "Boar": Beast(c, f, new Color(0.55f, 0.38f, 0.26f), new Color(0.72f, 0.55f, 0.40f), tusks: true, veins: false); break;
            case "Crimson Fang": Beast(c, f, new Color(0.45f, 0.14f, 0.14f), new Color(0.70f, 0.30f, 0.28f), tusks: false, veins: true); break;
            case "Goblin": Goblin(c, f); break;
            case "Crystal Bat": Bat(c, f); break;
            case "Fire Imp": Imp(c, f); break;
            case "Stone Golem": Golem(c, f); break;
            default: Slime(c, f, new Color(0.7f, 0.7f, 0.7f)); break;
        }
    }

    // --- Body types (coordinates are for a 24×24 canvas; bosses scale up) -------------

    private static void Slime(PixelCanvas c, int f, Color body)
    {
        float s = c.Scale;
        // Frame 1 squashes: wider and shorter, like a bounce.
        float rx = (f == 0 ? 9f : 10.5f) * s, ry = (f == 0 ? 8f : 6.5f) * s;
        float cy = c.N - 1 - ry;
        c.Ellipse(c.N / 2f, cy, rx, ry, body, flatBottom: true);
        c.Shade(body);
        // Glossy highlight and a little face.
        c.Ellipse(c.N / 2f - rx * 0.45f, cy - ry * 0.45f, 2f * s, 1.4f * s, body.Lightened(0.55f));
        float eyeY = cy - ry * 0.1f;
        c.Rect(c.N / 2f - 4 * s, eyeY - 1.5f * s, 1.6f * s, 3f * s, new Color(0.08f, 0.08f, 0.1f));
        c.Rect(c.N / 2f + 2.4f * s, eyeY - 1.5f * s, 1.6f * s, 3f * s, new Color(0.08f, 0.08f, 0.1f));
        c.Rect(c.N / 2f - 1f * s, eyeY + 2.5f * s, 2.5f * s, 1f * s, body.Darkened(0.5f));
    }

    private static void Crown(PixelCanvas c, int f)
    {
        var gold = new Color(1f, 0.82f, 0.25f);
        float s = c.Scale, top = c.N - 1 - (f == 0 ? 16f : 13f) * s - 4 * s;
        c.Rect(c.N / 2f - 5 * s, top + 2 * s, 10 * s, 3 * s, gold);
        for (int k = 0; k < 3; k++)
            c.Rect(c.N / 2f - 5 * s + k * 4 * s, top, 2 * s, 2.5f * s, gold);
        c.Pixel((int)(c.N / 2f), (int)(top + 3 * s), new Color(0.9f, 0.2f, 0.3f));
    }

    private static void Shroom(PixelCanvas c, int f)
    {
        float s = c.Scale, bob = f == 0 ? 0 : 1;
        var stem = new Color(0.93f, 0.88f, 0.75f);
        var cap = new Color(0.85f, 0.25f, 0.22f);
        c.Rect(c.N / 2f - 4 * s, c.N - 11 * s, 8 * s, 10 * s, stem);
        // Little feet alternate.
        c.Rect(c.N / 2f - 5 * s + bob, c.N - 2 * s, 3 * s, 2 * s, stem.Darkened(0.2f));
        c.Rect(c.N / 2f + 2 * s - bob, c.N - 2 * s, 3 * s, 2 * s, stem.Darkened(0.2f));
        c.Ellipse(c.N / 2f, c.N - 12 * s + bob, 10 * s, 7 * s, cap, flatBottom: true);
        c.Shade(cap);
        foreach (var (x, y) in new[] { (-5f, -2f), (3f, -4f), (6f, -1f), (-1f, -6f) })
            c.Ellipse(c.N / 2f + x * s, c.N - 12 * s + y * s + bob, 1.4f * s, 1.2f * s, Colors.White);
        c.Rect(c.N / 2f - 2.5f * s, c.N - 8 * s, 1.2f * s, 2 * s, new Color(0.1f, 0.1f, 0.1f));
        c.Rect(c.N / 2f + 1.5f * s, c.N - 8 * s, 1.2f * s, 2 * s, new Color(0.1f, 0.1f, 0.1f));
    }

    private static void Beast(PixelCanvas c, int f, Color fur, Color belly, bool tusks, bool veins)
    {
        float s = c.Scale;
        float ground = c.N - 1;
        // Horizontal positions are squeezed a little so the snout and tail fit the canvas.
        float X(float x) => (x * 0.88f + 0.8f) * s;
        // Legs: front and back pairs swap between frames.
        float stride = f == 0 ? 1.5f : -1.5f;
        foreach (float x in new[] { 5f, 8f, 15f, 18f })
        {
            float off = (x < 12 ? stride : -stride) * s;
            c.Rect(X(x) + off, ground - 6 * s, 2.2f * s, 6 * s, fur.Darkened(0.25f));
        }
        c.Ellipse(X(11.5f), ground - 9 * s, 7.6f * s, 5 * s, fur);
        c.Ellipse(X(11.5f), ground - 7 * s, 5.4f * s, 2.5f * s, belly);
        // Head at the front (right), ears and snout.
        c.Ellipse(X(19.5f), ground - 13 * s, 4.2f * s, 4 * s, fur);
        c.Rect(X(21.5f), ground - 12 * s, 2.5f * s, 2.5f * s, belly);
        c.Rect(X(17f), ground - 19 * s, 2 * s, 3.5f * s, fur.Darkened(0.15f));
        c.Rect(X(20f), ground - 19 * s, 2 * s, 3.5f * s, fur.Darkened(0.15f));
        // Tail.
        c.Rect(X(1.5f), ground - 13 * s + (f == 0 ? 0 : s), 4 * s, 2 * s, fur.Darkened(0.1f));
        c.Shade(fur);
        c.Rect(X(20f), ground - 14.5f * s, 1.5f * s, 1.5f * s, veins ? new Color(1f, 0.3f, 0.2f) : new Color(0.08f, 0.08f, 0.1f));
        c.Pixel((int)X(23.6f), (int)(ground - 11 * s), new Color(0.1f, 0.08f, 0.08f));
        if (tusks)
        {
            c.Rect(X(21f), ground - 10 * s, 1 * s, 2.5f * s, new Color(0.96f, 0.94f, 0.85f));
            c.Rect(X(23f), ground - 10 * s, 1 * s, 2.5f * s, new Color(0.96f, 0.94f, 0.85f));
        }
        if (veins)
        {
            // Glowing red veins streaking along the body.
            var red = new Color(1f, 0.25f, 0.2f);
            for (int k = 0; k < 4; k++)
                c.Rect(X(5 + k * 3.2f), ground - (12 - (k % 2) * 2) * s, 2.6f * s, 0.8f * s, red);
            c.Rect(X(17f), ground - 15 * s, 2 * s, 0.8f * s, red);
        }
    }

    private static void Goblin(PixelCanvas c, int f)
    {
        float s = c.Scale, ground = c.N - 1;
        var skin = new Color(0.50f, 0.68f, 0.28f);
        var cloth = new Color(0.50f, 0.35f, 0.22f);
        float step = f == 0 ? 1f : -1f;
        c.Rect((8 + step) * s, ground - 6 * s, 3 * s, 6 * s, skin.Darkened(0.2f));
        c.Rect((13 - step) * s, ground - 6 * s, 3 * s, 6 * s, skin.Darkened(0.2f));
        c.Rect(7 * s, ground - 13 * s, 10 * s, 8 * s, cloth);
        c.Ellipse(12 * s, ground - 17 * s, 5 * s, 4.5f * s, skin);
        // Big pointed ears.
        c.Rect(4 * s, ground - 19 * s, 3.5f * s, 2 * s, skin);
        c.Rect(16.5f * s, ground - 19 * s, 3.5f * s, 2 * s, skin);
        c.Shade(skin);
        c.Rect(10 * s, ground - 18 * s, 1.5f * s, 1.5f * s, new Color(1f, 0.85f, 0.2f));
        c.Rect(13.5f * s, ground - 18 * s, 1.5f * s, 1.5f * s, new Color(1f, 0.85f, 0.2f));
        // Wooden club, raised on frame 1.
        var wood = new Color(0.45f, 0.30f, 0.17f);
        if (f == 0)
        {
            c.Rect(18 * s, ground - 14 * s, 2 * s, 8 * s, wood);
            c.Rect(17.5f * s, ground - 16 * s, 3 * s, 3 * s, wood.Lightened(0.1f));
        }
        else
        {
            c.Rect(17 * s, ground - 20 * s, 2 * s, 8 * s, wood);
            c.Rect(16.5f * s, ground - 22 * s, 3 * s, 3 * s, wood.Lightened(0.1f));
        }
    }

    private static void Bat(PixelCanvas c, int f)
    {
        float s = c.Scale, cy = c.N / 2f + 2 * s;
        var body = new Color(0.40f, 0.30f, 0.62f);
        var wing = new Color(0.30f, 0.22f, 0.50f);
        // Wings up on frame 0, down on frame 1.
        float tipY = f == 0 ? cy - 8 * s : cy + 5 * s;
        c.Triangle(new Vector2(c.N / 2f - 3 * s, cy - 1 * s), new Vector2(1 * s, tipY), new Vector2(c.N / 2f - 3 * s, cy + 3 * s), wing);
        c.Triangle(new Vector2(c.N / 2f + 3 * s, cy - 1 * s), new Vector2(c.N - 1 * s, tipY), new Vector2(c.N / 2f + 3 * s, cy + 3 * s), wing);
        c.Ellipse(c.N / 2f, cy, 4.5f * s, 4.5f * s, body);
        c.Rect(c.N / 2f - 4 * s, cy - 7 * s, 2 * s, 3 * s, body);
        c.Rect(c.N / 2f + 2 * s, cy - 7 * s, 2 * s, 3 * s, body);
        c.Shade(body);
        // Crystal growths on its back.
        var crystal = new Color(0.60f, 0.85f, 1f);
        c.Triangle(new Vector2(c.N / 2f - 1 * s, cy - 3 * s), new Vector2(c.N / 2f + 1 * s, cy - 9 * s), new Vector2(c.N / 2f + 2.5f * s, cy - 3 * s), crystal);
        c.Rect(c.N / 2f - 2.5f * s, cy - 1 * s, 1.3f * s, 1.3f * s, new Color(1f, 0.9f, 0.3f));
        c.Rect(c.N / 2f + 1.2f * s, cy - 1 * s, 1.3f * s, 1.3f * s, new Color(1f, 0.9f, 0.3f));
    }

    private static void Imp(PixelCanvas c, int f)
    {
        float s = c.Scale, ground = c.N - 1;
        var skin = new Color(0.90f, 0.32f, 0.15f);
        float hop = f == 0 ? 0 : 1.5f * s;
        c.Rect(9 * s, ground - 5 * s, 2 * s, 5 * s, skin.Darkened(0.3f));
        c.Rect(13 * s, ground - 5 * s, 2 * s, 5 * s, skin.Darkened(0.3f));
        c.Ellipse(12 * s, ground - 10 * s - hop, 5 * s, 5.5f * s, skin);
        c.Ellipse(12 * s, ground - 17 * s - hop, 4.5f * s, 4 * s, skin);
        // Horns and a flame tail.
        c.Triangle(new Vector2(8.5f * s, ground - 19 * s - hop), new Vector2(7 * s, ground - 24 * s - hop), new Vector2(10.5f * s, ground - 20 * s - hop), new Color(0.25f, 0.18f, 0.15f));
        c.Triangle(new Vector2(13.5f * s, ground - 20 * s - hop), new Vector2(17 * s, ground - 24 * s - hop), new Vector2(15.5f * s, ground - 19 * s - hop), new Color(0.25f, 0.18f, 0.15f));
        c.Triangle(new Vector2(4 * s, ground - 9 * s), new Vector2(1 * s, ground - (f == 0 ? 17 : 15) * s), new Vector2(7 * s, ground - 11 * s), new Color(1f, 0.75f, 0.2f));
        c.Shade(skin);
        c.Rect(10 * s, ground - 18 * s - hop, 1.4f * s, 1.4f * s, new Color(1f, 0.95f, 0.4f));
        c.Rect(13 * s, ground - 18 * s - hop, 1.4f * s, 1.4f * s, new Color(1f, 0.95f, 0.4f));
    }

    private static void Golem(PixelCanvas c, int f)
    {
        float s = c.Scale, ground = c.N - 1;
        var stone = new Color(0.52f, 0.56f, 0.56f);
        float sway = f == 0 ? 0 : 1 * s;
        c.Rect(6 * s, ground - 6 * s, 4 * s, 6 * s, stone.Darkened(0.2f));
        c.Rect(14 * s, ground - 6 * s, 4 * s, 6 * s, stone.Darkened(0.2f));
        c.Rect(4 * s, ground - 17 * s, 16 * s, 12 * s, stone);
        c.Rect(1 * s, ground - 16 * s + sway, 3.5f * s, 10 * s, stone.Darkened(0.1f));
        c.Rect(19.5f * s, ground - 16 * s - sway, 3.5f * s, 10 * s, stone.Darkened(0.1f));
        c.Rect(7 * s, ground - 22 * s, 10 * s, 6 * s, stone.Lightened(0.05f));
        c.Shade(stone);
        // Cracks, glowing eyes and core.
        c.Rect(6 * s, ground - 12 * s, 3 * s, 0.8f * s, stone.Darkened(0.45f));
        c.Rect(15 * s, ground - 9 * s, 2.5f * s, 0.8f * s, stone.Darkened(0.45f));
        var glow = new Color(0.35f, 0.95f, 0.85f);
        c.Rect(9 * s, ground - 20 * s, 2 * s, 1.5f * s, glow);
        c.Rect(13 * s, ground - 20 * s, 2 * s, 1.5f * s, glow);
        c.Ellipse(12 * s, ground - 11 * s, 2 * s, 2 * s, glow);
    }

    /// <summary>A small pixel canvas with shape helpers, shading and an automatic outline.</summary>
    private sealed class PixelCanvas
    {
        public int N { get; }
        /// <summary>Shapes are designed for 24 px; bosses scale them up.</summary>
        public float Scale => N / 24f;

        private readonly Color?[,] _px;
        private readonly bool[,] _shaded;

        public PixelCanvas(int n)
        {
            N = n;
            _px = new Color?[n, n];
            _shaded = new bool[n, n];
        }

        public void Pixel(int x, int y, Color c)
        {
            if (x >= 0 && y >= 0 && x < N && y < N)
                _px[x, y] = c;
        }

        public void Rect(float x, float y, float w, float h, Color c)
        {
            for (int px = (int)MathF.Round(x); px < (int)MathF.Round(x + w); px++)
                for (int py = (int)MathF.Round(y); py < (int)MathF.Round(y + h); py++)
                    Pixel(px, py, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, bool flatBottom = false)
        {
            for (int x = (int)(cx - rx) - 1; x <= (int)(cx + rx) + 1; x++)
                for (int y = (int)(cy - ry) - 1; y <= (int)(cy + ry) + 1; y++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f || (flatBottom && y + 0.5f > cy && MathF.Abs(dx) <= 0.92f && y < cy + ry))
                        Pixel(x, y, c);
                }
        }

        public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color colour)
        {
            float minX = MathF.Min(a.X, MathF.Min(b.X, c.X)), maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
            float minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
            for (int x = (int)minX; x <= (int)maxX; x++)
                for (int y = (int)minY; y <= (int)maxY; y++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos))
                        Pixel(x, y, colour);
                }
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

        /// <summary>
        /// Pixel-art shading for pixels of exactly <paramref name="body"/>: a lighter
        /// rim on top-left edges, darker on bottom-right edges and the lower part.
        /// </summary>
        public void Shade(Color body)
        {
            var copy = (Color?[,])_px.Clone();
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    if (copy[x, y] is not { } c || !c.IsEqualApprox(body) || _shaded[x, y])
                        continue;
                    bool emptyUpLeft = Empty(copy, x - 1, y) || Empty(copy, x, y - 1);
                    bool emptyDownRight = Empty(copy, x + 1, y) || Empty(copy, x, y + 1);
                    int top = y, bottom = y;
                    while (top > 0 && !Empty(copy, x, top - 1)) top--;
                    while (bottom < N - 1 && !Empty(copy, x, bottom + 1)) bottom++;
                    float t = bottom > top ? (float)(y - top) / (bottom - top) : 0f;
                    var shade = emptyUpLeft ? body.Lightened(0.22f)
                        : emptyDownRight ? body.Darkened(0.28f)
                        : t > 0.68f ? body.Darkened(0.16f)
                        : body;
                    _px[x, y] = shade;
                    _shaded[x, y] = true;
                }
        }

        private bool Empty(Color?[,] grid, int x, int y) => x < 0 || y < 0 || x >= N || y >= N || grid[x, y] == null;

        /// <summary>Adds the dark one-pixel outline that makes pixel sprites read on any ground.</summary>
        public void Finish()
        {
            var copy = (Color?[,])_px.Clone();
            var outline = new Color(0.10f, 0.08f, 0.10f);
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                    if (copy[x, y] == null && (!Empty(copy, x - 1, y) || !Empty(copy, x + 1, y) || !Empty(copy, x, y - 1) || !Empty(copy, x, y + 1)))
                        _px[x, y] = outline;
        }

        public Image ToImage()
        {
            var data = new byte[N * N * 4];
            for (int x = 0; x < N; x++)
                for (int y = 0; y < N; y++)
                {
                    if (_px[x, y] is not { } c)
                        continue;
                    int i = (y * N + x) * 4;
                    data[i] = (byte)(c.R * 255);
                    data[i + 1] = (byte)(c.G * 255);
                    data[i + 2] = (byte)(c.B * 255);
                    data[i + 3] = 255;
                }
            return Image.CreateFromData(N, N, false, Image.Format.Rgba8, data);
        }
    }
}
