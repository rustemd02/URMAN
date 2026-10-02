using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string MosqueVisitInteraction = "urman.chapter1:interaction/mosque-visit";
    private Node3D? _mosqueRoom;
    private readonly List<StaticBody3D> _facilityBodies = new();
    private readonly List<FacilityDoor> _facilityDoors = new();
    private readonly List<OmniLight3D> _facilityLights = new();
    private RuntimeBridge? _facilityBridge;
    private object? _facilitySession;
    private JsonElement _facilityProps;
    private bool _facilityBusy;
    private bool _facilityTickBuilt;

    private sealed class FacilityDoor
    {
        internal required string Key;
        internal required Node3D Hinge;
        internal required StaticBody3D Body;
        internal required InteractionTarget Target;
        internal required float Width;
        internal required float Height;
        internal required float RestYaw;
        internal required float OpenYaw;
        internal int GeometryVersion;
        internal Func<float, float>? LegacyAngleProjection;
        internal bool Open;
        internal bool Blocked;
    }

    /// <summary>The existing mosque shell and world position remain the owners.</summary>
    private void BuildMosqueInterior()
    {
        var complex = GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex");
        var plinth = complex.GetNode<MeshInstance3D>("MosquePlinth");
        var floor = plinth.Position + Vector3.Up * (((BoxMesh)plinth.Mesh).Size.Y * .5f + .03f);
        ExtendMosquePlinthToTerrain(plinth);
        _mosqueRoom = new Node3D { Name = "MosqueInterior", Position = floor };
        complex.AddChild(_mosqueRoom);
        complex.SetMeta("visualOnly", false);
        complex.SetMeta("navigationOwner", nameof(Act1ConnectedWorld));
        complex.SetMeta("interactionOwner", "InteractionTarget / RuntimeBridge");
        complex.SetMeta("presentationRole", "existing village mosque with accessible warm hall and vestibule");
        complex.SetMeta("safeInterior", true);
        _mosqueRoom.SetMeta("runtimeStateOwner", "RuntimeBridge/world.props");
        _mosqueRoom.SetMeta("culturalReview", "open: room arrangement and religious details require local review; no invented sacred text");
        foreach (var name in new[] { "MosqueEntranceDoor", "MosqueEntranceRecess", "MosqueEntranceStep",
            "MosqueAblutionTrough", "MosqueAblutionPost", "MosqueHallNorth", "MosqueHallSouth" })
            HidePresentationNode(complex.GetNode<Node3D>(name));

        FacilitySolid(_mosqueRoom, "MosqueTimberFloor", new(10.45f, .06f, 7.95f), new(0, -.03f, 0), "ac8e65", "wood_furniture");
        BuildMosqueDecoratedCeiling();
        for (var side = -1; side <= 1; side += 2)
        {
            var z = side * 4.25f;
            FacilitySolid(_mosqueRoom, $"MosqueWindowWall{side}Below", new(11, 1.8f, .55f), new(0, .37f, z), "c9c1a9", "plaster");
            FacilitySolid(_mosqueRoom, $"MosqueWindowWall{side}Above", new(11, 2.05f, .55f), new(0, 3.675f, z), "c9c1a9", "plaster");
            foreach (var (x, width) in new[] { (-4.4f, 2.2f), (0f, 3.8f), (4.4f, 2.2f) })
                FacilitySolid(_mosqueRoom, $"MosqueWindowPier{side}_{x}", new(width, 1.38f, .55f), new(x, 1.96f, z), "c9c1a9", "plaster");
            foreach (var x in new[] { -2.6f, 2.6f })
            {
                FacilityWindow(_mosqueRoom, $"MosqueWindow{side}_{x}", new(x, 1.96f, z), new(1.4f, 1.38f));
                FacilityRadiator(_mosqueRoom, $"MosqueRadiator{side}_{x}", new(x, .52f, side * 3.79f), side < 0 ? 0 : 180);
            }
        }
        // A short vestibule leaves wet boots and washing facilities outside the prayer hall.
        FacilitySolid(_mosqueRoom, "MosqueVestibuleNorth", new(.14f, 2.55f, 3.29f), new(2.10f, 1.275f, -2.365f), "b2a384", "wood_furniture");
        FacilitySolid(_mosqueRoom, "MosqueVestibuleSouth", new(.14f, 2.55f, 3.29f), new(2.10f, 1.275f, 2.365f), "b2a384", "wood_furniture");
        FacilitySolid(_mosqueRoom, "MosqueVestibuleLintel", new(.14f, .35f, 1.44f), new(2.10f, 2.375f, 0), "b2a384", "wood_furniture");
        FacilityBench(_mosqueRoom, "MosqueShoeBench", new(3.65f, 0, 2.90f), 1.90f, 0);
        for (var level = 0; level < 3; level++)
            FacilitySolid(_mosqueRoom, "MosqueShoeShelf" + level, new(1.65f, .05f, .36f), new(3.65f, .13f + level * .24f, 3.60f), "74604a", "wood_furniture");
        foreach (var x in new[] { 2.86f, 4.44f })
            FacilitySolid(_mosqueRoom, "MosqueShoeRackUpright" + x, new(.06f, .78f, .40f), new(x, .39f, 3.60f), "74604a", "wood_furniture");
        AddVisualBox(_mosqueRoom, "MosqueEntranceMat", new(2.7f, .012f, 1.35f), new(3.75f, .007f, 0), "5c635a", "fabric");
        // Mosque07: the old white lettering lay across the window aperture.
        // The existing solid south pier supports the whole notice, with its
        // backing against the interior wall face at Z=3.975 (not the glass).
        var courtesyBacking = AddVisualBox(_mosqueRoom, "MosqueEntranceCourtesyBacking", new(.90f, .32f, .012f),
            new(4.15f, 1.60f, 3.969f), "74604a", "wood_furniture");
        courtesyBacking.SetMeta("mountedSurface", _mosqueRoom.GetNode<MeshInstance3D>("MosqueWindowPier1_4_4").GetPath().ToString());
        AddVisualBox(_mosqueRoom, "MosqueEntranceCourtesyPaper", new(.84f, .26f, .001f),
            new(4.15f, 1.60f, 3.9625f), "e5dec8", "paper");
        FacilityLabel(_mosqueRoom, "MosqueEntranceCourtesy", "Пожалуйста,\nснимите обувь", new(4.15f, 1.60f, 3.9608f), 180, .0017f);
        var courtesyText = _mosqueRoom.GetNode<Label3D>("MosqueEntranceCourtesy");
        courtesyText.Modulate = new Color("303b32");
        courtesyText.OutlineSize = 0;
        BuildPlayerFootwearPlace();
        BuildMosqueWashCorner();
        var prayerCarpet = BuildMosquePrayerAndLibrary();
        var prayerCarpetTop = prayerCarpet.Position.Y + prayerCarpet.Mesh.GetAabb().End.Y;
        FacilitySolid(_mosqueRoom, "MosqueHeatingCabinet", new(.52f, .76f, .30f), new(4.72f, 1.74f, 3.77f), "b4bbb0", "metal");
        for (var i = 0; i < 5; i++)
            AddVisualBox(_mosqueRoom, "MosqueHeatingVent" + i, new(.34f, .012f, .014f), new(4.72f, 1.53f + i * .046f, 3.61f), "5b6661", "metal");
        FacilityRod(_mosqueRoom, "MosqueHeatSupply", new(4.65f, 1.36f, 3.79f), new(4.65f, .27f, 3.79f), .018f, "a3aaa8");
        FacilityRod(_mosqueRoom, "MosqueHeatReturn", new(4.83f, 1.36f, 3.79f), new(4.83f, .12f, 3.79f), .018f, "a3aaa8");
        FacilityLamp(_mosqueRoom, "MosqueHallLamp", new(-1.10f, 2.90f, 0), "ffdfad", 1.20f, 7.5f);
        FacilityLamp(_mosqueRoom, "MosqueVestibuleLamp", new(3.63f, 2.56f, .15f), "ffdfa8", .68f, 4.2f);

        // Mosque03: the outward sweep reached the existing landing guard post.
        // Keep the closed leaf and the guarded landing; the vestibule provides
        // the clear inward arc. Project only this door's old positive angle.
        var entrance = FacilityManualDoor(_mosqueRoom, "MosqueEntrance", "mosque/entrance", new(5.54f, 0, -.72f), 1.44f, 2.20f, 0, -95);
        entrance.GeometryVersion = 1;
        entrance.LegacyAngleProjection = angle => -angle;
        entrance.Hinge.SetMeta("geometryRepair", "Mosque03: outward leaf hit MosqueLandingPost0; same closed plane and full leaf now opens inward -95 degrees; existing landing and guards retained; legacy positive angle maps to -angle once");
        entrance.Hinge.SetMeta("addressAccessPoint", true);
        // Raising the usable floor also raises the old canopy above the new leaf.
        complex.GetNode<Node3D>("MosqueEntranceAwning").Position += Vector3.Up * .55f;
        // Mosque02: the infill began exactly at the leaf's 2.20 m top, so the
        // real 2 mm full-shape margin stopped even the first opening sample.
        // Keep its upper seam at 3.07 m and give the leaf a 20 mm head gap.
        FacilitySolid(_mosqueRoom, "MosqueDoorHeaderInfill", new(.55f, .85f, 1.70f), new(5.50f, 2.645f, 0), "c2bcab", "plaster");
        var mosqueApproach = BuildMosqueEntrySteps(complex);
        GroundMosqueCourtyard(complex);
        ExcludeMosqueHallTerrain();
        var mosqueAccess = mosqueApproach[^1];
        // The complete plate belongs to EastLeft. EastRight was physically
        // mounted, but the existing minaret obscured its outward reading view.
        var mosqueSign = _mosqueRoom.ToGlobal(new(5.789f, 1.55f, -1.50f));
        RegisterAddressedBuilding(new AddressBuildingRegistration(complex, "act1/mosque", "BLD-MOSQUE", "PAR-MOSQUE", "ADR-MOSQUE",
            "tukay", "23А", "URM-Q01-P0023", mosqueAccess, mosqueSign, Vector3.Right, ApproachPath: mosqueApproach));

        // Move the existing actor and his existing interaction together; no duplicate imam.
        var timur = GetNode<Node3D>("Act1CoreWorldGreybox/Act1People/Npc_timur_hazrat");
        timur.Reparent(_mosqueRoom, false);
        timur.Position = new(-.55f, prayerCarpetTop, -1.67f);
        // Authored character faces +Z; its prayer pose follows the same true bearing as the mihrab.
        timur.Rotation = timur.Rotation with { Y = Mathf.Atan2(MosqueLocalQibla.X, MosqueLocalQibla.Z) };
        timur.SetMeta("qiblaBearingDegrees", MosqueQiblaBearingDegrees);
        ConfigureMosqueTimurFootwear(timur);
        GeneratedCharacterKitDressing.GroundSolesOnAnchor(timur);
        var conversation = FindDescendants<InteractionTarget>(_zoneInstances["village_day"])
            .Single(target => target.InteractionId == "urman.chapter1:interaction/route-to-mosque");
        conversation.GlobalPosition = timur.GlobalPosition + Vector3.Up * .9f;
        conversation.SetMeta("facility", "mosque");
        FacilityTarget("MosqueVisit", MosqueVisitInteraction, "Осмотреть прихожую мечети", _mosqueRoom, new(3.65f, .68f, 2.90f), new(1.7f, .34f, .50f));
        EnsureFacilityTick();
    }

    private Vector3[] BuildMosqueEntrySteps(Node3D complex)
    {
        var room = _mosqueRoom!;
        // Address04 measured 2.33 m of empty air below the old approach.
        // Keep the real plinth/floor and turn the stair within the existing
        // eastern yard, clear of both the minaret and the north gate pier.
        const float left = 7.30f, right = 8.80f, center = 8.05f;
        const float landingNear = -6.00f, flightStart = -5.20f, flightEnd = -.90f;
        float GroundAt(float x, float z)
        {
            var point = room.ToGlobal(new(x, 0, z));
            return AgentBAct1HeightField.CollisionGround(point.X, point.Z) - room.GlobalPosition.Y;
        }
        float TerrainExtreme(float minX, float maxX, float minZ, float maxZ, bool maximum)
        {
            var value = maximum ? float.NegativeInfinity : float.PositiveInfinity;
            var nx = Math.Max(1, Mathf.CeilToInt((maxX - minX) / .25f));
            var nz = Math.Max(1, Mathf.CeilToInt((maxZ - minZ) / .25f));
            for (var ix = 0; ix <= nx; ix++) for (var iz = 0; iz <= nz; iz++)
            {
                var y = GroundAt(Mathf.Lerp(minX, maxX, ix / (float)nx), Mathf.Lerp(minZ, maxZ, iz / (float)nz));
                value = maximum ? Math.Max(value, y) : Math.Min(value, y);
            }
            return value;
        }
        void Stone(string name, float minX, float maxX, float minZ, float maxZ, float top)
        {
            var bottom = Math.Min(top - .08f, TerrainExtreme(minX, maxX, minZ, maxZ, maximum: false) - .045f);
            var mesh = FacilitySolid(room, name, new(maxX - minX, top - bottom, maxZ - minZ),
                new((minX + maxX) * .5f, (top + bottom) * .5f, (minZ + maxZ) * .5f), "827d6a", "stone");
            mesh.SetMeta("terrainSupportPolicy", "solid entrance masonry; sampled actual terrain to unchanged mosque floor");
        }
        var lower = TerrainExtreme(left, right, landingNear, flightStart, maximum: true) + .012f;
        var rises = Math.Max(1, Mathf.CeilToInt(-lower / .18f));
        var going = (flightEnd - flightStart) / Math.Max(1, rises - 1);
        if (lower >= -.12f || going < .26f)
            throw new InvalidOperationException("The measured mosque slope no longer fits its authored entrance stair.");
        Stone("MosqueEntryLowerLanding", left, right, landingNear, flightStart, lower);
        Stone("MosqueEntryUpperLanding", 5.25f, 8.85f, flightEnd, .22f, 0);
        Stone("MosqueEntryDoorLanding", 5.25f, 6.15f, .22f, .94f, 0);
        var approach = new List<Vector3>();
        var treadCenters = new List<Vector3>();
        Vector3 On(float x, float y, float z) => room.ToGlobal(new(x, y + .035f, z));
        var outside = On(center, GroundAt(center, -6.40f), -6.40f);
        approach.Add(outside);
        approach.Add(On(center, lower, -5.60f));
        for (var step = 1; step < rises; step++)
        {
            var top = Mathf.Lerp(lower, 0, step / (float)rises);
            var near = flightStart + (step - 1) * going;
            Stone("MosqueEntryTread" + (step - 1), left, right, near, near + going, top);
            treadCenters.Add(On(center, top, near + going * .5f));
        }
        // Path anchors are actual places to stand. A capsule on a short tread
        // may already contact the next riser, so its mesh centre is evidence of
        // geometry, not a prescribed standing pose. The path verifier checks
        // the complete flight between these landings at its normal step size.
        approach.Add(On(center, 0, -.35f));
        approach.Add(On(7.80f, 0, -.25f));

        void Rail(string name, Vector3 from, Vector3 to)
        {
            var direction = to - from;
            var rotation = new Vector3(-Mathf.Atan2(direction.Y, new Vector2(direction.X, direction.Z).Length()),
                Mathf.Atan2(direction.X, direction.Z), 0);
            FacilitySolid(room, name, new(.05f, .05f, direction.Length()), (from + to) * .5f,
                "626b64", "metal", rotation);
        }
        foreach (var x in new[] { left - .025f, right + .025f })
        {
            var firstTop = Mathf.Lerp(lower, 0, 1f / rises);
            var lastTop = Mathf.Lerp(lower, 0, (rises - 1f) / rises);
            Rail("MosqueStairHandrail" + x, new(x, firstTop + .95f, flightStart + going * .5f),
                new(x, lastTop + .95f, flightEnd - going * .5f));
            for (var step = 1; step < rises; step += 3)
            {
                var top = Mathf.Lerp(lower, 0, step / (float)rises);
                FacilitySolid(room, $"MosqueStairPost{x}_{step}", new(.05f, .95f, .05f),
                    new(x, top + .475f, flightStart + (step - .5f) * going), "626b64", "metal");
            }
        }
        Rail("MosqueLandingNorthHandrail", new(6.19f, .95f, .24f), new(8.87f, .95f, .24f));
        Rail("MosqueLandingEastHandrail", new(8.87f, .95f, flightEnd), new(8.87f, .95f, .24f));
        var landingPosts = new[] { new Vector3(6.19f, .475f, .24f), new(7.53f, .475f, .24f),
            new(8.87f, .475f, .24f), new(8.87f, .475f, flightEnd) };
        for (var post = 0; post < landingPosts.Length; post++)
            FacilitySolid(room, "MosqueLandingPost" + post, new(.05f, .95f, .05f), landingPosts[post], "626b64", "metal");

        // This old decorative bank occupied the new lower approach high above
        // its actual terrain. Retain the rest and clear only this eastern end.
        var bank = complex.GetNode<MeshInstance3D>("MosqueYardSnowBank");
        var bankSize = ((BoxMesh)bank.Mesh).Size;
        bank.Mesh = new BoxMesh { Size = bankSize with { X = 16f } };
        bank.Position += new Vector3(-.90f, 0, 0);
        bank.SetMeta("entranceClearanceRepair", "Address04: retain west bank; east edge ends at room X7.10 before actual stair approach");
        room.SetMeta("entryApproach", outside);
        room.SetMeta("entryApproachPath", approach.ToArray());
        room.SetMeta("entryTreadCenters", treadCenters.ToArray());
        room.SetMeta("entryAccessPoint", approach[^1]);
        room.SetMeta("entryRiseCount", rises);
        room.SetMeta("entryRiseHeight", -lower / rises);
        room.SetMeta("entryTreadDepth", going);
        room.SetMeta("entryRepair", "Address04: terrain-supported stair and landings; original mosque plinth, floor, door and IDs preserved");
        return approach.ToArray();
    }

    /// <summary>Called after the existing authored-contact build, before play.</summary>
    private void FinalizeFacilityContacts()
    {
        var complex = GetNode<Node3D>("Act1CoreWorldGreybox/VillageMosqueComplex");
        var proxy = complex.GetNode<StaticBody3D>("MosqueCollisionProxy");
        var replaced = new[] { "MosqueEntranceDoor", "MosqueEntranceStep", "MosqueHallNorth", "MosqueHallSouth" };
        foreach (var shape in proxy.GetChildren().OfType<CollisionShape3D>().Where(shape =>
            replaced.Any(name => shape.Name == name + "_SurfaceContact" || shape.Name == name + "_Blocker")).ToArray())
        {
            proxy.RemoveChild(shape);
            shape.Free();
        }
        CompleteMosqueCourtyardContacts(complex, proxy);
        complex.SetMeta("contactPolicy", "existing shell/plinth; pierced window walls and explicit hinged door have matching contacts");
        proxy.SetMeta("mosqueBlockerShapeCount", proxy.GetChildCount());
        complex.SetMeta("mosqueBlockerShapeCount", proxy.GetChildCount());
        RefreshFacilityState();
    }

    public string FacilityInteriorAt(Vector3 point)
    {
        if (!FacilityExteriorActive) return string.Empty;
        if (_mosqueRoom is not null)
        {
            var p = _mosqueRoom.ToLocal(point);
            if (p.X > -5.20f && p.X < 5.20f && Math.Abs(p.Z) < 3.97f && p.Y > -.25f && p.Y < 3.30f)
                return "mosque";
        }
        if (_bathhouse is not null)
        {
            var p = _bathhouse.ToLocal(point);
            if (Math.Abs(p.X) < 1.89f && Math.Abs(p.Z) < 2.39f && p.Y > -.25f && p.Y < 2.55f)
                return "bathhouse";
        }
        return PublicInteriorAt(point);
    }

    internal bool CanUseFacilityInteraction(string id)
    {
        if (!CanUsePublicBuildingInteraction(id)) return false;
        if (id is not (MosqueVisitInteraction or BathhouseObservationInteraction)) return true;
        if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player) return false;
        if (id == MosqueVisitInteraction) return FacilityInteriorAt(player.GlobalPosition) == "mosque";
        var props = _runtimeBridge?.SelectWorldProps() ?? default;
        return FacilityInteriorAt(player.GlobalPosition) == "bathhouse"
            && _bathhouse!.ToLocal(player.GlobalPosition).Z < .30f
            && YardMechanism.Flag(props, "bathhouse/steam", "condensed");
    }

    private bool FacilityExteriorActive => ActiveZoneId is "village_day" or "zirat_road" or "kara_urman_night";

    private void EnsureFacilityTick()
    {
        if (_facilityTickBuilt) return;
        _facilityTickBuilt = true;
        AddChild(new Act1FacilityPresentation
        {
            Name = "FacilityPresentation", Tick = TickFacilities,
            Detached = () =>
            {
                if (_facilityBridge is not null && IsInstanceValid(_facilityBridge))
                    _facilityBridge.RuntimeStateChanged -= RefreshFacilityState;
                _facilityBridge = null;
            }
        });
    }

    internal void ProjectLoadedFacilities()
    {
        // The save owner calls this while ordinary actions remain closed. Only
        // project the existing snapshot; do not register or dispatch an effect.
        if (_facilityBridge is null && _runtimeBridge?.ProjectionSessionIdentity is not null)
        {
            _facilityBridge = _runtimeBridge;
            _facilityBridge.RuntimeStateChanged += RefreshFacilityState;
        }
        RefreshFacilityState();
        TickFacilities(0);
    }

    private void RefreshFacilityState()
    {
        if (_facilityBridge?.ProjectionSessionIdentity is not { } session) return;
        var reset = !ReferenceEquals(_facilitySession, session);
        _facilitySession = session;
        _facilityProps = _facilityBridge.SelectWorldProps();
        foreach (var door in _facilityDoors)
        {
            door.Open = YardMechanism.Flag(_facilityProps, door.Key, "open");
            if (reset)
            {
                var legacy = door.LegacyAngleProjection is not null
                    && FacilityNumber(_facilityProps, door.Key, "geometryVersion") < door.GeometryVersion;
                var fallback = legacy ? door.LegacyAngleProjection!(door.RestYaw) : door.RestYaw;
                var angle = (float)FacilityNumber(_facilityProps, door.Key, "angle", fallback);
                if (legacy) angle = door.LegacyAngleProjection!(angle);
                door.Hinge.Rotation = new(0, Mathf.Clamp(angle, Math.Min(door.RestYaw, door.OpenYaw), Math.Max(door.RestYaw, door.OpenYaw)), 0);
                door.Blocked = false;
            }
        }
        ApplyBathhouseState();
        ApplyPlayerFootwearState();
    }

    private void TickFacilities(double delta)
    {
        if (_facilityBridge is null && _runtimeBridge?.SessionIdentity is not null)
        {
            _facilityBridge = _runtimeBridge;
            _facilityBridge.RuntimeStateChanged += RefreshFacilityState;
            RefreshFacilityState();
        }
        if (_facilityBridge is null) return;
        var exterior = FacilityExteriorActive;
        foreach (var body in _facilityBodies) body.CollisionLayer = exterior ? 2u : 0u;
        foreach (var light in _facilityLights) light.Visible = exterior;
        var paused = _facilityBridge.CapturePlayTimeBlocks() != RuntimeBridge.PlayTimeBlock.None;
        foreach (var door in _facilityDoors)
        {
            var next = Mathf.MoveToward(door.Hinge.Rotation.Y, door.Open ? door.OpenYaw : door.RestYaw, (float)delta * 2.8f);
            if (!paused && !Mathf.IsEqualApprox(next, door.Hinge.Rotation.Y))
            {
                door.Blocked = !FacilityDoorSweepClear(door, door.Hinge.Rotation.Y, next);
                if (!door.Blocked) door.Hinge.Rotation = new(0, next, 0);
            }
            door.Target.GlobalTransform = door.Hinge.GlobalTransform * new Transform3D(Basis.Identity, new(0, door.Height * .5f, door.Width * .5f));
            door.Target.Prompt = door.Blocked ? "Дверь упёрлась — отступить или убрать вещь" : door.Open ? "Закрыть дверь" : "Открыть дверь";
            door.Target.CollisionLayer = exterior ? 4u : 0u;
        }
        TickBathhouse(paused);
        TickPlayerFootwear();
    }

    private async Task<bool> CommitFacilityProps(JsonArray changes, string message, Vector3 source, string sound = "")
    {
        if (_facilityBusy || !FacilityExteriorActive || _runtimeBridge?.SessionIdentity is not { } session) return false;
        _facilityBusy = true;
        try
        {
            var ok = await _runtimeBridge.DispatchWorldPropsAsync(changes);
            if (!ok || !ReferenceEquals(session, _runtimeBridge.SessionIdentity) || !IsInsideTree()) return false;
            RefreshFacilityState();
            if (!string.IsNullOrEmpty(sound)) UiFoley.PlayWorld(this, source, sound);
            if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player) player.NotifyTraversal(message);
            // This is the existing session-selected store, including guarded debug saves.
            await _runtimeBridge.SaveSlotAsync(RuntimeBridge.CheckpointSlot);
            return ReferenceEquals(session, _runtimeBridge.SessionIdentity);
        }
        finally { _facilityBusy = false; }
    }

    private FacilityDoor FacilityManualDoor(Node3D parent, string name, string key, Vector3 hingeAt,
        float width, float height, float restDegrees, float swingDegrees)
    {
        var hinge = new Node3D { Name = name + "Hinge", Position = hingeAt, RotationDegrees = new(0, restDegrees, 0) };
        parent.AddChild(hinge);
        // The rendered leaf and contact share a real 12 mm floor clearance.
        var body = FacilitySolid(hinge, name + "Leaf", new(.065f, height - .012f, width), new(0, (height + .012f) * .5f, width * .5f), "806044", "wood_furniture");
        body.SetMeta("worldPropId", key);
        for (var i = 1; i < 5; i++)
            AddVisualBox(hinge, name + "PlankJoint" + i, new(.069f, height - .04f, .008f), new(0, height * .5f, width * i / 5f), "62472f", "wood");
        foreach (var y in new[] { .25f, height - .25f })
        {
            FacilityRod(hinge, name + "HingePin" + y, new(0, y - .09f, .015f), new(0, y + .09f, .015f), .025f, "454a45");
            AddVisualBox(hinge, name + "HingeStrap" + y, new(.02f, .075f, .30f), new(-.045f, y, .16f), "454a45", "metal");
        }
        // Both grips keep their existing outer envelope. Their small fittings
        // occupy the former air gap to the leaf, with each rosette embedded
        // 1 mm into the wood. Batch the visual hardware into one mesh/surface.
        using (var fittings = new SurfaceTool())
        using (var rosette = new CylinderMesh { TopRadius = .020f, BottomRadius = .020f, Height = .0085f, RadialSegments = 12, Rings = 1 })
        using (var foot = new CylinderMesh { TopRadius = .0095f, BottomRadius = .0095f, Height = .061f, RadialSegments = 12, Rings = 1 })
        using (var screw = new CylinderMesh { TopRadius = .0025f, BottomRadius = .0025f, Height = .0025f, RadialSegments = 8, Rings = 1 })
        {
            fittings.Begin(Mesh.PrimitiveType.Triangles);
            var alongX = new Basis(Vector3.Forward, Mathf.Pi * .5f);
            foreach (var x in new[] { -.10f, .10f })
            {
                FacilityRod(hinge, name + "Handle" + x, new(x, .91f, width - .16f), new(x, 1.07f, width - .16f), .023f, "554f3e");
                var side = Math.Sign(x);
                foreach (var y in new[] { .935f, 1.045f })
                {
                    fittings.AppendFrom(rosette, 0, new(alongX, new(side * .03575f, y, width - .16f)));
                    fittings.AppendFrom(foot, 0, new(alongX, new(side * .0695f, y, width - .16f)));
                    foreach (var offset in new[] { -.013f, .013f })
                        fittings.AppendFrom(screw, 0, new(alongX, new(side * .04125f, y + offset, width - .16f)));
                }
            }
            fittings.Index();
            hinge.AddChild(new MeshInstance3D { Name = name + "HandleFittings", Mesh = fittings.Commit(),
                MaterialOverride = PainterlyMaterialLibrary.ForColor("554f3e", "metal") });
        }
        var target = FacilityTarget(name + "Use", "urman.chapter1:local/" + key, "Открыть дверь", hinge,
            new(0, height * .5f, width * .5f), new(.115f, height, width + .035f));
        var door = new FacilityDoor { Key = key, Hinge = hinge, Body = hinge.GetNode<StaticBody3D>(name + "LeafBody"), Target = target, Width = width, Height = height,
            RestYaw = Mathf.DegToRad(restDegrees), OpenYaw = Mathf.DegToRad(restDegrees + swingDegrees) };
        // Presence owns ray routing; the action itself rejects in-flight input.
        // A save commits while busy, so routing must not strand this target.
        target.PresentationRepeatAvailable = () => FacilityExteriorActive;
        target.PresentationRepeat = () => _ = ToggleFacilityDoor(door);
        _facilityDoors.Add(door);
        return door;
    }

    private async Task ToggleFacilityDoor(FacilityDoor door)
    {
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        door.Target.SetMeta("lastDoorActionNumber", door.Target.GetMeta("lastDoorActionNumber", 0).AsInt32() + 1);
        door.Target.SetMeta("lastDoorActionResult", _facilityBusy ? "busy" : _runtimeBridge is null ? "no-runtime" : player is null ? "no-player" : "received");
        door.Target.SetMeta("lastDoorAction", JsonSerializer.Serialize(new {
            phase = "received", key = door.Key, busy = _facilityBusy,
            sessionReady = _runtimeBridge?.SessionIdentity is not null,
            feet = player?.GlobalPosition.ToString(), focus = DisplayServer.WindowIsFocused(),
            forward = Input.GetActionStrength("move_forward"), backward = Input.GetActionStrength("move_backward"),
            left = Input.GetActionStrength("move_left"), right = Input.GetActionStrength("move_right") }));
        if (_facilityBusy || _runtimeBridge is null || player is null) return;
        var destination = door.Open ? door.RestYaw : door.OpenYaw;
        if (!FacilityDoorSweepClear(door, door.Hinge.Rotation.Y, destination, recordProbe: true))
        {
            door.Target.SetMeta("lastDoorActionResult", "blocked-by-full-shape-sweep");
            player.NotifyTraversal("Створке мешает человек или вещь. Нужно освободить место перед дверью.");
            return;
        }
        var committed = await CommitFacilityProps(new JsonArray { FacilityDoorSnapshot(door, !door.Open) },
            door.Open ? "Дверь закрыта." : "Дверь открыта.", door.Hinge.GlobalPosition + Vector3.Up, "door_creak");
        if (IsInsideTree()) door.Target.SetMeta("lastDoorActionResult", committed ? "committed" : "commit-rejected");
    }

    private bool FacilityDoorSweepClear(FacilityDoor door, float from, float to, bool recordProbe = false)
    {
        // Tip travel between samples is below half the leaf thickness. The
        // complete shape is checked again every physics tick, including the
        // player entering its path after the initial button press.
        var count = Math.Max(1, Mathf.CeilToInt(Math.Abs(to - from) * door.Width / .018f));
        var size = new Vector3(.065f, door.Height - .012f, door.Width);
        using var shape = new BoxShape3D { Size = size };
        using var query = new PhysicsShapeQueryParameters3D { Shape = shape, CollisionMask = 3,
            Exclude = new global::Godot.Collections.Array<Rid> { door.Body.GetRid() }, Margin = .002f };
        var owner = door.Hinge.GetParent<Node3D>();
        var space = door.Hinge.GetWorld3D().DirectSpaceState;
        var held = (GetTree().GetFirstNodeInGroup("carry_coordinator") as CarryCoordinator)?.HeldItem;
        for (var i = 1; i <= count; i++)
        {
            var yaw = Mathf.Lerp(from, to, (float)i / count);
            var hinge = owner.GlobalTransform * new Transform3D(new Basis(Vector3.Up, yaw), door.Hinge.Position);
            query.Transform = hinge * new Transform3D(Basis.Identity, new(0, (door.Height + .012f) * .5f, door.Width * .5f));
            var hits = space.IntersectShape(query, 1);
            if (hits.Count != 0)
            {
                if (recordProbe)
                {
                    var hit = hits[0];
                    var collider = hit["collider"].AsGodotObject() as Node;
                    var shapeIndex = hit["shape"].AsInt32();
                    var contact = collider is CollisionObject3D physical
                        ? physical.ShapeOwnerGetOwner(physical.ShapeFindOwner(shapeIndex)) as Node : null;
                    var source = contact?.HasMeta("authoredSourceMesh") == true ? contact
                        : collider?.HasMeta("authoredSourceMesh") == true ? collider : null;
                    door.Target.SetMeta("doorSweepProbe", JsonSerializer.Serialize(new {
                        clear = false, sample = i, samples = count, yaw, size = size.ToString(),
                        pose = query.Transform.ToString(), collider = collider?.GetPath().ToString(), shapeIndex,
                        shapePath = contact?.GetPath().ToString(),
                        authoredSourceMesh = source?.GetMeta("authoredSourceMesh").AsString() }));
                }
                return false;
            }
            // Held things intentionally have no physics layer; their same full
            // collision box must still stop the leaf.
            if (held is not null && FacilityVolumesOverlap(query.Transform, size,
                held.GlobalTransform * new Transform3D(Basis.Identity, Vector3.Up * held.Size.Y * .5f), held.Size))
            {
                if (recordProbe) door.Target.SetMeta("doorSweepProbe", JsonSerializer.Serialize(new {
                    clear = false, sample = i, samples = count, yaw, size = size.ToString(),
                    pose = query.Transform.ToString(), heldItem = held.ItemId, heldPose = held.GlobalTransform.ToString() }));
                return false;
            }
        }
        if (recordProbe) door.Target.SetMeta("doorSweepProbe", JsonSerializer.Serialize(new { clear = true, samples = count, from, to }));
        return true;
    }

    private static bool FacilityVolumesOverlap(Transform3D a, Vector3 aSize, Transform3D b, Vector3 bSize)
    {
        var aAxis = new[] { a.Basis.X, a.Basis.Y, a.Basis.Z };
        var bAxis = new[] { b.Basis.X, b.Basis.Y, b.Basis.Z };
        var axes = aAxis.Concat(bAxis).Concat(aAxis.SelectMany(left => bAxis.Select(left.Cross)));
        var center = b.Origin - a.Origin;
        foreach (var axis in axes)
        {
            if (axis.LengthSquared() < .000001f) continue;
            var aRadius = .5f * (Math.Abs(axis.Dot(aAxis[0])) * aSize.X + Math.Abs(axis.Dot(aAxis[1])) * aSize.Y + Math.Abs(axis.Dot(aAxis[2])) * aSize.Z);
            var bRadius = .5f * (Math.Abs(axis.Dot(bAxis[0])) * bSize.X + Math.Abs(axis.Dot(bAxis[1])) * bSize.Y + Math.Abs(axis.Dot(bAxis[2])) * bSize.Z);
            if (Math.Abs(axis.Dot(center)) > aRadius + bRadius + .002f * axis.Length()) return false;
        }
        return true;
    }

    internal async Task<bool> FlushFacilitiesForSaveAsync()
    {
        if (_runtimeBridge?.SessionIdentity is not { } session) return false;
        var changes = new JsonArray();
        foreach (var door in _facilityDoors)
            changes.Add(FacilityDoorSnapshot(door, door.Open));
        return changes.Count == 0 || await _runtimeBridge.DispatchWorldPropsAsync(changes) && ReferenceEquals(session, _runtimeBridge.SessionIdentity);
    }

    private static JsonObject FacilityDoorSnapshot(FacilityDoor door, bool open)
    {
        var state = new JsonObject { ["propId"] = door.Key, ["open"] = open, ["angle"] = door.Hinge.Rotation.Y };
        // Publish the new pose and its convention together through the existing
        // world.props transaction. Projection never rewrites an old snapshot.
        if (door.GeometryVersion > 0) state["geometryVersion"] = door.GeometryVersion;
        return state;
    }

    private InteractionTarget FacilityTarget(string name, string id, string prompt, Node3D anchor, Vector3 at, Vector3 size)
    {
        var target = new InteractionTarget { Name = name, InteractionId = id, Prompt = prompt, CollisionLayer = 4, CollisionMask = 0 };
        target.AddChild(new CollisionShape3D { Name = "SurfaceRay", Shape = new BoxShape3D { Size = size } });
        _zoneInstances["village_day"].AddChild(target);
        target.GlobalTransform = anchor.GlobalTransform * new Transform3D(Basis.Identity, at);
        target.SetMeta("runtimeStateOwner", "RuntimeBridge");
        target.SetMeta("facilityTarget", true);
        return target;
    }

    private MeshInstance3D FacilitySolid(Node3D parent, string name, Vector3 size, Vector3 at, string color, string surface,
        Vector3 rotation = default)
    {
        var mesh = AddVisualBox(parent, name, size, at, color, surface);
        // Apply the same pose through the actual references: Godot may sanitize
        // punctuation in generated node names before they become NodePaths.
        mesh.Rotation = rotation;
        var body = new StaticBody3D { Name = name + "Body", Position = at, Rotation = rotation, CollisionLayer = 2, CollisionMask = 0 };
        body.SetMeta("collisionOwner", "act1-exterior-architecture");
        body.SetMeta("authoredSourceMesh", mesh.GetPath().ToString());
        body.SetMeta("facilityContact", true);
        body.AddChild(new CollisionShape3D { Name = "Contact", Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        _facilityBodies.Add(body);
        return mesh;
    }

    private void FacilityBench(Node3D parent, string name, Vector3 at, float width, float yaw)
    {
        var bench = RuralPropModels.Bench(parent, name, at, yaw, width, backrest: false);
        var body = new StaticBody3D { Name = "SeatContact" };
        body.AddChild(new CollisionShape3D { Position = new(0, .445f, 0), Shape = new BoxShape3D { Size = new(width, .075f, .425f) } });
        bench.AddChild(body);
        _facilityBodies.Add(body);

    }

    private void FacilityTable(Node3D parent, string name, Vector3 at, Vector3 size)
    {
        var table = new Node3D { Name = name, Position = at };
        parent.AddChild(table);
        FacilitySolid(table, "Top", new(size.X, .045f, size.Z), new(0, size.Y, 0), "8d7453", "wood_furniture");
        foreach (var x in new[] { -.5f, .5f }) foreach (var z in new[] { -.5f, .5f })
            FacilitySolid(table, $"Leg{x}_{z}", new(.055f, size.Y - .025f, .055f), new(x * (size.X - .13f), (size.Y - .025f) * .5f, z * (size.Z - .13f)), "63523f", "wood_furniture");
    }

    private void FacilityWindow(Node3D parent, string name, Vector3 center, Vector2 size)
    {
        var window = new Node3D { Name = name, Position = center };
        parent.AddChild(window);
        foreach (var x in new[] { -size.X * .5f, 0, size.X * .5f })
            FacilitySolid(window, "Mullion" + x, new(.065f, size.Y + .12f, .12f), new(x, 0, 0), "dacfb5", "wood_furniture");
        foreach (var y in new[] { -size.Y * .5f, size.Y * .5f })
            FacilitySolid(window, "Rail" + y, new(size.X + .12f, .065f, .12f), new(0, y, 0), "dacfb5", "wood_furniture");
        var glass = FacilitySolid(window, "Glass", new(size.X, size.Y, .018f), Vector3.Zero, "b6c8cb", "glass");
        glass.MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(.66f, .77f, .78f, .19f), Roughness = .12f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    }

    private void FacilityRadiator(Node3D parent, string name, Vector3 at, float yaw)
    {
        var radiator = RuralPropModels.Radiator(parent, name, at - Vector3.Up * .3f, .86f, yaw);
        var body = new StaticBody3D { Name = "RadiatorContact" };
        body.AddChild(new CollisionShape3D { Position = new(0, .3f, 0), Shape = new BoxShape3D { Size = new(.88f, .55f, .15f) } });
        radiator.AddChild(body);
        _facilityBodies.Add(body);

    }

    private void FacilityLamp(Node3D parent, string name, Vector3 at, string color, float energy, float range)
    {
        var light = new OmniLight3D { Name = name, Position = at, LightColor = Color.FromHtml(color), LightEnergy = energy,
            OmniRange = range, ShadowEnabled = true, ShadowBias = .03f };
        parent.AddChild(light);
        _facilityLights.Add(light);
        DiscoveryCylinder(parent, name + "Shade", .17f, .22f, .10f, at + Vector3.Up * .08f, "d5cbb1");
        DiscoveryCylinder(parent, name + "Diffuser", .14f, .14f, .025f, at + Vector3.Up * .015f, "f1dfb8");
    }

    private static void FacilityRod(Node3D parent, string name, Vector3 a, Vector3 b, float radius, string color)
    {
        var delta = b - a;
        var mesh = DiscoveryCylinder(parent, name, radius, radius, delta.Length(), (a + b) * .5f, color);
        mesh.Quaternion = new Quaternion(Vector3.Up, delta.Normalized());
    }

    private static Node3D FacilityVessel(Node3D parent, string name, Vector3 at, float radius, float height, string color, bool water)
    {
        var vessel = new Node3D { Name = name, Position = at };
        parent.AddChild(vessel);
        var outer = new MeshInstance3D { Name = "Bowl", Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * .76f,
            Height = height, RadialSegments = 20, CapTop = false, CapBottom = true }, MaterialOverride = PainterlyMaterialLibrary.ForColor(color, "metal") };
        vessel.AddChild(outer);
        DiscoveryCylinder(vessel, "InnerBottom", radius * .73f, radius * .73f, .012f, new(0, -height * .44f, 0), color);
        if (water) DiscoveryCylinder(vessel, "Water", radius * .88f, radius * .88f, .006f, new(0, height * .27f, 0), "9caeae");
        return vessel;
    }

    private static void FacilityLabel(Node3D parent, string name, string text, Vector3 at, float yaw, float pixelSize)
    {
        parent.AddChild(new Label3D { Name = name, Text = text, Position = at, RotationDegrees = new(0, yaw, 0), FontSize = 40,
            PixelSize = pixelSize, Modulate = new Color("e3dcc7"), OutlineModulate = new Color("3d4540"), OutlineSize = 3 });
    }

    private static double FacilityNumber(JsonElement props, string key, string field, double fallback = 0) =>
        props.ValueKind == JsonValueKind.Object && props.TryGetProperty(key, out var record) && record.ValueKind == JsonValueKind.Object
        && record.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : fallback;
}
