using Godot;
using Urman.Studio.Core.Editing;

namespace Urman.Studio.App;

/// <summary>
/// "Кампания и мир": the real selector spec AI-13 asks for, as one self-contained
/// control. It lists the campaigns and startable worlds that exist in this
/// checkout, marks archive examples and worlds whose authored data the game does
/// not load, and shows the active content fingerprint and the reasons a second
/// editable world is not available yet.
///
/// It never writes authored content: the only file it may touch is the ignored
/// <c>.urman-studio/world-authoring-state.v1.json</c> through the shared store.
/// Switching the world preview itself stays with the owner, so wiring it is:
/// <code>
/// _selector = new StudioCampaignSelector(this);
/// _selector.Changed += context => { /* reload WorldSection with context */ };
/// topBar.AddChild(_selector.View);
/// </code>
/// </summary>
public sealed class StudioCampaignSelector
{
    private readonly StudioRoot _studio;
    private readonly OptionButton _campaign = new();
    private readonly OptionButton _world = new();
    private readonly Label _details = new();
    private readonly Label _blockers = new();
    private Control? _view;
    private CampaignWorldCatalog? _catalog;
    private WorldAuthoringStateStore? _states;
    private bool _filling;

    public StudioCampaignSelector(StudioRoot studio) => _studio = studio;

    /// <summary>The control to add to the Studio top bar; built on first use.</summary>
    public Control View => _view ??= Build();

    /// <summary>The campaign and world the author has selected, or null when none could be resolved.</summary>
    public CampaignWorldContext? Context { get; private set; }

    /// <summary>Raised after the author picked another campaign or world.</summary>
    public event Action<CampaignWorldContext>? Changed;

    private Control Build()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 2);

        var line = new HBoxContainer();
        line.AddChild(new Label { Text = "Кампания:", ThemeTypeVariation = "MutedLabel" });
        _campaign.CustomMinimumSize = new Vector2(220, 0);
        _campaign.ItemSelected += _ => OnCampaignSelected();
        line.AddChild(_campaign);

        line.AddChild(new Label { Text = "Мир:", ThemeTypeVariation = "MutedLabel" });
        _world.CustomMinimumSize = new Vector2(320, 0);
        _world.ItemSelected += _ => OnWorldSelected();
        line.AddChild(_world);

        StudioRoot.Button(line, "Обновить список", Refresh);
        root.AddChild(line);

        _details.ThemeTypeVariation = "MutedLabel";
        _details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_details);

        _blockers.ThemeTypeVariation = "MutedLabel";
        _blockers.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_blockers);

        Refresh();
        return root;
    }

    /// <summary>Re-reads the checkout and rebuilds both lists, keeping the current choice when it still exists.</summary>
    public void Refresh()
    {
        if (_view is null)
        {
            return;
        }

        try
        {
            _catalog = CampaignWorldCatalog.Discover(_studio.Workspace);
            _states = WorldAuthoringStateStore.Open(_studio.Workspace);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _details.Text = "Не удалось прочитать кампании и миры этого checkout: " + error.Message;
            _blockers.Text = "";
            Context = null;
            return;
        }

        _filling = true;
        var previousCampaign = Context?.Campaign.Selection;
        _campaign.Clear();
        foreach (var campaign in _catalog.Campaigns)
        {
            _campaign.AddItem(campaign.IsArchive ? $"{campaign.Id} — архивный пример" : campaign.Id);
            _campaign.SetItemMetadata(_campaign.ItemCount - 1, campaign.Selection);
        }

        var campaignIndex = IndexOf(_campaign, previousCampaign);
        if (campaignIndex < 0)
        {
            campaignIndex = IndexOf(_campaign, _catalog.SelectableCampaigns.FirstOrDefault()?.Selection);
        }

        if (campaignIndex < 0)
        {
            _campaign.Selected = -1;
            _world.Clear();
            _filling = false;
            _details.Text = "В этом checkout нет ни одной действующей кампании.";
            _blockers.Text = Blockers();
            Context = null;
            return;
        }

        _campaign.Selected = campaignIndex;
        _filling = false;
        FillWorlds(previousWorld: Context?.World.ScenePath);
    }

    private void OnCampaignSelected()
    {
        if (_filling)
        {
            return;
        }

        FillWorlds(previousWorld: null);
    }

    private void OnWorldSelected()
    {
        if (_filling || _catalog is null)
        {
            return;
        }

        var selection = _campaign.GetItemMetadata(_campaign.Selected).AsString();
        var scene = _world.GetItemMetadata(_world.Selected).AsString();
        try
        {
            var context = _catalog.Resolve(selection, scene);
            Context = context;
            Describe(context);
            Changed?.Invoke(context);
        }
        catch (InvalidOperationException error)
        {
            Context = null;
            _details.Text = "Этот мир нельзя открыть: " + error.Message;
        }
    }

    private void FillWorlds(string? previousWorld)
    {
        if (_catalog is null)
        {
            return;
        }

        var selection = _campaign.GetItemMetadata(_campaign.Selected).AsString();
        _filling = true;
        _world.Clear();
        foreach (var world in _catalog.WorldsFor(selection))
        {
            _world.AddItem($"{world.Title} · {Capability(world.Capability)}");
            _world.SetItemMetadata(_world.ItemCount - 1, world.ScenePath);
            _world.SetItemTooltip(_world.ItemCount - 1, $"{world.ScenePath}\n{string.Join("\n", world.CapabilityEvidence)}");
        }

        var index = IndexOf(_world, previousWorld);
        if (index < 0)
        {
            index = IndexOf(_world, Context?.World.ScenePath);
        }

        _filling = false;
        if (index < 0)
        {
            Context = null;
            _details.Text = "Для этой кампании нет ни одной стартовой сцены мира.";
            _blockers.Text = Blockers();
            return;
        }

        _world.Selected = index;
        OnWorldSelected();
    }

    private void Describe(CampaignWorldContext context)
    {
        var campaign = context.Campaign;
        var world = context.World;
        var zone = world.ZoneDeclaredByScene
            ? $"зона {world.ZoneId}, спавн {world.SpawnPointId}"
            : "зону и спавн задаёт код, сцена их не объявляет";
        var remembered = _states is null ? null : _states.Get(context);
        var state = remembered is null || (remembered.Section is null && remembered.Selection is null)
            ? "Сохранённого состояния этого мира пока нет."
            : $"Сохранённое состояние: раздел {remembered.Section ?? "—"}, выбор {remembered.Selection ?? "—"}, камера {remembered.Pivot.X:0.#}/{remembered.Pivot.Y:0.#}/{remembered.Pivot.Z:0.#}.";
        var binding = context.CampaignBinding == CampaignWorldContext.CampaignBindingDeclared
            ? "кампанию объявляет сама сцена"
            : "пару «кампания + сцена» выбрал автор, сцена кампанию не объявляет";

        _details.Text =
            $"Кампания {campaign.Id} v{campaign.ExactVersion} · вход {campaign.Entrypoint} · пак {campaign.CompiledPackPath} ({(campaign.CompiledPackExists ? "есть" : "нет")}).\n"
            + $"Мир {world.ScenePath} · {zone} · {Capability(world.Capability)} ({binding}).\n"
            + $"Отпечаток содержимого {context.ContentFingerprint[..12]}… по {context.AuthoredDependencies.Count} авторским файлам.\n"
            + state
            + "\nЭтот список выбирает контекст и не переключает предпросмотр мира сам: переключение делает окно Studio.";
        _blockers.Text = Blockers();
    }

    private string Blockers()
    {
        if (_catalog is null || _catalog.Blockers.Count == 0)
        {
            return "";
        }

        return "Почему доступен не любой мир:\n• " + string.Join("\n• ", _catalog.Blockers.Select(blocker => blocker.Detail));
    }

    private static string Capability(WorldAuthoringCapability capability) => capability switch
    {
        WorldAuthoringCapability.AuthoredWorldPlots => "авторский мир, редактируется",
        WorldAuthoringCapability.PresentationOnly => "только просмотр: сцена не грузит авторские данные",
        _ => "есть только спецификация, сцены нет"
    };

    private static int IndexOf(OptionButton list, string? metadata)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return -1;
        }

        for (var index = 0; index < list.ItemCount; index++)
        {
            if (string.Equals(list.GetItemMetadata(index).AsString(), metadata, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
