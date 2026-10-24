using Godot;
using RambleRumble.UI;

namespace RambleRumble;

/// <summary>Records a monster kill and shows what it dropped (gold pop-up, Echo and skill box toasts).</summary>
public static class KillFeedback
{
    public static void OnMonsterDied(Monster monster, Node fxParent, Hud hud)
    {
        var data = PlayerData.Instance;
        var drop = data.RecordKill(monster.SpeciesId);
        Fx.FloatingText(fxParent, monster.GlobalPosition, $"+{drop.Gold} gold", new Color(1f, 0.85f, 0.3f), 0.9f);
        if (drop.AddedPet is { } pet)
        {
            string note = drop.PityHit ? "  (pity!)" : "";
            hud.Toast($"A Monster Echo! {pet.Size} {pet.Species} ({pet.Rarity}){note}", GameConfig.RarityColor(pet.Rarity));
        }
        if (drop.SkillBox)
            ShowSkillBox(hud, drop.SkillLearned, drop.SkillIsNew);
        if (drop.Material != "")
            Fx.FloatingText(fxParent, monster.GlobalPosition + new Vector2(0, -22),
                $"+{drop.MaterialCount} {drop.Material}", GameConfig.Material(drop.Material).Color, 1.1f);
    }

    public static void ShowSkillBox(Hud hud, string skillId, bool isNew)
    {
        var skill = SkillCatalog.Get(skillId);
        hud.Toast(isNew ? $"Skill box: learned {skill.Name}!" : $"Skill box: {skill.Name} mastery +{GameConfig.MasteryFromDuplicateBox}",
            skill.Color);
    }
}
