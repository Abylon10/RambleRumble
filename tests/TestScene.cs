using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Base for headless test scenes: prints ok/FAIL lines and quits with the
/// failure count as the exit code. Run with "-- --test" so the real save is untouched.
/// </summary>
public abstract partial class TestScene : Node
{
    private int _failures;

    protected static PlayerData Data => PlayerData.Instance;

    protected void Check(bool ok, string what)
    {
        GD.Print((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok)
            _failures++;
    }

    protected void Finish()
    {
        Data.ResetProgress();
        GD.Print(_failures == 0 ? "RESULT: PASS" : $"RESULT: {_failures} FAILURES");
        GetTree().Quit(_failures);
    }

    protected SignalAwaiter PhysicsFrame() => ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

    /// <summary>One rendered frame. Use when waiting on _Process logic: several physics steps can run per frame.</summary>
    protected SignalAwaiter ProcessFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    protected SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Loads the real game (main scene) and waits for the village page.</summary>
    protected async System.Threading.Tasks.Task<World> LoadGame()
    {
        var game = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Game>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, game);
        return await WaitForPage<World>();
    }

    /// <summary>Runs a page change and waits until the new page of type T is ready.</summary>
    protected async System.Threading.Tasks.Task<T> Go<T>(System.Action change) where T : Node
    {
        change();
        return await WaitForPage<T>();
    }

    private async System.Threading.Tasks.Task<T> WaitForPage<T>() where T : Node
    {
        for (int i = 0; i < 120; i++)
        {
            await ProcessFrame();
            if (Game.Instance?.Page is T page && page.IsInsideTree() && page.IsNodeReady())
            {
                await ProcessFrame();
                return page;
            }
        }
        throw new System.InvalidOperationException($"Page {typeof(T).Name} never appeared");
    }
}
