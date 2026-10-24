class_name DropRoller
extends RefCounted
## Rolls monster-kill rewards: gold, a pet (with size), and a skill box.
## Pure logic with an injected RNG so it can be simulated and, later, run
## on the backend as the authoritative roll.

const Config = preload("res://scripts/config.gd")


## `pity` maps rarity -> kills since that rarity's last pet drop; it is updated in place.
static func roll_kill(species_id: String, pity: Dictionary, rng: RandomNumberGenerator) -> Dictionary:
	var species: Dictionary = Config.SPECIES[species_id]
	var rarity: String = species["rarity"]
	var rarity_data: Dictionary = Config.RARITY[rarity]

	var result := {
		"gold": rng.randi_range(species["gold_min"], species["gold_max"]),
		"pet": null,
		"pity_hit": false,
		"skill_box": rng.randf() < float(rarity_data["box_chance"]),
	}

	var pity_limit: int = rarity_data["pity"]
	var count: int = int(pity.get(rarity, 0)) + 1
	var got_pet := rng.randf() < float(rarity_data["pet_chance"])
	if not got_pet and pity_limit > 0 and count >= pity_limit:
		got_pet = true
		result["pity_hit"] = true

	if got_pet:
		result["pet"] = {"species": species_id, "rarity": rarity, "size": roll_size(rng)}
		pity[rarity] = 0
	else:
		pity[rarity] = count
	return result


static func roll_size(rng: RandomNumberGenerator) -> String:
	var roll := rng.randf()
	var acc := 0.0
	for s in Config.SIZES:
		acc += float(s["chance"])
		if roll < acc:
			return s["name"]
	return "Normal"
