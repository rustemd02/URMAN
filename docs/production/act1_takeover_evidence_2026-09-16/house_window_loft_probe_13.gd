extends "/Users/unterlantas/Documents/GitHub/URMAN/docs/production/act1_takeover_evidence_2026-09-16/yard_photo_probe_13.gd"

# Read-only queries against the current compiled scene. Reuse the standing
# capsule/contact helpers of the previous targeted probe. No player, camera,
# narrative state, save, material or world geometry is changed.
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
	var exclusions: Array[RID] = [player.get_rid()]
	for body in demo.find_children("*", "CollisionObject3D", true, false):
		var script = body.get_script()
		if script != null and script.resource_path.ends_with("/InteractionTarget.cs"):
			exclusions.append(body.get_rid())
	var facade = demo.find_child("BabaiApproachDwellingFacade", true, false) as Node3D
	var front = facade.global_basis.z.normalized()
	var side = facade.global_basis.x.normalized()
	var windows: Array[Vector3] = []
	var window_sources: Array = []
	for index in range(1, 4):
		var glass = facade.find_child("HeroHouse_Street_Window%d_Glass_LOD0" % index, true, false) as MeshInstance3D
		var bounds = glass.mesh.get_aabb()
		var point = glass.global_transform * (bounds.position + bounds.size * Vector3(0.5, 0.82, 0.5)) + front * 0.06
		windows.append(point)
		window_sources.append({"mesh": str(glass.get_path()), "landmark": _point(point)})
	var centre = (windows[0] + windows[1] + windows[2]) / 3.0
	var viewport_size = camera.get_viewport().get_visible_rect().size
	var aspect = viewport_size.x / maxf(viewport_size.y, 1.0)
	var tangent = tan(deg_to_rad(camera.fov) * 0.5)
	var half_width = tangent if camera.keep_aspect == Camera3D.KEEP_WIDTH else tangent * aspect
	var half_height = tangent / aspect if camera.keep_aspect == Camera3D.KEEP_WIDTH else tangent
	var result = {"scope": "3 street windows only; geometric candidates, not walked routes", "window_sources": window_sources,
		"facade_transform": {"origin": _point(facade.global_position), "front": _point(front), "side": _point(side)},
		"field_of_view": camera.fov, "viewport": [viewport_size.x, viewport_size.y], "candidates": [], "loft_visible_surface": []}
	for distance in [1.2, 1.6, 2.0, 2.4, 2.8, 3.2]:
		for lateral in [-3.2, -2.4, -1.6, -0.8, 0.0, 0.8, 1.6]:
			var candidate = centre + front * distance + side * lateral
			var sample = _standing_sample(space, player, Vector2(candidate.x, candidate.z))
			sample.forward_offset = distance
			sample.side_offset = lateral
			sample.look = _point(centre)
			sample.windows = []
			sample.all_three_framed_visible = false
			if sample.ground != null:
				var ground: Array = sample.ground.point
				var eye = Vector3(ground[0], ground[1] + 1.7, ground[2])
				var view_basis = Basis.looking_at(centre - eye)
				sample.eye = _point(eye)
				sample.rotation_degrees = _point(view_basis.get_euler() * 180.0 / PI)
				sample.centre_distance = eye.distance_to(centre)
				var all_visible = true
				for point in windows:
					var local = view_basis.inverse() * (point - eye)
					var framed = local.z < -0.05 and absf(local.x / -local.z) <= half_width and absf(local.y / -local.z) <= half_height
					var hit = space.intersect_ray(PhysicsRayQueryParameters3D.create(eye, point, 3, exclusions))
					var visible = hit.is_empty() or hit.position.distance_to(point) < 0.30
					all_visible = all_visible and framed and visible
					sample.windows.append({"point": _point(point), "framed": framed, "visible": visible, "hit": _hit(hit)})
				sample.all_three_framed_visible = all_visible
			result.candidates.append(sample)

	# Reconstruct only the recorded B12 camera projection mathematically. This
	# does not move the real camera or produce a substitute screenshot. Pixel
	# rays identify the actual closest visible source triangles in that frame.
	var origin = Vector3(-20.16507, 1.9582253, -1.8028497)
	var view = Basis.from_euler(Vector3(8.502714, 25.892086, 0.0) * PI / 180.0)
	var recorded_tangent = tan(deg_to_rad(75.0) * 0.5)
	var meshes = demo.find_children("*", "MeshInstance3D", true, false)
	for pixel in [Vector2(0.66, 0.56), Vector2(0.69, 0.50), Vector2(0.73, 0.56), Vector2(0.77, 0.59)]:
		var direction = (view * Vector3((pixel.x * 2.0 - 1.0) * recorded_tangent * 1280.0 / 720.0,
			(1.0 - pixel.y * 2.0) * recorded_tangent, -1.0)).normalized()
		result.loft_visible_surface.append({"pixel": [pixel.x, pixel.y], "origin": _point(origin), "direction": _point(direction),
			"recorded_camera": "B12 images-geometry-10b/hero_yard_shed_ground_approach.png; FOV75 keep-height 1280x720",
			"nearest_visible_triangles": _visible_surface_hits(meshes, camera.cull_mask, origin, direction)})
	print("HOUSE_WINDOW_LOFT_PROBE " + JSON.stringify(result))
	demo.queue_free()
	for frame in range(8):
		await process_frame
	quit(0)

func _visible_surface_hits(meshes: Array[Node], cull_mask: int, origin: Vector3, direction: Vector3) -> Array:
	var hits: Array = []
	for node in meshes:
		var mesh = node as MeshInstance3D
		if mesh.mesh == null or not mesh.is_visible_in_tree() or (mesh.layers & cull_mask) == 0:
			continue
		var inverse = mesh.global_transform.affine_inverse()
		var local_origin = inverse * origin
		var local_direction = inverse.basis * direction
		if not _probe_bounds(mesh.mesh.get_aabb(), local_origin, local_direction, 25.0):
			continue
		var centre_distance = origin.distance_to(mesh.global_transform * mesh.mesh.get_aabb().get_center())
		if mesh.visibility_range_end > 0.0 and centre_distance > mesh.visibility_range_end + mesh.visibility_range_end_margin:
			continue
		if mesh.visibility_range_begin > 0.0 and centre_distance < mesh.visibility_range_begin - mesh.visibility_range_begin_margin:
			continue
		var material = mesh.material_override if mesh.material_override != null else mesh.get_active_material(0)
		if material is BaseMaterial3D and material.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED and material.albedo_color.a <= 0.02:
			continue
		var faces = mesh.mesh.get_faces()
		var nearest = 26.0
		for index in range(0, faces.size(), 3):
			var hit = Geometry3D.segment_intersects_triangle(local_origin, local_origin + local_direction * 25.0,
				faces[index], faces[index + 1], faces[index + 2])
			if hit != null:
				var distance = origin.distance_to(mesh.to_global(hit))
				if distance > 0.001 and distance < nearest:
					nearest = distance
		if nearest >= 25.0:
			continue
		var finish = {"resource": str(material.resource_path) if material != null else "none"}
		if material is ShaderMaterial:
			finish.shader = str(material.shader.resource_path)
			finish.snow_coverage = str(material.get_shader_parameter("snow_coverage"))
		if material is BaseMaterial3D:
			finish.albedo = str(material.albedo_color)
			finish.texture = str(material.albedo_texture.resource_path) if material.albedo_texture != null else "none"
		hits.append({"mesh": str(mesh.get_path()), "distance": nearest, "point": _point(origin + direction * nearest), "material": finish})
	hits.sort_custom(func(a, b): return a.distance < b.distance)
	return hits.slice(0, 5)

func _probe_bounds(bounds: AABB, origin: Vector3, direction: Vector3, limit: float) -> bool:
	var near = 0.0
	var far = limit
	for axis in range(3):
		var low = bounds.position[axis] - 0.0001
		var high = bounds.end[axis] + 0.0001
		if absf(direction[axis]) < 0.000001:
			if origin[axis] < low or origin[axis] > high:
				return false
			continue
		var a = (low - origin[axis]) / direction[axis]
		var b = (high - origin[axis]) / direction[axis]
		near = maxf(near, minf(a, b))
		far = minf(far, maxf(a, b))
		if near > far:
			return false
	return true
