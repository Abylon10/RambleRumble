using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// The puzzle in a stage's side pocket. Pieces are things you walk onto
/// (plates, braziers, levers, crystals); solving it unlocks the puzzle chest.
/// The four kinds rotate across a chapter's stages.
/// </summary>
public partial class StagePuzzle : Node2D
{
    /// <summary>Braziers stay lit this long; all must burn at once.</summary>
    public const float BrazierBurn = 8f;
    private const float FlashTime = 0.6f;

    public event Action? Solved;
    /// <summary>A short hint for the HUD the first time the player comes near.</summary>
    public event Action<string>? Hint;

    public PuzzleKind Kind { get; init; }
    public IReadOnlyList<Vector2> PiecePositions { get; init; } = Array.Empty<Vector2>();
    public int Seed { get; init; }
    public bool IsSolved { get; private set; }

    /// <summary>State per piece: plate/crystal lit, brazier seconds left, or lever on.</summary>
    private float[] _state = Array.Empty<float>();
    /// <summary>Rune plate numbers, or the order the Echo crystals flash in.</summary>
    private int[] _order = Array.Empty<int>();
    private int _progress;
    private float _wrongFlash;
    private bool _hinted;
    // Echo crystals: playback of the sequence.
    private int _showing = -1;
    private float _showTimer;

    public int PieceCount => Kind == PuzzleKind.EchoCrystals ? 3 : 3;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        _state = new float[PiecePositions.Count];
        _order = Enumerable.Range(0, PieceCount).OrderBy(_ => rng.Randf()).ToArray();
        if (Kind == PuzzleKind.Levers)
        {
            // Any off/on mix but all-on is solvable with these flips; start somewhere unsolved.
            do
            {
                for (int k = 0; k < PieceCount; k++)
                    _state[k] = rng.Randf() < 0.5f ? 1f : 0f;
            } while (_state.Take(PieceCount).All(s => s > 0f));
        }

        for (int k = 0; k < PiecePositions.Count; k++)
        {
            int index = k;
            var area = new Area2D { CollisionLayer = 0, CollisionMask = 1, Position = PiecePositions[k] };
            area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 22f } });
            area.BodyEntered += body => { if (body is Player) Step(index); };
            AddChild(area);
        }
        var hintArea = new Area2D { CollisionLayer = 0, CollisionMask = 1, Position = PiecePositions[0] };
        hintArea.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 170f } });
        hintArea.BodyEntered += body => { if (body is Player && !_hinted && !IsSolved) { _hinted = true; Hint?.Invoke(HintText); } };
        AddChild(hintArea);
    }

    public string HintText => Kind switch
    {
        PuzzleKind.RunePlates => "Puzzle: step on the rune plates in order, 1 → 2 → 3.",
        PuzzleKind.Braziers => $"Puzzle: light all three braziers within {BrazierBurn:0} seconds.",
        PuzzleKind.Levers => "Puzzle: each lever flips itself and its neighbours. Turn them all on.",
        _ => "Puzzle: step on the glowing rune, watch the crystals, then step on them in the same order.",
    };

    /// <summary>The player stepped onto piece <paramref name="index"/>.</summary>
    public void Step(int index)
    {
        if (IsSolved)
            return;
        switch (Kind)
        {
            case PuzzleKind.RunePlates:
                if (_order[index] == _progress)
                {
                    _state[index] = 1f;
                    if (++_progress == PieceCount)
                        Solve();
                }
                else
                {
                    Fail();
                }
                break;
            case PuzzleKind.Braziers:
                _state[index] = BrazierBurn;
                if (_state.All(s => s > 0f))
                    Solve();
                break;
            case PuzzleKind.Levers:
                for (int k = index - 1; k <= index + 1; k++)
                    if (k >= 0 && k < PieceCount)
                        _state[k] = _state[k] > 0f ? 0f : 1f;
                if (_state.Take(PieceCount).All(s => s > 0f))
                    Solve();
                break;
            case PuzzleKind.EchoCrystals:
                if (index == PieceCount)
                {
                    // The start rune: replay the sequence.
                    Array.Clear(_state);
                    _progress = 0;
                    _showing = 0;
                    _showTimer = FlashTime;
                }
                else if (_showing < 0)
                {
                    if (_order[_progress] == index)
                    {
                        _state[index] = 1f;
                        if (++_progress == PieceCount)
                            Solve();
                    }
                    else
                    {
                        Fail();
                    }
                }
                break;
        }
        QueueRedraw();
    }

    /// <summary>For tests: the winning sequence of piece indexes.</summary>
    public int[] Solution() => Kind switch
    {
        PuzzleKind.RunePlates => Enumerable.Range(0, PieceCount).Select(n => Array.IndexOf(_order, n)).ToArray(),
        PuzzleKind.EchoCrystals => _order.ToArray(),
        _ => Array.Empty<int>(),
    };

    public float State(int index) => _state[index];

    private void Fail()
    {
        Array.Clear(_state);
        _progress = 0;
        _wrongFlash = 0.5f;
    }

    private void Solve()
    {
        IsSolved = true;
        for (int k = 0; k < PieceCount; k++)
            _state[k] = 1f;
        Solved?.Invoke();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool redraw = _wrongFlash > 0f;
        _wrongFlash = Mathf.Max(0f, _wrongFlash - dt);
        if (Kind == PuzzleKind.Braziers && !IsSolved)
        {
            for (int k = 0; k < PieceCount; k++)
            {
                if (_state[k] > 0f)
                {
                    _state[k] = Mathf.Max(0f, _state[k] - dt);
                    redraw = true;
                }
            }
        }
        if (_showing >= 0)
        {
            _showTimer -= dt;
            if (_showTimer <= 0f)
            {
                _showing++;
                _showTimer = FlashTime;
                if (_showing >= PieceCount)
                    _showing = -1;
            }
            redraw = true;
        }
        if (redraw)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        for (int k = 0; k < PiecePositions.Count; k++)
        {
            var at = PiecePositions[k];
            bool lit = k < _state.Length && _state[k] > 0f;
            switch (Kind)
            {
                case PuzzleKind.RunePlates:
                    Plate(at, lit ? new Color(0.5f, 0.9f, 1f) : new Color(0.55f, 0.55f, 0.6f));
                    string number = (_order[k] + 1).ToString();
                    DrawString(font, at + new Vector2(-10, 6), number, HorizontalAlignment.Center, 20, 16, lit ? Colors.White : new Color(0.15f, 0.15f, 0.2f));
                    break;
                case PuzzleKind.Braziers:
                    Brazier(at, lit, k < _state.Length ? _state[k] / BrazierBurn : 0f);
                    break;
                case PuzzleKind.Levers:
                    Lever(at, lit);
                    break;
                case PuzzleKind.EchoCrystals:
                    if (k == PieceCount)
                    {
                        Plate(at, new Color(0.8f, 0.6f, 1f));
                        Shapes.Ring(this, at, 12f, new Color(1f, 0.9f, 1f, 0.8f));
                    }
                    else
                    {
                        bool flashing = _showing >= 0 && _order[_showing] == k;
                        CrystalPiece(at, lit || flashing || IsSolved);
                    }
                    break;
            }
        }
        if (_wrongFlash > 0f)
            foreach (var at in PiecePositions)
                Shapes.Circle(this, at, 20f, new Color(1f, 0.2f, 0.2f, _wrongFlash));
    }

    private void Plate(Vector2 at, Color colour)
    {
        var d = new[] { at + new Vector2(0, -11), at + new Vector2(22, 0), at + new Vector2(0, 11), at + new Vector2(-22, 0) };
        DrawColoredPolygon(d, colour);
        DrawPolyline(new[] { d[0], d[1], d[2], d[3], d[0] }, colour.Darkened(0.4f), 2f);
    }

    private void Brazier(Vector2 at, bool lit, float burnLeft)
    {
        Shapes.Ellipse(this, at, new Vector2(14, 6), new Color(0, 0, 0, 0.3f));
        DrawColoredPolygon(new[] { at + new Vector2(-12, -24), at + new Vector2(12, -24), at + new Vector2(6, -6), at + new Vector2(-6, -6) }, new Color(0.45f, 0.42f, 0.40f));
        DrawRect(new Rect2(at + new Vector2(-3, -6), new Vector2(6, 6)), new Color(0.35f, 0.32f, 0.30f));
        if (lit)
        {
            Shapes.Circle(this, at + new Vector2(0, -30), 16, new Color(1f, 0.6f, 0.2f, 0.25f));
            DrawColoredPolygon(new[] { at + new Vector2(-9, -24), at + new Vector2(0, -46), at + new Vector2(9, -24) }, new Color(1f, 0.5f, 0.1f));
            DrawColoredPolygon(new[] { at + new Vector2(-4, -24), at + new Vector2(0, -36), at + new Vector2(4, -24) }, new Color(1f, 0.9f, 0.4f));
            if (!IsSolved)
                DrawArc(at + new Vector2(0, -30), 20, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * burnLeft, 20, new Color(1f, 0.85f, 0.3f), 2f);
        }
    }

    private void Lever(Vector2 at, bool on)
    {
        Plate(at, new Color(0.5f, 0.45f, 0.4f));
        var handleTip = at + (on ? new Vector2(10, -26) : new Vector2(-10, -26));
        DrawLine(at + new Vector2(0, -4), handleTip, new Color(0.35f, 0.25f, 0.15f), 4f);
        Shapes.Circle(this, handleTip, 5f, on ? new Color(0.4f, 0.95f, 0.45f) : new Color(0.9f, 0.3f, 0.3f));
    }

    private void CrystalPiece(Vector2 at, bool lit)
    {
        var colour = lit ? new Color(0.65f, 0.95f, 1f) : new Color(0.35f, 0.45f, 0.6f);
        if (lit)
            Shapes.Circle(this, at + new Vector2(0, -16), 18, new Color(colour, 0.3f));
        DrawColoredPolygon(new[] { at + new Vector2(-8, 0), at + new Vector2(0, -32), at + new Vector2(8, 0) }, colour);
        DrawColoredPolygon(new[] { at + new Vector2(0, -32), at + new Vector2(8, 0), at + new Vector2(2, -4) }, colour.Darkened(0.25f));
    }
}
