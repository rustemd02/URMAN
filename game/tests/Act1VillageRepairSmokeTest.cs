using Godot;

namespace Urman.Godot.Tests;

/// <summary>Built-world night projection, library full-shape sweep and representative native views.</summary>
public partial class Act1VillageRepairSmokeTest : Node
{
    private int _checks;
    private FirstPersonController _player = null!;
    private Main _main = null!;
    private Act1ConnectedWorld _world = null!;
    private Node3D _room = null!;
    private Camera3D? _captureCamera;

    public override void _Process(double delta)
    {
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true } pause)
        { DisplayServer.WindowMoveToForeground(); pause.Resume(); }
    }

    public override async void _Ready()
    {
        var exit = 1;
        ProcessMode = ProcessModeEnum.Always;
        try
        {
            _main = ResourceLoader.Load<PackedScene>("res://scenes/main.tscn").Instantiate<Main>();
            _main.EnableAct1ConnectedWorld = true; AddChild(_main);
            await Frames(10);
            var bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            Check(await bridge.StartNewGameAsync(), "ordinary runtime session");
            _world = _main.ConnectedWorld!; _player = _main.GetNode<FirstPersonController>("Player");
            _world.SetActiveLogicalZone("village_day"); await Frames(10);
            _room = _world.GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex/MosqueInterior");
            var director = _world.AuthoredWorld!;
            var residents = director.ObjectIds.Where(id => id.StartsWith("urman.world:act1/residents/", StringComparison.Ordinal))
                .Select(id => director.ObjectRoot(id)!).ToArray();
            Check(residents.Length >= 30, "all authored background residents are present in the content");
            var storyBefore = bridge.SelectRuntimeState().GetRawText();
            _world.SetActiveLogicalZone("kara_urman_night"); await Frames(4);
            Check(residents.All(n => !n.Visible && n.ProcessMode == ProcessModeEnum.Disabled), "ordinary residents stay indoors at night with animation processing off");
            Check(_world.FindChild("Npc_alsu", true, false) is Node3D { Visible: false }, "Alsu's daytime street walk is absent at night");
            Check(((InteractionTarget)_world.FindChild("AlsuNpc", true, false)).CollisionLayer == 0
                && ((InteractionTarget)_world.FindChild("RinatNpc", true, false)).CollisionLayer == 0, "absent ordinary night actors have no talk target in the street");
            Check(bridge.SelectRuntimeState().GetRawText() == storyBefore, "presence projection does not write narrative or save state");
            await Capture("night-yard", new(-12, 2.5f, 6), new(-9, 1.5f, -3), terrain: true);
            _world.SetActiveLogicalZone("village_day"); await Frames(4);
            Check(residents.Any(n => n.Visible) && residents.Any(n => !n.Visible), "daytime has short visits and households indoors, rather than a permanent outdoor crowd");
            Check(_world.GetMeta("villageYardCleanupHidden").AsInt32() > 0, "loose residential kit clutter was removed across the built village");
            var removed = _world.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Where(m => m.HasMeta("yardCleanup")).ToArray();
            Check(removed.All(m => !m.IsVisibleInTree()), "every audited redundant piece stays suppressed after day/night routing");
            GD.Print($"act1-village-repair: residents={residents.Length} dayOutside={residents.Count(n=>n.Visible)} removedPieces={removed.Length} owners={_world.GetMeta("villageYardCleanupOwners")}");
            await Capture("day-yard-west", new(-31, 2.8f, 124), new(-38.25f, 1.0f, 118.96f), terrain: true);
            await Capture("day-yard-east", new(30, 2.8f, 124), new(37.75f, 1.0f, 118.75f), terrain: true);
            CheckLibrarySweep();
            var carpet = _room.GetNode<MeshInstance3D>("MosquePrayerCarpet");
            Check(carpet.Position.Y + carpet.Mesh.GetAabb().End.Y < .003f, "textile and matching contact lie below the manual door floor clearance");
            Check(_room.GetMeta("libraryClearSizeMetres").AsVector2().X > 3.1f, "library clear width expanded from 1.69m to 3.14m");
            Check(_room.GetMeta("hallClearHeightMetres").AsSingle() > 3.7f, "room ceiling raised and actual headroom retained");
            Check(_room.GetNode<Node3D>("MosqueMinaretGallery/AzanMicrophone").GetChildCount() >= 6, "microphone, tripod, boom and cable at sheltered azan point");
            await Capture("mosque-exterior", new(13, 7, 13), new(-.6f, 7.5f, 0));
            await Capture("mosque-hall", new(1.45f, 1.65f, 2.2f), new(-1, 1.5f, -3.7f));
            await Capture("mosque-library", new(-3.95f, 1.6f, 1.45f), new(-5.7f, 1.1f, 3.9f));
            await Capture("mosque-spiral", new(3.83f, 1.6f, -.35f), new(3.8f, 2.4f, -2.8f));
            await Capture("minaret-lantern", new(3.25f, Act1ConnectedWorld.MinaretGalleryHeight + 1.6f, -1.75f), new(4.7f, Act1ConnectedWorld.MinaretGalleryHeight+1.1f, -1.4f));
            // Native/physics player starts on clear timber, then the real target callback commits
            // the library door through its complete arc. No direct hinge assignment.
            _player.ApplyZoneSpawn(_room.ToGlobal(new(-1.90f, .035f, 1.62f)), 0); await Frames(12);
            var target = (InteractionTarget)_world.FindChild("MosqueLibraryUse", true, false);
            Check(target.IsAvailable(), "library door uses the live ordinary repeat interaction");
            target.Interact();
            var hinge = _room.GetNode<Node3D>("MosqueLibraryHinge");
            for (var i = 0; i < 240 && Math.Abs(hinge.Rotation.Y - Mathf.Pi*.5f) > .01; i++) await Frames(1);
            Check(Math.Abs(hinge.Rotation.Y - Mathf.Pi*.5f) < .01f,
                "ordinary library action completes its 90-degree swing; result=" + target.GetMeta("lastDoorActionResult", "none"));
            _player.GetNode<Camera3D>("Head/Camera3D").MakeCurrent();
            await WalkTo(_room.ToGlobal(new(-2.6f, .035f, 1.75f)));
            await WalkTo(_room.ToGlobal(new(-4.25f, .035f, 1.75f)));
            Check(_player.IsOnFloor() && _world.FacilityInteriorAt(_player.GlobalPosition) == "mosque", "ordinary controller crosses library doorway on real floor");
            await Capture("library-open", new(-1.9f, 1.6f, 2.3f), new(-5.3f, 1.3f, 2.0f));
            target.Interact();
            for (var i = 0; i < 240 && Math.Abs(hinge.Rotation.Y) > .01; i++) await Frames(1);
            Check(Math.Abs(hinge.Rotation.Y) < .01f, "library closes through the same clear full arc");
            GD.Print($"act1-village-repair: PASS {_checks} checks; human art/culture/listening/performance external"); exit = 0;
        }
        catch (Exception e) { GD.PrintErr("act1-village-repair: FAIL " + e); }
        finally { Input.ActionRelease("move_forward"); if (_main is not null) await GodotSmokeCleanup.ReleaseAsync(_main); GetTree().Quit(exit); }
    }

    private void CheckLibrarySweep()
    {
        var hinge = _room.GetNode<Node3D>("MosqueLibraryHinge");
        var body = hinge.GetNode<StaticBody3D>("MosqueLibraryLeafBody");
        using var shape = new BoxShape3D { Size = new(.065f, 2.17f-.012f, 1.10f) };
        var excludes = new global::Godot.Collections.Array<Rid> { body.GetRid(), _player.GetRid() };
        using var owner = (global::Godot.Collections.Array)excludes;
        using var query = new PhysicsShapeQueryParameters3D { Shape=shape, CollisionMask=3, Margin=.002f, Exclude=excludes };
        for (var i=0; i<=100; i++)
        {
            query.Transform = _room.GlobalTransform * new Transform3D(new Basis(Vector3.Up, i * Mathf.Pi / 200), hinge.Position)
                * new Transform3D(Basis.Identity,new(0,(2.17f+.012f)*.5f,.55f));
            var hits=_room.GetWorld3D().DirectSpaceState.IntersectShape(query,1);
            using var hitsOwner=(global::Godot.Collections.Array)hits;
            Check(hits.Count==0,"library full leaf including .002m margin clear at sample "+i+"; blocker="+(hits.Count>0?(hits[0]["collider"].AsGodotObject() as Node)?.GetPath().ToString():"none"));
        }
    }
    private async Task WalkTo(Vector3 point)
    {
        for (var i=0;i<240;i++)
        {
            var delta=point-_player.GlobalPosition;delta.Y=0;if(delta.Length()<.10f)break;
            _player.ApplySmokeLook(0,Mathf.RadToDeg(Mathf.Atan2(-delta.X,-delta.Z)));Input.ActionPress("move_forward");await Frames(1);
        }
        Input.ActionRelease("move_forward");await Frames(8);
        Check(new Vector2(point.X-_player.GlobalPosition.X,point.Z-_player.GlobalPosition.Z).Length()<.20f,"input traversal to "+point+"; actual="+_player.GlobalPosition);
    }
    private async Task Capture(string name,Vector3 eye,Vector3 look,bool terrain=false)
    {
        var output=OS.GetEnvironment("URMAN_VILLAGE_REPAIR_CAPTURE");if(output.Length==0 || DisplayServer.GetName()=="headless")return;
        System.IO.Directory.CreateDirectory(output);
        _captureCamera ??= new Camera3D { Name="VillageRepairEvidenceCamera", Fov=72 };
        if (!_captureCamera.IsInsideTree()) AddChild(_captureCamera);
        if(terrain) { eye.Y += Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(eye.X,eye.Z);look.Y+=Urman.Experiments.AgentBAct1.AgentBAct1HeightField.CollisionGround(look.X,look.Z); }
        else { eye=_room.ToGlobal(eye);look=_room.ToGlobal(look); }
        _captureCamera.GlobalPosition=eye;_captureCamera.LookAt(look,Vector3.Up);_captureCamera.Current=true;await Frames(8);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var texture=GetViewport().GetTexture();using var img=texture.GetImage();Check(img.SavePng(System.IO.Path.Combine(output,name+".png"))==Error.Ok,"save native "+name);
    }
    private async Task Frames(int count) {for(var i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private void Check(bool result,string label) { _checks++; if(!result)throw new InvalidOperationException(label); }
}
