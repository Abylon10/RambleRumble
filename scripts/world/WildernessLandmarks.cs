using Godot;

namespace RambleRumble;

/// <summary>Wilderness border, campfires, swarm marker and the gate back to the Lobby.</summary>
public partial class WildernessLandmarks : Node2D
{
    public override void _Draw()
    {
        var outer = Zones.WildRect.Grow(40);
        var inner = Zones.WildRect;
        var dark = new Color(0.18f, 0.16f, 0.14f);
        DrawRect(new Rect2(outer.Position, new Vector2(outer.Size.X, 40)), dark);
        DrawRect(new Rect2(outer.Position.X, inner.End.Y, outer.Size.X, 40), dark);
        DrawRect(new Rect2(outer.Position.X, inner.Position.Y, 40, inner.Size.Y), dark);
        DrawRect(new Rect2(inner.End.X, inner.Position.Y, 40, inner.Size.Y), dark);

        foreach (var fire in Zones.Campfires)
            MapArt.Campfire(this, fire, Zones.CampfireRadius);
        DrawSwarmMarker();
        MapArt.Portal(this, Zones.Gate, "LOBBY");
    }

    private void DrawSwarmMarker()
    {
        var at = Zones.SwarmMarker;
        DrawCircle(at, 70, new Color(0.45f, 0.20f, 0.65f, 0.35f));
        DrawArc(at, 70, 0f, Mathf.Tau, 48, new Color(0.75f, 0.45f, 1f), 4f);
        DrawCircle(at + new Vector2(0, -6), 26, new Color(0.92f, 0.90f, 0.86f));
        DrawRect(new Rect2(at + new Vector2(-14, 12), new Vector2(28, 14)), new Color(0.92f, 0.90f, 0.86f));
        DrawCircle(at + new Vector2(-10, -8), 7, Colors.Black);
        DrawCircle(at + new Vector2(10, -8), 7, Colors.Black);
        MapArt.Label(this, at + new Vector2(0, -90), "WORLD BOSS / SWARMS", 18, new Color(0.85f, 0.65f, 1f));
        MapArt.Label(this, at + new Vector2(0, 100), "(Phase 3)", 14, new Color(1, 1, 1, 0.6f));
    }
}
