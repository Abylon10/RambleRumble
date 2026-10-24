using System;
using System.Collections.Generic;
using Godot;

namespace RambleRumble;

/// <summary>
/// Pixel-art monster sprites generated in code: each species is painted pixel
/// by pixel from a body type (slime, mushroom, beast, rabbit, bug, bat, sprite,
/// tree, goblin, scarecrow, knight) and its own colours and details, then
/// shaded and outlined like hand-made pixel art. Two frames each for a simple
/// idle/walk animation. Sprites face right; flip them to face left.
/// </summary>
public static class MonsterArt
{
    /// <summary>Canvas size of a normal sprite in pixels (bosses use <see cref="BossSize"/>).</summary>
    public const int Size = 24;
    public const int BossSize = 40;

    private static readonly Dictionary<(string, int), ImageTexture> Cache = new();

    private static readonly Color Ink = new(0.08f, 0.08f, 0.10f);
    private static readonly Color Bone = new(0.96f, 0.94f, 0.85f);
    private static readonly Color Leaf = new(0.40f, 0.78f, 0.30f);
    private static readonly Color Bark = new(0.50f, 0.34f, 0.20f);
    private static readonly Color Glow = new(0.35f, 0.95f, 0.90f);
    private static readonly Color RedVein = new(1f, 0.22f, 0.18f);

    public static bool IsBossSpecies(string species) => GameConfig.IsBoss(species);

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

    // --- Sprites cut from the roster sheets (tools/cut_monsters.py) --------------------

    private static readonly Dictionary<string, Texture2D?> SheetCache = new();

    /// <summary>Sheet sprites that face right; the rest face left (or the viewer).</summary>
    private static readonly HashSet<string> SheetFacesRight = new() { "Leafy", "Sproutling", "Forest Rabbit" };

    /// <summary>The monster as drawn on the roster sheet, or null if it has none (then the code-drawn sprite is used).</summary>
    public static Texture2D? SheetSprite(string species)
    {
        if (SheetCache.TryGetValue(species, out var cached))
            return cached;
        string path = $"res://art/monsters/{species.ToLower().Replace(' ', '_')}.png";
        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        SheetCache[species] = texture;
        return texture;
    }

    /// <summary>Draws a sprite standing on <paramref name="feet"/>, about <paramref name="height"/> pixels tall on screen.</summary>
    public static void Draw(CanvasItem canvas, string species, int frame, Vector2 feet, float height, bool faceLeft, Color modulate)
    {
        var texture = SheetSprite(species);
        Vector2 size;
        if (texture != null)
        {
            // Same texel size as the code-drawn canvas, so long beasts stay long and slimes stay small,
            // but never much taller than asked. Frame 1 squashes a little for a bouncy idle/walk.
            float texel = height / (IsBossSpecies(species) ? BossSize : Size);
            size = texture.GetSize() * Mathf.Min(texel, height * 1.15f / texture.GetHeight());
            if ((frame & 1) == 1)
                size *= new Vector2(1.05f, 0.94f);
            faceLeft ^= !SheetFacesRight.Contains(species);
        }
        else
        {
            texture = Frame(species, frame);
            size = texture.GetSize() * (height / texture.GetHeight());
        }
        var rect = new Rect2(feet - new Vector2(size.X / 2f, size.Y), size);
        if (faceLeft)
            rect = new Rect2(rect.Position + new Vector2(size.X, 0), new Vector2(-size.X, size.Y));
        canvas.DrawTextureRect(texture, rect, false, modulate);
    }

    private static void Paint(PixelCanvas c, string species, int f)
    {
        var body = GameConfig.Species.Length > 0 && Array.Exists(GameConfig.Species, s => s.Name == species)
            ? GameConfig.SpeciesOf(species).Color : new Color(0.7f, 0.7f, 0.7f);
        switch (species)
        {
            // Slimes.
            case "Green Slime": Slime(c, f, body); break;
            case "Red Slime": Slime(c, f, body); break;
            case "Blue Slime": Slime(c, f, body); c.Pixel(9, 15, new Color(0.85f, 0.95f, 1f)); break;
            case "Forest Slime": Slime(c, f, body); Sprout(c, c.N / 2f + 2 * c.Scale, SlimeTop(c, f), f, flower: false); break;
            case "Slime Plimt": Slime(c, f, body); Sprout(c, c.N / 2f, SlimeTop(c, f), f, flower: true); break;
            case "Thorn Slime": Slime(c, f, body); Thorns(c, f); break;
            case "Rock Slime": Slime(c, f, body, flat: true); Pebbles(c, f); break;
            // Mushrooms.
            case "Mushcap": Shroom(c, f, body, Colors.White, small: false); break;
            case "Sporeling": Shroom(c, f, body, new Color(1f, 0.85f, 0.3f), small: true); break;
            case "Poison Cap": Shroom(c, f, body, new Color(0.55f, 0.95f, 0.35f), small: false); Drips(c, f); break;
            // Four-legged beasts.
            case "Field Rat": Rat(c, f, body); break;
            case "Plains Boar": Beast(c, f, body, body.Lightened(0.25f), mane: new Color(0.80f, 0.22f, 0.15f), tusks: true); break;
            case "Horned Boar": Beast(c, f, body, body.Lightened(0.25f), mane: new Color(0.85f, 0.25f, 0.15f), tusks: true, horn: true); break;
            case "Wild Dog": Beast(c, f, body, new Color(0.98f, 0.88f, 0.70f)); break;
            case "Green Wolf": Beast(c, f, body, body.Lightened(0.3f), mane: new Color(0.28f, 0.48f, 0.22f)); break;
            case "Dire Wolf": Beast(c, f, body, body.Lightened(0.3f), mane: body.Darkened(0.35f), eye: new Color(1f, 0.85f, 0.2f)); break;
            case "Crimson Fang":
                Beast(c, f, body, new Color(0.80f, 0.78f, 0.80f), mane: new Color(0.88f, 0.86f, 0.90f), eye: RedVein, veins: true);
                break;
            case "Forest Rabbit": Rabbit(c, f, body, body.Darkened(0.1f)); break;
            case "Leafy": Rabbit(c, f, body, Leaf); break;
            // Bugs and fliers.
            case "Grasshopper": Grasshopper(c, f, body); break;
            case "Beelet": Beelet(c, f, body); break;
            case "Forest Bat": Bat(c, f, body, Ink, glowEyes: false); break;
            case "Shadow Bat": Bat(c, f, body, new Color(1f, 0.35f, 0.85f), glowEyes: true); break;
            case "Wood Sprite": Fairy(c, f, body); break;
            case "Wisp Spirit": Wisp(c, f, body); break;
            // Plants and trees.
            case "Sproutling": Sproutling(c, f, body); break;
            case "Treantling": Tree(c, f, body, big: false, staff: false); break;
            case "Ancient Treant": Tree(c, f, body, big: true, staff: false); break;
            case "Entling Shaman": Tree(c, f, body, big: false, staff: true); break;
            case "Elderwood Guardian": Tree(c, f, body, big: true, staff: false, core: true); break;
            // Humanoids.
            case "Scarecrow": Scarecrow(c, f, body); break;
            case "Goblin Scout": Goblin(c, f, body, brute: false); break;
            case "Goblin Brute": Goblin(c, f, body, brute: true); break;
            case "Rambling Knight": Knight(c, f, body); break;
            default: Slime(c, f, body); break;
        }
    }

    private static void Eye(PixelCanvas c, float x, float y, Color colour, float size = 1.5f) =>
        c.Rect(x, y, size * c.Scale, size * c.Scale, colour);

    // --- Slimes -------------------------------------------------------------------------

    private static float SlimeTop(PixelCanvas c, int f) => c.N - 1 - 2 * (f == 0 ? 8f : 6.5f) * c.Scale;

    private static void Slime(PixelCanvas c, int f, Color body, bool flat = false)
    {
        float s = c.Scale;
        // Frame 1 squashes: wider and shorter, like a bounce.
        float rx = (f == 0 ? 9f : 10.5f) * s, ry = (f == 0 ? 8f : 6.5f) * s;
        if (flat)
        {
            rx *= 1.12f;
            ry *= 0.85f;
        }
        float cy = c.N - 1 - ry;
        c.Ellipse(c.N / 2f, cy, rx, ry, body, flatBottom: true);
        c.Shade(body);
        // Glossy highlight and a little face.
        c.Ellipse(c.N / 2f - rx * 0.45f, cy - ry * 0.45f, 2f * s, 1.4f * s, body.Lightened(0.55f));
        float eyeY = cy - ry * 0.1f;
        c.Rect(c.N / 2f - 4 * s, eyeY - 1.5f * s, 1.6f * s, 3f * s, Ink);
        c.Rect(c.N / 2f + 2.4f * s, eyeY - 1.5f * s, 1.6f * s, 3f * s, Ink);
        c.Rect(c.N / 2f - 1f * s, eyeY + 2.5f * s, 2.5f * s, 1f * s, body.Darkened(0.5f));
    }

    /// <summary>A little stem with two leaves (and a pink flower) growing from a head.</summary>
    private static void Sprout(PixelCanvas c, float x, float top, int f, bool flower)
    {
        float s = c.Scale, sway = f == 0 ? 0 : s;
        c.Rect(x, top - 3 * s, 1 * s, 3.5f * s, Leaf.Darkened(0.2f));
        c.Ellipse(x - 2 * s + sway, top - 3 * s, 2.2f * s, 1.2f * s, Leaf);
        c.Ellipse(x + 3 * s + sway, top - 3.5f * s, 2.2f * s, 1.2f * s, Leaf.Lightened(0.1f));
        if (flower)
        {
            var pink = new Color(1f, 0.55f, 0.75f);
            c.Ellipse(x + 0.5f * s + sway, top - 5.5f * s, 2.2f * s, 2f * s, pink);
            c.Rect(x + sway, top - 6 * s, 1 * s, 1 * s, new Color(1f, 0.9f, 0.3f));
        }
    }

    private static void Thorns(PixelCanvas c, int f)
    {
        float s = c.Scale, ry = (f == 0 ? 8f : 6.5f) * s, rx = (f == 0 ? 9f : 10.5f) * s;
        float cy = c.N - 1 - ry;
        var spike = new Color(0.98f, 0.88f, 0.30f);
        for (int k = 0; k < 7; k++)
        {
            float a = Mathf.Pi * (1.05f + k * 0.15f);
            var at = new Vector2(c.N / 2f + Mathf.Cos(a) * rx * 0.92f, cy + Mathf.Sin(a) * ry * 0.92f);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var side = new Vector2(-dir.Y, dir.X) * 1.3f * s;
            c.Triangle(at - side, at + dir * 3.2f * s, at + side, spike);
        }
    }

    private static void Pebbles(PixelCanvas c, int f)
    {
        float s = c.Scale, top = c.N - 1 - 2 * (f == 0 ? 8f : 6.5f) * s * 0.85f;
        var dark = new Color(0.38f, 0.38f, 0.42f);
        c.Rect(c.N / 2f - 7 * s, top + 4 * s, 3 * s, 0.8f * s, dark);
        c.Rect(c.N / 2f + 4 * s, top + 3 * s, 2.5f * s, 0.8f * s, dark);
        c.Ellipse(c.N / 2f + 1 * s, top + 1.5f * s, 2.5f * s, 1.5f * s, new Color(0.70f, 0.70f, 0.74f));
    }

    // --- Mushrooms ----------------------------------------------------------------------

    private static void Shroom(PixelCanvas c, int f, Color cap, Color spot, bool small)
    {
        float s = c.Scale * (small ? 0.82f : 1f), bob = f == 0 ? 0 : 1;
        float mid = c.N / 2f, ground = c.N - 1;
        var stem = new Color(0.93f, 0.88f, 0.75f);
        c.Rect(mid - 4 * s, ground - 10 * s, 8 * s, 10 * s, stem);
        // Little feet alternate.
        c.Rect(mid - 5 * s + bob, ground - 1.5f * s, 3 * s, 2 * s, stem.Darkened(0.2f));
        c.Rect(mid + 2 * s - bob, ground - 1.5f * s, 3 * s, 2 * s, stem.Darkened(0.2f));
        c.Ellipse(mid, ground - 11 * s + bob, 10 * s, 7 * s, cap, flatBottom: true);
        c.Shade(cap);
        foreach (var (x, y) in new[] { (-5f, -2f), (3f, -4f), (6f, -1f), (-1f, -6f) })
            c.Ellipse(mid + x * s, ground - 11 * s + y * s + bob, 1.4f * s, 1.2f * s, spot);
        Eye(c, mid - 2.5f * s, ground - 7 * s, Ink);
        Eye(c, mid + 1.5f * s, ground - 7 * s, Ink);
    }

    private static void Drips(PixelCanvas c, int f)
    {
        float s = c.Scale, ground = c.N - 1;
        var ooze = new Color(0.55f, 0.95f, 0.35f);
        c.Rect(c.N / 2f - 8 * s, ground - 11 * s, 1 * s, (f == 0 ? 2 : 3) * s, ooze);
        c.Rect(c.N / 2f + 7 * s, ground - 11 * s, 1 * s, (f == 0 ? 3 : 2) * s, ooze);
    }

    // --- Beasts -------------------------------------------------------------------------

    private static void Beast(PixelCanvas c, int f, Color fur, Color belly, Color? mane = null, Color? eye = null,
        bool tusks = false, bool horn = false, bool veins = false)
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
        if (mane is { } m)
        {
            // A spiky ridge of fur along the back and neck.
            for (int k = 0; k < 6; k++)
            {
                float x = X(6 + k * 2.4f), y = ground - (13.2f + k * 0.5f) * s;
                c.Triangle(new Vector2(x - 1.3f * s, y + 1.5f * s), new Vector2(x + 0.6f * s, y - 2.2f * s), new Vector2(x + 1.6f * s, y + 1.5f * s), m);
            }
        }
        c.Rect(X(20f), ground - 14.5f * s, 1.5f * s, 1.5f * s, eye ?? Ink);
        c.Pixel((int)X(23.6f), (int)(ground - 11 * s), new Color(0.1f, 0.08f, 0.08f));
        if (tusks)
        {
            c.Rect(X(21f), ground - 10 * s, 1 * s, 2.5f * s, Bone);
            c.Rect(X(23f), ground - 10 * s, 1 * s, 2.5f * s, Bone);
        }
        if (horn)
            c.Triangle(new Vector2(X(21.5f), ground - 14 * s), new Vector2(X(24f), ground - 19 * s), new Vector2(X(23.2f), ground - 13 * s), Bone);
        if (veins)
        {
            // Glowing red veins streaking along the body and a red-tipped tail.
            for (int k = 0; k < 4; k++)
                c.Rect(X(5 + k * 3.2f), ground - (10 - (k % 2) * 2) * s, 2.6f * s, 0.8f * s, RedVein);
            c.Rect(X(17f), ground - 15 * s, 2 * s, 0.8f * s, RedVein);
            c.Rect(X(0.5f), ground - 13 * s + (f == 0 ? 0 : s), 2 * s, 2 * s, RedVein);
        }
    }

    private static void Rat(PixelCanvas c, int f, Color fur)
    {
        float s = c.Scale, ground = c.N - 1, step = f == 0 ? 1 : -1;
        var pink = new Color(0.95f, 0.65f, 0.65f);
        // Long thin tail curling up behind.
        c.Rect(2 * s, ground - 3 * s, 5 * s, 1 * s, pink);
        c.Rect(1 * s, ground - (f == 0 ? 6 : 5) * s, 1 * s, 3 * s, pink);
        c.Rect((8 + step) * s, ground - 3 * s, 2 * s, 3 * s, fur.Darkened(0.25f));
        c.Rect((14 - step) * s, ground - 3 * s, 2 * s, 3 * s, fur.Darkened(0.25f));
        c.Ellipse(12 * s, ground - 6 * s, 6 * s, 4.5f * s, fur);
        c.Ellipse(18 * s, ground - 8 * s, 3.5f * s, 3.2f * s, fur);
        c.Rect(20.5f * s, ground - 8 * s, 2.5f * s, 2 * s, fur);
        c.Ellipse(15.5f * s, ground - 12 * s, 2 * s, 2 * s, fur.Lightened(0.1f));
        c.Shade(fur);
        c.Pixel((int)(15.5f * s), (int)(ground - 12 * s), pink);
        Eye(c, 18.5f * s, ground - 10 * s, Ink, 1.2f);
        c.Pixel((int)(23 * s) - 1, (int)(ground - 8 * s), pink);
    }

    private static void Rabbit(PixelCanvas c, int f, Color fur, Color ears)
    {
        float s = c.Scale, ground = c.N - 1, hop = f == 0 ? 0 : 2 * s;
        c.Ellipse(11 * s, ground - 5 * s - hop, 6 * s, 5 * s, fur);
        c.Ellipse(16 * s, ground - 11 * s - hop, 4 * s, 3.8f * s, fur);
        // Tall ears (leaves for Leafy).
        c.Ellipse(14.5f * s, ground - 18 * s - hop, 1.4f * s, 4 * s, ears);
        c.Ellipse(17.8f * s, ground - 18.5f * s - hop, 1.4f * s, 4 * s, ears);
        c.Ellipse(5 * s, ground - 7 * s - hop, 1.8f * s, 1.8f * s, Colors.White);
        c.Rect(13 * s, ground - 1.5f * s - hop, 4 * s, 1.5f * s, fur.Darkened(0.15f));
        c.Shade(fur);
        if (ears != fur.Darkened(0.1f))
            c.Shade(ears);
        Eye(c, 17.5f * s, ground - 12.5f * s - hop, Ink, 1.3f);
        c.Pixel((int)(19.8f * s), (int)(ground - 10.5f * s - hop), new Color(0.95f, 0.55f, 0.6f));
    }

    // --- Bugs and fliers ----------------------------------------------------------------

    private static void Grasshopper(PixelCanvas c, int f, Color body)
    {
        float s = c.Scale, ground = c.N - 1, kick = f == 0 ? 0 : 1.5f * s;
        // Big folded hind leg, thin front legs and antennae.
        c.Triangle(new Vector2(6 * s, ground - 8 * s), new Vector2(12 * s, ground - 14 * s - kick), new Vector2(13 * s, ground - 9 * s), body.Darkened(0.15f));
        c.Rect(5 * s, ground - 8 * s, 1.2f * s, 8 * s, body.Darkened(0.3f));
        c.Rect(15 * s, ground - 5 * s, 1 * s, 5 * s, body.Darkened(0.3f));
        c.Rect(18 * s, ground - 5 * s, 1 * s, 5 * s, body.Darkened(0.3f));
        c.Ellipse(12 * s, ground - 7 * s, 8 * s, 2.8f * s, body);
        c.Ellipse(20 * s, ground - 9 * s, 2.8f * s, 2.8f * s, body);
        c.Shade(body);
        c.Rect(7 * s, ground - 9 * s, 9 * s, 1 * s, body.Lightened(0.3f));
        Eye(c, 20.5f * s, ground - 10.5f * s, Ink, 1.3f);
        c.Rect(21 * s, ground - 16 * s, 0.8f * s, 5 * s, body.Darkened(0.4f));
        c.Rect(21.5f * s, ground - 17 * s, 2 * s, 0.8f * s, body.Darkened(0.4f));
    }

    private static void Beelet(PixelCanvas c, int f, Color body)
    {
        float s = c.Scale, cy = c.N / 2f + 3 * s, bob = f == 0 ? 0 : s;
        var wing = new Color(0.85f, 0.95f, 1f);
        c.Ellipse(9 * s, cy - (f == 0 ? 8 : 6) * s, 3 * s, 4 * s, wing);
        c.Ellipse(14 * s, cy - (f == 0 ? 9 : 7) * s, 3 * s, 4 * s, wing.Darkened(0.06f));
        c.Ellipse(12 * s, cy + bob, 7 * s, 5.5f * s, body);
        c.Shade(body);
        for (int k = 0; k < 3; k++)
            c.Rect((7 + k * 3.5f) * s, cy - 4 * s + bob, 1.4f * s, 9 * s, new Color(0.18f, 0.14f, 0.10f));
        c.Triangle(new Vector2(4 * s, cy - 1 * s + bob), new Vector2(1 * s, cy + bob), new Vector2(4 * s, cy + 1.5f * s + bob), Ink);
        Eye(c, 16 * s, cy - 2 * s + bob, Ink);
        c.Rect(17 * s, cy - 9 * s + bob, 0.8f * s, 4 * s, Ink);
    }

    private static void Bat(PixelCanvas c, int f, Color body, Color eyes, bool glowEyes)
    {
        float s = c.Scale, cy = c.N / 2f + 2 * s;
        var wing = body.Darkened(0.25f);
        // Wings up on frame 0, down on frame 1.
        float tipY = f == 0 ? cy - 8 * s : cy + 5 * s;
        c.Triangle(new Vector2(c.N / 2f - 3 * s, cy - 1 * s), new Vector2(1 * s, tipY), new Vector2(c.N / 2f - 3 * s, cy + 3 * s), wing);
        c.Triangle(new Vector2(c.N / 2f + 3 * s, cy - 1 * s), new Vector2(c.N - 1 * s, tipY), new Vector2(c.N / 2f + 3 * s, cy + 3 * s), wing);
        // Membrane ribs.
        c.Rect(4 * s, (tipY + cy) / 2f, 5 * s, 0.8f * s, wing.Darkened(0.3f));
        c.Rect(c.N - 9 * s, (tipY + cy) / 2f, 5 * s, 0.8f * s, wing.Darkened(0.3f));
        c.Ellipse(c.N / 2f, cy, 4.5f * s, 4.5f * s, body);
        c.Rect(c.N / 2f - 4 * s, cy - 7 * s, 2 * s, 3 * s, body);
        c.Rect(c.N / 2f + 2 * s, cy - 7 * s, 2 * s, 3 * s, body);
        c.Shade(body);
        Eye(c, c.N / 2f - 2.5f * s, cy - 1.5f * s, glowEyes ? eyes : new Color(1f, 0.9f, 0.3f), 1.3f);
        Eye(c, c.N / 2f + 1.2f * s, cy - 1.5f * s, glowEyes ? eyes : new Color(1f, 0.9f, 0.3f), 1.3f);
        c.Pixel((int)(c.N / 2f - 1), (int)(cy + 2 * s), Bone);
        c.Pixel((int)(c.N / 2f + 1), (int)(cy + 2 * s), Bone);
    }

    private static void Fairy(PixelCanvas c, int f, Color glow)
    {
        float s = c.Scale, bob = f == 0 ? 0 : 1.5f * s, mid = c.N / 2f;
        float y = c.N - 6 * s - bob;
        // Leaf wings, a small leafy body and a glowing head.
        var wing = new Color(glow.R, glow.G, glow.B, 1f).Lightened(0.3f);
        c.Ellipse(mid - 5 * s, y - 9 * s, 3.2f * s, (f == 0 ? 4.5f : 3.5f) * s, wing);
        c.Ellipse(mid + 5 * s, y - 9 * s, 3.2f * s, (f == 0 ? 4.5f : 3.5f) * s, wing);
        c.Triangle(new Vector2(mid - 3 * s, y - 6 * s), new Vector2(mid, y + 2 * s), new Vector2(mid + 3 * s, y - 6 * s), Leaf);
        c.Ellipse(mid, y - 9 * s, 3.5f * s, 3.5f * s, glow);
        c.Shade(glow);
        c.Triangle(new Vector2(mid - 2 * s, y - 12 * s), new Vector2(mid - 1 * s, y - 16 * s), new Vector2(mid + 1 * s, y - 12 * s), Leaf.Darkened(0.1f));
        Eye(c, mid - 2 * s, y - 10 * s, Ink, 1.2f);
        Eye(c, mid + 1 * s, y - 10 * s, Ink, 1.2f);
        c.Pixel((int)(mid + 6 * s), (int)(y - 3 * s), glow.Lightened(0.5f));
        c.Pixel((int)(mid - 7 * s), (int)(y - 1 * s), glow.Lightened(0.5f));
    }

    private static void Wisp(PixelCanvas c, int f, Color flame)
    {
        float s = c.Scale, bob = f == 0 ? 0 : 1.5f * s, mid = c.N / 2f;
        float cy = c.N - 8 * s - bob;
        // A teardrop of blue fire with a flickering tip.
        c.Ellipse(mid, cy, 5.5f * s, 5.5f * s, flame);
        c.Triangle(new Vector2(mid - 5 * s, cy - 1 * s), new Vector2(mid + (f == 0 ? 2 : -2) * s, cy - 14 * s), new Vector2(mid + 5 * s, cy - 1 * s), flame);
        c.Shade(flame);
        c.Ellipse(mid, cy + 0.5f * s, 3.2f * s, 3.2f * s, flame.Lightened(0.5f));
        Eye(c, mid - 2 * s, cy - 1 * s, Ink, 1.2f);
        Eye(c, mid + 1 * s, cy - 1 * s, Ink, 1.2f);
        c.Pixel((int)(mid + 7 * s), (int)(cy - 6 * s), flame.Lightened(0.4f));
        c.Pixel((int)(mid - 7 * s), (int)(cy + 2 * s), flame.Lightened(0.4f));
    }

    // --- Plants and trees ---------------------------------------------------------------

    private static void Sproutling(PixelCanvas c, int f, Color seed)
    {
        float s = c.Scale, ground = c.N - 1, step = f == 0 ? 1 : -1, mid = c.N / 2f;
        c.Rect(mid - 3 * s + step, ground - 3 * s, 1.5f * s, 3 * s, seed.Darkened(0.3f));
        c.Rect(mid + 1.5f * s - step, ground - 3 * s, 1.5f * s, 3 * s, seed.Darkened(0.3f));
        c.Ellipse(mid, ground - 8 * s, 5 * s, 5.5f * s, seed);
        c.Shade(seed);
        Eye(c, mid - 2.5f * s, ground - 9 * s, Ink, 1.3f);
        Eye(c, mid + 1.3f * s, ground - 9 * s, Ink, 1.3f);
        c.Rect(mid - 1 * s, ground - 6.5f * s, 2 * s, 0.8f * s, seed.Darkened(0.45f));
        Sprout(c, mid - 0.5f * s, ground - 13 * s, f, flower: false);
    }

    private static void Tree(PixelCanvas c, int f, Color bark, bool big, bool staff, bool core = false)
    {
        float s = c.Scale * (big ? 1f : 0.85f), ground = c.N - 1, mid = c.N / 2f, sway = f == 0 ? 0 : 1 * s;
        var leaves = new Color(0.30f, 0.62f, 0.25f);
        // Root feet, trunk body, branch arms and a leafy crown.
        c.Rect(mid - 6 * s, ground - 3 * s, 4 * s, 3 * s, bark.Darkened(0.25f));
        c.Rect(mid + 2 * s, ground - 3 * s, 4 * s, 3 * s, bark.Darkened(0.25f));
        c.Rect(mid - 5 * s, ground - 14 * s, 10 * s, 12 * s, bark);
        float armW = big ? 5f : 3.5f;
        c.Rect(mid - 5 * s - armW * s, ground - 13 * s + sway, armW * s, 2.2f * s, bark);
        c.Rect(mid - 5 * s - armW * s, ground - 13 * s + sway, 2 * s, 6 * s, bark);
        c.Rect(mid + 5 * s, ground - 13 * s - sway, armW * s, 2.2f * s, bark);
        c.Rect(mid + 5 * s + (armW - 2) * s, ground - 13 * s - sway, 2 * s, 6 * s, bark);
        c.Shade(bark);
        c.Ellipse(mid, ground - 18 * s, (big ? 9.5f : 7.5f) * s, 5 * s, leaves);
        c.Ellipse(mid - 4 * s, ground - 20 * s, 4 * s, 3 * s, leaves.Lightened(0.08f));
        c.Shade(leaves);
        // Bark grain and a carved face.
        c.Rect(mid - 3 * s, ground - 6 * s, 0.8f * s, 3 * s, bark.Darkened(0.4f));
        c.Rect(mid + 2.5f * s, ground - 8 * s, 0.8f * s, 3 * s, bark.Darkened(0.4f));
        var eyes = core || staff ? Glow : new Color(1f, 0.85f, 0.3f);
        Eye(c, mid - 3 * s, ground - 12 * s, eyes, 1.6f);
        Eye(c, mid + 1.4f * s, ground - 12 * s, eyes, 1.6f);
        c.Rect(mid - 1.5f * s, ground - 9 * s, 3 * s, 1 * s, bark.Darkened(0.55f));
        if (big)
        {
            // Moss patches.
            c.Rect(mid - 5 * s, ground - 7 * s, 2 * s, 2 * s, leaves.Darkened(0.1f));
            c.Rect(mid + 3 * s, ground - 4 * s, 2 * s, 1.5f * s, leaves.Darkened(0.1f));
        }
        if (core)
        {
            // Corrupted heart-crystal glowing in the trunk, and floating sparks.
            c.Triangle(new Vector2(mid, ground - 9 * s), new Vector2(mid - 2 * s, ground - 6 * s), new Vector2(mid + 2 * s, ground - 6 * s), Glow);
            c.Triangle(new Vector2(mid - 2 * s, ground - 6 * s), new Vector2(mid, ground - 3 * s), new Vector2(mid + 2 * s, ground - 6 * s), Glow.Darkened(0.2f));
            c.Ellipse(mid + (f == 0 ? 10 : 11) * s, ground - 22 * s, 1.2f * s, 1.2f * s, Glow);
            c.Ellipse(mid - (f == 0 ? 11 : 10) * s, ground - 6 * s, 1.2f * s, 1.2f * s, Glow);
        }
        if (staff)
        {
            float x = mid + 5 * s + armW * s;
            c.Rect(x, ground - 20 * s - sway, 1.2f * s, 20 * s, Bark.Darkened(0.2f));
            c.Ellipse(x + 0.6f * s, ground - 21 * s - sway, 1.8f * s, 1.8f * s, Glow);
        }
    }

    // --- Humanoids ----------------------------------------------------------------------

    private static void Scarecrow(PixelCanvas c, int f, Color coat)
    {
        float s = c.Scale, ground = c.N - 1, mid = c.N / 2f, tilt = f == 0 ? 0 : 1 * s;
        var straw = new Color(0.95f, 0.80f, 0.35f);
        var sack = new Color(0.85f, 0.72f, 0.50f);
        c.Rect(mid - 0.8f * s, ground - 8 * s, 1.6f * s, 8 * s, Bark);
        // Outstretched stick arms ending in straw tufts.
        c.Rect(2 * s, ground - 13 * s + tilt, c.N - 4 * s, 1.4f * s, Bark);
        c.Rect(1 * s, ground - 14 * s + tilt, 2 * s, 3 * s, straw);
        c.Rect(c.N - 3 * s, ground - 14 * s + tilt, 2 * s, 3 * s, straw);
        c.Rect(mid - 4.5f * s, ground - 14 * s, 9 * s, 8 * s, coat);
        c.Shade(coat);
        c.Rect(mid + 1 * s, ground - 10 * s, 2 * s, 2 * s, new Color(0.45f, 0.55f, 0.75f));
        c.Rect(mid - 4.5f * s, ground - 7 * s, 9 * s, 1 * s, straw);
        c.Ellipse(mid, ground - 17 * s, 3.5f * s, 3.2f * s, sack);
        c.Shade(sack);
        Eye(c, mid - 2 * s, ground - 18 * s, Ink, 1.2f);
        Eye(c, mid + 1 * s, ground - 18 * s, Ink, 1.2f);
        c.Rect(mid - 1.5f * s, ground - 16 * s, 3 * s, 0.8f * s, Ink);
        // Straw hat.
        c.Rect(mid - 6 * s, ground - 20 * s, 12 * s, 1.5f * s, straw.Darkened(0.1f));
        c.Rect(mid - 3 * s, ground - 23 * s, 6 * s, 3 * s, straw.Darkened(0.15f));
        c.Rect(mid - 3 * s, ground - 21 * s, 6 * s, 0.8f * s, new Color(0.7f, 0.25f, 0.2f));
    }

    private static void Goblin(PixelCanvas c, int f, Color skin, bool brute)
    {
        float s = c.Scale * (brute ? 1.08f : 1f), ground = c.N - 1, mid = c.N / 2f;
        var cloth = brute ? new Color(0.45f, 0.30f, 0.20f) : new Color(0.55f, 0.40f, 0.25f);
        float step = f == 0 ? 1f : -1f;
        c.Rect(mid - 4 * s + step, ground - 6 * s, 3 * s, 6 * s, skin.Darkened(0.2f));
        c.Rect(mid + 1 * s - step, ground - 6 * s, 3 * s, 6 * s, skin.Darkened(0.2f));
        float w = brute ? 12 : 9;
        c.Rect(mid - w / 2 * s, ground - 13 * s, w * s, 8 * s, cloth);
        c.Rect(mid - w / 2 * s, ground - 7 * s, w * s, 1.2f * s, new Color(0.25f, 0.18f, 0.12f));
        c.Ellipse(mid, ground - 16.5f * s, 4.5f * s, 4 * s, skin);
        // Big pointed ears.
        c.Triangle(new Vector2(mid - 4 * s, ground - 18 * s), new Vector2(mid - 8.5f * s, ground - 19.5f * s), new Vector2(mid - 4 * s, ground - 15.5f * s), skin);
        c.Triangle(new Vector2(mid + 4 * s, ground - 18 * s), new Vector2(mid + 8.5f * s, ground - 19.5f * s), new Vector2(mid + 4 * s, ground - 15.5f * s), skin);
        c.Shade(skin);
        c.Shade(cloth);
        Eye(c, mid - 2 * s, ground - 17.5f * s, new Color(1f, 0.85f, 0.2f));
        Eye(c, mid + 1 * s, ground - 17.5f * s, new Color(1f, 0.85f, 0.2f));
        if (brute)
        {
            c.Pixel((int)(mid - 1.5f * s), (int)(ground - 14 * s), Bone);
            c.Pixel((int)(mid + 1.5f * s), (int)(ground - 14 * s), Bone);
            // Heavy club, raised on frame 1.
            var wood = Bark;
            float lift = f == 0 ? 0 : 5 * s;
            c.Rect(mid + 6 * s, ground - 14 * s - lift, 2.2f * s, 9 * s, wood);
            c.Ellipse(mid + 7 * s, ground - 15 * s - lift, 2.6f * s, 3 * s, wood.Lightened(0.1f));
        }
        else
        {
            // Spear held upright, thrust forward on frame 1.
            float poke = f == 0 ? 0 : 2 * s;
            c.Rect(mid + 6 * s + poke, ground - 20 * s, 1 * s, 16 * s, Bark);
            c.Triangle(new Vector2(mid + 4.8f * s + poke, ground - 20 * s), new Vector2(mid + 6.5f * s + poke, ground - 24 * s), new Vector2(mid + 8.2f * s + poke, ground - 20 * s), new Color(0.75f, 0.78f, 0.82f));
        }
    }

    private static void Knight(PixelCanvas c, int f, Color steel)
    {
        float s = c.Scale, ground = c.N - 1, mid = c.N / 2f, step = f == 0 ? 1 : -1;
        c.Rect(mid - 4 * s + step, ground - 6 * s, 3 * s, 6 * s, steel.Darkened(0.25f));
        c.Rect(mid + 1 * s - step, ground - 6 * s, 3 * s, 6 * s, steel.Darkened(0.25f));
        c.Rect(mid - 5 * s, ground - 14 * s, 10 * s, 9 * s, steel);
        c.Ellipse(mid, ground - 17.5f * s, 4.5f * s, 4.5f * s, steel);
        c.Shade(steel);
        // Visor slit, plume, belt.
        c.Rect(mid - 3 * s, ground - 18 * s, 6 * s, 1 * s, Ink);
        c.Rect(mid - 1 * s, ground - 24 * s, 2 * s, 3 * s, new Color(0.85f, 0.2f, 0.2f));
        c.Rect(mid - 5 * s, ground - 8 * s, 10 * s, 1 * s, new Color(0.45f, 0.30f, 0.18f));
        // Shield on the left, sword raised on frame 1.
        var shield = new Color(0.25f, 0.40f, 0.75f);
        c.Rect(mid - 9 * s, ground - 14 * s, 5 * s, 7 * s, shield);
        c.Rect(mid - 7 * s, ground - 13 * s, 1 * s, 5 * s, new Color(1f, 0.85f, 0.3f));
        float lift = f == 0 ? 0 : 4 * s;
        c.Rect(mid + 6 * s, ground - 20 * s - lift, 1.4f * s, 11 * s, new Color(0.85f, 0.88f, 0.92f));
        c.Rect(mid + 4.5f * s, ground - 10 * s - lift, 4.4f * s, 1.2f * s, new Color(1f, 0.85f, 0.3f));
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
