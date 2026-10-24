# Ramble Rumble: Echoes of the Wild

An **offline, single-player**, story-driven 2D top-down pixel action RPG for mobile. It's built with **Godot 4.7 (.NET)** in **C#**.
You train skills, explore a wilderness whose monsters are being corrupted, collect Monster Echo companions, and restore an abandoned ranch.
There's no online play, no multiplayer and no raids. Saves stay on the device.

## Status: first story milestone (Chapters 1–2)

| Feature | State |
|---|---|
| **Story:** Prologue intro, then Chapter 1 *A Rambler's Beginning* and Chapter 2 *The First Companion*, given by Garrick in Hearthwood Village | ✅ |
| Quest tracker, dialogue box, quest giver with `!`/`?` markers, scripted swarm of red-streaked slimes | ✅ |
| **Chapter maps (Alien Creeps style):** a road of 10 stage circles per chapter; tap one to play it on its own page | ✅ |
| **Chapter 1 Whispering Plains** (Slime King boss) and **Chapter 2 Verdant Forest** (Crimson Fang boss), 20 stages | ✅ |
| **Isometric stages (Final Fantasy Tactics look):** long strips with raised tiles, cliffs, rivers, bridges, waterfalls; enemy groups along the road; defeat all and reach the flag | ✅ |
| **Every stage has a treasure chest and a puzzle chest.** Puzzles rotate: Rune Plates, Braziers, Levers, Echo Crystals | ✅ |
| Stars (1–3 by HP left), first-clear rewards, half rewards on replays, stages unlock in order | ✅ |
| **Materials** from monsters and chests; **Market** in Hearthwood to buy potions, tonics and skill boxes and sell materials | ✅ |
| Character creation: male/female + one of four awakening stones (starter skill), which sets your class lineage | ✅ |
| **Character level 1–200** from kill and quest XP, with 10 ranks (Novice to Ascended) | ✅ |
| **Skill slots by level:** 1 at the start, then at 25, 60, 100, 150 and 200 | ✅ |
| **10 lineages × 5 class tiers (50 classes)** as data; Tier I class assigned by your starter | ✅ |
| **Data-driven skills:** 4 starters + the Ember lineage SK001–SK020; the first five Ember skills are playable | ✅ |
| **Rumble energy** (skill cost, regen, Energy Well upgrade) | ✅ |
| **Skill mastery** ranks 1–20 (+3% damage per rank, −5% cooldown every 5 ranks); duplicate skill boxes give mastery | ✅ |
| **Battle companion:** an Echo follows you and fights; it earns no fence gold while battling | ✅ |
| Animated character sprites, skill icons, portraits | ✅ |
| Hearthwood Village hub: training square, ranch with Echo fence, Upgrade Station, Market, quest board, gate to the chapter maps | ✅ |

| Drops: gold, Echoes with rolled size, skill boxes, pity for rare Echoes | ✅ |
| Echo fence income (8 h offline cap), Echo levels, star-up merging, Star Shards | ✅ |
| Upgrades: Fence Slots, Energy Well, Training Ground, Pet Lab | ✅ |
| Chapter 3 onward (Crimson Fang, Kael and the Rambler Guild, the Worldheart...) | next |
| Dodge, more lineages' skills, more class tiers | later |

Monsters, Echoes, maps and effects are still placeholder shapes drawn in code.

## Run it

1. Install **Godot 4.7 .NET** (the ".NET" download, not the standard one) and the **.NET 8 SDK**.
2. In Godot, choose **Import** and pick `project.godot`.
3. Press **F5**. Godot builds the C# code the first time.

**Desktop controls:**
- Move: WASD or the arrow keys
- Attack: **J** or **Space**
- Skill slots 1–6: **K L U I O P**
- Talk to Garrick: **E**
- In stages: Health Potion **Q**, Energy Tonic **R**

You can also drag with the mouse on the joystick and buttons, because mouse input is turned into touch.

## Tests

Build first, then run each test scene headless (the exit code is the number of failures):

```bash
dotnet build
godot --headless --path . res://tests/DropSim.tscn -- --test
godot --headless --path . res://tests/EconomyTest.tscn -- --test
godot --headless --path . res://tests/GameplaySmoke.tscn -- --test
godot --headless --path . res://tests/ProgressionTest.tscn -- --test
godot --headless --path . res://tests/PuzzleTest.tscn -- --test
```

- `DropSim` checks drop rates, size odds and pity limits over 200k kills.
- `EconomyTest` covers the fence, income, the offline cap, treats, merging, Star Shards, the Pet Lab, releasing Echoes and upgrades.
- `GameplaySmoke` plays the real pages: village (training, Upgrade Station, Market), gate → chapter map → stage 1-1 (combat, treasure chest, puzzle chest, goal flag, stars, unlocks), a defeat with a potion, and Chapter 2's boss stage.
- `PuzzleTest` solves every puzzle kind, checks wrong moves reset, and checks every stage map keeps its road, chests and puzzle reachable.
- `ProgressionTest` covers levels and slots, lineages and classes, skill boxes and mastery, energy, each Ember skill, the battle companion, the **whole Chapter 1–2 quest line** and save migration.

`tests/PerfProbe.tscn` (needs a window) measures FPS in several spots. `tests/Screenshot.tscn` captures screens for checking.
Passing `-- --test` makes the game use `user://test_save.json`, so tests never touch your real save.

## Code layout

```
scripts/GameConfig.cs        balance: rarities, sizes, stars, species, upgrades, XP/level, ranks, energy, mastery
scripts/data/SkillCatalog.cs skills as data (archetype + effect), starters and the Ember lineage
scripts/data/Lineages.cs     the 10 lineages and 50 class names
scripts/data/Quests.cs       story quests, chapter names, prologue text
scripts/SkillExecutor.cs     casts any skill from its archetype (Strike, Projectile, Area, Dash, Surge, Guard, Step)
scripts/PlayerData.cs        all progress + save/load (autoload; PlayerData.Instance)
scripts/Game.cs              root: shows one page at a time (village, chapter map, stage)
scripts/World.cs             Hearthwood Village hub: stations, Garrick, gate
scripts/stages/              stage data, isometric map builder and drawing, stage page, puzzles, chests, chapter map
scripts/Baker.cs             bakes static drawings (stage ground) into one texture for speed
scripts/Player.cs            movement, attacks, energy, buffs, sprite animation
scripts/BattlePetNode.cs     the Echo that fights beside you
scripts/world/               village map, quest giver
scripts/ui/                  HUD, dialogue, Echoes/Skills/Upgrades/Market panels, touch controls
tests/                       headless test scenes, perf probe, screenshot tool
art/ and tools/              character art and the sprite cutter
```

Adding a skill means adding one line to `SkillCatalog` with an existing archetype. A new archetype means one new case in `SkillExecutor`.

## Art

`art/source/character_sheet.webp` is the concept sheet. `tools/cut_sprites.py` cuts it into:
- `art/characters/<gender>_<anim>.png` strips, plus `manifest.json`
- `art/icons/` (skill icons)
- `art/portraits/`

Skills without their own icon use their lineage starter's icon.
To use proper sprite sheets later, replace the strips and update `manifest.json`. No code changes are needed.

## Exporting to Android

In Godot, open **Editor → Manage Export Templates** and install the .NET templates. Then set up the Android SDK (**Editor Settings → Export → Android**), add an Android preset under **Project → Export**, and export.
The project uses the Compatibility renderer and landscape orientation, which suits low-end phones.
