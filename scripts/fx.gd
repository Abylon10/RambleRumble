class_name Fx
extends Node2D
## Short-lived visual effect: an expanding ring, a filled circle, a line, or
## floating text. Placeholder art until real sprites and particles arrive.

var kind := "ring"
var color := Color.WHITE
var radius := 40.0
var to := Vector2.ZERO
var text := ""
var duration := 0.3

var _t := 0.0


static func spawn(parent: Node, pos: Vector2, props: Dictionary) -> Fx:
	var fx := Fx.new()
	for key in props:
		fx.set(key, props[key])
	fx.position = pos
	fx.z_index = 10
	parent.add_child(fx)
	return fx


func _process(delta: float) -> void:
	_t += delta
	if kind == "text":
		position.y -= 45.0 * delta
	queue_redraw()
	if _t >= duration:
		queue_free()


func _draw() -> void:
	var fade := clampf(1.0 - _t / duration, 0.0, 1.0)
	match kind:
		"ring":
			draw_arc(Vector2.ZERO, radius * (0.6 + 0.4 * (1.0 - fade)), 0.0, TAU, 40, Color(color, fade), 5.0)
		"circle":
			draw_circle(Vector2.ZERO, radius, Color(color, fade * 0.55))
		"line":
			draw_line(Vector2.ZERO, to, Color(color, fade), 8.0)
		"text":
			draw_string(ThemeDB.fallback_font, Vector2(-60, 0), text, HORIZONTAL_ALIGNMENT_CENTER, 120, 20, Color(color, fade))
