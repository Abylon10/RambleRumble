class_name TouchButton
extends Control
## Round action button that supports multitouch (unlike Button, which only
## sees the first finger through mouse emulation). `held` stays true while
## pressed so the HUD can repeat actions like the basic attack.

signal pressed

var label := ""
var color := Color.WHITE
## 1.0 = just used, 0.0 = ready. Drawn as a dark sweep.
var cooldown_ratio := 0.0
var held := false

var _touch_index := -1


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _input(event: InputEvent) -> void:
	if not is_visible_in_tree():
		return
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		var center := get_global_rect().get_center()
		if touch.pressed and _touch_index == -1 and touch.position.distance_to(center) <= size.x * 0.5:
			_touch_index = touch.index
			held = true
			pressed.emit()
			get_viewport().set_input_as_handled()
		elif not touch.pressed and touch.index == _touch_index:
			_touch_index = -1
			held = false


func _process(_delta: float) -> void:
	queue_redraw()


func _draw() -> void:
	var center := size * 0.5
	var radius := size.x * 0.5
	draw_circle(center, radius, Color(0, 0, 0, 0.45 if not held else 0.6))
	draw_arc(center, radius - 2, 0.0, TAU, 48, color, 4.0)
	if cooldown_ratio > 0.0:
		var points := PackedVector2Array([center])
		var steps := 32
		for i in steps + 1:
			var angle := -PI / 2 + TAU * cooldown_ratio * i / steps
			points.append(center + Vector2.from_angle(angle) * (radius - 4))
		draw_colored_polygon(points, Color(0, 0, 0, 0.55))
	var font := ThemeDB.fallback_font
	var font_size := 22 if size.x > 110 else 17
	draw_string(font, Vector2(0, center.y + font_size * 0.35), label, HORIZONTAL_ALIGNMENT_CENTER, size.x, font_size, Color.WHITE)
