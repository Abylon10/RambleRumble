class_name Player
extends CharacterBody2D
## The player's character: movement (keyboard or virtual joystick), a basic
## attack, and the equipped skills. Attacks auto-aim at the nearest monster,
## which matters on touch screens.

signal defeated

const SPEED := 230.0
const RADIUS := 18.0
const AUTO_AIM_RANGE := 280.0
const SAFE_ZONE_REGEN := 25.0
const DASH_SPEED := 1400.0
const DASH_TIME := 0.16

var hp := 100.0
var joystick_vector := Vector2.ZERO
var facing := Vector2.RIGHT
var in_safe_zone := true
## action id -> seconds of cooldown left
var cooldowns := {}

var _dash_left := 0.0
var _dash_dir := Vector2.ZERO
var _dash_damage := 0.0
var _dash_hit := {}
var _invuln := 0.0
var _flash := 0.0


func _ready() -> void:
	add_to_group("player")
	collision_layer = 1
	collision_mask = 4
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = RADIUS
	shape.shape = circle
	add_child(shape)
	var camera := Camera2D.new()
	camera.position_smoothing_enabled = true
	add_child(camera)
	camera.make_current()
	hp = PlayerData.max_hp()


func _physics_process(delta: float) -> void:
	for key in cooldowns.keys():
		cooldowns[key] = maxf(0.0, cooldowns[key] - delta)
	_invuln = maxf(0.0, _invuln - delta)
	_flash = maxf(0.0, _flash - delta)
	queue_redraw()

	if _dash_left > 0.0:
		_dash_left -= delta
		velocity = _dash_dir * DASH_SPEED
		move_and_slide()
		_apply_dash_hits()
		return

	var move := (Input.get_vector("move_left", "move_right", "move_up", "move_down") + joystick_vector).limit_length(1.0)
	if move.length() > 0.1:
		facing = move.normalized()
	velocity = move * SPEED
	move_and_slide()

	if in_safe_zone:
		hp = minf(PlayerData.max_hp(), hp + SAFE_ZONE_REGEN * delta)

	if Input.is_action_pressed("attack"):
		basic_attack()
	if Input.is_action_just_pressed("skill_1"):
		use_skill(0)
	if Input.is_action_just_pressed("skill_2"):
		use_skill(1)


## 1.0 right after use, 0.0 when ready.
func cooldown_ratio(action_id: String, total: float) -> float:
	return clampf(float(cooldowns.get(action_id, 0.0)) / total, 0.0, 1.0)


func basic_attack() -> void:
	if not _start_cooldown("basic", GameConfig.BASIC_ATTACK["cooldown"]):
		return
	_aim()
	var reach: float = GameConfig.BASIC_ATTACK["range"]
	var damage: float = PlayerData.attack_power() * float(GameConfig.BASIC_ATTACK["damage"])
	for monster in _monsters_in_arc(reach):
		monster.take_damage(damage)
	Fx.spawn(get_parent(), global_position + facing * 34.0, {"kind": "ring", "radius": 30.0, "duration": 0.15})


func use_skill(slot: int) -> void:
	if slot >= PlayerData.equipped_skills.size():
		return
	var skill_id: String = PlayerData.equipped_skills[slot]
	var skill: Dictionary = GameConfig.SKILLS[skill_id]
	if not _start_cooldown(skill_id, skill["cooldown"]):
		return
	_aim()
	var damage: float = PlayerData.attack_power() * float(skill["damage"])
	var color: Color = skill["color"]
	match skill_id:
		"flame_fist":
			for monster in _monsters_in_arc(84.0):
				monster.take_damage(damage)
				monster.burn(damage * 0.25, 2.0)
			Fx.spawn(get_parent(), global_position + facing * 45.0, {"kind": "circle", "radius": 46.0, "color": color, "duration": 0.25})
		"wind_slash":
			var projectile := Projectile.new()
			projectile.position = global_position + facing * 20.0
			projectile.direction = facing
			projectile.damage = damage
			projectile.color = color
			get_parent().add_child(projectile)
		"stone_breaker":
			for node in get_tree().get_nodes_in_group("monsters"):
				var monster := node as Monster
				if monster.global_position.distance_to(global_position) <= 120.0 + monster.radius:
					monster.take_damage(damage)
					monster.stun(1.2)
			Fx.spawn(get_parent(), global_position, {"kind": "ring", "radius": 120.0, "color": color, "duration": 0.35})
		"thunder_dash":
			_dash_left = DASH_TIME
			_dash_dir = facing
			_dash_damage = damage
			_dash_hit.clear()
			_invuln = DASH_TIME + 0.1
			Fx.spawn(get_parent(), global_position, {"kind": "line", "to": facing * DASH_SPEED * DASH_TIME, "color": color, "duration": 0.3})


func take_damage(amount: float) -> void:
	if _invuln > 0.0 or in_safe_zone:
		return
	hp -= amount
	_flash = 0.12
	Fx.spawn(get_parent(), global_position + Vector2(0, -RADIUS - 12), {
		"kind": "text", "text": "-%d" % int(round(amount)), "color": Color(1, 0.4, 0.4), "duration": 0.6})
	if hp <= 0.0:
		hp = PlayerData.max_hp()
		defeated.emit()


func heal_full() -> void:
	hp = PlayerData.max_hp()


func _start_cooldown(action_id: String, seconds: float) -> bool:
	if float(cooldowns.get(action_id, 0.0)) > 0.0:
		return false
	cooldowns[action_id] = seconds
	return true


func _aim() -> void:
	var best: Monster = null
	var best_dist := AUTO_AIM_RANGE
	for node in get_tree().get_nodes_in_group("monsters"):
		var monster := node as Monster
		var dist := monster.global_position.distance_to(global_position)
		if dist < best_dist:
			best_dist = dist
			best = monster
	if best != null:
		facing = (best.global_position - global_position).normalized()


func _monsters_in_arc(reach: float) -> Array[Monster]:
	var hits: Array[Monster] = []
	for node in get_tree().get_nodes_in_group("monsters"):
		var monster := node as Monster
		var offset := monster.global_position - global_position
		if offset.length() > reach + monster.radius:
			continue
		if offset.length() < RADIUS + monster.radius or facing.dot(offset.normalized()) > 0.3:
			hits.append(monster)
	return hits


func _apply_dash_hits() -> void:
	for node in get_tree().get_nodes_in_group("monsters"):
		var monster := node as Monster
		if _dash_hit.has(monster):
			continue
		if monster.global_position.distance_to(global_position) <= RADIUS + monster.radius + 16.0:
			_dash_hit[monster] = true
			monster.take_damage(_dash_damage)


func _draw() -> void:
	var body := Color.WHITE if _flash > 0.0 else Color(0.95, 0.80, 0.55)
	draw_circle(Vector2.ZERO, RADIUS, body)
	draw_arc(Vector2.ZERO, RADIUS, 0.0, TAU, 32, Color(0.25, 0.2, 0.15), 3.0)
	draw_circle(facing * RADIUS * 0.75, 5.0, Color(0.25, 0.2, 0.15))
	if _invuln > 0.0:
		draw_arc(Vector2.ZERO, RADIUS + 5, 0.0, TAU, 32, Color(1, 0.95, 0.4, 0.8), 2.0)
