using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// Shows fenced pets wandering inside the ranch fence, drawn at their size,
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
    private float _time;

    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest; // crisp pixel art

    public override void _Process(double delta)
    {
        var data = PlayerData.Instance;
        _time += (float)delta;
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

    private static readonly Vector2[] StarShape = BuildStar(5f, 2.2f);

    private static Vector2[] BuildStar(float outer, float inner)
    {
        var points = new Vector2[10];
        for (int i = 0; i < 10; i++)
            points[i] = Vector2.FromAngle(-Mathf.Pi / 2 + i * Mathf.Pi / 5) * (i % 2 == 0 ? outer : inner);
        return points;
    }

    private void DrawStar(Vector2 at)
    {
        DrawSetTransform(at, 0f, Vector2.One);
        DrawColoredPolygon(StarShape, new Color(1f, 0.85f, 0.3f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    public override void _Draw()
    {
        // Draw back-to-front so lower pets overlap higher ones.
        foreach (var walker in _walkers.Values.OrderBy(w => w.Pos.Y))
        {
            var pet = walker.Pet;
            var species = GameConfig.SpeciesOf(pet.Species);
            float radius = species.Radius * 0.8f * GameConfig.Size(pet.Size).Scale;
            var pos = walker.Pos;
            var feet = pos + new Vector2(0, radius * 0.9f);
            Shapes.Ellipse(this, feet, new Vector2(radius * 1.05f, radius * 0.4f), new Color(0, 0, 0, 0.22f));
            Shapes.Ring(this, feet, radius * 0.9f, new Color(GameConfig.RarityColor(pet.Rarity), 0.6f), thin: true);
            bool faceLeft = walker.Target.X < walker.Pos.X;
            MonsterArt.Draw(this, pet.Species, (int)(_time * 4f + pet.Id), feet, radius * 2.6f, faceLeft, Colors.White);
            radius *= 1.7f; // stars go above the sprite
            // Small drawn stars: text here would be re-shaped every frame as pets walk.
            for (int s = 0; s < pet.Stars; s++)
                DrawStar(pos + new Vector2((s - (pet.Stars - 1) / 2f) * 10f, -radius - 8));
        }
    }
}
