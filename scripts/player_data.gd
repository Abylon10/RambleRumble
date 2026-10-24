extends Node
## The player's progress, saved locally. When online raids arrive (Phase 5)
## the backend becomes the source of truth, so every change goes through here.

signal changed
signal notify(text: String, color: Color)

## Tests pass "-- --test" so they never touch the real save.
var save_path := "user://test_save.json" if OS.get_cmdline_user_args().has("--test") else "user://save.json"
const SAVE_VERSION := 1

var gold: int = 0
var training_level: int = 1
var training_xp: int = 0
var owned_skills: Array[String] = []
var equipped_skills: Array[String] = []
var pets: Array[Dictionary] = []
var pity: Dictionary = {}
var kills: int = 0
var rng := RandomNumberGenerator.new()

var _next_pet_id: int = 1


func _ready() -> void:
	rng.randomize()
	load_game()


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
	return GameConfig.BASE_SKILL_SLOTS


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
	return float(species["pet_power"]) * (1.0 + GameConfig.PET_LEVEL_BONUS * (int(pet["level"]) - 1)) \
		* float(star["mult"]) * (1.0 + (size_mult - 1.0) * 0.5)


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
		pet["role"] = "none"
		_next_pet_id += 1
		pets.append(pet)
	drop["skill_learned"] = _open_skill_box() if drop["skill_box"] else ""
	_commit()
	return drop


# --- Persistence --------------------------------------------------------------

func _commit(save := true) -> void:
	changed.emit()
	if save:
		save_game()


func save_game() -> void:
	var data := {
		"version": SAVE_VERSION,
		"gold": gold,
		"training_level": training_level,
		"training_xp": training_xp,
		"owned_skills": owned_skills,
		"equipped_skills": equipped_skills,
		"pets": pets,
		"pity": pity,
		"kills": kills,
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
	pity.clear()
	var saved_pity: Dictionary = data.get("pity", {})
	for rarity in saved_pity:
		pity[rarity] = int(saved_pity[rarity])
	pets.clear()
	for p in data.get("pets", []):
		var pet: Dictionary = p
		for key in ["id", "level", "xp", "stars"]:
			pet[key] = int(pet.get(key, 1))
		pets.append(pet)


func reset_progress() -> void:
	gold = 0
	training_level = 1
	training_xp = 0
	owned_skills.clear()
	equipped_skills.clear()
	pets.clear()
	pity.clear()
	kills = 0
	_next_pet_id = 1
	_commit()
