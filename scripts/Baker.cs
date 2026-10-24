using System;
using Godot;

namespace RambleRumble;

/// <summary>
/// Renders a static drawing once into a texture and swaps it for a single
/// Sprite2D, so hundreds of polygons cost one quad per frame. Falls back to
/// drawing live where there's no GPU (headless tests).
/// </summary>
public static class Baker
{
    /// <param name="host">Node that owns the temporary viewport and the result.</param>
    /// <param name="art">The drawing to bake; it must not be in the tree yet.</param>
    /// <param name="region">Area of <paramref name="art"/>'s coordinates to capture.</param>
    /// <param name="place">Adds the result (baked sprite, or the live art as a fallback) where it belongs.</param>
    public static async void Bake(Node host, Node2D art, Rect2 region, Action<Node2D> place)
    {
        if (DisplayServer.GetName() == "headless")
        {
            place(art);
            return;
        }
        var viewport = new SubViewport
        {
            Size = (Vector2I)region.Size.Ceil(),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        art.Position = -region.Position;
        viewport.AddChild(art);
        host.AddChild(viewport);
        await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
            return;

        var image = viewport.GetTexture().GetImage();
        viewport.RemoveChild(art);
        viewport.QueueFree();
        if (image == null || image.IsEmpty())
        {
            art.Position = Vector2.Zero;
            place(art);
            return;
        }
        art.QueueFree();
        place(new Sprite2D { Texture = ImageTexture.CreateFromImage(image), Centered = false, Position = region.Position });
    }
}
