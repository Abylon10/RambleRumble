class_name FenceDisplay
extends Node2D
## Shows fenced pets wandering inside the Lobby fence, drawn at their size,
## with occasional "+gold" pops so passive income is visible.

const PET_SPEED := 28.0
const COIN_POP_INTERVAL := 6.0

var area := Rect2()

## pet id -> {"pos", "target", "pet"}
var _walkers := {}
var _seen_version := -1
var _coin_timer := COIN_POP_INTERVAL


func _process(delta: float) -> void:
	if _seen_version != PlayerData.pets_version:
		_seen_version = PlayerData.pets_version
		_sync()
	var inner := area.grow(-24)
	for walker in _walkers.values():
		var to_target: Vector2 = walker["target"] - walker["pos"]
		if to_target.length() < 4.0:
			walker["target"] = Vector2(randf_range(inner.position.x, inner.end.x), randf_range(inner.position.y, inner.end.y))
		else:
			walker["pos"] += to_target.normalized() * PET_SPEED * delta
	_coin_timer -= delta
	if _coin_timer <= 0.0 and not _walkers.is_empty():
		_coin_timer = COIN_POP_INTERVAL
		var walker: Dictionary = _walkers.values().pick_random()
		var per_pop := PlayerData.pet_income_per_min(walker["pet"]) * COIN_POP_INTERVAL / 60.0 * _walkers.size()
		Fx.spawn(self, walker["pos"] + Vector2(0, -26), {"kind": "text", "text": "+%.1f" % per_pop, "color": Color(1, 0.85, 0.3), "duration": 1.0})
	queue_redraw()


func _sync() -> void:
	var fenced := PlayerData.fenced_pets()
	var ids := {}
	for pet in fenced:
		var id := int(pet["id"])
		ids[id] = true
		if _walkers.has(id):
			_walkers[id]["pet"] = pet
		else:
			var start := area.get_center() + Vector2(randf_range(-60, 60), randf_range(-40, 40))
			_walkers[id] = {"pos": start, "target": start, "pet": pet}
	for id in _walkers.keys():
		if not ids.has(id):
			_walkers.erase(id)


func _draw() -> void:
	var font := ThemeDB.fallback_font
	# Draw back-to-front so lower pets overlap higher ones.
	var ordered := _walkers.values()
	ordered.sort_custom(func(a, b): return a["pos"].y < b["pos"].y)
	for walker in ordered:
		var pet: Dictionary = walker["pet"]
		var species: Dictionary = GameConfig.SPECIES[pet["species"]]
		var radius: float = float(species["radius"]) * 0.8 * float(GameConfig.size_data(pet["size"])["scale"])
		var pos: Vector2 = walker["pos"]
		draw_circle(pos + Vector2(0, radius * 0.8), radius * 0.9, Color(0, 0, 0, 0.18))
		draw_circle(pos, radius, species["color"])
		draw_arc(pos, radius, 0.0, TAU, 24, GameConfig.rarity_color(pet["rarity"]), 2.0)
		draw_circle(pos + Vector2(-radius * 0.3, -radius * 0.2), 2.5, Color.BLACK)
		draw_circle(pos + Vector2(radius * 0.3, -radius * 0.2), 2.5, Color.BLACK)
		draw_string(font, pos + Vector2(-40, -radius - 6), "★".repeat(int(pet["stars"])), HORIZONTAL_ALIGNMENT_CENTER, 80, 12, Color(1, 0.85, 0.3))
