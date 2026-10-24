using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// The player's character: movement (keyboard or virtual joystick), a basic
/// attack, and the equipped skills (cast by <see cref="SkillExecutor"/>, paid
/// for with Rumble energy). Attacks auto-aim at the nearest monster, which
/// matters on touch screens. Drawn with the animated character art.
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
    /// <summary>A skill only trains mastery if a non-trivial monster is this close.</summary>
    private const float MasteryRange = 450f;
    public const int MaxSkillSlots = 6;

    public float Hp { get; private set; } = 100f;
    public Vector2 JoystickVector { get; set; }
    public Vector2 Facing { get; private set; } = Vector2.Right;
    public bool InSafeZone { get; set; } = true;
    /// <summary>True while at a Wilderness campfire (World heals the player there).</summary>
    public bool Resting { get; set; }
    /// <summary>Rumble energy spent by skills; regenerates over time.</summary>
    public float Energy { get; private set; } = GameConfig.BaseMaxEnergy;
    /// <summary>Damage multiplier from Surge skills (1 when inactive).</summary>
    public float SurgeMult => _surgeLeft > 0f ? _surgeMult : 1f;
    public bool Guarding => _guardLeft > 0f;

    /// <summary>Action id -> seconds of cooldown left.</summary>
    private readonly Dictionary<string, float> _cooldowns = new();
    private float _dashLeft;
    private Vector2 _dashDir;
    private float _dashDamage;
    private readonly HashSet<Monster> _dashHit = new();
    private float _invuln;
    private float _flash;
    private float _surgeLeft;
    private float _surgeMult = 1f;
    private float _guardLeft;
    private float _guardReduction;
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
        _surgeLeft = Mathf.Max(0f, _surgeLeft - dt);
        _guardLeft = Mathf.Max(0f, _guardLeft - dt);
        Energy = Mathf.Min(Data.MaxEnergy, Energy + GameConfig.EnergyRegenPerSec * dt);
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
        for (int slot = 0; slot < MaxSkillSlots; slot++)
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
        float damage = Data.AttackPower * GameConfig.BasicAttackDamage * SurgeMult;
        foreach (var monster in MonstersInArc(GameConfig.BasicAttackRange))
            monster.TakeDamage(damage);
        Fx.Spawn(GetParent(), GlobalPosition + Facing * 34f, FxKind.Ring, radius: 30f, duration: 0.15f);
    }

    /// <summary>Casts the skill in an equipped slot if it's off cooldown and affordable.</summary>
    public bool UseSkill(int slot)
    {
        if (slot >= Data.EquippedSkills.Count)
            return false;
        var skill = SkillCatalog.Get(Data.EquippedSkills[slot]);
        if (!skill.Implemented || Energy < skill.Cost || _cooldowns.GetValueOrDefault(skill.Id) > 0f)
            return false;
        _cooldowns[skill.Id] = skill.Cooldown * Data.SkillCooldownMult(skill.Id);
        Energy -= skill.Cost;
        Aim();
        PlayOneShot("attack");
        float damage = Data.AttackPower * skill.Damage * Data.SkillDamageMult(skill.Id) * SurgeMult;
        SkillExecutor.Cast(this, skill, damage);
        if (InMeaningfulFight())
            Data.AddMastery(skill.Id, GameConfig.MasteryPerUse);
        return true;
    }

    /// <summary>Mastery only grows against enemies that aren't far below the player's level.</summary>
    private bool InMeaningfulFight() => AllMonsters().Any(m =>
        m.GlobalPosition.DistanceTo(GlobalPosition) < MasteryRange && m.Level >= Data.Level - GameConfig.TrivialLevelGap);

    public void StartDash(float damage, Color color)
    {
        _dashLeft = DashTime;
        _dashDir = Facing;
        _dashDamage = damage;
        _dashHit.Clear();
        _invuln = DashTime + 0.1f;
        Fx.Spawn(GetParent(), GlobalPosition, FxKind.Line, color, duration: 0.3f, to: Facing * DashSpeed * DashTime);
    }

    public void ApplySurge(float multiplier, float seconds)
    {
        _surgeMult = multiplier;
        _surgeLeft = seconds;
    }

    /// <param name="reduction">Fraction of incoming damage blocked (0.5 = half).</param>
    public void ApplyGuard(float reduction, float seconds)
    {
        _guardReduction = reduction;
        _guardLeft = seconds;
    }

    /// <summary>Tops energy up (e.g. when returning to the village).</summary>
    public void RefillEnergy() => Energy = Data.MaxEnergy;

    public void SetEnergy(float value) => Energy = Mathf.Clamp(value, 0f, Data.MaxEnergy);

    public void TakeDamage(float amount)
    {
        if (_invuln > 0f || InSafeZone)
            return;
        if (Guarding)
            amount *= 1f - _guardReduction;
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

    public IEnumerable<Monster> AllMonsters() => GetTree().GetNodesInGroup("monsters").OfType<Monster>();

    private void Aim()
    {
        var nearest = AllMonsters()
            .Where(m => m.GlobalPosition.DistanceTo(GlobalPosition) < AutoAimRange)
            .MinBy(m => m.GlobalPosition.DistanceTo(GlobalPosition));
        if (nearest != null)
            Facing = (nearest.GlobalPosition - GlobalPosition).Normalized();
    }

    public List<Monster> MonstersInArc(float reach)
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
            if (_dashDamage > 0f && monster.GlobalPosition.DistanceTo(GlobalPosition) <= BodyRadius + monster.Radius + 16f)
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
        if (Guarding)
            Shapes.Ring(this, Vector2.Zero, BodyRadius + 9, new Color(1f, 0.6f, 0.2f, 0.7f));
        if (_surgeLeft > 0f)
            Shapes.Ring(this, new Vector2(0, FeetY - 2), BodyRadius * 0.9f, new Color(1f, 0.45f, 0.15f, 0.6f));
    }
}
