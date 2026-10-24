using System.Linq;
using Godot;

namespace RambleRumble.Tests;

/// <summary>
/// Measures frame time in a few spots of the real world (needs a window, not --headless).
/// Run: godot --path . res://tests/PerfProbe.tscn -- --test
/// </summary>
public partial class PerfProbe : Node
{
    public override async void _Ready()
    {
        PlayerData.Instance.ResetProgress();
        PlayerData.Instance.ChooseStarter("wind_slash");
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var world = GD.Load<PackedScene>("res://scenes/main.tscn").Instantiate<World>();
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, world);
        await Wait(1.0);
        world.Hud.StarterPanel.Visible = false;

        await Measure(world, "Lobby plaza", null, LobbyMap.Plaza);
        await Measure(world, "Starter Plains", Zones.ArrivalPoint, null);
        await Measure(world, "Forest", null, Zones.ById("B").Cells[0].GetCenter());
        await Measure(world, "Rocky Hills", null, Zones.ById("D").Cells[0].GetCenter());
        await Measure(world, "Crystal Caves", null, Zones.ById("E").Cells[0].GetCenter());
        if (OS.GetCmdlineUserArgs().Contains("experiments"))
        {
            world.GetChildren().OfType<WildernessMap>().First().Visible = false;
            await Measure(world, "-map drawing", null, Zones.ById("D").Cells[0].GetCenter());
            world.GetChildren().OfType<WildernessMap>().First().Visible = true;
            foreach (var monster in world.Monsters.GetChildren().OfType<Monster>())
                monster.ProcessMode = ProcessModeEnum.Disabled;
            await Measure(world, "-monster AI", null, Zones.ById("D").Cells[0].GetCenter());
            world.Monsters.Visible = false;
            await Measure(world, "-monster draw", null, Zones.ById("D").Cells[0].GetCenter());
        }
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task Measure(World world, string label, Vector2? teleport, Vector2? place)
    {
        if (teleport is { } t)
            world.Teleport(t, toWilderness: true);
        if (place is { } p)
        {
            world.Player.GlobalPosition = p;
            world.Player.InSafeZone = true; // keep the probe alive
        }
        await Wait(1.5);
        int frames = 0;
        double start = Time.GetTicksMsec();
        double worstMs = 0, processMs = 0, physicsMs = 0;
        double last = start;
        while (Time.GetTicksMsec() - start < 3000)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            double now = Time.GetTicksMsec();
            worstMs = System.Math.Max(worstMs, now - last);
            last = now;
            frames++;
            processMs += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000;
            physicsMs += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000;
        }
        double seconds = (Time.GetTicksMsec() - start) / 1000.0;
        int monsters = world.Monsters.GetChildren().OfType<Monster>().Count();
        GD.Print($"{label,-15} fps {frames / seconds,6:0.0}  worst {worstMs,5:0}ms  process {processMs / frames,5:0.00}ms  " +
            $"physics {physicsMs / frames,5:0.00}ms  draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),5}  " +
            $"primitives {Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),7}  nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}  monsters {monsters}");
    }

    private SignalAwaiter Wait(double seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
