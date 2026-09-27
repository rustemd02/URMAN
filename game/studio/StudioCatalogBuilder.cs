using System.Text.Json.Nodes;
using Godot;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.App;

/// <summary>
/// Builds the placeable-object catalogue from URMAN's real model kits
/// (<c>--urman-studio-build-catalog</c>). Each component root of a kit — or,
/// for kits exported as loose parts, each name-prefix group — becomes one
/// entry with its size, category and source. Names and categories an author
/// already changed are kept on a rebuild, and removed entries are reported,
/// never silently restored (spec WORLD04, DATA08). Preview images are a
/// reproducible cache under .urman-studio/cache, not project sources.
/// </summary>
public static class StudioCatalogBuilder
{
    public const string Flag = "--urman-studio-build-catalog";
    public const string CatalogPath = "game/content/world/catalog.v1.json";

    private sealed record Kit(string Scene, bool Grouped, string Label, bool LengthOnY = false);

    /// <summary>The legacy wet-road kit carries its length on the exported Y axis; the village builder turns it +90° about X, and so do the catalogue and the game.</summary>
    public static Basis AxisCorrection(bool lengthOnY) => lengthOnY ? Basis.FromEuler(new Vector3(Mathf.Pi * .5f, 0, 0)) : Basis.Identity;

    private static readonly Kit[] Kits =
    [
        new("res://assets/models/act1/urman_village_exterior_kit.glb", false, "деревня"),
        new("res://assets/models/act1/urman_zirat_roadside_kit.glb", false, "обочина"),
        new("res://assets/models/act1/urman_fap_clinic_kit.glb", false, "ФАП"),
        new("res://assets/models/act1/urman_kara_forest_edge_kit.glb", false, "лес"),
        new("res://assets/models/act1/urman_wet_village_road_kit.glb", false, "дорога", LengthOnY: true),
        new("res://assets/models/act1/urman_winter_pine.glb", false, "ель"),
        new("res://assets/models/act1/urman_winter_dead_tree.glb", false, "сухое дерево"),
        new("res://assets/generated/urman_modular_kit.glb", true, "модульный набор"),
        new("res://assets/models/agent_b_act1/agentb_village_buildings_kit.glb", true, "постройки"),
        new("res://assets/models/agent_b_act1/agentb_foliage_kit.glb", true, "растительность"),
        new("res://assets/models/agent_b_act1/agentb_zirat_kit.glb", true, "зират"),
    ];

    private static readonly (string Token, string Category)[] Categories =
    [
        ("Woodpile", "Бытовой реквизит"), ("Well", "Бытовой реквизит"), ("Notice", "Бытовой реквизит"),
        ("Gate", "Заборы и ворота"), ("Fence", "Заборы и ворота"), ("Picket", "Заборы и ворота"),
        ("House", "Дома и постройки"), ("Dwelling", "Дома и постройки"), ("Shed", "Дома и постройки"), ("Banya", "Дома и постройки"),
        ("Parcel", "Дома и постройки"), ("Facade", "Дома и постройки"), ("Porch", "Дома и постройки"), ("Awning", "Дома и постройки"), ("Fap", "Дома и постройки"),
        ("Pine", "Деревья и кусты"), ("Birch", "Деревья и кусты"), ("Tree", "Деревья и кусты"), ("Shrub", "Деревья и кусты"), ("Fern", "Деревья и кусты"),
        ("Grass", "Деревья и кусты"), ("Sedge", "Деревья и кусты"), ("Stump", "Деревья и кусты"), ("Log", "Деревья и кусты"), ("Branch", "Деревья и кусты"), ("Forest", "Деревья и кусты"),
        ("Stone", "Камни и снег"), ("Boulder", "Камни и снег"), ("Rock", "Камни и снег"), ("Snow", "Камни и снег"), ("Root", "Камни и снег"), ("Bank", "Камни и снег"),
        ("Road", "Дороги и покрытия"), ("Shoulder", "Дороги и покрытия"), ("Ditch", "Дороги и покрытия"), ("Culvert", "Дороги и покрытия"), ("Puddle", "Дороги и покрытия"), ("Path", "Дороги и покрытия"), ("Ruts", "Дороги и покрытия"),
        ("Bench", "Мебель"), ("Table", "Мебель"), ("Chair", "Мебель"), ("Bed", "Мебель"), ("Interior", "Мебель"),
        ("Well", "Бытовой реквизит"), ("Woodpile", "Бытовой реквизит"), ("Board", "Бытовой реквизит"), ("Notice", "Бытовой реквизит"), ("Marker", "Бытовой реквизит"),
        ("Lamp", "Источники света"), ("Light", "Источники света"),
        ("Cat", "Звук и эффекты"), ("Crow", "Звук и эффекты"),
    ];

    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["house"] = "дом", ["hero"] = "главный", ["dwelling"] = "жилой", ["facade"] = "фасад", ["timber"] = "бревенчатый", ["plaster"] = "оштукатуренный",
        ["fence"] = "забор", ["segment"] = "секция", ["rough"] = "грубый", ["picket"] = "штакетник", ["gate"] = "калитка", ["crooked"] = "кривой", ["open"] = "открытая",
        ["yard"] = "двор", ["shed"] = "сарай", ["loft"] = "с сеновалом", ["outbuilding"] = "хозпостройка", ["low"] = "низкий", ["tall"] = "высокий",
        ["parcel"] = "участок", ["variant"] = "вариант", ["gable"] = "фронтон", ["annex"] = "пристройка", ["banya"] = "баня", ["well"] = "колодец", ["landmark"] = "ориентир",
        ["woodpile"] = "дровница", ["stacked"] = "сложенные", ["logs"] = "брёвна", ["log"] = "бревно", ["cat"] = "кошка", ["crow"] = "ворона", ["ambient"] = "",
        ["culvert"] = "труба под дорогой", ["stone"] = "камни", ["cluster"] = "группа", ["roadside"] = "придорожная", ["ditch"] = "канава", ["wet"] = "мокрая",
        ["road"] = "дорога", ["shoulder"] = "обочина", ["left"] = "слева", ["right"] = "справа", ["birch"] = "берёза", ["shrub"] = "кусты", ["mass"] = "массив",
        ["boundary"] = "граница", ["distant"] = "дальняя", ["village"] = "деревня", ["marker"] = "знак", ["group"] = "группа", ["far"] = "дальняя", ["path"] = "тропа", ["edge"] = "край",
        ["bench"] = "скамья", ["entry"] = "вход", ["porch"] = "крыльцо", ["main"] = "основной", ["run"] = "линия", ["interior"] = "интерьер", ["set"] = "набор",
        ["notice"] = "объявления", ["board"] = "доска", ["puddle"] = "лужа", ["rain"] = "дождевой", ["awning"] = "навес", ["service"] = "служебный", ["wayfinding"] = "указатель",
        ["pine"] = "сосна", ["stump"] = "пень", ["forest"] = "лес", ["fallen"] = "поваленные", ["bank"] = "берег", ["mixed"] = "смешанные", ["tree"] = "деревья",
        ["mossy"] = "мшистые", ["boulder"] = "валуны", ["root"] = "корни", ["wall"] = "стена", ["crossing"] = "переезд", ["fern"] = "папоротник", ["break"] = "разрыв",
        ["grass"] = "трава", ["sedge"] = "осока", ["muddy"] = "грязная", ["crown"] = "полотно", ["approach"] = "подъезд", ["worn"] = "разбитое", ["branch"] = "ветка",
        ["spruce"] = "ель", ["linden"] = "липа", ["maple"] = "клён", ["rowan"] = "рябина", ["willow"] = "ива", ["bird"] = "черёмуха", ["cherry"] = "",
        ["tuft"] = "пучок", ["moss"] = "мох", ["light"] = "светлая", ["sunken"] = "просевшее", ["ruts"] = "колея", ["near"] = "ближняя", ["winter"] = "зимняя", ["dead"] = "сухое", ["fap"] = "ФАП", ["zirat"] = "зират",
    };

    public static async void Run(StudioRoot studio)
    {
        var root = studio.Workspace.Root;
        var path = Path.Combine(root, CatalogPath);
        var previous = File.Exists(path) ? (JsonNode.Parse(File.ReadAllText(path))!["entities"] ?? JsonNode.Parse(File.ReadAllText(path))!["entries"])!.AsArray().OfType<JsonObject>()
            .ToDictionary(entry => (string)entry["id"]!, StringComparer.Ordinal) : new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var previews = Path.Combine(root, ".urman-studio", "cache", "previews");
        Directory.CreateDirectory(previews);
        var entries = new List<JsonObject>();
        var headless = DisplayServer.GetName() == "headless";
        var stage = BuildStage(studio);
        foreach (var kit in Kits)
        {
            var packed = ResourceLoader.Load<PackedScene>(kit.Scene);
            if (packed is null)
            {
                GD.PushWarning($"catalog: kit {kit.Scene} did not load");
                continue;
            }

            var instance = packed.Instantiate<Node3D>();
            var top = instance.GetChildCount() == 1 && instance.GetChild(0) is Node3D only && only.GetChildCount() > 1 ? only : instance;
            var groups = new Dictionary<string, List<Node3D>>(StringComparer.Ordinal);
            foreach (var child in top.GetChildren().OfType<Node3D>())
            {
                var name = child.Name.ToString();
                if (name.Contains("LOD1", StringComparison.Ordinal) || name.Contains("LOD2", StringComparison.Ordinal) || name.EndsWith("-col", StringComparison.Ordinal))
                {
                    continue;
                }

                var key = kit.Grouped ? GroupKey(name) : name;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
                list.Add(child);
            }

            foreach (var (key, nodes) in groups.OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var aabb = new Transform3D(AxisCorrection(kit.LengthOnY), Vector3.Zero) * Bounds(nodes, instance);
                if (aabb.Size.LengthSquared() < .0001f || aabb.Size.X > 80 || aabb.Size.Z > 80)
                {
                    continue; // empty helpers and whole-street layouts are not placeable objects
                }

                var kitName = Path.GetFileNameWithoutExtension(kit.Scene);
                var id = $"urman.catalog:{kitName}/{Slug(key)}";
                var entry = new JsonObject
                {
                    ["id"] = id,
                    ["name"] = Humanize(key.Replace("_LOD0", "")),
                    ["category"] = Category(key),
                    ["source"] = kit.LengthOnY
                        ? new JsonObject { ["scene"] = kit.Scene, ["node"] = nodes[0].Name.ToString(), ["axisCorrection"] = "x+90" }
                        : new JsonObject { ["scene"] = kit.Scene, [kit.Grouped ? "prefix" : "node"] = kit.Grouped ? key : nodes[0].Name.ToString() },
                    ["size"] = new JsonArray(Round(aabb.Size.X), Round(aabb.Size.Y), Round(aabb.Size.Z)),
                    ["origin"] = new JsonArray(Round(aabb.Position.X + aabb.Size.X / 2), Round(aabb.Position.Y), Round(aabb.Position.Z + aabb.Size.Z / 2)),
                    ["collision"] = "box",
                    ["kit"] = kit.Label,
                    ["license"] = "проектный ассет URMAN (см. assets/asset_registry.json)"
                };
                if (previous.TryGetValue(id, out var authored))
                {
                    // Fields the author changed in Studio are listed in "authored"
                    // and win over the generator; everything else is regenerated.
                    foreach (var field in (authored["authored"] as JsonArray ?? []).Select(item => (string)item!))
                    {
                        if (authored[field] is { } value) entry[field] = value.DeepClone();
                    }

                    if (authored["authored"] is JsonArray marks) entry["authored"] = marks.DeepClone();
                }

                entries.Add(entry);
                if (!headless)
                {
                    await RenderPreview(studio, stage, nodes, aabb, Path.Combine(previews, Slug(id) + ".png"), AxisCorrection(kit.LengthOnY));
                }
            }

            instance.QueueFree();
        }

        var removed = previous.Keys.Where(id => !id.StartsWith("urman.catalog:imported/", StringComparison.Ordinal)).Except(entries.Select(entry => (string)entry["id"]!), StringComparer.Ordinal).ToArray();
        var document = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["kind"] = "urman.catalog",
            ["note"] = "Каталог объектов URMAN Studio. Собран из наборов моделей; названия и категории можно менять — пересборка их сохраняет.",
            ["entities"] = new JsonArray(entries.Concat(previous.Values.Where(item => ((string)item["id"]!).StartsWith("urman.catalog:imported/", StringComparison.Ordinal)).Select(item => (JsonObject)item.DeepClone())).Select(entry => (JsonNode?)entry).ToArray())
        };
        AtomicFile.WriteAllText(path, document.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
        }) + "\n");
        GD.Print($"studio-catalog: {entries.Count} entries, {entries.Select(entry => (string)entry["category"]!).Distinct().Count()} categories, removed-since-last={removed.Length}");
        foreach (var id in removed) GD.Print($"studio-catalog: no longer in kits (kept out, not restored): {id}");
        studio.GetTree().Quit(0);
    }

    /// <summary>Render one preview for a single scene (an imported model).</summary>
    public static async Task RenderSingle(StudioRoot studio, Node3D scene, string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var stage = BuildStage(studio);
        Aabb? total = null;
        foreach (var mesh in scene.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>())
        {
            var box = RelativeTransform(mesh, null) * mesh.GetAabb();
            total = total is { } current ? current.Merge(box) : box;
        }

        await RenderPreview(studio, stage, [scene], total ?? new Aabb(Vector3.Zero, Vector3.One), file, Basis.Identity);
        stage.Viewport.QueueFree();
    }

    public static string PreviewPath(string root, string id) => Path.Combine(root, ".urman-studio", "cache", "previews", Slug(id) + ".png");

    private static (SubViewport Viewport, Node3D Holder, Camera3D Camera) BuildStage(StudioRoot studio)
    {
        var viewport = new SubViewport { Size = new Vector2I(256, 192), OwnWorld3D = true, TransparentBg = false, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        studio.AddChild(viewport);
        var environment = new global::Godot.Environment { BackgroundMode = global::Godot.Environment.BGMode.Color, BackgroundColor = new Color("2a3038"), AmbientLightSource = global::Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c9d3de"), AmbientLightEnergy = .9f };
        viewport.AddChild(new WorldEnvironment { Environment = environment });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 35, 0), LightEnergy = 1.2f });
        var holder = new Node3D();
        viewport.AddChild(holder);
        var camera = new Camera3D { Fov = 35 };
        viewport.AddChild(camera);
        return (viewport, holder, camera);
    }

    private static async Task RenderPreview(StudioRoot studio, (SubViewport Viewport, Node3D Holder, Camera3D Camera) stage, List<Node3D> nodes, Aabb aabb, string file, Basis correction)
    {
        stage.Holder.Basis = correction;
        foreach (var child in stage.Holder.GetChildren()) child.QueueFree();
        foreach (var node in nodes)
        {
            var copy = (Node3D)node.Duplicate();
            stage.Holder.AddChild(copy);
            copy.Transform = RelativeTransform(node, null);
        }

        var center = aabb.GetCenter();
        var radius = Mathf.Max(aabb.Size.Length() * .5f, .2f);
        stage.Camera.GlobalPosition = center + new Vector3(1f, .75f, 1.25f).Normalized() * radius * 3.4f;
        stage.Camera.LookAt(center, Vector3.Up);
        stage.Camera.MakeCurrent();
        for (var frame = 0; frame < 3; frame++)
        {
            await studio.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        stage.Viewport.GetTexture().GetImage().SavePng(file);
    }

    // Bounds in the kit's root space, so a rotated or scaled export root
    // (Blender's +Z-up conversion) is part of the measured size.
    private static Aabb Bounds(IEnumerable<Node3D> nodes, Node3D root)
    {
        Aabb? total = null;
        foreach (var node in nodes)
        {
            foreach (var visual in node.FindChildren("*", nameof(VisualInstance3D), true, false).OfType<VisualInstance3D>().Prepend(node as VisualInstance3D).OfType<VisualInstance3D>())
            {
                var box = RelativeTransform(visual, root) * visual.GetAabb();
                total = total is { } current ? current.Merge(box) : box;
            }
        }

        return total ?? new Aabb();
    }

    private static Transform3D RelativeTransform(Node3D node, Node3D? ancestor)
    {
        var transform = Transform3D.Identity;
        for (Node? current = node; current is Node3D spatial && current != ancestor && current.GetParent() is not null; current = current.GetParent())
        {
            transform = spatial.Transform * transform;
        }

        return transform;
    }

    // "Birch_1_Trunk" belongs to "Birch_1": a numbered variant is its own object.
    public static string GroupKey(string name)
    {
        var parts = name.Split('_');
        if (parts.Length > 1 && parts[1].Length > 0 && parts[1].All(char.IsDigit)) return $"{parts[0]}_{parts[1]}";
        return parts[0].Length > 0 ? parts[0] : name;
    }

    private static string Category(string key) =>
        Categories.FirstOrDefault(pair => key.Contains(pair.Token, StringComparison.OrdinalIgnoreCase)).Category ?? "Бытовой реквизит";

    private static string Humanize(string key)
    {
        var parts = System.Text.RegularExpressions.Regex.Matches(key.Replace('_', ' '), "[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\\d+")
            .Select(match => match.Value)
            .Select(word => Words.TryGetValue(word, out var ru) ? ru : word)
            .Where(word => word.Length > 0)
            .ToArray();
        var text = string.Join(' ', parts);
        return text.Length == 0 ? key : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Slug(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static double Round(float value) => Math.Round(value, 2);
}
