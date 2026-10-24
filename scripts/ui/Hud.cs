using System;
using System.Collections.Generic;
using Godot;

namespace RambleRumble.UI;

/// <summary>
/// In-game HUD built in code: stats, joystick, action buttons, toasts, and the
/// starter-skill, pets, skills and upgrades panels.
/// </summary>
public partial class Hud : CanvasLayer
{
    public event Action? TrainPressed;

    public Player Player { get; set; } = null!;
    public string AreaName { get; set; } = "Lobby";

    private Label _cpLabel = null!;
    private Label _goldLabel = null!;
    private Label _trainLabel = null!;
    private ProgressBar _trainBar = null!;
    private ProgressBar _hpBar = null!;
    private Label _areaLabel = null!;
    private TextureRect _portrait = null!;
    private Label _toastLabel = null!;
    private readonly Queue<(string Text, Color Color)> _toasts = new();
    private float _toastTime;
    private MoveJoystick _joystick = null!;
    private TouchButton _attackButton = null!;
    private readonly List<TouchButton> _skillButtons = new();
    private Button _trainButton = null!;
    private VBoxContainer _skillsList = null!;

    // Exposed so tests and screenshots can open panels directly.
    public PanelContainer StarterPanel { get; private set; } = null!;
    public PetsPanel PetsPanel { get; private set; } = null!;
    public UpgradesPanel UpgradesPanel { get; private set; } = null!;
    public PanelContainer SkillsPanel { get; private set; } = null!;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = new Theme { DefaultFontSize = 20 } };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        BuildStats(root);
        BuildTopButtons(root);
        BuildControls(root);
        BuildToast(root);
        PetsPanel = new PetsPanel();
        root.AddChild(PetsPanel);
        UpgradesPanel = new UpgradesPanel();
        root.AddChild(UpgradesPanel);
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
        _areaLabel.Text = AreaName;
        _attackButton.CooldownRatio = Player.CooldownRatio("basic", GameConfig.BasicAttackCooldown);
        for (int i = 0; i < _skillButtons.Count && i < Data.EquippedSkills.Count; i++)
        {
            var skill = GameConfig.Skill(Data.EquippedSkills[i]);
            _skillButtons[i].CooldownRatio = Player.CooldownRatio(skill.Id, skill.Cooldown);
        }

        if (_toastTime > 0f)
        {
            _toastTime -= (float)delta;
            if (_toastTime <= 0f)
                ShowNextToast();
        }
    }

    public void SetTrainingAvailable(bool available) => _trainButton.Visible = available;

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
        foreach (var each in new Control[] { PetsPanel, SkillsPanel, UpgradesPanel })
            each.Visible = each == panel;
        Refresh();
    }

    // --- Builders -------------------------------------------------------------

    private void BuildStats(Control root)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(new Color(0.4f, 0.4f, 0.4f)));
        UiKit.Place(panel, Vector2.Zero, new Vector2(16, 16), new Vector2(280, 0));
        root.AddChild(panel);
        var box = new VBoxContainer();
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
        _areaLabel = UiKit.MakeLabel("", 20, new Color(0.9f, 0.75f, 0.5f));
        _areaLabel.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(_areaLabel);
        _cpLabel = UiKit.MakeLabel("");
        box.AddChild(_cpLabel);
        _goldLabel = UiKit.MakeLabel("", 20, new Color(1f, 0.85f, 0.3f));
        box.AddChild(_goldLabel);
        _hpBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14), Modulate = new Color(1f, 0.45f, 0.45f) };
        box.AddChild(_hpBar);
        _trainLabel = UiKit.MakeLabel("", 16);
        box.AddChild(_trainLabel);
        _trainBar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
        box.AddChild(_trainBar);
    }

    private void BuildTopButtons(Control root)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        UiKit.Place(row, new Vector2(1, 0), new Vector2(-406, 16), new Vector2(390, 56));
        root.AddChild(row);
        foreach (var (text, panel) in new (string, Func<Control>)[]
                 { ("Pets", () => PetsPanel), ("Skills", () => SkillsPanel), ("Upgrades", () => UpgradesPanel) })
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(123, 56) };
            button.Pressed += () => Open(panel());
            row.AddChild(button);
        }
    }

    private void BuildControls(Control root)
    {
        _joystick = new MoveJoystick();
        UiKit.Place(_joystick, new Vector2(0, 1), new Vector2(48, -268), new Vector2(220, 220));
        root.AddChild(_joystick);

        _attackButton = new TouchButton { Label = "Attack", Tint = new Color(0.95f, 0.95f, 0.95f) };
        UiKit.Place(_attackButton, new Vector2(1, 1), new Vector2(-188, -188), new Vector2(140, 140));
        root.AddChild(_attackButton);

        // Skill buttons (up to 4 slots) sit in an arc around the attack button.
        foreach (var offset in new[] { new Vector2(-310, -140), new Vector2(-220, -310), new Vector2(-330, -260), new Vector2(-110, -300) })
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
    }

    private void BuildToast(Control root)
    {
        _toastLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _toastLabel.AddThemeFontSizeOverride("font_size", 26);
        _toastLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _toastLabel.AddThemeConstantOverride("outline_size", 8);
        UiKit.Place(_toastLabel, new Vector2(0.5f, 0), new Vector2(-400, 90), new Vector2(800, 40));
        root.AddChild(_toastLabel);
    }

    private PanelContainer BuildStarterPanel(Control root)
    {
        var panel = new PanelContainer();
        root.AddChild(panel);
        var (box, _) = UiKit.MakeModal(panel, "Create your adventurer", new Vector2(760, 560), closable: false);

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
        box.AddChild(UiKit.MakeLabel("Starter skill", 20, new Color(0.9f, 0.75f, 0.5f)));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        box.AddChild(grid);
        foreach (var skillId in GameConfig.StarterSkills)
        {
            var skill = GameConfig.Skill(skillId);
            var button = new Button
            {
                Text = $"{skill.Name}\n{skill.Desc}",
                Icon = CharacterArt.SkillIcon(skillId),
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(360, 110),
                IconAlignment = HorizontalAlignment.Left,
            };
            button.AddThemeColorOverride("font_color", skill.Color);
            button.Pressed += () =>
            {
                Data.ChooseStarter(skillId);
                panel.Visible = false;
                Toast($"You learned {skill.Name}!", skill.Color);
            };
            grid.AddChild(button);
        }
        return panel;
    }

    private PanelContainer BuildSkillsPanel(Control root)
    {
        var panel = new PanelContainer();
        root.AddChild(panel);
        var (box, _) = UiKit.MakeModal(panel, "Skills", new Vector2(560, 460));
        _skillsList = new VBoxContainer();
        _skillsList.AddThemeConstantOverride("separation", 8);
        box.AddChild(_skillsList);
        return panel;
    }

    // --- Refresh --------------------------------------------------------------

    private void Refresh()
    {
        _portrait.Texture = CharacterArt.Portrait(Data.Gender);
        _cpLabel.Text = $"CP  {Data.CombatPower()}";
        float income = Data.IncomePerMin();
        _goldLabel.Text = $"Gold  {Data.Gold}" + (income > 0f ? $"   (+{income:0.0}/min)" : "");
        int needed = GameConfig.TrainingXpNeeded(Data.TrainingLevel);
        _trainLabel.Text = $"Training Lv {Data.TrainingLevel}   ({Data.TrainingXp} / {needed})";
        _trainBar.MaxValue = needed;
        _trainBar.Value = Data.TrainingXp;

        for (int i = 0; i < _skillButtons.Count; i++)
        {
            var button = _skillButtons[i];
            button.Visible = i < Data.EquippedSkills.Count;
            if (!button.Visible)
                continue;
            var skill = GameConfig.Skill(Data.EquippedSkills[i]);
            button.Label = skill.Short;
            button.Tint = skill.Color;
            button.Icon = CharacterArt.SkillIcon(skill.Id);
        }

        if (SkillsPanel.Visible)
            RefreshSkills();
    }

    private void RefreshSkills()
    {
        foreach (var child in _skillsList.GetChildren())
            child.QueueFree();
        _skillsList.AddChild(UiKit.MakeLabel(
            $"Skill slots: {Data.EquippedSkills.Count} / {Data.SkillSlots}   (buy more in Upgrades)", 16));
        foreach (var skill in GameConfig.Skills)
        {
            var row = new HBoxContainer();
            bool owned = Data.OwnedSkills.Contains(skill.Id);
            var label = UiKit.MakeLabel(owned ? $"{skill.Name} — {skill.Desc}" : "???  (find in skill boxes)", 20,
                owned ? skill.Color : new Color(0.5f, 0.5f, 0.5f));
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            if (owned)
            {
                var button = new Button
                {
                    Text = Data.EquippedSkills.Contains(skill.Id) ? "Equipped" : "Equip",
                    CustomMinimumSize = new Vector2(120, 48),
                };
                string id = skill.Id;
                button.Pressed += () => Data.ToggleEquip(id);
                row.AddChild(button);
            }
            _skillsList.AddChild(row);
        }
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
