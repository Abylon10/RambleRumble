class_name UpgradesPanel
extends PanelContainer
## Gold upgrades: fence slots, skill slots, training ground and pet lab.

var _rows := {}


func _ready() -> void:
	var parts := UiKit.make_modal(self, "Upgrades", Vector2(760, 480))
	var box: VBoxContainer = parts[0]
	for upgrade_id in GameConfig.UPGRADE_ORDER:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 12)
		var text_box := VBoxContainer.new()
		text_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var title := UiKit.label("", 20, Color(1, 0.9, 0.7))
		var detail := UiKit.label("", 15, Color(0.8, 0.8, 0.8))
		text_box.add_child(title)
		text_box.add_child(detail)
		row.add_child(text_box)
		var button := Button.new()
		button.custom_minimum_size = Vector2(180, 56)
		button.pressed.connect(func(): PlayerData.buy_upgrade(upgrade_id))
		row.add_child(button)
		box.add_child(row)
		_rows[upgrade_id] = {"title": title, "detail": detail, "button": button}
	PlayerData.changed.connect(refresh)
	visibility_changed.connect(refresh)


func refresh() -> void:
	if not visible:
		return
	for upgrade_id in GameConfig.UPGRADE_ORDER:
		var upgrade: Dictionary = GameConfig.UPGRADES[upgrade_id]
		var row: Dictionary = _rows[upgrade_id]
		var level := PlayerData.upgrade_level(upgrade_id)
		row["title"].text = "%s   Lv %d / %d" % [upgrade["name"], level, upgrade["max"]]
		row["detail"].text = "%s\nNow: %s" % [upgrade["desc"], _current_effect(upgrade_id)]
		var button: Button = row["button"]
		if PlayerData.upgrade_maxed(upgrade_id):
			button.text = "Maxed"
			button.disabled = true
		else:
			var cost := PlayerData.next_upgrade_cost(upgrade_id)
			button.text = "Buy  %dg" % cost
			button.disabled = PlayerData.gold < cost


func _current_effect(upgrade_id: String) -> String:
	match upgrade_id:
		"fence_slots":
			return "%d fence slots" % PlayerData.fence_slots()
		"skill_slots":
			return "%d skill slots" % PlayerData.skill_slots()
		"training_ground":
			return "x%.2f training XP" % PlayerData.training_multiplier()
		"pet_lab":
			return "up to %d★, -%d%% merge cost" % [PlayerData.max_stars(), int(PlayerData.merge_discount() * 100)]
	return ""
