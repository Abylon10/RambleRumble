using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Solves each puzzle kind and checks that wrong moves reset it, plus that
/// every stage's map keeps its road, chests and puzzle reachable.
/// Run: godot --headless --path . res://tests/PuzzleTest.tscn -- --test
/// </summary>
public partial class PuzzleTest : TestScene
{
    public override async void _Ready()
    {
        Data.ResetProgress();
        CheckRunePlates();
        await CheckBraziers();
        CheckLevers();
        await CheckEchoCrystals();
        CheckEveryStageMap();
        Finish();
    }

    private StagePuzzle Make(PuzzleKind kind, int seed = 1)
    {
        int pieces = kind == PuzzleKind.EchoCrystals ? 4 : 3;
        var puzzle = new StagePuzzle
        {
            Kind = kind,
            Seed = seed,
            PiecePositions = Enumerable.Range(0, pieces).Select(k => new Vector2(k * 80, 0)).ToList(),
        };
        AddChild(puzzle);
        return puzzle;
    }

    private void CheckRunePlates()
    {
        var puzzle = Make(PuzzleKind.RunePlates);
        var solution = puzzle.Solution();
        puzzle.Step(solution[1]); // wrong first plate
        Check(!puzzle.IsSolved && puzzle.State(solution[1]) == 0f, "rune plates: a wrong plate resets");
        foreach (int piece in solution)
            puzzle.Step(piece);
        Check(puzzle.IsSolved, "rune plates: stepping 1 → 2 → 3 solves");
    }

    private async System.Threading.Tasks.Task CheckBraziers()
    {
        var puzzle = Make(PuzzleKind.Braziers);
        puzzle.Step(0);
        puzzle.Step(1);
        Check(!puzzle.IsSolved && puzzle.State(0) > 0f, "braziers: two lit isn't enough");
        puzzle.Step(2);
        Check(puzzle.IsSolved, "braziers: all three lit together solves");

        // A brazier burns out if you're too slow.
        var slow = Make(PuzzleKind.Braziers, seed: 2);
        slow.Step(0);
        for (int i = 0; i < 3; i++)
            slow._Process(StagePuzzle.BrazierBurn / 2f);
        Check(slow.State(0) == 0f, "braziers: a lit brazier burns out after 8 seconds");
        await ProcessFrame();
    }

    private void CheckLevers()
    {
        // Try every set of lever presses on fresh copies; one must solve it.
        bool solvedAny = false;
        for (int mask = 1; mask < 8 && !solvedAny; mask++)
        {
            var puzzle = Make(PuzzleKind.Levers, seed: 5);
            Check(mask > 1 || !puzzle.IsSolved, "levers: start unsolved");
            for (int k = 0; k < 3; k++)
                if ((mask & (1 << k)) != 0)
                    puzzle.Step(k);
            solvedAny = puzzle.IsSolved;
            puzzle.QueueFree();
        }
        Check(solvedAny, "levers: some combination of flips turns them all on");
    }

    private async System.Threading.Tasks.Task CheckEchoCrystals()
    {
        var puzzle = Make(PuzzleKind.EchoCrystals);
        puzzle.Step(3); // the start rune plays the sequence
        puzzle.Step(puzzle.Solution()[0]);
        Check(!puzzle.IsSolved && puzzle.State(puzzle.Solution()[0]) == 0f, "echo crystals: stepping during the show does nothing");
        await Wait(2.2);
        var solution = puzzle.Solution();
        puzzle.Step(solution[1]);
        Check(!puzzle.IsSolved, "echo crystals: wrong order resets");
        foreach (int piece in solution)
            puzzle.Step(piece);
        Check(puzzle.IsSolved, "echo crystals: repeating the sequence solves");
    }

    private void CheckEveryStageMap()
    {
        int ok = 0, total = 0;
        foreach (var stage in StageCatalog.Chapters.SelectMany(c => c.Stages))
        {
            total++;
            var map = StageMapBuilder.Build(stage);
            bool roadOpen = map.Road.All(t => !map.IsBlockedTile(t.X, t.Y));
            bool spots = map.IsOpen(map.TreasureChest.X, map.TreasureChest.Y) && map.IsOpen(map.PuzzleChest.X, map.PuzzleChest.Y)
                         && map.PuzzleTiles.All(t => map.IsOpen(t.X, t.Y));
            if (roadOpen && spots && map.Road.Count > 20)
                ok++;
            else
                GD.Print($"  info  stage {stage.Id}: road open {roadOpen}, chest/puzzle spots open {spots}");
        }
        Check(ok == total, $"every stage map keeps its road, chests and puzzle reachable ({ok}/{total})");
        Check(StageCatalog.Chapters.All(c => c.Stages.Length == 10), "each chapter map has 10 stages");
    }
}
