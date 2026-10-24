# Ramble Rumble

A 2D top-down mobile game about monster hunting, skills, pet collection and base raids. It's built with **Godot 4.7 (.NET)** in **C#**.
The design follows the *Ramble Rumble Game Proposal v1.1*.

## Status: Phase 2 (economy and pets) + character art + world map

| Feature | State |
|---|---|
| Character creation: male/female adventurer + starter skill (Flame Fist, Wind Slash, Stone Breaker, Thunder Dash) | ✅ |
| Animated character sprites (idle, walk, run, attack, hurt, victory) and skill icons | ✅ |
| Lobby laid out like the world map: central plaza, spawn, portal, training ground, player plot with pet fence, Upgrade Station (walk in to open) | ✅ |
| Lobby signposts for future features: Raid Board, Cosmetics & Titles, Leaderboard, Event Area | ✅ |
| Training (tap or stand in the zone) raises training level, attack, HP and CP | ✅ |
| Wilderness with 7 zones by difficulty: Starter Plains, Forest, River, Rocky Hills, Crystal Caves, Volcanic Ridge, Ancient Ruins (Stone Golem boss) | ✅ |
| 9 monster species, per-zone spawning, campfire rest spots that heal | ✅ |
| Kill drops: gold, pets with rolled **size**, and skill boxes | ✅ |
| Pity for Rare and higher pets | ✅ |
| Best-loadout CP (unequipping can't lower it) | ✅ |
| Touch controls: multitouch joystick, attack button, up to 4 skill buttons, auto-aim | ✅ |
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

Monsters, pets, the maps and effects are still placeholder shapes drawn in code. `art/source/world_map.webp` is the layout concept the maps follow.

## Run it

1. Install **Godot 4.7 .NET** (the ".NET" download, not the standard one) and the **.NET 8 SDK**.
2. In Godot, choose **Import** and pick `project.godot`.
3. Press **F5**. Godot builds the C# code the first time.

**Desktop controls:** WASD or the arrow keys to move, **J** or **Space** to attack, **K**, **L**, **U** and **I** for skill slots 1–4. You can also drag with the mouse on the joystick and buttons, because mouse input is turned into touch.

## Tests

Build first, then run each test scene headless (the exit code is the number of failures):

```bash
dotnet build
godot --headless --path . res://tests/DropSim.tscn -- --test
godot --headless --path . res://tests/GameplaySmoke.tscn -- --test
godot --headless --path . res://tests/EconomyTest.tscn -- --test
```

- `DropSim` rolls 200k kills per species and checks the pet and skill-box rates, the size odds and the pity limits.
- `GameplaySmoke` plays the real scene: it picks a starter, trains, uses the portal, kills monsters with all four skill slots, and checks save/load. It also checks the world: the zone layout, zone tracking, campfire healing, that every zone spawns monsters within its cap, that defeat returns you to the Lobby, and that the Upgrade Station opens its panel.
- `EconomyTest` covers fence slots, income, the offline cap, treats and level caps, merging, Star Shards, Pet Lab limits, releasing pets, upgrades, and saving/loading.

Passing `-- --test` makes the game use `user://test_save.json`, so tests never touch your real save.

## Code layout

```
RambleRumble.csproj        C# project (Godot.NET.Sdk)
scenes/main.tscn           entry scene (World)
scripts/GameConfig.cs      balance data as C# records: rarities, sizes, stars, species, skills, upgrades
scripts/PlayerData.cs      progress + save/load (autoload; use PlayerData.Instance); every change goes through here
scripts/Pet.cs             pet data class
scripts/DropRoller.cs      pure kill-reward rolls with injected RNG (will move to the backend later)
scripts/World.cs           puts the world together: maps, walls, stations, portals, campfires, spawning
scripts/world/Zones.cs     Wilderness zone layout, spawn tables and landmarks
scripts/world/LobbyMap.cs  Lobby layout and drawing
scripts/world/WildernessMap.cs  zone ground and decorations
scripts/world/MapArt.cs    shared placeholder drawing helpers
scripts/Player.cs          movement, basic attack, the four skills, auto-aim, sprite animation
scripts/CharacterArt.cs    loads character strips, portraits and skill icons
scripts/Monster.cs         wander / chase / leash AI, burn and stun
scripts/Projectile.cs      Wind Slash
scripts/Fx.cs              placeholder effects and floating text
scripts/FenceDisplay.cs    fenced pets wandering in the Lobby fence
scripts/ui/                HUD, Pets/Pet Lab and Upgrades panels, joystick, touch buttons
tests/                     headless test scenes
art/                       sprites, icons, portraits (see below)
tools/cut_sprites.py       cuts the art out of the concept sheet
```

## Art

`art/source/character_sheet.webp` is the concept sheet. `tools/cut_sprites.py` cuts it into:

- `art/characters/<gender>_<anim>.png`: one strip of equal-size frames per animation.
- `art/characters/manifest.json`: the frame count and frame size of each strip.
- `art/icons/` and `art/portraits/`: skill icons and character portraits.

The sprites face right and only come in side view; the game flips them to face left.
To use proper sprite sheets later, replace the strips and update `manifest.json`. No code changes are needed.

To re-run the cutter (needs Python with Pillow, NumPy and SciPy):

```bash
python tools/cut_sprites.py
```

## Exporting to Android

In Godot, open **Editor → Manage Export Templates** and install the .NET templates. Then set up the Android SDK (**Editor Settings → Export → Android**), add an Android preset under **Project → Export**, and export.
The project uses the Compatibility renderer and landscape orientation, which suits low-end phones.

## Notes for online play (Phase 5)

Drops, gold, CP, merges and offline income are worked out on the device for now. Offline income trusts the device clock, which players could change to earn more. Before raids can steal gold from other players, the server has to be the one that decides these values. The plan is to move `DropRoller` and the gold settlement into Supabase Edge Functions. `PlayerData` then becomes a cache of the server's copy.
