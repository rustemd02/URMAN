using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private const string ObservationInteractionPrefix = "urman.chapter1:interaction/";
    private readonly Dictionary<string, Func<Camera3D, bool>> _observationGuards = new(StringComparer.Ordinal);
    private readonly global::Godot.Collections.Array<Rid> _observationRayExclusions = new();
    private MeshInstance3D? _observationTagSnow;
    private Node3D? _photoGateInsert;
    private Action? _refreshSketchObservationMetadata;

    // Keep geometry out of the cached narrative availability. FPC checks this
    // live; RuntimeBridge checks the same predicate immediately before dispatch.
    // These checks never grant knowledge or change the player's route.
    internal bool CanUseObservationInteraction(string interactionId)
    {
        if (!_observationGuards.TryGetValue(interactionId, out var accepts)) return true;
        var player = GetTree().GetFirstNodeInGroup("player_controller") as FirstPersonController;
        var camera = player?.GetNodeOrNull<Camera3D>("Head/Camera3D");
        if (camera is null || !camera.IsInsideTree()) return false;
        if (!_observationRayExclusions.Contains(player!.GetRid()))
            _observationRayExclusions.Add(player.GetRid());
        return accepts(camera);
    }

    private void BuildAct1ImageDiscoveries()
    {
        if (_observationGuards.Count != 0) return;
        var street = (StyleBenchmarkZone)_zoneInstances["village_day"];
        var core = GetNode<Node3D>("Act1CoreWorldGreybox");
        BuildObservationQuestions(street, core);
        BuildPhotoPlaceObservation(street, core);
        BuildSketchPlaceObservation((StyleBenchmarkZone)_zoneInstances["zirat_road"]);
        BuildCuriosityReturns(street);

        // Semantic ray proxies are not visual obstructions. Real walls, ground
        // and furniture remain in the visibility ray; this does not waive reach.
        foreach (var target in FindDescendants<InteractionTarget>(this))
            _observationRayExclusions.Add(target.GetRid());
    }

    private void BuildObservationQuestions(StyleBenchmarkZone street, Node3D core)
    {
        var well = ObservationExistingTarget("discover-arrival-insulated-well");
        var hook = core.GetNode<Node3D>("MainStreet/DiscoveryArrivalWellDetail/PolishedHook");
        var wellQuestion = ObservationTarget(street, "observe-well-tie", new(1.6f, 1.7f, 1.2f), well.GlobalPosition);
        wellQuestion.DialogueId = "urman.chapter1:dialogue/well_observation";
        ObserveGuard(wellQuestion, "Подойти к варежке и посмотреть на её крепление.",
            camera => ObservationNear(camera, wellQuestion.GlobalPosition, 3.2f));
        ObserveGuard(well, "Спереди крепления не видно. Обойди колодец и посмотри на крючок сзади.",
            camera => camera.GlobalPosition.Z < hook.GlobalPosition.Z - .30f
                && ObservationNear(camera, hook.GlobalPosition, 2.8f));
        well.SetMeta("observationReferenceEye", hook.GlobalPosition + new Vector3(0, .35f, -1.5f));
        well.SetMeta("observationLookAt", hook.GlobalPosition);

        var bench = ObservationExistingTarget("discover-zirat-outer-rest-bench");
        var benchQuestion = ObservationTarget(street, "observe-bench-plank", new(1.9f, .20f, .52f), bench.GlobalPosition);
        benchQuestion.DialogueId = "urman.chapter1:dialogue/bench_observation";
        ObserveGuard(benchQuestion, "Нужно рассмотреть светлую полоску на сиденье.",
            camera => ObservationNear(camera, bench.GlobalPosition + Vector3.Up * .06f, 3f));
        ObserveGuard(bench, "Подойди к сиденью и посмотри на снег сверху; поддевать прибитую планку не нужно.",
            camera => camera.GlobalPosition.Y > bench.GlobalPosition.Y + .30f
                && ObservationNear(camera, bench.GlobalPosition + Vector3.Up * .06f, 2.4f));
        bench.SetMeta("observationReferenceEye", bench.GlobalPosition + new Vector3(0, .95f, 1.15f));
        bench.SetMeta("observationLookAt", bench.GlobalPosition + Vector3.Up * .06f);

        var profile = ObservationExistingTarget("discover-kara-branch-profile");
        var profileQuestion = ObservationTarget(street, "observe-branch-profile", new(2.1f, 2.25f, 1.65f), profile.GlobalPosition);
        profileQuestion.DialogueId = "urman.chapter1:dialogue/profile_observation";
        var firstStem = ObservationFindNode("KaraBranchProfileTrunk");
        var secondStem = ObservationFindNode("KaraBranchProfileSecondaryStemBase");
        var stemA = ObservationMeshCenter(firstStem);
        var stemB = ObservationMeshCenter(secondStem);
        var betweenStems = (stemA + stemB) * .5f;
        var separation = ObservationHorizontal(stemA - stemB).Normalized();
        ObserveGuard(profileQuestion, "Остановись на тропе и проследи контур у двух стволов.",
            camera => ObservationNear(camera, profile.GlobalPosition, 4.3f));
        ObserveGuard(profile, "С этого ракурса линии перекрываются. Сместись вдоль доступной тропы и проследи оба основания.",
            camera =>
            {
                var side = ObservationHorizontal(camera.GlobalPosition - betweenStems);
                return side.Length() is > .85f and < 3.8f
                    && Math.Abs(side.Normalized().Dot(separation)) < .73f
                    && camera.GlobalPosition.X < betweenStems.X + .45f
                    && camera.GlobalPosition.Z > betweenStems.Z - 2.8f
                    && ObservationNear(camera, betweenStems + Vector3.Up * .25f, 4.2f, .72f)
                    && ObservationVisible(camera.GlobalPosition, stemA)
                    && ObservationVisible(camera.GlobalPosition, stemB);
            });
        profile.SetMeta("observationReferenceEye", betweenStems + new Vector3(-2.1f, 1.1f, -1.65f));
        profile.SetMeta("observationLookAt", betweenStems + Vector3.Up * .25f);
    }

    private void BuildPhotoPlaceObservation(StyleBenchmarkZone street, Node3D core)
    {
        var presentation = core.GetNode<Node3D>("Act1AuthoredExteriorKitPresentation");
        var facade = presentation.GetNode<Node3D>("BabaiApproachDwellingFacade");
        var gate = presentation.GetNode<Node3D>("BabaiYardAuthoredGate");
        var birch = core.GetNode<Node3D>("BabaiEbiYard/BabaiYardBirchMass");
        var glasses = Enumerable.Range(1, 3).Select(index =>
            FindDescendants<MeshInstance3D>(facade).Single(mesh =>
                mesh.Name == $"HeroHouse_Street_Window{index}_Glass_LOD0")).ToArray();
        var brace = FindDescendants<MeshInstance3D>(gate).Single(mesh => mesh.Name == "Gate_CrossBrace_LOD0");
        var front = ObservationHorizontal(facade.GlobalTransform.Basis.Z).Normalized();
        var gateCenter = ObservationMeshCenter(brace);
        var windows = glasses.Select(mesh =>
        {
            var bounds = mesh.Mesh!.GetAabb();
            return mesh.GlobalTransform * (bounds.Position + bounds.Size * new Vector3(.5f, .82f, .5f))
                + front * .06f;
        }).ToArray();
        var birchPoint = birch.GlobalPosition + Vector3.Up * 1.8f;

        // The shed hides the far window from the gate. The player can inspect
        // the real facade separately, then compare the entrance with that
        // remembered observation. No landmark is accepted through a solid wall.
        var facadeSide = ObservationHorizontal(facade.GlobalTransform.Basis.X).Normalized();
        var facadeView = ObservationTarget(street, "observe-photo-facade-windows", new(.60f, .38f, .13f),
            windows[0] + front * .03f);
        facadeView.JournalEntryId = "urman.chapter1:knowledge/clue_photo_facade_windows_observed";
        facadeView.Rotation = new Vector3(0, facade.GlobalRotation.Y, 0);
        ObserveGuard(facadeView, "Посмотри на три окна с открытой стороны длинного фасада.",
            camera => ObservationHorizontal(camera.GlobalPosition - windows[1]).Dot(front) > .35f
                && windows.All(point => ObservationFramed(camera, point)));
        facadeView.SetMeta("observationLandmarks", new global::Godot.Collections.Array<Vector3>(windows));
        facadeView.SetMeta("observationReferenceEye", windows[1] + front * 1.2f - facadeSide * 2.4f);
        facadeView.SetMeta("observationLookAt", windows[0]);

        // A worn repair is a visible ordinary detail, not an invisible unlock.
        // Its date, maker and relationship to any plot event remain unspecified.
        _photoGateInsert = new Node3D { Name = "ImageGateLowerInsert" };
        gate.AddChild(_photoGateInsert);
        _photoGateInsert.Position = new Vector3(0, .29f, .255f);
        AddVisualBox(_photoGateInsert, "PaleWoodInsert", new(1.86f, .22f, .035f), Vector3.Zero, "b39a76", "wood");
        foreach (var x in new[] { -.72f, .72f })
        {
            DiscoveryCylinder(_photoGateInsert, $"Fastener{x}", .015f, .015f, .007f,
                new(x, .015f, .026f), "665647").RotationDegrees = new(90, 0, 0);
        }
        AddVisualBox(_photoGateInsert, "UpperSeam", new(1.9f, .010f, .012f), new(0, .116f, .02f), "514638", "wood");

        var view = ObservationTarget(street, "observe-photo-yard", new(4.8f, 2.1f, .24f), gateCenter + front * .40f);
        view.JournalEntryId = "urman.chapter1:knowledge/clue_photo_yard_observed";
        view.Rotation = new Vector3(0, facade.GlobalRotation.Y, 0);
        ObserveGuard(view, "Сверь берёзу и диагональную калитку у входа. Окна можно сначала осмотреть с открытой стороны фасада.",
            camera =>
            {
                var offset = ObservationHorizontal(camera.GlobalPosition - gateCenter);
                return offset.Length() is > 1.3f and < 7.2f
                    && offset.Dot(front) > 1.1f
                    && ObservationFramed(camera, birchPoint)
                    && ObservationFramed(camera, gateCenter)
                    && (windows.All(point => ObservationFramed(camera, point))
                        || ObservationKnown("clue_photo_facade_windows_observed"));
            });
        view.SetMeta("observationLandmarks", new global::Godot.Collections.Array<Vector3>(
            new[] { birchPoint, gateCenter }.Concat(windows)));
        view.SetMeta("observationReferenceEye", gateCenter + front * 3.1f + new Vector3(-.8f, .7f, 0) - facadeSide);
        view.SetMeta("observationLookAt", (birchPoint + gateCenter + windows[1]) / 3f);

        var repairPoint = _photoGateInsert.GlobalPosition + front * .07f;
        var repair = ObservationTarget(street, "inspect-photo-gate-repair", new(1.95f, .31f, .12f), repairPoint);
        repair.Rotation = new Vector3(0, facade.GlobalRotation.Y, 0);
        repair.JournalEntryId = "urman.chapter1:knowledge/clue_photo_gate_repair_checked";
        ObserveGuard(repair, "Подойди к нижней вставке и рассмотри стык с креплениями.",
            camera => ObservationNear(camera, repairPoint, 2.4f)
                && ObservationHorizontal(camera.GlobalPosition - gateCenter).Dot(front) > .25f);
        repair.SetMeta("observationReferenceEye", repairPoint + front * 1.05f + Vector3.Up * 1.25f);
        repair.SetMeta("observationLookAt", repairPoint);
    }

    private void BuildSketchPlaceObservation(StyleBenchmarkZone zirat)
    {
        var tag = zirat.GetNode<MeshInstance3D>("ZiratRouteTraceTag");
        var markerPoint = ObservationMeshCenter(tag);
        // The logical benchmark kit is hidden in the connected world. Bind to
        // the core presentation and to the bank/picket beside the actual tag;
        // the ditch's broad relief and water meshes are deliberately suppressed.
        var presentation = GetNode<Node3D>(
            "Act1CoreWorldGreybox/ZiratMemoryField/ZiratRoadsideAuthoredKitPresentation");
        var ditchAnchors = ObservationSurfaceAnchors(
            presentation.GetNode<Node3D>("ZiratRoadsideDitch/RoadsideDitch"), "RoadsideDitch_Bank_03");
        var fenceAnchors = ObservationSurfaceAnchors(
            presentation.GetNode<Node3D>("ZiratAuthoredBoundaryFence/ZiratBoundaryFence"), "ZiratBoundaryFence_Picket_West_05");
        var view = ObservationTarget(zirat, "observe-sketch-landmarks", new(2.5f, 1.1f, .20f), markerPoint);
        view.JournalEntryId = "urman.chapter1:knowledge/clue_sketch_field_landmarks";

        bool ReadLandmarks(Camera3D? camera, out Vector3 ditchPoint, out Vector3 fencePoint)
        {
            ditchPoint = fencePoint = default;
            if (!ObservationMeshPresent(tag, camera)) return false;
            var ditch = ditchAnchors.FirstOrDefault(anchor => ObservationMeshPresent(anchor.Mesh, camera));
            var fence = fenceAnchors.FirstOrDefault(anchor => ObservationMeshPresent(anchor.Mesh, camera));
            if (ditch.Mesh is null || fence.Mesh is null) return false;
            markerPoint = ObservationMeshCenter(tag);
            // Roadside grounding runs later in world construction. Keep points
            // in mesh space and resolve their current transform on every use.
            ditchPoint = ditch.Mesh.GlobalTransform * ditch.LocalPoint;
            fencePoint = fence.Mesh.GlobalTransform * fence.LocalPoint;
            view.SetMeta("observationLandmarkPaths", string.Join('|',
                tag.GetPath().ToString(), ditch.Mesh.GetPath().ToString(), fence.Mesh.GetPath().ToString()));
            return true;
        }

        void RefreshMetadata()
        {
            if (!ReadLandmarks(null, out var ditchPoint, out var fencePoint)) return;
            var eye = new Vector3(ditchPoint.X + .4f, 0f, markerPoint.Z + 4.7f);
            // first_person_player.tscn places the standing Head 1.70 m above feet.
            eye.Y = AgentBAct1HeightField.CollisionGround(eye.X, eye.Z) + 1.7f;
            var lookAt = (markerPoint + ditchPoint + fencePoint) / 3f;
            view.GlobalPosition = eye + (lookAt - eye).Normalized() * 2f;
            view.SetMeta("observationLandmarks", new global::Godot.Collections.Array<Vector3>(new[] { markerPoint, ditchPoint, fencePoint }));
            view.SetMeta("observationReferenceEye", eye);
            view.SetMeta("observationLookAt", lookAt);
        }

        _refreshSketchObservationMetadata = RefreshMetadata;
        RefreshMetadata();
        CallDeferred(nameof(UpdateAct1ImageDiscoveries));
        ObserveGuard(view, "Отойди на дорогу снаружи ограды, чтобы увидеть бирку, канаву и ограду вместе.",
            camera => ReadLandmarks(camera, out var ditchPoint, out var fencePoint)
                && camera.GlobalPosition.X > ditchPoint.X + .1f
                && camera.GlobalPosition.X < fencePoint.X - .30f
                && camera.GlobalPosition.Z > markerPoint.Z + .70f
                && ObservationHorizontal(camera.GlobalPosition - markerPoint).Length() < 8.3f
                && camera.ToLocal(markerPoint).X < 0f
                && camera.ToLocal(fencePoint).X > 0f
                // Compare the landmarks with each other. The ditch may fall
                // just beside the crosshair when an ordinary walking step ends;
                // that must not turn a clear view into a centimetre-wide puzzle.
                && camera.UnprojectPosition(markerPoint).X < camera.UnprojectPosition(ditchPoint).X
                && camera.UnprojectPosition(ditchPoint).X < camera.UnprojectPosition(fencePoint).X
                && ObservationFramed(camera, markerPoint)
                && ObservationFramed(camera, ditchPoint)
                && ObservationFramed(camera, fencePoint));

        var detail = new Node3D { Name = "ImageSketchTagBase", Position = new(0, -.11f, .035f) };
        tag.AddChild(detail);
        foreach (var x in new[] { -.03f, .03f })
            AddVisualBox(detail, $"WireSide{x}", new(.009f, .065f, .010f), new(x, -.013f, .008f), "78604a", "metal");
        AddVisualBox(detail, "WireBottom", new(.068f, .009f, .010f), new(0, -.045f, .008f), "78604a", "metal");
        // This optional lower-edge detail stays below BOTH existing notches.
        // Clearing it is independent of the required roadside source and field view.
        _observationTagSnow = AddVisualBox(detail, "SnowBelowNotches", new(.20f, .070f, .038f),
            new(0, -.012f, .032f), "dce5e8", "snow_ground");
        var inspectPoint = detail.GlobalPosition + new Vector3(0, 0, .075f);
        var inspect = ObservationTarget(zirat, "inspect-sketch-tag-base", new(.26f, .16f, .12f), inspectPoint);
        inspect.JournalEntryId = "urman.chapter1:knowledge/clue_sketch_tag_base_checked";
        ObserveGuard(inspect, "Подойди к бирке с наружной стороны ограды и посмотри на её нижний край.",
            camera => camera.GlobalPosition.Z > markerPoint.Z + .20f
                && ObservationNear(camera, inspectPoint, 2.25f));
        inspect.SetMeta("observationReferenceEye", inspectPoint + new Vector3(.2f, 1.05f, 1f));
        inspect.SetMeta("observationLookAt", inspectPoint);
    }

    private void BuildCuriosityReturns(StyleBenchmarkZone street)
    {
        var sign = ObservationExistingTarget("discover-main-street-sign-reverse");
        var words = ObservationTarget(street, "return-local-words", new(1.35f, .95f, .80f), sign.GlobalPosition);
        words.DialogueId = "urman.chapter1:dialogue/local_words_return";
        ObserveGuard(words, "Вернись к обороту указателя: слово «юл» нужно прочитать здесь, а «өй» — на домашней карточке.",
            camera => ObservationNear(camera, words.GlobalPosition, 3f));
        words.SetMeta("observationReferenceEye", words.GlobalPosition + new Vector3(0, .25f, 1.5f));
        words.SetMeta("observationLookAt", words.GlobalPosition);

        var sled = ObservationExistingTarget("discover-babai-yard-sled-repair");
        var gate = ObservationExistingTarget("discover-babai-yard-loose-side-gate-board");
        var returnDirection = ObservationHorizontal(gate.GlobalPosition - sled.GlobalPosition).Normalized();
        var yard = ObservationTarget(street, "return-yard-loop", new(.82f, .82f, 1.1f), sled.GlobalPosition);
        yard.JournalEntryId = "urman.chapter1:knowledge/clue_yard_loop_return";
        ObserveGuard(yard, "Боковой проход выводит обратно к санкам. Посмотри на двор со стороны этого прохода.",
            camera => ObservationNear(camera, sled.GlobalPosition, 3f)
                && ObservationHorizontal(camera.GlobalPosition - sled.GlobalPosition).Normalized().Dot(returnDirection) > .35f);
        yard.SetMeta("observationReferenceEye", sled.GlobalPosition + returnDirection * 1.5f + Vector3.Up * .85f);
        yard.SetMeta("observationLookAt", sled.GlobalPosition);
    }

    private void UpdateAct1ImageDiscoveries()
    {
        _refreshSketchObservationMetadata?.Invoke();
        if (_observationTagSnow is not null)
            _observationTagSnow.Visible = !ObservationKnown("clue_sketch_tag_base_checked");
    }

    private bool ObservationKnown(string localId)
    {
        if (_runtimeBridge?.ActiveSceneId is null) return false;
        var state = _runtimeBridge.SelectRuntimeState();
        return state.TryGetProperty("knowledge", out var knowledge)
            && knowledge.TryGetProperty("urman.chapter1:knowledge/" + localId, out var entry)
            && entry.TryGetProperty("status", out var status) && status.GetString() == "confirmed";
    }

    private InteractionTarget ObservationTarget(StyleBenchmarkZone zone, string id, Vector3 size, Vector3 worldPosition)
    {
        var target = zone.MakeInteractionBox("Observation_" + id, size, zone.ToLocal(worldPosition),
            "756a57", ObservationInteractionPrefix + id, id, rayOnly: true);
        // Labels resolve from authoring once the runtime is initialized.
        target.SetMeta("observationLabelId", "urman.chapter1:text/" + id);
        target.Prompt = id switch
        {
            "observe-well-tie" => "Посмотреть, как держится варежка",
            "observe-bench-plank" => "Рассмотреть светлую полоску на скамье",
            "observe-branch-profile" => "Проследить странный контур у тропы",
            "observe-photo-facade-windows" => "Посчитать окна на фасаде",
            "observe-photo-yard" => "Сверить берёзу, калитку и три окна",
            "inspect-photo-gate-repair" => "Рассмотреть стык светлой вставки",
            "observe-sketch-landmarks" => "Сверить канаву, бирку и внешнюю ограду",
            "inspect-sketch-tag-base" => "Смахнуть снег с нижнего края бирки",
            "return-local-words" => "Вернуться к слову на указателе",
            "return-yard-loop" => "Узнать двор с обратной стороны прохода",
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown observation target")
        };
        return target;
    }

    private InteractionTarget ObservationExistingTarget(string id) =>
        FindDescendants<InteractionTarget>(this).FirstOrDefault(target => target.InteractionId == ObservationInteractionPrefix + id)
        ?? throw new InvalidOperationException("Observation source target is missing: " + id);

    private Node3D ObservationFindNode(string name) => FindDescendants<Node3D>(this)
        .FirstOrDefault(node => node.Name == name)
        ?? throw new InvalidOperationException("Observation landmark is missing: " + name);

    private void ObserveGuard(InteractionTarget target, string hint, Func<Camera3D, bool> accepts)
    {
        _observationGuards.Add(target.InteractionId, accepts);
        target.SetMeta("observationBlockedHint", hint);
        target.SetMeta("observationRequiresLiveView", true);
    }

    private bool ObservationNear(Camera3D camera, Vector3 point, float reach, float facing = .78f)
    {
        var direction = point - camera.GlobalPosition;
        return direction.Length() is > .05f && direction.Length() < reach
            && (-camera.GlobalTransform.Basis.Z).Normalized().Dot(direction.Normalized()) > facing
            && ObservationVisible(camera.GlobalPosition, point);
    }

    private bool ObservationFramed(Camera3D camera, Vector3 point)
    {
        var local = camera.ToLocal(point);
        if (local.Z >= -.05f) return false;
        var viewport = camera.GetViewport().GetVisibleRect().Size;
        var aspect = viewport.X / Math.Max(viewport.Y, 1f);
        var tangent = Mathf.Tan(Mathf.DegToRad(camera.Fov) * .5f);
        var halfWidth = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent : tangent * aspect;
        var halfHeight = camera.KeepAspect == Camera3D.KeepAspectEnum.Width ? tangent / aspect : tangent;
        return Math.Abs(local.X / -local.Z) <= halfWidth
            && Math.Abs(local.Y / -local.Z) <= halfHeight
            && ObservationVisible(camera.GlobalPosition, point);
    }

    private bool ObservationVisible(Vector3 eye, Vector3 point)
    {
        using var ray = PhysicsRayQueryParameters3D.Create(eye, point, 3u, _observationRayExclusions);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(ray);
        return hit.Count == 0 || hit["position"].AsVector3().DistanceTo(point) < .30f;
    }

    private static Vector3 ObservationHorizontal(Vector3 vector) => new(vector.X, 0, vector.Z);

    private static Vector3 ObservationMeshCenter(Node3D node) => node is MeshInstance3D { Mesh: { } mesh }
        ? node.GlobalTransform * mesh.GetAabb().GetCenter() : node.GlobalPosition;

    private static (MeshInstance3D Mesh, Vector3 LocalPoint)[] ObservationSurfaceAnchors(Node3D root, string family)
    {
        var anchors = FindDescendants<MeshInstance3D>(root)
            .Where(mesh => mesh.Mesh is not null && mesh.Name.ToString().StartsWith(family + "_LOD", StringComparison.Ordinal))
            .OrderBy(mesh => mesh.Name.ToString(), StringComparer.Ordinal)
            .Select(mesh =>
            {
                var faces = mesh.Mesh!.GetFaces();
                if (faces.Length < 3)
                    throw new InvalidOperationException("Observation landmark has no surface: " + mesh.GetPath());
                var point = Vector3.Zero;
                var top = float.NegativeInfinity;
                for (var i = 0; i + 2 < faces.Length; i += 3)
                {
                    var center = (faces[i] + faces[i + 1] + faces[i + 2]) / 3f;
                    var height = (mesh.GlobalTransform * center).Y;
                    if (height <= top) continue;
                    top = height;
                    point = center;
                }
                return (Mesh: mesh, LocalPoint: point);
            }).ToArray();
        return anchors.Length > 0 ? anchors
            : throw new InvalidOperationException("Observation landmark family is missing: " + root.GetPath() + "/" + family);
    }

    private static bool ObservationMeshPresent(MeshInstance3D mesh, Camera3D? camera)
    {
        if (mesh.Mesh is null || !mesh.IsInsideTree() || !mesh.IsVisibleInTree()) return false;
        if (camera is null) return true;
        if ((mesh.Layers & camera.CullMask) == 0) return false;
        var distance = camera.GlobalPosition.DistanceTo(ObservationMeshCenter(mesh));
        // Respect the rendered variant, including a parent's hidden state and
        // range-based LOD. Stay clear of fade/hysteresis margins; a clear physics
        // ray alone cannot prove that the landmark is actually on screen.
        return (mesh.VisibilityRangeBegin <= 0f
                || distance >= mesh.VisibilityRangeBegin + mesh.VisibilityRangeBeginMargin)
            && (mesh.VisibilityRangeEnd <= 0f
                || distance < mesh.VisibilityRangeEnd - mesh.VisibilityRangeEndMargin);
    }
}
