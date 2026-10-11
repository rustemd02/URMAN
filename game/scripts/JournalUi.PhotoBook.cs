using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private const int PhotoBookTabIndex = 7;
    private VBoxContainer _photoBookPage = null!;
    private ItemList _photoBookList = null!;
    private ScrollContainer _photoBookScroll = null!;
    private VBoxContainer _photoBookSpread = null!;
    private Label _photoBookEmpty = null!;
    private Label _photoBookTitle = null!;
    private Label _photoBookPlacement = null!;
    private Label _photoBookFrontHeading = null!;
    private Label _photoBookFrontBrief = null!;
    private HSeparator _photoBookSeparator = null!;
    private Label _photoBookBackHeading = null!;
    private Label _photoBookBackText = null!;
    private IReadOnlyList<RuntimeBridge.PhotoBookPhoto> _photoBookProjection = [];
    private string? _photoBookSelectedId;
    private object? _photoBookSession;
    private bool _photoBookRefreshing;

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
        _photoBookPage.AddChild(spread);
        layout.AddChild(_photoBookPage);

        _photoBookList = new ItemList
        {
            Name = "AcquiredPhotos",
            CustomMinimumSize = new Vector2(240, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
            AllowReselect = true
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
        _photoBookFrontHeading = new Label { Text = "На лицевой стороне · описание снимка",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _photoBookSpread.AddChild(_photoBookFrontHeading);
        _photoBookFrontBrief = new Label { Name = "FrontDescription",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _photoBookSpread.AddChild(_photoBookFrontBrief);
        _photoBookSeparator = new HSeparator();
        _photoBookSpread.AddChild(_photoBookSeparator);
        _photoBookBackHeading = new Label { Text = "Оборот",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _photoBookSpread.AddChild(_photoBookBackHeading);
        _photoBookBackText = new Label { Name = "BackText",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _photoBookSpread.AddChild(_photoBookBackText);

        _tabs.TabChanged += index =>
        {
            _photoBookPage.Visible = index == PhotoBookTabIndex;
            if (index != PhotoBookTabIndex) return;
            RefreshPhotoBookPage();
            if (_screen.Visible) FocusPhotoBookPage();
        };
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

        if (!ReferenceEquals(_photoBookSession, session))
        {
            _photoBookSession = session;
            _photoBookSelectedId = null;
        }

        _photoBookProjection = bridge.PhotoBookPhotos();
        var selectedIndex = _photoBookProjection.ToList()
            .FindIndex(photo => photo.PhotoId == _photoBookSelectedId);
        if (selectedIndex < 0 && _photoBookProjection.Count > 0) selectedIndex = 0;

        _photoBookRefreshing = true;
        _photoBookList.Clear();
        for (var index = 0; index < _photoBookProjection.Count; index++)
            _photoBookList.AddItem($"{index + 1:D2} · {_photoBookProjection[index].Title}");

        var hasPhotos = selectedIndex >= 0;
        _photoBookEmpty.Visible = !hasPhotos;
        _photoBookTitle.Visible = hasPhotos;
        _photoBookPlacement.Visible = hasPhotos;
        _photoBookFrontHeading.Visible = hasPhotos;
        _photoBookFrontBrief.Visible = hasPhotos;
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
            _photoBookTitle.Text = string.Empty;
            _photoBookPlacement.Text = string.Empty;
            _photoBookFrontBrief.Text = string.Empty;
            _photoBookBackText.Text = string.Empty;
        }

        _photoBookRefreshing = false;
    }

    private void ClearPhotoBookPage(bool resetSession)
    {
        if (resetSession)
        {
            _photoBookSession = null;
            _photoBookSelectedId = null;
        }
        _photoBookProjection = [];
        _photoBookRefreshing = true;
        _photoBookList.Clear();
        _photoBookEmpty.Visible = true;
        _photoBookTitle.Visible = false;
        _photoBookPlacement.Visible = false;
        _photoBookFrontHeading.Visible = false;
        _photoBookFrontBrief.Visible = false;
        _photoBookSeparator.Visible = false;
        _photoBookBackHeading.Visible = false;
        _photoBookBackText.Visible = false;
        _photoBookTitle.Text = string.Empty;
        _photoBookPlacement.Text = string.Empty;
        _photoBookFrontBrief.Text = string.Empty;
        _photoBookBackText.Text = string.Empty;
        _photoBookRefreshing = false;
    }

    private void SelectPhotoBookPhoto(long index)
    {
        if (_photoBookRefreshing || index < 0 || index >= _photoBookProjection.Count) return;
        var selected = _photoBookProjection[(int)index];
        _photoBookSelectedId = selected.PhotoId;
        ShowPhotoBookPhoto(selected);
    }

    private void ShowPhotoBookPhoto(RuntimeBridge.PhotoBookPhoto photo)
    {
        _photoBookTitle.Text = photo.Title;
        _photoBookPlacement.Text = photo.Mounted
            ? "Место в альбоме: снимок вклеен на свою страницу."
            : "Место в альбоме: снимок пока не вклеен.";
        _photoBookFrontBrief.Text = string.IsNullOrWhiteSpace(photo.FrontBrief)
            ? "Описание снимка пока не добавлено."
            : photo.FrontBrief;
        _photoBookBackText.Text = !photo.BackRead
            ? "Оборот ещё не прочитан."
            : string.IsNullOrWhiteSpace(photo.BackTextRu)
                ? "На обороте пока нет записи."
                : photo.BackTextRu;
    }

    private void FocusPhotoBookPage()
    {
        if (_photoBookProjection.Count > 0) _photoBookList.GrabFocus();
        else _photoBookEmpty.GrabFocus();
    }
}
