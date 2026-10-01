using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Diagnostic for the village relayout (no rendering): prints every player-facing
/// feature with its world position, so a move of houses and roads can be checked for
/// what rides on it. Lines: "inv|kind|name|id|x|z" for interaction targets, carryable
/// props, trigger areas and named discovery/mechanism nodes, and "bld|..." for every
/// building in the address registry with its footprint bounds.
/// </summary>
public partial class Act1LayoutInventory : Node
{
    public override async void _Ready()
    {
        var exit = 0;
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("demo did not start");
            if (demo.PrologueActive)
            {
                demo._Input(new InputEventAction { Action = "ui_cancel", Pressed = true });
                for (var frame = 0; frame < 1200 && demo.PrologueActive; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            for (var i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var world = GetTree().Root.FindChild("Act1ConnectedWorld", true, false) as Act1ConnectedWorld ?? throw new InvalidOperationException("no world");
            var I = System.Globalization.CultureInfo.InvariantCulture;
            string Path(Node n) => GetTree().Root.GetPathTo(n).ToString().Replace("Act1LayoutInventory/Act1Demo/Main/ZoneHost/", "");
            foreach (var t in GetTree().Root.FindChildren("*", "", true, false).OfType<InteractionTarget>())
                GD.Print(string.Format(I, "inv|interaction|{0}|{1}|{2:0.0}|{3:0.0}", Path(t), t.InteractionId, t.GlobalPosition.X, t.GlobalPosition.Z));
            foreach (var t in GetTree().Root.FindChildren("*", "", true, false).OfType<CarryableProp>())
                GD.Print(string.Format(I, "inv|carry|{0}||{1:0.0}|{2:0.0}", Path(t), t.GlobalPosition.X, t.GlobalPosition.Z));
            foreach (var t in GetTree().Root.FindChildren("*", nameof(Area3D), true, false).OfType<Area3D>())
                GD.Print(string.Format(I, "inv|area|{0}||{1:0.0}|{2:0.0}", Path(t), t.GlobalPosition.X, t.GlobalPosition.Z));
            foreach (var t in world.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
                         .Where(n => n.Name.ToString().Contains("Discovery", StringComparison.Ordinal) || n.Name.ToString().Contains("Mechanism", StringComparison.Ordinal)
                                  || n.Name.ToString().Contains("Gate", StringComparison.Ordinal) || n.Name.ToString().Contains("Ladder", StringComparison.Ordinal)))
                GD.Print(string.Format(I, "inv|named|{0}||{1:0.0}|{2:0.0}", Path(t), t.GlobalPosition.X, t.GlobalPosition.Z));
            var registry = world.AddressRegistry ?? throw new InvalidOperationException("no registry");
            foreach (var b in registry.Buildings.Values)
            {
                var xs = b.Footprint.Select(p => p.X).DefaultIfEmpty(b.Position.X).ToArray();
                var zs = b.Footprint.Select(p => p.Z).DefaultIfEmpty(b.Position.Z).ToArray();
                GD.Print(string.Format(I, "bld|{0}|{1}|{2}|{3:0.0}|{4:0.0}|{5:0.0}|{6:0.0}|{7:0.0}|{8:0.0}", b.BuildingId, b.SourceKey, b.AddressId ?? "-",
                    b.Position.X, b.Position.Z, xs.Min(), xs.Max(), zs.Min(), zs.Max()));
            }
            // The physical verifier walks every door's standing route to the road graph; wait for it.
            for (var frame = 0; frame < 40000 && registry.AccessPoints.Values.Any(a => a.State.StartsWith("pending", StringComparison.Ordinal)); frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            foreach (var a in registry.AccessPoints.Values)
                GD.Print(string.Format(I, "acc|{0}|{1}|{2:0.00}|{3:0.00}|{4}|{5}", a.BuildingId, a.Kind, a.Position.X, a.Position.Z, a.State,
                    world.GetNodeOrNull<AddressAccessVerifier>("AddressAccessVerification")?.DescribeProgress() is { } progress && a.State != "verified" ? progress : ""));
            foreach (var n in world.FindChildren("*", "Node3D", true, false).OfType<Node3D>()
                         .Where(n => n.HasMeta("placementOwner") && n.GetMeta("placementOwner").AsString().StartsWith("authored plot", StringComparison.Ordinal)))
                GD.Print(string.Format(I, "kit|{0}|{1:0.00}|{2:0.00}|{3:0.0}|{4:0.0}|{5}", n.GetMeta(AuthoredWorldPlot.AuthoredIdMeta).AsString(),
                    n.GlobalPosition.X, n.GlobalPosition.Z, n.GlobalRotationDegrees.Y, n.RotationDegrees.Y, n.Visible));
            // Ambient residents stand where nothing solid is: a 0.28 m capsule over their feet.
            using (var plot = System.Text.Json.JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(
                       "res://content/world/act1_north_street_residents.world.v1.json")))
            {
                var space = world.GetWorld3D().DirectSpaceState;
                foreach (var entity in plot.RootElement.GetProperty("entities").EnumerateArray())
                {
                    var id = entity.GetProperty("id").GetString()!;
                    if (id.EndsWith("-sitter", StringComparison.Ordinal)) continue;   // seated on a bench by design
                    var root = world.AuthoredWorld?.ObjectRoot(id);
                    if (root is null) { GD.Print($"resident-clear|{id}|missing"); continue; }
                    var feet = root.GlobalPosition;
                    var query = new PhysicsShapeQueryParameters3D
                    {
                        Shape = new CapsuleShape3D { Radius = .28f, Height = 1.5f },
                        Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * 1.0f),
                        CollisionMask = 1 | 2
                    };
                    var hits = space.IntersectShape(query, 8)
                        .Select(hit => hit["collider"].AsGodotObject() as Node)
                        .Where(node => node is not null && !root.IsAncestorOf(node) && node != root
                            && node.Name != "AgentB_TerrainCollision")
                        .Select(node => node!.Name.ToString() + "<" + node.GetParent()?.Name).Distinct().ToArray();
                    GD.Print(string.Format(I, "resident-clear|{0}|{1:0.0}|{2:0.0}|{3}", id.Split('/')[^1], feet.X, feet.Z,
                        hits.Length == 0 ? "clear" : string.Join(";", hits)));
                }
            }
            GD.Print("inventory: done");
        }
        catch (Exception error)
        {
            GD.PrintErr("inventory: FAIL " + error);
            exit = 1;
        }
        GetTree().Quit(exit);
    }
}
