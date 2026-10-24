extends SceneTree
## Simulates many kills to check drop rates, size odds and pity.
## Run: godot --headless --path . -s res://tests/drop_sim.gd

const Config = preload("res://scripts/config.gd")
const KILLS := 200000


func _initialize() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 12345
	var failures := 0
	for species_id in Config.SPECIES:
		var rarity: String = Config.SPECIES[species_id]["rarity"]
		var limit: int = Config.RARITY[rarity]["pity"]
		var pity := {}
		var pets := 0
		var boxes := 0
		var longest_drought := 0
		var drought := 0
		for i in KILLS:
			var drop := DropRoller.roll_kill(species_id, pity, rng)
			boxes += 1 if drop["skill_box"] else 0
			if drop["pet"] != null:
				pets += 1
				drought = 0
			else:
				drought += 1
				longest_drought = maxi(longest_drought, drought)
		var expected: float = Config.RARITY[rarity]["pet_chance"]
		var rate := float(pets) / KILLS
		print("%-7s %-9s pet %.4f (base %.3f)  box %.4f  longest drought %d" % [
			species_id, rarity, rate, expected, float(boxes) / KILLS, longest_drought])
		if rate < expected * 0.95:
			failures += 1
			print("  FAIL: pet rate below base chance")
		if limit > 0 and longest_drought >= limit:
			failures += 1
			print("  FAIL: pity limit %d exceeded" % limit)

	var counts := {}
	for i in KILLS:
		var size := DropRoller.roll_size(rng)
		counts[size] = int(counts.get(size, 0)) + 1
	for s in Config.SIZES:
		var rate := float(counts.get(s["name"], 0)) / KILLS
		print("size %-6s %.4f (target %.3f)" % [s["name"], rate, s["chance"]])
		if absf(rate - float(s["chance"])) > 0.01:
			failures += 1
			print("  FAIL: size odds off")

	print("RESULT: %s" % ("PASS" if failures == 0 else "%d FAILURES" % failures))
	quit(failures)
