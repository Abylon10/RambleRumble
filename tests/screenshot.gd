extends Node
## Opens the main scene, optionally in the Wilderness, and saves a screenshot.
## Run: godot --path . res://tests/screenshot.tscn -- --test <out.png> [wild]

func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	var out: String = args[1]
	var world: Node2D = load("res://scenes/main.tscn").instantiate()
	get_tree().root.add_child.call_deferred(world)
	await get_tree().create_timer(0.5).timeout
	if args.has("starter"):
		PlayerData.choose_starter("wind_slash")
		world.hud._starter_panel.visible = false
	if args.has("wild"):
		world._teleport(world.WILD_SPAWN, true)
		for i in 6:
			world._spawn_monster()
	await get_tree().create_timer(1.5).timeout
	get_viewport().get_texture().get_image().save_png(out)
	PlayerData.reset_progress()
	get_tree().quit()
