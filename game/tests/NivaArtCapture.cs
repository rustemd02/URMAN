using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Art-review frames of the babay Niva exactly as VehicleVisualFactory builds
/// it, on neutral winter ground under a daylight sun: four exterior views, the
/// driver's view of the dashboard and a close look at the radio. Presentation
/// evidence only; it proves no driving, collision or radio behaviour.
/// Run windowed or with --display-driver macos; URMAN_NIVA_CAPTURE_DIR must be
/// an existing absolute directory.
/// </summary>
public partial class NivaArtCapture : Node
{
    public override async void _Ready()
    {
        try
        {
            var output = System.Environment.GetEnvironmentVariable("URMAN_NIVA_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output) || !System.IO.Path.IsPathFullyQualified(output)
                || !System.IO.Directory.Exists(output))
                throw new InvalidOperationException("URMAN_NIVA_CAPTURE_DIR must be an existing absolute directory.");
            if (RenderingServer.GetRenderingDevice() is null)
                throw new InvalidOperationException("Niva art capture requires a rendering device.");

            var definition = VehicleDefinition.Load(VehicleFleet.DefinitionPath)
                .First(item => item.Kind == VehicleKind.Niva);
            var viewport = new SubViewport
            {
                Size = new Vector2I(1600, 900), OwnWorld3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X
            };
            AddChild(viewport);
            var sky = new ProceduralSkyMaterial
            {
                SkyTopColor = new Color("8fa6bd"), SkyHorizonColor = new Color("dfe6ea"),
                GroundBottomColor = new Color("cfd6da"), GroundHorizonColor = new Color("dfe6ea")
            };
            var environment = new WorldEnvironment
            {
                Environment = new global::Godot.Environment
                {
                    BackgroundMode = global::Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = sky },
                    AmbientLightSource = global::Godot.Environment.AmbientSource.Sky, AmbientLightEnergy = .9f,
                    TonemapMode = global::Godot.Environment.ToneMapper.Filmic, SsaoEnabled = true
                }
            };
            viewport.AddChild(environment);
            var sun = new DirectionalLight3D { LightEnergy = 1.25f, ShadowEnabled = true, LightColor = new Color("fff4e6") };
            viewport.AddChild(sun);
            sun.RotationDegrees = new Vector3(-38, 35, 0);
            viewport.AddChild(new MeshInstance3D
            {
                Name = "Snow", Mesh = new PlaneMesh { Size = new Vector2(40, 40) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("eef2f4"), Roughness = .95f }
            });

            var visual = VehicleVisualFactory.Build(definition);
            viewport.AddChild(visual.Root);
            // Imported wheel extents relative to each pivot: the physical wheel
            // is r .345 x half-width .095, and the vehicle smoke holds every vertex to it.
            foreach (var wheel in visual.Wheels)
            {
                var mesh = wheel.GetNode<MeshInstance3D>("RoadWheelMesh");
                var relative = wheel.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
                var radius = 0f; var halfWidth = 0f;
                for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                    foreach (var vertex in mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    {
                        var p = relative * vertex;
                        radius = Math.Max(radius, new Vector2(p.Y, p.Z).Length());
                        halfWidth = Math.Max(halfWidth, Math.Abs(p.X));
                    }
                GD.Print($"niva-art-capture: wheel {wheel.Name} at {wheel.Position} radius={radius:0.0000} halfWidth={halfWidth:0.0000} meshOffset={relative.Origin}");
            }
            var camera = new Camera3D { Current = true, Fov = 50 };
            viewport.AddChild(camera);

            var views = new (string Name, Vector3 Eye, Vector3 Target, float Fov)[]
            {
                ("front-three-quarter", new(-3.6f, 1.55f, -4.4f), new(0, .85f, -.2f), 45),
                ("full-side", new(-6.2f, 1.25f, 0.1f), new(0, .85f, .1f), 40),
                ("front", new(0, 1.05f, -6.0f), new(0, .9f, 0), 34),
                ("rear-three-quarter", new(3.8f, 1.6f, 4.6f), new(0, .85f, .3f), 45),
                ("front-arch-close", new(-2.3f, .75f, -2.3f), new(-.8f, .55f, -1.18f), 40),
                ("driver-dashboard", new(-.40f, 1.38f, .22f), new(-.05f, 1.02f, -.45f), 70),
                ("radio-close", new(.02f, 1.22f, -.02f), new(.10f, 1.0f, -.38f), 38),
                ("driver-eye", new(-.40f, 1.40f, .12f), new(-.30f, 1.18f, -1.2f), 75),
                ("charm-close", new(-.12f, 1.36f, -.08f), new(0, 1.28f, -.35f), 40),
                ("cabin-seats", new(.50f, 1.45f, -.18f), new(-.30f, .80f, .45f), 75),
                ("floor-mats", new(0, 1.45f, .15f), new(-.30f, .52f, -.40f), 70),
                ("rear-cabin", new(.35f, 1.40f, -.30f), new(0, .85f, 1.30f), 70),
                ("low-side", new(-4.2f, .45f, .4f), new(0, .5f, .2f), 45),
                ("roof-high", new(-3.4f, 3.3f, -3.0f), new(0, 1.2f, .1f), 45)
            };
            foreach (var view in views)
            {
                camera.Fov = view.Fov;
                camera.Position = view.Eye;
                camera.LookAt(view.Target, Vector3.Up);
                for (var frame = 0; frame < 12; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                using var image = viewport.GetTexture().GetImage();
                if (image is null || image.IsEmpty()) throw new InvalidOperationException("Empty frame: " + view.Name);
                var path = System.IO.Path.Combine(output, $"niva_{view.Name}.png");
                if (image.SavePng(path) != Error.Ok) throw new System.IO.IOException("Cannot save " + path);
                GD.Print($"niva-art-capture: {view.Name} -> {path}");
            }
            // Detach the sky before freeing the viewport: a live Sky owned only by a
            // freed world is reported as a leaked reference at exit.
            environment.Environment = null;
            await GodotSmokeCleanup.ReleaseAsync(viewport);
            GD.Print($"niva-art-capture: PASS {views.Length} presentation frames");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("niva-art-capture: " + exception.Message);
            GetTree().Quit(1);
        }
    }
}
