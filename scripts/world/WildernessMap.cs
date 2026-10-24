using Godot;

namespace RambleRumble;

/// <summary>
/// Builds the Wilderness art: one <see cref="ZoneCellArt"/> per zone cell plus
/// <see cref="WildernessLandmarks"/>. Each cell is rendered once into a
/// texture at startup, so the hundreds of trees, rocks and crystals cost one
/// quad per cell instead of being redrawn every frame (the map ran at ~5 FPS
/// on an Intel HD 520 when drawn live).
/// </summary>
public partial class WildernessMap : Node2D
{
    private Node2D _cells = null!;

    public override void _Ready()
    {
        // Cells go in their own layer first so they stay under the landmarks
        // even though baked sprites arrive a frame later.
        _cells = new Node2D();
        AddChild(_cells);
        AddChild(new WildernessLandmarks());

        bool canBake = DisplayServer.GetName() != "headless";
        int seed = 1;
        foreach (var zone in Zones.All)
        {
            for (int i = 0; i < zone.Cells.Length; i++)
            {
                var art = new ZoneCellArt { Zone = zone, Cell = zone.Cells[i], Seed = (ulong)(seed++ * 7919), ShowName = i == 0 };
                if (canBake)
                    Bake(art);
                else
                    _cells.AddChild(art);
            }
        }
    }

    /// <summary>Renders <paramref name="art"/> into an off-screen viewport once, then shows it as a Sprite2D.</summary>
    private async void Bake(ZoneCellArt art)
    {
        var viewport = new SubViewport
        {
            Size = (Vector2I)art.Cell.Size,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        art.Position = -art.Cell.Position;
        viewport.AddChild(art);
        AddChild(viewport);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        var image = viewport.GetTexture().GetImage();
        viewport.QueueFree();
        if (image == null || image.IsEmpty())
        {
            // Baking unavailable (e.g. no GPU): fall back to drawing live.
            art.Position = Vector2.Zero;
            art.GetParent()?.RemoveChild(art);
            _cells.AddChild(art);
            return;
        }
        _cells.AddChild(new Sprite2D
        {
            Texture = ImageTexture.CreateFromImage(image),
            Centered = false,
            Position = art.Cell.Position,
        });
    }
}
