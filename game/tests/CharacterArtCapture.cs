using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Art-review frames of Act I people exactly as GeneratedCharacterKitDressing
/// attaches them, on winter ground in daylight, playing their Idle clip: a
/// line-up, then a full-length and a conversation-distance frame per person.
/// Presentation evidence only. Run windowed or with --display-driver macos;
/// URMAN_CHARACTER_CAPTURE_DIR must be an existing absolute directory.
/// </summary>
public partial class CharacterArtCapture : Node
{
    private static readonly (string Id, string Prefix)[] People =
    [
        ("mansur", "Mansur"), ("gulsina", "Gulsina"), ("naila", "Naila"), ("alsu", "Alsu")
    ];

    public override async void _Ready()
    {
        try
        {
            var output = System.Environment.GetEnvironmentVariable("URMAN_CHARACTER_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output) || !System.IO.Path.IsPathFullyQualified(output)
                || !System.IO.Directory.Exists(output))
                throw new InvalidOperationException("URMAN_CHARACTER_CAPTURE_DIR must be an existing absolute directory.");
            if (RenderingServer.GetRenderingDevice() is null)
                throw new InvalidOperationException("Character art capture requires a rendering device.");

            var viewport = new SubViewport
            {
                Size = new Vector2I(1600, 900), OwnWorld3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X
            };
            AddChild(viewport);
            var environment = new WorldEnvironment
            {
                Environment = new global::Godot.Environment
                {
                    BackgroundMode = global::Godot.Environment.BGMode.Sky,
                    Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial
                    {
                        SkyTopColor = new Color("8fa6bd"), SkyHorizonColor = new Color("dfe6ea"),
                        GroundBottomColor = new Color("cfd6da"), GroundHorizonColor = new Color("dfe6ea")
                    } },
                    AmbientLightSource = global::Godot.Environment.AmbientSource.Sky, AmbientLightEnergy = .9f,
                    TonemapMode = global::Godot.Environment.ToneMapper.Filmic, SsaoEnabled = true
                }
            };
            viewport.AddChild(environment);
            var sun = new DirectionalLight3D { LightEnergy = 1.2f, ShadowEnabled = true, LightColor = new Color("fff4e6") };
            viewport.AddChild(sun);
            sun.RotationDegrees = new Vector3(-40, 25, 0);
            viewport.AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(30, 30) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("eef2f4"), Roughness = .95f }
            });

            var spacing = 1.1f;
            for (var i = 0; i < People.Length; i++)
            {
                var host = new Node3D { Name = "Host_" + People[i].Id };
                viewport.AddChild(host);
                var person = GeneratedCharacterKitDressing.Attach(host, People[i].Id, People[i].Prefix,
                    new Vector3((i - (People.Length - 1) * .5f) * spacing, 0, 0));
                GeneratedCharacterKitDressing.GroundSolesOnAnchor(person);
                GD.Print($"character-art-capture: {People[i].Prefix} kit={person.GetMeta("characterKit")} meshes={person.GetMeta("visibleMeshCount")}");
            }

            var camera = new Camera3D { Current = true };
            viewport.AddChild(camera);
            async Task Shot(string name, Vector3 eye, Vector3 target, float fov)
            {
                camera.Fov = fov;
                camera.Position = eye;
                camera.LookAt(target, Vector3.Up);
                for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                using var image = viewport.GetTexture().GetImage();
                if (image is null || image.IsEmpty()) throw new InvalidOperationException("Empty frame: " + name);
                var path = System.IO.Path.Combine(output, $"people_{name}.png");
                if (image.SavePng(path) != Error.Ok) throw new System.IO.IOException("Cannot save " + path);
                GD.Print($"character-art-capture: {name} -> {path}");
            }

            await Shot("lineup", new(0, 1.2f, 5.2f), new(0, .95f, 0), 40);
            for (var i = 0; i < People.Length; i++)
            {
                var x = (i - (People.Length - 1) * .5f) * spacing;
                await Shot(People[i].Prefix + "_full", new(x, 1.15f, 2.6f), new(x, .9f, 0), 42);
                await Shot(People[i].Prefix + "_talk", new(x + .15f, 1.55f, 1.05f), new(x, 1.5f, 0), 40);
            }
            // Detach the sky before freeing the viewport: a live Sky owned only by a
            // freed world is reported as a leaked reference at exit.
            environment.Environment = null;
            await GodotSmokeCleanup.ReleaseAsync(viewport);
            GD.Print("character-art-capture: PASS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("character-art-capture: " + exception.Message);
            GetTree().Quit(1);
        }
    }
}
