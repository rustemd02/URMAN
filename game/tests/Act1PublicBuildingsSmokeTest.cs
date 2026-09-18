using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot.Tests;

/// <summary>Real pedestrian traversal of the three existing public footprints.
/// The recorded exterior setup for each building is a local fixture, not a playtime proof.</summary>
public partial class Act1PublicBuildingsSmokeTest : Node
{
    private Act1DemoRoot _demo = null!;
    private RuntimeBridge _bridge = null!;
    private Act1ConnectedWorld _world = null!;
    private FirstPersonController _player = null!;
    private Camera3D _camera = null!;
    private readonly List<object> _events = new();
    private int _checks;
    private float _walked;
    private string Output => System.Environment.GetEnvironmentVariable("URMAN_PUBLIC_OUTPUT")
        ?? throw new InvalidOperationException("Set URMAN_PUBLIC_OUTPUT to a new absolute evidence folder.");

    public override async void _Ready()
    {
        var exit = 1;
        try
        {
            if (!Path.IsPathFullyQualified(Output)) throw new InvalidOperationException("Evidence path must be absolute.");
            _demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(_demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(_demo), "ordinary New Game");
            _demo._UnhandledInput(new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = true });
            await Frames(8);
            _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge ?? throw new InvalidOperationException("Missing runtime.");
            _world = _demo.DemoMain.ConnectedWorld ?? throw new InvalidOperationException("Missing connected world.");
            _player = _demo.DemoMain.GetNode<FirstPersonController>("Player");
            _camera = _player.GetNode<Camera3D>("Head/Camera3D");
            Require(!_player.ModalOpen && _world.PublicBuildingRooms.Count == 3, "three public rooms are in the ordinary connected world");
            var requested = System.Environment.GetEnvironmentVariable("URMAN_PUBLIC_BUILDING");
            var rooms = _world.PublicBuildingRooms.Where(room => string.IsNullOrEmpty(requested) || room.Id == requested).ToArray();
            Require(rooms.Length > 0, "requested public building exists");
            foreach (var building in rooms) await Visit(building);
            exit = 0;
        }
        catch (Exception error)
        {
            GD.PrintErr("act1-public-buildings: " + error);
            _events.Add(new { kind = "failure", error = error.ToString(), feet = _player?.GlobalPosition.ToString(),
                currentAim = _camera is null ? string.Empty : DescribeAim(7) });
            try { await Capture("failure"); } catch (Exception capture) { _events.Add(new { kind = "capture-failure", error = capture.Message }); }
        }
        finally
        {
            Release();
            try
            {
                Directory.CreateDirectory(Output);
                using var file = new FileStream(Path.Combine(Output, "public-buildings-receipt.json"),
                    FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(file, new { passed = exit == 0, checks = _checks, measuredWalkingMetres = _walked,
                    fixture = "ordinary NewGame; one explicit supported exterior setup per existing public building; all entrances/returns use controller input",
                    humanPlaytime = "external/not-run", visualCulturalAcceptance = "external/not-run",
                    events = _events }, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception error) { exit = 1; GD.PrintErr("Cannot preserve public-building receipt: " + error); }
            if (_demo is not null) await GodotSmokeCleanup.ReleaseAsync(_demo);
        }
        GetTree().Quit(exit);
    }

    private async Task Visit(Act1ConnectedWorld.PublicBuildingRoom building)
    {
        if(building.Id=="school")CheckSchoolBankCut(building);
        if(building.Id=="council")CheckCouncilFenceJunction(building);
        var outward = building.Metric.GlobalBasis.Z.Normalized();
        var start = building.Outside + outward * .55f;
        start.Y = AgentBAct1HeightField.CollisionGround(start.X, start.Z) + .035f;
        Release();
        Require(_player.CanStandAt(start), building.Id + ": measured exterior setup fits the standing capsule");
        _player.ApplyZoneSpawn(start, 0);
        await Frames(8);
        Require(_player.IsOnFloor(), building.Id + ": setup has actual support");
        _events.Add(new { kind = "local-exterior-fixture", building = building.Id, source = building.Building.GetPath().ToString(),
            feet = _player.GlobalPosition.ToString(), addressId = building.AddressId, countsAsWalk = false });
        Aim(building.OuterDoor.GlobalPosition);
        await Frames(3);
        await Capture(building.Id + "_01_closed_entrance");
        Require(!Open(building.Id + "/entrance") && !Open(building.Id + "/inner-door"),
            building.Id + ": approaching leaves both doors closed and does not knock automatically");
        Require(building.OuterDoor.IsAvailable() && RayTarget(building.OuterDoor), building.Id + ": explicit entrance interaction is aimed and available");

        // Actually push toward the closed leaf, keeping normal collision and speed.
        await MoveFor(building.Vestibule, 35);
        Require(_world.PublicInteriorAt(_player.GlobalPosition) != building.Id,
            building.Id + ": closed outer leaf stops the standing player");
        Require(!Open(building.Id + "/entrance"), building.Id + ": walking against the leaf never opens it");
        Aim(building.OuterDoor.GlobalPosition);
        await Frames(3);
        await Press("interact");
        await Frames(12);
        Require(!Open(building.Id + "/entrance"), building.Id + ": outward leaf refuses a player standing in its swept arc");
        await WalkTo(start, building.Id + ": step away from the leaf");
        await ToggleDoor(building.OuterDoor, building.Id + "/entrance", true);
        await Capture(building.Id + "_02_opened_outward");

        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        await WalkTo(building.Vestibule, building.Id + ": walk up real treads and through the outer aperture");
        Require(_world.PublicInteriorAt(_player.GlobalPosition) == building.Id && !_player.IsCrouching,
            building.Id + ": the seni has standing headroom");
        ProbeSupport(building.Id + "/seni");
        await ToggleDoor(building.InnerDoor, building.Id + "/inner-door", true);
        await EnterMainRoom(building);
        Require(_player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries,
            building.Id + ": both thresholds are crossed with input and no relocation or fall recovery");
        ProbeSupport(building.Id + "/main");
        Require(building.Room.ToLocal(_player.GlobalPosition).Y > -.045f
            && building.Room.ToLocal(_player.GlobalPosition).Y < .06f, building.Id + ": timber floor is the actual support level");
        await ExploreRoom(building);
        await ExitToOutside(building, start);
        Require(_world.PublicInteriorAt(_player.GlobalPosition) != building.Id, building.Id + ": ordinary return reaches the exterior");

        var slot = "public-door-proof-" + building.Id;
        Require(await _bridge.SaveSlotAsync(slot), building.Id + ": actual open doors and exterior player pose save together");
        await ToggleDoor(building.OuterDoor, building.Id + "/entrance", false);
        Require(await _bridge.LoadSlotAsync(slot), building.Id + ": restore the same runtime snapshot");
        await Frames(10);
        Require(Open(building.Id + "/entrance") && Open(building.Id + "/inner-door"),
            building.Id + ": both door states are restored without discovering a source");
        Require(_player.IsOnFloor() && _player.CanStandAt(_player.GlobalPosition),
            building.Id + ": load leaves an unobstructed standing body");
        await WalkTo(building.Vestibule, building.Id + ": return through restored outward leaf");
        await EnterMainRoom(building);
        if (building.Id == "council")
        {
            await WalkTo(building.Room.ToGlobal(new(1.10f, 0, -.73f)), "council: return to the existing hall aisle after load");
            await WalkTo(building.Room.ToGlobal(new(.45f, 0, .02f)), "council: approach the saved poster through the clear aisle");
            await CheckCouncilPoster(building, afterLoad: true);
            await WalkTo(building.Room.ToGlobal(new(.70f, 0, .02f)), "council: leave the poster by the existing hall passage");
        }
        await Capture(building.Id + "_05_return_after_load");
        await ExitToOutside(building, start);
        await ToggleDoor(building.OuterDoor, building.Id + "/entrance", false);
        _events.Add(new { kind = "building-complete", building = building.Id, addressId = building.AddressId,
            exterior = start.ToString(), entry = building.Entrance.ToString(), halfSize = building.HalfSize.ToString(), ceiling = building.Ceiling });
    }

    private async Task EnterMainRoom(Act1ConnectedWorld.PublicBuildingRoom building)
    {
        var local = building.Metric.ToLocal(building.Vestibule);
        // The interaction target follows the swinging leaf. Inside is authored
        // on the fixed aperture axis and cannot move toward the locked store.
        var inner = building.Metric.ToLocal(building.Inside);
        var aligned = building.Metric.ToGlobal(new(local.X, local.Y, inner.Z));
        var leaf = building.Metric.ToLocal(building.InnerDoor.GlobalPosition);
        var movingTargetGoal = building.Metric.ToGlobal(new(local.X, local.Y, leaf.Z));
        _events.Add(new { kind = "fixed-aperture-approach", building = building.Id,
            actualApertureGoal = aligned.ToString(), formerMovingTargetGoal = movingTargetGoal.ToString(),
            fixedGoalFits = _player.CanStandAt(aligned), movingTargetGoalFits = _player.CanStandAt(movingTargetGoal) });
        await WalkTo(aligned, building.Id + ": align within existing seni");
        await WalkTo(building.Inside, building.Id + ": cross the actual main-wall aperture");
        Require(_world.PublicInteriorAt(_player.GlobalPosition) == building.Id, building.Id + ": public interior owner matches the physical room");
    }

    private async Task ExitToOutside(Act1ConnectedWorld.PublicBuildingRoom building, Vector3 outside)
    {
        await WalkTo(building.Inside, building.Id + ": return along the room corridor");
        var local = building.Metric.ToLocal(building.Vestibule);
        var inner = building.Metric.ToLocal(building.Inside);
        await WalkTo(building.Metric.ToGlobal(new(local.X, local.Y, inner.Z)), building.Id + ": return through inner aperture");
        await WalkTo(building.Vestibule, building.Id + ": pass the free end of the outer leaf");
        await WalkTo(outside, building.Id + ": descend the real entry treads");
    }

    private async Task ExploreRoom(Act1ConnectedWorld.PublicBuildingRoom building)
    {
        Vector3 At(float x, float z) => building.Room.ToGlobal(new(x, 0, z));
        if (building.Id == "shop")
        {
            await WalkTo(At(.65f, -.58f), "shop: reach the front of Razilya's counter");
            var counter = Target("urman.chapter1:local/shop-counter");
            Aim(counter.GlobalPosition); await Frames(3);
            Require(counter.IsInGroup("village_shop_counter") && counter.IsAvailable() && RayTarget(counter),
                "shop: physical counter and shop UI use the same reachable target");
            await Capture("shop_03_counter_and_razilya");
            var book = Target("urman.chapter1:interaction/shop-account-book");
            Aim(book.GlobalPosition); await Frames(3);
            Require(book.DocumentId == "urman.chapter1:document/shop-account-book" && book.IsAvailable() && RayTarget(book),
                "shop: the actual debt notebook is readable from the public side of the counter");
        }
        else if (building.Id == "school")
        {
            await WalkTo(At(1.27f, -1.26f), "school: reach the corridor outside the office");
            var note = Target("urman.chapter1:interaction/school-staff-note");
            Require(_camera.GlobalPosition.DistanceTo(note.GlobalPosition) < 3.15f
                && !_world.CanUsePublicBuildingInteraction(note.InteractionId), "school: closed office leaf physically blocks a nearby source");
            await ToggleDoor(Target("urman.chapter1:local/school/office"), "school/office", true);
            await WalkTo(At(.26f, -1.26f), "school: pass the office door");
            Aim(note.GlobalPosition); await Frames(3);
            Require(note.IsAvailable() && RayTarget(note), "school: actual staff source becomes readable after entering");
            await Capture("school_03_office");
            await WalkTo(At(1.27f, -1.26f), "school: return to corridor");
            await WalkTo(At(1.27f, .76f), "school: walk to the classroom door");
            await ToggleDoor(Target("urman.chapter1:local/school/classroom"), "school/classroom", true);
            await WalkTo(At(-.07f, .76f), "school: enter the preserved classroom footprint");
            var photo = Target("urman.chapter1:interaction/school-class-photo");
            Aim(photo.GlobalPosition); await Frames(3);
            Require(photo.DocumentId == "urman.chapter1:document/school-class-photo" && photo.IsAvailable() && RayTarget(photo),
                "school: class photo belongs to the real room and actual content source");
            CheckPhoto("school_class_photo_Source", "res://assets/images/school-class-2005.png");
            await Capture("school_04_classroom_photo");
            await WalkTo(At(1.27f, .76f), "school: leave classroom through the same opening");
            await WalkTo(At(1.27f, -1.26f), "school: return along the corridor without crossing partitions");
        }
        else
        {
            await WalkTo(At(1.10f, -.73f), "council: enter the hall beside the village plan");
            var plan = Target("urman.chapter1:interaction/council-village-plan");
            Aim(plan.GlobalPosition); await Frames(3);
            Require(plan.IsAvailable() && RayTarget(plan), "council: physical village plan is reachable in the hall");
            var planMount = building.Room.GetNode<Node3D>("council_village_plan_Source");
            Require(planMount.HasMeta("mapDataOwner") && planMount.GetNode<MeshInstance3D>("SharedRegistryRoads").Mesh.GetSurfaceCount() > 0,
                "council: displayed roads come from the actual common registry graph");
            await Capture("council_03_hall_and_plan");
            await WalkTo(At(.45f, -.16f), "council: approach the office from the open hall");
            await ToggleDoor(Target("urman.chapter1:local/council/office"), "council/office", true);
            // B32's straight line at Z=-.16 crossed the right casing corner.
            // Record the exact old stop and old desk goal against live shapes,
            // then walk in the open aisle rather than clipping their geometry.
            var formerStop=CouncilStandingProbe(building.Room.ToGlobal(new(-.537808f,.000942f,-.133578f)));
            var formerDesk=CouncilStandingProbe(At(-1.08f,-.96f));
            _events.Add(new{kind="council-route-contact-probes",formerStop=formerStop.Evidence,
                formerAlignmentGoal=CouncilStandingProbe(At(-1.08f,-.16f)).Evidence,formerDeskGoal=formerDesk.Evidence,
                aisleStart=CouncilStandingProbe(At(.45f,.02f)).Evidence,
                aisleAlignment=CouncilStandingProbe(At(-1.08f,.02f)).Evidence,
                deskStanding=CouncilStandingProbe(At(-1.08f,-.84f)).Evidence});
            Require(formerStop.Owners.Any(owner=>owner.Contains("CouncilOfficeFrame",StringComparison.Ordinal)),
                "council: the historical stop is identified against the actual office casing");
            Require(formerDesk.Owners.Any(owner=>owner.Contains("CouncilDesk/TopBody",StringComparison.Ordinal)),
                "council: the former desk goal intersects the actual tabletop");
            Require(_player.CanStandAt(At(.45f,.02f))&&_player.CanStandAt(At(-1.08f,.02f))&&_player.CanStandAt(At(-1.08f,-.84f)),
                "council: aisle and desk standing points fit the unchanged full standing capsule");
            await WalkTo(At(.45f,.02f),"council: step into the clear aisle in front of the casing");
            await WalkTo(At(-1.08f,.02f),"council: align with the real office doorway through the aisle");
            await WalkTo(At(-1.08f,-.84f),"council: enter the office and stand before the desk");
            var album = Target("urman.chapter1:interaction/council-photo-album");
            Aim(album.GlobalPosition); await Frames(3);
            Require(album.IsAvailable() && RayTarget(album), "council: album is readable at the actual desk");
            CheckPhoto("council_photo_album_Source", "res://assets/images/council-sabantuy-2005.png");
            await Capture("council_04_archive_album");
            await WalkTo(At(-1.08f,.02f), "council: return from office through the same clear aisle");
            await WalkTo(At(.45f,.02f), "council: step clear of the office leaf before closing it");
            await ToggleDoor(Target("urman.chapter1:local/council/office"), "council/office", false);
            await CheckCouncilPoster(building, afterLoad: false);
            await WalkTo(At(.70f,.02f), "council: return to the hall passage");
        }
    }

    private async Task CheckCouncilPoster(Act1ConnectedWorld.PublicBuildingRoom building, bool afterLoad)
    {
        const string documentId = "urman.chapter1:document/council-sabantuy-poster";
        const string knowledgeId = "urman.chapter1:knowledge/council-sabantuy-poster-read";
        var mount = building.Room.GetNode<Node3D>("council_sabantuy_poster_Source");
        var target = Target("urman.chapter1:interaction/council-sabantuy-poster");
        var printed = mount.GetNode<Node3D>("PrintedPoster");
        Require(!Open("council/office"), "council poster: the manually closed office leaf leaves the wall approach clear");
        var document = _bridge.RequireDocument(documentId);
        var album = _bridge.RequireDocument("urman.chapter1:document/council-photo-album");
        var sourceLines = document.BodyMarkdown.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        var fields = new[] { "Heading", "Date", "Programme" }.Select(name => printed.GetNode<Label3D>(name)).ToArray();
        Require(fields.Select(label => label.Text).SequenceEqual(sourceLines.Take(3).Select(SourceExcerptSelection.FormatPlainSourceText)),
            "council poster: the visible face uses the three actual authored lines, without the reverse-side list");
        var caption = printed.GetNode<Label3D>("PreparationCaption");
        var expectedCaption = album.BodyMarkdown.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Skip(1).First();
        Require(caption.Text == SourceExcerptSelection.FormatPlainSourceText(expectedCaption)
            && fields.All(label => !label.DoubleSided && !label.NoDepthTest) && !caption.DoubleSided,
            "council poster: the photograph is explicitly captioned as preparation and ink retains ordinary occlusion");
        Require(mount.GetMeta("printedPosterBound", false).AsBool() && !mount.GetNode<Label3D>("DocumentTitle").Visible
            && Enumerable.Range(0, 5).All(row => !mount.GetNode<MeshInstance3D>("WrittenLine" + row).Visible),
            "council poster: only this bound poster retires its title-and-lines placeholder");
        Require(target.DocumentId == documentId && mount.ToLocal(target.GlobalPosition).DistanceTo(new(0, 0, .052f)) < .0001f
            && mount.Position.DistanceTo(new(-building.HalfSize.X + .035f, 1.42f, .19f)) < .0001f
            && ((QuadMesh)mount.GetNode<MeshInstance3D>("SourceFace").Mesh).Size.DistanceTo(new(.65f, .86f)) < .0001f,
            "council poster: source ID and paper dimensions remain fixed, with target and mount on the solid window pier");
        var photograph = printed.GetNode<MeshInstance3D>("PreparationPhotograph");
        var material = photograph.MaterialOverride as StandardMaterial3D;
        Require(photograph.Mesh is QuadMesh image && Math.Abs(image.Size.X / image.Size.Y - 1.5f) < .0001f
            && material is not null && material.AlbedoTexture?.ResourcePath == album.Images!.Single().ResourcePath
            && material.TextureFilter == BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            "council poster: the existing source photograph retains its 3:2 frame and mipmapped material");
        var wall = building.Building.FindChild("VariantA_TimberGable_Dwelling_Left_Wall_LOD0", true, false) as MeshInstance3D
            ?? throw new InvalidOperationException("The council poster needs the existing pierced left wall.");
        var expectedWallSource = wall.GetPath().ToString();
        for (var index = 0; index < 4; index++)
        {
            var fixing = printed.GetNode<Node3D>("WallFixing" + index);
            // Check the full head footprint, not just a centre ray that could
            // accept a narrow edge or this same building's window glass.
            foreach (var offset in new[] { Vector2.Zero, new(-.006f, 0), new(.006f, 0), new(0, -.006f), new(0, .006f) })
                CheckWallSupport(fixing, offset, "council-poster-wall-fixing", index);
        }
        var backing = mount.GetNode<MeshInstance3D>("PaperOrFrame").GetAabb();
        foreach (var x in new[] { backing.Position.X, backing.End.X })
            foreach (var y in new[] { backing.Position.Y, backing.End.Y })
                CheckWallSupport(mount, new(x, y), "council-poster-frame-corner", -1);

        void CheckWallSupport(Node3D anchor, Vector2 offset, string kind, int index)
        {
            using var query = PhysicsRayQueryParameters3D.Create(anchor.ToGlobal(new(offset.X, offset.Y, .05f)), anchor.ToGlobal(new(offset.X, offset.Y, -.20f)), 3,
                new global::Godot.Collections.Array<Rid> { _player.GetRid() });
            var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
            Require(hit.Count > 0, "council poster: " + kind + " " + index + " at " + offset + " has a real wall contact");
            var body = hit["collider"].AsGodotObject() as CollisionObject3D;
            var shape = body?.ShapeOwnerGetOwner(body.ShapeFindOwner(hit["shape"].AsInt32())) as Node;
            var source = shape?.GetMeta("authoredSourceMesh", string.Empty).AsString() ?? string.Empty;
            var localHit = anchor.ToLocal(hit["position"].AsVector3());
            Require(localHit.Z >= -.098f && localHit.Z <= .032f
                && hit["normal"].AsVector3().Dot(anchor.GlobalBasis.Z.Normalized()) > .85f
                && source == expectedWallSource,
                "council poster: " + kind + " " + index + " at " + offset + " reaches the exact opaque left wall at fixing depth");
            _events.Add(new { kind, index, afterLoad, sampleOffset = offset.ToString(), expectedWallSource, point = hit["position"].AsVector3().ToString(),
                owner = body?.GetPath().ToString(), shape = shape?.GetPath().ToString(), source, localHit = localHit.ToString() });
        }

        int Entries() => _bridge.JournalEntries().Count(entry => entry.EntryId == knowledgeId);
        string Knowledge() => _bridge.SelectRuntimeState().GetProperty("knowledge").GetRawText();
        var beforeKnowledge = Knowledge();
        var beforeScene = _bridge.ActiveSceneId;
        Require(Entries() == (afterLoad ? 1 : 0), "council poster: the real visit starts with the expected saved reading state");
        await WalkTo(building.Room.ToGlobal(new(-.65f, 0, .02f)), "council: view the poster from the clear hall aisle");
        for (var turn = 0; turn < 6; turn++) Aim(mount.GlobalPosition);
        await Frames(3);
        if (!afterLoad) await Capture("council_04a_poster_in_hall");
        await WalkTo(building.Room.ToGlobal(new(-1.60f, 0, .02f)), "council: approach the actual poster past the closed office leaf");
        for (var turn = 0; turn < 6; turn++) Aim(target.GlobalPosition);
        await Frames(3);
        Require(target.IsAvailable() && RayTarget(target), "council poster: the current camera ray reaches its unchanged manual target");
        Require(Knowledge() == beforeKnowledge && Entries() == (afterLoad ? 1 : 0) && _bridge.ActiveSceneId == beforeScene,
            "council poster: viewing either angle grants no knowledge, journal entry or scene progress");
        if (!afterLoad) await Capture("council_04b_poster_face");
        var reader = GetTree().GetFirstNodeInGroup("document_ui") as DocumentUi
            ?? throw new InvalidOperationException("Missing existing document reader.");
        await Press("interact");
        for (var frame = 0; frame < 240 && !reader.IsOpen; frame++) await Frames(1);
        Require(reader.IsOpen && reader.OpenDocumentId == documentId && _player.ModalOpen,
            "council poster: one deliberate mapped press opens the same existing document");
        Require(reader.GetNode<RichTextLabel>("Screen/Document/Layout/Reader/Body").Text
            == SourceExcerptSelection.FormatSourceText(document.BodyMarkdown) && Entries() == 1 && _bridge.ActiveSceneId == beforeScene,
            "council poster: manual reading includes its authored reverse and records one existing knowledge entry");
        if (!afterLoad) await Capture("council_04c_poster_manual_reader");
        reader.GetNode<Button>("Screen/Document/Layout/Header/Close").EmitSignal(Button.SignalName.Pressed);
        await Frames(6);
        Require(!reader.IsOpen && !_player.ModalOpen, "council poster: closing the same reader restores ordinary input");
        _events.Add(new { kind = "council-poster-source-read", afterLoad, documentId, knowledgeId, entries = Entries(),
            manualInput = true, printedReverse = false, image = material!.AlbedoTexture!.ResourcePath,
            faceText = fields.Select(label => label.Text).ToArray(), caption = caption.Text,
            visualAcceptance = "requires inspection of the actual two world views and reader capture" });
    }

    private (object Evidence,string[] Owners) CouncilStandingProbe(Vector3 feet)
    {
        // Same fit dimensions as FirstPersonController.CanStandAt; this only
        // identifies contacts. All movement and acceptance still use that owner.
        var height=_player.StandingBodyHeight;
        using var shape=new CapsuleShape3D{Radius=_player.BodyRadius,Height=height-.015f};
        using var query=new PhysicsShapeQueryParameters3D{Shape=shape,CollisionMask=_player.CollisionMask,
            Transform=new(Basis.Identity,feet+Vector3.Up*(height*.5f+.01f)),Margin=.002f,
            Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()}};
        var hits=_player.GetWorld3D().DirectSpaceState.IntersectShape(query,32);
        var contacts=hits.Select(hit=>
        {
            var owner=hit["collider"].AsGodotObject() as Node;
            var index=hit["shape"].AsInt32();
            var contact=owner is CollisionObject3D body?body.ShapeOwnerGetOwner(body.ShapeFindOwner(index)) as Node:null;
            return new{owner=owner?.GetPath().ToString()??"unresolved",shapeIndex=index,shape=contact?.GetPath().ToString(),
                shapeType=(contact as CollisionShape3D)?.Shape?.GetClass().ToString(),
                authoredSourceMesh=owner?.HasMeta("authoredSourceMesh")==true?owner.GetMeta("authoredSourceMesh").AsString():null,
                transform=(contact as Node3D)?.GlobalTransform.ToString()};
        }).ToArray();
        return(new{feet=feet.ToString(),canStand=_player.CanStandAt(feet),height,radius=_player.BodyRadius,
            mask=_player.CollisionMask,margin=.002f,contacts,saturated=hits.Count==32},contacts.Select(hit=>hit.owner).ToArray());
    }

    private void CheckPhoto(string mountName, string path)
    {
        var mount = _world.FindChild(mountName, true, false) as Node3D ?? throw new InvalidOperationException("Missing real photo mount.");
        var face = mount.GetNode<MeshInstance3D>("SourceFace");
        var material = face.MaterialOverride as StandardMaterial3D
            ?? throw new InvalidOperationException("Missing photograph material: " + mountName);
        var texture = material.AlbedoTexture
            ?? throw new InvalidOperationException("Missing photograph texture: " + mountName);
        Require(texture.GetWidth() > 0, mountName + ": a real photograph texture is bound");
        var size = ((QuadMesh)face.Mesh).Size;
        Require(texture.ResourcePath == path && Math.Abs(size.X / size.Y - texture.GetWidth() / (float)texture.GetHeight()) < .001f,
            mountName + ": published image provenance and aspect ratio are preserved");
    }

    // FindChildren filters native engine classes; the managed script type is
    // selected only after retrieving its actual StaticBody3D instances.
    private InteractionTarget Target(string id) => _world.FindChildren("*", nameof(StaticBody3D), true, false)
        .OfType<InteractionTarget>().Single(target => target.InteractionId == id);
    private bool Open(string key)
    {
        var props = _bridge.SelectWorldProps();
        return props.TryGetProperty(key, out var record) && record.TryGetProperty("open", out var open)
            && open.ValueKind == JsonValueKind.True;
    }
    private async Task ToggleDoor(InteractionTarget target, string key, bool open)
    {
        void RecordAttempt(string phase)
        {
            _events.Add(new { kind = "door-input-probe", phase, key,
                feet = _player.GlobalPosition.ToString(), velocity = _player.Velocity.ToString(),
                forward = Input.GetActionStrength("move_forward"), backward = Input.GetActionStrength("move_backward"),
                left = Input.GetActionStrength("move_left"), right = Input.GetActionStrength("move_right"),
                interact = Input.GetActionStrength("interact"), focus = DisplayServer.WindowIsFocused(),
                modal = _player.ModalOpen, sessionReady = _bridge.SessionIdentity is not null,
                noticeActive = _player.InteractionNoticeActive, focusedInteraction = _player.FocusedInteractionId,
                aim = DescribeAim(7),
                actionNumber = target.GetMeta("lastDoorActionNumber", 0).AsInt32(),
                transformRevision = _player.PresentationTransformRevision,
                action = target.GetMeta("lastDoorAction", "not-called").ToString(),
                result = target.GetMeta("lastDoorActionResult", "not-called").ToString(),
                sweep = target.GetMeta("doorSweepProbe", "not-called").ToString() });
        }
        Require(Open(key) != open, key + ": door starts in the expected state");
        Aim(target.GlobalPosition); await Frames(3);
        Require(target.IsAvailable() && RayTarget(target), key + ": live input ray reaches the door");
        RecordAttempt("before-input");
        Require(_player.FocusedInteractionId == target.InteractionId,
            key + ": passive focus follows the actual aimed door during a result notice");
        var actionBefore = target.GetMeta("lastDoorActionNumber", 0).AsInt32();
        await Press("interact");
        RecordAttempt("after-input");
        Require(target.GetMeta("lastDoorActionNumber", 0).AsInt32() == actionBefore + 1,
            key + ": one deliberate mapped press reaches exactly one door action");
        var deadline = Time.GetTicksMsec() + 12000;
        var nextSample = Time.GetTicksMsec() + 500;
        while (Open(key) != open && Time.GetTicksMsec() < deadline)
        {
            await Frames(1);
            if (Time.GetTicksMsec() >= nextSample)
            {
                RecordAttempt("waiting");
                nextSample = Time.GetTicksMsec() + 500;
            }
        }
        RecordAttempt("finished-wait");
        Require(Open(key) == open, key + ": explicit interaction changes the existing saved door property");
        await Frames(70);
        _events.Add(new { kind = "door-action", key, open, player = _player.GlobalPosition.ToString(), target = target.GlobalPosition.ToString() });
    }

    private async Task WalkTo(Vector3 goal, string label)
    {
        var from = _player.GlobalPosition;
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        try
        {
            for (var frame = 0; frame < 210; frame++)
            {
                var delta = goal - _player.GlobalPosition; delta.Y = 0;
                if (delta.Length() < .085f) break;
                _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
                Input.ActionPress("move_forward", delta.Length() < .24f ? .40f : 1f);
                var previous = _player.GlobalPosition;
                await Frames(1);
                _walked += new Vector2(_player.GlobalPosition.X - previous.X, _player.GlobalPosition.Z - previous.Z).Length();
                if(label.StartsWith("council:",StringComparison.Ordinal)&&frame%15==0
                    &&new Vector2(_player.GlobalPosition.X-previous.X,_player.GlobalPosition.Z-previous.Z).Length()<.0005f)
                    _events.Add(new{kind="council-motion-contact",label,frame,current=CouncilStandingProbe(_player.GlobalPosition).Evidence,
                        goal=CouncilStandingProbe(goal).Evidence,velocity=_player.Velocity.ToString()});
            }
        }
        finally { Release(); }
        await Frames(3);
        var distance = new Vector2(goal.X - _player.GlobalPosition.X, goal.Z - _player.GlobalPosition.Z).Length();
        _events.Add(new { kind = "walking", label, from = from.ToString(), goal = goal.ToString(),
            final = _player.GlobalPosition.ToString(), remaining = distance, support = _player.IsOnFloor(),
            canStand = _player.CanStandAt(_player.GlobalPosition), crouched = _player.IsCrouching,
            councilStanding = label.StartsWith("council:",StringComparison.Ordinal)?CouncilStandingProbe(_player.GlobalPosition).Evidence:null,
            contacts = Enumerable.Range(0, _player.GetSlideCollisionCount()).Select(index =>
            {
                var contact = _player.GetSlideCollision(index);
                return new { owner = (contact.GetCollider() as Node)?.GetPath().ToString(),
                    point = contact.GetPosition().ToString(), normal = contact.GetNormal().ToString() };
            }).ToArray() });
        Require(distance < .12f && _player.IsOnFloor() && !_player.IsCrouching && _player.CanStandAt(_player.GlobalPosition),
            label + ": standing capsule reaches the actual destination");
        Require(_player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries,
            label + ": no teleport or recovery during movement");
    }

    private async Task MoveFor(Vector3 goal, int frames)
    {
        var revision = _player.PresentationTransformRevision;
        var recoveries = _player.FallRecoveries;
        var delta = goal - _player.GlobalPosition;
        _player.ApplySmokeLook(0, Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
        try { Input.ActionPress("move_forward"); await Frames(frames); }
        finally { Release(); }
        Require(_player.PresentationTransformRevision == revision && _player.FallRecoveries == recoveries,
            "closed-leaf approach uses real controller movement");
    }

    private void ProbeSupport(string label)
    {
        using var query = PhysicsRayQueryParameters3D.Create(_player.GlobalPosition + Vector3.Up * .18f,
            _player.GlobalPosition - Vector3.Up * .22f, 3,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        Require(hit.Count > 0 && hit["normal"].AsVector3().Y > .9f, label + ": real support is level enough to stand");
        _events.Add(new { kind = "floor-probe", label, feet = _player.GlobalPosition.ToString(),
            hit = hit["position"].AsVector3().ToString(), owner = (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() });
    }

    private void Aim(Vector3 point)
    {
        var delta = point - _camera.GlobalPosition;
        _player.ApplySmokeLook(Mathf.RadToDeg(Mathf.Atan2(delta.Y, new Vector2(delta.X, delta.Z).Length())),
            Mathf.RadToDeg(Mathf.Atan2(-delta.X, -delta.Z)));
    }
    private bool RayTarget(InteractionTarget target)
    {
        using var query = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
            _camera.GlobalPosition - _camera.GlobalBasis.Z * 2.7f, 5,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 && hit["collider"].AsGodotObject() == target;
    }
    private string DescribeAim(uint mask)
    {
        using var query = PhysicsRayQueryParameters3D.Create(_camera.GlobalPosition,
            _camera.GlobalPosition - _camera.GlobalBasis.Z * 2.7f, mask,
            new global::Godot.Collections.Array<Rid> { _player.GetRid() });
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? "no hit" : (hit["collider"].AsGodotObject() as Node)?.GetPath().ToString() + " at " + hit["position"].AsVector3();
    }

    private async Task Capture(string name)
    {
        if (System.Environment.GetEnvironmentVariable("URMAN_PUBLIC_CAPTURE") != "1") return;
        await Act1StateFlowProof.WaitForRenderedFrameAsync(this, "public/" + name);
        Directory.CreateDirectory(Output);
        var path = Path.Combine(Output, name + ".png");
        if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
        using var image = GetViewport().GetTexture().GetImage();
        if (image.SavePng(path) != Error.Ok) throw new IOException(path);
        if(name is "school_03_office" or "school_04_classroom_photo" or "shop_03_counter_and_razilya")
        {
            Act1VisibleSurfaceProbe.Log(GetTree().Root,_camera,"public/"+name+"/floor-owner",
                new Vector2(.45f,.92f),new Vector2(.9f,.9f));
            _events.Add(new{kind="visible-surface-owner-probe",phase=name,key="floor-owner",file=path,
                normalizedPixels=new[]{"(.45,.92)","(.9,.9)"},camera=_camera.GlobalTransform.ToString(),
                resultLocation="act1-visible-surface lines in this run's log",meshVisibilityChanged=false});
        }
        var extra=name switch
        {
            "school_02_opened_outward"=>new[]{new Vector2(.448f,.227f),new(.468f,.233f)},
            "shop_02_opened_outward"=>new[]{new Vector2(.450f,.161f),new(.477f,.160f)},
            "council_02_opened_outward"=>new[]{new Vector2(.450f,.231f),new(.472f,.234f),new(.550f,.617f),new(.500f,.563f),new(.560f,.710f),new(.519f,.642f)},
            _=>Array.Empty<Vector2>()
        };
        if(extra.Length>0)
        {
            Act1VisibleSurfaceProbe.Log(GetTree().Root,_camera,"public/"+name+"/seni-surface-owners",extra);
            _events.Add(new{kind="visible-surface-owner-probe",phase=name,key="seni-surface-owners",file=path,
                normalizedPixels=extra.Select(pixel=>pixel.ToString()).ToArray(),camera=_camera.GlobalTransform.ToString(),
                resultLocation="act1-visible-surface lines in this run's log",meshVisibilityChanged=false});
            var building=_world.PublicBuildingRooms.Single(room=>name.StartsWith(room.Id+"_",StringComparison.Ordinal));
            foreach(var infill in building.Building.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>()
                .Where(mesh=>mesh.Name.ToString().EndsWith("_SeniRearRoofInfill_LOD0",StringComparison.Ordinal)))
            {
                var source=infill.Mesh!.SurfaceGetMaterial(0);var active=infill.MaterialOverride??infill.GetActiveMaterial(0);
                _events.Add(new{kind="seni-infill-culling",phase=name,owner=infill.GetPath().ToString(),
                    sourceMaterial=source?.ResourceName,sourceCull=(source as BaseMaterial3D)?.CullMode.ToString(),
                    activeClass=active?.GetClass(),preserved=active?.GetMeta("sourceCullingPreserved",false).AsBool()??false,
                    renderMode=(active as ShaderMaterial)?.Shader?.Code.Split('\n').FirstOrDefault(line=>line.TrimStart().StartsWith("render_mode",StringComparison.Ordinal))});
            }
        }
        _events.Add(new { kind = "game-camera", file = path });
    }

    private void CheckSchoolBankCut(Act1ConnectedWorld.PublicBuildingRoom school)
    {
        Require(school.Room.GetMeta("exteriorBankFootprintExcluded",false).AsBool(),"school: the measured exterior bank has an explicit occupied-room cut");
        var bank=GetNode<MeshInstance3D>(school.Room.GetMeta("exteriorBankCutOwner").AsString());
        var original=bank.GetMeta("occupiedRoomOriginalMesh").AsGodotObject() as ArrayMesh
            ??throw new InvalidOperationException("School bank lost its actual pre-cut mesh.");
        var result=bank.Mesh as ArrayMesh??throw new InvalidOperationException("School bank has no published mesh.");
        var half=bank.GetMeta("occupiedRoomTerrainHalfSize").AsVector2();
        var wallThickness=.20f*school.Building.GlobalBasis.Scale.X;
        Require(bank.Name=="Ditch_FieldBank_EastZirat"&&bank.GetMeta("occupiedRoomTerrainCut").AsString()==school.Room.GetPath().ToString()
            &&result.GetSurfaceCount()==1&&original.GetSurfaceCount()==1&&result.SurfaceGetMaterial(0)==original.SurfaceGetMaterial(0),
            "school: only the identified single-surface bank is cut with its original material");
        Require(half.X>school.HalfSize.X&&half.Y>school.HalfSize.Y
            &&half.X<school.HalfSize.X+wallThickness&&half.Y<school.HalfSize.Y+wallThickness,
            "school: the cut boundary lies inside the existing wall thickness");
        var transform=school.Room.GlobalTransform.AffineInverse()*bank.GlobalTransform;
        // GetFaces builds a TriangleMesh and snaps coordinates to 0.1 mm in
        // this Godot version. Validate the actual published render vertices;
        // that cache changes thin triangles enough to falsify plane/area checks.
        static Vector3[] PublishedTriangles(ArrayMesh mesh)
        {
            if(mesh.SurfaceGetPrimitiveType(0)!=Mesh.PrimitiveType.Triangles)
                throw new InvalidOperationException("School bank is not a triangle surface.");
            var arrays=mesh.SurfaceGetArrays(0);
            var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var indexArray=arrays[(int)Mesh.ArrayType.Index];
            var indices=indexArray.VariantType==Variant.Type.Nil?Array.Empty<int>():indexArray.AsInt32Array();
            var faces=indices.Length==0?vertices:indices.Select(index=>vertices[index]).ToArray();
            if(faces.Length==0||faces.Length%3!=0)
                throw new InvalidOperationException("School bank has an incomplete triangle surface.");
            return faces;
        }
        var before=PublishedTriangles(original);var after=PublishedTriangles(result);
        var beforeRoom=before.Select(p=>transform*p).ToArray();var afterRoom=after.Select(p=>transform*p).ToArray();
        (double X,double Z) InRoom(Vector3 p)=>((double)transform.Basis.X.X*p.X+(double)transform.Basis.Y.X*p.Y+(double)transform.Basis.Z.X*p.Z+transform.Origin.X,
            (double)transform.Basis.X.Z*p.X+(double)transform.Basis.Y.Z*p.Y+(double)transform.Basis.Z.Z*p.Z+transform.Origin.Z);
        static double PlanArea((double X,double Z) a,(double X,double Z) b,(double X,double Z) c)
            =>Math.Abs((b.X-a.X)*(c.Z-a.Z)-(b.Z-a.Z)*(c.X-a.X))*.5;
        double Area(Vector3 a,Vector3 b,Vector3 c)=>PlanArea(InRoom(a),InRoom(b),InRoom(c));
        List<(double X,double Z)> InsidePolygon(Vector3 a,Vector3 b,Vector3 c)
        {
            // Keep independent validation arithmetic double throughout. Casting
            // the result of Vector3 subtraction or Dot still loses precision
            // first, especially for the long, narrow imported bank triangles.
            var polygon=new List<(double X,double Z)>{InRoom(a),InRoom(b),InRoom(c)};
            foreach(var plane in new (int Axis,double Sign,double Half)[]{(0,1,half.X),(0,-1,half.X),(1,1,half.Y),(1,-1,half.Y)})
            {
                if(polygon.Count==0)break;
                var clipped=new List<(double X,double Z)>();var previous=polygon[^1];
                var prev=plane.Half-plane.Sign*(plane.Axis==0?previous.X:previous.Z);
                foreach(var point in polygon)
                {
                    var distance=plane.Half-plane.Sign*(plane.Axis==0?point.X:point.Z);
                    if((distance>=0)!=(prev>=0))
                    {var t=prev/(prev-distance);clipped.Add((previous.X+(point.X-previous.X)*t,previous.Z+(point.Z-previous.Z)*t));}
                    if(distance>=0)clipped.Add(point);
                    previous=point;prev=distance;
                }
                polygon=clipped;
            }
            return polygon;
        }
        static double PolygonArea(IReadOnlyList<(double X,double Z)> polygon)
        {double area=0;for(var i=1;i+1<polygon.Count;i++)area+=PlanArea(polygon[0],polygon[i],polygon[i+1]);return area;}
        double originalArea=0,removedArea=0,publishedArea=0,insideArea=0;
        var exteriorTriangles=0;var missingExterior=0;var offOriginalPlane=0;
        var fragmentDiagnostics=new List<object>();
        for(var i=0;i<beforeRoom.Length;i+=3)
        {
            originalArea+=Area(before[i],before[i+1],before[i+2]);
            removedArea+=PolygonArea(InsidePolygon(before[i],before[i+1],before[i+2]));
            var triangle=beforeRoom.Skip(i).Take(3).ToArray();
            if(!(triangle.All(p=>p.X<=-half.X)||triangle.All(p=>p.X>=half.X)
                ||triangle.All(p=>p.Z<=-half.Y)||triangle.All(p=>p.Z>=half.Y)))continue;
            exteriorTriangles++;
            if(!Enumerable.Range(0,after.Length/3).Any(t=>Enumerable.Range(0,3).Any(offset=>
                Enumerable.Range(0,3).All(c=>before[i+c].DistanceTo(after[t*3+(offset+c)%3])<.00002f))))missingExterior++;
        }
        for(var i=0;i<afterRoom.Length;i+=3)
        {
            publishedArea+=Area(after[i],after[i+1],after[i+2]);
            var polygon=InsidePolygon(after[i],after[i+1],after[i+2]);
            var fragmentInside=PolygonArea(polygon);insideArea+=fragmentInside;
            // Every published fragment must remain within one original triangle
            // on its original plane, not merely have the same aggregate area.
            var fits=false;var bestError=double.PositiveInfinity;var bestParent=-1;
            double bestPlane=0,bestMinBary=0,bestMaxBary=0;
            for(var j=0;j<before.Length&&!fits;j+=3)
            {
                var a=before[j];var b=before[j+1];var c=before[j+2];
                var ex=(double)b.X-a.X;var ey=(double)b.Y-a.Y;var ez=(double)b.Z-a.Z;
                var fx=(double)c.X-a.X;var fy=(double)c.Y-a.Y;var fz=(double)c.Z-a.Z;
                var nx=ey*fz-ez*fy;var ny=ez*fx-ex*fz;var nz=ex*fy-ey*fx;
                var length=Math.Sqrt(nx*nx+ny*ny+nz*nz);if(length<1e-12)continue;
                // Dominant-axis barycentrics avoid subtracting nearly equal
                // squared dot products for a thin source triangle.
                var drop=Math.Abs(nx)>=Math.Abs(ny)&&Math.Abs(nx)>=Math.Abs(nz)?0:Math.Abs(ny)>=Math.Abs(nz)?1:2;
                var axis0=(drop+1)%3;var axis1=(drop+2)%3;
                var eU=(double)b[axis0]-a[axis0];var eV=(double)b[axis1]-a[axis1];
                var fU=(double)c[axis0]-a[axis0];var fV=(double)c[axis1]-a[axis1];
                var denominator=eU*fV-eV*fU;
                double planeError=0,minBary=double.PositiveInfinity,maxBary=double.NegativeInfinity;
                fits=true;
                for(var corner=0;corner<3;corner++)
                {
                    var point=after[i+corner];var du=(double)point[axis0]-a[axis0];var dv=(double)point[axis1]-a[axis1];
                    var u=(du*fV-dv*fU)/denominator;var v=(eU*dv-eV*du)/denominator;
                    var distance=Math.Abs(((double)point.X-a.X)*nx+((double)point.Y-a.Y)*ny+((double)point.Z-a.Z)*nz)/length;
                    planeError=Math.Max(planeError,distance);minBary=Math.Min(minBary,Math.Min(u,v));maxBary=Math.Max(maxBary,u+v);
                    if(distance>.00003||u<-.0001||v<-.0001||u+v>1.0001)fits=false;
                }
                var error=Math.Max(planeError/.00003,Math.Max(-minBary/.0001,(maxBary-1)/.0001));
                if(error<bestError){bestError=error;bestParent=j/3;bestPlane=planeError;bestMinBary=minBary;bestMaxBary=maxBary;}
            }
            if(!fits)offOriginalPlane++;
            if(!fits||fragmentInside>1e-10)fragmentDiagnostics.Add(new{triangle=i/3,fits,fragmentInside,bestParent,bestPlane,bestMinBary,bestMaxBary,
                maximumInsideDepth=polygon.Count==0?0:polygon.Max(p=>Math.Min(half.X-Math.Abs(p.X),half.Y-Math.Abs(p.Z)))});
        }
        var sourceArrays=original.SurfaceGetArrays(0);var finalArrays=result.SurfaceGetArrays(0);
        var sourceVertices=sourceArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var finalVertices=finalArrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var sourceNormals=sourceArrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var finalNormals=finalArrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var sourceUv=sourceArrays[(int)Mesh.ArrayType.TexUV].VariantType==Variant.Type.Nil?Array.Empty<Vector2>():sourceArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var finalUv=finalArrays[(int)Mesh.ArrayType.TexUV].VariantType==Variant.Type.Nil?Array.Empty<Vector2>():finalArrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var changedExteriorAttributes=0;
        for(var i=0;i<sourceVertices.Length;i++)
        {
            var local=transform*sourceVertices[i];
            if(Math.Abs(local.X)<=half.X+.0001f&&Math.Abs(local.Z)<=half.Y+.0001f)continue;
            if(!Enumerable.Range(0,finalVertices.Length).Any(j=>sourceVertices[i].DistanceTo(finalVertices[j])<.00002f
                &&sourceNormals[i].DistanceTo(finalNormals[j])<.0001f
                &&(sourceUv.Length==0||finalUv.Length==finalVertices.Length&&sourceUv[i].DistanceTo(finalUv[j])<.00002f)))changedExteriorAttributes++;
        }
        var floor=school.Room.GetNode<MeshInstance3D>("TimberFloor");
        Require(floor.Mesh is BoxMesh floorBox&&Math.Abs(floor.Position.Y+floorBox.Size.Y*.5f)<.00001f,
            "school: the timber floor top retains the existing room height");
        _events.Add(new{kind="school-exterior-bank-cut",owner=bank.GetPath().ToString(),room=school.Room.GetPath().ToString(),
            halfSize=half.ToString(),wallThickness,triangleSource="SurfaceGetArrays with published indices",originalTriangles=before.Length/3,publishedTriangles=after.Length/3,
            originalArea,removedArea,publishedArea,insideArea,exteriorTriangles,missingExterior,offOriginalPlane,changedExteriorAttributes,
            floorY=school.Room.GlobalPosition.Y,limit="published mesh geometry and material; actual floor appearance is captured with the same camera pixel probes"});
        float[] Point(Vector3 p)=>new[]{p.X,p.Y,p.Z};
        object Pose(Transform3D pose)=>new{x=Point(pose.Basis.X),y=Point(pose.Basis.Y),z=Point(pose.Basis.Z),origin=Point(pose.Origin)};
        object MeshData(ArrayMesh mesh)
        {
            var arrays=mesh.SurfaceGetArrays(0);var uv=arrays[(int)Mesh.ArrayType.TexUV];
            return new{format=mesh.SurfaceGetFormat(0).ToString(),vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(Point).ToArray(),
                normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array().Select(Point).ToArray(),indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array(),
                uv=uv.VariantType==Variant.Type.Nil?Array.Empty<float[]>():uv.AsVector2Array().Select(p=>new[]{p.X,p.Y}).ToArray()};
        }
        var geometry=JsonSerializer.Serialize(new{owner=bank.GetPath().ToString(),bankTransform=Pose(bank.GlobalTransform),roomTransform=Pose(school.Room.GlobalTransform),
            bankToRoom=Pose(transform),halfSize=new[]{half.X,half.Y},triangleSource="SurfaceGetArrays with published indices",original=MeshData(original),published=MeshData(result),
            originalArea,removedArea,publishedArea,insideArea,offOriginalPlane,fragmentDiagnostics},new JsonSerializerOptions{WriteIndented=true});
        Directory.CreateDirectory(Output);
        using(var file=new FileStream(Path.Combine(Output,"school-bank-published-geometry.json"),FileMode.CreateNew,System.IO.FileAccess.Write))
        using(var writer=new StreamWriter(file))writer.Write(geometry);
        Require(removedArea>.01&&insideArea<.00001&&Math.Abs(originalArea-removedArea-publishedArea)<.0002,
            "school: published bank has no area inside the room cut and preserves the complete exterior area");
        Require(exteriorTriangles>0&&missingExterior==0&&offOriginalPlane==0&&changedExteriorAttributes==0,
            "school: unchanged exterior triangles, normals, UVs and all clipped source planes are retained");
    }
    private static void Release() { foreach (var action in new[] { "move_forward", "move_backward", "move_left", "move_right", "sprint", "jump", "interact" }) Input.ActionRelease(action); }
    private async Task Press(string action) { Input.ActionPress(action); await Frames(2); Input.ActionRelease(action); await Frames(3); }
    private async Task Frames(int count) { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++; GD.Print("act1-public-buildings: " + label);
        _events.Add(new { kind = "check", label, passed = true });
    }
}
