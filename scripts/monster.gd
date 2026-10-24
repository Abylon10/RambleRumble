class_name Monster
extends CharacterBody2D
## A wilderness monster: wanders near its spawn point, chases the player inside
## its aggro range, and gives up once pulled too far from home.

signal died(monster: Monster)

const AGGRO_RANGE := 260.0
const LEASH_RANGE := 520.0
const ATTACK_COOLDOWN := 1.1
const PLAYER_RADIUS := 18.0

var species_id := "Slime"
var data: Dictionary
var hp := 1.0
var max_hp := 1.0
var radius := 16.0
var home := Vector2.ZERO
var target: Player

var _attack_timer := 0.0
var _stun_left := 0.0
var _burn_left := 0.0
var _burn_dps := 0.0
var _flash := 0.0
var _wander_dir := Vector2.ZERO
var _wander_timer := 0.0
var _dead := false


func setup(id: String, pos: Vector2, player: Player) -> void:
	species_id = id
	data = GameConfig.SPECIES[id]
	max_hp = data["hp"]
	hp = max_hp
	radius = data["radius"]
	position = pos
	home = pos
	target = player


func _ready() -> void:
	add_to_group("monsters")
	collision_layer = 2
	collision_mask = 4
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = radius
	shape.shape = circle
	add_child(shape)


func _physics_process(delta: float) -> void:
	if _dead:
		return
	_attack_timer = maxf(0.0, _attack_timer - delta)
	_flash = maxf(0.0, _flash - delta)
	if _burn_left > 0.0:
		_burn_left -= delta
		take_damage(_burn_dps * delta, false)
		if _dead:
			return
	queue_redraw()
	if _stun_left > 0.0:
		_stun_left -= delta
		return

	var speed: float = data["speed"]
	var to_player := target.global_position - global_position
	var from_home := global_position - home
	if to_player.length() < AGGRO_RANGE and from_home.length() < LEASH_RANGE:
		if to_player.length() <= radius + PLAYER_RADIUS + 8.0:
			velocity = Vector2.ZERO
			if _attack_timer <= 0.0:
				_attack_timer = ATTACK_COOLDOWN
				target.take_damage(data["damage"])
		else:
			velocity = to_player.normalized() * speed
	elif from_home.length() > 60.0:
		velocity = -from_home.normalized() * speed
	else:
		_wander_timer -= delta
		if _wander_timer <= 0.0:
			_wander_timer = randf_range(1.0, 3.0)
			_wander_dir = Vector2.ZERO if randf() < 0.4 else Vector2.from_angle(randf() * TAU)
		velocity = _wander_dir * speed * 0.4
	move_and_slide()


func take_damage(amount: float, show_number := true) -> void:
	if _dead:
		return
	hp -= amount
	_flash = 0.1
	if show_number:
		Fx.spawn(get_parent(), global_position + Vector2(0, -radius - 12), {
			"kind": "text", "text": str(int(round(amount))), "color": Color.WHITE, "duration": 0.6})
	if hp <= 0.0:
		_dead = true
		died.emit(self)
		queue_free()


func stun(seconds: float) -> void:
	_stun_left = maxf(_stun_left, seconds)


func burn(dps: float, seconds: float) -> void:
	_burn_dps = dps
	_burn_left = seconds


func _draw() -> void:
	var body: Color = Color.WHITE if _flash > 0.0 else data["color"]
	if _burn_left > 0.0:
		body = body.lerp(Color(1, 0.4, 0.1), 0.35)
	draw_circle(Vector2.ZERO, radius, body)
	draw_arc(Vector2.ZERO, radius, 0.0, TAU, 32, GameConfig.rarity_color(data["rarity"]), 3.0)
	var eye_dir := (target.global_position - global_position).normalized() * radius * 0.35
	draw_circle(eye_dir + Vector2(-5, -4), 3.0, Color.BLACK)
	draw_circle(eye_dir + Vector2(5, -4), 3.0, Color.BLACK)
	if _stun_left > 0.0:
		draw_arc(Vector2(0, -radius - 6), 8.0, 0.0, TAU, 12, Color(1, 1, 0.4), 2.0)
	if hp < max_hp:
		var w := radius * 2.2
		draw_rect(Rect2(-w / 2, -radius - 10, w, 5), Color(0, 0, 0, 0.6))
		draw_rect(Rect2(-w / 2, -radius - 10, w * hp / max_hp, 5), Color(0.9, 0.2, 0.2))
