using System.Collections.Generic;
using Godot;

namespace Urman.Godot;

public partial class JournalUi
{
    private bool _focusedPhotoInspection, _focusedPhotoBack;
    private Button? _focusedPhotoFlip;
    private Button? _focusedPhotoAlbum;
    private Label? _focusedPhotoFeedback;
    private readonly Dictionary<Control, bool> _focusedPhotoChrome = new();

    private void BuildFocusedPhotoActions(HFlowContainer actions)
    {
        _focusedPhotoFlip = new Button
        {
            Name = "TurnPhysicalPhoto", Text = "Перевернуть снимок", Visible = false,
            FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0, 44)
        };
        actions.AddChild(_focusedPhotoFlip);
        _focusedPhotoFlip.Pressed += async () =>
        {
            if (!_focusedPhotoInspection || _bridge is not { } bridge || bridge.SessionIdentity is not { } session)
                return;
            if (_focusedPhotoBack)
            {
                OpenSelectedPhotoBookFrontReader();
                _focusedPhotoFlip?.GrabFocus();
                return;
            }
            var photo = bridge.PhotoBookPhotos().FirstOrDefault(item => item.PhotoId == _photoBookSelectedId);
            if (photo is null) return;
            var generation = _photoBookVisibilityGeneration;
            if (_focusedPhotoFeedback is { } feedback) { feedback.Text = string.Empty; feedback.Visible = false; }
            // This is the existing authored reading command, not a second flag
            // or an automatic reward for merely opening the photograph.
            if (!photo.BackRead) await PerformPhotoBookActionAsync(readBack: true);
            if (!_focusedPhotoInspection || !ReferenceEquals(bridge.SessionIdentity, session)
                || !ReferenceEquals(_photoBookSession, session) || !_screen.Visible
                || generation != _photoBookVisibilityGeneration) return;
            var readPhoto = bridge.PhotoBookPhotos().FirstOrDefault(item => item.PhotoId == _photoBookSelectedId);
            if (readPhoto?.BackRead != true)
            {
                if (_focusedPhotoFeedback is { } failure)
                {
                    failure.Text = _photoBookActionStatus.Text;
                    failure.Visible = !string.IsNullOrWhiteSpace(failure.Text);
                }
                _focusedPhotoFlip?.GrabFocus();
                return;
            }
            OpenSelectedPhotoBookBackReader();
            if (!_focusedPhotoBack && _photoBookImageReaderBody is { } body)
            {
                // The existing text reader remains usable if a reverse-face
                // image is absent. It shows only the canonically read caption.
                body.Text = _photoBookBackText.Text;
                _photoBookImageReader?.ShowPicture(false);
                _focusedPhotoBack = true;
                RefreshFocusedPhotoInspection();
            }
            _focusedPhotoFlip?.GrabFocus();
        };
        _focusedPhotoAlbum = new Button
        {
            Name = "BrowseAlbum", Text = "К фотоальбому", Visible = false,
            FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(0, 44)
        };
        actions.AddChild(_focusedPhotoAlbum);
        _focusedPhotoAlbum.Pressed += () => ClosePhotoBookImageReader(returnFocus: true);
        _focusedPhotoFeedback = new Label
        {
            Name = "PhotoReadingStatus", Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = Control.FocusModeEnum.All
        };
        _photoBookImageReaderHeader!.AddChild(_focusedPhotoFeedback);
    }

    private void BeginFocusedPhotoInspection()
    {
        if (_photoBookImageReaderHeader?.Visible != true || _photoBookImageReader?.Visible != true) return;
        _focusedPhotoInspection = true;
        _photoBookImageReader.ShowCanvasTooltip(false);
        RefreshFocusedPhotoInspection();
        RefitToViewport();
        _focusedPhotoFlip?.GrabFocus();
    }

    private void RefreshFocusedPhotoInspection()
    {
        if (!_focusedPhotoInspection) return;
        void Hide(Control control)
        {
            if (!_focusedPhotoChrome.ContainsKey(control)) _focusedPhotoChrome.Add(control, control.Visible);
            control.Visible = false;
        }
        Hide(_tabs);
        if (_book.GetNodeOrNull<Control>("Layout/Header") is { } journalHeader) Hide(journalHeader);
        foreach (var child in _photoBookPage.GetChildren())
            if (child is Control control && !ReferenceEquals(control, _photoBookImageReaderHeader)
                && !ReferenceEquals(control, _photoBookImageReaderBody)
                && !ReferenceEquals(control, _photoBookImageReader)) Hide(control);
        // The same reader still owns zoom, pan, description and focus. Its
        // document Text/Image tabs add no choice while holding this one photo.
        if (_photoBookImageReader?.GetNodeOrNull<Control>("Tabs") is { } readerTabs) Hide(readerTabs);
        if (_photoBookImageReaderReturnButton is { } close)
        {
            close.Text = "Вернуться к столу";
            close.CustomMinimumSize = new Vector2(0, 44);
        }
        var selected = _photoBookProjection.FirstOrDefault(photo => photo.PhotoId == _photoBookSelectedId);
        if (_photoBookImageReaderTitle is { } title && selected is not null)
            title.Text = selected.Title + (_focusedPhotoBack ? " · оборот" : string.Empty);
        if (_focusedPhotoFlip is { } flip)
        {
            flip.Visible = true;
            flip.Text = _focusedPhotoBack ? "Посмотреть снимок" : "Перевернуть снимок";
            flip.Disabled = _photoBookActionBusy || _notesLoadInProgress
                || selected is null || (!_focusedPhotoBack && !selected.BackRead && !selected.CanReadBack);
        }
        if (_focusedPhotoAlbum is { } album) album.Visible = true;
        if (_photoBookActionBusy && _focusedPhotoFeedback is { } status)
        {
            status.Text = _photoBookActionStatus.Text;
            status.Visible = true;
        }
    }

    private void EndFocusedPhotoInspection()
    {
        if (!_focusedPhotoInspection) return;
        _focusedPhotoInspection = _focusedPhotoBack = false;
        _photoBookImageReader?.ShowCanvasTooltip(true);
        foreach (var (control, visible) in _focusedPhotoChrome)
            if (GodotObject.IsInstanceValid(control)) control.Visible = visible;
        _focusedPhotoChrome.Clear();
        if (_focusedPhotoFlip is { } flip) flip.Visible = false;
        if (_focusedPhotoAlbum is { } album) album.Visible = false;
        if (_focusedPhotoFeedback is { } status) { status.Visible = false; status.Text = string.Empty; }
        if (_photoBookImageReaderReturnButton is { } close) close.Text = "Вернуться к фотоальбому";
        RefitToViewport();
    }

    private bool RefitFocusedPhotoInspection(Vector2 viewport)
    {
        if (!_focusedPhotoInspection) return false;
        var size = new Vector2(Mathf.Max(1, viewport.X - 32), Mathf.Max(1, viewport.Y - 24));
        _book.SetAnchorsPreset(Control.LayoutPreset.Center);
        _book.OffsetLeft = -size.X * .5f;
        _book.OffsetRight = size.X * .5f;
        _book.OffsetTop = -size.Y * .5f;
        _book.OffsetBottom = size.Y * .5f;
        _book.PivotOffset = size * .5f;
        return true;
    }
}
