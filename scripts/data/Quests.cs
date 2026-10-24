using System.Linq;

namespace RambleRumble;

public enum QuestGoal
{
    /// <summary>Reach a Training level (Count).</summary>
    TrainingLevel,
    /// <summary>Defeat Count monsters of species Target.</summary>
    Kill,
    /// <summary>Have Count Echoes in the ranch fence.</summary>
    FencePet,
    /// <summary>Clear the stage with id Target (e.g. "1-3").</summary>
    ClearStage,
    /// <summary>Have a battle companion chosen.</summary>
    BattlePet,
    /// <summary>Buy Count upgrades of any kind.</summary>
    BuyUpgrade,
}

/// <param name="Chapter">Story chapter (see <see cref="Quests.ChapterNames"/>).</param>
/// <param name="Intro">What Garrick says when offering it.</param>
/// <param name="Outro">What Garrick says when it is handed in.</param>
/// <param name="SkillBox">Hand-in also opens a skill box.</param>
/// <param name="RewardPet">Species of a Monster Echo given on hand-in ("" = none).</param>
public record QuestDef(string Id, int Chapter, string Title, string Intro, string Objective, QuestGoal Goal,
    string Target, int Count, int Gold, int Xp, string Outro, bool SkillBox = false, string RewardPet = "");

/// <summary>
/// Story quests from "Echoes of the Wild": Chapter 1 (A Rambler's Beginning) and
/// Chapter 2 (The First Companion), given by Garrick in Hearthwood Village.
/// </summary>
public static class Quests
{
    public const string GiverName = "Garrick";
    /// <summary>Chapters in the written story; later regions wait on chapters not yet in the game.</summary>
    public const int StoryChapters = 10;

    /// <summary>Index = chapter number.</summary>
    public static readonly string[] ChapterNames =
    {
        "Prologue",
        "Chapter 1: A Rambler's Beginning",
        "Chapter 2: The First Companion",
        "Chapter 3: The Forest That Whispers",
        "Chapter 4: The Price of Power",
        "Chapter 5: The Rival Ramblers",
        "Chapter 6: The Mountain That Breathes",
        "Chapter 7: The Truth Beneath Hearthwood",
        "Chapter 8: The Great Rumble",
        "Chapter 9: The Last Rambler",
        "Chapter 10: A World Worth Sharing",
    };

    /// <summary>Shown once after character creation.</summary>
    public const string PrologueTitle = "Prologue: The Night the Wilderness Awakened";
    public const string PrologueText =
        "Long before the kingdoms raised their walls, the wilderness belonged to creatures whose power flowed from a living " +
        "force called Rumble. The most gifted humans became Ramblers: wanderers who wielded strange skills and bonded with monsters.\n\n" +
        "Then, on a night when the sky turned crimson, peaceful creatures attacked villages and swarms crossed the old roads. " +
        "Six legendary Ramblers went beneath the earth to stop it. Only one returned. Generations later, in Hearthwood " +
        "Village, a young adventurer is about to reopen those roads.";

    public static readonly QuestDef[] All =
    {
        new("ch1_trial", 1, "The Adventurer Trial",
            "\"Rumble is not a gift you own. It is a force you learn to answer.\" The awakening stone answered you more " +
            "strongly than any novice's in decades. Now learn to control it: train here at the square.",
            "Reach Training Lv 2 at the Training Square", QuestGoal.TrainingLevel, "", 2, 50, 40,
            "Good. You're starting to hear it. Wait... that bell. That's the alarm."),
        new("ch1_defend", 1, "Red in the Grass",
            "A wounded guard says slimes are pouring in from the Whispering Plains, their bodies streaked with red light. " +
            "Go through the gate to the plains map and stop them before they reach Hearthwood!",
            "Clear Stage 1-3: Red in the Grass (through the village gate)", QuestGoal.ClearStage, "1-3", 1, 150, 250,
            "You held them off. But this red residue on the earth... It has started again."),
        new("ch2_echo", 2, "The First Companion",
            "One of the slimes you fought left a small crystal instead of vanishing. A Monster Echo: the surviving imprint " +
            "of a creature's Rumble. Calm a few more slimes in the plains and let's see if it wakes.",
            "Defeat 5 Slimes in the Whispering Plains", QuestGoal.Kill, "Slime", 5, 60, 120,
            "Look, it's moving! ...Ha! Not every victory ends with an enemy.", RewardPet: "Slime"),
        new("ch2_ranch", 2, "The Abandoned Ranch",
            "Take these keys: an old ranch by the village. Ramblers once housed Echo companions there. Settle your " +
            "slime in the fence; their surplus energy can be traded for gold.",
            "Have 1 Echo in your ranch fence", QuestGoal.FencePet, "", 1, 100, 80,
            "It settled in as if it had always lived there."),
        new("ch2_repair", 2, "Mending Fences",
            "The fence is broken and the yard's overgrown. Use your Echoes' gold at the Upgrade Station to fix the place up.",
            "Buy any upgrade at the Upgrade Station", QuestGoal.BuyUpgrade, "", 1, 100, 120,
            "Now that looks like a Rambler's ranch."),
        new("ch2_together", 2, "Walking Together",
            "An Echo can also walk beside you into the wild. Choose a battle companion in the Echoes menu. While it " +
            "fights with you it won't earn fence gold, so choose wisely.",
            "Choose a battle companion in the Echoes menu", QuestGoal.BattlePet, "", 1, 120, 150,
            "Did you see that red glow beyond the hills tonight? Travelers say wolves are gathering in the Verdant " +
            "Forest. The forest road is yours now. Home will be worth protecting.", SkillBox: true),
    };

    public static QuestDef? At(int index) => index >= 0 && index < All.Length ? All[index] : null;

    /// <summary>True if <paramref name="index"/> is the last quest of its chapter.</summary>
    public static bool EndsChapter(int index) =>
        At(index) is { } quest && (At(index + 1) is not { } next || next.Chapter != quest.Chapter);

    /// <summary>Last chapter with quests in the game.</summary>
    public static int LastChapter => All.Max(q => q.Chapter);
}
