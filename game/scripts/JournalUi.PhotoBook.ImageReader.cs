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
    private Button? _photoBookImageReaderLaunchButton;

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
        if (_photoBookInspectFrontButton is { } frontButton
            && GodotObject.IsInstanceValid(frontButton))
        {
            var path = PhotoBookFrontImageResourcePath(photo.PhotoId);
            var available = path is not null && ResourceLoader.Exists(path)
                && PhotoBookFrontTexture(photo.PhotoId) is not null;
            frontButton.Visible = available;
            frontButton.Disabled = !available;
        }

        if (_photoBookInspectBackButton is { } backButton
            && GodotObject.IsInstanceValid(backButton))
        {
            var available = false;
            if (!_notesLoadInProgress
                && _bridge is { } bridge && GodotObject.IsInstanceValid(bridge)
                && !bridge.IsQueuedForDeletion() && bridge.IsPhotoWorldsCampaign
                && bridge.SessionIdentity is { } session
                && ReferenceEquals(_photoBookSession, session)
                && StringComparer.Ordinal.Equals(_photoBookSelectedId, photo.PhotoId))
            {
                var currentPhoto = bridge.PhotoBookPhotos()
                    .FirstOrDefault(candidate => candidate.PhotoId == photo.PhotoId);
                available = currentPhoto is not null
                    && IsReadableAuthoredPhotoBack(currentPhoto)
                    && PhotoBookBackTexture(currentPhoto.PhotoId) is not null;
            }
            backButton.Visible = available;
            backButton.Disabled = !available;
        }
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
        _photoBookImageReaderLaunchButton = _photoBookInspectFrontButton;
        _photoBookSpread.Visible = false;
        header.Visible = true;
        reader.ShowPicture(true);
    }

    private void OpenSelectedPhotoBookBackReader()
    {
        if (!_screen.Visible || !_photoBookPage.Visible || _tabs.CurrentTab != PhotoBookTabIndex
            || _photoBookSelectedId is not { } selectedId
            || _bridge is not { } bridge || !GodotObject.IsInstanceValid(bridge)
            || bridge.IsQueuedForDeletion() || !bridge.IsPhotoWorldsCampaign
            || bridge.SessionIdentity is not { } session
            || !ReferenceEquals(_photoBookSession, session)
            || _notesLoadInProgress
            || _photoBookImageReader is not { } reader
            || _photoBookImageReaderBody is not { } body
            || _photoBookImageReaderHeader is not { } header)
            return;

        var photo = bridge.PhotoBookPhotos()
            .FirstOrDefault(candidate => candidate.PhotoId == selectedId);
        if (photo is null || !IsReadableAuthoredPhotoBack(photo)
            || !PhotoBookBackImages.TryGetValue(photo.PhotoId, out var authoredBack)
            || !ResourceLoader.Exists(authoredBack.Path)
            || PhotoBookBackTexture(photo.PhotoId) is null)
        {
            if (photo is not null) RefreshPhotoBookImageReaderAction(photo);
            return;
        }

        CloseBookInspection();
        body.Text = authoredBack.Caption;
        if (_photoBookImageReaderTitle is { } titleLabel)
        {
            var origin = string.IsNullOrWhiteSpace(photo.OriginLabel)
                ? string.Empty
                : $"\nПроисхождение: {photo.OriginLabel}.";
            titleLabel.Text = photo.Title + origin;
        }
        reader.SetImages(new[]
        {
            new DocumentImageContent(photo.PhotoId, authoredBack.Path, authoredBack.Caption)
        });
        _photoBookImageReaderLaunchButton = _photoBookInspectBackButton;
        _photoBookSpread.Visible = false;
        header.Visible = true;
        reader.ShowPicture(true);
    }

    private static bool IsReadableAuthoredPhotoBack(RuntimeBridge.PhotoBookPhoto photo) =>
        photo.BackRead
        && PhotoBookBackImages.TryGetValue(photo.PhotoId, out var authoredBack)
        && string.Equals(photo.BackTextRu, authoredBack.Caption, StringComparison.Ordinal);

    private static Texture2D? PhotoBookBackTexture(string photoId)
    {
        if (!PhotoBookBackImages.TryGetValue(photoId, out var authoredBack)
            || !ResourceLoader.Exists(authoredBack.Path))
            return null;
        return ResourceLoader.Load<Texture2D>(authoredBack.Path);
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

        var launchButton = _photoBookImageReaderLaunchButton;
        _photoBookImageReaderLaunchButton = null;
        if (returnFocus && _screen.Visible && _photoBookPage.Visible
            && _tabs.CurrentTab == PhotoBookTabIndex
            && launchButton is { } button
            && GodotObject.IsInstanceValid(button) && button.IsVisibleInTree())
            button.GrabFocus();
    }
}
