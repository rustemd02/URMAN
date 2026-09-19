extends SceneTree

# Read-only placement diagnosis on the currently compiled Act I world.
# No narrative input, transforms, settings or saves are written by this probe.
func _initialize():
	_run.call_deferred()

func _run():
	var demo = load("res://scenes/act1_demo.tscn").instantiate()
	root.add_child(demo)
	for frame in range(120):
		await process_frame
		if demo.find_child("WinterBirch_1_Plant25", true, false) != null:
			break
	var plants: Array = []
	var samples: Array = []
	for item in demo.find_children("*", "Node3D", true, false):
		if item.has_meta("plantPosition") and item.has_meta("plantVariant"):
			var at: Vector3 = item.global_position
			if Vector2(at.x + 32.0, at.z - 3.0).length() < 7.0:
				plants.append({"path": str(item.get_path()), "position": _point(at), "planPosition": _point(item.get_meta("plantPosition")), "variant": item.get_meta("plantVariant")})
	var physical = demo.find_child("Player", true, false) as Node3D
	if physical != null:
		var space = physical.get_world_3d().direct_space_state
		for xz in [Vector2(-35.8, 5.8), Vector2(-35.8, 4.2), Vector2(-36.6, 5.8)]:
			var ray = PhysicsRayQueryParameters3D.create(Vector3(xz.x, 5, xz.y), Vector3(xz.x, -3, xz.y), 1)
			var hit = space.intersect_ray(ray)
			var sample = {"xz": [xz.x, xz.y], "ground": null, "nearContacts": []}
			if not hit.is_empty():
				sample.ground = {"point": _point(hit.position), "normal": _point(hit.normal), "body": str(hit.collider.get_path())}
				var shape = CylinderShape3D.new()
				shape.radius = 0.9
				shape.height = 2.0
				var query = PhysicsShapeQueryParameters3D.new()
				query.shape = shape
				query.collision_mask = 3
				query.transform = Transform3D(Basis.IDENTITY, hit.position + Vector3.UP * 1.10)
				for contact in space.intersect_shape(query, 20):
					var body = contact.collider as CollisionObject3D
					var owner = body.shape_owner_get_owner(body.shape_find_owner(contact.shape))
					sample.nearContacts.append({"body": str(body.get_path()), "shape": str(owner.get_path())})
			samples.append(sample)
	print("YARD_PLANT_PROBE " + JSON.stringify({"plants": plants, "candidates": samples}))
	demo.queue_free()
	for frame in range(8):
		await process_frame
	quit(0)

func _point(at: Vector3) -> Array:
	return [at.x, at.y, at.z]
