using System.Collections.Generic;
using Godot;

namespace RambleRumble;

/// <summary>Wind Slash projectile: flies straight and hits each monster once (pierces).</summary>
public partial class Projectile : Node2D
{
    private const float Speed = 650f;
    private const float MaxDistance = 420f;
    private const float HitRadius = 22f;

    public Vector2 Direction { get; set; } = Vector2.Right;
    public float Damage { get; set; } = 10f;
    public Color Tint { get; set; } = Colors.White;

    private float _travelled;
    private readonly HashSet<Monster> _hit = new();

    public override void _PhysicsProcess(double delta)
    {
        float step = Speed * (float)delta;
        Position += Direction * step;
        _travelled += step;
        foreach (var node in GetTree().GetNodesInGroup("monsters"))
        {
            if (node is not Monster monster || _hit.Contains(monster))
                continue;
            if (monster.GlobalPosition.DistanceTo(GlobalPosition) <= HitRadius + monster.Radius)
            {
                _hit.Add(monster);
                monster.TakeDamage(Damage);
            }
        }
        QueueRedraw();
        if (_travelled >= MaxDistance)
            QueueFree();
    }

    public override void _Draw()
    {
        float angle = Direction.Angle();
        DrawArc(Vector2.Zero, 20f, angle - 1.1f, angle + 1.1f, 16, Tint, 6f);
        DrawArc(new Vector2(-8, 0).Rotated(angle), 16f, angle - 0.9f, angle + 0.9f, 12, new Color(Tint, 0.5f), 3f);
    }
}
