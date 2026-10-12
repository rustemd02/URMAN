using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private const string InspectionBookPath = "res://assets/models/photoworlds/family-book-v1.glb";
    private const string InspectionHandsPath = "res://assets/models/photoworlds/book-hands-v1.glb";
    private Button _bookInspectionToggle = null!;
    private VBoxContainer _bookInspectionPanel = null!;
    private SubViewportContainer? _bookInspectionView;
    private SubViewport? _bookInspectionViewport;
    private Node3D? _bookInspectionPivot;
    private Node3D? _bookInspectionModel;
    private Node3D? _bookInspectionHands;
    private object? _bookInspectionSession;

    private void BuildBookInspection()
    {
        _bookInspectionToggle = new Button { Text = "Осмотреть книгу в руках", Visible = false };
        _photoBookSpread.AddChild(_bookInspectionToggle);
        _bookInspectionPanel = new VBoxContainer { Name = "PhysicalBookInspection", Visible = false };
        _photoBookSpread.AddChild(_bookInspectionPanel);
        _bookInspectionToggle.Pressed += () =>
        {
            if (_bookInspectionView is not null) CloseBookInspection();
            else OpenBookInspection();
        };
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
        if (!available || _bookInspectionView is not null
            && !ReferenceEquals(_bookInspectionSession, _bridge?.SessionIdentity)) CloseBookInspection();
    }

    private void OpenBookInspection()
    {
        if (!CanInspectBook() || _bookInspectionView is not null) return;
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
        if (_bookInspectionViewport is not null) _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void RotateBookInspection(float degrees)
    {
        if (_bookInspectionPivot is not null) _bookInspectionPivot.RotateY(Mathf.DegToRad(degrees));
        if (_bookInspectionViewport is not null) _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void CloseBookInspection()
    {
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
        if (GodotObject.IsInstanceValid(_bookInspectionToggle)) _bookInspectionToggle.Text = "Осмотреть книгу в руках";
    }
}
