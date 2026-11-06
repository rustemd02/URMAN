using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private const string InspectionBookPath = "res://assets/models/photoworlds/family-book-v1.glb";
    private const string InspectionHandsPath = "res://assets/models/photoworlds/book-hands-v1.glb";
    private Button _bookInspectionToggle = null!;
    private Button _bookInspectionGripToggle = null!;
    private Button _photoSceneReferenceButton = null!;
    private VBoxContainer _bookInspectionPanel = null!;
    private SubViewportContainer? _bookInspectionView;
    private SubViewport? _bookInspectionViewport;
    private Node3D? _bookInspectionPivot;
    private Node3D? _bookInspectionModel;
    private Node3D? _bookInspectionHands;
    private object? _bookInspectionSession;
    private MeshInstance3D? _bookInspectionPhotoInsert;
    private int _bookInspectionPhotoSurface = -1;
    private Material? _bookInspectionPhotoOriginalSurfaceMaterial;
    private Material? _bookInspectionPhotoAppliedMaterial;
    private string? _bookInspectionPhotoAppliedPhotoId;
    private string? _bookInspectionPhotoAppliedPageId;

    // The GLB's only AlbumInsert top face is local Y=+0.000325 with normal +Y.
    // Its exported UV rectangle is U[-.0747075006,.0747075006], V[.9502924681,1.0497075319].
    private const string InspectionGardenOverlayShader = """
        shader_type spatial;
        render_mode cull_back, unshaded, depth_draw_opaque;

        uniform sampler2D photo_front : source_color, filter_linear, repeat_disable;
        varying float insert_height;
        varying float insert_normal_y;

        void vertex() {
            insert_height = VERTEX.y;
            insert_normal_y = NORMAL.y;
        }

        void fragment() {
            if (!FRONT_FACING || insert_height < 0.0003245 || insert_normal_y < 0.999) discard;

            vec2 photo_uv = (UV - vec2(-0.0747075006365776, 0.9502924680709839))
                / vec2(0.1494150012731552, 0.0994150638580322);
            if (any(lessThan(photo_uv, vec2(0.0))) || any(greaterThan(photo_uv, vec2(1.0)))) discard;
            ALBEDO = texture(photo_front, photo_uv).rgb;
        }
        """;

    private void BuildBookInspection()
    {
        _bookInspectionToggle = new Button { Text = "Осмотреть книгу в руках", Visible = false };
        _photoBookSpread.AddChild(_bookInspectionToggle);
        _bookInspectionGripToggle = new Button { Text = "Нести книгу", Visible = false };
        _photoBookSpread.AddChild(_bookInspectionGripToggle);
        _photoSceneReferenceButton = new Button
        {
            Text = "Сверять снимок на месте", Visible = false,
            TooltipText = "Оставить снимок рядом с видом на лес. Можно идти и осматриваться."
        };
        _photoBookSpread.AddChild(_photoSceneReferenceButton);
        _photoSceneReferenceButton.Pressed += OpenPhotoSceneReference;
        _bookInspectionPanel = new VBoxContainer { Name = "PhysicalBookInspection", Visible = false };
        _photoBookSpread.AddChild(_bookInspectionPanel);
        _bookInspectionToggle.Pressed += () =>
        {
            if (_bookInspectionView is not null) CloseBookInspection();
            else OpenBookInspection();
        };
        _bookInspectionGripToggle.Pressed += TogglePhotoBookGrip;
    }

    private bool CanInspectBook() => _screen.Visible && _photoBookPage.Visible
        && _tabs.CurrentTab == PhotoBookTabIndex && !_notesLoadInProgress
        && _bridge is { } bridge && bridge.CanInspectReceivedPhotoBook
        && ReferenceEquals(_photoBookSession, bridge.SessionIdentity)
        && GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { VehicleControlled: false }
        && GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { HeldItem: not null }
        && GetTree().GetFirstNodeInGroup("carry_coordinator") is not CarryCoordinator { ActionInProgress: true }
        && ResourceLoader.Exists(InspectionBookPath) && ResourceLoader.Exists(InspectionHandsPath);

    private void RefreshBookInspection()
    {
        var available = CanInspectBook();
        _bookInspectionToggle.Visible = available;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        _photoSceneReferenceButton.Visible = !_notesLoadInProgress && _screen.Visible
            && _photoBookPage.Visible && _tabs.CurrentTab == PhotoBookTabIndex
            && _photoBookSelectedId is { } photoId && _bridge is { } bridge && player is not null
            && ReferenceEquals(_photoBookSession, bridge.SessionIdentity)
            && player.CanShowPhotoSceneReference(bridge, photoId, allowJournalModal: true)
            && PhotoBookFrontTexture(photoId) is not null;
        var canPutAway = _screen.Visible && _photoBookPage.Visible
            && _tabs.CurrentTab == PhotoBookTabIndex && player?.HasPhotoBookGrip == true;
        _bookInspectionGripToggle.Visible = available || canPutAway;
        if (player is not null)
            _bookInspectionGripToggle.Text = player.HasPhotoBookGrip ? "Убрать книгу" : "Нести книгу";
        if (!available || _bookInspectionView is not null
            && !ReferenceEquals(_bookInspectionSession, _bridge?.SessionIdentity))
        {
            CloseBookInspection();
            return;
        }
        RefreshBookInspectionPhoto();
    }

    private async void OpenPhotoSceneReference()
    {
        if (_notesLoadInProgress || !_screen.Visible || !_photoBookPage.Visible
            || _tabs.CurrentTab != PhotoBookTabIndex || _photoBookSelectedId is not { } photoId
            || _bridge is not { } bridge || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(_photoBookSession, session)
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player
            || !player.CanShowPhotoSceneReference(bridge, photoId, allowJournalModal: true)
            || PhotoBookFrontTexture(photoId) is not { } front) return;

        if (!await CloseAsync(session, _photoBookVisibilityGeneration))
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) RefreshBookInspection();
            return;
        }
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()
            || !GodotObject.IsInstanceValid(bridge) || !GodotObject.IsInstanceValid(player)) return;
        CloseBookInspection();
        player.TryShowPhotoSceneReference(bridge, session, photoId, front);
    }

    private async void TogglePhotoBookGrip()
    {
        if (!_screen.Visible || !_photoBookPage.Visible || _tabs.CurrentTab != PhotoBookTabIndex
            || GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return;

        if (player.HasPhotoBookGrip)
        {
            player.TrySetPhotoBookGrip(false);
            RefreshBookInspection();
            return;
        }

        if (!CanInspectBook() || _bridge is not { } bridge
            || bridge.SessionIdentity is not { } session) return;
        if (!player.CanPreparePhotoBookGrip()) return;
        if (!await CloseAsync())
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) RefreshBookInspection();
            return;
        }

        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree()) return;
        CloseBookInspection();
        var carry = GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator;
        if (!IsInsideTree() || !GodotObject.IsInstanceValid(player) || !player.IsInsideTree()
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("player_controller"), player)
            || !GodotObject.IsInstanceValid(bridge)
            || !ReferenceEquals(GetTree().GetFirstNodeInGroup("runtime_bridge"), bridge)
            || !ReferenceEquals(session, bridge.SessionIdentity) || !bridge.CanInspectReceivedPhotoBook
            || player.ModalOpen || player.VehicleControlled || player.IsClimbingLadder
            || carry is { HeldItem: not null } or { ActionInProgress: true }) return;
        player.TrySetPhotoBookGrip(true, session);
    }

    private void OpenBookInspection()
    {
        if (!CanInspectBook() || _bookInspectionView is not null) return;
        _bookInspectionOpenPose = false;
        var bookScene = ResourceLoader.Load<PackedScene>(InspectionBookPath);
        var handsScene = ResourceLoader.Load<PackedScene>(InspectionHandsPath);
        if (bookScene is null || handsScene is null) return;
        _bookInspectionSession = _bridge!.SessionIdentity;
        var controls = new HFlowContainer { Name = "InspectionControls" };
        void Action(string text, System.Action action)
        {
            var button = new Button { Text = text };
            button.Pressed += () => { if (CanInspectBook()) action(); else CloseBookInspection(); };
            controls.AddChild(button);
        }
        Action("Закрытая", () => SetBookInspectionPose(open: false));
        Action("Разворот", () => SetBookInspectionPose(open: true));
        Action("↶", () => RotateBookInspection(-15));
        Action("↷", () => RotateBookInspection(15));
        Action("Закончить осмотр", CloseBookInspection);
        _bookInspectionPanel.AddChild(controls);
        _bookInspectionView = new SubViewportContainer
        {
            Name = "BookAndHands", Stretch = true, CustomMinimumSize = new(0, 320),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _bookInspectionPanel.AddChild(_bookInspectionView);
        var viewport = new SubViewport
        {
            Size = new(800, 480), OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once
        };
        _bookInspectionViewport = viewport;
        _bookInspectionView.AddChild(viewport);
        var world = new Node3D { Name = "InspectionWorld" };
        viewport.AddChild(world);
        world.AddChild(new WorldEnvironment { Environment = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("333b40"),
            AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("d9e3eb"), AmbientLightEnergy = .8f
        } });
        world.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new(-55, -30, 0), LightColor = new Color("fff1dc"), LightEnergy = 1.1f
        });
        var from = new Vector3(-.10f, .38f, .26f);
        var target = new Vector3(-.10f, .015f, 0);
        world.AddChild(new Camera3D
        {
            Current = true, Fov = 70, Near = .01f, Far = 5,
            Transform = new Transform3D(Basis.LookingAt(target - from, Vector3.Up), from)
        });
        _bookInspectionPivot = new Node3D { Name = "BookAndHandsPivot" };
        world.AddChild(_bookInspectionPivot);
        _bookInspectionModel = bookScene.Instantiate<Node3D>();
        _bookInspectionHands = handsScene.Instantiate<Node3D>();
        _bookInspectionPivot.AddChild(_bookInspectionModel);
        _bookInspectionPivot.AddChild(_bookInspectionHands);
        // This is a separate visual world: never borrow the table prop or player
        // skeleton, and never add progress, interaction, collision or carry owners.
        foreach (var animation in _bookInspectionPivot.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>())
        { animation.Autoplay = ""; animation.Stop(); }
        _bookInspectionPanel.Visible = true;
        _bookInspectionToggle.Text = "Скрыть осмотр книги";
        RefreshBookInspectionPhoto();
    }

    private void RefreshBookInspectionPhoto()
    {
        var selected = _photoBookProjection.FirstOrDefault(photo =>
            StringComparer.Ordinal.Equals(photo.PhotoId, _photoBookSelectedId));
        if (_bookInspectionView is null || _bookInspectionModel is null
            || !_bookInspectionOpenPose || !CanInspectBook()
            || !ReferenceEquals(_bookInspectionSession, _bridge?.SessionIdentity)
            || selected is null
            || string.IsNullOrWhiteSpace(selected.PageId)
            || !selected.Mounted)
        {
            ClearBookInspectionPhotoOverride();
            return;
        }

        // The selected projection is acquired-only, and its PageId comes from
        // the validated catalog. Mounted verifies that exact page before the
        // existing front-only resolver feeds the physical insert.
        var frontTexture = PhotoBookFrontTexture(selected.PhotoId);
        if (frontTexture is null)
        {
            ClearBookInspectionPhotoOverride();
            return;
        }

        if (_bookInspectionPhotoInsert is { } currentInsert
            && GodotObject.IsInstanceValid(currentInsert)
            && _bookInspectionPhotoAppliedMaterial is { } currentMaterial
            && _bookInspectionPhotoSurface >= 0
            && StringComparer.Ordinal.Equals(_bookInspectionPhotoAppliedPhotoId, selected.PhotoId)
            && StringComparer.Ordinal.Equals(_bookInspectionPhotoAppliedPageId, selected.PageId)
            && ReferenceEquals(currentInsert.GetSurfaceOverrideMaterial(_bookInspectionPhotoSurface), currentMaterial))
        {
            RefreshBookInspectionPhotoCaption(selected.Title);
            return;
        }

        ClearBookInspectionPhotoOverride();
        var insert = _bookInspectionModel.FindChildren("AlbumInsert", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().FirstOrDefault();
        if (insert?.Mesh is not { } mesh || mesh.GetSurfaceCount() != 1) return;

        const int surface = 0;
        var previousOverride = insert.GetSurfaceOverrideMaterial(surface);
        var sourceMaterial = previousOverride ?? mesh.SurfaceGetMaterial(surface);
        if (sourceMaterial is null || sourceMaterial.NextPass is not null
            || sourceMaterial.Duplicate() is not Material isolatedMaterial) return;

        var overlay = new ShaderMaterial
        {
            Shader = new Shader { Code = InspectionGardenOverlayShader }
        };
        overlay.SetShaderParameter("photo_front", frontTexture);
        isolatedMaterial.NextPass = overlay;
        insert.SetSurfaceOverrideMaterial(surface, isolatedMaterial);
        _bookInspectionPhotoInsert = insert;
        _bookInspectionPhotoSurface = surface;
        _bookInspectionPhotoOriginalSurfaceMaterial = previousOverride;
        _bookInspectionPhotoAppliedMaterial = isolatedMaterial;
        _bookInspectionPhotoAppliedPhotoId = selected.PhotoId;
        _bookInspectionPhotoAppliedPageId = selected.PageId;
        RefreshBookInspectionPhotoCaption(selected.Title);
        if (_bookInspectionViewport is not null)
            _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void ClearBookInspectionPhotoOverride()
    {
        ClearBookInspectionPhotoCaption();
        if (_bookInspectionPhotoInsert is { } insert
            && GodotObject.IsInstanceValid(insert)
            && _bookInspectionPhotoSurface >= 0
            && insert.Mesh is { } mesh
            && _bookInspectionPhotoSurface < mesh.GetSurfaceCount())
        {
            insert.SetSurfaceOverrideMaterial(_bookInspectionPhotoSurface, _bookInspectionPhotoOriginalSurfaceMaterial);
            if (_bookInspectionViewport is not null)
                _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }
        _bookInspectionPhotoInsert = null;
        _bookInspectionPhotoSurface = -1;
        _bookInspectionPhotoOriginalSurfaceMaterial = null;
        _bookInspectionPhotoAppliedMaterial = null;
        _bookInspectionPhotoAppliedPhotoId = null;
        _bookInspectionPhotoAppliedPageId = null;
    }

    private void SetBookInspectionPose(bool open)
    {
        void Pose(Node3D? model, string clip)
        {
            if (model is null) return;
            foreach (var player in model.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>())
            {
                if (!player.HasAnimation(clip)) continue;
                player.Play(clip);
                player.Seek(player.GetAnimation(clip).Length, update: true);
                player.Pause();
            }
        }
        // Explicit still poses also respect reduced motion. No story page or
        // photo state changes when the player examines the book's construction.
        Pose(_bookInspectionModel, open ? "open" : "close");
        Pose(_bookInspectionHands, open ? "hold_open" : "hold_closed");
        _bookInspectionOpenPose = open;
        RefreshBookInspectionPhoto();
        if (_bookInspectionViewport is not null) _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void RotateBookInspection(float degrees)
    {
        if (_bookInspectionPivot is not null) _bookInspectionPivot.RotateY(Mathf.DegToRad(degrees));
        if (_bookInspectionViewport is not null) _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void CloseBookInspection()
    {
        ClearBookInspectionPhotoOverride();
        if (GodotObject.IsInstanceValid(_bookInspectionPanel))
        {
            foreach (var child in _bookInspectionPanel.GetChildren())
            { _bookInspectionPanel.RemoveChild(child); child.QueueFree(); }
            _bookInspectionPanel.Visible = false;
        }
        _bookInspectionView = null;
        _bookInspectionViewport = null;
        _bookInspectionPivot = _bookInspectionModel = _bookInspectionHands = null;
        _bookInspectionSession = null;
        _bookInspectionOpenPose = false;
        if (GodotObject.IsInstanceValid(_bookInspectionToggle)) _bookInspectionToggle.Text = "Осмотреть книгу в руках";
    }
}
