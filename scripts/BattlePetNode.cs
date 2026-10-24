using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// The equipped battle pet: trails the player and attacks the nearest monster
/// in range. Shows whichever pet has <see cref="PetRole.Battle"/>; hidden if none.
/// </summary>
public partial class BattlePetNode : Node2D
{
    private const float FollowDistance = 46f;
    private const float FollowSpeed = 6f;
    private const float AttackRange = 170f;
    private const float AttackInterval = 1.2f;

    public Player Player { get; set; } = null!;

    private float _attackTimer;
    private float _lunge;
    private Vector2 _lungeDir;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var pet = Data.BattlePet;
        Visible = pet != null;
        if (pet == null)
            return;

        // Trail behind the player, on the side away from where they face.
        var target = Player.GlobalPosition - Player.Facing * FollowDistance + new Vector2(0, 6);
        if (GlobalPosition.DistanceTo(Player.GlobalPosition) > 600f)
            GlobalPosition = target; // teleported with the player
        else
            GlobalPosition = GlobalPosition.Lerp(target, Mathf.Min(1f, FollowSpeed * dt));

        _lunge = Mathf.Max(0f, _lunge - dt * 4f);
        _attackTimer -= dt;
        if (_attackTimer <= 0f && !Player.InSafeZone)
        {
            var foe = GetTree().GetNodesInGroup("monsters").OfType<Monster>()
                .Where(m => m.GlobalPosition.DistanceTo(GlobalPosition) < AttackRange)
                .MinBy(m => m.GlobalPosition.DistanceTo(GlobalPosition));
            if (foe != null)
            {
                _attackTimer = AttackInterval;
                _lunge = 1f;
                _lungeDir = (foe.GlobalPosition - GlobalPosition).Normalized();
                foe.TakeDamage(Data.BattlePetDamage(pet));
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var pet = Data.BattlePet;
        if (pet == null)
            return;
        var species = GameConfig.SpeciesOf(pet.Species);
        float radius = species.Radius * 0.75f * GameConfig.Size(pet.Size).Scale;
        var at = _lungeDir * _lunge * 14f;
        Shapes.Ellipse(this, new Vector2(0, radius * 0.8f), new Vector2(radius * 0.9f, radius * 0.35f), new Color(0, 0, 0, 0.25f));
        Shapes.Circle(this, at, radius, species.Color);
        Shapes.Ring(this, at, radius + 1f, GameConfig.RarityColor(pet.Rarity));
        Shapes.Circle(this, at + new Vector2(-radius * 0.3f, -radius * 0.2f), 2.5f, Colors.Black);
        Shapes.Circle(this, at + new Vector2(radius * 0.3f, -radius * 0.2f), 2.5f, Colors.Black);
    }
}
