extends CanvasLayer
## In-game HUD built in code: stats, joystick, action buttons, toasts, and
## the starter-skill, pets, skills and upgrades panels.

signal train_pressed

var player: Player
var area_name := "Lobby"

var _cp_label: Label
var _gold_label: Label
var _train_label: Label
var _train_bar: ProgressBar
var _hp_bar: ProgressBar
var _area_label: Label
var _toast_label: Label
var _toasts: Array = []
var _toast_time := 0.0
var _joystick: MoveJoystick
var _attack_button: TouchButton
var _skill_buttons: Array[TouchButton] = []
var _train_button: Button
var _starter_panel: PanelContainer
var _pets_panel: PetsPanel
var _upgrades_panel: UpgradesPanel
var _skills_panel: PanelContainer
var _skills_list: VBoxContainer


func _ready() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var theme := Theme.new()
	theme.default_font_size = 20
	root.theme = theme
	add_child(root)

	_build_stats(root)
	_build_top_buttons(root)
	_build_controls(root)
	_build_toast(root)
	_pets_panel = PetsPanel.new()
	root.add_child(_pets_panel)
	_upgrades_panel = UpgradesPanel.new()
	root.add_child(_upgrades_panel)
	_skills_panel = _build_skills_panel(root)
	_starter_panel = _build_starter_panel(root)
	_starter_panel.visible = not PlayerData.has_starter()

	PlayerData.changed.connect(_refresh)
	PlayerData.notify.connect(toast)
	_refresh()
	if PlayerData.offline_gold > 0:
		toast("While you were away, your pets earned %d gold!" % PlayerData.offline_gold, Color(1, 0.85, 0.3))
		PlayerData.offline_gold = 0


func _process(delta: float) -> void:
	player.joystick_vector = _joystick.vector
	if _attack_button.held:
		player.basic_attack()
	for i in _skill_buttons.size():
		if _skill_buttons[i].held:
			player.use_skill(i)

	_hp_bar.max_value = PlayerData.max_hp()
	_hp_bar.value = player.hp
	_area_label.text = area_name
	_attack_button.cooldown_ratio = player.cooldown_ratio("basic", GameConfig.BASIC_ATTACK["cooldown"])
	for i in _skill_buttons.size():
		if i < PlayerData.equipped_skills.size():
			var skill_id: String = PlayerData.equipped_skills[i]
			_skill_buttons[i].cooldown_ratio = player.cooldown_ratio(skill_id, GameConfig.SKILLS[skill_id]["cooldown"])

	if _toast_time > 0.0:
		_toast_time -= delta
		if _toast_time <= 0.0:
			_show_next_toast()


func set_training_available(available: bool) -> void:
	_train_button.visible = available


func toast(text: String, color: Color = Color.WHITE) -> void:
	_toasts.append([text, color])
	if _toast_time <= 0.0:
		_show_next_toast()


# --- Builders -----------------------------------------------------------------

func _build_stats(root: Control) -> void:
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", UiKit.panel_style(Color(0.4, 0.4, 0.4)))
	UiKit.place(panel, Vector2.ZERO, Vector2(16, 16), Vector2(280, 0))
	root.add_child(panel)
	var box := VBoxContainer.new()
	panel.add_child(box)
	_area_label = Label.new()
	_area_label.add_theme_color_override("font_color", Color(0.9, 0.75, 0.5))
	box.add_child(_area_label)
	_cp_label = Label.new()
	box.add_child(_cp_label)
	_gold_label = Label.new()
	_gold_label.add_theme_color_override("font_color", Color(1, 0.85, 0.3))
	box.add_child(_gold_label)
	_hp_bar = ProgressBar.new()
	_hp_bar.show_percentage = false
	_hp_bar.custom_minimum_size = Vector2(0, 14)
	_hp_bar.modulate = Color(1, 0.45, 0.45)
	box.add_child(_hp_bar)
	_train_label = Label.new()
	_train_label.add_theme_font_size_override("font_size", 16)
	box.add_child(_train_label)
	_train_bar = ProgressBar.new()
	_train_bar.show_percentage = false
	_train_bar.custom_minimum_size = Vector2(0, 10)
	box.add_child(_train_bar)


func _build_top_buttons(root: Control) -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	UiKit.place(row, Vector2(1, 0), Vector2(-406, 16), Vector2(390, 56))
	root.add_child(row)
	for entry in [["Pets", func(): _open(_pets_panel)], ["Skills", func(): _open(_skills_panel)],
			["Upgrades", func(): _open(_upgrades_panel)]]:
		var button := Button.new()
		button.text = entry[0]
		button.custom_minimum_size = Vector2(123, 56)
		button.pressed.connect(entry[1])
		row.add_child(button)


func _build_controls(root: Control) -> void:
	_joystick = MoveJoystick.new()
	UiKit.place(_joystick, Vector2(0, 1), Vector2(48, -268), Vector2(220, 220))
	root.add_child(_joystick)

	_attack_button = TouchButton.new()
	_attack_button.label = "Attack"
	_attack_button.color = Color(0.95, 0.95, 0.95)
	UiKit.place(_attack_button, Vector2(1, 1), Vector2(-188, -188), Vector2(140, 140))
	root.add_child(_attack_button)

	# Skill buttons sit in an arc around the attack button.
	for offset in [Vector2(-310, -140), Vector2(-200, -300)]:
		var button := TouchButton.new()
		UiKit.place(button, Vector2(1, 1), offset, Vector2(100, 100))
		root.add_child(button)
		_skill_buttons.append(button)

	_train_button = Button.new()
	_train_button.text = "TRAIN  (tap!)"
	_train_button.add_theme_font_size_override("font_size", 28)
	UiKit.place(_train_button, Vector2(0.5, 1), Vector2(-130, -110), Vector2(260, 80))
	_train_button.visible = false
	_train_button.pressed.connect(func(): train_pressed.emit())
	root.add_child(_train_button)


func _build_toast(root: Control) -> void:
	_toast_label = Label.new()
	_toast_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_toast_label.add_theme_font_size_override("font_size", 26)
	_toast_label.add_theme_color_override("font_outline_color", Color.BLACK)
	_toast_label.add_theme_constant_override("outline_size", 8)
	UiKit.place(_toast_label, Vector2(0.5, 0), Vector2(-400, 90), Vector2(800, 40))
	root.add_child(_toast_label)


func _build_starter_panel(root: Control) -> PanelContainer:
	var panel := PanelContainer.new()
	root.add_child(panel)
	var box: VBoxContainer = UiKit.make_modal(panel, "Choose your starter skill", Vector2(720, 380), false)[0]
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 12)
	grid.add_theme_constant_override("v_separation", 12)
	box.add_child(grid)
	for skill_id in GameConfig.STARTER_SKILLS:
		var skill: Dictionary = GameConfig.SKILLS[skill_id]
		var button := Button.new()
		button.text = "%s\n%s" % [skill["name"], skill["desc"]]
		button.custom_minimum_size = Vector2(340, 130)
		button.add_theme_color_override("font_color", skill["color"])
		button.pressed.connect(func():
			PlayerData.choose_starter(skill_id)
			panel.visible = false
			toast("You learned %s!" % skill["name"], skill["color"]))
		grid.add_child(button)
	panel.visible = true
	return panel


func _build_skills_panel(root: Control) -> PanelContainer:
	var panel := PanelContainer.new()
	root.add_child(panel)
	var box: VBoxContainer = UiKit.make_modal(panel, "Skills", Vector2(560, 460))[0]
	_skills_list = VBoxContainer.new()
	_skills_list.add_theme_constant_override("separation", 8)
	box.add_child(_skills_list)
	return panel


func _open(panel: Control) -> void:
	if _starter_panel.visible:
		return
	for each in [_pets_panel, _skills_panel, _upgrades_panel]:
		each.visible = each == panel
	_refresh()


# --- Refresh ------------------------------------------------------------------

func _refresh() -> void:
	_cp_label.text = "CP  %d" % PlayerData.combat_power()
	var income := PlayerData.income_per_min()
	_gold_label.text = "Gold  %d" % PlayerData.gold + ("   (+%.1f/min)" % income if income > 0.0 else "")
	var needed := GameConfig.training_xp_needed(PlayerData.training_level)
	_train_label.text = "Training Lv %d   (%d / %d)" % [PlayerData.training_level, PlayerData.training_xp, needed]
	_train_bar.max_value = needed
	_train_bar.value = PlayerData.training_xp

	for i in _skill_buttons.size():
		var button := _skill_buttons[i]
		button.visible = i < PlayerData.equipped_skills.size()
		if button.visible:
			var skill: Dictionary = GameConfig.SKILLS[PlayerData.equipped_skills[i]]
			button.label = skill["short"]
			button.color = skill["color"]

	if _skills_panel.visible:
		_refresh_skills()


func _refresh_skills() -> void:
	for child in _skills_list.get_children():
		child.queue_free()
	var slots := Label.new()
	slots.text = "Skill slots: %d / %d   (buy more in Upgrades)" % [PlayerData.equipped_skills.size(), PlayerData.skill_slots()]
	slots.add_theme_font_size_override("font_size", 16)
	_skills_list.add_child(slots)
	for skill_id in GameConfig.SKILLS:
		var skill: Dictionary = GameConfig.SKILLS[skill_id]
		var row := HBoxContainer.new()
		var label := Label.new()
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var owned := PlayerData.owned_skills.has(skill_id)
		label.text = "%s — %s" % [skill["name"], skill["desc"]] if owned else "???  (find in skill boxes)"
		label.add_theme_color_override("font_color", skill["color"] if owned else Color(0.5, 0.5, 0.5))
		row.add_child(label)
		if owned:
			var button := Button.new()
			button.text = "Equipped" if PlayerData.equipped_skills.has(skill_id) else "Equip"
			button.custom_minimum_size = Vector2(120, 48)
			button.pressed.connect(func(): PlayerData.toggle_equip(skill_id))
			row.add_child(button)
		_skills_list.add_child(row)


func _show_next_toast() -> void:
	if _toasts.is_empty():
		_toast_label.text = ""
		return
	var next: Array = _toasts.pop_front()
	_toast_label.text = next[0]
	_toast_label.add_theme_color_override("font_color", next[1])
	_toast_time = 2.2
