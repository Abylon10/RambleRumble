extends Node
## The player's progress, saved locally. When online raids arrive (Phase 5)
## the backend becomes the source of truth, so every change goes through here.

signal changed
signal notify(text: String, color: Color)

## Tests pass "-- --test" so they never touch the real save.
var save_path := "user://test_save.json" if OS.get_cmdline_user_args().has("--test") else "user://save.json"
const SAVE_VERSION := 2
const AUTOSAVE_INTERVAL := 15.0

var gold: int = 0
var training_level: int = 1
var training_xp: int = 0
var owned_skills: Array[String] = []
var equipped_skills: Array[String] = []
var pets: Array[Dictionary] = []
var pity: Dictionary = {}
var kills: int = 0
## upgrade id -> level bought
var upgrades: Dictionary = {}
## rarity -> Star Shards owned
var star_shards: Dictionary = {}
## Bumped whenever the pet list or a pet changes, so UI can skip rebuilding.
var pets_version: int = 0
## Gold earned while the app was closed, reported once by the HUD.
var offline_gold: int = 0
var rng := RandomNumberGenerator.new()

var _next_pet_id: int = 1
var _train_frac := 0.0
var _income_frac := 0.0
var _fence_xp_frac := 0.0
var _autosave_left := AUTOSAVE_INTERVAL


func _ready() -> void:
	rng.randomize()
	load_game()


func _process(delta: float) -> void:
	tick(delta)
	_autosave_left -= delta
	if _autosave_left <= 0.0:
		_autosave_left = AUTOSAVE_INTERVAL
		save_game()


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_PAUSED or what == NOTIFICATION_WM_CLOSE_REQUEST:
		save_game()


# --- Skills -------------------------------------------------------------------

func has_starter() -> bool:
	return not owned_skills.is_empty()


func choose_starter(skill_id: String) -> void:
	if has_starter() or not GameConfig.STARTER_SKILLS.has(skill_id):
		return
	owned_skills.append(skill_id)
	equipped_skills.append(skill_id)
	_commit()


func skill_slots() -> int:
	return GameConfig.BASE_SKILL_SLOTS + upgrade_level("skill_slots")


func toggle_equip(skill_id: String) -> void:
	if equipped_skills.has(skill_id):
		if equipped_skills.size() > 1:
			equipped_skills.erase(skill_id)
	elif owned_skills.has(skill_id) and equipped_skills.size() < skill_slots():
		equipped_skills.append(skill_id)
	_commit()


## Returns the learned skill id, or "" if the box was a duplicate converted to gold.
func _open_skill_box() -> String:
	var unowned: Array = GameConfig.SKILLS.keys().filter(func(id): return not owned_skills.has(id))
	if unowned.is_empty():
		gold += GameConfig.SKILL_BOX_DUPLICATE_GOLD
		return ""
	var skill_id: String = unowned[rng.randi_range(0, unowned.size() - 1)]
	owned_skills.append(skill_id)
	if equipped_skills.size() < skill_slots():
		equipped_skills.append(skill_id)
	return skill_id


# --- Training and stats -------------------------------------------------------

## Adds training XP after the Training Ground bonus; fractions carry over.
func train(base_xp: float) -> void:
	_train_frac += base_xp * training_multiplier()
	var whole := int(_train_frac)
	if whole > 0:
		_train_frac -= whole
		add_training_xp(whole)


func training_multiplier() -> float:
	return 1.0 + 0.25 * upgrade_level("training_ground")


func add_training_xp(amount: int) -> void:
	training_xp += amount
	var leveled := false
	while training_xp >= GameConfig.training_xp_needed(training_level):
		training_xp -= GameConfig.training_xp_needed(training_level)
		training_level += 1
		leveled = true
	if leveled:
		notify.emit("Training level %d!" % training_level, Color(0.5, 0.9, 1.0))
	# Disk writes only on level-up; idle training ticks every second.
	_commit(leveled)


func attack_power() -> float:
	return 10.0 * (1.0 + 0.12 * (training_level - 1))


func max_hp() -> float:
	return 100.0 + 15.0 * (training_level - 1)


func pet_power(pet: Dictionary) -> float:
	var species: Dictionary = GameConfig.SPECIES[pet["species"]]
	var star: Dictionary = GameConfig.STARS[int(pet["stars"])]
	var size_mult: float = GameConfig.size_data(pet["size"])["mult"]
	# Size boosts HP but not damage, so it counts half toward power.
	return float(species["pet_power"]) * _level_mult(pet) * float(star["mult"]) * (1.0 + (size_mult - 1.0) * 0.5)


## Offense CP from the best loadout the player owns, so unequipping can't lower it.
func combat_power() -> int:
	var cp := 100.0 + 25.0 * (training_level - 1)
	var skill_cps: Array = owned_skills.map(func(id): return GameConfig.SKILLS[id]["cp"])
	skill_cps.sort()
	skill_cps.reverse()
	for i in mini(skill_slots(), skill_cps.size()):
		cp += float(skill_cps[i])
	var best_pet := 0.0
	for pet in pets:
		best_pet = maxf(best_pet, pet_power(pet))
	cp += best_pet * 2.0
	return int(round(cp))


# --- Upgrades -----------------------------------------------------------------

func upgrade_level(upgrade_id: String) -> int:
	return int(upgrades.get(upgrade_id, 0))


func upgrade_maxed(upgrade_id: String) -> bool:
	return upgrade_level(upgrade_id) >= int(GameConfig.UPGRADES[upgrade_id]["max"])


func next_upgrade_cost(upgrade_id: String) -> int:
	return GameConfig.upgrade_cost(upgrade_id, upgrade_level(upgrade_id))


func buy_upgrade(upgrade_id: String) -> bool:
	if upgrade_maxed(upgrade_id):
		return false
	var cost := next_upgrade_cost(upgrade_id)
	if gold < cost:
		return false
	gold -= cost
	upgrades[upgrade_id] = upgrade_level(upgrade_id) + 1
	notify.emit("%s upgraded to level %d!" % [GameConfig.UPGRADES[upgrade_id]["name"], upgrades[upgrade_id]], Color(1, 0.85, 0.3))
	_commit()
	return true


func fence_slots() -> int:
	return GameConfig.BASE_FENCE_SLOTS + upgrade_level("fence_slots")


func max_stars() -> int:
	return mini(GameConfig.MAX_STARS, GameConfig.BASE_MAX_STARS + upgrade_level("pet_lab"))


func merge_discount() -> float:
	return 0.1 * upgrade_level("pet_lab")


# --- Pets: fence, levels, merging, release ------------------------------------

func find_pet(pet_id: int) -> Dictionary:
	for pet in pets:
		if int(pet["id"]) == pet_id:
			return pet
	return {}


func fenced_pets() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for pet in pets:
		if pet["role"] == "fence":
			result.append(pet)
	return result


func pet_income_per_min(pet: Dictionary) -> float:
	var base: float = GameConfig.RARITY[pet["rarity"]]["income"]
	var star_mult: float = GameConfig.STARS[int(pet["stars"])]["mult"]
	var size_mult: float = GameConfig.size_data(pet["size"])["mult"]
	return base * _level_mult(pet) * star_mult * size_mult


func income_per_min() -> float:
	var total := 0.0
	for pet in fenced_pets():
		total += pet_income_per_min(pet)
	return total


func pet_max_level(pet: Dictionary) -> int:
	return GameConfig.STARS[int(pet["stars"])]["max_level"]


## Returns false if the fence is full. Each pet has one role at a time.
func set_fenced(pet_id: int, fenced: bool) -> bool:
	var pet := find_pet(pet_id)
	if pet.is_empty():
		return false
	if fenced and pet["role"] != "fence" and fenced_pets().size() >= fence_slots():
		return false
	pet["role"] = "fence" if fenced else "none"
	_pets_changed()
	return true


## Feeds up to `count` treats, stopping at the level cap or when gold runs out.
func feed_treats(pet_id: int, count: int) -> int:
	var pet := find_pet(pet_id)
	var fed := 0
	while fed < count and not pet.is_empty() and int(pet["level"]) < pet_max_level(pet) and gold >= int(GameConfig.TREAT["cost"]):
		gold -= int(GameConfig.TREAT["cost"])
		_add_pet_xp(pet, int(GameConfig.TREAT["xp"]))
		fed += 1
	if fed > 0:
		_pets_changed()
	return fed


## Describes the next star-up for a pet without changing anything.
## Fodder is the weakest duplicates of the same species; Star Shards of the
## pet's rarity fill in for up to half of the required copies.
func merge_plan(pet_id: int) -> Dictionary:
	var plan := {"ok": false, "reason": "", "next_stars": 0, "fodder": [], "shards": 0, "cost": 0, "result_size": "",
		"copies_needed": 0, "copies_owned": 0, "shard_limit": 0, "shards_owned": 0}
	var pet := find_pet(pet_id)
	if pet.is_empty():
		plan["reason"] = "Pet not found"
		return plan
	var next := int(pet["stars"]) + 1
	plan["next_stars"] = next
	if next > GameConfig.MAX_STARS:
		plan["reason"] = "Already at max stars"
		return plan
	if next > max_stars():
		plan["reason"] = "Upgrade the Pet Lab to unlock %d★" % next
		return plan

	var need: int = GameConfig.STARS[next]["copies"]
	var copies: Array[Dictionary] = []
	for other in pets:
		if other["species"] == pet["species"] and int(other["id"]) != pet_id:
			copies.append(other)
	copies.sort_custom(func(a, b): return pet_power(a) < pet_power(b))
	var used_copies := mini(need, copies.size())
	var shards_needed := need - used_copies
	var shard_limit := int(floor(need * GameConfig.SHARD_REPLACE_FRACTION))
	var shards_owned := int(star_shards.get(pet["rarity"], 0))
	plan["copies_needed"] = need
	plan["copies_owned"] = copies.size()
	plan["shard_limit"] = shard_limit
	plan["shards_owned"] = shards_owned
	plan["fodder"] = copies.slice(0, used_copies).map(func(p): return int(p["id"]))
	plan["shards"] = shards_needed
	plan["cost"] = int(round(float(GameConfig.STARS[next]["cost"]) * (1.0 - merge_discount())))

	# The result keeps the largest size among everything merged.
	var best: Dictionary = GameConfig.size_data(pet["size"])
	for i in used_copies:
		var size: Dictionary = GameConfig.size_data(copies[i]["size"])
		if float(size["mult"]) > float(best["mult"]):
			best = size
	plan["result_size"] = best["name"]

	if shards_needed > shard_limit or shards_needed > shards_owned:
		var missing := shards_needed - mini(shard_limit, shards_owned)
		plan["reason"] = "Need %d more %s %s" % [missing, pet["species"], "copy" if missing == 1 else "copies"]
	elif gold < int(plan["cost"]):
		plan["reason"] = "Need %d gold" % plan["cost"]
	else:
		plan["ok"] = true
	return plan


func merge(pet_id: int) -> bool:
	var plan := merge_plan(pet_id)
	if not plan["ok"]:
		return false
	var pet := find_pet(pet_id)
	gold -= int(plan["cost"])
	var rarity: String = pet["rarity"]
	star_shards[rarity] = int(star_shards.get(rarity, 0)) - int(plan["shards"])
	var fodder: Array = plan["fodder"]
	pets.assign(pets.filter(func(p): return not fodder.has(int(p["id"]))))
	pet["stars"] = plan["next_stars"]
	pet["size"] = plan["result_size"]
	notify.emit("%s %s reached %d★!" % [pet["size"], pet["species"], pet["stars"]], Color(1, 0.85, 0.3))
	_pets_changed()
	return true


func release_pet(pet_id: int) -> bool:
	var pet := find_pet(pet_id)
	if pet.is_empty():
		return false
	var rarity: String = pet["rarity"]
	gold += int(GameConfig.RARITY[rarity]["release_gold"])
	star_shards[rarity] = int(star_shards.get(rarity, 0)) + 1
	pets.erase(pet)
	_pets_changed()
	return true


func _level_mult(pet: Dictionary) -> float:
	return 1.0 + GameConfig.PET_LEVEL_BONUS * (int(pet["level"]) - 1)


## Levels stop at the star cap; XP beyond the cap is lost.
func _add_pet_xp(pet: Dictionary, amount: int) -> void:
	var cap := pet_max_level(pet)
	pet["xp"] = int(pet["xp"]) + amount
	while int(pet["level"]) < cap and int(pet["xp"]) >= GameConfig.pet_xp_needed(int(pet["level"])):
		pet["xp"] = int(pet["xp"]) - GameConfig.pet_xp_needed(int(pet["level"]))
		pet["level"] = int(pet["level"]) + 1
	if int(pet["level"]) >= cap:
		pet["xp"] = 0


# --- Income -------------------------------------------------------------------

## Advances fence income and fence XP by `seconds`. Used every frame and for
## time spent offline.
func tick(seconds: float) -> void:
	var fenced := fenced_pets()
	if fenced.is_empty():
		return
	_income_frac += income_per_min() / 60.0 * seconds
	var earned := int(_income_frac)
	_fence_xp_frac += GameConfig.FENCE_XP_PER_MIN / 60.0 * seconds
	var xp := int(_fence_xp_frac)
	if earned > 0:
		_income_frac -= earned
		gold += earned
	if xp > 0:
		_fence_xp_frac -= xp
		for pet in fenced:
			_add_pet_xp(pet, xp)
		pets_version += 1
	if earned > 0 or xp > 0:
		changed.emit()


## Applies income for time away (capped) and returns the gold earned.
func apply_offline(seconds: float) -> int:
	var capped := clampf(seconds, 0.0, GameConfig.OFFLINE_CAP_HOURS * 3600.0)
	var before := gold
	tick(capped)
	return gold - before


# --- Kills and drops ----------------------------------------------------------

func record_kill(species_id: String) -> Dictionary:
	kills += 1
	var drop := DropRoller.roll_kill(species_id, pity, rng)
	gold += int(drop["gold"])
	if drop["pet"] != null:
		var pet: Dictionary = drop["pet"]
		pet["id"] = _next_pet_id
		pet["level"] = 1
		pet["xp"] = 0
		pet["stars"] = 1
		# New pets go straight into a free fence slot so income starts at once.
		pet["role"] = "fence" if fenced_pets().size() < fence_slots() else "none"
		_next_pet_id += 1
		pets.append(pet)
		pets_version += 1
	drop["skill_learned"] = _open_skill_box() if drop["skill_box"] else ""
	_commit()
	return drop


# --- Persistence --------------------------------------------------------------

func _pets_changed() -> void:
	pets_version += 1
	_commit()


func _commit(save := true) -> void:
	changed.emit()
	if save:
		save_game()


func save_game() -> void:
	var data := {
		"version": SAVE_VERSION,
		"saved_at": Time.get_unix_time_from_system(),
		"gold": gold,
		"training_level": training_level,
		"training_xp": training_xp,
		"owned_skills": owned_skills,
		"equipped_skills": equipped_skills,
		"pets": pets,
		"pity": pity,
		"kills": kills,
		"upgrades": upgrades,
		"star_shards": star_shards,
		"next_pet_id": _next_pet_id,
	}
	var file := FileAccess.open(save_path, FileAccess.WRITE)
	if file == null:
		push_warning("Could not save: %s" % FileAccess.get_open_error())
		return
	file.store_string(JSON.stringify(data))


func load_game() -> void:
	if not FileAccess.file_exists(save_path):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(save_path))
	if typeof(parsed) != TYPE_DICTIONARY:
		push_warning("Save file unreadable; starting fresh.")
		return
	var data: Dictionary = parsed
	# JSON numbers load as floats, so cast everything that should be an int.
	gold = int(data.get("gold", 0))
	training_level = int(data.get("training_level", 1))
	training_xp = int(data.get("training_xp", 0))
	owned_skills.assign(data.get("owned_skills", []))
	equipped_skills.assign(data.get("equipped_skills", []))
	kills = int(data.get("kills", 0))
	_next_pet_id = int(data.get("next_pet_id", 1))
	pity = _int_dict(data.get("pity", {}))
	upgrades = _int_dict(data.get("upgrades", {}))
	star_shards = _int_dict(data.get("star_shards", {}))
	pets.clear()
	for p in data.get("pets", []):
		var pet: Dictionary = p
		for key in ["id", "level", "xp", "stars"]:
			pet[key] = int(pet.get(key, 1 if key != "xp" else 0))
		pet["role"] = pet.get("role", "none")
		pets.append(pet)
	pets_version += 1

	# Version 1 saves have no timestamp, so they earn nothing offline.
	if data.has("saved_at"):
		# Clock changes can make this negative; apply_offline clamps it.
		offline_gold = apply_offline(Time.get_unix_time_from_system() - float(data["saved_at"]))


func _int_dict(source: Dictionary) -> Dictionary:
	var result := {}
	for key in source:
		result[key] = int(source[key])
	return result


func reset_progress() -> void:
	gold = 0
	training_level = 1
	training_xp = 0
	owned_skills.clear()
	equipped_skills.clear()
	pets.clear()
	pity.clear()
	upgrades.clear()
	star_shards.clear()
	kills = 0
	offline_gold = 0
	_next_pet_id = 1
	_train_frac = 0.0
	_income_frac = 0.0
	_fence_xp_frac = 0.0
	pets_version += 1
	_commit()
