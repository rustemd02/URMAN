extends "./yard_photo_probe_13.gd"

# Bounded diagnostic search on the unchanged compiled layout. No transforms or
# gameplay state are written. Reports candidates for later real input testing.
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
	var target = demo.find_child("Observation_observe-photo-yard", true, false) as Node3D
	var shape = target.find_children("*", "CollisionShape3D", true, false)[0] as CollisionShape3D
	var bounds = AABB(-shape.shape.size * 0.5, shape.shape.size)
	var exclude: Array[RID] = [player.get_rid()]
	for body in demo.find_children("*", "CollisionObject3D", true, false):
		var script = body.get_script()
		if script != null and script.resource_path.ends_with("/InteractionTarget.cs"):
			exclude.append(body.get_rid())
	var landmarks = target.get_meta("observationLandmarks")
	var center: Vector3 = landmarks[1]
	var front = target.global_basis.z.normalized()
	var side = target.global_basis.x.normalized()
	var result = {"candidates": [], "best_rejected": [], "tested": 0}
	var aspect = camera.get_viewport().get_visible_rect().size.aspect()
	var tangent = tan(deg_to_rad(camera.fov) * 0.5)
	var hw = tangent if camera.keep_aspect == Camera3D.KEEP_WIDTH else tangent * aspect
	var hh = tangent / aspect if camera.keep_aspect == Camera3D.KEEP_WIDTH else tangent
	for distance in [1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0, 6.0]:
		for lateral in [-6.0, -5.0, -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0]:
			if Vector2(distance, lateral).length() >= 7.2:
				continue
			var at = center + front * distance + side * lateral
			var sample = _standing_sample(space, player, Vector2(at.x, at.z))
			result.tested += 1
			if sample.ground == null or not sample.standing_contacts.is_empty():
				continue
			var ground: Array = sample.ground.point
			var eye = Vector3(ground[0], ground[1] + 1.7, ground[2])
			var reference: Vector3 = target.get_meta("observationLookAt") - eye
			var base_angle = atan2(reference.x, -reference.z)
			var yaw_min = INF
			var yaw_max = -INF
			var pitch_min = INF
			var pitch_max = -INF
			var visible_count = 0
			for point in landmarks:
				var offset: Vector3 = point - eye
				var yaw = base_angle + wrapf(atan2(offset.x, -offset.z) - base_angle, -PI, PI)
				var pitch = atan2(offset.y, Vector2(offset.x, offset.z).length())
				yaw_min = minf(yaw_min, yaw); yaw_max = maxf(yaw_max, yaw)
				pitch_min = minf(pitch_min, pitch); pitch_max = maxf(pitch_max, pitch)
				var hit = space.intersect_ray(PhysicsRayQueryParameters3D.create(eye, point, 3, exclude))
				if hit.is_empty() or hit.position.distance_to(point) < 0.30:
					visible_count += 1
			var yaw = (yaw_min + yaw_max) * 0.5
			var pitch = (pitch_min + pitch_max) * 0.5
			var direction = Vector3(sin(yaw) * cos(pitch), sin(pitch), -cos(yaw) * cos(pitch))
			var basis = Basis.looking_at(direction)
			var framed_count = 0
			for point in landmarks:
				var local = basis.inverse() * (point - eye)
				if local.z < -0.05 and absf(local.x / -local.z) <= hw and absf(local.y / -local.z) <= hh:
					framed_count += 1
			var reach = camera.get_node("InteractionRay").target_position.length()
			var proxy = bounds.intersects_segment(shape.to_local(eye), shape.to_local(eye + direction * reach))
			var record = {"xz": sample.xz, "eye": _point(eye), "direction": _point(direction), "yawDegrees": rad_to_deg(yaw), "pitchDegrees": rad_to_deg(pitch), "visible": visible_count, "framed": framed_count, "rayInProxy": proxy != null, "reach": reach}
			if visible_count == 5 and framed_count == 5 and proxy != null:
				result.candidates.append(record)
			elif visible_count >= 4 and framed_count == 5:
				result.best_rejected.append(record)
	print("PHOTO_VIEW_PROBE " + JSON.stringify(result))
	demo.queue_free()
	for frame in range(8):
		await process_frame
	quit(0)
