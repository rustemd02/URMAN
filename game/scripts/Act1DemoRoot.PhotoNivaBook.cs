using Godot;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private Node3D? _photoNivaLapBook;

    private void BuildPhotoNivaLapBook()
    {
        ReleasePhotoNivaLapBook();
        const string bookPath = "res://assets/models/photoworlds/family-book-v1.glb";
        const string handsPath = "res://assets/models/photoworlds/book-hands-v1.glb";
        const string printPath = "res://assets/textures/photobook/p-forest-front-v2.png";
        if (_rideNiva is null || ResourceLoader.Load<PackedScene>(bookPath) is not { } bookScene
            || ResourceLoader.Load<Texture2D>(printPath) is not { } print) return;

        var book = bookScene.Instantiate<Node3D>();
        var insert = book.FindChildren("AlbumInsert", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().FirstOrDefault();
        var animations = book.FindChildren("*", nameof(AnimationPlayer), true, false)
            .OfType<AnimationPlayer>().ToArray();
        if (insert is null || !animations.Any(player => player.HasAnimation("open")))
        {
            book.Free();
            return;
        }

        // Car space, not camera space: the open volume stays on the passenger's
        // lap when he looks up. This instance grants no book or photo progress.
        var lap = new Node3D
        {
            Name = "NivaOpenFamilyBook",
            Position = new(.52f, 1.10f, -.12f)
        };
        lap.SetMeta("presentationOnly", true);
        lap.AddChild(book);
        _rideNiva.AddChild(lap);
        _photoNivaLapBook = lap;
        SetPhotoNivaBookPose(book, "open");

        // Match the existing 150 x 100 mm AlbumInsert. Its top is local
        // Y=0.000325; the print stays below the four physical corner pockets.
        insert.AddChild(new MeshInstance3D
        {
            Name = "NivaForestPrint",
            Position = new(0, .00040f, 0),
            RotationDegrees = new(-90f, 0, 0),
            Mesh = new QuadMesh { Size = new(.1494f, .0994f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = print,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            }
        });

        if (ResourceLoader.Load<PackedScene>(handsPath) is { } handsScene)
        {
            var hands = handsScene.Instantiate<Node3D>();
            lap.AddChild(hands);
            SetPhotoNivaBookPose(hands, "hold_open");
        }
        foreach (var mesh in lap.FindChildren("*", nameof(GeometryInstance3D), true, false)
                     .OfType<GeometryInstance3D>())
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private static void SetPhotoNivaBookPose(Node3D model, string clip)
    {
        foreach (var animation in model.FindChildren("*", nameof(AnimationPlayer), true, false)
                     .OfType<AnimationPlayer>())
        {
            animation.Autoplay = string.Empty;
            animation.Stop();
            if (!animation.HasAnimation(clip)) continue;
            animation.Play(clip);
            animation.Seek(animation.GetAnimation(clip).Length, update: true);
            animation.Pause();
        }
    }

    private void RefreshPhotoNivaLapBook()
    {
        if (_photoNivaLapBook is not { } book || !IsInstanceValid(book)) return;
        book.Visible = !MainMenuVisible && _pauseMenu?.IsOpen != true && !_menuBusy
            && _photoNivaPlayer?.ModalOpen == false
            && GetTree().GetFirstNodeInGroup("settings_ui") is not SettingsUi { IsOpen: true };
    }

    private void ReleasePhotoNivaLapBook()
    {
        if (_photoNivaLapBook is { } book && IsInstanceValid(book))
        {
            book.Visible = false;
            book.QueueFree();
        }
        _photoNivaLapBook = null;
    }
}
