using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble.UI;

/// <summary>
/// Pet collection and Pet Lab: a card grid on the left and the selected pet's
/// details on the right (fence, feed treats, star-up merge, release).
/// </summary>
public partial class PetsPanel : PanelContainer
{
    private Label _summary = null!;
    private GridContainer _grid = null!;
    private VBoxContainer _detail = null!;
    private Label _emptyHint = null!;
    private Label _nameLabel = null!;
    private Label _infoLabel = null!;
    private ProgressBar _xpBar = null!;
    private Button _fenceButton = null!;
    private Button _feedButton = null!;
    private Button _feedManyButton = null!;
    private Label _mergeLabel = null!;
    private Button _mergeButton = null!;
    private Button _releaseButton = null!;

    /// <summary>Selected pet id (-1 = none). Public so tests and screenshots can pick one.</summary>
    public int SelectedId { get; set; } = -1;
    private int _gridVersion = -1;
    private bool _releaseArmed;

    private static PlayerData Data => PlayerData.Instance;

    public override void _Ready()
    {
        var (box, _) = UiKit.MakeModal(this, "Pets", new Vector2(1120, 620));
        _summary = UiKit.MakeLabel("", 16, new Color(0.8f, 0.8f, 0.8f));
        box.AddChild(_summary);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        box.AddChild(columns);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        columns.AddChild(scroll);
        _grid = new GridContainer { Columns = 3 };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_grid);

        _detail = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _detail.AddThemeConstantOverride("separation", 8);
        columns.AddChild(_detail);
        _emptyHint = AddToDetail(UiKit.MakeLabel("Select a pet.", 18, new Color(0.7f, 0.7f, 0.7f)));
        _nameLabel = AddToDetail(UiKit.MakeLabel("", 24));
        _infoLabel = AddToDetail(UiKit.MakeLabel("", 16, new Color(0.85f, 0.85f, 0.85f)));
        _xpBar = AddToDetail(new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) });

        _fenceButton = MakeAction(OnFence);
        var feedRow = AddToDetail(new HBoxContainer());
        feedRow.AddThemeConstantOverride("separation", 8);
        _feedButton = MakeAction(() => Data.FeedTreats(SelectedId, 1), feedRow);
        _feedManyButton = MakeAction(() => Data.FeedTreats(SelectedId, 10), feedRow);
        _mergeLabel = AddToDetail(UiKit.MakeLabel("", 15, new Color(0.85f, 0.8f, 0.6f)));
        _mergeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mergeButton = MakeAction(() => Data.Merge(SelectedId));
        _releaseButton = MakeAction(OnRelease);

        Data.Changed += Refresh;
        VisibilityChanged += Refresh;
    }

    public override void _ExitTree() => Data.Changed -= Refresh;

    private T AddToDetail<T>(T control) where T : Control
    {
        _detail.AddChild(control);
        return control;
    }

    private Button MakeAction(System.Action onPressed, Control? parent = null)
    {
        var button = new Button { CustomMinimumSize = new Vector2(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        button.Pressed += onPressed;
        (parent ?? _detail).AddChild(button);
        return button;
    }

    public void Refresh()
    {
        if (!Visible)
            return;
        var pity = GameConfig.Rarities.Where(r => r.Pity > 0)
            .Select(r => $"{r.Name} {Data.Pity.GetValueOrDefault(r.Name)}/{r.Pity}");
        var shards = GameConfig.Rarities.Where(r => Data.StarShards.GetValueOrDefault(r.Name) > 0)
            .Select(r => $"{r.Name} {Data.StarShards[r.Name]}").ToList();
        _summary.Text = $"{Data.Pets.Count} pets  •  Fence {Data.FencedPets().Count}/{Data.FenceSlots} " +
            $"(+{Data.IncomePerMin():0.0} gold/min)  •  Star Shards: {(shards.Count > 0 ? string.Join(", ", shards) : "none")}" +
            $"  •  Pity: {string.Join(", ", pity)}";

        if (Data.FindPet(SelectedId) == null)
        {
            SelectedId = -1;
            _releaseArmed = false;
        }
        if (_gridVersion != Data.PetsVersion)
        {
            _gridVersion = Data.PetsVersion;
            RebuildGrid();
        }
        RefreshDetail();
    }

    private void RebuildGrid()
    {
        foreach (var child in _grid.GetChildren())
            child.QueueFree();
        var sorted = Data.Pets
            .OrderByDescending(p => GameConfig.RarityIndex(p.Rarity))
            .ThenByDescending(p => p.Stars)
            .ThenByDescending(Data.PetPower);
        foreach (var pet in sorted)
        {
            var color = GameConfig.RarityColor(pet.Rarity);
            var card = new Button
            {
                CustomMinimumSize = new Vector2(220, 92),
                Alignment = HorizontalAlignment.Left,
                Text = $"{pet.Size} {pet.Species}  {new string('★', pet.Stars)}\nLv {pet.Level}" +
                    (pet.Role == PetRole.Fence ? "   • In fence" : ""),
            };
            card.AddThemeFontSizeOverride("font_size", 16);
            card.AddThemeColorOverride("font_color", color.Lightened(0.3f));
            bool selected = pet.Id == SelectedId;
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
                card.AddThemeStyleboxOverride(state, UiKit.PanelStyle(selected ? Colors.White : color, selected ? 4 : 2));
            int petId = pet.Id;
            card.Pressed += () =>
            {
                SelectedId = petId;
                _releaseArmed = false;
                _gridVersion = -1;
                Refresh();
            };
            _grid.AddChild(card);
        }
        if (Data.Pets.Count == 0)
            _grid.AddChild(UiKit.MakeLabel("No pets yet. Defeat monsters in the Wilderness!", 18));
    }

    private void RefreshDetail()
    {
        var pet = Data.FindPet(SelectedId);
        _emptyHint.Visible = pet == null;
        foreach (var child in _detail.GetChildren().OfType<Control>())
        {
            if (child != _emptyHint)
                child.Visible = pet != null;
        }
        if (pet == null)
            return;

        var color = GameConfig.RarityColor(pet.Rarity);
        int maxLevel = Data.PetMaxLevel(pet);
        _nameLabel.Text = $"{pet.Size} {pet.Species}  {new string('★', pet.Stars)}";
        _nameLabel.AddThemeColorOverride("font_color", color.Lightened(0.2f));
        _infoLabel.Text = $"{pet.Rarity}  •  Level {pet.Level} / {maxLevel}\n" +
            $"Power {(int)Data.PetPower(pet)}  •  Income {Data.PetIncomePerMin(pet):0.00} gold/min\n" +
            $"Role: {(pet.Role == PetRole.Fence ? "Fence (earning gold)" : "Resting")}";
        _xpBar.MaxValue = GameConfig.PetXpNeeded(pet.Level);
        _xpBar.Value = pet.Level < maxLevel ? pet.Xp : _xpBar.MaxValue;

        bool fenced = pet.Role == PetRole.Fence;
        bool fenceFull = Data.FencedPets().Count >= Data.FenceSlots;
        _fenceButton.Text = fenced ? "Remove from fence" : fenceFull ? "Fence is full" : "Put in fence";
        _fenceButton.Disabled = !fenced && fenceFull;

        bool atCap = pet.Level >= maxLevel;
        _feedButton.Text = atCap ? "Max level" : $"Feed treat ({GameConfig.TreatCost}g)";
        _feedManyButton.Text = $"Feed x10 ({GameConfig.TreatCost * 10}g)";
        _feedButton.Disabled = _feedManyButton.Disabled = atCap || Data.Gold < GameConfig.TreatCost;

        var plan = Data.PlanMerge(SelectedId);
        if (plan.NextStars > GameConfig.MaxStars)
        {
            _mergeLabel.Text = "Max stars reached.";
            _mergeButton.Text = "Star-up";
            _mergeButton.Disabled = true;
        }
        else
        {
            string header = $"Star-up to {plan.NextStars}★ (max Lv {GameConfig.Star(plan.NextStars).MaxLevel}, size {plan.ResultSize})";
            if (plan.Ok)
            {
                string uses = $"{plan.Fodder.Count} duplicate {Plural(plan.Fodder.Count, "copy", "copies")}";
                if (plan.Shards > 0)
                    uses += $" + {plan.Shards} {pet.Rarity} Star {Plural(plan.Shards, "Shard", "Shards")}";
                _mergeLabel.Text = $"{header}\nUses {uses}.";
            }
            else if (plan.CopiesNeeded > 0)
            {
                _mergeLabel.Text = $"{header}\nNeeds {plan.CopiesNeeded} {pet.Species} {Plural(plan.CopiesNeeded, "copy", "copies")} " +
                    $"(up to {plan.ShardLimit} can be {pet.Rarity} Star Shards).\n" +
                    $"You have {plan.CopiesOwned} {Plural(plan.CopiesOwned, "copy", "copies")}, " +
                    $"{plan.ShardsOwned} {Plural(plan.ShardsOwned, "shard", "shards")}.\n{plan.Reason}";
            }
            else
            {
                _mergeLabel.Text = $"{header}\n{plan.Reason}";
            }
            _mergeButton.Text = $"Star-up  ({plan.Cost}g)";
            _mergeButton.Disabled = !plan.Ok;
        }

        _releaseButton.Text = _releaseArmed
            ? "Tap again to release"
            : $"Release (+{GameConfig.Rarity(pet.Rarity).ReleaseGold}g, +1 {pet.Rarity} Shard)";
    }

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;

    private void OnFence()
    {
        var pet = Data.FindPet(SelectedId);
        if (pet != null)
            Data.SetFenced(SelectedId, pet.Role != PetRole.Fence);
    }

    private void OnRelease()
    {
        if (!_releaseArmed)
        {
            _releaseArmed = true;
            RefreshDetail();
            return;
        }
        _releaseArmed = false;
        Data.ReleasePet(SelectedId);
    }
}
