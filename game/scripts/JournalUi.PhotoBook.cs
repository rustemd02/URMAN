using Godot;
using Urman.Core.Persistence;
using System.Threading.Tasks;

namespace Urman.Godot;

public partial class JournalUi
{
    private const int PhotoBookTabIndex = 7;
    private const string SGardenPhotoId = "S-GARDEN";
    private const string SGardenPageId = "PAGE-GARDEN";
    private const string SGardenFrontImagePath = "res://assets/textures/photobook/s-garden-front-v1.png";
    private const string PForestFrontImagePath = "res://assets/textures/photobook/p-forest-front-v2.png";
    private const string PForestThumbnailPath = "res://assets/textures/photobook/p-forest-front-v2-thumbnail.png";
    private static readonly IReadOnlyDictionary<string, (string Path, string Caption)> PhotoBookBackImages =
        new Dictionary<string, (string Path, string Caption)>(StringComparer.Ordinal)
        {
            ["P-FOREST"] = ("res://assets/textures/photobook/backs-v1/p-forest-back.svg", "У леса. Перед отъездом."),
            ["P-WATER"] = ("res://assets/textures/photobook/backs-v1/p-water-back.svg", "У старых мостков."),
            ["P-HOUSE"] = ("res://assets/textures/photobook/backs-v1/p-house-back.svg", "Во дворе. Ставня опять заедала."),
            ["P-COMMON"] = ("res://assets/textures/photobook/backs-v1/p-common-back.svg", "Черновик. Подпись не закончена."),
            ["P-CELLAR"] = ("res://assets/textures/photobook/backs-v1/p-cellar-back.svg", "Перед уборкой."),
            ["S-SCHOOL"] = ("res://assets/textures/photobook/backs-v1/s-school-back.svg", "Копия из школьного альбома."),
            ["S-BANK"] = ("res://assets/textures/photobook/backs-v1/s-bank-back.svg", "Старый подход к мосткам."),
            ["S-PHOTOGRAPHER"] = ("res://assets/textures/photobook/backs-v1/s-photographer-back.svg", "Снимала Гөлсинә."),
            ["S-GARDEN"] = ("res://assets/textures/photobook/backs-v1/s-garden-back.svg", "Сад после работы."),
            ["S-PRINT"] = ("res://assets/textures/photobook/backs-v1/s-print-back.svg", "Семейная папка. Копия."),
            ["S-MARAT"] = ("res://assets/textures/photobook/backs-v1/s-marat-back.svg", "Обычный день."),
            ["S-HAND"] = ("res://assets/textures/photobook/backs-v1/s-hand-back.svg", "На столе всё должно помещаться."),
            ["S-NOW"] = ("res://assets/textures/photobook/backs-v1/s-now-back.svg", "Сегодня. Снимал Айдар.")
        };
    private VBoxContainer _photoBookPage = null!;
    private ItemList _photoBookList = null!;
    private ScrollContainer _photoBookScroll = null!;
    private VBoxContainer _photoBookSpread = null!;
    private Label _photoBookEmpty = null!;
    private HBoxContainer _photoBookZoomControls = null!;
    private Button _photoBookZoomOut = null!;
    private Label _photoBookZoomValue = null!;
    private Button _photoBookZoomIn = null!;
    private Label _photoBookPreparation = null!;
    private Label _photoBookTitle = null!;
    private Label _photoBookPlacement = null!;
    private PanelContainer _photoBookFrontCard = null!;
    private Label _photoBookFrontHeading = null!;
    private TextureRect _photoBookFrontImage = null!;
    private Label _photoBookFrontBrief = null!;
    private Label _photoBookFoldMarker = null!;
    private HSeparator _photoBookSeparator = null!;
    private Label _photoBookBackHeading = null!;
    private TextureRect _photoBookBackImage = null!;
    private Label _photoBookBackText = null!;
    private Button _photoBookReadBackButton = null!;
    private Button _photoBookInspectBackButton = null!;
    private Button _photoBookMountButton = null!;
    private Label _photoBookActionStatus = null!;
    private IReadOnlyList<RuntimeBridge.PhotoBookPhoto> _photoBookProjection = [];
    private string? _photoBookSelectedId;
    private object? _photoBookSession;
    private bool _photoBookRefreshing;
    private bool _photoBookActionBusy;
    private float _photoBookReadingZoom = 1f;
    private Tween? _photoBookPlacementTween;
    private long _photoBookPlacementGeneration;

    private void BuildPhotoBookUi()
    {
        var layout = GetNode<VBoxContainer>("Screen/Book/Layout");
        _tabs.AddTab("Фотоальбом");
        var spread = new HSplitContainer
        {
            Name = "PhotoBook",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _photoBookPage = new VBoxContainer { Name = "PhotoBookPage", Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _photoBookPage.AddChild(new Label { Text = "Найденные снимки и записи на обороте",
            AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _photoBookPreparation = new Label
        {
            Name = "PreparationObjective",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        _photoBookPage.AddChild(_photoBookPreparation);
        _photoBookPage.AddChild(spread);
        layout.AddChild(_photoBookPage);

        _photoBookList = new ItemList
        {
            Name = "AcquiredPhotos",
            CustomMinimumSize = new Vector2(240, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
            AllowReselect = true,
            MaxColumns = 1,
            FixedIconSize = new Vector2I(60, 40)
        };
        spread.AddChild(_photoBookList);
        _photoBookList.ItemSelected += SelectPhotoBookPhoto;

        _photoBookScroll = new ScrollContainer
        {
            Name = "PhotoSpreadScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        spread.AddChild(_photoBookScroll);
        _photoBookSpread = new VBoxContainer
        {
            Name = "PhotoSpread",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _photoBookSpread.AddThemeConstantOverride("separation", 12);
        _photoBookScroll.AddChild(_photoBookSpread);
        _photoBookZoomControls = new HBoxContainer { Name = "ReadingZoom" };
        _photoBookZoomControls.AddThemeConstantOverride("separation", 6);
        _photoBookSpread.AddChild(_photoBookZoomControls);
        _photoBookZoomOut = new Button
        {
            Name = "ZoomOut",
            Text = "−",
            TooltipText = "Уменьшить размер текста",
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookZoomControls.AddChild(_photoBookZoomOut);
        _photoBookZoomOut.Pressed += () => SetPhotoBookReadingZoom(_photoBookReadingZoom - .1f);
        _photoBookZoomValue = new Label
        {
            Name = "ZoomValue",
            Text = "100%",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(64, 0),
            TooltipText = "Масштаб текста в фотоальбоме"
        };
        _photoBookZoomControls.AddChild(_photoBookZoomValue);
        _photoBookZoomIn = new Button
        {
            Name = "ZoomIn",
            Text = "+",
            TooltipText = "Увеличить размер текста",
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookZoomControls.AddChild(_photoBookZoomIn);
        _photoBookZoomIn.Pressed += () => SetPhotoBookReadingZoom(_photoBookReadingZoom + .1f);
        _photoBookEmpty = new Label
        {
            Name = "Empty",
            Text = "Найденные фотографии появятся здесь.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookSpread.AddChild(_photoBookEmpty);
        _photoBookTitle = new Label { Name = "Title", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _photoBookTitle.AddThemeFontSizeOverride("font_size", 24);
        _photoBookSpread.AddChild(_photoBookTitle);
        _photoBookPlacement = new Label { Name = "Placement", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _photoBookSpread.AddChild(_photoBookPlacement);
        _photoBookFrontCard = new PanelContainer
        {
            Name = "FrontDescriptionCard",
            ThemeTypeVariation = UrmanUiTheme.Sheet,
            PivotOffset = Vector2.Zero
        };
        _photoBookFrontCard.Resized += () =>
            _photoBookFrontCard.PivotOffset = _photoBookFrontCard.Size / 2f;
        var frontCardContent = new VBoxContainer { Name = "PaperContents" };
        frontCardContent.AddThemeConstantOverride("separation", 6);
        _photoBookFrontCard.AddChild(frontCardContent);
        _photoBookSpread.AddChild(_photoBookFrontCard);
        var frontCardHeading = new HBoxContainer { Name = "PaperHeading" };
        frontCardContent.AddChild(frontCardHeading);
        _photoBookFrontHeading = new Label
        {
            Text = "Описание снимка",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        frontCardHeading.AddChild(_photoBookFrontHeading);
        frontCardHeading.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _photoBookFoldMarker = new Label
        {
            Name = "FoldedCorner",
            Text = "⌝",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(18, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        };
        frontCardHeading.AddChild(_photoBookFoldMarker);
        _photoBookFrontImage = new TextureRect
        {
            Name = "GardenFrontIllustration",
            CustomMinimumSize = new Vector2(0f, 230f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        frontCardContent.AddChild(_photoBookFrontImage);
        _photoBookFrontBrief = new Label { Name = "FrontDescription",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        frontCardContent.AddChild(_photoBookFrontBrief);
        _photoBookInspectFrontButton = new Button
        {
            Name = "InspectPhotoFront",
            Text = "Рассмотреть снимок",
            Visible = false,
            FocusMode = Control.FocusModeEnum.All
        };
        frontCardContent.AddChild(_photoBookInspectFrontButton);
        _photoBookInspectFrontButton.Pressed += OpenSelectedPhotoBookFrontReader;
        _photoBookSeparator = new HSeparator();
        _photoBookSpread.AddChild(_photoBookSeparator);
        _photoBookBackHeading = new Label { Text = "Оборот",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _photoBookSpread.AddChild(_photoBookBackHeading);
        _photoBookBackImage = new TextureRect
        {
            Name = "AuthoredPhotoBack",
            CustomMinimumSize = new Vector2(0f, 200f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _photoBookSpread.AddChild(_photoBookBackImage);
        _photoBookBackText = new Label { Name = "BackText",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _photoBookSpread.AddChild(_photoBookBackText);
        var photoActions = new HBoxContainer { Name = "PhotoActions" };
        _photoBookSpread.AddChild(photoActions);
        _photoBookReadBackButton = new Button
        {
            Name = "ReadBack",
            Text = "Прочитать оборот",
            Disabled = true
        };
        photoActions.AddChild(_photoBookReadBackButton);
        _photoBookReadBackButton.Pressed += () => _ = PerformPhotoBookActionAsync(readBack: true);
        _photoBookMountButton = new Button
        {
            Name = "MountPhoto",
            Text = "Вклеить снимок",
            Disabled = true
        };
        photoActions.AddChild(_photoBookMountButton);
        _photoBookMountButton.Pressed += () => _ = PerformPhotoBookActionAsync(readBack: false);

        var photoBackActions = new HBoxContainer { Name = "PhotoBackActions" };
        _photoBookSpread.AddChild(photoBackActions);
        _photoBookInspectBackButton = new Button
        {
            Name = "InspectPhotoBack",
            Text = "Рассмотреть оборот",
            Visible = false,
            Disabled = true,
            FocusMode = Control.FocusModeEnum.All
        };
        photoBackActions.AddChild(_photoBookInspectBackButton);
        _photoBookInspectBackButton.Pressed += OpenSelectedPhotoBookBackReader;
        _photoBookActionStatus = new Label { Name = "ActionStatus",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _photoBookSpread.AddChild(_photoBookActionStatus);

        BuildBookInspection();
        BuildPhotoBookImageReader();

        _tabs.TabChanged += index =>
        {
            _photoBookPage.Visible = index == PhotoBookTabIndex;
            if (index != PhotoBookTabIndex)
            {
                CancelPhotoBookPlacementAnimation();
                ClosePhotoBookImageReader();
                CloseBookInspection();
                return;
            }
            RefreshPhotoBookPage();
            if (_screen.Visible) FocusPhotoBookPage();
        };
        _screen.VisibilityChanged += () =>
        {
            if (!_screen.Visible)
            { CancelPhotoBookPlacementAnimation(); ClosePhotoBookImageReader(); CloseBookInspection(); }
            else RefreshBookInspection();
        };
        _screen.TreeExiting += () =>
        {
            CancelPhotoBookPlacementAnimation();
            ClosePhotoBookImageReader();
        };
    }

    internal void OpenPhotoBook(RuntimeBridge bridge)
    {
        if (!IsInsideTree() || bridge.SessionIdentity is null || !bridge.IsPhotoWorldsCampaign) return;
        Open(bridge);
        if (bridge.SessionIdentity is null || !bridge.IsPhotoWorldsCampaign) return;
        _tabs.CurrentTab = PhotoBookTabIndex;
        FocusCurrentPage();
    }

    private void RefreshPhotoBookPage()
    {
        if (_notesLoadInProgress)
        {
            ClearPhotoBookPage(resetSession: false);
            return;
        }
        if (_bridge is not { } bridge)
        {
            ClearPhotoBookPage(resetSession: true);
            return;
        }
        if (bridge.SessionIdentity is not { } session)
        {
            ClearPhotoBookPage(resetSession: false);
            return;
        }

        RefreshPhotoBookPreparation();

        if (!ReferenceEquals(_photoBookSession, session))
        {
            CancelPhotoBookPlacementAnimation();
            ClosePhotoBookImageReader();
            ClearPhotoBookImages();
            _photoBookSession = session;
            _photoBookSelectedId = null;
            _photoBookActionStatus.Text = string.Empty;
            _photoBookReadingZoom = 1f;
            ApplyPhotoBookZoomPresentation(_accessibility);
        }

        _photoBookProjection = bridge.PhotoBookPhotos();
        var previousSelectedId = _photoBookSelectedId;
        var selectedIndex = _photoBookProjection.ToList()
            .FindIndex(photo => photo.PhotoId == _photoBookSelectedId);
        if (selectedIndex < 0 && _photoBookProjection.Count > 0) selectedIndex = 0;
        var nextSelectedId = selectedIndex < 0 ? null : _photoBookProjection[selectedIndex].PhotoId;
        if (!StringComparer.Ordinal.Equals(previousSelectedId, nextSelectedId))
        {
            CancelPhotoBookPlacementAnimation();
            ClosePhotoBookImageReader();
            _photoBookActionStatus.Text = string.Empty;
        }

        _photoBookRefreshing = true;
        _photoBookList.Clear();
        for (var index = 0; index < _photoBookProjection.Count; index++)
        {
            var photo = _photoBookProjection[index];
            _photoBookList.AddItem($"{index + 1:D2} · {photo.Title}", PhotoBookFrontTexture(photo.PhotoId, thumbnail: true));
            _photoBookList.SetItemTooltip(index, photo.Title);
        }

        var hasPhotos = selectedIndex >= 0;
        _photoBookZoomControls.Visible = hasPhotos;
        _photoBookEmpty.Visible = !hasPhotos;
        _photoBookTitle.Visible = hasPhotos;
        _photoBookPlacement.Visible = hasPhotos;
        _photoBookFrontCard.Visible = hasPhotos;
        _photoBookSeparator.Visible = hasPhotos;
        _photoBookBackHeading.Visible = hasPhotos;
        _photoBookBackText.Visible = hasPhotos;
        if (hasPhotos)
        {
            _photoBookList.Select(selectedIndex);
            var selected = _photoBookProjection[selectedIndex];
            _photoBookSelectedId = selected.PhotoId;
            ShowPhotoBookPhoto(selected);
        }
        else
        {
            _photoBookSelectedId = null;
            ClearPhotoBookImages();
            _photoBookTitle.Text = string.Empty;
            _photoBookPlacement.Text = string.Empty;
            _photoBookFrontBrief.Text = string.Empty;
            _photoBookBackText.Text = string.Empty;
        }

        _photoBookRefreshing = false;
        RefreshPhotoBookActionButtons();
        RefreshBookInspection();
    }

    private void ClearPhotoBookPage(bool resetSession)
    {
        ClosePhotoBookImageReader();
        CloseBookInspection();
        _bookInspectionToggle.Visible = false;
        CancelPhotoBookPlacementAnimation();
        ClearPhotoBookImages();
        if (resetSession)
        {
            _photoBookSession = null;
            _photoBookSelectedId = null;
            _photoBookReadingZoom = 1f;
            ApplyPhotoBookZoomPresentation(_accessibility);
        }
        _photoBookProjection = [];
        _photoBookRefreshing = true;
        _photoBookList.Clear();
        _photoBookPreparation.Text = string.Empty;
        _photoBookPreparation.Visible = false;
        _photoBookInspectFrontButton.Visible = false;
        _photoBookInspectBackButton.Visible = false;
        _photoBookInspectBackButton.Disabled = true;
        _photoBookZoomControls.Visible = false;
        _photoBookEmpty.Visible = true;
        _photoBookTitle.Visible = false;
        _photoBookPlacement.Visible = false;
        _photoBookFrontCard.Visible = false;
        _photoBookSeparator.Visible = false;
        _photoBookBackHeading.Visible = false;
        _photoBookBackText.Visible = false;
        _photoBookTitle.Text = string.Empty;
        _photoBookPlacement.Text = string.Empty;
        _photoBookFrontBrief.Text = string.Empty;
        _photoBookBackText.Text = string.Empty;
        _photoBookActionStatus.Text = string.Empty;
        _photoBookRefreshing = false;
        RefreshPhotoBookActionButtons();
    }

    private void RefreshPhotoBookPreparation()
    {
        var text = PhotoBookPreparationObjectiveText();
        _photoBookPreparation.Text = text ?? string.Empty;
        _photoBookPreparation.Visible = text is not null;
    }

    private void SelectPhotoBookPhoto(long index)
    {
        if (_photoBookRefreshing || index < 0 || index >= _photoBookProjection.Count) return;
        var selected = _photoBookProjection[(int)index];
        if (!StringComparer.Ordinal.Equals(_photoBookSelectedId, selected.PhotoId))
        {
            CancelPhotoBookPlacementAnimation();
            ClosePhotoBookImageReader();
            _photoBookActionStatus.Text = string.Empty;
        }
        _photoBookSelectedId = selected.PhotoId;
        ShowPhotoBookPhoto(selected);
        RefreshPhotoBookActionButtons();
        RefreshBookInspection();
    }

    // Front-only resource lookup: callers use the acquired projection. Never use
    // back images as list icons, since their captions require the ReadBack action.
    private static string? PhotoBookFrontImageResourcePath(string photoId, bool thumbnail = false)
    {
        // The school contact print copies the same fictional staged photograph.
        return photoId switch
        {
            SGardenPhotoId => SGardenFrontImagePath,
            "P-FOREST" or "S-SCHOOL" => thumbnail ? PForestThumbnailPath : PForestFrontImagePath,
            _ => null
        };
    }

    private static Texture2D? PhotoBookFrontTexture(string photoId, bool thumbnail = false)
    {
        var path = PhotoBookFrontImageResourcePath(photoId, thumbnail);
        return path is not null && ResourceLoader.Exists(path)
            ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private void ShowPhotoBookPhoto(RuntimeBridge.PhotoBookPhoto photo)
    {
        ClearPhotoBookImages();
        if (PhotoBookFrontTexture(photo.PhotoId) is { } frontImage)
        {
            _photoBookFrontImage.Texture = frontImage;
            _photoBookFrontImage.Visible = true;
        }
        _photoBookTitle.Text = photo.Title;
        var placement = photo.Mounted
            ? "Место в альбоме: снимок вклеен на свою страницу."
            : "Место в альбоме: снимок пока не вклеен.";
        _photoBookPlacement.Text = string.IsNullOrWhiteSpace(photo.OriginLabel)
            ? placement
            : $"{placement}\nПроисхождение: {photo.OriginLabel}.";
        _photoBookFrontBrief.Text = string.IsNullOrWhiteSpace(photo.FrontBrief)
            ? "Описание снимка пока не добавлено."
            : photo.FrontBrief;
        _photoBookBackText.Text = !photo.BackRead
            ? "Оборот ещё не прочитан."
            : string.IsNullOrWhiteSpace(photo.BackTextRu)
                ? "На обороте пока нет записи."
                : photo.BackTextRu;
        if (photo.BackRead
            && PhotoBookBackImages.TryGetValue(photo.PhotoId, out var authoredBack)
            && string.Equals(photo.BackTextRu, authoredBack.Caption, StringComparison.Ordinal)
            && ResourceLoader.Exists(authoredBack.Path)
            && ResourceLoader.Load<Texture2D>(authoredBack.Path) is { } backImage)
        {
            _photoBookBackImage.Texture = backImage;
            _photoBookBackImage.Visible = true;
        }
        RefreshPhotoBookImageReaderAction(photo);
    }

    private void ClearPhotoBookImages()
    {
        if (GodotObject.IsInstanceValid(_photoBookInspectBackButton))
        {
            _photoBookInspectBackButton.Visible = false;
            _photoBookInspectBackButton.Disabled = true;
        }
        if (GodotObject.IsInstanceValid(_photoBookFrontImage))
        {
            _photoBookFrontImage.Texture = null;
            _photoBookFrontImage.Visible = false;
        }
        if (GodotObject.IsInstanceValid(_photoBookBackImage))
        {
            _photoBookBackImage.Texture = null;
            _photoBookBackImage.Visible = false;
        }
    }

    private void SetPhotoBookReadingZoom(float zoom)
    {
        var next = Mathf.Clamp(Mathf.Round(zoom * 10f) / 10f, .9f, 1.6f);
        if (Mathf.IsEqualApprox(next, _photoBookReadingZoom)) return;
        _photoBookReadingZoom = next;
        ApplyPhotoBookZoomPresentation(_accessibility);
    }

    private void ApplyPhotoBookZoomPresentation(AccessibilitySettingsSnapshot settings)
    {
        if (_photoBookZoomValue is null || !GodotObject.IsInstanceValid(_photoBookZoomValue)) return;
        _photoBookZoomValue.Text = $"{Mathf.RoundToInt(_photoBookReadingZoom * 100f)}%";
        _photoBookZoomOut.Disabled = _photoBookReadingZoom <= .9f;
        _photoBookZoomIn.Disabled = _photoBookReadingZoom >= 1.6f;

        var accessibilityScale = Mathf.Clamp((float)settings.TextScale, .8f, 1.6f);
        var presentationScale = accessibilityScale * _photoBookReadingZoom;
        foreach (var label in new[]
        {
            _photoBookTitle,
            _photoBookPlacement,
            _photoBookFrontHeading,
            _photoBookFrontBrief,
            _photoBookBackHeading,
            _photoBookBackText,
            _photoBookActionStatus
        })
        {
            if (!GodotObject.IsInstanceValid(label)) continue;
            const string baseFontMeta = "accessibilityBase_font_size";
            if (!label.HasMeta(baseFontMeta))
            {
                var baseSize = label.GetThemeFontSize("font_size");
                label.SetMeta(baseFontMeta, baseSize > 0 ? baseSize : UrmanUiTheme.Size.Body);
            }
            var logicalBaseSize = label.GetMeta(baseFontMeta).AsInt32();
            label.AddThemeFontSizeOverride("font_size",
                Mathf.RoundToInt(logicalBaseSize * presentationScale));
        }
    }

    private void FocusPhotoBookPage()
    {
        if (_photoBookProjection.Count > 0) _photoBookList.GrabFocus();
        else _photoBookEmpty.GrabFocus();
    }

    private void RefreshPhotoBookActionButtons()
    {
        var currentProjection = !_notesLoadInProgress
            && _bridge is { } bridge
            && GodotObject.IsInstanceValid(bridge)
            && !bridge.IsQueuedForDeletion()
            && bridge.IsPhotoWorldsCampaign
            && bridge.SessionIdentity is { } session
            && ReferenceEquals(_photoBookSession, session);
        var selected = currentProjection
            ? _photoBookProjection.FirstOrDefault(photo => photo.PhotoId == _photoBookSelectedId)
            : null;
        _photoBookReadBackButton.Disabled = _photoBookActionBusy || selected?.CanReadBack != true;
        _photoBookMountButton.Disabled = _photoBookActionBusy || selected?.CanMount != true;
    }

    private bool IsCurrentPhotoBookOperation(RuntimeBridge bridge, object session) =>
        GodotObject.IsInstanceValid(this) && IsInsideTree() && !IsQueuedForDeletion()
        && GodotObject.IsInstanceValid(bridge) && !bridge.IsQueuedForDeletion()
        && !_notesLoadInProgress
        && ReferenceEquals(_bridge, bridge)
        && ReferenceEquals(bridge.SessionIdentity, session)
        && ReferenceEquals(_photoBookSession, session);

    private bool IsCurrentMountedPhotoCard(RuntimeBridge bridge, object session, string photoId) =>
        IsCurrentPhotoBookOperation(bridge, session)
        && _screen.Visible
        && _tabs.CurrentTab == PhotoBookTabIndex
        && _photoBookPage.Visible
        && StringComparer.Ordinal.Equals(_photoBookSelectedId, photoId)
        && _photoBookProjection.Any(photo => photo.PhotoId == photoId && photo.Mounted);

    private void CancelPhotoBookPlacementAnimation()
    {
        _photoBookPlacementGeneration++;
        if (_photoBookPlacementTween is { } tween && GodotObject.IsInstanceValid(tween)) tween.Kill();
        _photoBookPlacementTween = null;
        if (GodotObject.IsInstanceValid(_photoBookFrontCard))
        {
            _photoBookFrontCard.Scale = Vector2.One;
            _photoBookFrontCard.Rotation = 0f;
            _photoBookFrontCard.Modulate = Colors.White;
        }
        if (GodotObject.IsInstanceValid(_photoBookFoldMarker))
        {
            _photoBookFoldMarker.Scale = Vector2.One;
            _photoBookFoldMarker.Rotation = 0f;
        }
    }

    private void StartPhotoBookPlacementAnimation(RuntimeBridge bridge, object session, string photoId)
    {
        if (!IsCurrentMountedPhotoCard(bridge, session, photoId)) return;
        CancelPhotoBookPlacementAnimation();
        var generation = _photoBookPlacementGeneration;
        var reducedMotion = _accessibility.ReducedMotion;
        _photoBookFrontCard.Modulate = reducedMotion ? new Color(1f, 1f, 1f, 0f) : new Color(1f, 1f, 1f, .88f);
        _photoBookFrontCard.Scale = reducedMotion ? Vector2.One : new Vector2(.97f, .97f);
        _photoBookFrontCard.Rotation = reducedMotion ? 0f : -.025f;
        _photoBookFoldMarker.Scale = reducedMotion ? Vector2.One : new Vector2(.72f, .72f);
        _photoBookFoldMarker.Rotation = reducedMotion ? 0f : .32f;

        var duration = reducedMotion ? .15 : .95;
        var tween = CreateTween().SetParallel(true);
        _photoBookPlacementTween = tween;
        if (reducedMotion)
        {
            tween.TweenProperty(_photoBookFrontCard, "modulate:a", 1f, duration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        else
        {
            tween.TweenProperty(_photoBookFrontCard, "scale", Vector2.One, duration)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_photoBookFrontCard, "rotation", 0f, duration)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_photoBookFrontCard, "modulate", Colors.White, duration)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_photoBookFoldMarker, "scale", Vector2.One, .42)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_photoBookFoldMarker, "rotation", 0f, .42)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        tween.Finished += () =>
        {
            if (generation != _photoBookPlacementGeneration) return;
            _photoBookPlacementTween = null;
            if (!IsCurrentMountedPhotoCard(bridge, session, photoId))
            {
                CancelPhotoBookPlacementAnimation();
                return;
            }
            _photoBookFrontCard.Scale = Vector2.One;
            _photoBookFrontCard.Rotation = 0f;
            _photoBookFrontCard.Modulate = Colors.White;
            _photoBookFoldMarker.Scale = Vector2.One;
            _photoBookFoldMarker.Rotation = 0f;
        };
    }

    private async Task PerformPhotoBookActionAsync(bool readBack)
    {
        if (_photoBookActionBusy || _notesLoadInProgress
            || _bridge is not { } bridge || !bridge.IsPhotoWorldsCampaign
            || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(_photoBookSession, session))
            return;

        var photo = _photoBookProjection.FirstOrDefault(item => item.PhotoId == _photoBookSelectedId);
        if (photo is null || (readBack ? !photo.CanReadBack : !photo.CanMount)) return;

        var photoId = photo.PhotoId;
        _photoBookActionBusy = true;
        _photoBookActionStatus.Text = readBack ? "Читаю оборот…" : "Вклеиваю снимок…";
        RefreshPhotoBookActionButtons();
        var animatePlacement = false;
        try
        {
            var completed = readBack
                ? await bridge.ReadPhotoBookBackAsync(photoId)
                : await bridge.MountPhotoBookPhotoAsync(photoId);
            if (!IsCurrentPhotoBookOperation(bridge, session)) return;

            RefreshPhotoBookPage();
            animatePlacement = !readBack && completed && !photo.Mounted
                && StringComparer.Ordinal.Equals(_photoBookSelectedId, photoId)
                && _photoBookProjection.Any(item => item.PhotoId == photoId && item.Mounted);
            if (_photoBookSelectedId == photoId)
                _photoBookActionStatus.Text = completed
                    ? readBack ? "Оборот прочитан." : "Снимок вклеен в альбом."
                    : readBack ? "Не удалось прочитать оборот. Попробуйте ещё раз."
                    : "Не удалось вклеить снимок. Попробуйте ещё раз.";
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Photo book action failed: {exception.GetType().Name}");
            if (!IsCurrentPhotoBookOperation(bridge, session)) return;

            RefreshPhotoBookPage();
            if (_photoBookSelectedId == photoId)
                _photoBookActionStatus.Text = readBack
                    ? "Не удалось прочитать оборот. Попробуйте ещё раз."
                    : "Не удалось вклеить снимок. Попробуйте ещё раз.";
        }
        finally
        {
            _photoBookActionBusy = false;
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) RefreshPhotoBookActionButtons();
        }
        if (animatePlacement) StartPhotoBookPlacementAnimation(bridge, session, photoId);
    }
}
