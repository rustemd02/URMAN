using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private Button _photoBookInspectFrontButton = null!;
    private HBoxContainer? _photoBookImageReaderHeader;
    private Label? _photoBookImageReaderTitle;
    private Button? _photoBookImageReaderReturnButton;
    private RichTextLabel? _photoBookImageReaderBody;
    private DocumentImageReader? _photoBookImageReader;

    private void BuildPhotoBookImageReader()
    {
        _photoBookImageReaderHeader = new HBoxContainer
        {
            Name = "PhotoFrontImageReaderHeader",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _photoBookImageReaderTitle = new Label
        {
            Name = "PhotoTitleAndOrigin",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        _photoBookImageReaderHeader.AddChild(_photoBookImageReaderTitle);
        _photoBookImageReaderReturnButton = new Button
        {
            Name = "ReturnToPhotoAlbum",
            Text = "Вернуться к фотоальбому",
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookImageReaderHeader.AddChild(_photoBookImageReaderReturnButton);
        _photoBookImageReaderReturnButton.Pressed += () => ClosePhotoBookImageReader(returnFocus: true);
        _photoBookPage.AddChild(_photoBookImageReaderHeader);

        _photoBookImageReaderBody = new RichTextLabel
        {
            Name = "PhotoFrontImageReaderText",
            Visible = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ScrollActive = true,
            FocusMode = Control.FocusModeEnum.All
        };
        _photoBookPage.AddChild(_photoBookImageReaderBody);
        _photoBookImageReader = DocumentImageReader.Attach(_photoBookImageReaderBody);
        _photoBookImageReader.Name = "PhotoFrontImageReader";
    }

    private void RefreshPhotoBookImageReaderAction(RuntimeBridge.PhotoBookPhoto photo)
    {
        if (_photoBookInspectFrontButton is null || !GodotObject.IsInstanceValid(_photoBookInspectFrontButton)) return;
        var path = PhotoBookFrontImageResourcePath(photo.PhotoId);
        var available = path is not null && ResourceLoader.Exists(path)
            && PhotoBookFrontTexture(photo.PhotoId) is not null;
        _photoBookInspectFrontButton.Visible = available;
        _photoBookInspectFrontButton.Disabled = !available;
    }

    private void OpenSelectedPhotoBookFrontReader()
    {
        if (!_screen.Visible || !_photoBookPage.Visible || _tabs.CurrentTab != PhotoBookTabIndex
            || _photoBookSelectedId is not { } selectedId
            || _bridge is not { } bridge
            || !ReferenceEquals(_photoBookSession, bridge.SessionIdentity)
            || _photoBookImageReader is not { } reader
            || _photoBookImageReaderBody is not { } body
            || _photoBookImageReaderHeader is not { } header)
            return;

        var photo = bridge.PhotoBookPhotos()
            .FirstOrDefault(candidate => candidate.PhotoId == selectedId);
        var path = photo is null ? null : PhotoBookFrontImageResourcePath(photo.PhotoId);
        if (photo is null || path is null || !ResourceLoader.Exists(path)
            || PhotoBookFrontTexture(photo.PhotoId) is null)
        {
            if (photo is not null) RefreshPhotoBookImageReaderAction(photo);
            return;
        }

        CloseBookInspection();
        var description = string.IsNullOrWhiteSpace(photo.FrontBrief) ? photo.Title : photo.FrontBrief!;
        body.Text = photo.Title + "\n\n" + description;
        if (_photoBookImageReaderTitle is { } titleLabel)
        {
            var origin = string.IsNullOrWhiteSpace(photo.OriginLabel)
                ? string.Empty
                : $"\nПроисхождение: {photo.OriginLabel}.";
            titleLabel.Text = photo.Title + origin;
        }
        reader.SetImages(new[]
        {
            new DocumentImageContent(photo.PhotoId, path, description)
        });
        _photoBookSpread.Visible = false;
        header.Visible = true;
        reader.ShowPicture(true);
    }

    private void ClosePhotoBookImageReader(bool returnFocus = false)
    {
        if (_photoBookImageReader is { } reader && GodotObject.IsInstanceValid(reader))
            reader.SetImages(null);
        if (_photoBookImageReaderBody is { } body && GodotObject.IsInstanceValid(body))
        {
            body.Text = string.Empty;
            body.Visible = false;
        }
        if (_photoBookImageReaderHeader is { } header && GodotObject.IsInstanceValid(header))
            header.Visible = false;
        if (_photoBookImageReaderTitle is { } titleLabel && GodotObject.IsInstanceValid(titleLabel))
            titleLabel.Text = string.Empty;
        if (_photoBookSpread is not null && GodotObject.IsInstanceValid(_photoBookSpread))
            _photoBookSpread.Visible = true;

        if (returnFocus && _screen.Visible && _photoBookPage.Visible
            && _tabs.CurrentTab == PhotoBookTabIndex
            && _photoBookInspectFrontButton is { } button
            && GodotObject.IsInstanceValid(button) && button.IsVisibleInTree())
            button.GrabFocus();
    }
}
