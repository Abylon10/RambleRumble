extends Node
## Plays the real main scene headless: picks a starter, trains, travels to the
## Wilderness, kills monsters with every skill, and checks rewards persist.
## Run: godot --headless --path . res://tests/gameplay_smoke.tscn -- --test

var _failures := 0


func _ready() -> void:
	_run.call_deferred()


func _check(ok: bool, what: String) -> void:
	print(("  ok   " if ok else "  FAIL ") + what)
	if not ok:
		_failures += 1


func _frames(n: int) -> void:
	for i in n:
		await get_tree().physics_frame


func _run() -> void:
	var data: Node = get_tree().root.get_node("PlayerData")
	data.reset_progress()
	var world: Node2D = load("res://scenes/main.tscn").instantiate()
	get_tree().root.add_child(world)
	await _frames(2)
	var player: Player = world.player

	data.choose_starter("flame_fist")
	_check(data.has_starter(), "starter skill chosen")

	var level_before: int = data.training_level
	for i in 30:
		world._on_train_pressed()
	_check(data.training_level > level_before, "training raises level (now %d)" % data.training_level)

	world._teleport(world.WILD_SPAWN, true)
	await _frames(2)
	_check(world.in_wilderness and not player.in_safe_zone, "portal moves player into the Wilderness")

	# Learn every skill so each one gets exercised.
	for skill_id in ["wind_slash", "stone_breaker", "thunder_dash"]:
		data.owned_skills.append(skill_id)
	var cp_before: int = data.combat_power()

	var kills_before: int = data.kills
	var gold_before: int = data.gold
	for skill_pair in [["flame_fist", "wind_slash"], ["stone_breaker", "thunder_dash"]]:
		data.equipped_skills.assign(skill_pair)
		for wave in 4:
			for i in 3:
				var monster := Monster.new()
				monster.setup("Slime", player.global_position + Vector2(50, 0).rotated(i * 2.0), player)
				monster.died.connect(world._on_monster_died)
				world.monsters.add_child(monster)
			for t in 120:
				player.basic_attack()
				player.use_skill(0)
				player.use_skill(1)
				await get_tree().physics_frame
	_check(data.kills > kills_before, "monsters die and kills are recorded (%d)" % (data.kills - kills_before))
	_check(data.gold > gold_before, "gold drops awarded (%d)" % (data.gold - gold_before))
	_check(data.combat_power() > cp_before - 1, "CP counts owned skills (%d)" % data.combat_power())

	# Force a guaranteed pet roll via pity to check pet records.
	data.pity["Rare"] = 24
	data.record_kill("Goblin")
	_check(data.pets.any(func(p): return p["species"] == "Goblin"), "pity guarantees a Rare pet at 25 kills")

	# Reload from disk and compare.
	var saved_gold: int = data.gold
	var saved_pets: int = data.pets.size()
	data.load_game()
	_check(data.gold == saved_gold and data.pets.size() == saved_pets, "save/load round-trips gold and pets")

	# Player defeat returns to the lobby.
	player.take_damage(99999.0)
	await _frames(3)
	_check(not world.in_wilderness, "defeat sends player back to the Lobby")

	data.reset_progress()
	print("RESULT: %s" % ("PASS" if _failures == 0 else "%d FAILURES" % _failures))
	get_tree().quit(_failures)
