extends Node2D
## Builds the world in code: the Lobby plot (safe zone with training, the pet
## fence and a Raid Board placeholder) and one Wilderness zone.

const HudScript = preload("res://scripts/ui/hud.gd")

const LOBBY_RECT := Rect2(-600, -400, 1200, 800)
const WILD_RECT := Rect2(1400, -900, 2400, 1800)
const WALL := 40.0
const TRAINING_RECT := Rect2(-520, -320, 280, 220)
const FENCE_RECT := Rect2(-520, 80, 360, 240)
const RAID_BOARD_POS := Vector2(0, -330)
const LOBBY_PORTAL := Vector2(520, 0)
const LOBBY_SPAWN := Vector2(380, 120)
const WILD_PORTAL := Vector2(1480, -160)
const WILD_SPAWN := Vector2(1620, 80)
const PORTAL_RADIUS := 46.0
const SPAWN_INTERVAL := 2.5

var player: Player
var hud: CanvasLayer
var monsters: Node2D
var in_wilderness := false

var _in_training := false
var _spawn_timer := 0.0
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_rng.randomize()
	_setup_input()
	_build_walls(LOBBY_RECT)
	_build_walls(WILD_RECT)
	_build_zone(TRAINING_RECT, func(entered): _set_training(entered))
	_build_portal(LOBBY_PORTAL, func(): _teleport(WILD_SPAWN, true))
	_build_portal(WILD_PORTAL, func(): _teleport(LOBBY_SPAWN, false))

	var fence := FenceDisplay.new()
	fence.area = FENCE_RECT
	add_child(fence)

	monsters = Node2D.new()
	add_child(monsters)

	player = Player.new()
	player.position = Vector2(0, 120)
	player.defeated.connect(_on_player_defeated)
	add_child(player)

	hud = HudScript.new()
	hud.player = player
	hud.train_pressed.connect(_on_train_pressed)
	add_child(hud)


func _process(delta: float) -> void:
	if _in_training:
		PlayerData.train(GameConfig.TRAIN_IDLE_XP_PER_SEC * delta)

	_spawn_timer -= delta
	if _spawn_timer <= 0.0:
		_spawn_timer = SPAWN_INTERVAL
		if monsters.get_child_count() < GameConfig.MAX_WILD_MONSTERS:
			_spawn_monster()


# --- Setup --------------------------------------------------------------------

func _setup_input() -> void:
	var keys := {
		"move_left": [KEY_A, KEY_LEFT], "move_right": [KEY_D, KEY_RIGHT],
		"move_up": [KEY_W, KEY_UP], "move_down": [KEY_S, KEY_DOWN],
		"attack": [KEY_J, KEY_SPACE], "skill_1": [KEY_K], "skill_2": [KEY_L],
	}
	for action in keys:
		if InputMap.has_action(action):
			continue
		InputMap.add_action(action)
		for key in keys[action]:
			var event := InputEventKey.new()
			event.physical_keycode = key
			InputMap.action_add_event(action, event)


func _build_walls(rect: Rect2) -> void:
	var body := StaticBody2D.new()
	body.collision_layer = 4
	body.collision_mask = 0
	var walls := [
		Rect2(rect.position.x - WALL, rect.position.y - WALL, rect.size.x + WALL * 2, WALL),
		Rect2(rect.position.x - WALL, rect.end.y, rect.size.x + WALL * 2, WALL),
		Rect2(rect.position.x - WALL, rect.position.y, WALL, rect.size.y),
		Rect2(rect.end.x, rect.position.y, WALL, rect.size.y),
	]
	for wall: Rect2 in walls:
		var shape := CollisionShape2D.new()
		var box := RectangleShape2D.new()
		box.size = wall.size
		shape.shape = box
		shape.position = wall.get_center()
		body.add_child(shape)
	add_child(body)


func _build_zone(rect: Rect2, on_change: Callable) -> void:
	var area := Area2D.new()
	area.collision_layer = 0
	area.collision_mask = 1
	var shape := CollisionShape2D.new()
	var box := RectangleShape2D.new()
	box.size = rect.size
	shape.shape = box
	area.position = rect.get_center()
	area.add_child(shape)
	area.body_entered.connect(func(body): if body is Player: on_change.call(true))
	area.body_exited.connect(func(body): if body is Player: on_change.call(false))
	add_child(area)


func _build_portal(pos: Vector2, on_enter: Callable) -> void:
	var area := Area2D.new()
	area.collision_layer = 0
	area.collision_mask = 1
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = PORTAL_RADIUS
	shape.shape = circle
	area.position = pos
	area.add_child(shape)
	# Physics state can't change inside the callback, so teleport deferred.
	area.body_entered.connect(func(body): if body is Player: on_enter.call_deferred())
	add_child(area)


# --- Gameplay -----------------------------------------------------------------

func _teleport(destination: Vector2, to_wilderness: bool) -> void:
	player.global_position = destination
	player.in_safe_zone = not to_wilderness
	in_wilderness = to_wilderness
	hud.area_name = "Wilderness" if to_wilderness else "Lobby"
	player.get_viewport().get_camera_2d().reset_smoothing()


func _set_training(active: bool) -> void:
	_in_training = active
	hud.set_training_available(active)


func _on_train_pressed() -> void:
	PlayerData.train(GameConfig.TRAIN_TAP_XP)
	Fx.spawn(self, TRAINING_RECT.get_center(), {"kind": "ring", "radius": 50.0, "color": Color(0.5, 0.9, 1.0), "duration": 0.25})


func _spawn_monster() -> void:
	var inner := WILD_RECT.grow(-80)
	var pos := Vector2.ZERO
	for _attempt in 10:
		pos = Vector2(_rng.randf_range(inner.position.x, inner.end.x), _rng.randf_range(inner.position.y, inner.end.y))
		if pos.distance_to(player.global_position) > 350.0 and pos.distance_to(WILD_SPAWN) > 300.0:
			break
	var monster := Monster.new()
	monster.setup(_pick_species(), pos, player)
	monster.died.connect(_on_monster_died)
	monsters.add_child(monster)


func _pick_species() -> String:
	var total := 0
	for entry in GameConfig.WILD_SPAWNS:
		total += int(entry[1])
	var roll := _rng.randi_range(1, total)
	for entry in GameConfig.WILD_SPAWNS:
		roll -= int(entry[1])
		if roll <= 0:
			return entry[0]
	return "Slime"


func _on_monster_died(monster: Monster) -> void:
	var drop := PlayerData.record_kill(monster.species_id)
	Fx.spawn(self, monster.global_position, {"kind": "text", "text": "+%d gold" % drop["gold"], "color": Color(1, 0.85, 0.3), "duration": 0.9})
	if drop["pet"] != null:
		var pet: Dictionary = drop["pet"]
		var note := "  (pity!)" if drop["pity_hit"] else ""
		hud.toast("New pet: %s %s (%s)%s" % [pet["size"], pet["species"], pet["rarity"], note], GameConfig.rarity_color(pet["rarity"]))
	if drop["skill_box"]:
		var learned: String = drop["skill_learned"]
		if learned != "":
			var skill: Dictionary = GameConfig.SKILLS[learned]
			hud.toast("Skill box: learned %s!" % skill["name"], skill["color"])
		else:
			hud.toast("Skill box: duplicate, +%d gold" % GameConfig.SKILL_BOX_DUPLICATE_GOLD, Color(1, 0.85, 0.3))


func _on_player_defeated() -> void:
	hud.toast("Defeated! Back to the Lobby.", Color(1, 0.4, 0.4))
	_teleport.call_deferred(LOBBY_SPAWN, false)
	player.heal_full()


# --- Drawing (placeholder art) ------------------------------------------------

func _draw() -> void:
	var font := ThemeDB.fallback_font
	draw_rect(LOBBY_RECT.grow(WALL), Color(0.30, 0.24, 0.18))
	draw_rect(LOBBY_RECT, Color(0.36, 0.52, 0.34))
	draw_rect(WILD_RECT.grow(WALL), Color(0.22, 0.18, 0.14))
	draw_rect(WILD_RECT, Color(0.24, 0.33, 0.20))
	# Scatter some deterministic "grass tufts" in the wilderness.
	var tufts := RandomNumberGenerator.new()
	tufts.seed = 7
	for i in 220:
		var p := Vector2(tufts.randf_range(WILD_RECT.position.x, WILD_RECT.end.x), tufts.randf_range(WILD_RECT.position.y, WILD_RECT.end.y))
		draw_circle(p, tufts.randf_range(6, 16), Color(0.20, 0.28, 0.16))

	# Training ground with dummies.
	draw_rect(TRAINING_RECT, Color(0.55, 0.45, 0.30))
	draw_rect(TRAINING_RECT, Color(0.35, 0.28, 0.18), false, 4.0)
	for x in [-460, -380, -300]:
		draw_rect(Rect2(x - 6, -240, 12, 50), Color(0.45, 0.30, 0.18))
		draw_circle(Vector2(x, -248), 16, Color(0.85, 0.75, 0.55))
	draw_string(font, TRAINING_RECT.position + Vector2(0, -12), "TRAINING", HORIZONTAL_ALIGNMENT_CENTER, TRAINING_RECT.size.x, 22)

	# Pet fence placeholder.
	draw_rect(FENCE_RECT, Color(0.42, 0.60, 0.36))
	for i in 13:
		var x := FENCE_RECT.position.x + i * FENCE_RECT.size.x / 12.0
		draw_rect(Rect2(x - 4, FENCE_RECT.position.y - 4, 8, 14), Color(0.55, 0.38, 0.22))
		draw_rect(Rect2(x - 4, FENCE_RECT.end.y - 10, 8, 14), Color(0.55, 0.38, 0.22))
	draw_rect(FENCE_RECT, Color(0.55, 0.38, 0.22), false, 3.0)
	draw_string(font, FENCE_RECT.position + Vector2(0, -12), "PET FENCE", HORIZONTAL_ALIGNMENT_CENTER, FENCE_RECT.size.x, 20)

	# Raid Board placeholder.
	draw_rect(Rect2(RAID_BOARD_POS - Vector2(80, 30), Vector2(160, 60)), Color(0.35, 0.22, 0.15))
	draw_string(font, RAID_BOARD_POS + Vector2(-80, 8), "RAID BOARD", HORIZONTAL_ALIGNMENT_CENTER, 160, 18)
	draw_string(font, RAID_BOARD_POS + Vector2(-80, 50), "(Phase 4)", HORIZONTAL_ALIGNMENT_CENTER, 160, 16, Color(1, 1, 1, 0.6))

	# Portals.
	for entry in [[LOBBY_PORTAL, "WILDERNESS"], [WILD_PORTAL, "LOBBY"]]:
		draw_circle(entry[0], PORTAL_RADIUS, Color(0.45, 0.30, 0.85, 0.7))
		draw_arc(entry[0], PORTAL_RADIUS, 0.0, TAU, 40, Color(0.8, 0.7, 1.0), 4.0)
		draw_string(font, entry[0] + Vector2(-90, -PORTAL_RADIUS - 12), entry[1], HORIZONTAL_ALIGNMENT_CENTER, 180, 20)
