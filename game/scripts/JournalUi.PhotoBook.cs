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
    private const string SBankFrontImagePath = "res://assets/textures/photobook/s-bank-front-v1.png";
    private const string PForestFrontImagePath = "res://assets/textures/photobook/p-forest-front-v2.png";
    private const string PForestThumbnailPath = "res://assets/textures/photobook/p-forest-front-v2-thumbnail.png";
    private const string CommonRelationsSourceId = "urman.fullgame:interaction/pw-common-three-places";
    private const string CommonRelationsStudyAssetId = "urman.fullgame:asset/w04-common-relations-study";
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
    private Label _photoBookCommonNote = null!;
    private TextureRect _photoBookCommonRelationsStudy = null!;
    private Label _photoBookCommonRelationsCaption = null!;
    private Button _photoBookCommonRelationsInspectButton = null!;
    private Button _photoBookReadBackButton = null!;
    private Button _photoBookInspectBackButton = null!;
    private Button _photoBookMountButton = null!;
    private Button _photoBookEnterButton = null!;
    private Label _photoBookEntryHint = null!;
    private Label _photoBookReturnConfirmation = null!;
    private Button _photoBookReturnCancel = null!;
    private RuntimeBridge? _photoBookReturnBridge;
    private object? _photoBookReturnSession;
    private string? _photoBookReturnSourceScene;
    private Label _photoBookActionStatus = null!;
    private IReadOnlyList<RuntimeBridge.PhotoBookPhoto> _photoBookProjection = [];
    private string? _photoBookSelectedId;
    private object? _photoBookSession;
    private bool _photoBookRefreshing;
    private bool _photoBookActionBusy;
    private float _photoBookReadingZoom = 1f;
    private Tween? _photoBookPlacementTween;
    private long _photoBookPlacementGeneration;
    private long _photoBookVisibilityGeneration;

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
        _photoBookEnterButton = new Button
        {
            Name = "EnterPhoto",
            Text = "Войти в снимок",
            Visible = false,
            Disabled = true,
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookSpread.AddChild(_photoBookEnterButton);
        _photoBookEnterButton.Pressed += () => _ = EnterSelectedPhotoBookAsync();
        _photoBookEntryHint = new Label
        {
            Name = "EntryHint",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        _photoBookSpread.AddChild(_photoBookEntryHint);
        // Match the pause shell's two-step confirmation inside the existing
        // book modal; a second modal would contend for the player input gate.
        _photoBookReturnConfirmation = new Label
        {
            Name = "ReturnConfirmation",
            Text = "Найденное сохранится. Вернуться домой?",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false
        };
        _photoBookSpread.AddChild(_photoBookReturnConfirmation);
        _photoBookReturnCancel = new Button
        {
            Name = "CancelReturn",
            Text = "Остаться в снимке",
            Visible = false,
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookSpread.AddChild(_photoBookReturnCancel);
        _photoBookReturnCancel.Pressed += () =>
        {
            ClearPhotoBookReturnConfirmation();
            RefreshPhotoBookActionButtons();
            _photoBookEnterButton.GrabFocus();
        };

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
        _photoBookCommonNote = new Label { Name = "CommonFrameAuthorNote", Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.All };
        _photoBookSpread.AddChild(_photoBookCommonNote);
        _photoBookCommonRelationsStudy = new TextureRect
        {
            Name = "CommonRelationsStudy",
            CustomMinimumSize = new Vector2(0f, 140f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _photoBookSpread.AddChild(_photoBookCommonRelationsStudy);
        _photoBookCommonRelationsCaption = new Label
        {
            Name = "CommonRelationsCaption", Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, FocusMode = Control.FocusModeEnum.All
        };
        _photoBookSpread.AddChild(_photoBookCommonRelationsCaption);
        _photoBookCommonRelationsInspectButton = new Button
        {
            Name = "InspectCommonRelationsStudy", Text = "Рассмотреть наброски",
            Visible = false, Disabled = true, FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0f, 44f)
        };
        _photoBookSpread.AddChild(_photoBookCommonRelationsInspectButton);
        _photoBookCommonRelationsInspectButton.Pressed += OpenSelectedPhotoBookCommonRelationsReader;
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
                ClearPhotoBookReturnConfirmation();
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
            {
                _photoBookVisibilityGeneration++;
                ClearPhotoBookReturnConfirmation();
                CancelPhotoBookPlacementAnimation();
                ClosePhotoBookImageReader();
                CloseBookInspection();
            }
            else RefreshBookInspection();
        };
        _screen.TreeExiting += () =>
        {
            _photoBookVisibilityGeneration++;
            ClearPhotoBookReturnConfirmation();
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
            _photoBookCommonNote.Text = string.Empty;
            _photoBookCommonNote.Visible = false;
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
        _photoBookCommonNote.Text = string.Empty;
        _photoBookCommonNote.Visible = false;
        _photoBookActionStatus.Text = string.Empty;
        _photoBookRefreshing = false;
        RefreshPhotoBookActionButtons();
    }

    private void RefreshPhotoBookPreparation()
    {
        var text = PhotoBookPreparationObjectiveText(_bridge);
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
            "S-BANK" => SBankFrontImagePath,
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
        var commonNote = photo.PhotoId == "P-COMMON" ? _bridge?.CommonFrameNoteText() : null;
        _photoBookCommonNote.Text = string.IsNullOrEmpty(commonNote) ? string.Empty : "Моя помета:\n" + commonNote;
        _photoBookCommonNote.Visible = !string.IsNullOrEmpty(commonNote);
        RefreshPhotoBookCommonRelationsStudy();
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
        if (GodotObject.IsInstanceValid(_photoBookCommonRelationsStudy))
        {
            _photoBookCommonRelationsStudy.Texture = null;
            _photoBookCommonRelationsStudy.Visible = false;
            _photoBookCommonRelationsCaption.Text = string.Empty;
            _photoBookCommonRelationsCaption.Visible = false;
            _photoBookCommonRelationsInspectButton.Visible = false;
            _photoBookCommonRelationsInspectButton.Disabled = true;
        }
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

    private DocumentImageContent? CurrentCommonRelationsStudy()
    {
        if (_notesLoadInProgress || _photoBookSelectedId != "P-COMMON"
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.IsQueuedForDeletion() || !bridge.IsPhotoWorldsCampaign
            || bridge.IsPhotoBookProjectionDeferred
            || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(_photoBookSession, session)
            || !bridge.PhotoBookPhotos().Any(photo => photo.PhotoId == "P-COMMON"))
            return null;

        return bridge.PhotoWorldKnowledgeEntries()
            .FirstOrDefault(entry => StringComparer.Ordinal.Equals(entry.SourceId, CommonRelationsSourceId))
            ?.Images?.FirstOrDefault(image => StringComparer.Ordinal.Equals(image.AssetId, CommonRelationsStudyAssetId));
    }

    private void RefreshPhotoBookCommonRelationsStudy()
    {
        var study = CurrentCommonRelationsStudy();
        if (study is null || !ResourceLoader.Exists(study.ResourcePath)
            || ResourceLoader.Load<Texture2D>(study.ResourcePath) is not { } texture)
        {
            if (ReferenceEquals(_photoBookImageReaderLaunchButton, _photoBookCommonRelationsInspectButton))
                ClosePhotoBookImageReader();
            return;
        }
        _photoBookCommonRelationsStudy.Texture = texture;
        _photoBookCommonRelationsStudy.Visible = true;
        _photoBookCommonRelationsCaption.Text = "Три взгляда с одной площадки.";
        _photoBookCommonRelationsCaption.Visible = true;
        _photoBookCommonRelationsInspectButton.Visible = true;
        _photoBookCommonRelationsInspectButton.Disabled = false;
    }

    private void OpenSelectedPhotoBookCommonRelationsReader()
    {
        if (!_screen.Visible || !_photoBookPage.Visible || _tabs.CurrentTab != PhotoBookTabIndex
            || CurrentCommonRelationsStudy() is not { } study
            || !ResourceLoader.Exists(study.ResourcePath)
            || ResourceLoader.Load<Texture2D>(study.ResourcePath) is null
            || _photoBookImageReader is not { } reader
            || _photoBookImageReaderBody is not { } body
            || _photoBookImageReaderHeader is not { } header)
            return;

        CloseBookInspection();
        body.Text = "Мои наброски к общему кадру\n\n" + study.Description;
        if (_photoBookImageReaderTitle is { } titleLabel) titleLabel.Text = "Мои наброски к общему кадру";
        reader.SetImages(new[] { study });
        _photoBookImageReaderLaunchButton = _photoBookCommonRelationsInspectButton;
        _photoBookSpread.Visible = false;
        header.Visible = true;
        reader.ShowPicture(true);
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
            _photoBookCommonNote,
            _photoBookCommonRelationsCaption,
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
        var showReturn = currentProjection && _bridge?.IsPhotoBookReturnScene == true;
        if (!showReturn || !IsPhotoBookReturnConfirmationCurrent()) ClearPhotoBookReturnConfirmation();
        var showEntry = !showReturn && selected is { Mounted: true }
            && (selected.PhotoId is "P-FOREST" or "P-WATER" or "P-HOUSE" or "P-COMMON" or "P-CELLAR" || selected.WorldCompleted);
        _photoBookEnterButton.Text = showReturn && _bridge is { } returnBridge
            ? _photoBookReturnSession is not null ? "Подтвердить возвращение"
                : returnBridge.ResolveText("urman.fullgame:text/pw-label-forest-book-return")
            : selected?.WorldCompleted == true ? "Вернуться в снимок"
                : selected?.HasResumePoint == true ? "Продолжить снимок" : "Войти в снимок";
        _photoBookEnterButton.Visible = showEntry || showReturn;
        _photoBookEnterButton.Disabled = true;
        _photoBookEntryHint.Visible = false;
        _photoBookEntryHint.Text = string.Empty;
        if ((showEntry || showReturn) && _bridge is { } entryBridge)
        {
            string reason;
            var canEnter = showReturn
                ? entryBridge.CanReturnFromPhotoBook(out reason)
                : entryBridge.CanEnterPhotoBookPhoto(selected!.PhotoId, out reason);
            // A world action may finish while the reader stays open; retry
            // rechecks its live guards without polling narrative rules per frame.
            _photoBookEnterButton.Disabled = _photoBookActionBusy || (!showReturn && !canEnter);
            _photoBookEntryHint.Text = canEnter && !showReturn
                ? selected?.WorldCompleted == true
                    ? "Знакомый мир. Сделанное сохранится; вернётесь к этому столу."
                    : selected?.PhotoId == "P-CELLAR"
                        ? selected.HasResumePoint
                            ? "У двери сухих сеней семейного дома выберите «Продолжить снимок». Сделанное сохранено."
                            : "У двери сухих сеней семейного дома выберите «Войти в снимок»."
                    : selected?.HasResumePoint == true
                        ? "Сделанное сохранено. Продолжите с последней доступной точки."
                        : reason
                : reason;
            _photoBookEntryHint.Visible = _photoBookEntryHint.Text.Length > 0;
        }
    }

    private async Task EnterSelectedPhotoBookAsync()
    {
        if (_photoBookActionBusy || _notesLoadInProgress
            || _bridge is not { } bridge || bridge.SessionIdentity is not { } session
            || !IsCurrentPhotoBookOperation(bridge, session)
            || !_screen.Visible || _tabs.CurrentTab != PhotoBookTabIndex || !_photoBookPage.Visible) return;
        var returning = bridge.IsPhotoBookReturnScene;
        var entryPhotoId = _photoBookSelectedId;
        if (!returning && (entryPhotoId is null || !IsCurrentMountedPhotoCard(bridge, session, entryPhotoId))) return;
        string reason;
        var canAct = returning
            ? bridge.CanReturnFromPhotoBook(out reason)
            : bridge.CanEnterPhotoBookPhoto(entryPhotoId!, out reason);
        if (!canAct)
        {
            RefreshPhotoBookActionButtons();
            _photoBookActionStatus.Text = reason;
            return;
        }

        if (returning && !IsPhotoBookReturnConfirmationCurrent())
        {
            _photoBookReturnBridge = bridge;
            _photoBookReturnSession = session;
            _photoBookReturnSourceScene = bridge.ActiveSceneId;
            _photoBookReturnConfirmation.Visible = true;
            _photoBookReturnCancel.Visible = true;
            _photoBookActionStatus.Text = string.Empty;
            RefreshPhotoBookActionButtons();
            return;
        }

        ClearPhotoBookReturnConfirmation();
        var visibilityGeneration = _photoBookVisibilityGeneration;
        bool CurrentUi() => visibilityGeneration == _photoBookVisibilityGeneration
            && IsCurrentPhotoBookOperation(bridge, session);
        _photoBookActionBusy = true;
        _photoBookActionStatus.Text = returning ? "Возвращаюсь домой…" : "Вхожу в снимок…";
        RefreshPhotoBookActionButtons();
        try
        {
            var returnOutcome = returning
                ? await bridge.ReturnFromPhotoBookAsync()
                : RuntimeBridge.PhotoBookReturnOutcome.Failed;
            var entered = returning
                ? returnOutcome == RuntimeBridge.PhotoBookReturnOutcome.ReturnedAndSaved
                : await bridge.EnterPhotoBookPhotoAsync(entryPhotoId!);
            if (!CurrentUi() || bridge.IsPhotoBookProjectionDeferred) return;
            if (returning && returnOutcome == RuntimeBridge.PhotoBookReturnOutcome.ReturnedSaveFailed)
            {
                RefreshPhotoBookPage();
                var gamepad = FindPlayer()?.CurrentInputDevice == "gamepad";
                var closeHint = InputBindingService.ActionHint("ui_cancel", gamepad);
                var pauseHint = InputBindingService.ActionHint("pause", gamepad);
                _photoBookActionStatus.Text = $"Вы вернулись домой, но автосохранение не удалось. Закройте альбом {closeHint}, откройте меню {pauseHint} и выберите «Сохранить».";
                return;
            }
            if (entered)
            {
                await CloseAsync(session, visibilityGeneration);
                return;
            }
            RefreshPhotoBookPage();
            if (returning && bridge.ActiveSceneId == "urman.fullgame:scene/pw-home")
            {
                _photoBookActionStatus.Text = "Не удалось подготовить место возвращения. Сохранение не обновлено.";
                return;
            }
            if (!returning && bridge.PhotoVisitPreparationCancelled)
            {
                _photoBookActionStatus.Text = "Переход отменён. Можно продолжить рассматривать книгу.";
                return;
            }
            canAct = returning
                ? bridge.CanReturnFromPhotoBook(out reason)
                : bridge.CanEnterPhotoBookPhoto(entryPhotoId!, out reason);
            _photoBookActionStatus.Text = canAct
                ? returning ? "Не удалось вернуться домой. Попробуйте ещё раз."
                    : "Не удалось войти в снимок. Попробуйте ещё раз."
                : reason;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Photo book entry failed: {exception.GetType().Name}");
            if (!CurrentUi()) return;
            RefreshPhotoBookPage();
            _photoBookActionStatus.Text = returning
                ? "Не удалось вернуться домой. Попробуйте ещё раз."
                : "Не удалось войти в снимок. Попробуйте ещё раз.";
        }
        finally
        {
            _photoBookActionBusy = false;
            if (GodotObject.IsInstanceValid(this) && IsInsideTree())
            {
                RefreshPhotoBookActionButtons();
                if (CurrentUi() && _screen.Visible && _photoBookEnterButton.IsVisibleInTree()
                    && !_photoBookEnterButton.Disabled) _photoBookEnterButton.GrabFocus();
            }
        }
    }

    private bool IsPhotoBookReturnConfirmationCurrent() =>
        _photoBookReturnBridge is { } bridge
        && _photoBookReturnSession is { } session
        && IsCurrentPhotoBookOperation(bridge, session)
        && _screen.Visible && _tabs.CurrentTab == PhotoBookTabIndex && _photoBookPage.Visible
        && bridge.IsPhotoBookReturnScene
        && StringComparer.Ordinal.Equals(_photoBookReturnSourceScene, bridge.ActiveSceneId);

    private void ClearPhotoBookReturnConfirmation()
    {
        _photoBookReturnBridge = null;
        _photoBookReturnSession = null;
        _photoBookReturnSourceScene = null;
        if (GodotObject.IsInstanceValid(_photoBookReturnConfirmation)) _photoBookReturnConfirmation.Visible = false;
        if (GodotObject.IsInstanceValid(_photoBookReturnCancel)) _photoBookReturnCancel.Visible = false;
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
