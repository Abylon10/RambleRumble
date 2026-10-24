extends Node
## Phase 2 economy checks: fence, income, offline cap, treats, merging,
## Star Shards, Pet Lab gating, release and upgrades.
## Run: godot --headless --path . res://tests/economy_test.tscn -- --test

var _failures := 0
var data: Node


func _ready() -> void:
	data = get_tree().root.get_node("PlayerData")
	_run()
	data.reset_progress()
	print("RESULT: %s" % ("PASS" if _failures == 0 else "%d FAILURES" % _failures))
	get_tree().quit(_failures)


func _check(ok: bool, what: String) -> void:
	print(("  ok   " if ok else "  FAIL ") + what)
	if not ok:
		_failures += 1


func _add_pet(species: String, size := "Normal") -> int:
	var id: int = data._next_pet_id
	data._next_pet_id += 1
	data.pets.append({"id": id, "species": species, "rarity": GameConfig.SPECIES[species]["rarity"],
		"size": size, "level": 1, "xp": 0, "stars": 1, "role": "none"})
	data.pets_version += 1
	return id


func _run() -> void:
	data.reset_progress()

	# Fence slots and one-role rule.
	var ids: Array[int] = []
	for i in 4:
		ids.append(_add_pet("Slime"))
	for i in 3:
		data.set_fenced(ids[i], true)
	_check(not data.set_fenced(ids[3], true), "4th pet can't enter a 3-slot fence")
	_check(data.fenced_pets().size() == 3, "3 pets fenced")

	# Income: 3 Normal Common pets at Lv1/1★ = 3 gold/min.
	_check(is_equal_approx(data.income_per_min(), 3.0), "income is 3.0/min (got %.2f)" % data.income_per_min())
	data.gold = 0
	data.tick(60.0)
	_check(data.gold == 3, "one minute of ticks pays 3 gold (got %d)" % data.gold)

	# Offline cap: 24h away pays only 8h.
	data.gold = 0
	var earned: int = data.apply_offline(24 * 3600.0)
	_check(earned == 3 * 60 * 8, "offline pays at most 8h (got %d)" % earned)
	_check(data.apply_offline(-500.0) == 0, "negative offline time pays nothing")

	# Treats and the level cap (1★ caps at Lv10).
	data.gold = 100000
	var fed: int = data.feed_treats(ids[3], 1000)
	var pet: Dictionary = data.find_pet(ids[3])
	_check(int(pet["level"]) == 10, "treats level a 1★ pet to its cap of 10 (got %d)" % pet["level"])
	_check(fed < 1000, "feeding stops at the cap (fed %d)" % fed)

	# Merge 1★ -> 2★ needs 1 copy; result takes the largest size.
	data.reset_progress()
	data.gold = 1000000
	var base := _add_pet("Wolf")
	var giant_copy := _add_pet("Wolf", "Giant")
	var plan: Dictionary = data.merge_plan(base)
	_check(plan["ok"] and plan["fodder"] == [giant_copy], "2★ plan uses the one duplicate")
	_check(data.merge(base), "merge to 2★ succeeds")
	pet = data.find_pet(base)
	_check(int(pet["stars"]) == 2 and pet["size"] == "Giant", "result is 2★ and Giant (got %d★ %s)" % [pet["stars"], pet["size"]])
	_check(data.find_pet(giant_copy).is_empty(), "fodder pet consumed")
	_check(data.gold == 1000000 - 500, "2★ merge cost 500 gold")

	# 2★ -> 3★ needs 2 copies; shards may cover 1 of them.
	plan = data.merge_plan(base)
	_check(not plan["ok"], "3★ blocked with no copies or shards (%s)" % plan["reason"])
	data.star_shards["Uncommon"] = 5
	plan = data.merge_plan(base)
	_check(not plan["ok"], "shards alone can't cover more than half the copies")
	_add_pet("Wolf")
	plan = data.merge_plan(base)
	_check(plan["ok"] and plan["shards"] == 1 and plan["fodder"].size() == 1, "1 copy + 1 shard makes 3★")
	data.merge(base)
	_check(int(data.star_shards["Uncommon"]) == 4, "one shard spent")

	# Pet Lab gates 4★.
	for i in 3:
		_add_pet("Wolf")
	plan = data.merge_plan(base)
	_check(not plan["ok"] and String(plan["reason"]).contains("Pet Lab"), "4★ needs a Pet Lab upgrade")
	_check(data.buy_upgrade("pet_lab"), "buy Pet Lab level 1")
	plan = data.merge_plan(base)
	_check(plan["ok"] and int(plan["cost"]) == 9000, "4★ allowed with 10%% discount (cost %d)" % plan["cost"])
	_check(data.merge(base) and int(data.find_pet(base)["stars"]) == 4, "merge to 4★")

	# Release gives gold and a shard.
	var spare := _add_pet("Goblin")
	var gold_before: int = data.gold
	_check(data.release_pet(spare), "release succeeds")
	_check(data.gold == gold_before + 40 and int(data.star_shards["Rare"]) == 1, "release pays 40 gold + 1 Rare shard")

	# Upgrades: costs grow and effects apply.
	data.reset_progress()
	data.gold = 1000000
	_check(data.next_upgrade_cost("fence_slots") == 200, "first fence slot costs 200")
	data.buy_upgrade("fence_slots")
	_check(data.fence_slots() == 4 and data.next_upgrade_cost("fence_slots") == 340, "fence slots 4, next costs 340")
	data.buy_upgrade("skill_slots")
	data.buy_upgrade("skill_slots")
	_check(data.skill_slots() == 4 and data.upgrade_maxed("skill_slots"), "skill slots max at 4")
	_check(not data.buy_upgrade("skill_slots"), "can't buy past max")
	data.buy_upgrade("training_ground")
	var level_before: int = data.training_level
	var xp_before: int = data.training_xp
	data.train(4.0)
	_check(data.training_xp + (data.training_level - level_before) * 100 > xp_before + 4, "Training Ground boosts XP (4 base -> 5)")
	data.gold = 0
	_check(not data.buy_upgrade("fence_slots"), "can't buy without gold")

	# New drops auto-fence while slots are free.
	data.reset_progress()
	data.pity["Rare"] = 24
	data.record_kill("Goblin")
	_check(data.pets.size() == 1 and data.pets[0]["role"] == "fence", "new pet goes into a free fence slot")

	# Save/load keeps the new fields.
	data.upgrades["fence_slots"] = 2
	data.star_shards["Rare"] = 3
	data.save_game()
	data.upgrades.clear()
	data.star_shards.clear()
	data.load_game()
	_check(data.upgrade_level("fence_slots") == 2 and int(data.star_shards["Rare"]) == 3, "upgrades and shards survive save/load")
