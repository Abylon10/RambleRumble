using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// The player's character: movement (keyboard or virtual joystick), a basic
/// attack, and the equipped skills. Attacks auto-aim at the nearest monster,
/// which matters on touch screens. Drawn with the animated character art.
/// </summary>
public partial class Player : CharacterBody2D
{
    public event Action? Defeated;

    private const float Speed = 230f;
    public const float BodyRadius = 18f;
    private const float AutoAimRange = 280f;
    private const float SafeZoneRegen = 25f;
    private const float DashSpeed = 1400f;
    private const float DashTime = 0.16f;
    /// <summary>How far below the body centre the sprite's feet sit.</summary>
    private const float FeetY = 20f;

    public float Hp { get; private set; } = 100f;
    public Vector2 JoystickVector { get; set; }
    public Vector2 Facing { get; private set; } = Vector2.Right;
    public bool InSafeZone { get; set; } = true;
    /// <summary>True while at a Wilderness campfire (World heals the player there).</summary>
    public bool Resting { get; set; }

    /// <summary>Action id -> seconds of cooldown left.</summary>
    private readonly Dictionary<string, float> _cooldowns = new();
    private float _dashLeft;
    private Vector2 _dashDir;
    private float _dashDamage;
    private readonly HashSet<Monster> _dashHit = new();
    private float _invuln;
    private float _flash;
    private AnimatedSprite2D _sprite = null!;
    private string _spriteGender = "";
    private bool _playingOneShot;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        AddToGroup("player");
        CollisionLayer = 1;
        CollisionMask = 4;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = BodyRadius } });
        var camera = new Camera2D { PositionSmoothingEnabled = true };
        AddChild(camera);
        camera.MakeCurrent();
        Hp = Data.MaxHp;

        _sprite = new AnimatedSprite2D { TextureFilter = TextureFilterEnum.Nearest };
        _sprite.AnimationFinished += () => _playingOneShot = false;
        AddChild(_sprite);
        ApplyGender();
    }

    public override void _Process(double delta)
    {
        if (_spriteGender != Data.Gender)
            ApplyGender();
        var frames = _sprite.SpriteFrames;
        if (!_playingOneShot)
        {
            string locomotion = _dashLeft > 0f || Velocity.Length() > Speed * 0.6f ? "run"
                : Velocity.Length() > 5f ? "walk" : "idle";
            if (_sprite.Animation != locomotion)
                _sprite.Play(locomotion);
        }
        _sprite.FlipH = Facing.X < 0f;
        _sprite.Modulate = _flash > 0f ? new Color(1f, 0.5f, 0.5f) : Colors.White;
        // Strips are bottom-aligned, so put each frame's bottom edge at the feet.
        var texture = frames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        _sprite.Offset = new Vector2(0, -texture.GetHeight() / 2f + FeetY);
    }

    private void ApplyGender()
    {
        _spriteGender = Data.Gender;
        _sprite.SpriteFrames = CharacterArt.FramesFor(_spriteGender);
        _playingOneShot = false;
        _sprite.Play("idle");
    }

    /// <summary>Plays a non-looping animation (attack, hurt, victory) before returning to idle/walk/run.</summary>
    public void PlayOneShot(string animation)
    {
        if (!_sprite.SpriteFrames.HasAnimation(animation))
            return;
        _sprite.Play(animation);
        _sprite.Frame = 0;
        _playingOneShot = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        foreach (var key in _cooldowns.Keys.ToList())
            _cooldowns[key] = Mathf.Max(0f, _cooldowns[key] - dt);
        _invuln = Mathf.Max(0f, _invuln - dt);
        _flash = Mathf.Max(0f, _flash - dt);
        QueueRedraw();

        if (_dashLeft > 0f)
        {
            _dashLeft -= dt;
            Velocity = _dashDir * DashSpeed;
            MoveAndSlide();
            ApplyDashHits();
            return;
        }

        var move = (Input.GetVector("move_left", "move_right", "move_up", "move_down") + JoystickVector).LimitLength(1f);
        if (move.Length() > 0.1f)
            Facing = move.Normalized();
        Velocity = move * Speed;
        MoveAndSlide();

        if (InSafeZone)
            Hp = Mathf.Min(Data.MaxHp, Hp + SafeZoneRegen * dt);

        if (Input.IsActionPressed("attack"))
            BasicAttack();
        for (int slot = 0; slot < 4; slot++)
        {
            if (Input.IsActionJustPressed($"skill_{slot + 1}"))
                UseSkill(slot);
        }
    }

    /// <summary>1.0 right after use, 0.0 when ready.</summary>
    public float CooldownRatio(string actionId, float total) =>
        Mathf.Clamp(_cooldowns.GetValueOrDefault(actionId) / total, 0f, 1f);

    public void BasicAttack()
    {
        if (!StartCooldown("basic", GameConfig.BasicAttackCooldown))
            return;
        Aim();
        PlayOneShot("attack");
        float damage = Data.AttackPower * GameConfig.BasicAttackDamage;
        foreach (var monster in MonstersInArc(GameConfig.BasicAttackRange))
            monster.TakeDamage(damage);
        Fx.Spawn(GetParent(), GlobalPosition + Facing * 34f, FxKind.Ring, radius: 30f, duration: 0.15f);
    }

    public void UseSkill(int slot)
    {
        if (slot >= Data.EquippedSkills.Count)
            return;
        var skill = GameConfig.Skill(Data.EquippedSkills[slot]);
        if (!StartCooldown(skill.Id, skill.Cooldown))
            return;
        Aim();
        PlayOneShot("attack");
        float damage = Data.AttackPower * skill.Damage;
        switch (skill.Id)
        {
            case "flame_fist":
                foreach (var monster in MonstersInArc(84f))
                {
                    monster.TakeDamage(damage);
                    monster.Burn(damage * 0.25f, 2f);
                }
                Fx.Spawn(GetParent(), GlobalPosition + Facing * 45f, FxKind.Circle, skill.Color, radius: 46f, duration: 0.25f);
                break;
            case "wind_slash":
                GetParent().AddChild(new Projectile
                {
                    Position = GlobalPosition + Facing * 20f,
                    Direction = Facing,
                    Damage = damage,
                    Tint = skill.Color,
                });
                break;
            case "stone_breaker":
                foreach (var monster in AllMonsters().Where(m => m.GlobalPosition.DistanceTo(GlobalPosition) <= 120f + m.Radius))
                {
                    monster.TakeDamage(damage);
                    monster.Stun(1.2f);
                }
                Fx.Spawn(GetParent(), GlobalPosition, FxKind.Ring, skill.Color, radius: 120f, duration: 0.35f);
                break;
            case "thunder_dash":
                _dashLeft = DashTime;
                _dashDir = Facing;
                _dashDamage = damage;
                _dashHit.Clear();
                _invuln = DashTime + 0.1f;
                Fx.Spawn(GetParent(), GlobalPosition, FxKind.Line, skill.Color, duration: 0.3f, to: Facing * DashSpeed * DashTime);
                break;
        }
    }

    public void TakeDamage(float amount)
    {
        if (_invuln > 0f || InSafeZone)
            return;
        Hp -= amount;
        _flash = 0.12f;
        PlayOneShot("hurt");
        Fx.FloatingText(GetParent(), GlobalPosition + new Vector2(0, -BodyRadius - 12), $"-{Mathf.RoundToInt(amount)}", new Color(1f, 0.4f, 0.4f));
        if (Hp <= 0f)
        {
            Hp = Data.MaxHp;
            Defeated?.Invoke();
        }
    }

    public void HealFull() => Hp = Data.MaxHp;

    public void Heal(float amount) => Hp = Mathf.Min(Data.MaxHp, Hp + amount);

    private bool StartCooldown(string actionId, float seconds)
    {
        if (_cooldowns.GetValueOrDefault(actionId) > 0f)
            return false;
        _cooldowns[actionId] = seconds;
        return true;
    }

    private IEnumerable<Monster> AllMonsters() => GetTree().GetNodesInGroup("monsters").OfType<Monster>();

    private void Aim()
    {
        var nearest = AllMonsters()
            .Where(m => m.GlobalPosition.DistanceTo(GlobalPosition) < AutoAimRange)
            .MinBy(m => m.GlobalPosition.DistanceTo(GlobalPosition));
        if (nearest != null)
            Facing = (nearest.GlobalPosition - GlobalPosition).Normalized();
    }

    private List<Monster> MonstersInArc(float reach)
    {
        return AllMonsters().Where(m =>
        {
            var offset = m.GlobalPosition - GlobalPosition;
            if (offset.Length() > reach + m.Radius)
                return false;
            return offset.Length() < BodyRadius + m.Radius || Facing.Dot(offset.Normalized()) > 0.3f;
        }).ToList();
    }

    private void ApplyDashHits()
    {
        foreach (var monster in AllMonsters().ToList())
        {
            if (_dashHit.Contains(monster))
                continue;
            if (monster.GlobalPosition.DistanceTo(GlobalPosition) <= BodyRadius + monster.Radius + 16f)
            {
                _dashHit.Add(monster);
                monster.TakeDamage(_dashDamage);
            }
        }
    }

    public override void _Draw()
    {
        // Ground shadow under the feet; the sprite (a child) draws on top.
        Shapes.Ellipse(this, new Vector2(0, FeetY - 2), new Vector2(BodyRadius, BodyRadius * 0.35f), new Color(0, 0, 0, 0.3f));
        if (_invuln > 0f)
            Shapes.Ring(this, Vector2.Zero, BodyRadius + 5, new Color(1f, 0.95f, 0.4f, 0.8f));
    }
}
