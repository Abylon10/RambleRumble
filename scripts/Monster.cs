using System;
using Godot;

namespace RambleRumble;

/// <summary>
/// A monster. On stage battlefields it always hunts the player
/// (<see cref="AlwaysAggro"/>); otherwise it wanders near its spawn point,
/// chases inside its aggro range and gives up when pulled too far from home.
/// </summary>
public partial class Monster : CharacterBody2D
{
    public event Action<Monster>? Died;

    private const float AggroRange = 260f;
    private const float LeashRange = 520f;
    private const float AttackCooldown = 1.1f;
    private const float PlayerRadius = 18f;
    /// <summary>Beyond this distance from the player a monster skips AI and redraws.</summary>
    private const float SleepRange = 1400f;

    public string SpeciesId { get; private set; } = "Green Slime";
    /// <summary>Tag of whatever spawned it, e.g. a stage wave ("" if none).</summary>
    public string ZoneId { get; private set; } = "";
    /// <summary>Hunts the player anywhere (stage battlefields), ignoring aggro range and leash.</summary>
    public bool AlwaysAggro { get; set; }
    public bool IsBoss { get; private set; }
    public float Radius { get; private set; } = 16f;
    /// <summary>Red-streaked by corrupted Rumble (story swarms).</summary>
    public bool Corrupted { get; set; }
    /// <summary>Species level, used for XP and to decide whether fights train skill mastery.</summary>
    public int Level => _species.Level;

    private GameConfig.SpeciesInfo _species = GameConfig.Species[0];
    private float _hp = 1f;
    private float _maxHp = 1f;
    private float _damage = 1f;
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
    private bool _asleep;
    private float _animTime;
    private bool _faceLeft;
    private bool _elite;
    private string _eliteTitle = "";

    public void Setup(string speciesId, Vector2 position, Player target, string zoneId = "")
    {
        SpeciesId = speciesId;
        ZoneId = zoneId;
        _species = GameConfig.SpeciesOf(speciesId);
        _maxHp = _hp = _species.Hp;
        _damage = _species.Damage;
        Radius = _species.Radius;
        Position = _home = position;
        _target = target;
    }

    public override void _Ready()
    {
        AddToGroup("monsters");
        TextureFilter = TextureFilterEnum.Nearest; // crisp pixel-art sprites
        CollisionLayer = 2;
        CollisionMask = 4;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } });
        if (_elite)
            AddChild(MakeBossLabel(_eliteTitle));
        if (GameConfig.IsBoss(_species.Name) && !_elite && !IsBoss)
            AddChild(MakeBossLabel($"BOSS: {_species.Name.ToUpper()}"));
    }

    /// <summary>Stage difficulty: multiplies HP and damage (call before adding to the tree).</summary>
    public void ApplyScale(float scale)
    {
        _maxHp = _hp = _maxHp * scale;
        _damage *= scale;
    }

    /// <summary>Turns this monster into a tougher, bigger, labelled leader (call before adding to the tree).</summary>
    public void MakeElite(string title, float hpMult = 4f, float sizeMult = 1.5f)
    {
        _maxHp = _hp = _maxHp * hpMult;
        Radius *= sizeMult;
        _elite = true;
        _eliteTitle = title;
    }

    /// <summary>A chapter boss: keeps its size, gets a name banner.</summary>
    public void MakeBoss()
    {
        MakeElite($"BOSS: {_species.Name.ToUpper()}", hpMult: 1f, sizeMult: 1f);
        IsBoss = true;
    }

    public float HpRatio => _hp / _maxHp;

    /// <summary>
    /// A Label node shapes its text once; drawing the same outlined text from
    /// _Draw every frame cost ~10 ms each time on a laptop GPU.
    /// </summary>
    private Label MakeBossLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Size = new Vector2(240, 24),
            Position = new Vector2(-120, -Radius * 1.7f - 34),
        };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", GameConfig.RarityColor("Mythic"));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    public override void _Process(double delta)
    {
        // Redraw once per rendered frame (not per physics step), and only when awake.
        if (_asleep || _dead)
            return;
        // Two-frame animation: quick while moving, slow while idle.
        _animTime += (float)delta * (Velocity.LengthSquared() > 1f ? 6f : 2f);
        if (Mathf.Abs(_target.GlobalPosition.X - GlobalPosition.X) > 4f)
            _faceLeft = _target.GlobalPosition.X < GlobalPosition.X;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead)
            return;
        _asleep = !AlwaysAggro && _target.GlobalPosition.DistanceSquaredTo(GlobalPosition) > SleepRange * SleepRange;
        if (_asleep)
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
        if (_stunLeft > 0f)
        {
            _stunLeft -= dt;
            return;
        }

        var toPlayer = _target.GlobalPosition - GlobalPosition;
        var fromHome = GlobalPosition - _home;
        if (AlwaysAggro || (toPlayer.Length() < AggroRange && fromHome.Length() < LeashRange))
        {
            if (toPlayer.Length() <= Radius + PlayerRadius + 8f)
            {
                Velocity = Vector2.Zero;
                if (_attackTimer <= 0f)
                {
                    _attackTimer = AttackCooldown;
                    _target.TakeDamage(_damage);
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

    /// <summary>On-screen sprite height relative to the collision radius.</summary>
    private const float SpriteScale = 2.6f;

    public override void _Draw()
    {
        var feet = new Vector2(0, Radius * 0.9f);
        float height = Radius * SpriteScale;
        // Ground shadow with a rarity-coloured rim; corrupted monsters stand in a red haze.
        if (Corrupted)
            Shapes.Ellipse(this, feet, new Vector2(Radius * 1.5f, Radius * 0.6f), new Color(1f, 0.15f, 0.15f, 0.40f));
        Shapes.Ellipse(this, feet, new Vector2(Radius * 1.05f, Radius * 0.4f), new Color(0, 0, 0, 0.3f));
        Shapes.Ring(this, feet, Radius * 0.9f, new Color(GameConfig.RarityColor(_species.Rarity), 0.55f), thin: true);

        var tint = _flash > 0f ? new Color(1f, 0.55f, 0.55f)
            : _burnLeft > 0f ? new Color(1f, 0.75f, 0.55f)
            : Corrupted ? new Color(1f, 0.82f, 0.82f)
            : Colors.White;
        MonsterArt.Draw(this, SpeciesId, (int)_animTime, feet, height, _faceLeft, tint);

        float top = feet.Y - height;
        if (_stunLeft > 0f)
            Shapes.Ring(this, new Vector2(0, top - 6), 8f, new Color(1f, 1f, 0.4f));
        if (_hp < _maxHp)
        {
            float w = Radius * 2.2f;
            DrawRect(new Rect2(-w / 2, top - 8, w, 5), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(-w / 2, top - 8, w * _hp / _maxHp, 5), new Color(0.9f, 0.2f, 0.2f));
        }
    }
}
