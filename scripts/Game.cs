using System;
using Godot;

namespace RambleRumble;

/// <summary>
/// Root of the game: shows one page at a time (Hearthwood Village, the world
/// map, a chapter map, or a single stage battlefield) and frees the previous one, so only
/// what's on screen is ever loaded.
/// </summary>
public partial class Game : Node
{
    public static Game Instance { get; private set; } = null!;

    /// <summary>The page on screen: a <see cref="World"/>, <see cref="WorldMapPage"/>, <see cref="ChapterMapPage"/> or <see cref="StagePage"/>.</summary>
    public Node? Page { get; private set; }

    /// <summary>Raised after a new page enters the tree (tests wait on this).</summary>
    public event Action<Node>? PageShown;

    public override void _EnterTree() => Instance = this;

    public override void _Ready() => ShowVillage();

    /// <param name="fromGate">Arrive at the village gate (returning from the maps) instead of the spawn point.</param>
    public void ShowVillage(bool fromGate = false) => Swap(new World { ArriveAtGate = fromGate });

    public void ShowWorldMap() => Swap(new WorldMapPage());

    public void ShowChapterMap(int chapter) => Swap(new ChapterMapPage { Chapter = chapter });

    public void StartStage(StageDef stage) => Swap(new StagePage { Stage = stage });

    // Swapping during physics callbacks or button presses is unsafe, so it waits a moment.
    private void Swap(Node next) => Callable.From(() =>
    {
        if (Page != null)
        {
            RemoveChild(Page);
            Page.QueueFree();
        }
        Page = next;
        AddChild(next);
        PageShown?.Invoke(next);
    }).CallDeferred();
}
