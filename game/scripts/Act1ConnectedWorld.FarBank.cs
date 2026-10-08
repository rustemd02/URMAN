using System.Text.Json;
using Godot;
using Urman.Experiments.AgentBAct1;

namespace Urman.Godot;

/// <summary>
/// Заречье (relayout v3, 2026-10-01): the far-bank quarter. Households are generic
/// authored parcels in <see cref="AgentBAct1HeightField.FarBankPlotPath"/> (built by
/// AuthoredWorldDirector with their addresses); this part draws the quarter's streets,
/// the green with its well and the four public buildings named in the same plot.
/// </summary>
public partial class Act1ConnectedWorld
{
    internal sealed record FarBankRoad(string Id, string Street, Vector2[] Points, float Width);
    internal sealed record FarBankPublic(string Id, string Label, Vector3 Position, float Yaw, Vector2 Size);

    private static (FarBankRoad[] Roads, FarBankPublic[] Buildings, Vector3 Green)? _farBank;

    internal static (FarBankRoad[] Roads, FarBankPublic[] Buildings, Vector3 Green) FarBankPlot()
    {
        if (_farBank is { } cached) return cached;
        var path = AgentBAct1HeightField.FarBankPlotPath;
        if (!global::Godot.FileAccess.FileExists(path)) return (_farBank = ([], [], Vector3.Zero)).Value;
        using var doc = JsonDocument.Parse(global::Godot.FileAccess.GetFileAsString(path));
        var root = doc.RootElement;
        var roads = root.GetProperty("roads").EnumerateArray().Select(road => new FarBankRoad(
            road.GetProperty("id").GetString()!, road.GetProperty("street").GetString()!,
            road.GetProperty("points").EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToArray(),
            road.GetProperty("width").GetSingle())).ToArray();
        var buildings = root.GetProperty("publicBuildings").EnumerateArray().Select(b => new FarBankPublic(
            b.GetProperty("id").GetString()!, b.GetProperty("label").GetString()!,
            AuthoredWorldPlot.ToVector3(b.GetProperty("position")), b.GetProperty("yawDegrees").GetSingle(),
            new Vector2(b.GetProperty("size")[0].GetSingle(), b.GetProperty("size")[1].GetSingle()))).ToArray();
        var green = root.GetProperty("green");
        _farBank = (roads, buildings, new Vector3(green.GetProperty("x").GetSingle(), 0, green.GetProperty("z").GetSingle()));
        return _farBank.Value;
    }

    private static void AddFarBankQuarter(Node3D farBank)
    {
        var (roads, buildings, green) = FarBankPlot();
        foreach (var road in roads)
        {
            using var curve = new Curve3D();
            foreach (var point in road.Points) curve.AddPoint(new Vector3(point.X, 0f, point.Y));
            AddVisualLandformSurface(farBank, "FarBankStreet_" + road.Id, road.Width, .025f, curve.GetBakedLength(),
                new(0f, .02f, 0f), "cbd3d8", "snow_trampled", 0f, true, curve);
        }
        // The green at the bridgehead: a trampled round with the well in it.
        using (var ring = new Curve3D())
        {
            for (var a = 0; a <= 360; a += 20)
                ring.AddPoint(green + new Vector3(Mathf.Cos(Mathf.DegToRad(a)) * 3.2f, 0, Mathf.Sin(Mathf.DegToRad(a)) * 3.2f));
            AddVisualLandformSurface(farBank, "FarBankGreenPath", 1.6f, .02f, ring.GetBakedLength(), new(0f, .02f, 0f),
                "c6cfd3", "snow_trampled", 0f, true, ring);
        }
        foreach (var building in buildings) AddFarBankPublicBuilding(farBank, building);
    }

    /// <summary>A small one-storey public building: plastered walls on a plinth, a pitched
    /// roof, a porch with its door toward the street and a painted name board. Solid
    /// collision covers the walls; the board is presentation only.</summary>
    private static void AddFarBankPublicBuilding(Node3D parent, FarBankPublic spec)
    {
        var (width, depth) = (spec.Size.X, spec.Size.Y);
        var ground = new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) }
            .Select(c => new Vector3(spec.Position.X, 0, spec.Position.Z)
                + new Basis(Vector3.Up, Mathf.DegToRad(spec.Yaw)) * new Vector3(c.Item1 * width * .5f, 0, c.Item2 * depth * .5f))
            .Select(p => AgentBAct1HeightField.CollisionGround(p.X, p.Z)).ToArray();
        var floor = ground.Max() + .3f;
        var building = new Node3D
        {
            Name = "FarBankPublic_" + spec.Id.Replace("far-", ""),
            Position = new Vector3(spec.Position.X, floor, spec.Position.Z),
            RotationDegrees = new Vector3(0, spec.Yaw, 0)
        };
        building.SetMeta("publicRole", spec.Label);
        building.SetMeta("plotOwner", AgentBAct1HeightField.FarBankPlotPath);
        parent.AddChild(building);
        var (wall, trim, roofColour) = spec.Id switch
        {
            "far-police" => ("b9c3c9", "e8ecee", "4f5b61"),
            "far-bakery" => ("d8c7a6", "efe6d4", "6e5a45"),
            "far-dairy" => ("c9cfc8", "eef0ec", "5d6660"),
            _ => ("8a6a4c", "d9c9ad", "4d5a45"),      // forestry: timber
        };
        const float height = 3.1f;
        var plinth = floor - ground.Min() + .25f;
        AddVisualBox(building, "Plinth", new(width + .3f, plinth, depth + .3f), new(0, -plinth * .5f, 0), "7d786f", "stone_foundation");
        AddVisualBox(building, "Walls", new(width, height, depth), new(0, height * .5f, 0), wall, spec.Id == "far-forestry" ? "wood" : "plaster");
        AddVisualPitchedRoof(building, "Roof", width, depth, height, Mathf.Min(width, depth) * .28f, .45f, roofColour);
        AddRoofSnowCap(building, "RoofSnow", width, depth, height, Mathf.Min(width, depth) * .28f, .45f);
        var front = depth * .5f;
        // Windows on the front, either side of the door; the door faces the street (+Z).
        foreach (var x in new[] { -width * .3f, width * .3f })
        {
            AddVisualBox(building, "WindowFrame" + x, new(1.3f, 1.3f, .08f), new(x, 1.7f, front + .04f), trim, "wood_painted_trim");
            AddVisualBox(building, "WindowGlass" + x, new(1.1f, 1.1f, .09f), new(x, 1.7f, front + .05f), "37424c", "frost_window");
        }
        AddVisualBox(building, "Door", new(1.1f, 2.1f, .08f), new(0, 1.05f, front + .05f), "5b4636", "wood");
        // Porch at floor level, then ordinary 17 cm steps down to the actual ground in front.
        var porchDepth = 1.5f;
        var porchHeight = floor - ground.Min() + .3f;
        AddVisualBox(building, "Porch", new(2.6f, porchHeight, porchDepth), new(0, -porchHeight * .5f, front + porchDepth * .5f), "8c877d", "stone_foundation");
        var foot = building.ToGlobal(new Vector3(0, 0, front + porchDepth + 1.2f));
        var drop = floor - (float)AgentBAct1HeightField.CollisionGround(foot.X, foot.Z);
        var steps = drop < .05f ? 0 : Mathf.CeilToInt(drop / .17f);
        // Where a visitor stands before climbing: just past the lowest tread.
        building.SetMeta("porchFoot", building.ToGlobal(new Vector3(0, 0, front + porchDepth + .34f * steps + .6f)));
        var stepShapes = new List<(Vector3 Size, Vector3 At)>();
        for (var i = 0; i < steps; i++)
        {
            var top = -drop + (i + 1) * drop / (steps + 1);
            var run = .34f * (steps - i);
            var size = new Vector3(2.2f, top + drop + .3f, run);
            var at = new Vector3(0, top - size.Y * .5f, front + porchDepth + run * .5f);
            AddVisualBox(building, $"PorchStep{i}", size, at, "8c877d", "stone_foundation");
            stepShapes.Add((size, at));
        }
        AddVisualBox(building, "Canopy", new(2.8f, .1f, 1.4f), new(0, 2.5f, front + .7f), "e6ebef", "snow_roof");
        // Reuse the inventory's ImageGen W05 painted-wood material on the board;
        // exact lettering stays separate (see the far_bank_signs.md passport).
        AddVisualBox(building, "NameBoard", new(Mathf.Min(width - .6f, 3.2f), .62f, .06f), new(0, 2.85f, front + .07f), trim, "wood_painted_trim");
        var label = new Label3D
        {
            Name = "NameBoardText", Text = spec.Label.ToUpperInvariant(), FontSize = 64, PixelSize = .0045f,
            Modulate = new Color(.16f, .17f, .2f), Position = new Vector3(0, 2.85f, front + .105f), DoubleSided = false,
            OutlineSize = 0, Width = 640, AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        building.AddChild(label);
        if (spec.Id == "far-police")
            AddVisualBox(building, "PoliceStripe", new(width + .01f, .22f, depth + .01f), new(0, 2.25f, 0), "2f5d9a", "plaster");
        if (spec.Id == "far-dairy")
            AddVisualBox(building, "DairyCanRack", new(1.8f, .9f, .6f), new(width * .5f - 1.2f, .45f, front + .6f), "9aa0a2", "metal");
        if (spec.Id == "far-bakery")
            AddVisualBox(building, "BakeryChimney", new(.6f, 1.8f, .6f), new(width * .25f, height + 1.4f, -depth * .15f), "7a5a4a", "stone");

        var body = new StaticBody3D { Name = "FarBankPublicBody", CollisionLayer = 1, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(width, height, depth) }, Position = new(0, height * .5f, 0) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(width + .3f, plinth, depth + .3f) }, Position = new(0, -plinth * .5f, 0) });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(2.6f, porchHeight, porchDepth) }, Position = new(0, -porchHeight * .5f, front + porchDepth * .5f) });
        foreach (var (size, at) in stepShapes)
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = at });
        building.AddChild(body);
    }
}
