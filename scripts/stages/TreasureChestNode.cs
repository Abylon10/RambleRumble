using System;
using Godot;

namespace RambleRumble;

/// <summary>
/// A chest on a stage. Walk into it to open it. A puzzle chest stays chained
/// until its puzzle is solved. Chests already looted on an earlier visit show
/// as open and empty.
/// </summary>
public partial class TreasureChestNode : Node2D
{
    public event Action<TreasureChestNode>? Opened;

    public bool IsPuzzleChest { get; init; }
    public bool Locked { get; private set; }
    public bool IsOpen { get; private set; }

    private float _sparkle;

    /// <param name="alreadyLooted">Opened on a previous visit: shown open and empty.</param>
    public void Setup(Vector2 ground, bool alreadyLooted)
    {
        Position = ground - new Vector2(0, IsoDecor.SortBias);
        IsOpen = alreadyLooted;
        Locked = IsPuzzleChest && !alreadyLooted;
    }

    public override void _Ready()
    {
        var area = new Area2D { CollisionLayer = 0, CollisionMask = 1, Position = new Vector2(0, IsoDecor.SortBias) };
        area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 34f } });
        area.BodyEntered += body => { if (body is Player) TryOpen(); };
        AddChild(area);
    }

    public void Unlock()
    {
        Locked = false;
        QueueRedraw();
    }

    public void TryOpen()
    {
        if (IsOpen || Locked)
            return;
        IsOpen = true;
        _sparkle = 1f;
        Opened?.Invoke(this);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_sparkle <= 0f)
            return;
        _sparkle = Mathf.Max(0f, _sparkle - (float)delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var g = new Vector2(0, IsoDecor.SortBias);
        Shapes.Ellipse(this, g, new Vector2(22, 9), new Color(0, 0, 0, 0.3f));
        var wood = IsPuzzleChest ? new Color(0.45f, 0.30f, 0.55f) : new Color(0.62f, 0.38f, 0.18f);
        var trim = new Color(1f, 0.82f, 0.30f);
        // Box: front-left face and front-right face, like the tactics-style crates.
        DrawColoredPolygon(new[] { g + new Vector2(-20, -6), g + new Vector2(0, 2), g + new Vector2(0, -20), g + new Vector2(-20, -28) }, wood);
        DrawColoredPolygon(new[] { g + new Vector2(0, 2), g + new Vector2(20, -6), g + new Vector2(20, -28), g + new Vector2(0, -20) }, wood.Darkened(0.25f));
        DrawLine(g + new Vector2(-20, -17), g + new Vector2(0, -9), trim, 2f);
        DrawLine(g + new Vector2(0, -9), g + new Vector2(20, -17), trim, 2f);
        if (IsOpen)
        {
            // Lid flipped back, dark inside.
            DrawColoredPolygon(new[] { g + new Vector2(-20, -28), g + new Vector2(0, -20), g + new Vector2(20, -28), g + new Vector2(0, -36) }, new Color(0.12f, 0.08f, 0.05f));
            DrawColoredPolygon(new[] { g + new Vector2(-20, -28), g + new Vector2(0, -36), g + new Vector2(0, -58), g + new Vector2(-20, -50) }, wood.Lightened(0.1f));
            if (_sparkle > 0f)
            {
                for (int k = 0; k < 6; k++)
                {
                    var at = g + new Vector2(0, -34) + Vector2.FromAngle(k * Mathf.Tau / 6 + _sparkle * 3f) * (1f - _sparkle) * 40f;
                    Shapes.Circle(this, at, 4f * _sparkle + 1f, new Color(trim, _sparkle));
                }
            }
        }
        else
        {
            DrawColoredPolygon(new[] { g + new Vector2(-20, -28), g + new Vector2(0, -20), g + new Vector2(20, -28), g + new Vector2(0, -36) }, wood.Lightened(0.15f));
            Shapes.Circle(this, g + new Vector2(0, -12), 3.5f, trim);
        }
        if (Locked)
        {
            // Chains across the box and a padlock.
            var chain = new Color(0.75f, 0.75f, 0.78f);
            DrawLine(g + new Vector2(-20, -24), g + new Vector2(20, -10), chain, 3f);
            DrawLine(g + new Vector2(-20, -10), g + new Vector2(20, -24), chain, 3f);
            DrawRect(new Rect2(g + new Vector2(-6, -20), new Vector2(12, 10)), new Color(0.85f, 0.75f, 0.35f));
            DrawArc(g + new Vector2(0, -20), 4, Mathf.Pi, Mathf.Tau, 8, new Color(0.85f, 0.75f, 0.35f), 2f);
        }
    }
}
