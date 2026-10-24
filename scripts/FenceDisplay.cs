using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// Shows fenced pets wandering inside the Lobby fence, drawn at their size,
/// with occasional "+gold" pops so passive income is visible.
/// </summary>
public partial class FenceDisplay : Node2D
{
    private const float PetSpeed = 28f;
    private const float CoinPopInterval = 6f;

    public Rect2 Area { get; set; }

    private sealed class Walker
    {
        public Vector2 Pos;
        public Vector2 Target;
        public Pet Pet = null!;
    }

    private readonly Dictionary<int, Walker> _walkers = new();
    private int _seenVersion = -1;
    private float _coinTimer = CoinPopInterval;

    public override void _Process(double delta)
    {
        var data = PlayerData.Instance;
        if (_seenVersion != data.PetsVersion)
        {
            _seenVersion = data.PetsVersion;
            Sync();
        }
        var inner = Area.Grow(-24);
        foreach (var walker in _walkers.Values)
        {
            var toTarget = walker.Target - walker.Pos;
            if (toTarget.Length() < 4f)
                walker.Target = new Vector2((float)GD.RandRange(inner.Position.X, inner.End.X), (float)GD.RandRange(inner.Position.Y, inner.End.Y));
            else
                walker.Pos += toTarget.Normalized() * PetSpeed * (float)delta;
        }
        _coinTimer -= (float)delta;
        if (_coinTimer <= 0f && _walkers.Count > 0)
        {
            _coinTimer = CoinPopInterval;
            var walker = _walkers.Values.ElementAt(GD.RandRange(0, _walkers.Count - 1));
            float perPop = data.IncomePerMin() * CoinPopInterval / 60f;
            Fx.FloatingText(this, walker.Pos + new Vector2(0, -26), $"+{perPop:0.0}", new Color(1f, 0.85f, 0.3f), 1f);
        }
        QueueRedraw();
    }

    private void Sync()
    {
        var fenced = PlayerData.Instance.FencedPets();
        foreach (var pet in fenced)
        {
            if (_walkers.TryGetValue(pet.Id, out var walker))
            {
                walker.Pet = pet;
                continue;
            }
            var start = Area.GetCenter() + new Vector2((float)GD.RandRange(-60, 60), (float)GD.RandRange(-40, 40));
            _walkers[pet.Id] = new Walker { Pos = start, Target = start, Pet = pet };
        }
        var keep = fenced.Select(p => p.Id).ToHashSet();
        foreach (var id in _walkers.Keys.Where(id => !keep.Contains(id)).ToList())
            _walkers.Remove(id);
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        // Draw back-to-front so lower pets overlap higher ones.
        foreach (var walker in _walkers.Values.OrderBy(w => w.Pos.Y))
        {
            var pet = walker.Pet;
            var species = GameConfig.SpeciesOf(pet.Species);
            float radius = species.Radius * 0.8f * GameConfig.Size(pet.Size).Scale;
            var pos = walker.Pos;
            DrawCircle(pos + new Vector2(0, radius * 0.8f), radius * 0.9f, new Color(0, 0, 0, 0.18f));
            DrawCircle(pos, radius, species.Color);
            DrawArc(pos, radius, 0f, Mathf.Tau, 24, GameConfig.RarityColor(pet.Rarity), 2f);
            DrawCircle(pos + new Vector2(-radius * 0.3f, -radius * 0.2f), 2.5f, Colors.Black);
            DrawCircle(pos + new Vector2(radius * 0.3f, -radius * 0.2f), 2.5f, Colors.Black);
            DrawString(font, pos + new Vector2(-40, -radius - 6), new string('★', pet.Stars), HorizontalAlignment.Center, 80, 12, new Color(1f, 0.85f, 0.3f));
        }
    }
}
