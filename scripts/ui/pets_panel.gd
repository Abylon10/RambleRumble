class_name PetsPanel
extends PanelContainer
## Pet collection and Pet Lab: a card grid on the left and the selected
## pet's details on the right (fence, feed treats, star-up merge, release).

var _summary: Label
var _grid: GridContainer
var _name_label: Label
var _info_label: Label
var _xp_bar: ProgressBar
var _fence_button: Button
var _feed_button: Button
var _feed_many_button: Button
var _merge_label: Label
var _merge_button: Button
var _release_button: Button
var _detail: VBoxContainer
var _empty_hint: Label

var _selected_id := -1
var _grid_version := -1
var _release_armed := false


func _ready() -> void:
	var parts := UiKit.make_modal(self, "Pets", Vector2(1120, 620))
	var box: VBoxContainer = parts[0]
	_summary = UiKit.label("", 16, Color(0.8, 0.8, 0.8))
	box.add_child(_summary)

	var columns := HBoxContainer.new()
	columns.size_flags_vertical = Control.SIZE_EXPAND_FILL
	columns.add_theme_constant_override("separation", 14)
	box.add_child(columns)

	var scroll := ScrollContainer.new()
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	columns.add_child(scroll)
	_grid = GridContainer.new()
	_grid.columns = 3
	_grid.add_theme_constant_override("h_separation", 8)
	_grid.add_theme_constant_override("v_separation", 8)
	scroll.add_child(_grid)

	_detail = VBoxContainer.new()
	_detail.custom_minimum_size = Vector2(360, 0)
	_detail.add_theme_constant_override("separation", 8)
	columns.add_child(_detail)
	_empty_hint = UiKit.label("Select a pet.", 18, Color(0.7, 0.7, 0.7))
	_detail.add_child(_empty_hint)
	_name_label = UiKit.label("", 24)
	_detail.add_child(_name_label)
	_info_label = UiKit.label("", 16, Color(0.85, 0.85, 0.85))
	_detail.add_child(_info_label)
	_xp_bar = ProgressBar.new()
	_xp_bar.show_percentage = false
	_xp_bar.custom_minimum_size = Vector2(0, 10)
	_detail.add_child(_xp_bar)

	_fence_button = _action("", _on_fence)
	var feed_row := HBoxContainer.new()
	feed_row.add_theme_constant_override("separation", 8)
	_detail.add_child(feed_row)
	_feed_button = _action("", func(): _feed(1), feed_row)
	_feed_many_button = _action("", func(): _feed(10), feed_row)
	_merge_label = UiKit.label("", 15, Color(0.85, 0.8, 0.6))
	_merge_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_detail.add_child(_merge_label)
	_merge_button = _action("", _on_merge)
	_release_button = _action("", _on_release)

	PlayerData.changed.connect(refresh)
	visibility_changed.connect(refresh)


func _action(text: String, callback: Callable, parent: Control = null) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(0, 48)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.pressed.connect(callback)
	(parent if parent != null else _detail).add_child(button)
	return button


func refresh() -> void:
	if not visible:
		return
	var pity_parts: Array[String] = []
	for rarity in GameConfig.RARITY_ORDER:
		var limit: int = GameConfig.RARITY[rarity]["pity"]
		if limit > 0:
			pity_parts.append("%s %d/%d" % [rarity, int(PlayerData.pity.get(rarity, 0)), limit])
	var shard_parts: Array[String] = []
	for rarity in GameConfig.RARITY_ORDER:
		var count := int(PlayerData.star_shards.get(rarity, 0))
		if count > 0:
			shard_parts.append("%s %d" % [rarity, count])
	_summary.text = "%d pets  •  Fence %d/%d (+%.1f gold/min)  •  Star Shards: %s  •  Pity: %s" % [
		PlayerData.pets.size(), PlayerData.fenced_pets().size(), PlayerData.fence_slots(),
		PlayerData.income_per_min(), ", ".join(shard_parts) if not shard_parts.is_empty() else "none",
		", ".join(pity_parts)]

	if PlayerData.find_pet(_selected_id).is_empty():
		_selected_id = -1
		_release_armed = false
	if _grid_version != PlayerData.pets_version:
		_grid_version = PlayerData.pets_version
		_rebuild_grid()
	_refresh_detail()


func _rebuild_grid() -> void:
	for child in _grid.get_children():
		child.queue_free()
	var sorted := PlayerData.pets.duplicate()
	sorted.sort_custom(func(a, b):
		var ra := GameConfig.RARITY_ORDER.find(a["rarity"])
		var rb := GameConfig.RARITY_ORDER.find(b["rarity"])
		if ra != rb:
			return ra > rb
		if a["stars"] != b["stars"]:
			return a["stars"] > b["stars"]
		return PlayerData.pet_power(a) > PlayerData.pet_power(b))
	for pet in sorted:
		var pet_id := int(pet["id"])
		var color := GameConfig.rarity_color(pet["rarity"])
		var card := Button.new()
		card.custom_minimum_size = Vector2(220, 92)
		card.alignment = HORIZONTAL_ALIGNMENT_LEFT
		card.text = "%s %s  %s\nLv %d%s" % [
			pet["size"], pet["species"], "★".repeat(int(pet["stars"])), pet["level"],
			"   • In fence" if pet["role"] == "fence" else ""]
		card.add_theme_font_size_override("font_size", 16)
		card.add_theme_color_override("font_color", color.lightened(0.3))
		var selected := pet_id == _selected_id
		for state in ["normal", "hover", "pressed", "focus"]:
			card.add_theme_stylebox_override(state, UiKit.panel_style(Color.WHITE if selected else color, 4 if selected else 2))
		card.pressed.connect(func():
			_selected_id = pet_id
			_release_armed = false
			_grid_version = -1
			refresh())
		_grid.add_child(card)
	if sorted.is_empty():
		_grid.add_child(UiKit.label("No pets yet. Defeat monsters in the Wilderness!", 18))


func _refresh_detail() -> void:
	var pet := PlayerData.find_pet(_selected_id)
	var has_pet := not pet.is_empty()
	_empty_hint.visible = not has_pet
	for child in _detail.get_children():
		if child != _empty_hint:
			child.visible = has_pet
	if not has_pet:
		return

	var color := GameConfig.rarity_color(pet["rarity"])
	var level := int(pet["level"])
	var max_level := PlayerData.pet_max_level(pet)
	_name_label.text = "%s %s  %s" % [pet["size"], pet["species"], "★".repeat(int(pet["stars"]))]
	_name_label.add_theme_color_override("font_color", color.lightened(0.2))
	_info_label.text = "%s  •  Level %d / %d\nPower %d  •  Income %.2f gold/min\nRole: %s" % [
		pet["rarity"], level, max_level, int(PlayerData.pet_power(pet)), PlayerData.pet_income_per_min(pet),
		"Fence (earning gold)" if pet["role"] == "fence" else "Resting"]
	_xp_bar.max_value = GameConfig.pet_xp_needed(level)
	_xp_bar.value = int(pet["xp"]) if level < max_level else _xp_bar.max_value

	var fenced: bool = pet["role"] == "fence"
	var fence_full := PlayerData.fenced_pets().size() >= PlayerData.fence_slots()
	_fence_button.text = "Remove from fence" if fenced else ("Fence is full" if fence_full else "Put in fence")
	_fence_button.disabled = not fenced and fence_full

	var treat_cost: int = GameConfig.TREAT["cost"]
	var at_cap := level >= max_level
	_feed_button.text = "Max level" if at_cap else "Feed treat (%dg)" % treat_cost
	_feed_many_button.text = "Feed x10 (%dg)" % (treat_cost * 10)
	_feed_button.disabled = at_cap or PlayerData.gold < treat_cost
	_feed_many_button.disabled = at_cap or PlayerData.gold < treat_cost

	var plan := PlayerData.merge_plan(_selected_id)
	if int(plan["next_stars"]) > GameConfig.MAX_STARS:
		_merge_label.text = "Max stars reached."
		_merge_button.text = "Star-up"
		_merge_button.disabled = true
	else:
		var next := int(plan["next_stars"])
		var header := "Star-up to %d★ (max Lv %d, size %s)" % [next, GameConfig.STARS[next]["max_level"], plan["result_size"]]
		if plan["ok"]:
			var uses := "%d duplicate %s" % [plan["fodder"].size(), "copy" if plan["fodder"].size() == 1 else "copies"]
			if int(plan["shards"]) > 0:
				uses += " + %d %s Star Shard%s" % [plan["shards"], pet["rarity"], "" if int(plan["shards"]) == 1 else "s"]
			_merge_label.text = "%s\nUses %s." % [header, uses]
		elif int(plan["copies_needed"]) > 0:
			_merge_label.text = "%s\nNeeds %d %s %s (up to %d can be %s Star Shards).\nYou have %d %s, %d %s.\n%s" % [
				header, plan["copies_needed"], pet["species"], "copy" if int(plan["copies_needed"]) == 1 else "copies",
				plan["shard_limit"], pet["rarity"],
				plan["copies_owned"], "copy" if int(plan["copies_owned"]) == 1 else "copies",
				plan["shards_owned"], "shard" if int(plan["shards_owned"]) == 1 else "shards", plan["reason"]]
		else:
			_merge_label.text = "%s\n%s" % [header, plan["reason"]]
		_merge_button.text = "Star-up  (%dg)" % plan["cost"]
		_merge_button.disabled = not plan["ok"]

	var release_gold: int = GameConfig.RARITY[pet["rarity"]]["release_gold"]
	_release_button.text = "Tap again to release" if _release_armed else "Release (+%dg, +1 %s Shard)" % [release_gold, pet["rarity"]]


func _on_fence() -> void:
	var pet := PlayerData.find_pet(_selected_id)
	PlayerData.set_fenced(_selected_id, pet["role"] != "fence")


func _feed(count: int) -> void:
	PlayerData.feed_treats(_selected_id, count)


func _on_merge() -> void:
	PlayerData.merge(_selected_id)


func _on_release() -> void:
	if not _release_armed:
		_release_armed = true
		_refresh_detail()
		return
	_release_armed = false
	PlayerData.release_pet(_selected_id)
