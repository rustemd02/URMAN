using Godot;
using System;
using System.Linq;

namespace Urman.Godot;

public partial class JournalUi
{
    private const string InspectionCaptionFontPath = "res://assets/fonts/PT_Sans-Web-Regular.ttf";
    private const float InspectionCaptionPixelSize = 0.000125f;
    private const float InspectionCaptionWidthMeters = 0.16f;
    private Label3D? _bookInspectionPhotoCaption;
    private string? _bookInspectionPhotoCaptionTitle;
    private bool _bookInspectionOpenPose;

    private void RefreshBookInspectionPhotoCaption(string title)
    {
        if (!_bookInspectionOpenPose || string.IsNullOrWhiteSpace(title))
        {
            if (_bookInspectionPhotoCaption is { } hiddenCaption
                && GodotObject.IsInstanceValid(hiddenCaption) && hiddenCaption.Visible)
            {
                hiddenCaption.Visible = false;
                RequestBookInspectionTextRender();
            }
            return;
        }

        var caption = _bookInspectionPhotoCaption;
        if (caption is null || !GodotObject.IsInstanceValid(caption))
        {
            _bookInspectionPhotoCaption = null;
            _bookInspectionPhotoCaptionTitle = null;
            var hinge = _bookInspectionModel?.FindChildren("AlbumPageTurnHinge", nameof(Node3D), true, false)
                .OfType<Node3D>().FirstOrDefault();
            if (hinge is null) return;

            caption = new Label3D
            {
                Name = "SelectedPhotoPhysicalCaption",
                Font = ResourceLoader.Load<FontFile>(InspectionCaptionFontPath) ?? UrmanUiTheme.BodyFont,
                FontSize = 40,
                PixelSize = InspectionCaptionPixelSize,
                Width = InspectionCaptionWidthMeters / InspectionCaptionPixelSize,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector3(.110f, .0004f, .076f),
                RotationDegrees = new Vector3(-90f, 0f, 0f),
                Modulate = Color.FromHtml("342f2a"),
                OutlineSize = 0,
                Shaded = true,
                NoDepthTest = false,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false
            };
            hinge.AddChild(caption);
            _bookInspectionPhotoCaption = caption;
        }

        var changed = false;
        if (!StringComparer.Ordinal.Equals(_bookInspectionPhotoCaptionTitle, title))
        {
            caption.Text = title;
            _bookInspectionPhotoCaptionTitle = title;
            changed = true;
        }
        if (!caption.Visible)
        {
            caption.Visible = true;
            changed = true;
        }
        if (changed) RequestBookInspectionTextRender();
    }

    private void ClearBookInspectionPhotoCaption()
    {
        if (_bookInspectionPhotoCaption is { } caption && GodotObject.IsInstanceValid(caption))
        {
            caption.Visible = false;
            caption.QueueFree();
            RequestBookInspectionTextRender();
        }
        _bookInspectionPhotoCaption = null;
        _bookInspectionPhotoCaptionTitle = null;
    }

    private void RequestBookInspectionTextRender()
    {
        if (_bookInspectionViewport is not null)
            _bookInspectionViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }
}
