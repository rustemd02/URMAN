using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace Urman.Godot;

/// <summary>The optional fate of the mitten Aidar actually brought home.</summary>
internal partial class HomeMittenPresence : Node3D
{
    private const string HomeScene = "urman.fullgame:scene/pw-home";
    private RuntimeBridge? _bridge;
    private FirstPersonController? _player;
    private InteractionTarget _showTarget = null!;
    private InteractionTarget _hideTarget = null!;
    private Node3D _wool = null!;
    private Transform3D _shownPose;
    private Transform3D _hiddenPose;
    private object? _session;
    private string? _zone;
    private bool _eligible;
    private bool _busy;
    private bool _hinted;

    internal static void Attach(StyleBenchmarkZone house)
    {
        var furniture = house.GetNode<Node3D>("GeneratedHouseInteriorAct1");
        var hearth = furniture.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_HearthTop_LOD0");
        var coat = house.GetNode<Node3D>("Kitchen/KitchenCoat");
        var bounds = PublicBuildingShell.Bounds(house, hearth);
        // The near-left edge clears the existing water vessel on the right.
        var position = new Vector3(bounds.Position.X + .11f, bounds.End.Y + .067f, bounds.GetCenter().Z);
        var localFrom = hearth.ToLocal(house.ToGlobal(position + Vector3.Up));
        var localTo = hearth.ToLocal(house.ToGlobal(position - Vector3.Up * 2));
        var top = float.NegativeInfinity;
        var faces = hearth.Mesh!.GetFaces();
        for (var index = 0; index + 2 < faces.Length; index += 3)
        {
            var hit = Geometry3D.SegmentIntersectsTriangle(localFrom, localTo, faces[index], faces[index + 1], faces[index + 2]);
            if (hit.VariantType != Variant.Type.Nil)
                top = Mathf.Max(top, house.ToLocal(hearth.ToGlobal(hit.AsVector3())).Y);
        }
        if (!float.IsFinite(top)) throw new InvalidOperationException("The home mitten has no real hearth support.");
        position.Y = top + .067f;
        var presence = new HomeMittenPresence
        {
            Name = "HomeBlueMitten",
            _shownPose = new Transform3D(Basis.FromEuler(new Vector3(Mathf.Pi * .5f, 0, 0)), position),
            _hiddenPose = house.GlobalTransform.AffineInverse() * coat.GlobalTransform
                * new Transform3D(Basis.Identity, new Vector3(.12f, -.44f, .07f))
        };
        presence._wool = PrologueDeepForest.AddMitten(presence, Vector3.Zero, Vector3.Zero);
        presence._wool.Visible = false;
        presence._showTarget = presence.Target("Show", position + Vector3.Up * .08f, new(.34f, .20f, .34f),
            "Достать варежку и показать Гөлсинә", show: true);
        presence._hideTarget = presence.Target("Hide", house.ToLocal(coat.ToGlobal(new Vector3(0, -.2f, -.075f))),
            new(.40f, .65f, .10f), "Спрятать варежку за куртку", show: false);
        presence._hideTarget.Transform = house.GlobalTransform.AffineInverse() * coat.GlobalTransform
            * new Transform3D(Basis.Identity, new Vector3(0, -.2f, -.075f));
        house.AddChild(presence);
    }

    private InteractionTarget Target(string name, Vector3 position, Vector3 size, string prompt, bool show)
    {
        var target = new InteractionTarget
        {
            Name = name + "MittenTarget", Position = position, CollisionLayer = 4, CollisionMask = 0,
            InteractionId = "urman.fullgame:local/home-mitten/" + name.ToLowerInvariant(), Prompt = prompt,
            PresentationRepeatAvailable = () => _eligible && !_busy,
            PresentationGate = CanChoose,
            PresentationRepeat = () => _ = ChooseAsync(show)
        };
        target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(target);
        return target;
    }

    public override void _Process(double delta)
    {
        if (_bridge is null)
        {
            if (GetTree().GetFirstNodeInGroup("runtime_bridge") is not RuntimeBridge bridge) return;
            _bridge = bridge;
            _player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
            bridge.RuntimeStateChanged += Apply;
            Apply();
        }
        if (_player is null) _player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        if (_zone != _bridge.CurrentZoneId || !ReferenceEquals(_session, _bridge.SessionIdentity)) Apply();
        if (_hinted || !CanChoose() || _player is not { ModalOpen: false } player) return;
        if (player.InteractionNoticeActive || player.CurrentRemark.Length > 0) return;
        _hinted = true;
        player.ShowRemark("Айдар", "Варежка всё ещё в кармане. Показать әби у печки или убрать за куртку?");
    }

    private bool CanChoose() => _eligible && !_busy && _bridge is { } bridge
        && !bridge.IsPhotoBookProjectionDeferred && bridge.ActiveSceneId == HomeScene
        && bridge.CurrentZoneId == "house_old_pc" && bridge.CanInspectReceivedPhotoBook
        && _player is { ModalOpen: false, HasPhotoBookGrip: false }
        && !MainMenuUi.AnyUndismissed(GetTree());

    private static string Disposition(JsonElement props) =>
        props.ValueKind == JsonValueKind.Object && props.TryGetProperty(W01Controller.PrologueMittenKey, out var record)
        && record.ValueKind == JsonValueKind.Object && record.TryGetProperty("homeDisposition", out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    private void Apply()
    {
        if (!IsInsideTree() || _bridge is not { } bridge || !IsInstanceValid(bridge)) return;
        if (!ReferenceEquals(_session, bridge.SessionIdentity))
        {
            _session = bridge.SessionIdentity;
            _hinted = false;
        }
        _zone = bridge.CurrentZoneId;
        var props = bridge.SelectWorldProps();
        var disposition = Disposition(props);
        var taken = YardMechanism.Flag(props, W01Controller.PrologueMittenKey, "taken");
        var home = _zone == "house_old_pc" && bridge.ActiveSceneId == HomeScene;
        _wool.Visible = home && taken && disposition is ("shown" or "hidden");
        if (_wool.Visible) _wool.Transform = disposition == "shown" ? _shownPose : _hiddenPose;
        _eligible = home && taken && disposition.Length == 0
            && YardMechanism.Flag(props, W01Controller.PrologueMittenKey, "gateSeen")
            && !bridge.PhotoBookPhotos().Any(photo => photo.WorldCompleted || photo.HasResumePoint
                || photo.PhotoId is "P-WATER" or "P-HOUSE" or "P-COMMON" or "P-CELLAR")
            && !bridge.PhotoWorldFactObserved("first_world_home_talk");
        // Presentation controls only the optional local targets. The book,
        // family conversations and their authored conditions are untouched.
        _showTarget.SetPresentationEnabled(_eligible && !_busy);
        _hideTarget.SetPresentationEnabled(_eligible && !_busy);
    }

    private async Task ChooseAsync(bool show)
    {
        if (!CanChoose() || _bridge is not { } bridge || bridge.SessionIdentity is not { } session) return;
        var target = show ? _showTarget : _hideTarget;
        if (!target.IsPresentationCurrent(bridge, session)) return;
        _busy = true;
        Apply();
        try
        {
            if (!await bridge.DispatchWorldPropsAsync(new JsonArray(new JsonObject
                { ["propId"] = W01Controller.PrologueMittenKey, ["homeDisposition"] = show ? "shown" : "hidden" }))) return;
            if (!target.IsPresentationCurrent(bridge, session) || bridge.ActiveSceneId != HomeScene) return;
            Apply();
            UiFoley.PlayWorld(this, _wool.GlobalPosition, "forest/brush_rustle", -17f, 6f, 2f);
            _player?.ShowRemark(show ? "Гөлсинә" : "Айдар", show
                ? "Положи здесь, пусть подсохнет. Где нашёл?"
                : "Не покажу. Пока пусть будет здесь.");
            await bridge.SaveCheckpointAsync(force: true);
            if (!show) return;
            await ToSignal(GetTree().CreateTimer(3.5), SceneTreeTimer.SignalName.Timeout);
            if (target.IsPresentationCurrent(bridge, session) && bridge.ActiveSceneId == HomeScene
                && bridge.CurrentZoneId == "house_old_pc")
                _player?.ShowRemark("Айдар", "В лесу. Перед тем как проснулся в машине.");
        }
        catch (Exception error) { GD.PushWarning($"Home mitten action failed: {error.Message}"); }
        finally
        {
            _busy = false;
            if (IsInstanceValid(this) && IsInsideTree()) Apply();
        }
    }

    public override void _ExitTree()
    {
        if (_bridge is { } bridge && IsInstanceValid(bridge)) bridge.RuntimeStateChanged -= Apply;
    }
}
