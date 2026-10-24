class_name Projectile
extends Node2D
## Wind Slash projectile: flies straight and hits each monster once (pierces).

const SPEED := 650.0
const MAX_DISTANCE := 420.0
const HIT_RADIUS := 22.0

var direction := Vector2.RIGHT
var damage := 10.0
var color := Color.WHITE

var _travelled := 0.0
var _hit := {}


func _physics_process(delta: float) -> void:
	var step := SPEED * delta
	position += direction * step
	_travelled += step
	for node in get_tree().get_nodes_in_group("monsters"):
		var monster := node as Monster
		if monster == null or _hit.has(monster):
			continue
		if monster.global_position.distance_to(global_position) <= HIT_RADIUS + monster.radius:
			_hit[monster] = true
			monster.take_damage(damage)
	queue_redraw()
	if _travelled >= MAX_DISTANCE:
		queue_free()


func _draw() -> void:
	var angle := direction.angle()
	draw_arc(Vector2.ZERO, 20.0, angle - 1.1, angle + 1.1, 16, color, 6.0)
	draw_arc(Vector2(-8, 0).rotated(angle), 16.0, angle - 0.9, angle + 0.9, 12, Color(color, 0.5), 3.0)
