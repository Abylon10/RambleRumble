extends Node
## Opens the main scene in a given state and saves a screenshot.
## Run: godot --path . res://tests/screenshot.tscn -- --test <out.png> [starter] [wild] [pets] [upgrades] [fence]

func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	var out: String = args[1]
	PlayerData.reset_progress()
	var world: Node2D = load("res://scenes/main.tscn").instantiate()
	get_tree().root.add_child.call_deferred(world)
	await get_tree().create_timer(0.5).timeout
	if args.has("starter"):
		PlayerData.choose_starter("wind_slash")
		world.hud._starter_panel.visible = false
	if args.has("pets") or args.has("upgrades") or args.has("fence"):
		_seed_pets()
		world.player.global_position = Vector2(-340, 150)
	if args.has("wild"):
		world._teleport(world.WILD_SPAWN, true)
		for i in 6:
			world._spawn_monster()
	await get_tree().create_timer(1.0).timeout
	if args.has("pets"):
		world.hud._open(world.hud._pets_panel)
		var panel: PetsPanel = world.hud._pets_panel
		panel._selected_id = 1
		panel._grid_version = -1
		panel.refresh()
	if args.has("upgrades"):
		world.hud._open(world.hud._upgrades_panel)
	await get_tree().create_timer(1.0).timeout
	get_viewport().get_texture().get_image().save_png(out)
	PlayerData.reset_progress()
	get_tree().quit()


func _seed_pets() -> void:
	PlayerData.gold = 3200
	var specs := [["Goblin", "Giant", 2], ["Wolf", "Big", 1], ["Wolf", "Normal", 1], ["Slime", "Titan", 1],
		["Slime", "Normal", 1], ["Slime", "Tiny", 1], ["Wolf", "Tiny", 1]]
	for spec in specs:
		var id: int = PlayerData._next_pet_id
		PlayerData._next_pet_id += 1
		PlayerData.pets.append({"id": id, "species": spec[0], "rarity": GameConfig.SPECIES[spec[0]]["rarity"],
			"size": spec[1], "level": 4, "xp": 15, "stars": spec[2], "role": "none"})
	for id in [1, 2, 4]:
		PlayerData.set_fenced(id, true)
	PlayerData.star_shards["Uncommon"] = 1
