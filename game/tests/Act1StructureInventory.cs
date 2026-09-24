using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

namespace Urman.Godot.Tests;

/// <summary>
/// Diagnostic, not a gate: lists every sizeable built structure in the
/// connected Act I world with its footprint and the purpose its metadata
/// declares, so structures with no readable reason to exist can be found.
/// Writes JSON lines to the path in URMAN_STRUCTURE_INVENTORY.
/// </summary>
public partial class Act1StructureInventory : Node
{
    private static readonly Regex Built = new(
        "House|Volume|Mass|Shed|Barn|Building|Kiosk|Hut|Garage|Bath|Store|Shop|School|Mosque|Hall|Fap|Tower|Tank|Booth|Shelter|Pavilion|Dwelling|Facade|Parcel|Annex|Stable|Hangar|Club|Council|Station|Stop|Cabin|Coop|Pen|Well|Minaret|Kitchen|Porch|Veranda|Bridge|Gate|Silo|Pole|Transformer|Container|Wall",
        RegexOptions.Compiled);

    public override async void _Ready()
    {
        try
        {
            var demo = ResourceLoader.Load<PackedScene>("res://scenes/act1_demo.tscn").Instantiate<Act1DemoRoot>();
            AddChild(demo);
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!await this.StartThroughMainMenuAsync(demo)) throw new InvalidOperationException("demo did not start");
            for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var world = demo.DemoMain.ConnectedWorld ?? throw new InvalidOperationException("no connected world");
            var path = OS.GetEnvironment("URMAN_STRUCTURE_INVENTORY");
            using var output = new StreamWriter(path);
            var taken = new List<Node>();
            var containers = new HashSet<Node>();
            var count = 0;
            foreach (var node in Walk(world))
            {
                if (node is not Node3D spatial || !spatial.IsVisibleInTree()) continue;
                if (taken.Any(parent => parent.IsAncestorOf(node))) continue;
                var name = node.Name.ToString();
                var role = FirstMeta(node, "presentationRole", "role", "purpose", "assetRole", "structureRole", "status");
                var insideContainer = node.GetParent() is { } parentNode && containers.Contains(parentNode);
                if (!Built.IsMatch(name) && role.Length == 0 && !insideContainer) continue;
                var box = Bounds(spatial);
                if (box is not { } b || b.Size.Y < 1.5f || Math.Max(b.Size.X, b.Size.Z) < 1.8f) continue;
                // A whole quarter or kit is a container: list what stands in it.
                if (Math.Max(b.Size.X, b.Size.Z) > 18f && !name.StartsWith("Backdrop", StringComparison.Ordinal)
                    && name != "VillageForestRiver" && node.GetChildCount() > 0)
                {
                    containers.Add(node);
                    continue;
                }
                if (node is MeshInstance3D && !insideContainer) continue;
                taken.Add(node);
                count++;
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    name, path = world.GetPathTo(node).ToString(), role,
                    cx = b.GetCenter().X, cz = b.GetCenter().Z, sx = b.Size.X, sy = b.Size.Y, sz = b.Size.Z,
                    meshes = node.FindChildren("*", nameof(MeshInstance3D), true, false).Count,
                    interactions = node.FindChildren("*", nameof(InteractionTarget), true, false).Count,
                    meta = string.Join(";", node.GetMetaList().Select(key => key.ToString()))
                }));
            }
            GD.Print($"act1-structure-inventory: {count} structures -> {path}");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError("act1-structure-inventory: " + error.Message);
            GetTree().Quit(1);
        }
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            var children = node.GetChildren();
            for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }
    }

    private static string FirstMeta(Node node, params string[] keys)
    {
        foreach (var key in keys)
            if (node.HasMeta(key)) return node.GetMeta(key).ToString();
        return string.Empty;
    }

    private static Aabb? Bounds(Node3D root)
    {
        Aabb? result = null;
        foreach (var mesh in root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (!mesh.IsVisibleInTree() || mesh.Mesh is null) continue;
            if (mesh.VisibilityRangeBegin > 0) continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            result = result is { } r ? r.Merge(box) : box;
        }
        if (root is MeshInstance3D self && self.Mesh is not null)
        {
            var box = self.GlobalTransform * self.GetAabb();
            result = result is { } r ? r.Merge(box) : box;
        }
        return result;
    }
}
