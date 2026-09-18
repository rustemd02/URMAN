extends SceneTree
# Isolated native renderer diagnostic. It never loads a game scene or a save.
# Run with the existing project/renderer, --script <this file> -- --mode=empty|one|three
# Optional --report=<new absolute JSON path>. Root owns serial engine execution.
# "empty" keeps the same simple receiver/camera/light scene without a probe.
# "three" creates/frees three distinct OwnWorld3D viewports, one probe in each.
# Completion is not a leak PASS: compare allocator warnings after process shutdown.

var _mode: String = "empty"
var _report_path: String = ""
var _samples: Array[Dictionary] = []
var _world_ids: Array[int] = []

func _initialize() -> void:
	for argument: String in OS.get_cmdline_user_args():
		if argument.begins_with("--mode="):
			_mode = argument.trim_prefix("--mode=")
		elif argument.begins_with("--report="):
			_report_path = argument.trim_prefix("--report=")
	call_deferred("_run")

func _run() -> void:
	if _mode not in ["empty", "one", "three"]:
		push_error("probe-lifetime: mode must be empty, one, or three")
		quit(1)
		return
	if not _report_path.is_empty() and FileAccess.file_exists(_report_path):
		push_error("probe-lifetime: refusing to overwrite " + _report_path)
		quit(1)
		return
	if DisplayServer.get_name() == "headless":
		push_error("probe-lifetime: external/not-run; requires the native renderer")
		quit(2)
		return

	# Only this throwaway process changes these in-memory defaults. Newly
	# created worlds need one small atlas slot; no ProjectSettings.save call.
	ProjectSettings.set_setting("rendering/reflections/reflection_atlas/reflection_size", 64)
	ProjectSettings.set_setting("rendering/reflections/reflection_atlas/reflection_count", 1)
	await _draw_frames(4)
	_sample("before", 0)
	var cycles: int = 3 if _mode == "three" else 1
	for cycle: int in range(1, cycles + 1):
		var viewport: SubViewport = _make_viewport(_mode != "empty")
		_world_ids.append(viewport.find_world_3d().get_instance_id())
		await _draw_frames(32)
		_sample("active_after_32_draws", cycle)
		viewport.queue_free()
		viewport = null
		await physics_frame
		await _draw_frames(8)
		_sample("after_world_free", cycle)

	var report: Dictionary = {
		"mode": _mode,
		"engine": Engine.get_version_info(),
		"display": DisplayServer.get_name(),
		"renderer": RenderingServer.get_current_rendering_method(),
		"world_count": cycles,
		"probes_per_world": 0 if _mode == "empty" else 1,
		"diagnostic_atlas": {"size": 64, "count": 1, "settings_saved": false},
		"distinct_world_ids": _world_ids,
		"samples": _samples,
		"status": "controlled scene completed; inspect post-shutdown allocator log",
		"limits": [
			"Public texture-memory totals do not identify private reflection-atlas RIDs.",
			"No assertion equates waiting 32 draws with a public probe-ready signal.",
			"No gameplay state, assets, materials, or bundled engine were changed."
		]
	}
	var serialized: String = JSON.stringify(report, "  ")
	if not _report_path.is_empty():
		var output: FileAccess = FileAccess.open(_report_path, FileAccess.WRITE)
		if output == null:
			push_error("probe-lifetime: cannot write " + _report_path)
			quit(1)
			return
		output.store_string(serialized + "\n")
		output.close()
	print("probe-lifetime-report: " + serialized)
	quit(0)

func _make_viewport(with_probe: bool) -> SubViewport:
	var viewport: SubViewport = SubViewport.new()
	viewport.name = "IsolatedProbeLifetime"
	viewport.size = Vector2i(128, 128)
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	get_root().add_child(viewport)

	var host: Node3D = Node3D.new()
	viewport.add_child(host)
	var environment: WorldEnvironment = WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color(0.20, 0.24, 0.28)
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color(0.7, 0.75, 0.8)
	environment.environment.ambient_light_energy = 0.5
	host.add_child(environment)

	var receiver: MeshInstance3D = MeshInstance3D.new()
	receiver.mesh = BoxMesh.new()
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.albedo_color = Color(0.75, 0.75, 0.75)
	material.metallic = 0.9
	material.roughness = 0.2
	receiver.material_override = material
	host.add_child(receiver)

	var lamp: DirectionalLight3D = DirectionalLight3D.new()
	lamp.rotation_degrees = Vector3(-35, -30, 0)
	lamp.light_energy = 1.0
	lamp.shadow_enabled = false
	host.add_child(lamp)

	var camera: Camera3D = Camera3D.new()
	camera.position = Vector3(0, 0.4, 3)
	camera.current = true
	host.add_child(camera)
	camera.look_at(Vector3.ZERO)

	if with_probe:
		var probe: ReflectionProbe = ReflectionProbe.new()
		probe.position = Vector3(0, 1.6, 0)
		probe.size = Vector3(8, 8, 8)
		probe.max_distance = 6.0
		probe.update_mode = ReflectionProbe.UPDATE_ONCE
		probe.enable_shadows = false
		host.add_child(probe)
	return viewport

func _draw_frames(count: int) -> void:
	for index: int in range(count):
		await process_frame
		await RenderingServer.frame_post_draw

func _sample(phase: String, cycle: int) -> void:
	var sample: Dictionary = {
		"phase": phase,
		"cycle": cycle,
		"texture_memory_bytes": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TEXTURE_MEM_USED),
		"buffer_memory_bytes": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_BUFFER_MEM_USED),
		"object_count": int(Performance.get_monitor(Performance.OBJECT_COUNT)),
		"resource_count": int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT))
	}
	_samples.append(sample)
	print("probe-lifetime-sample: " + JSON.stringify(sample))
