using Godot;

namespace Urman.Godot.Tests;

/// <summary>Regression proof against the live ordinary world: measured carriers,
/// real standing gate traversal and the runtime projection of stored inventory.
/// Local spawn fixtures grant no quest progress; this is not artistic acceptance.</summary>
public partial class Act1VillageCozySmokeTest : Node
{
    private Act1ConnectedWorld _world = null!;
    private FirstPersonController _player = null!;
    private RuntimeBridge _bridge = null!;
    private int _checks;

    public override void _Process(double delta)
    {
        if (GetTree().GetFirstNodeInGroup("pause_menu") is PauseMenuUi { IsOpen: true } pause)
        { DisplayServer.WindowMoveToForeground(); pause.Resume(); }
    }

    public override async void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        var exit = 1;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            await Frames(10);
            Require(await this.StartThroughMainMenuAsync(demo), "ordinary New Game starts");
            demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
            for (var frame = 0; frame < 900 && demo.PrologueActive; frame++) await Frames(1);
            await Frames(12);
            Require(!demo.PrologueActive && !demo.IntroVisible && !demo.MainMenuVisible, "production skip hands control to the village");
            _world = demo.DemoMain.ConnectedWorld ?? throw new InvalidOperationException("missing ordinary connected world");
            _player = demo.DemoMain.GetNode<FirstPersonController>("Player");
            _bridge = (RuntimeBridge)GetTree().GetFirstNodeInGroup("runtime_bridge");
            var school = Named<Node3D>(_world, "OldSchool");
            var club = Named<Node3D>(_world, "HouseOfCulture");
            CheckCivicShell(school);
            CheckCivicShell(club);
            CheckDocuments(school);
            CheckCarriersAgainstWindows(school);
            CheckCarriersAgainstWindows(club);
            var plates = CheckAddressPlates();
            CheckStoredInventory();
            CheckWarmWindows();
            Require(CheckAddressPlates().SequenceEqual(plates), "runtime item projection preserves the real plate/registry baseline");
            await CheckTimberFences();
            GD.Print($"act1-village-cozy: PASS {_checks} live geometry, aperture, texture, inventory and standing-physics checks; art and human play remain external");
            exit = 0;
        }
        catch (Exception error) { GD.PrintErr("act1-village-cozy: FAIL " + error); }
        finally { Input.ActionRelease("move_forward"); GetTree().Quit(exit); }
    }

    private void CheckCivicShell(Node3D building)
    {
        var ceiling = building.GetMeta("interiorCeiling").AsSingle();
        Require(building.GetMeta("storeys").AsInt32() == 1 && ceiling is > 2.7f and <= 4.5f,
            building.Name + ": one storey with a usable ceiling below 4.5m");
        var front = building.GetNode<Node3D>("FrontWall");
        var wallMeshes = All<MeshInstance3D>(front).Where(m => m.Name.ToString().EndsWith("Core", StringComparison.Ordinal)).ToArray();
        var wallBounds = BoundsIn(wallMeshes, building);
        Require(wallBounds.End.Y <= 4.5f && Math.Abs(wallBounds.End.Y - ceiling) < .025f,
            building.Name + ": actual wall vertices agree with the single-storey envelope");
        var floor = building.GetNode<MeshInstance3D>(building.Name == "OldSchool" ? "GroundFloor" : "ClubFloor");
        Require(HasTexture(floor) && floor.Mesh is not null && floor.IsVisibleInTree(), building.Name + ": textured real floor survives rebuild");
        var roof = building.GetNode<MeshInstance3D>("RoofFront");
        Require(BoundsIn(new[] { roof }, building).Position.Y >= ceiling - .05f,
            building.Name + ": roof sits above its real walls");
        GD.Print($"act1-village-cozy: {building.Name} real wall envelope={wallBounds}; ceiling={ceiling}");
    }

    private void CheckDocuments(Node3D school)
    {
        foreach (var slug in new[] { "school-class-photo", "school-transport-notice", "school-staff-note" })
        {
            var id = "urman.chapter1:document/" + slug;
            var targets = All<InteractionTarget>(_world).Where(t => t.DocumentId == id).ToArray();
            Require(targets.Length == 1 && targets[0].IsVisibleInTree() && targets[0].ActiveCollisionLayer == 4,
                slug + ": exactly one authored original document target on the reading layer; actual="+string.Join(", ",targets.Select(t=>$"{t.GetPath()} visible={t.IsVisibleInTree()} activeLayer={t.ActiveCollisionLayer} layer={t.CollisionLayer}")));
            Require(!targets[0].IsSemanticallyAvailable() || targets[0].CollisionLayer==4,
                slug + ": ordinary story availability retains its ray routing");
            Require(_bridge.RequireDocument(id).Title.Length > 0, slug + ": original runtime content remains bound");
            var half = school.GetMeta("interiorHalfSize").AsVector2();
            var local = school.ToLocal(targets[0].GlobalPosition);
            Require(Math.Abs(local.X) < half.X && Math.Abs(local.Z) < half.Y && local.Y > 0 && local.Y < 3f,
                slug + ": actual source lies within the rebuilt ground floor");
            var source=school.GetNode<Node3D>(slug.Replace('-','_')+"_Source");
            Require(All<MeshInstance3D>(source).Any(HasTexture) && targets[0].GlobalPosition.DistanceTo(source.GlobalPosition)<.1f,
                slug + ": real loaded source and zone-hosted target remain physically bound");
        }
        foreach (var id in new[] { "SchoolBlueMitten", "SchoolHeightMarks", "SchoolRegister", "SchoolStairWindow", "SchoolDrawings", "SchoolMuseumCase", "SchoolUpperWindow" })
            Require(All<InteractionTarget>(_world).Count(t => t.InteractionId == "urman.chapter1:local/square/" + id) == 1,
                id + ": existing inspection survives the relocation");
    }

    private void CheckCarriersAgainstWindows(Node3D building)
    {
        var windows = All<MeshInstance3D>(building).Where(m => m.Name.ToString().StartsWith("Glass", StringComparison.Ordinal) && m.IsVisibleInTree()).ToArray();
        Require(windows.Length > 4, building.Name + ": actual glazing still exists");
        var carriers = All<Node3D>(building).Where(n => n is not MeshInstance3D && n.GetNodeOrNull<MeshInstance3D>("HandmadeFace") is not null
            || n.Name.ToString().EndsWith("_Source", StringComparison.Ordinal) && n.GetNodeOrNull<MeshInstance3D>("SourceFace") is not null).ToArray();
        Require(carriers.Length > 2, building.Name + ": real art/sign carriers survive");
        foreach (var carrier in carriers)
        {
            var face = carrier.GetNodeOrNull<MeshInstance3D>("HandmadeFace") ?? carrier.GetNode<MeshInstance3D>("SourceFace");
            if (!face.IsVisibleInTree()) continue;
            Require(HasTexture(face), carrier.Name + ": real loaded texture on its measured carrier");
            foreach (var window in windows)
            {
                // Compare real world transforms in the glazing plane. A world-axis AABB alone
                // overestimates diagonally facing buildings and reports false intersections.
                if (Math.Abs(carrier.GlobalBasis.Z.Normalized().Dot(window.GlobalBasis.Z.Normalized())) < .98f) continue;
                var panel = BoundsIn(All<MeshInstance3D>(carrier).Where(m => m.IsVisibleInTree()), window);
                var opening = window.GetAabb();
                var nearWall = panel.Position.Z < .30f && panel.End.Z > -.30f;
                var intersectX = panel.Position.X < opening.End.X - .002f && panel.End.X > opening.Position.X + .002f;
                var intersectY = panel.Position.Y < opening.End.Y - .002f && panel.End.Y > opening.Position.Y + .002f;
                Require(!(nearWall && intersectX && intersectY), $"{carrier.Name}: transformed carrier/frame does not cover {window.GetPath()}");
            }
        }
        if (building.Name == "HouseOfCulture")
        {
            var poster = building.GetNode<Node3D>("SabantuyPoster");
            var face = poster.GetNode<MeshInstance3D>("HandmadeFace");
            var shape = face.GetAabb().Size;
            Require(Math.Abs(shape.X / shape.Y - 2f) < .025f, "Sabantuy poster preserves the source cell aspect ratio");
        }
    }

    private string[] CheckAddressPlates()
    {
        var registry = _world.AddressRegistry ?? throw new InvalidOperationException("no live address registry");
        var plates = All<AddressSignVisualComponent>(_world).Where(p => p.IsVisibleInTree()).ToArray();
        Require(plates.Length == registry.Addresses.Count && plates.Select(p => p.AddressId).Distinct().Count() == plates.Length,
            "every live registry address retains exactly one actual visible mounted plate");
        foreach (var plate in plates)
        {
            Require(registry.Addresses.TryGetValue(plate.AddressId, out var address)
                && registry.Buildings.ContainsKey(address.BuildingId) && registry.AccessPoints.ContainsKey(address.AccessId),
                plate.AddressId + ": plate resolves through stable building and access records");
            Require(plate.GetNode<MeshInstance3D>("EnamelFace").IsVisibleInTree()
                && plate.GetNode<Label3D>("HouseNumber").Text == registry.Addresses[plate.AddressId].HouseNumber,
                plate.AddressId + ": physical enamel and registry number are present");
        }
        GD.Print($"act1-village-cozy: real plates={plates.Length}; registry addresses={registry.Addresses.Count}");
        return plates.Select(p => p.AddressId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private void CheckWarmWindows()
    {
        var facade=Named<Node3D>(_world,"BabaiApproachDwellingFacade");
        var panes=All<MeshInstance3D>(facade).Where(m=>m.HasMeta("lightingRole")).ToArray();
        var spills=All<SpotLight3D>(facade).Where(l=>l.Name.ToString().StartsWith("WarmWindowSpill_",StringComparison.Ordinal)).ToArray();
        Require(panes.Length>1 && panes.Length==facade.GetMeta("litWindowCount").AsInt32() && spills.Length==panes.Length,
            "every Babai glazed window has its own actual warm spill source");
        foreach(var pane in panes)
        {
            Require(pane.MaterialOverride is ShaderMaterial shader && shader.Shader.Code.Contains("EMISSION",StringComparison.Ordinal),
                pane.Name+": warm frosted window uses a real emissive material");
            var light=spills.Single(l=>l.Name.ToString()=="WarmWindowSpill_"+pane.Name);
            var centre=pane.GlobalTransform*pane.Mesh!.GetAabb().GetCenter();
            Require(light.ShadowEnabled && light.SpotRange is >2f and <6f && light.LightEnergy>0f
                && light.GlobalPosition.DistanceTo(centre)<.25f,
                pane.Name+": finite shadowed source sits just outside the actual glass");
        }
    }

    private void CheckStoredInventory()
    {
        var barn = Named<Node3D>(_world, "BabaiStorageBarn");
        var toilet = Named<Node3D>(_world, "BabaiMoonOuthouse");
        foreach (var building in new[] { barn, toilet })
        {
            var meshes = All<MeshInstance3D>(building).Where(m => m.IsVisibleInTree() && m.Mesh is not null).ToArray();
            Require(meshes.Length > 4 && meshes.All(HasTexture), building.Name + ": built shell uses loaded textures");
            Require(All<CollisionShape3D>(building).Any(c => !c.Disabled && c.Shape is BoxShape3D), building.Name + ": shell has real contacts");
        }
        var moonDoor = toilet.GetNode<MeshInstance3D>("MoonCutTimberDoor");
        Require(moonDoor.Mesh is ArrayMesh && moonDoor.Mesh.GetFaces().Length > 100, "moon toilet has constructed timber geometry");
        var doorFaces = moonDoor.Mesh.GetFaces();
        var doorBounds = moonDoor.GetAabb();
        var clearSamples = 0; var solidSamples = 0;
        for(var ix=0;ix<9;ix++)for(var iy=0;iy<9;iy++)
        {
            // Search the upper central face using its live bounds. A decal on a
            // solid door cannot pass this actual triangle-ray cutout check.
            var from = new Vector3(doorBounds.Position.X+doorBounds.Size.X*(.33f+ix*.0425f),
                doorBounds.Position.Y+doorBounds.Size.Y*(.64f+iy*.02625f),doorBounds.End.Z+.1f);
            var blocked=false;
            for(var face=0;face<doorFaces.Length;face+=3)
                if(Geometry3D.RayIntersectsTriangle(from,Vector3.Forward,doorFaces[face],doorFaces[face+1],doorFaces[face+2]).VariantType!=Variant.Type.Nil)
                {blocked=true;break;}
            if(blocked)solidSamples++;else clearSamples++;
        }
        Require(clearSamples>2 && solidSamples>8,"moon opening is actual missing timber triangles bounded by a solid door face");
        var carry = (CarryCoordinator)GetTree().GetFirstNodeInGroup("carry_coordinator");
        var stored = new[] { "carry-crate", "carry-bucket", "carry-board", "carry-tool-shovel", "carry-tool-pole" }
            .Select(id => carry.Items.Single(p => p.ItemId == id)).ToArray();
        var floor = barn.GetNode<MeshInstance3D>("Floor").GetAabb();
        var floorInBarn = BoundsIn(new[] { barn.GetNode<MeshInstance3D>("Floor") }, barn);
        var snapshot = _bridge.SelectWorldProps().GetRawText();
        var poses = stored.ToDictionary(p => p.ItemId, p => p.GlobalTransform);
        foreach (var prop in stored)
        {
            AssertStored(prop);
            prop.Place(prop.GlobalPosition + barn.GlobalBasis.X * (floor.Size.X + 2f), prop.YawDegrees);
            prop.ResetToAuthored();
            Require(prop.GlobalTransform.IsEqualApprox(poses[prop.ItemId]), prop.ItemId + ": _Ready captured its actual storage pose");
            AssertStored(prop);
            prop.Place(prop.GlobalPosition + barn.GlobalBasis.X * (floor.Size.X + 2f), prop.YawDegrees);
        }
        carry.ProjectLoadedState();
        carry.ApplyWorldState();
        foreach (var prop in stored)
        {
            Require(prop.GlobalTransform.IsEqualApprox(poses[prop.ItemId]) && prop.State == CarryableProp.CarryState.World,
                prop.ItemId + ": real runtime projection resets the displaced diagnostic prop to storage");
            AssertStored(prop);
        }
        Require(_bridge.SelectWorldProps().GetRawText() == snapshot, "storage/reset checks grant no runtime progress or item command");
        void AssertStored(CarryableProp prop)
        {
            var box = BoundsIn(All<MeshInstance3D>(prop), barn);
            Require(box.Position.X >= floorInBarn.Position.X - .06f && box.End.X <= floorInBarn.End.X + .06f
                && box.Position.Z >= floorInBarn.Position.Z - .06f && box.End.Z <= floorInBarn.End.Z + .06f
                && box.Position.Y >= floorInBarn.End.Y - .025f && box.End.Y < 2.8f,
                prop.ItemId + ": entire actual textured item bounds are inside the store (" + box + ")");
        }
    }

    private async Task CheckTimberFences()
    {
        var fence = Named<Node3D>(_world, "YardFences");
        var meshes = All<MeshInstance3D>(fence).Where(m => m.IsVisibleInTree() && m.Mesh is not null).ToArray();
        Require(meshes.Length > 2 && meshes.All(HasTexture), "rebuilt village fences have actual textured timber surfaces");
        var bays = All<CollisionShape3D>(fence).Where(c => c.HasMeta("timberStyle") && c.HasMeta("streetFence") && c.GetMeta("streetFence").AsBool()).ToArray();
        var styles = bays.GroupBy(c => c.GetMeta("timberStyle").AsInt32()).OrderBy(g => g.Key).ToArray();
        Require(styles.Length == 5, "all five timber designs exist in the live street geometry");
        var faces = meshes.SelectMany(m => m.Mesh!.GetFaces().Select(v => m.ToGlobal(v))).ToArray();
        foreach (var style in styles)
        {
            var bay = style.Where(c => c.Shape is BoxShape3D b && b.Size.Z > 1.2f).OrderByDescending(c => ((BoxShape3D)c.Shape).Size.Z).First();
            var box = (BoxShape3D)bay.Shape;
            var sampleY = .70f - box.Size.Y * .5f;
            var intervals = new List<(float Start, float End)>();
            var inverse = bay.GlobalTransform.AffineInverse();
            for (var i = 0; i < faces.Length; i += 3)
            {
                var a = inverse * faces[i]; var b = inverse * faces[i+1]; var c = inverse * faces[i+2];
                if (Math.Min(a.Y, Math.Min(b.Y,c.Y)) > sampleY || Math.Max(a.Y,Math.Max(b.Y,c.Y)) < sampleY) continue;
                if (Math.Min(a.X,Math.Min(b.X,c.X)) > .18f || Math.Max(a.X,Math.Max(b.X,c.X)) < -.18f) continue;
                var cross = new List<Vector3>();
                Edge(a,b); Edge(b,c); Edge(c,a);
                var inside = cross.Where(v => Math.Abs(v.X) < .18f && Math.Abs(v.Z) < box.Size.Z*.5f).ToArray();
                if (inside.Length > 1)
                    intervals.Add((inside.Min(v=>v.Z),inside.Max(v=>v.Z)));
                void Edge(Vector3 p,Vector3 q)
                {
                    if ((p.Y-sampleY)*(q.Y-sampleY)>0 || Math.Abs(p.Y-q.Y)<.00001f) return;
                    cross.Add(p.Lerp(q,(sampleY-p.Y)/(q.Y-p.Y)));
                }
            }
            var cursor = -box.Size.Z*.5f; var covered = 0f; var holes = 0;
            foreach(var span in intervals.OrderBy(p=>p.Start))
            {
                if(span.Start>cursor+.045f) holes++;
                if(span.End<=cursor) continue;
                covered+=span.End-Math.Max(cursor,span.Start); cursor=span.End;
            }
            Require(covered > .02f && covered < box.Size.Z*.85f && holes >= 3,
                $"timber style {style.Key}: actual triangle cross-section has separate slats and daylight gaps ({holes} gaps, covered {covered:0.00}/{box.Size.Z:0.00}m)");
        }
        var gates = All<Node3D>(fence).Where(n => n.Name.ToString().StartsWith("TimberGate_",StringComparison.Ordinal)).ToArray();
        Require(gates.Length >= 3, "real open village gate anchors exist");
        foreach(var gate in gates)
        {
            var outward=(-gate.GetMeta("inward").AsVector3()).Normalized();
            foreach(var offset in new[]{-.8f,0f,.8f})
            {
                var point=Support(gate.GlobalPosition+outward*offset);
                Require(_player.CanStandAt(point), gate.Name+": standing capsule clears the actual gate path at "+offset);
            }
        }
        foreach(var gate in gates.Take(3))
        {
            var outward=(-gate.GetMeta("inward").AsVector3()).Normalized();
            var start=Support(gate.GlobalPosition+outward*.9f);
            var goal=Support(gate.GlobalPosition-outward*.9f);
            _player.ApplyZoneSpawn(start,0);
            await Frames(8);
            await WalkTo(goal,gate.Name.ToString()+" inward");
            await WalkTo(start,gate.Name.ToString()+" outward");
        }
    }

    private Vector3 Support(Vector3 near)
    {
        using var ray=PhysicsRayQueryParameters3D.Create(near+Vector3.Up*2f,near-Vector3.Up*3f,3);
        ray.Exclude=new global::Godot.Collections.Array<Rid>{_player.GetRid()};
        var hit=_world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
        Require(hit.Count>0 && hit["normal"].AsVector3().Y>.75f,"actual floor supports gate capsule at "+near);
        return hit["position"].AsVector3()+Vector3.Up*.035f;
    }

    private async Task WalkTo(Vector3 goal,string label)
    {
        for(var frame=0;frame<360;frame++)
        {
            var direction=goal-_player.GlobalPosition; direction.Y=0;
            if(direction.Length()<.13f)break;
            _player.ApplySmokeLook(0,Mathf.RadToDeg(Mathf.Atan2(-direction.X,-direction.Z)));
            Input.ActionPress("move_forward");
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        }
        Input.ActionRelease("move_forward"); await Frames(8);
        var distance=new Vector2(goal.X-_player.GlobalPosition.X,goal.Z-_player.GlobalPosition.Z).Length();
        Require(distance<.24f && Math.Abs(goal.Y-_player.GlobalPosition.Y)<.14f && _player.IsOnFloor(),
            label+": ordinary standing controller reaches supported endpoint; distance="+distance);
    }

    private static bool HasTexture(MeshInstance3D mesh)
    {
        var material=mesh.MaterialOverride ?? (mesh.Mesh is { } source && source.GetSurfaceCount()>0 ? mesh.GetActiveMaterial(0) : null);
        return material switch {
            StandardMaterial3D standard => standard.AlbedoTexture is { } texture && texture.GetWidth()>1 && texture.GetHeight()>1,
            ShaderMaterial shader => shader.GetShaderParameter("albedo_texture").AsGodotObject() is Texture2D texture
                && texture.GetWidth()>1 && texture.GetHeight()>1,
            _ => false };
    }

    private static Aabb BoundsIn(IEnumerable<MeshInstance3D> meshes,Node3D space)
    {
        var inverse=space.GlobalTransform.AffineInverse(); var has=false; var bounds=new Aabb();
        foreach(var mesh in meshes.Where(m=>m.Mesh is not null))
        {
            var aabb=mesh.GetAabb(); var transform=inverse*mesh.GlobalTransform;
            for(var i=0;i<8;i++)
            {
                var point=transform*aabb.GetEndpoint(i);
                bounds=has ? bounds.Expand(point) : new Aabb(point,Vector3.Zero); has=true;
            }
        }
        if(!has)throw new InvalidOperationException("no actual mesh bounds for "+space.GetPath());
        return bounds;
    }

    private static T Named<T>(Node parent,string name) where T:Node => All<T>(parent).Single(n=>n.Name==name);
    private static IEnumerable<T> All<T>(Node parent) where T:Node => parent.GetChildren().SelectMany(child =>
        (child is T matched ? new[]{matched} : Array.Empty<T>()).Concat(All<T>(child)));
    private async Task Frames(int count) {for(var i=0;i<count;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private void Require(bool condition,string label){_checks++;if(!condition)throw new InvalidOperationException(label);}
}
