extends SceneTree

# Read-only geometry diagnosis of two rejected local fixtures on the compiled world.
# No player/camera transform, input, narrative flags, settings or save is changed.
func _initialize():
	_run.call_deferred()

func _run():
	var demo = load("res://scenes/act1_demo.tscn").instantiate()
	root.add_child(demo)
	for frame in range(120):
		await process_frame
		if demo.find_child("Observation_observe-photo-yard", true, false) != null:
			break
	for frame in range(8):
		await process_frame
	var player = demo.find_child("Player", true, false) as CharacterBody3D
	var camera = player.get_node("Head/Camera3D") as Camera3D
	var space = player.get_world_3d().direct_space_state
	var exclude: Array[RID] = [player.get_rid()]
	for body in demo.find_children("*", "CollisionObject3D", true, false):
		var script = body.get_script()
		if script != null and script.resource_path.ends_with("/InteractionTarget.cs"):
			exclude.append(body.get_rid())
	var result = {"yard": [], "photo": [], "semantic_ray_exclusions": exclude.size()}
	for xz in [Vector2(-28.2, 3.4), Vector2(-28.2, 4.2), Vector2(-28.2, 1.8)]:
		result.yard.append(_standing_sample(space, player, xz))
	var target = demo.find_child("Observation_observe-photo-yard", true, false) as Node3D
	var reference: Vector3 = target.get_meta("observationReferenceEye")
	var look: Vector3 = target.get_meta("observationLookAt")
	var landmarks = target.get_meta("observationLandmarks")
	var front = target.global_basis.z.normalized()
	var side = target.global_basis.x.normalized()
	result.photo_reference = _point(reference)
	result.photo_look = _point(look)
	result.photo_landmarks = []
	for point in landmarks:
		result.photo_landmarks.append(_point(point))
	# Existing reference and adjacent points on the same exterior side. These are
	# geometric candidates, not a claim that the player walked any of these paths.
	for forward in [0.0, 1.0, 2.0]:
		for lateral in [-1.0, 0.0, 1.0]:
			var candidate = reference + front * forward + side * lateral
			var sample = _standing_sample(space, player, Vector2(candidate.x, candidate.z))
			if sample.ground != null:
				var ground: Array = sample.ground.point
				var eye = Vector3(ground[0], ground[1] + 1.7, ground[2])
				var basis = Basis.looking_at(look - eye)
				var size = camera.get_viewport().get_visible_rect().size
				var aspect = size.x / maxf(size.y, 1.0)
				var tangent = tan(deg_to_rad(camera.fov) * 0.5)
				var half_width = tangent if camera.keep_aspect == Camera3D.KEEP_WIDTH else tangent * aspect
				var half_height = tangent / aspect if camera.keep_aspect == Camera3D.KEEP_WIDTH else tangent
				sample.eye = _point(eye)
				sample.landmarks = []
				for point in landmarks:
					var local = basis.inverse() * (point - eye)
					var framed = local.z < -0.05 and absf(local.x / -local.z) <= half_width and absf(local.y / -local.z) <= half_height
					var ray = PhysicsRayQueryParameters3D.create(eye, point, 3, exclude)
					var hit = space.intersect_ray(ray)
					var visible = hit.is_empty() or hit.position.distance_to(point) < 0.30
					sample.landmarks.append({"point": _point(point), "framed": framed, "visible": visible, "hit": _hit(hit)})
			result.photo.append(sample)
	print("YARD_PHOTO_PROBE " + JSON.stringify(result))
	demo.queue_free()
	for frame in range(8):
		await process_frame
	quit(0)

func _standing_sample(space: PhysicsDirectSpaceState3D, player: CharacterBody3D, xz: Vector2) -> Dictionary:
	var ray = PhysicsRayQueryParameters3D.create(Vector3(xz.x, 6, xz.y), Vector3(xz.x, -4, xz.y), 1, [player.get_rid()])
	var hit = space.intersect_ray(ray)
	var sample = {"xz": [xz.x, xz.y], "ground": null, "standing_contacts": []}
	if hit.is_empty():
		return sample
	sample.ground = {"point": _point(hit.position), "normal": _point(hit.normal), "body": str(hit.collider.get_path())}
	var actual = player.get_node("CollisionShape3D").shape as CapsuleShape3D
	var shape = CapsuleShape3D.new()
	shape.radius = actual.radius
	shape.height = 1.8 - 0.015
	var query = PhysicsShapeQueryParameters3D.new()
	query.shape = shape
	query.collision_mask = player.collision_mask
	query.exclude = [player.get_rid()]
	query.margin = 0.002
	query.transform = Transform3D(Basis.IDENTITY, hit.position + Vector3.UP * (0.06 + 0.91))
	for contact in space.intersect_shape(query, 24):
		var body = contact.collider as CollisionObject3D
		var owner = body.shape_owner_get_owner(body.shape_find_owner(contact.shape))
		sample.standing_contacts.append({"body": str(body.get_path()), "shape": str(owner.get_path())})
	return sample

func _hit(hit: Dictionary):
	if hit.is_empty():
		return null
	var body = hit.collider as CollisionObject3D
	var owner = body.shape_owner_get_owner(body.shape_find_owner(hit.shape))
	return {"point": _point(hit.position), "body": str(body.get_path()), "shape": str(owner.get_path())}

func _point(at: Vector3) -> Array:
	return [at.x, at.y, at.z]
