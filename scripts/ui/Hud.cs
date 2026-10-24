using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble.UI;

/// <summary>
/// In-game HUD built in code: stats (level, class, XP, HP, energy), quest
/// tracker, joystick and action buttons, toasts, dialogue, and the
/// character-creation, pets, skills and upgrades panels.
/// </summary>
public partial class Hud : CanvasLayer
{
    public event Action? TrainPressed;
    public event Action? TalkPressed;

    public Player Player { get; set; } = null!;
    public string AreaName { get; private set; } = "Hearthwood Village";

    private Label _levelLabel = null!;
    private ProgressBar _xpBar = null!;
    private Label _cpLabel = null!;
    private Label _goldLabel = null!;
    private Label _trainLabel = null!;
    private ProgressBar _trainBar = null!;
    private ProgressBar _hpBar = null!;
    private ProgressBar _energyBar = null!;
    private Label _areaLabel = null!;
    private TextureRect _portrait = null!;
    private Label _questTitle = null!;
    private Label _questObjective = null!;
    private bool _questTrackerAllowed = true;
    private PanelContainer _questPanel = null!;
    private Label _toastLabel = null!;
    private readonly Queue<(string Text, Color Color)> _toasts = new();
    private float _toastTime;
    private MoveJoystick _joystick = null!;
    private TouchButton _attackButton = null!;
    private readonly List<TouchButton> _skillButtons = new();
    private Button _trainButton = null!;
    private Button _talkButton = null!;
    private Label _skillsHeader = null!;
    private VBoxContainer _skillsList = null!;

    // Exposed so tests and screenshots can open panels directly.
    public PanelContainer StarterPanel { get; private set; } = null!;
    public PetsPanel PetsPanel { get; private set; } = null!;
    public UpgradesPanel UpgradesPanel { get; private set; } = null!;
    public MarketPanel MarketPanel { get; private set; } = null!;
    public PanelContainer SkillsPanel { get; private set; } = null!;
    public DialoguePanel Dialogue { get; private set; } = null!;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = new Theme { DefaultFontSize = 20 } };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        BuildStats(root);
        BuildTopButtons(root);
        BuildQuestTracker(root);
        BuildControls(root);
        BuildToast(root);
        Dialogue = new DialoguePanel();
        root.AddChild(Dialogue);
        PetsPanel = new PetsPanel();
        root.AddChild(PetsPanel);
        UpgradesPanel = new UpgradesPanel();
        root.AddChild(UpgradesPanel);
        MarketPanel = new MarketPanel();
        root.AddChild(MarketPanel);
        SkillsPanel = BuildSkillsPanel(root);
        StarterPanel = BuildStarterPanel(root);
        StarterPanel.Visible = !Data.HasStarter;

        Data.Changed += Refresh;
        Data.Notify += Toast;
        Refresh();
        if (Data.OfflineGold > 0)
        {
            Toast($"While you were away, your pets earned {Data.OfflineGold} gold!", new Color(1f, 0.85f, 0.3f));
            Data.OfflineGold = 0;
        }
    }

    public override void _ExitTree()
    {
        Data.Changed -= Refresh;
        Data.Notify -= Toast;
    }

    public override void _Process(double delta)
    {
        Player.JoystickVector = _joystick.Vector;
        if (_attackButton.Held)
            Player.BasicAttack();
        for (int i = 0; i < _skillButtons.Count; i++)
        {
            if (_skillButtons[i].Held)
                Player.UseSkill(i);
        }

        _hpBar.MaxValue = Data.MaxHp;
        _hpBar.Value = Player.Hp;
        _energyBar.MaxValue = Data.MaxEnergy;
        _energyBar.Value = Player.Energy;
        _areaLabel.Text = AreaName;
        _attackButton.CooldownRatio = Player.CooldownRatio("basic", GameConfig.BasicAttackCooldown);
        for (int i = 0; i < _skillButtons.Count && i < Data.EquippedSkills.Count; i++)
        {
            var skill = SkillCatalog.Get(Data.EquippedSkills[i]);
            _skillButtons[i].CooldownRatio = Player.CooldownRatio(skill.Id, skill.Cooldown * Data.SkillCooldownMult(skill.Id));
            // Dim the ring when there isn't enough energy to cast.
            _skillButtons[i].Tint = Player.Energy >= skill.Cost ? skill.Color : skill.Color.Darkened(0.6f);
        }

        if (_toastTime > 0f)
        {
            _toastTime -= (float)delta;
            if (_toastTime <= 0f)
                ShowNextToast();
        }
    }

    public void SetTrainingAvailable(bool available) => _trainButton.Visible = available;

    /// <summary>Stages show their own status in the tracker's place.</summary>
    public void SetQuestTrackerVisible(bool visible)
    {
        _questTrackerAllowed = visible;
        _questPanel.Visible = visible && Data.HasStarter;
    }

    public void SetTalkAvailable(bool available)
    {
        _talkButton.Visible = available;
        if (!available)
            Dialogue.Visible = false;
    }

    /// <summary>Shows where the player is (Village or a Wilderness zone) in the stats panel.</summary>
    public void SetArea(string name, Color color)
    {
        AreaName = name;
        _areaLabel.AddThemeColorOverride("font_color", color);
    }

    public void Toast(string text, Color color)
    {
        _toasts.Enqueue((text, color));
        if (_toastTime <= 0f)
            ShowNextToast();
    }

    /// <summary>Shows one panel and hides the others (blocked until a starter is chosen).</summary>
    public void Open(Control panel)
    {
        if (StarterPanel.Visible)
            return;
        foreach (var each in new Control[] { PetsPanel, SkillsPanel, UpgradesPanel, MarketPanel })
            each.Visible = each == panel;
        Dialogue.Visible = false;
        Refresh();
    }

    // --- Builders -------------------------------------------------------------

    private static ProgressBar Bar(float height, Color tint) =>
        new() { ShowPercentage = false, CustomMinimumSize = new Vector2(0, height), Modulate = tint };

    private void BuildStats(Control root)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.4f, 0.4f, 0.4f)));
        UiKit.Place(panel, Vector2.Zero, new Vector2(16, 16), new Vector2(300, 0));
        root.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        box.AddChild(header);
        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(44, 44),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        header.AddChild(_portrait);
        _areaLabel = UiKit.MakeLabel("", 18, new Color(0.9f, 0.75f, 0.5f));
        _areaLabel.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_areaLabel);
        _levelLabel = UiKit.MakeLabel("", 16, new Color(1f, 0.95f, 0.6f));
        box.AddChild(_levelLabel);
        _xpBar = Bar(8, new Color(1f, 0.9f, 0.4f));
        box.AddChild(_xpBar);
        _cpLabel = UiKit.MakeLabel("", 18);
        box.AddChild(_cpLabel);
        _goldLabel = UiKit.MakeLabel("", 18, new Color(1f, 0.85f, 0.3f));
        box.AddChild(_goldLabel);
        _hpBar = Bar(12, new Color(1f, 0.45f, 0.45f));
        box.AddChild(_hpBar);
        _energyBar = Bar(8, new Color(0.45f, 0.75f, 1f));
        box.AddChild(_energyBar);
        _trainLabel = UiKit.MakeLabel("", 14);
        box.AddChild(_trainLabel);
        _trainBar = Bar(6, Colors.White);
        box.AddChild(_trainBar);
    }

    private void BuildTopButtons(Control root)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        UiKit.Place(row, new Vector2(1, 0), new Vector2(-406, 16), new Vector2(390, 56));
        root.AddChild(row);
        foreach (var (text, panel) in new (string, Func<Control>)[]
                 { ("Echoes", () => PetsPanel), ("Skills", () => SkillsPanel), ("Upgrades", () => UpgradesPanel) })
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(123, 56) };
            button.Pressed += () => Open(panel());
            row.AddChild(button);
        }
    }

    private void BuildQuestTracker(Control root)
    {
        _questPanel = new PanelContainer();
        _questPanel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.9f, 0.75f, 0.35f)));
        UiKit.Place(_questPanel, new Vector2(1, 0), new Vector2(-406, 84), new Vector2(390, 0));
        root.AddChild(_questPanel);
        var box = new VBoxContainer();
        _questPanel.AddChild(box);
        _questTitle = UiKit.MakeLabel("", 17, new Color(1f, 0.85f, 0.4f));
        _questTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_questTitle);
        _questObjective = UiKit.MakeLabel("", 15, new Color(0.9f, 0.9f, 0.9f));
        _questObjective.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_questObjective);
    }

    private void BuildControls(Control root)
    {
        _joystick = new MoveJoystick();
        UiKit.Place(_joystick, new Vector2(0, 1), new Vector2(48, -268), new Vector2(220, 220));
        root.AddChild(_joystick);

        _attackButton = new TouchButton { Label = "Attack", Tint = new Color(0.95f, 0.95f, 0.95f) };
        UiKit.Place(_attackButton, new Vector2(1, 1), new Vector2(-188, -188), new Vector2(140, 140));
        root.AddChild(_attackButton);

        // Skill buttons (up to 6 slots by level) sit in an arc around the attack button.
        var offsets = new[]
        {
            new Vector2(-310, -140), new Vector2(-220, -310), new Vector2(-330, -260),
            new Vector2(-110, -300), new Vector2(-430, -150), new Vector2(-440, -290),
        };
        foreach (var offset in offsets)
        {
            var button = new TouchButton();
            UiKit.Place(button, new Vector2(1, 1), offset, new Vector2(100, 100));
            root.AddChild(button);
            _skillButtons.Add(button);
        }

        _trainButton = new Button { Text = "TRAIN  (tap!)", Visible = false };
        _trainButton.AddThemeFontSizeOverride("font_size", 28);
        UiKit.Place(_trainButton, new Vector2(0.5f, 1), new Vector2(-130, -110), new Vector2(260, 80));
        _trainButton.Pressed += () => TrainPressed?.Invoke();
        root.AddChild(_trainButton);

        _talkButton = new Button { Text = $"TALK  ({Quests.GiverName})", Visible = false };
        _talkButton.AddThemeFontSizeOverride("font_size", 24);
        // Above the Train button: Garrick stands right next to the Training Square.
        UiKit.Place(_talkButton, new Vector2(0.5f, 1), new Vector2(-170, -200), new Vector2(340, 80));
        _talkButton.Pressed += () => TalkPressed?.Invoke();
        root.AddChild(_talkButton);
    }

    private void BuildToast(Control root)
    {
        _toastLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _toastLabel.AddThemeFontSizeOverride("font_size", 26);
        _toastLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _toastLabel.AddThemeConstantOverride("outline_size", 8);
        // Below the quest tracker so long messages don't run into it.
        UiKit.Place(_toastLabel, new Vector2(0.5f, 0), new Vector2(-400, 170), new Vector2(800, 40));
        root.AddChild(_toastLabel);
    }

    private PanelContainer BuildStarterPanel(Control root)
    {
        var panel = new PanelContainer();
        root.AddChild(panel);
        var (box, _) = UiKit.MakeModal(panel, "The Adventurer Trial", new Vector2(760, 580), closable: false);

        var genderRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        genderRow.AddThemeConstantOverride("separation", 16);
        box.AddChild(genderRow);
        var genderButtons = new List<Button>();
        foreach (var gender in CharacterArt.Genders)
        {
            var button = new Button
            {
                Text = gender == "male" ? "Male" : "Female",
                Icon = CharacterArt.Portrait(gender),
                ExpandIcon = true,
                ToggleMode = true,
                ButtonPressed = Data.Gender == gender,
                CustomMinimumSize = new Vector2(220, 110),
                IconAlignment = HorizontalAlignment.Left,
            };
            button.Pressed += () =>
            {
                Data.SetGender(gender);
                foreach (var other in genderButtons)
                    other.ButtonPressed = other == button;
            };
            genderButtons.Add(button);
            genderRow.AddChild(button);
        }
        box.AddChild(UiKit.MakeLabel("Touch an awakening stone (your starter skill sets your class lineage)", 18, new Color(0.9f, 0.75f, 0.5f)));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        box.AddChild(grid);
        foreach (var skillId in SkillCatalog.StarterSkills)
        {
            var skill = SkillCatalog.Get(skillId);
            var lineage = Lineages.Get(skill.Lineage);
            var button = new Button
            {
                Text = $"{skill.Name}\n{skill.Desc}\n{lineage.Name}: {lineage.Classes[0]}",
                Icon = CharacterArt.SkillIcon(skillId),
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(360, 120),
                IconAlignment = HorizontalAlignment.Left,
            };
            button.AddThemeColorOverride("font_color", skill.Color);
            button.Pressed += () =>
            {
                Data.ChooseStarter(skillId);
                panel.Visible = false;
                Toast($"You awakened {skill.Name}! Class: {Data.ClassName}", skill.Color);
                Dialogue.Say(Quests.PrologueTitle, Quests.PrologueText, "Begin", () =>
                {
                    Dialogue.Visible = false;
                    Toast($"Talk to {Quests.GiverName} at the Training Square.", new Color(1f, 0.9f, 0.4f));
                });
            };
            grid.AddChild(button);
        }
        return panel;
    }

    private PanelContainer BuildSkillsPanel(Control root)
    {
        var panel = new PanelContainer();
        root.AddChild(panel);
        var (box, _) = UiKit.MakeModal(panel, "Skills", new Vector2(820, 600));
        _skillsHeader = UiKit.MakeLabel("", 16, new Color(0.85f, 0.85f, 0.85f));
        _skillsHeader.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_skillsHeader);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);
        _skillsList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _skillsList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_skillsList);
        return panel;
    }

    // --- Refresh --------------------------------------------------------------

    private void Refresh()
    {
        _portrait.Texture = CharacterArt.Portrait(Data.Gender);
        _levelLabel.Text = $"Lv {Data.Level}  {Data.Rank}  •  {Data.ClassName}";
        _xpBar.MaxValue = GameConfig.XpToNextLevel(Data.Level);
        _xpBar.Value = Data.Xp;
        _cpLabel.Text = $"CP  {Data.CombatPower()}";
        float income = Data.IncomePerMin();
        _goldLabel.Text = $"Gold  {Data.Gold}" + (income > 0f ? $"   (+{income:0.0}/min)" : "");
        int needed = GameConfig.TrainingXpNeeded(Data.TrainingLevel);
        _trainLabel.Text = $"Training Lv {Data.TrainingLevel}   ({Data.TrainingXp} / {needed})";
        _trainBar.MaxValue = needed;
        _trainBar.Value = Data.TrainingXp;
        RefreshQuest();

        for (int i = 0; i < _skillButtons.Count; i++)
        {
            var button = _skillButtons[i];
            button.Visible = i < Data.EquippedSkills.Count;
            if (!button.Visible)
                continue;
            var skill = SkillCatalog.Get(Data.EquippedSkills[i]);
            button.Label = skill.Short;
            button.Icon = CharacterArt.SkillIcon(skill.Id);
        }

        if (SkillsPanel.Visible)
            RefreshSkills();
    }

    private void RefreshQuest()
    {
        var quest = Data.CurrentQuest;
        _questPanel.Visible = Data.HasStarter && _questTrackerAllowed;
        if (quest == null)
        {
            _questTitle.Text = "Story complete (for now)";
            _questObjective.Text = "More chapters are on the way.";
            return;
        }
        _questTitle.Text = $"Ch {quest.Chapter}  •  {quest.Title}";
        _questObjective.Text = !Data.QuestAccepted ? $"Talk to {Quests.GiverName} at the Training Square."
            : Data.QuestReady ? $"Done! Return to {Quests.GiverName}."
            : quest.Count > 1 ? $"{quest.Objective}  ({Data.QuestProgress}/{quest.Count})"
            : quest.Objective;
    }

    private void RefreshSkills()
    {
        foreach (var child in _skillsList.GetChildren())
            child.QueueFree();
        int nextSlotLevel = GameConfig.SkillSlotLevels.FirstOrDefault(l => l > Data.Level);
        _skillsHeader.Text = Data.Lineage == "" ? "Choose a starter skill first."
            : $"{Lineages.Get(Data.Lineage).Name} lineage  •  {Data.ClassName} (Tier {Data.ClassTier})  •  " +
              $"Slots {Data.EquippedSkills.Count}/{Data.SkillSlots}" +
              (nextSlotLevel > 0 ? $" (next at Lv {nextSlotLevel})" : "") +
              "\nUsing a skill against enemies near your level raises its mastery (rank 20 max).";

        // Owned skills first, then the rest of your lineage's catalog.
        foreach (var skill in SkillCatalog.All.Where(s => Data.OwnedSkills.Contains(s.Id)))
            _skillsList.AddChild(OwnedSkillRow(skill));
        if (Data.Lineage != "")
        {
            foreach (var skill in SkillCatalog.LineageSkills(Data.Lineage).Where(s => !Data.OwnedSkills.Contains(s.Id)))
            {
                string status = !skill.Implemented ? "coming in a future update"
                    : skill.UnlockLevel > Data.Level ? $"unlocks at Lv {skill.UnlockLevel}"
                    : "drops from skill boxes";
                _skillsList.AddChild(UiKit.MakeLabel($"{skill.Code}  {skill.Name}: {skill.Desc}  ({status})", 15, new Color(0.55f, 0.55f, 0.55f)));
            }
        }
    }

    private Control OwnedSkillRow(SkillInfo skill)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var label = UiKit.MakeLabel(
            $"{skill.Name}  •  Mastery {Data.MasteryRank(skill.Id)}  •  {skill.Cost} energy, {skill.Cooldown * Data.SkillCooldownMult(skill.Id):0.0}s\n{skill.Desc}",
            16, skill.Color);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        bool equipped = Data.EquippedSkills.Contains(skill.Id);
        var button = new Button
        {
            Text = equipped ? "Equipped" : "Equip",
            CustomMinimumSize = new Vector2(120, 48),
            Disabled = !equipped && Data.EquippedSkills.Count >= Data.SkillSlots,
        };
        string id = skill.Id;
        button.Pressed += () => Data.ToggleEquip(id);
        row.AddChild(button);
        return row;
    }

    private void ShowNextToast()
    {
        if (!_toasts.TryDequeue(out var next))
        {
            _toastLabel.Text = "";
            return;
        }
        _toastLabel.Text = next.Text;
        _toastLabel.AddThemeColorOverride("font_color", next.Color);
        _toastTime = 2.2f;
    }
}
