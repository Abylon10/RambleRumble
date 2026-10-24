using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// Builds the world in code: the Lobby plot (safe zone with training, the pet
/// fence and a Raid Board placeholder) and one Wilderness zone.
/// </summary>
public partial class World : Node2D
{
    public static readonly Rect2 LobbyRect = new(-600, -400, 1200, 800);
    public static readonly Rect2 WildRect = new(1400, -900, 2400, 1800);
    private const float Wall = 40f;
    private static readonly Rect2 TrainingRect = new(-520, -320, 280, 220);
    private static readonly Rect2 FenceRect = new(-520, 80, 360, 240);
    private static readonly Vector2 RaidBoardPos = new(0, -330);
    private static readonly Vector2 LobbyPortal = new(520, 0);
    public static readonly Vector2 LobbySpawn = new(380, 120);
    private static readonly Vector2 WildPortal = new(1480, -160);
    public static readonly Vector2 WildSpawn = new(1620, 80);
    private const float PortalRadius = 46f;
    private const float SpawnInterval = 2.5f;

    public Player Player { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    public Node2D Monsters { get; private set; } = null!;
    public bool InWilderness { get; private set; }

    private bool _inTraining;
    private int _shownTrainingLevel;
    private float _spawnTimer;
    private readonly RandomNumberGenerator _rng = new();

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        _rng.Randomize();
        SetupInput();
        BuildWalls(LobbyRect);
        BuildWalls(WildRect);
        BuildZone(TrainingRect, SetTraining);
        BuildPortal(LobbyPortal, () => Teleport(WildSpawn, toWilderness: true));
        BuildPortal(WildPortal, () => Teleport(LobbySpawn, toWilderness: false));

        AddChild(new FenceDisplay { Area = FenceRect });

        Monsters = new Node2D();
        AddChild(Monsters);

        Player = new Player { Position = new Vector2(0, 120) };
        Player.Defeated += OnPlayerDefeated;
        AddChild(Player);

        Hud = new Hud { Player = Player };
        Hud.TrainPressed += OnTrainPressed;
        AddChild(Hud);
        _shownTrainingLevel = Data.TrainingLevel;
    }

    public override void _Process(double delta)
    {
        if (_inTraining)
            Data.Train(GameConfig.TrainIdleXpPerSec * (float)delta);
        if (Data.TrainingLevel > _shownTrainingLevel)
            Player.PlayOneShot("victory");
        _shownTrainingLevel = Data.TrainingLevel;

        _spawnTimer -= (float)delta;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = SpawnInterval;
            if (Monsters.GetChildCount() < GameConfig.MaxWildMonsters)
                SpawnMonster();
        }
    }

    // --- Setup ----------------------------------------------------------------

    private static void SetupInput()
    {
        var keys = new Dictionary<string, Key[]>
        {
            ["move_left"] = new[] { Key.A, Key.Left },
            ["move_right"] = new[] { Key.D, Key.Right },
            ["move_up"] = new[] { Key.W, Key.Up },
            ["move_down"] = new[] { Key.S, Key.Down },
            ["attack"] = new[] { Key.J, Key.Space },
            ["skill_1"] = new[] { Key.K },
            ["skill_2"] = new[] { Key.L },
            ["skill_3"] = new[] { Key.U },
            ["skill_4"] = new[] { Key.I },
        };
        foreach (var (action, actionKeys) in keys)
        {
            if (InputMap.HasAction(action))
                continue;
            InputMap.AddAction(action);
            foreach (var key in actionKeys)
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        }
    }

    private void BuildWalls(Rect2 rect)
    {
        var body = new StaticBody2D { CollisionLayer = 4, CollisionMask = 0 };
        Rect2[] walls =
        {
            new(rect.Position.X - Wall, rect.Position.Y - Wall, rect.Size.X + Wall * 2, Wall),
            new(rect.Position.X - Wall, rect.End.Y, rect.Size.X + Wall * 2, Wall),
            new(rect.Position.X - Wall, rect.Position.Y, Wall, rect.Size.Y),
            new(rect.End.X, rect.Position.Y, Wall, rect.Size.Y),
        };
        foreach (var wall in walls)
            body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = wall.Size }, Position = wall.GetCenter() });
        AddChild(body);
    }

    private void BuildZone(Rect2 rect, Action<bool> onChange)
    {
        var area = new Area2D { CollisionLayer = 0, CollisionMask = 1, Position = rect.GetCenter() };
        area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
        area.BodyEntered += body => { if (body is Player) onChange(true); };
        area.BodyExited += body => { if (body is Player) onChange(false); };
        AddChild(area);
    }

    private void BuildPortal(Vector2 position, Action onEnter)
    {
        var area = new Area2D { CollisionLayer = 0, CollisionMask = 1, Position = position };
        area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = PortalRadius } });
        // Physics state can't change inside the callback, so teleport deferred.
        area.BodyEntered += body => { if (body is Player) Callable.From(onEnter).CallDeferred(); };
        AddChild(area);
    }

    // --- Gameplay -------------------------------------------------------------

    public void Teleport(Vector2 destination, bool toWilderness)
    {
        Player.GlobalPosition = destination;
        Player.InSafeZone = !toWilderness;
        InWilderness = toWilderness;
        Hud.AreaName = toWilderness ? "Wilderness" : "Lobby";
        GetViewport().GetCamera2D()?.ResetSmoothing();
    }

    private void SetTraining(bool active)
    {
        _inTraining = active;
        Hud.SetTrainingAvailable(active);
    }

    public void OnTrainPressed()
    {
        Data.Train(GameConfig.TrainTapXp);
        Fx.Spawn(this, TrainingRect.GetCenter(), FxKind.Ring, new Color(0.5f, 0.9f, 1f), radius: 50f, duration: 0.25f);
    }

    public Monster SpawnMonster(string? speciesId = null, Vector2? at = null)
    {
        var position = at ?? RandomSpawnPoint();
        var monster = new Monster();
        monster.Setup(speciesId ?? PickSpecies(), position, Player);
        monster.Died += OnMonsterDied;
        Monsters.AddChild(monster);
        return monster;
    }

    private Vector2 RandomSpawnPoint()
    {
        var inner = WildRect.Grow(-80);
        var position = Vector2.Zero;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            position = new Vector2(_rng.RandfRange(inner.Position.X, inner.End.X), _rng.RandfRange(inner.Position.Y, inner.End.Y));
            if (position.DistanceTo(Player.GlobalPosition) > 350f && position.DistanceTo(WildSpawn) > 300f)
                break;
        }
        return position;
    }

    private string PickSpecies()
    {
        int roll = _rng.RandiRange(1, GameConfig.WildSpawns.Sum(s => s.Weight));
        foreach (var (species, weight) in GameConfig.WildSpawns)
        {
            roll -= weight;
            if (roll <= 0)
                return species;
        }
        return "Slime";
    }

    private void OnMonsterDied(Monster monster)
    {
        var drop = Data.RecordKill(monster.SpeciesId);
        Fx.FloatingText(this, monster.GlobalPosition, $"+{drop.Gold} gold", new Color(1f, 0.85f, 0.3f), 0.9f);
        if (drop.AddedPet is { } pet)
        {
            string note = drop.PityHit ? "  (pity!)" : "";
            Hud.Toast($"New pet: {pet.Size} {pet.Species} ({pet.Rarity}){note}", GameConfig.RarityColor(pet.Rarity));
        }
        if (drop.SkillBox)
        {
            if (drop.SkillLearned != "")
            {
                var skill = GameConfig.Skill(drop.SkillLearned);
                Hud.Toast($"Skill box: learned {skill.Name}!", skill.Color);
            }
            else
            {
                Hud.Toast($"Skill box: duplicate, +{GameConfig.SkillBoxDuplicateGold} gold", new Color(1f, 0.85f, 0.3f));
            }
        }
    }

    private void OnPlayerDefeated()
    {
        Hud.Toast("Defeated! Back to the Lobby.", new Color(1f, 0.4f, 0.4f));
        Callable.From(() => Teleport(LobbySpawn, toWilderness: false)).CallDeferred();
        Player.HealFull();
    }

    // --- Drawing (placeholder art) --------------------------------------------

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        DrawRect(LobbyRect.Grow(Wall), new Color(0.30f, 0.24f, 0.18f));
        DrawRect(LobbyRect, new Color(0.36f, 0.52f, 0.34f));
        DrawRect(WildRect.Grow(Wall), new Color(0.22f, 0.18f, 0.14f));
        DrawRect(WildRect, new Color(0.24f, 0.33f, 0.20f));
        // Scatter some deterministic "grass tufts" in the wilderness.
        var tufts = new RandomNumberGenerator { Seed = 7 };
        for (int i = 0; i < 220; i++)
        {
            var p = new Vector2(tufts.RandfRange(WildRect.Position.X, WildRect.End.X), tufts.RandfRange(WildRect.Position.Y, WildRect.End.Y));
            DrawCircle(p, tufts.RandfRange(6, 16), new Color(0.20f, 0.28f, 0.16f));
        }

        // Training ground with dummies.
        DrawRect(TrainingRect, new Color(0.55f, 0.45f, 0.30f));
        DrawRect(TrainingRect, new Color(0.35f, 0.28f, 0.18f), false, 4f);
        foreach (int x in new[] { -460, -380, -300 })
        {
            DrawRect(new Rect2(x - 6, -240, 12, 50), new Color(0.45f, 0.30f, 0.18f));
            DrawCircle(new Vector2(x, -248), 16, new Color(0.85f, 0.75f, 0.55f));
        }
        DrawString(font, TrainingRect.Position + new Vector2(0, -12), "TRAINING", HorizontalAlignment.Center, TrainingRect.Size.X, 22);

        // Pet fence.
        DrawRect(FenceRect, new Color(0.42f, 0.60f, 0.36f));
        var post = new Color(0.55f, 0.38f, 0.22f);
        for (int i = 0; i < 13; i++)
        {
            float x = FenceRect.Position.X + i * FenceRect.Size.X / 12f;
            DrawRect(new Rect2(x - 4, FenceRect.Position.Y - 4, 8, 14), post);
            DrawRect(new Rect2(x - 4, FenceRect.End.Y - 10, 8, 14), post);
        }
        DrawRect(FenceRect, post, false, 3f);
        DrawString(font, FenceRect.Position + new Vector2(0, -12), "PET FENCE", HorizontalAlignment.Center, FenceRect.Size.X, 20);

        // Raid Board placeholder.
        DrawRect(new Rect2(RaidBoardPos - new Vector2(80, 30), new Vector2(160, 60)), new Color(0.35f, 0.22f, 0.15f));
        DrawString(font, RaidBoardPos + new Vector2(-80, 8), "RAID BOARD", HorizontalAlignment.Center, 160, 18);
        DrawString(font, RaidBoardPos + new Vector2(-80, 50), "(Phase 4)", HorizontalAlignment.Center, 160, 16, new Color(1, 1, 1, 0.6f));

        // Portals.
        foreach (var (position, label) in new[] { (LobbyPortal, "WILDERNESS"), (WildPortal, "LOBBY") })
        {
            DrawCircle(position, PortalRadius, new Color(0.45f, 0.30f, 0.85f, 0.7f));
            DrawArc(position, PortalRadius, 0f, Mathf.Tau, 40, new Color(0.8f, 0.7f, 1f), 4f);
            DrawString(font, position + new Vector2(-90, -PortalRadius - 12), label, HorizontalAlignment.Center, 180, 20);
        }
    }
}
