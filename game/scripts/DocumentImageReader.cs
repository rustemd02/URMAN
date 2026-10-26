using Godot;

namespace Urman.Godot;

/// <summary>
/// Shared presentation for paper, archive and saved journal images. It does not
/// own input capture, document effects or a second modal: the enclosing reader
/// keeps those contracts, including closing back to the unchanged world view.
/// </summary>
public partial class DocumentImageReader : VBoxContainer
{
    private RichTextLabel _body = null!;
    private HBoxContainer _tabs = null!;
    private VBoxContainer _picturePage = null!;
    private Control _canvas = null!;
    private TextureRect _picture = null!;
    private Control _referenceCanvas = null!;
    private Control _currentCanvas = null!;
    private TextureRect _referencePicture = null!;
    private HBoxContainer _comparisonCaptions = null!;
    private Label _referenceCaption = null!;
    private Label _currentCaption = null!;
    private bool _comparing;
    private Button _textTab = null!;
    private Button _imageTab = null!;
    private Button _previous = null!;
    private Button _next = null!;
    private Button _descriptionToggle = null!;
    private RichTextLabel _description = null!;
    private Label _counter = null!;
    private IReadOnlyList<DocumentImageContent> _images = [];
    private int _index;
    private float _zoom = 1;
    private Vector2 _pan;
    private bool _dragging;

    public int ImageCount => _images.Count;
    public bool ShowingImage => Visible && _picturePage.Visible;
    public string? ActiveAssetId => _images.Count == 0 ? null : _images[_index].AssetId;
    public float ZoomFactor => _zoom;
    public Texture2D? DisplayedTexture => _picture.Texture;

    public static DocumentImageReader Attach(RichTextLabel body)
    {
        // Returning from the image is also a keyboard reading action. Some
        // enclosing scenes leave the body with accessibility-only focus.
        body.FocusMode = FocusModeEnum.All;
        var parent = body.GetParent();
        var reader = new DocumentImageReader
        {
            Name = "DocumentImages", _body = body, Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        parent.AddChild(reader);
        parent.MoveChild(reader, body.GetIndex());
        return reader;
    }

    public override void _Ready()
    {
        _tabs = new HBoxContainer { Name = "Tabs" };
        AddChild(_tabs);
        _textTab = ButtonIn(_tabs, "Text", "Текст", () => ShowPicture(false));
        _imageTab = ButtonIn(_tabs, "Image", "Изображение", () => ShowPicture(true));
        _picturePage = new VBoxContainer { Name = "PicturePage", Visible = false,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(_picturePage);
        var tools = new HBoxContainer { Name = "Tools" };
        _picturePage.AddChild(tools);
        _previous = ButtonIn(tools, "Previous", "‹", () => SelectImage(_index - 1));
        _counter = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center };
        tools.AddChild(_counter);
        _next = ButtonIn(tools, "Next", "›", () => SelectImage(_index + 1));
        ButtonIn(tools, "ZoomOut", "−", () => Zoom(1 / 1.4f));
        ButtonIn(tools, "Fit", "Уместить", ResetView);
        ButtonIn(tools, "ZoomIn", "+", () => Zoom(1.4f));
        _comparisonCaptions = new HBoxContainer { Name = "ComparisonCaptions", Visible = false,
            MouseFilter = MouseFilterEnum.Ignore };
        _comparisonCaptions.AddThemeConstantOverride("separation", 12);
        _referenceCaption = new Label { Name = "ReferenceCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _currentCaption = new Label { Name = "CurrentCaption", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _comparisonCaptions.AddChild(_referenceCaption);
        _comparisonCaptions.AddChild(_currentCaption);
        _picturePage.AddChild(_comparisonCaptions);
        _canvas = new Control { Name = "ImageCanvas", ClipContents = true,
            SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 80), MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All,
            TooltipText = "Колесо — масштаб; перетаскивание или стрелки — сдвиг изображения" };
        _picturePage.AddChild(_canvas);
        _picture = new TextureRect { Name = "Image", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        _referenceCanvas = new Control { Name = "ReferenceCanvas", ClipContents = true, Visible = false,
            MouseFilter = MouseFilterEnum.Ignore };
        _currentCanvas = new Control { Name = "CurrentCanvas", ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore };
        _canvas.AddChild(_referenceCanvas);
        _canvas.AddChild(_currentCanvas);
        _referencePicture = new TextureRect { Name = "ReferenceImage", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        _referenceCanvas.AddChild(_referencePicture);
        _currentCanvas.AddChild(_picture);
        _canvas.Resized += LayoutPicture;
        _canvas.GuiInput += CanvasInput;
        _descriptionToggle = ButtonIn(_picturePage, "Describe", "Описание изображения", () =>
        {
            _description.Visible = !_description.Visible;
            _descriptionToggle.Text = _description.Visible ? "Скрыть описание" : "Описание изображения";
        });
        _description = new RichTextLabel { Name = "Description", Visible = false, ScrollActive = true,
            CustomMinimumSize = new Vector2(0, 100), FocusMode = FocusModeEnum.All };
        _picturePage.AddChild(_description);
    }

    public void SetImages(IReadOnlyList<DocumentImageContent>? images)
    {
        _images = images ?? [];
        _dragging = false;
        ClearComparison();
        _picture.Texture = null;
        _description.Text = string.Empty;
        _description.Visible = false;
        _descriptionToggle.Text = "Описание изображения";
        _picturePage.Visible = false;
        _body.Visible = true;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        Visible = _images.Count > 0;
        _textTab.Disabled = true;
        _imageTab.Disabled = false;
        if (_images.Count > 0) SelectImage(0);
    }

    public void CompareWith(DocumentImageContent reference, string currentCaption)
    {
        if (_images.Count != 1 || _picture.Texture is null) return;
        ClearComparison();
        if (!ResourceLoader.Exists(reference.ResourcePath)
            || ResourceLoader.Load<Texture2D>(reference.ResourcePath) is not { } texture)
        {
            GD.PushWarning($"Comparison image is missing from the game: {reference.AssetId} ({reference.ResourcePath})");
            LayoutPicture();
            return;
        }
        _referencePicture.Texture = texture;
        _referenceCaption.Text = reference.Description;
        _currentCaption.Text = currentCaption;
        _comparing = true;
        _referenceCanvas.Visible = _comparisonCaptions.Visible = true;
        ResetView();
    }

    private void ClearComparison()
    {
        _comparing = false;
        _referencePicture.Texture = null;
        _referenceCanvas.Visible = false;
        _comparisonCaptions.Visible = false;
        _referenceCaption.Text = _currentCaption.Text = string.Empty;
    }

    public void ShowPicture(bool visible)
    {
        if (_images.Count == 0) return;
        _body.Visible = !visible;
        _picturePage.Visible = visible;
        SizeFlagsVertical = visible ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        _textTab.Disabled = !visible;
        _imageTab.Disabled = visible;
        if (visible) _canvas.GrabFocus();
        else _body.GrabFocus();
        LayoutPicture();
    }

    private void SelectImage(int index)
    {
        if (_images.Count == 0) return;
        _index = Mathf.Clamp(index, 0, _images.Count - 1);
        var image = _images[_index];
        _picture.Texture = ResourceLoader.Load<Texture2D>(image.ResourcePath);
        _description.Text = image.Description;
        _counter.Text = _images.Count > 1 ? $"{_index + 1} / {_images.Count}" : string.Empty;
        _previous.Visible = _next.Visible = _images.Count > 1;
        _previous.Disabled = _index == 0;
        _next.Disabled = _index == _images.Count - 1;
        if (_picture.Texture is null)
        {
            GD.PushError($"Document image is missing from the game: {image.AssetId} ({image.ResourcePath})");
            _description.Text = "Изображение недоступно.\n\n" + image.Description;
            _description.Visible = true;
        }
        ResetView();
    }

    private void ResetView()
    {
        _zoom = 1;
        _pan = Vector2.Zero;
        LayoutPicture();
    }

    public void Zoom(float multiplier)
    {
        var previous = _zoom;
        _zoom = Mathf.Clamp(_zoom * multiplier, 1, 5);
        _pan *= _zoom / previous;
        LayoutPicture();
    }

    private void LayoutPicture()
    {
        if (_picture.Texture is not { } texture || _canvas.Size.X <= 0 || _canvas.Size.Y <= 0) return;
        if (_comparing && _referencePicture.Texture is { } reference)
        {
            LayoutComparisonPictures(texture, reference);
            return;
        }
        _currentCanvas.Position = Vector2.Zero;
        _currentCanvas.Size = _canvas.Size;
        var natural = texture.GetSize();
        var fit = Mathf.Min(_canvas.Size.X / natural.X, _canvas.Size.Y / natural.Y);
        var size = natural * fit * _zoom;
        var limit = new Vector2(Mathf.Max(0, (size.X - _canvas.Size.X) * .5f),
            Mathf.Max(0, (size.Y - _canvas.Size.Y) * .5f));
        _pan = new Vector2(Mathf.Clamp(_pan.X, -limit.X, limit.X), Mathf.Clamp(_pan.Y, -limit.Y, limit.Y));
        _picture.Size = size;
        _picture.Position = (_canvas.Size - size) * .5f + _pan;
    }

    private void LayoutComparisonPictures(Texture2D current, Texture2D reference)
    {
        var viewport = new Vector2(Mathf.Max(1, (_canvas.Size.X - 12f) * .5f), _canvas.Size.Y);
        _referenceCanvas.Size = _currentCanvas.Size = viewport;
        _referenceCanvas.Position = Vector2.Zero;
        _currentCanvas.Position = new Vector2(viewport.X + 12f, 0);
        var currentNatural = current.GetSize();
        var referenceNatural = reference.GetSize();
        var currentSize = currentNatural * Mathf.Min(viewport.X / currentNatural.X, viewport.Y / currentNatural.Y) * _zoom;
        var referenceSize = referenceNatural * Mathf.Min(viewport.X / referenceNatural.X, viewport.Y / referenceNatural.Y) * _zoom;
        var currentLimit = new Vector2(Mathf.Max(0, (currentSize.X - viewport.X) * .5f),
            Mathf.Max(0, (currentSize.Y - viewport.Y) * .5f));
        var referenceLimit = new Vector2(Mathf.Max(0, (referenceSize.X - viewport.X) * .5f),
            Mathf.Max(0, (referenceSize.Y - viewport.Y) * .5f));
        var limit = new Vector2(Mathf.Max(currentLimit.X, referenceLimit.X), Mathf.Max(currentLimit.Y, referenceLimit.Y));
        _pan = new Vector2(Mathf.Clamp(_pan.X, -limit.X, limit.X), Mathf.Clamp(_pan.Y, -limit.Y, limit.Y));
        // Shared fractional pan keeps both edges reachable even when the two prints have different proportions.
        var fraction = new Vector2(limit.X > 0 ? _pan.X / limit.X : 0, limit.Y > 0 ? _pan.Y / limit.Y : 0);
        _picture.Size = currentSize;
        _picture.Position = (viewport - currentSize) * .5f + fraction * currentLimit;
        _referencePicture.Size = referenceSize;
        _referencePicture.Position = (viewport - referenceSize) * .5f + fraction * referenceLimit;
    }

    private void CanvasInput(InputEvent input)
    {
        if (input is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left) { _dragging = mouse.Pressed; _canvas.GrabFocus(); }
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelUp) Zoom(1.25f);
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelDown) Zoom(.8f);
            else return;
        }
        else if (input is InputEventMouseMotion motion && _dragging)
        {
            _pan += motion.Relative;
            LayoutPicture();
        }
        else if (_zoom > 1 && input.IsPressed())
        {
            var previousPan = _pan;
            if (input.IsAction("ui_left")) _pan.X += 48;
            else if (input.IsAction("ui_right")) _pan.X -= 48;
            else if (input.IsAction("ui_up")) _pan.Y += 48;
            else if (input.IsAction("ui_down")) _pan.Y -= 48;
            else return;
            LayoutPicture();
            // At the edge, let ordinary directional focus reach the reader buttons.
            if (_pan == previousPan) return;
        }
        else return;
        _canvas.AcceptEvent();
    }

    private static Button ButtonIn(Node parent, string name, string label, Action pressed)
    {
        var button = new Button { Name = name, Text = label, CustomMinimumSize = new Vector2(44, 44),
            FocusMode = FocusModeEnum.All };
        parent.AddChild(button);
        button.Pressed += pressed;
        return button;
    }
}
