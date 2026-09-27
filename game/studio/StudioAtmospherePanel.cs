using System.Text.Json.Nodes;
using Godot;
using Urman.Godot;

namespace Urman.Studio.App;

/// <summary>
/// Light, sky and fog (spec WORLD12): the outdoor profiles as sliders and
/// colours with a live preview on the real village. A slider drag is one undo
/// step. "Смотреть как" previews another profile (say, night) without changing
/// the zone or story time; the game still picks profiles by zone.
/// </summary>
public partial class StudioAtmospherePanel : AcceptDialog
{
    public const string FilePath = "game/content/world/atmosphere.v1.json";
    private readonly StudioRoot _studio;
    private readonly Func<Act1ConnectedWorld?> _world;
    private readonly VBoxContainer _fields = new();
    private readonly OptionButton _profile = new();
    private string[] _ids = [];
    private IDisposable? _drag;

    public StudioAtmospherePanel(StudioRoot studio, Func<Act1ConnectedWorld?> world)
    {
        _studio = studio;
        _world = world;
        Title = "Свет, небо и туман";
        OkButtonText = "Закрыть";
        Exclusive = false;
        MinSize = new Vector2I(460, 640);
        var root = new VBoxContainer();
        AddChild(root);
        root.AddChild(new Label { Text = "Профиль", ThemeTypeVariation = "MutedLabel" });
        root.AddChild(_profile);
        _profile.ItemSelected += _ => Fill();
        var look = new HBoxContainer();
        look.AddChild(new Label { Text = "Смотреть как:", ThemeTypeVariation = "MutedLabel" });
        StudioRoot.Button(look, "по зоне", () => Preview(null));
        StudioRoot.Button(look, "этот профиль", () => Preview(Profile()));
        root.AddChild(look);
        root.AddChild(new Label { Text = "«Смотреть как» не меняет сюжетное время и зону — это только вид в Studio.", ThemeTypeVariation = "MutedLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(420, 480), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(_fields);
        _fields.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.AddChild(scroll);
        Reload();
    }

    public void Reload()
    {
        var selected = _profile.Selected;
        _ids = _studio.Workspace.EntityIds.Where(id => id.StartsWith("urman.world:atmosphere/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        _profile.Clear();
        foreach (var id in _ids) _profile.AddItem(_studio.Catalog.NameOf(id));
        if (_ids.Length > 0) _profile.Selected = Math.Clamp(selected, 0, _ids.Length - 1);
        Fill();
    }

    private string? Profile() => _profile.Selected >= 0 && _profile.Selected < _ids.Length ? _ids[_profile.Selected] : null;

    private void Preview(string? profileId)
    {
        Act1ConnectedWorld.StudioPreviewProfile = profileId is null ? null : (string?)_studio.Workspace.Get(profileId)?["params"]?["profile"];
        ApplyToWorld();
    }

    /// <summary>Show the current (possibly unsaved) values on the Studio's village.</summary>
    public void ApplyToWorld()
    {
        AtmosphereProfiles.Reload(_studio.Workspace.File(FilePath).Text);
        _world()?.RefreshAtmosphereForStudio();
    }

    private void Fill()
    {
        foreach (var child in _fields.GetChildren()) child.QueueFree();
        if (Profile() is not { } id || _studio.Workspace.Get(id)?["params"] is not JsonObject parameters) return;
        Slider("Экспозиция (яркость кадра)", parameters, ["exposure"], .4, 2.0);
        Header("Солнце");
        Colour("Цвет солнца", parameters, ["sun", "color"]);
        Slider("Сила солнца", parameters, ["sun", "energy"], 0, 3);
        Slider("Плотность теней", parameters, ["sun", "shadowOpacity"], 0, 1);
        Slider("Высота солнца, °", parameters, ["sun", "rotation", "0"], -89, -5);
        Slider("Направление солнца, °", parameters, ["sun", "rotation", "1"], -180, 180);
        Header("Небо и рассеянный свет");
        Colour("Верх неба", parameters, ["sky", "top"]);
        Colour("Горизонт", parameters, ["sky", "horizon"]);
        Colour("Рассеянный свет", parameters, ["ambient", "color"]);
        Slider("Сила рассеянного света", parameters, ["ambient", "energy"], 0, 2);
        Header("Туман и дымка");
        Colour("Цвет дымки", parameters, ["fog", "color"]);
        Slider("Плотность тумана", parameters, ["fog", "density"], 0, .03);
        Slider("Воздушная перспектива", parameters, ["fog", "aerialPerspective"], 0, 1);
    }

    private void Header(string text) => _fields.AddChild(new Label { Text = text, ThemeTypeVariation = "HeaderLabel" });

    private void Slider(string label, JsonObject parameters, string[] path, double min, double max)
    {
        var value = Read(parameters, path);
        var row = new HBoxContainer();
        var caption = new Label { Text = $"{label}: {value:0.####}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _fields.AddChild(caption);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = (max - min) / 400, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        slider.DragStarted += () => _drag = _studio.Session.Begin(label.ToLowerInvariant());
        slider.ValueChanged += next =>
        {
            caption.Text = $"{label}: {next:0.####}";
            Write(path, Math.Round(next, 5), label.ToLowerInvariant());
        };
        slider.DragEnded += _ => { _drag?.Dispose(); _drag = null; };
        row.AddChild(slider);
        _fields.AddChild(row);
    }

    private void Colour(string label, JsonObject parameters, string[] path)
    {
        var picker = new ColorPickerButton { Color = Color.FromHtml((string)Node(parameters, path)!), CustomMinimumSize = new Vector2(0, 28), EditAlpha = false };
        picker.ColorChanged += color => Write(path, color.ToHtml(false), label.ToLowerInvariant());
        picker.PickerCreated += () => picker.GetPopup().PopupHide += () => { };
        _fields.AddChild(new Label { Text = label, ThemeTypeVariation = "MutedLabel" });
        _fields.AddChild(picker);
    }

    private void Write(string[] path, JsonNode value, string label)
    {
        var id = Profile()!;
        var parameters = _studio.Workspace.Get(id)!["params"]!.DeepClone().AsObject();
        JsonNode node = parameters;
        foreach (var part in path[..^1]) node = node[part]!;
        if (node is JsonArray array) array[int.Parse(path[^1])] = value; else node[path[^1]] = value;
        _studio.Session.SetField(id, ["params"], parameters, label);
        ApplyToWorld();
    }

    private static double Read(JsonObject parameters, string[] path) => (double)Node(parameters, path)!;

    private static JsonNode? Node(JsonObject parameters, string[] path)
    {
        JsonNode? node = parameters;
        foreach (var part in path) node = node is JsonArray array ? array[int.Parse(part)] : node?[part];
        return node;
    }
}
