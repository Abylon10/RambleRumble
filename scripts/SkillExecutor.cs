using System.Linq;
using Godot;

namespace RambleRumble;

/// <summary>
/// Casts any skill from its archetype and effect, so new skills are data in
/// <see cref="SkillCatalog"/> rather than new code. Each archetype here is
/// shared by one skill per lineage (e.g. Strike: Flame Fist, Ember Spark...).
/// </summary>
public static class SkillExecutor
{
    private const float StrikeReach = 84f;
    private const float AreaRadius = 120f;
    private const float BuffSeconds = 6f;
    private const float GuardSeconds = 4f;
    private const float StunSeconds = 1.2f;
    /// <summary>Radians per second that Curve projectiles turn.</summary>
    private const float CurveRate = 1.8f;

    /// <param name="damage">Final damage for damaging archetypes (already includes buffs and mastery).</param>
    public static void Cast(Player player, SkillInfo skill, float damage)
    {
        var parent = player.GetParent();
        var at = player.GlobalPosition;
        var facing = player.Facing;
        switch (skill.Archetype)
        {
            case SkillArchetype.Strike:
                foreach (var monster in player.MonstersInArc(StrikeReach))
                {
                    monster.TakeDamage(damage);
                    if (skill.Effect == SkillEffect.Burn)
                        monster.Burn(damage * 0.25f, 2f);
                }
                Fx.Spawn(parent, at + facing * 45f, FxKind.Circle, skill.Color, radius: 46f, duration: 0.25f);
                break;

            case SkillArchetype.Projectile:
                parent.AddChild(new Projectile
                {
                    Position = at + facing * 20f,
                    Direction = facing,
                    Damage = damage,
                    Tint = skill.Color,
                    TurnRate = skill.Effect == SkillEffect.Curve ? CurveRate : 0f,
                });
                break;

            case SkillArchetype.Area:
                foreach (var monster in player.AllMonsters().Where(m => m.GlobalPosition.DistanceTo(at) <= AreaRadius + m.Radius).ToList())
                {
                    monster.TakeDamage(damage);
                    if (skill.Effect == SkillEffect.Stun)
                        monster.Stun(StunSeconds);
                }
                Fx.Spawn(parent, at, FxKind.Ring, skill.Color, radius: AreaRadius, duration: 0.35f);
                break;

            case SkillArchetype.Dash:
                player.StartDash(damage, skill.Color);
                break;

            case SkillArchetype.Step:
                // A reposition: same burst of speed and invulnerability, no damage.
                player.StartDash(0f, skill.Color);
                break;

            case SkillArchetype.Surge:
                player.ApplySurge(1f + skill.Damage, BuffSeconds);
                Fx.Spawn(parent, at, FxKind.Ring, skill.Color, radius: 50f, duration: 0.4f);
                break;

            case SkillArchetype.Guard:
                player.ApplyGuard(skill.Damage, GuardSeconds);
                Fx.Spawn(parent, at, FxKind.Circle, skill.Color, radius: 36f, duration: 0.4f);
                break;
        }
    }
}
