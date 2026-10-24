class_name MoveJoystick
extends Control
## Multitouch virtual joystick. Reads raw touch events in _input so it keeps
## working while another finger holds an action button.

const KNOB_RADIUS := 34.0

var vector := Vector2.ZERO

var _touch_index := -1
var _knob := Vector2.ZERO


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func _input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed and _touch_index == -1 and get_global_rect().has_point(touch.position):
			_touch_index = touch.index
			_update(touch.position)
		elif not touch.pressed and touch.index == _touch_index:
			_touch_index = -1
			vector = Vector2.ZERO
			_knob = Vector2.ZERO
			queue_redraw()
	elif event is InputEventScreenDrag:
		var drag := event as InputEventScreenDrag
		if drag.index == _touch_index:
			_update(drag.position)


func _update(screen_pos: Vector2) -> void:
	var radius := size.x * 0.5
	var center := get_global_rect().get_center()
	_knob = (screen_pos - center).limit_length(radius)
	vector = _knob / radius
	queue_redraw()


func _draw() -> void:
	var center := size * 0.5
	draw_circle(center, size.x * 0.5, Color(0, 0, 0, 0.30))
	draw_arc(center, size.x * 0.5, 0.0, TAU, 48, Color(1, 1, 1, 0.35), 3.0)
	draw_circle(center + _knob, KNOB_RADIUS, Color(1, 1, 1, 0.55))
