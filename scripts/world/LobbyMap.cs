using Godot;

namespace RambleRumble;

/// <summary>
/// Lobby layout from the world map concept (the shops were cut), drawn as
/// placeholder shapes. Positions are shared with World, which builds the
/// interaction zones on top.
/// </summary>
public partial class LobbyMap : Node2D
{
    public static readonly Rect2 Bounds = new(-900, -700, 1800, 1400);

    // Numbers match the map legend.
    public static readonly Vector2 SpawnPoint = new(0, 600);         // 1
    public static readonly Vector2 Plaza = new(0, 0);                // 2
    public const float PlazaRadius = 170f;
    public static readonly Vector2 RaidBoard = new(-560, -330);      // 3
    public static readonly Rect2 TrainingGround = new(-830, 120, 340, 260); // 8
    public static readonly Vector2 Portal = new(-320, 470);          // 9
    public static readonly Rect2 PlayerPlot = new(360, 150, 470, 340); // 10
    public static readonly Rect2 FenceRect = new(390, 250, 300, 210);
    public static readonly Vector2 UpgradeStation = new(560, -130);  // 11
    public static readonly Vector2 CosmeticArea = new(-600, -40);    // 12
    public static readonly Vector2 Leaderboard = new(560, -400);     // 13
    public static readonly Vector2 EventArea = new(0, -520);         // 14
    /// <summary>Where players land when coming back through the gate.</summary>
    public static readonly Vector2 ReturnPoint = new(-320, 350);

    public const float StationRadius = 70f;

    private static readonly Color Grass = new(0.36f, 0.55f, 0.33f);
    private static readonly Color Stone = new(0.62f, 0.60f, 0.55f);
    private static readonly Color StoneDark = new(0.45f, 0.43f, 0.40f);
    private static readonly Color Wood = new(0.50f, 0.34f, 0.20f);
    private static readonly Color Roof = new(0.70f, 0.25f, 0.20f);

    public override void _Draw()
    {
        DrawRect(Bounds.Grow(40), new Color(0.28f, 0.25f, 0.22f));
        DrawRect(Bounds, Grass);
        DrawTrees();

        // Stone paths from the plaza out to every station: dark edging first, then the path.
        var stations = new[] { SpawnPoint, RaidBoard, TrainingGround.GetCenter(), Portal,
            PlayerPlot.GetCenter(), UpgradeStation, CosmeticArea, Leaderboard, EventArea };
        foreach (var target in stations)
            DrawLine(Plaza, target, StoneDark, 64f);
        foreach (var target in stations)
            DrawLine(Plaza, target, Stone, 52f);

        DrawPlaza();
        DrawSpawn();
        DrawTraining();
        DrawPlot();
        DrawBuilding(RaidBoard, new Color(0.45f, 0.28f, 0.16f), "RAID BOARD", "Phase 4");
        DrawBuilding(CosmeticArea, new Color(0.55f, 0.30f, 0.60f), "COSMETICS & TITLES", "Phase 6");
        DrawBuilding(Leaderboard, new Color(0.35f, 0.30f, 0.20f), "LEADERBOARD", "Phase 5");
        DrawBuilding(EventArea, new Color(0.25f, 0.45f, 0.40f), "EVENT AREA", "Phase 3");
        DrawUpgradeStation();
        MapArt.Portal(this, Portal, "WILDERNESS");
    }

    private void DrawTrees()
    {
        var rng = new RandomNumberGenerator { Seed = 11 };
        for (int i = 0; i < 70; i++)
        {
            // Trees only along the edges so the middle stays open.
            var p = new Vector2(rng.RandfRange(Bounds.Position.X, Bounds.End.X), rng.RandfRange(Bounds.Position.Y, Bounds.End.Y));
            if (Bounds.Grow(-110).HasPoint(p))
                continue;
            MapArt.Tree(this, p, rng.RandfRange(22, 36), new Color(0.18f, 0.40f, 0.20f), new Color(0.26f, 0.52f, 0.26f));
        }
    }

    private void DrawPlaza()
    {
        DrawCircle(Plaza, PlazaRadius, StoneDark);
        DrawCircle(Plaza, PlazaRadius - 8, Stone);
        DrawArc(Plaza, PlazaRadius - 40, 0f, Mathf.Tau, 64, StoneDark, 3f);
        // Crystal fountain.
        DrawCircle(Plaza, 62, new Color(0.30f, 0.55f, 0.80f));
        DrawArc(Plaza, 62, 0f, Mathf.Tau, 48, new Color(0.75f, 0.70f, 0.60f), 8f);
        DrawColoredPolygon(new[] { Plaza + new Vector2(0, -70), Plaza + new Vector2(22, -10), Plaza + new Vector2(0, 20), Plaza + new Vector2(-22, -10) },
            new Color(0.55f, 0.85f, 1f));
        DrawColoredPolygon(new[] { Plaza + new Vector2(0, -70), Plaza + new Vector2(22, -10), Plaza + new Vector2(0, -2) },
            new Color(0.80f, 0.95f, 1f));
        Label(Plaza + new Vector2(0, PlazaRadius + 26), "CENTRAL PLAZA", 18);
    }

    private void DrawSpawn()
    {
        DrawCircle(SpawnPoint, 46, StoneDark);
        DrawCircle(SpawnPoint, 38, new Color(0.85f, 0.75f, 0.45f));
        DrawArc(SpawnPoint, 28, 0f, Mathf.Tau, 32, new Color(1f, 0.95f, 0.7f), 3f);
        Label(SpawnPoint + new Vector2(0, 70), "SPAWN", 16);
    }

    private void DrawTraining()
    {
        var rect = TrainingGround;
        DrawRect(rect, new Color(0.62f, 0.50f, 0.33f));
        DrawRect(rect, Wood, false, 5f);
        for (int i = 0; i < 3; i++)
        {
            var dummy = rect.Position + new Vector2(70 + i * 100, 110);
            DrawRect(new Rect2(dummy.X - 6, dummy.Y, 12, 50), Wood);
            DrawRect(new Rect2(dummy.X - 26, dummy.Y + 12, 52, 8), Wood);
            DrawCircle(dummy, 16, new Color(0.85f, 0.75f, 0.55f));
        }
        Label(rect.Position + new Vector2(rect.Size.X / 2, -14), "TRAINING GROUND", 20);
    }

    private void DrawPlot()
    {
        var plot = PlayerPlot;
        DrawRect(plot, new Color(0.40f, 0.58f, 0.34f));
        DrawRect(plot, Wood, false, 3f);
        // Pet fence (FenceDisplay draws the pets on top).
        var fence = FenceRect;
        DrawRect(fence, new Color(0.46f, 0.66f, 0.38f));
        for (int i = 0; i < 13; i++)
        {
            float x = fence.Position.X + i * fence.Size.X / 12f;
            DrawRect(new Rect2(x - 4, fence.Position.Y - 4, 8, 14), Wood);
            DrawRect(new Rect2(x - 4, fence.End.Y - 10, 8, 14), Wood);
        }
        DrawRect(fence, Wood, false, 3f);
        // House.
        var house = new Rect2(plot.End.X - 120, plot.Position.Y + 40, 100, 80);
        DrawRect(house, new Color(0.80f, 0.70f, 0.55f));
        DrawColoredPolygon(new[] { house.Position + new Vector2(-12, 0), house.Position + new Vector2(house.Size.X / 2, -50),
            house.Position + new Vector2(house.Size.X + 12, 0) }, new Color(0.25f, 0.40f, 0.75f));
        DrawRect(new Rect2(house.Position.X + 40, house.End.Y - 34, 22, 34), Wood);
        Label(plot.Position + new Vector2(plot.Size.X / 2, -14), "YOUR PLOT  •  PET FENCE", 20);
    }

    private void DrawBuilding(Vector2 at, Color walls, string name, string phase)
    {
        var body = new Rect2(at - new Vector2(70, 30), new Vector2(140, 70));
        DrawRect(new Rect2(body.Position + new Vector2(6, 8), body.Size), new Color(0, 0, 0, 0.2f));
        DrawRect(body, walls);
        DrawColoredPolygon(new[] { body.Position + new Vector2(-14, 0), body.Position + new Vector2(body.Size.X / 2, -46),
            body.Position + new Vector2(body.Size.X + 14, 0) }, Roof);
        DrawRect(new Rect2(at.X - 14, body.End.Y - 32, 28, 32), Wood);
        Label(at + new Vector2(0, -88), name, 18);
        Label(at + new Vector2(0, 64), $"({phase})", 14, new Color(1, 1, 1, 0.6f));
    }

    private void DrawUpgradeStation()
    {
        var at = UpgradeStation;
        DrawRect(new Rect2(at - new Vector2(60, 20), new Vector2(120, 60)), StoneDark);
        DrawRect(new Rect2(at - new Vector2(40, 34), new Vector2(80, 22)), new Color(0.30f, 0.30f, 0.33f));
        DrawRect(new Rect2(at - new Vector2(10, 12), new Vector2(20, 30)), new Color(0.30f, 0.30f, 0.33f));
        DrawCircle(at + new Vector2(44, -30), 12, new Color(1f, 0.55f, 0.15f));
        DrawCircle(at + new Vector2(44, -33), 6, new Color(1f, 0.90f, 0.40f));
        Label(at + new Vector2(0, -60), "UPGRADE STATION", 18);
    }

    private void Label(Vector2 center, string text, int size, Color? color = null) =>
        MapArt.Label(this, center, text, size, color);
}
