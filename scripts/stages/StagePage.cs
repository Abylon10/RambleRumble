using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

public enum StageOutcome
{
    Playing,
    Victory,
    Defeat,
}

/// <summary>
/// One stage on its own page: a long isometric strip of land. Enemy groups
/// wait along the road (the boss by the flag on stage 10); a treasure chest
/// hides off the road and a puzzle guards a second chest. Defeat every enemy
/// and reach the goal flag to clear the stage. Only this stage is loaded
/// while it runs.
/// </summary>
public partial class StagePage : Node2D
{
    private const float CameraZoom = 1.3f;
    private const float GoalRadius = 60f;

    public StageDef Stage { get; init; } = null!;
    public StageMap Map { get; private set; } = null!;
    public Player Player { get; private set; } = null!;
    public Hud Hud { get; private set; } = null!;
    /// <summary>Y-sorted layer holding scenery, chests, the player, the battle pet, monsters and effects.</summary>
    public Node2D Actors { get; private set; } = null!;
    public StagePuzzle Puzzle { get; private set; } = null!;
    public TreasureChestNode TreasureChest { get; private set; } = null!;
    public TreasureChestNode PuzzleChest { get; private set; } = null!;
    public StageOutcome Outcome { get; private set; } = StageOutcome.Playing;
    public PlayerData.StageReward? Reward { get; private set; }

    public event Action<StageOutcome>? Finished;

    private readonly List<Monster> _monsters = new();
    private readonly RandomNumberGenerator _rng = new();
    private Label _banner = null!;
    private Button _potionButton = null!;
    private Button _tonicButton = null!;
    private PanelContainer _resultPanel = null!;
    private Label _resultTitle = null!;
    private Label _resultBody = null!;
    private Button _resultPrimary = null!;
    private Button _resultSecondary = null!;
    private float _goalToastCooldown;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        _rng.Randomize();
        World.SetupInput();
        Map = StageMapBuilder.Build(Stage);

        AddChild(new StageBackdrop { Map = Map });
        // The ground is static: bake its ~1,000 polygons into one texture.
        var groundLayer = new Node2D();
        AddChild(groundLayer);
        var ground = Map.ScreenBounds().Grow(40);
        ground.Size += new Vector2(0, 5 * Iso.Step);
        Baker.Bake(this, new StageArena { Map = Map }, ground, baked => groundLayer.AddChild(baked));
        BuildCollision();

        // Puzzle pieces lie on the ground, under everything that stands.
        var pieces = Map.PuzzleTiles.Take(Stage.Puzzle == PuzzleKind.EchoCrystals ? 4 : 3)
            .Select(t => Iso.TileCenter(t.X, t.Y)).ToList();
        Puzzle = new StagePuzzle { Kind = Stage.Puzzle, PiecePositions = pieces, Seed = Stage.Number * 7 + Stage.Chapter };
        AddChild(Puzzle);

        Actors = new Node2D { YSortEnabled = true };
        AddChild(Actors);
        foreach (var decor in Map.Decor)
            Actors.AddChild(new IsoDecor { Spec = decor, Palette = Map.Palette });

        TreasureChest = AddChest(Map.TreasureChest, puzzle: false);
        PuzzleChest = AddChest(Map.PuzzleChest, puzzle: true);
        Puzzle.Hint += hint => Hud.Toast(hint, new Color(0.75f, 0.85f, 1f));
        Puzzle.Solved += () =>
        {
            PuzzleChest.Unlock();
            Hud.Toast("Puzzle solved! The chest's chains fall away.", new Color(0.6f, 1f, 0.7f));
            UpdateBanner();
        };
        if (Data.ChestOpened(Stage.Id, PlayerData.PuzzleBit))
            Puzzle.Visible = false;

        Player = new Player { Position = Map.StartPoint, InSafeZone = false };
        Player.Defeated += OnDefeated;
        Actors.AddChild(Player);
        Actors.AddChild(new BattlePetNode { Player = Player, Position = Map.StartPoint + new Vector2(-30, 10) });
        SetupCamera();

        Hud = new Hud { Player = Player };
        AddChild(Hud);
        Hud.SetArea(Stage.Title, StageCatalog.Chapter(Stage.Chapter).Accent);
        BuildStageUi();
        SpawnGroups();
        if (Stage.Story != "")
            Hud.Toast(Stage.Story, new Color(1f, 0.85f, 0.6f));
        Hud.Toast("Defeat every enemy, then reach the flag!", new Color(1f, 0.95f, 0.7f));
    }

    public override void _Process(double delta)
    {
        _goalToastCooldown -= (float)delta;
        if (Outcome != StageOutcome.Playing)
            return;
        if (Input.IsActionJustPressed("use_potion"))
            UsePotion();
        if (Input.IsActionJustPressed("use_tonic"))
            UseTonic();
        if (Player.GlobalPosition.DistanceTo(Map.GoalPoint) < GoalRadius)
        {
            if (EnemiesLeft == 0)
            {
                Win();
            }
            else if (_goalToastCooldown <= 0f)
            {
                _goalToastCooldown = 3f;
                Hud.Toast($"Defeat the remaining {EnemiesLeft} enemies first!", new Color(1f, 0.6f, 0.5f));
            }
        }
    }

    // --- Enemies --------------------------------------------------------------

    public IReadOnlyList<Monster> AliveMonsters => _monsters.Where(m => IsInstanceValid(m) && !m.IsQueuedForDeletion()).ToList();
    public int EnemiesLeft => AliveMonsters.Count;

    /// <summary>Each wave of the stage becomes a group waiting further along the road; the boss guards the flag.</summary>
    private void SpawnGroups()
    {
        int groups = Stage.Waves.Length;
        for (int k = 0; k < groups; k++)
        {
            var wave = Stage.Waves[k];
            bool last = k == groups - 1;
            float along = last && Stage.IsBoss ? 0.88f : 0.22f + 0.66f * (k + 0.5f) / groups;
            var centre = Map.RoadPoint(along);
            foreach (var (species, count) in wave.Enemies)
                for (int n = 0; n < count; n++)
                    Spawn(species, centre);
            if (wave.Leader != "")
                Spawn(wave.Leader, centre, eliteTitle: Stage.Corrupted ? "SWARM LEADER" : wave.Leader.ToUpper());
            if (last && Stage.IsBoss)
                Spawn(Stage.Boss, Map.RoadPoint(0.93f), boss: true);
        }
        UpdateBanner();
    }

    /// <summary>Adds one more enemy at a spot (tests and scripted events).</summary>
    public Monster SpawnAt(string species, Vector2 at)
    {
        Spawn(species, at);
        return _monsters[^1];
    }

    private void Spawn(string species, Vector2 centre, string? eliteTitle = null, bool boss = false)
    {
        // Find an open spot near the group's centre.
        var at = centre;
        for (int tries = 0; tries < 12; tries++)
        {
            var candidate = centre + new Vector2(_rng.RandfRange(-70, 70), _rng.RandfRange(-35, 35));
            var tile = Iso.ToTile(candidate);
            if (Map.IsOpen(tile.X, tile.Y))
            {
                at = candidate;
                break;
            }
        }
        var monster = new Monster();
        monster.Setup(species, at, Player, $"stage-{Stage.Id}");
        monster.ApplyScale(Stage.Scale);
        monster.Corrupted = Stage.Corrupted;
        if (eliteTitle != null)
            monster.MakeElite(eliteTitle, hpMult: 2.5f, sizeMult: 1.3f);
        if (boss)
            monster.MakeBoss();
        monster.Died += m =>
        {
            KillFeedback.OnMonsterDied(m, Actors, Hud);
            Callable.From(UpdateBanner).CallDeferred();
        };
        Actors.AddChild(monster);
        _monsters.Add(monster);
    }

    // --- Treasure, puzzle and items -------------------------------------------

    private TreasureChestNode AddChest(Vector2I tile, bool puzzle)
    {
        var chest = new TreasureChestNode { IsPuzzleChest = puzzle };
        chest.Setup(Iso.TileCenter(tile.X, tile.Y), Data.ChestOpened(Stage.Id, puzzle ? PlayerData.PuzzleBit : PlayerData.TreasureBit));
        chest.Opened += c =>
        {
            string loot = Data.OpenStageChest(Stage, c.IsPuzzleChest);
            Hud.Toast(loot == "" ? "Empty." : $"{(c.IsPuzzleChest ? "Puzzle chest" : "Treasure")}: {loot}", new Color(1f, 0.85f, 0.35f));
            UpdateBanner();
        };
        Actors.AddChild(chest);
        return chest;
    }

    public void UsePotion()
    {
        if (Outcome != StageOutcome.Playing || !Data.ConsumeItem("health_potion"))
            return;
        Player.Heal(Data.MaxHp * 0.5f);
        Fx.Spawn(Actors, Player.GlobalPosition, FxKind.Ring, new Color(0.95f, 0.35f, 0.4f), radius: 40f, duration: 0.4f);
        UpdateItemButtons();
    }

    public void UseTonic()
    {
        if (Outcome != StageOutcome.Playing || !Data.ConsumeItem("energy_tonic"))
            return;
        Player.RefillEnergy();
        Fx.Spawn(Actors, Player.GlobalPosition, FxKind.Ring, new Color(0.4f, 0.7f, 1f), radius: 40f, duration: 0.4f);
        UpdateItemButtons();
    }

    // --- Results ----------------------------------------------------------------

    /// <summary>3 stars above 60% HP, 2 above 25%, otherwise 1.</summary>
    public static int StarsFor(float hpRatio) => hpRatio >= 0.6f ? 3 : hpRatio >= 0.25f ? 2 : 1;

    private void Win()
    {
        Outcome = StageOutcome.Victory;
        Player.InSafeZone = true;
        Player.PlayOneShot("victory");
        int stars = StarsFor(Player.Hp / Data.MaxHp);
        Reward = Data.CompleteStage(Stage, stars);
        if (Reward.SkillLearned != "")
            KillFeedback.ShowSkillBox(Hud, Reward.SkillLearned, Reward.SkillIsNew);

        string starText = new string('★', stars) + new string('☆', 3 - stars);
        string chests = $"Treasure {(Data.ChestOpened(Stage.Id, PlayerData.TreasureBit) ? "✓" : "missed")}   " +
                        $"Puzzle {(Data.ChestOpened(Stage.Id, PlayerData.PuzzleBit) ? "✓" : "missed")}";
        string body = $"{starText}\n+{Reward.Gold} gold   +{Reward.Xp} XP" + (Reward.FirstClear ? "   (first clear)" : "   (replay: half rewards)") +
                      $"\n{chests}";
        var next = NextStage();
        bool canGoOn = next != null && Data.IsStageUnlocked(next);
        ShowResult("Stage Clear!", body, new Color(1f, 0.9f, 0.4f),
            canGoOn ? $"Next: {next!.Id}" : "Back to map",
            () =>
            {
                if (canGoOn)
                    Game.Instance?.StartStage(next!);
                else
                    Game.Instance?.ShowChapterMap(Stage.Chapter);
            },
            "Map", () => Game.Instance?.ShowChapterMap(next?.Chapter ?? Stage.Chapter));
        Finished?.Invoke(Outcome);
    }

    private void OnDefeated()
    {
        if (Outcome != StageOutcome.Playing)
            return;
        Outcome = StageOutcome.Defeat;
        Player.InSafeZone = true;
        foreach (var monster in AliveMonsters)
            monster.ProcessMode = ProcessModeEnum.Disabled;
        ShowResult("Defeated", "Train, level up, buy potions at the Market or equip stronger skills, then try again.", new Color(1f, 0.45f, 0.4f),
            "Retry", () => Game.Instance?.StartStage(Stage), "Map", () => Game.Instance?.ShowChapterMap(Stage.Chapter));
        Finished?.Invoke(Outcome);
    }

    private StageDef? NextStage()
    {
        var chapter = StageCatalog.Chapter(Stage.Chapter);
        if (Stage.Number < chapter.Stages.Length)
            return chapter.Stages[Stage.Number];
        return StageCatalog.Chapters.FirstOrDefault(c => c.Number == Stage.Chapter + 1)?.Stages[0];
    }

    // --- Setup ------------------------------------------------------------------

    /// <summary>Map edges, raised tiles and water block movement, as do solid scenery and chests.</summary>
    private void BuildCollision()
    {
        var body = new StaticBody2D { CollisionLayer = 4, CollisionMask = 0 };
        float inset = 0.25f;
        var corners = new[]
        {
            Iso.ToScreen(inset, inset), Iso.ToScreen(Map.W - inset, inset),
            Iso.ToScreen(Map.W - inset, Map.H - inset), Iso.ToScreen(inset, Map.H - inset),
        };
        for (int k = 0; k < 4; k++)
            body.AddChild(new CollisionShape2D { Shape = new SegmentShape2D { A = corners[k], B = corners[(k + 1) % 4] } });
        for (int i = 0; i < Map.W; i++)
            for (int j = 0; j < Map.H; j++)
                if (Map.IsBlockedTile(i, j))
                    body.AddChild(new CollisionShape2D { Shape = new ConvexPolygonShape2D { Points = Iso.Diamond(i, j) } });
        foreach (var decor in Map.Decor.Where(d => d.BlockRadius > 0f))
            body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = decor.BlockRadius }, Position = decor.Ground });
        AddChild(body);
    }

    private void SetupCamera()
    {
        var camera = Player.Camera;
        camera.Zoom = Vector2.One * CameraZoom;
        var bounds = Map.ScreenBounds();
        camera.LimitLeft = (int)bounds.Position.X - 80;
        camera.LimitRight = (int)bounds.End.X + 80;
        camera.LimitTop = (int)bounds.Position.Y - 80;
        camera.LimitBottom = (int)bounds.End.Y + 80;
        camera.ResetSmoothing();
    }

    private void BuildStageUi()
    {
        var ui = new CanvasLayer { Layer = 5 };
        AddChild(ui);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = new Theme { DefaultFontSize = 20 } };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ui.AddChild(root);

        var leave = new Button { Text = "◀ Map", CustomMinimumSize = new Vector2(110, 48) };
        UiKit.Place(leave, new Vector2(0.5f, 0), new Vector2(-55, 16), new Vector2(110, 48));
        leave.Pressed += () => Game.Instance?.ShowChapterMap(Stage.Chapter);
        root.AddChild(leave);
        // Stage status sits where the story quest tracker normally is.
        Hud.SetQuestTrackerVisible(false);
        var bannerPanel = new PanelContainer();
        bannerPanel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(StageCatalog.Chapter(Stage.Chapter).Accent));
        UiKit.Place(bannerPanel, new Vector2(1, 0), new Vector2(-406, 84), new Vector2(390, 0));
        root.AddChild(bannerPanel);
        _banner = UiKit.MakeLabel("", 16, new Color(1f, 0.95f, 0.8f));
        bannerPanel.AddChild(_banner);

        var items = new HBoxContainer();
        items.AddThemeConstantOverride("separation", 10);
        UiKit.Place(items, new Vector2(0.5f, 1f), new Vector2(-150, -70), new Vector2(300, 54));
        root.AddChild(items);
        _potionButton = new Button { CustomMinimumSize = new Vector2(140, 54) };
        _potionButton.Pressed += UsePotion;
        items.AddChild(_potionButton);
        _tonicButton = new Button { CustomMinimumSize = new Vector2(140, 54) };
        _tonicButton.Pressed += UseTonic;
        items.AddChild(_tonicButton);
        UpdateItemButtons();

        _resultPanel = new PanelContainer { Visible = false };
        _resultPanel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.9f, 0.75f, 0.35f), 3));
        UiKit.Place(_resultPanel, new Vector2(0.5f, 0.5f), new Vector2(-280, -150), new Vector2(560, 300));
        root.AddChild(_resultPanel);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 12);
        _resultPanel.AddChild(box);
        _resultTitle = UiKit.MakeLabel("", 34);
        _resultTitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_resultTitle);
        _resultBody = UiKit.MakeLabel("", 20);
        _resultBody.HorizontalAlignment = HorizontalAlignment.Center;
        _resultBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_resultBody);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        box.AddChild(buttons);
        _resultPrimary = new Button { CustomMinimumSize = new Vector2(180, 54) };
        _resultSecondary = new Button { CustomMinimumSize = new Vector2(140, 54) };
        buttons.AddChild(_resultPrimary);
        buttons.AddChild(_resultSecondary);
    }

    private void UpdateItemButtons()
    {
        _potionButton.Text = $"Potion ×{Data.ItemCount("health_potion")}";
        _potionButton.Disabled = Data.ItemCount("health_potion") == 0;
        _tonicButton.Text = $"Tonic ×{Data.ItemCount("energy_tonic")}";
        _tonicButton.Disabled = Data.ItemCount("energy_tonic") == 0;
    }

    private void UpdateBanner()
    {
        if (_banner == null || !IsInstanceValid(_banner))
            return;
        string goal = EnemiesLeft > 0 ? $"Enemies left {EnemiesLeft}" : "Reach the flag!";
        string treasure = Data.ChestOpened(Stage.Id, PlayerData.TreasureBit) ? "✓" : "?";
        string puzzle = Data.ChestOpened(Stage.Id, PlayerData.PuzzleBit) ? "✓" : Puzzle.IsSolved ? "open it" : "?";
        _banner.Text = $"{Stage.Title}\n{goal}   •   Treasure {treasure}   •   Puzzle {puzzle}";
    }

    private void ShowResult(string title, string body, Color colour, string primary, Action onPrimary, string secondary, Action onSecondary)
    {
        _resultTitle.Text = title;
        _resultTitle.AddThemeColorOverride("font_color", colour);
        _resultBody.Text = body;
        _resultPrimary.Text = primary;
        _resultSecondary.Text = secondary;
        _resultPrimary.Pressed += onPrimary;
        _resultSecondary.Pressed += onSecondary;
        _resultPanel.Visible = true;
    }
}
