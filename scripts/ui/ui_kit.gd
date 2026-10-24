class_name UiKit
extends RefCounted
## Shared helpers for building the code-made UI.


## Places `ctrl` relative to an anchor point (0..1 on each axis) of its parent.
static func place(ctrl: Control, anchor: Vector2, offset: Vector2, ctrl_size: Vector2) -> void:
	ctrl.anchor_left = anchor.x
	ctrl.anchor_right = anchor.x
	ctrl.anchor_top = anchor.y
	ctrl.anchor_bottom = anchor.y
	ctrl.offset_left = offset.x
	ctrl.offset_top = offset.y
	ctrl.offset_right = offset.x + ctrl_size.x
	ctrl.offset_bottom = offset.y + ctrl_size.y


static func panel_style(border: Color, width := 2) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.09, 0.08, 0.92)
	style.border_color = border
	style.set_border_width_all(width)
	style.set_corner_radius_all(10)
	style.set_content_margin_all(12)
	return style


static func label(text: String, font_size := 20, color := Color.WHITE) -> Label:
	var result := Label.new()
	result.text = text
	result.add_theme_font_size_override("font_size", font_size)
	result.add_theme_color_override("font_color", color)
	return result


## Turns `panel` into a centered modal with a title row; returns [content box, header row].
static func make_modal(panel: PanelContainer, title: String, panel_size: Vector2, closable := true) -> Array:
	panel.add_theme_stylebox_override("panel", panel_style(Color(0.9, 0.7, 0.4)))
	place(panel, Vector2(0.5, 0.5), -panel_size / 2, panel_size)
	panel.visible = false
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 10)
	panel.add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	var title_label := label(title, 26)
	title_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title_label)
	if closable:
		var close := Button.new()
		close.text = "  Close  "
		close.pressed.connect(func(): panel.visible = false)
		header.add_child(close)
	return [box, header]
