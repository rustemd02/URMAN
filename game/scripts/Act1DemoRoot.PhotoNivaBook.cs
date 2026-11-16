using Godot;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    private Node3D? _photoNivaLapBook;
    private bool _photoNivaPhotoInspected, _photoNivaPhotoShown, _photoNivaSignalAnswered;
    private float _photoNivaBookInspection, _photoNivaBookShowing;
    private float _photoNivaEvidenceClock = -1f;
    private Vector3 _photoNivaDriverGazeRest;
    private Label? _photoNivaEvidenceCue;
    private static readonly Vector3 PhotoNivaBookRest = new(.52f, 1.10f, -.12f);

    private void BuildPhotoNivaLapBook()
    {
        ReleasePhotoNivaLapBook();
        const string bookPath = "res://assets/models/photoworlds/family-book-v1.glb";
        const string printPath = "res://assets/textures/photobook/p-forest-front-v2.png";
        if (_rideNiva is null || ResourceLoader.Load<PackedScene>(bookPath) is not { } bookScene
            || ResourceLoader.Load<Texture2D>(printPath) is not { } print) return;

        var book = bookScene.Instantiate<Node3D>();
        var insert = book.FindChildren("AlbumInsert", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().FirstOrDefault();
        var animations = book.FindChildren("*", nameof(AnimationPlayer), true, false)
            .OfType<AnimationPlayer>().ToArray();
        if (insert is null || !animations.Any(player => player.HasAnimation("open")))
        {
            book.Free();
            return;
        }

        // Car space, not camera space: the open volume stays on the passenger's
        // lap when he looks up. This instance grants no book or photo progress.
        var lap = new Node3D
        {
            Name = "NivaOpenFamilyBook",
            Position = PhotoNivaBookRest
        };
        lap.SetMeta("presentationOnly", true);
        lap.AddChild(book);
        _rideNiva.AddChild(lap);
        _photoNivaLapBook = lap;
        SetPhotoNivaBookPose(book, "open");

        // Match the existing 150 x 100 mm AlbumInsert. Its top is local
        // Y=0.000325; the print stays below the four physical corner pockets.
        insert.AddChild(new MeshInstance3D
        {
            Name = "NivaForestPrint",
            Position = new(0, .00040f, 0),
            RotationDegrees = new(-90f, 0, 0),
            Mesh = new QuadMesh { Size = new(.1494f, .0994f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = print,
                Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            }
        });

        var hands = PhotoBookHands.Create();
        lap.AddChild(hands);
        SetPhotoNivaBookPose(hands, "hold_open");
        foreach (var mesh in lap.FindChildren("*", nameof(GeometryInstance3D), true, false)
                     .OfType<GeometryInstance3D>())
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        if (_prologueOverlay is { } overlay)
        {
            _photoNivaEvidenceCue = new Label
            {
                Name = "NivaEvidenceAction", Visible = false,
                Theme = UrmanUiTheme.For(_photoNivaPlayer?.Accessibility ?? AccessibilitySettingsSnapshot.Default),
                AnchorLeft = .5f, AnchorRight = .5f, AnchorTop = 1f, AnchorBottom = 1f,
                OffsetLeft = -420, OffsetRight = 420, OffsetTop = -202, OffsetBottom = -160,
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _photoNivaEvidenceCue.AddThemeFontSizeOverride("font_size", (int)(24 * (_photoNivaPlayer?.Accessibility.TextScale ?? 1f)));
            _photoNivaEvidenceCue.AddThemeColorOverride("font_outline_color", Colors.Black);
            _photoNivaEvidenceCue.AddThemeConstantOverride("outline_size", 6);
            overlay.AddChild(_photoNivaEvidenceCue);
        }
    }

    private static void SetPhotoNivaBookPose(Node3D model, string clip)
    {
        foreach (var animation in model.FindChildren("*", nameof(AnimationPlayer), true, false)
                     .OfType<AnimationPlayer>())
        {
            animation.Autoplay = string.Empty;
            animation.Stop();
            if (!animation.HasAnimation(clip)) continue;
            animation.Play(clip);
            animation.Seek(animation.GetAnimation(clip).Length, update: true);
            animation.Pause();
        }
    }

    private void RefreshPhotoNivaLapBook(float delta)
    {
        if (_photoNivaLapBook is not { } book || !IsInstanceValid(book)) return;
        book.Visible = !MainMenuVisible && _pauseMenu?.IsOpen != true && !_menuBusy
            && _photoNivaPlayer?.ModalOpen == false
            && GetTree().GetFirstNodeInGroup("settings_ui") is not SettingsUi { IsOpen: true };
        if (!book.Visible || PhotoNivaDrivePaused) return;
        var showing = _photoNivaEvidenceClock >= 0f;
        var reading = !showing && _photoNivaPhotoInspected && !_photoNivaPhotoShown && _rideLook.Y < -20f;
        var step = delta * (_photoNivaPlayer?.ReducedMotion == true ? 8f : 2.8f);
        _photoNivaBookInspection = Mathf.MoveToward(_photoNivaBookInspection, reading ? 1f : 0f, step);
        _photoNivaBookShowing = Mathf.MoveToward(_photoNivaBookShowing, showing ? 1f : 0f, step);
        book.Position = PhotoNivaBookRest.Lerp(new(.47f, 1.28f, .08f), _photoNivaBookInspection)
            .Lerp(new(.18f, 1.24f, -.12f), _photoNivaBookShowing);
        book.RotationDegrees = new(0, -32f * _photoNivaBookShowing, 0);
        if (_driverGazeTarget is { } gaze && IsInstanceValid(gaze))
            gaze.Position = _photoNivaDriverGazeRest.Lerp(book.Position + Vector3.Up * .08f, _photoNivaBookShowing);
    }

    private string? PhotoNivaEvidenceAction => !_photoNivaWoke
        || _photoNivaGateClock >= 0f || PhotoNivaDialogueOpen || _photoNivaEvidenceClock >= 0f
        || _photoNivaDriveElapsed < _photoNivaNextBarkReady
        || _photoNivaLapBook is not { } book || !IsInstanceValid(book) || !book.Visible
            ? null : _rideLook.Y < -20f && Math.Abs(_rideLook.X) < 40f && !_photoNivaPhotoShown
                ? _photoNivaPhotoInspected ? "Показать снимок бабаю" : "Поднять снимок и рассмотреть"
                : CanOfferPhotoNivaLanguage ? "Выбрать язык разговора с бабаем" : null;

    private bool CanOfferPhotoNivaLanguage => _rideLook.X > 25f && _rideLook.Y > -18f
        && !_photoNivaVillageStarted && !_photoNivaLanguageActive && _bridge is { HasNivaLanguageResponse: false };

    private bool HandlePhotoNivaEvidenceInput(InputEvent inputEvent)
    {
        if (!_photoNivaDriving || _photoNivaSession is not { } session || !IsCurrentPhotoNivaArrival(session)
            || PhotoNivaDrivePaused || inputEvent is InputEventKey { Echo: true }
            || !inputEvent.IsActionPressed("interact") || PhotoNivaEvidenceAction is null) return false;
        _photoNivaGazeTaken = true;
        if (CanOfferPhotoNivaLanguage)
        {
            _photoNivaLanguageActive = true;
            _photoNivaLanguageOpenedAt = _photoNivaDriveElapsed;
            _bridge!.OpenDialogueUi(PrologueRideDialogue);
        }
        else if (!_photoNivaPhotoInspected)
        {
            _photoNivaPhotoInspected = true;
            ShowPrologueCaption("Айдар (про себя): «Эти стволы… Я только что шёл между ними».", 3.2);
            _photoNivaNextBarkReady = _photoNivaDriveElapsed + 3.4f;
        }
        else
        {
            _photoNivaPhotoShown = true;
            _photoNivaEvidenceClock = 0f;
            StopRideRadio(.4);
            PrologueVoice.Stop();
            ShowPrologueCaption(_photoNivaSignalAnswered
                ? "Айдар: «Это ты мне мигал с дороги? Я тебе ответил».": "Айдар: «Этот лес был во сне. Посмотри на снимок».", 3.2);
            _photoNivaNextBarkReady = _photoNivaDriveElapsed + 11.3f;
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void UpdatePhotoNivaEvidence(float delta)
    {
        if (_photoNivaEvidenceClock >= 0f)
        {
            var before = _photoNivaEvidenceClock;
            _photoNivaEvidenceClock += delta;
            if (before < 3.5f && _photoNivaEvidenceClock >= 3.5f)
                ShowPrologueCaption(_photoNivaSignalAnswered
                    ? "Мансур бабай: «Не я. Я тебе не мигал»." : "Мансур бабай: «Покажи…».", 2.4);
            if (before < 6.3f && _photoNivaEvidenceClock >= 6.3f)
                ShowPrologueCaption("Мансур бабай: «Гөлсинә покажешь. Она людей на снимках помнит. Убери пока».", 4.3);
            if (_photoNivaEvidenceClock >= 11f) _photoNivaEvidenceClock = -1f;
        }
        if (_photoNivaEvidenceCue is not { } cue || !IsInstanceValid(cue)) return;
        var action = PhotoNivaEvidenceAction;
        cue.Visible = action is not null && !PhotoNivaDrivePaused;
        if (action is not null)
            cue.Text = InputBindingService.ActionHint("interact", _photoNivaPlayer?.CurrentInputDevice == "gamepad") + " — " + action;
    }

    private void ReleasePhotoNivaLapBook()
    {
        if (_driverGazeTarget is { } gaze && IsInstanceValid(gaze)) gaze.Position = _photoNivaDriverGazeRest;
        if (_photoNivaLapBook is { } book && IsInstanceValid(book))
        {
            book.Visible = false;
            book.QueueFree();
        }
        _photoNivaLapBook = null;
    }
}
