using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>
/// Stage preparation, laid out like the mockup: Stage Info (story, recommended
/// CP, main enemies, possible drops) on the left; Party and Stage Rewards on
/// the right with Start Battle and Sweep.
/// </summary>
public partial class StagePrepPanel : Control
{
    /// <summary>Raised when Start Battle is pressed on an unlocked stage.</summary>
    public event Action<StageDef>? StartPressed;

    public Texture2D? MapArt { get; set; }

    private StageDef _stage = null!;
    private Vector2 _mapPoint;
    private PanelContainer _window = null!;
    private Label _title = null!;
    private HBoxContainer _columns = null!;
    private Label _sweepResult = null!;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true })
                Visible = false;
        };
        AddChild(dim);

        _window = new PanelContainer();
        _window.AddThemeStyleboxOverride("panel", UiKit.NavyStyle(UiKit.Gold, 3, 8, 14f));
        UiKit.Place(_window, new Vector2(0.5f, 0.5f), new Vector2(-600, -330), new Vector2(1200, 660));
        AddChild(_window);
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 12);
        _window.AddChild(outer);

        var header = new HBoxContainer();
        outer.AddChild(header);
        _title = UiKit.MakeLabel("", 28, UiKit.Cream);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(56, 48) };
        close.Pressed += () => Visible = false;
        header.AddChild(close);

        _columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _columns.AddThemeConstantOverride("separation", 14);
        outer.AddChild(_columns);
    }

    public void Show(StageDef stage, Vector2 mapPoint)
    {
        _stage = stage;
        _mapPoint = mapPoint;
        Rebuild();
        Visible = true;
    }

    private void Rebuild()
    {
        foreach (var child in _columns.GetChildren())
            child.QueueFree();
        var stage = _stage;
        _title.Text = $"Chapter {stage.Chapter} - Stage {stage.Number}   {stage.Name}";

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _columns.AddChild(left);
        BuildStageInfo(left);
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(580, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 12);
        _columns.AddChild(right);
        BuildParty(right);
        BuildRewards(right);
        BuildButtons(right);
    }

    private void BuildStageInfo(Control parent)
    {
        var stage = _stage;
        var box = UiKit.TitledPanel(parent, "Stage Info", out var panel);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        box.AddChild(top);
        if (MapArt != null)
        {
            var size = MapArt.GetSize();
            var region = new Vector2(size.X * 0.24f, size.X * 0.24f / 1.5f);
            // The scenery just above the stage's painted circle.
            var corner = (_mapPoint * size - new Vector2(region.X / 2f, region.Y * 1.3f)).Clamp(Vector2.Zero, size - region);
            top.AddChild(new TextureRect
            {
                Texture = new AtlasTexture { Atlas = MapArt, Region = new Rect2(corner, region) },
                CustomMinimumSize = new Vector2(150, 100),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            });
        }
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        top.AddChild(text);
        text.AddChild(UiKit.MakeLabel($"{stage.Number}. {stage.Name}", 22, stage.IsBoss ? new Color(1f, 0.6f, 0.5f) : UiKit.Cream));
        string treasure = Data.ChestOpened(stage.Id, PlayerData.TreasureBit) ? "found" : "hidden";
        string puzzle = Data.ChestOpened(stage.Id, PlayerData.PuzzleBit) ? "solved" : PuzzleName(stage.Puzzle);
        var blurb = UiKit.MakeLabel((stage.Story != "" ? stage.Story : $"{stage.Landmark}, {StageCatalog.Chapter(stage.Chapter).Region}.") +
            $"\nTreasure: {treasure}   •   Puzzle: {puzzle}", 16, UiKit.Muted);
        blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        blurb.CustomMinimumSize = new Vector2(360, 0);
        text.AddChild(blurb);

        int cp = Data.CombatPower();
        var cpRow = new HBoxContainer();
        cpRow.AddThemeConstantOverride("separation", 10);
        box.AddChild(cpRow);
        cpRow.AddChild(UiKit.MakeLabel("✦ Recommended CP", 18, UiKit.Cream));
        cpRow.AddChild(UiKit.MakeLabel($"⚔ {stage.RecommendedCp}", 20, Colors.White));
        cpRow.AddChild(UiKit.MakeLabel($"(yours {cp})", 18, cp >= stage.RecommendedCp ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.5f)));

        box.AddChild(UiKit.MakeLabel("✦ Main Enemies", 18, UiKit.Cream));
        var enemies = new HBoxContainer();
        enemies.AddThemeConstantOverride("separation", 14);
        box.AddChild(enemies);
        foreach (var species in stage.EnemySpecies.Take(5))
            enemies.AddChild(EnemyChip(species, species == stage.Boss));

        box.AddChild(UiKit.MakeLabel("✦ Possible Drops", 18, UiKit.Cream));
        var drops = new HBoxContainer();
        drops.AddThemeConstantOverride("separation", 8);
        box.AddChild(drops);
        foreach (var name in new[] { "Gold" }.Concat(stage.PossibleMaterials).Take(7))
            drops.AddChild(UiKit.ItemSlot(name, side: 52f));
    }

    private static Control EnemyChip(string species, bool boss)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(88, 0) };
        var sprite = MonsterArt.SheetSprite(species) ?? MonsterArt.Frame(species, 0);
        box.AddChild(new TextureRect
        {
            Texture = sprite,
            CustomMinimumSize = new Vector2(88, 60),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
        });
        var name = UiKit.MakeLabel(boss ? $"BOSS\n{species}" : species, 13, boss ? new Color(1f, 0.55f, 0.5f) : UiKit.Cream);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(name);
        return box;
    }

    private void BuildParty(Control parent)
    {
        var box = UiKit.TitledPanel(parent, "Party / Preparation", out _);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        box.AddChild(row);
        row.AddChild(PartyCard(CharacterArt.Portrait(Data.Gender), $"Lv. {Data.Level}", false));
        if (Data.BattlePet is { } pet)
            row.AddChild(PartyCard(MonsterArt.SheetSprite(pet.Species) ?? MonsterArt.Frame(pet.Species, 0), $"Lv. {pet.Level}", true));
        else
            row.AddChild(PartyCard(null, "Companion", false));
        var note = UiKit.MakeLabel(Data.BattlePet is { } p
                ? $"{p.Species} fights beside you.\nChange it in Hearthwood under Echoes."
                : "No battle companion yet.\nPick one in Hearthwood under Echoes.", 15, UiKit.Muted);
        note.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(note);
    }

    private static Control PartyCard(Texture2D? picture, string caption, bool flip)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(96, 112) };
        frame.AddThemeStyleboxOverride("panel", UiKit.NavyStyle(UiKit.Gold.Darkened(0.2f), 2, 4, 4f));
        var box = new VBoxContainer();
        frame.AddChild(box);
        if (picture != null)
            box.AddChild(new TextureRect
            {
                Texture = picture,
                CustomMinimumSize = new Vector2(84, 78),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest,
                FlipH = flip,
            });
        else
        {
            var plus = UiKit.MakeLabel("+", 44, UiKit.Muted);
            plus.HorizontalAlignment = HorizontalAlignment.Center;
            plus.CustomMinimumSize = new Vector2(84, 78);
            box.AddChild(plus);
        }
        var label = UiKit.MakeLabel(caption, 15, UiKit.Cream);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(label);
        return frame;
    }

    private void BuildRewards(Control parent)
    {
        var stage = _stage;
        bool first = !Data.StageCleared(stage.Id);
        var box = UiKit.TitledPanel(parent, first ? "Stage Rewards (First Clear)" : "Stage Rewards (Replay)", out _);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        box.AddChild(row);
        row.AddChild(UiKit.ItemSlot("XP", (first ? stage.Xp : stage.Xp / 2).ToString()));
        row.AddChild(UiKit.ItemSlot("Gold", (first ? stage.Gold : stage.Gold / 2).ToString()));
        if (first && stage.SkillBoxFirstClear)
            row.AddChild(UiKit.ItemSlot("Skill Box", "1"));
        foreach (var name in GameConfig.ChapterMaterials(stage.Chapter).Append("Crystal Shard"))
            row.AddChild(UiKit.ItemSlot(name, "chest"));
        var note = UiKit.MakeLabel("Stars: ★ clear  ★ within 3:00  ★ finish above half HP", 15, UiKit.Muted);
        box.AddChild(note);
    }

    private void BuildButtons(Control parent)
    {
        var stage = _stage;
        bool unlocked = Data.IsStageUnlocked(stage);
        int stars = Data.StageStars.GetValueOrDefault(stage.Id);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 14);
        parent.AddChild(row);

        _sweepResult = UiKit.MakeLabel("", 15, new Color(0.6f, 1f, 0.7f));
        _sweepResult.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _sweepResult.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sweepResult.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        row.AddChild(_sweepResult);

        var sweep = new Button
        {
            Text = stars >= 3 ? "Sweep" : "Sweep\n(3★ first)",
            Disabled = stars < 3,
            CustomMinimumSize = new Vector2(150, 70),
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
        };
        sweep.Pressed += () =>
        {
            if (Data.SweepStage(stage) is not { } result)
                return;
            string mats = result.Materials.Count == 0 ? "no materials" : string.Join(", ", result.Materials.GroupBy(m => m).Select(g => $"{g.Key} ×{g.Count()}"));
            _sweepResult.Text = $"Swept! +{result.Reward.Gold} gold, +{result.Reward.Xp} XP, {mats}";
        };
        row.AddChild(sweep);

        var start = UiKit.GoldButton(unlocked ? "Start Battle" : "Locked", new Vector2(260, 70));
        start.Disabled = !unlocked;
        start.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        start.Pressed += () => StartPressed?.Invoke(stage);
        row.AddChild(start);
    }

    private static string PuzzleName(PuzzleKind kind) => kind switch
    {
        PuzzleKind.RunePlates => "Rune Plates",
        PuzzleKind.Braziers => "Braziers",
        PuzzleKind.Levers => "Levers",
        _ => "Echo Crystals",
    };
}
