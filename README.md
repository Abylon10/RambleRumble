# Ramble Rumble

A 2D top-down mobile game about monster hunting, skills, pet collection and base raids. It's built with **Godot 4.7** (GDScript).
The design follows the *Ramble Rumble Game Proposal v1.1*.

## Status: Phase 2, economy and pets

| Feature | State |
|---|---|
| Starter skill choice (Flame Fist, Wind Slash, Stone Breaker, Thunder Dash) | ✅ |
| Lobby (safe zone) with training ground and pet fence, plus a Raid Board placeholder | ✅ |
| Training (tap or stand in the zone) raises training level, attack, HP and CP | ✅ |
| One Wilderness zone with Slime (Common), Wolf (Uncommon) and Goblin (Rare) | ✅ |
| Kill drops: gold, pets with rolled **size**, and skill boxes | ✅ |
| Pity for Rare and higher pets | ✅ |
| Best-loadout CP (unequipping can't lower it) | ✅ |
| Touch controls: multitouch joystick, attack button, skill buttons, auto-aim | ✅ |
| Local save (`user://save.json`) | ✅ |
| Pet fence: fenced pets earn gold per minute and wander in the Lobby fence | ✅ |
| Offline fence income, capped at 8 hours | ✅ |
| Pet levels via treats (gold) and fence XP, capped by star rank | ✅ |
| Star-up merging with duplicates; result keeps the largest size | ✅ |
| Star Shards from releasing pets, covering up to half of a merge | ✅ |
| Upgrades: Fence Slots, Skill Slots, Training Ground, Pet Lab (unlocks 4★/5★) | ✅ |
| Battle pet, more drops, swarm events | Phase 3 |
| Raids vs. bot base | Phase 4 |
| Online async raids (Supabase backend), loss caps, shields | Phase 5 |
| Cosmetics and temporary boosts (IAP), polish | Phase 6 |

All art is placeholder shapes drawn in code.

## Run it

1. Install Godot 4.7 or newer (standard build, not .NET).
2. In Godot, choose **Import** and pick `project.godot`.
3. Press **F5**.

**Desktop controls:** WASD or the arrow keys to move, **J** or **Space** to attack, **K** and **L** for the skills. You can also drag with the mouse on the joystick and buttons, because mouse input is turned into touch.

## Tests

```bash
godot --headless --path . -s res://tests/drop_sim.gd
godot --headless --path . res://tests/gameplay_smoke.tscn -- --test
godot --headless --path . res://tests/economy_test.tscn -- --test
```

- `drop_sim` rolls 200k kills per species and checks the pet and skill-box rates, the size odds and the pity limits.
- `gameplay_smoke` plays the real scene: it picks a starter, trains, uses the portal, kills monsters with every skill, then checks save/load and that defeat returns you to the Lobby.
- `economy_test` covers fence slots, income, the offline cap, treats and level caps, merging, Star Shards, Pet Lab limits, releasing pets, upgrades, and saving/loading the new data.

Passing `-- --test` makes the game use `user://test_save.json`, so tests never touch your real save.

## Code layout

```
scenes/main.tscn          entry scene (World)
scripts/config.gd         balance data (autoload GameConfig): rarities, sizes, stars, species, skills
scripts/player_data.gd    progress + save/load (autoload PlayerData); every change goes through here
scripts/drop_roller.gd    pure kill-reward rolls with injected RNG (will move to the backend later)
scripts/world.gd          builds the Lobby and Wilderness, portals, spawning, training
scripts/player.gd         movement, basic attack, the four skills, auto-aim
scripts/monster.gd        wander / chase / leash AI, burn and stun
scripts/projectile.gd     Wind Slash
scripts/fx.gd             placeholder effects and floating text
scripts/fence_display.gd  fenced pets wandering in the Lobby fence
scripts/ui/               HUD, Pets/Pet Lab and Upgrades panels, joystick, buttons
tests/                    headless checks
```

## Exporting to Android

In Godot, open **Editor → Manage Export Templates** and install the templates. Then set up the Android SDK (**Editor Settings → Export → Android**), add an Android preset under **Project → Export**, and export.
The project uses the Compatibility renderer and landscape orientation, which suits low-end phones.

## Notes for online play (Phase 5)

Drops, gold, CP, merges and offline income are worked out on the device for now. Offline income trusts the device clock, which players could change to earn more. Before raids can steal gold from other players, the server has to be the one that decides these values. The plan is to move `DropRoller` and the gold settlement into Supabase Edge Functions. `PlayerData` then becomes a cache of the server's copy.
