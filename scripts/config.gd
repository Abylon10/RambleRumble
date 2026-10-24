extends Node
## Game balance data from the v1.1 proposal. Every number here is a starting
## assumption to be tuned in playtests. Kept as constants so other scripts can
## read them through `preload` without needing the autoload instance.

const RARITY_ORDER = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]

## pet_chance / box_chance are per kill. pity = guaranteed pet on this many
## kills of that rarity without a pet drop (0 = no pity).
const RARITY = {
	"Common": {"pet_chance": 0.25, "box_chance": 0.10, "pity": 0, "color": Color(0.70, 0.70, 0.70)},
	"Uncommon": {"pet_chance": 0.18, "box_chance": 0.12, "pity": 0, "color": Color(0.40, 0.85, 0.40)},
	"Rare": {"pet_chance": 0.10, "box_chance": 0.15, "pity": 25, "color": Color(0.35, 0.60, 1.00)},
	"Epic": {"pet_chance": 0.05, "box_chance": 0.20, "pity": 50, "color": Color(0.75, 0.40, 1.00)},
	"Legendary": {"pet_chance": 0.02, "box_chance": 0.25, "pity": 120, "color": Color(1.00, 0.65, 0.10)},
	"Mythic": {"pet_chance": 0.005, "box_chance": 0.30, "pity": 400, "color": Color(1.00, 0.30, 0.40)},
}

## Size is rolled once per pet. mult applies to HP and fence income only.
const SIZES = [
	{"name": "Tiny", "chance": 0.20, "scale": 0.8, "mult": 0.90},
	{"name": "Normal", "chance": 0.60, "scale": 1.0, "mult": 1.00},
	{"name": "Big", "chance": 0.15, "scale": 1.25, "mult": 1.10},
	{"name": "Giant", "chance": 0.045, "scale": 1.5, "mult": 1.20},
	{"name": "Titan", "chance": 0.005, "scale": 2.0, "mult": 1.35},
]

## Star rank -> level cap and stat/income multiplier (merging arrives in Phase 2).
const STARS = {
	1: {"max_level": 10, "mult": 1.00},
	2: {"max_level": 20, "mult": 1.25},
	3: {"max_level": 30, "mult": 1.60},
	4: {"max_level": 40, "mult": 2.00},
	5: {"max_level": 50, "mult": 2.50},
}
const PET_LEVEL_BONUS = 0.04

const SPECIES = {
	"Slime": {"rarity": "Common", "hp": 45.0, "damage": 5.0, "speed": 70.0, "radius": 16.0,
		"pet_power": 10.0, "gold_min": 2, "gold_max": 5, "color": Color(0.45, 0.85, 0.45)},
	"Wolf": {"rarity": "Uncommon", "hp": 80.0, "damage": 8.0, "speed": 115.0, "radius": 18.0,
		"pet_power": 18.0, "gold_min": 5, "gold_max": 9, "color": Color(0.60, 0.58, 0.62)},
	"Goblin": {"rarity": "Rare", "hp": 130.0, "damage": 12.0, "speed": 90.0, "radius": 17.0,
		"pet_power": 30.0, "gold_min": 10, "gold_max": 18, "color": Color(0.55, 0.65, 0.25)},
}

## Spawn weights for the first Wilderness zone.
const WILD_SPAWNS = [["Slime", 60], ["Wolf", 30], ["Goblin", 10]]
const MAX_WILD_MONSTERS = 10

## damage is a multiplier on the player's attack power.
const SKILLS = {
	"flame_fist": {"name": "Flame Fist", "short": "Flame", "desc": "Burning melee strike",
		"cooldown": 1.5, "damage": 1.6, "cp": 40, "color": Color(1.00, 0.45, 0.15)},
	"wind_slash": {"name": "Wind Slash", "short": "Wind", "desc": "Fast ranged slash",
		"cooldown": 1.2, "damage": 1.2, "cp": 40, "color": Color(0.45, 0.90, 1.00)},
	"stone_breaker": {"name": "Stone Breaker", "short": "Stone", "desc": "Heavy stun attack",
		"cooldown": 4.0, "damage": 2.0, "cp": 40, "color": Color(0.70, 0.52, 0.32)},
	"thunder_dash": {"name": "Thunder Dash", "short": "Dash", "desc": "Mobility and burst damage",
		"cooldown": 3.0, "damage": 1.5, "cp": 40, "color": Color(1.00, 0.90, 0.25)},
}
const STARTER_SKILLS = ["flame_fist", "wind_slash", "stone_breaker", "thunder_dash"]
const BASE_SKILL_SLOTS = 2
const SKILL_BOX_DUPLICATE_GOLD = 50

const BASIC_ATTACK = {"cooldown": 0.45, "damage": 1.0, "range": 64.0}

## Training: XP per tap on the Train button and per second spent in the zone.
const TRAIN_TAP_XP = 2
const TRAIN_IDLE_XP_PER_SEC = 1.0


static func training_xp_needed(level: int) -> int:
	return 10 + 5 * level


static func size_data(size_name: String) -> Dictionary:
	for s in SIZES:
		if s["name"] == size_name:
			return s
	return SIZES[1]


static func rarity_color(rarity: String) -> Color:
	return RARITY[rarity]["color"]
