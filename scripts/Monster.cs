using System;
using Godot;

namespace RambleRumble;

/// <summary>
/// A wilderness monster: wanders near its spawn point, chases the player inside
/// its aggro range, and gives up once pulled too far from home.
/// </summary>
public partial class Monster : CharacterBody2D
{
    public event Action<Monster>? Died;

    private const float AggroRange = 260f;
    private const float LeashRange = 520f;
    private const float AttackCooldown = 1.1f;
    private const float PlayerRadius = 18f;

    public string SpeciesId { get; private set; } = "Slime";
    public float Radius { get; private set; } = 16f;

    private GameConfig.SpeciesInfo _species = GameConfig.Species[0];
    private float _hp = 1f;
    private float _maxHp = 1f;
    private Vector2 _home;
    private Player _target = null!;

    private float _attackTimer;
    private float _stunLeft;
    private float _burnLeft;
    private float _burnDps;
    private float _flash;
    private Vector2 _wanderDir;
    private float _wanderTimer;
    private bool _dead;

    public void Setup(string speciesId, Vector2 position, Player target)
    {
        SpeciesId = speciesId;
        _species = GameConfig.SpeciesOf(speciesId);
        _maxHp = _hp = _species.Hp;
        Radius = _species.Radius;
        Position = _home = position;
        _target = target;
    }

    public override void _Ready()
    {
        AddToGroup("monsters");
        CollisionLayer = 2;
        CollisionMask = 4;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead)
            return;
        float dt = (float)delta;
        _attackTimer = Mathf.Max(0f, _attackTimer - dt);
        _flash = Mathf.Max(0f, _flash - dt);
        if (_burnLeft > 0f)
        {
            _burnLeft -= dt;
            TakeDamage(_burnDps * dt, showNumber: false);
            if (_dead)
                return;
        }
        QueueRedraw();
        if (_stunLeft > 0f)
        {
            _stunLeft -= dt;
            return;
        }

        var toPlayer = _target.GlobalPosition - GlobalPosition;
        var fromHome = GlobalPosition - _home;
        if (toPlayer.Length() < AggroRange && fromHome.Length() < LeashRange)
        {
            if (toPlayer.Length() <= Radius + PlayerRadius + 8f)
            {
                Velocity = Vector2.Zero;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = AttackCooldown;
                    _target.TakeDamage(_species.Damage);
                }
            }
            else
            {
                Velocity = toPlayer.Normalized() * _species.Speed;
            }
        }
        else if (fromHome.Length() > 60f)
        {
            Velocity = -fromHome.Normalized() * _species.Speed;
        }
        else
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = (float)GD.RandRange(1.0, 3.0);
                _wanderDir = GD.Randf() < 0.4f ? Vector2.Zero : Vector2.FromAngle(GD.Randf() * Mathf.Tau);
            }
            Velocity = _wanderDir * _species.Speed * 0.4f;
        }
        MoveAndSlide();
    }

    public void TakeDamage(float amount, bool showNumber = true)
    {
        if (_dead)
            return;
        _hp -= amount;
        _flash = 0.1f;
        if (showNumber)
            Fx.FloatingText(GetParent(), GlobalPosition + new Vector2(0, -Radius - 12), Mathf.RoundToInt(amount).ToString(), Colors.White);
        if (_hp <= 0f)
        {
            _dead = true;
            Died?.Invoke(this);
            QueueFree();
        }
    }

    public void Stun(float seconds) => _stunLeft = Mathf.Max(_stunLeft, seconds);

    public void Burn(float dps, float seconds)
    {
        _burnDps = dps;
        _burnLeft = seconds;
    }

    public override void _Draw()
    {
        var body = _flash > 0f ? Colors.White : _species.Color;
        if (_burnLeft > 0f)
            body = body.Lerp(new Color(1f, 0.4f, 0.1f), 0.35f);
        DrawCircle(Vector2.Zero, Radius, body);
        DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 32, GameConfig.RarityColor(_species.Rarity), 3f);
        var eyeDir = (_target.GlobalPosition - GlobalPosition).Normalized() * Radius * 0.35f;
        DrawCircle(eyeDir + new Vector2(-5, -4), 3f, Colors.Black);
        DrawCircle(eyeDir + new Vector2(5, -4), 3f, Colors.Black);
        if (_stunLeft > 0f)
            DrawArc(new Vector2(0, -Radius - 6), 8f, 0f, Mathf.Tau, 12, new Color(1f, 1f, 0.4f), 2f);
        if (_hp < _maxHp)
        {
            float w = Radius * 2.2f;
            DrawRect(new Rect2(-w / 2, -Radius - 10, w, 5), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(-w / 2, -Radius - 10, w * _hp / _maxHp, 5), new Color(0.9f, 0.2f, 0.2f));
        }
    }
}
