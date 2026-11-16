using Godot;
using System.Text.Json.Nodes;
using Urman.Core.Persistence;

namespace Urman.Godot;

public partial class Act1DemoRoot
{
    internal const string MansurForestClue = "prologue/mansur-forest-print";
    private Node3D? _photoNivaLapBook;
    private bool _photoNivaPhotoInspected, _photoNivaPhotoShown, _photoNivaSignalAnswered, _photoNivaHutKnocked;
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
            && _photoNivaPlayer?.UiModalOpen == false
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

    private string? PhotoNivaEvidenceAction
    {
        get
        {
            if (!_photoNivaWoke || _photoNivaGateClock >= 0f || PhotoNivaDialogueOpen
                || _photoNivaJourneyCut >= 0f
                || _photoNivaArrivalFailure is not null
                || _photoNivaEvidenceClock >= 0f || _photoNivaLapBook is not { } book
                || !IsInstanceValid(book) || !book.Visible) return null;
            var canSpeak = _photoNivaDriveElapsed >= _photoNivaNextBarkReady;
            // The photograph remains an action after the automatic waking glance ends.
            if (!_photoNivaPhotoInspected) return "Рассмотреть снимок";
            if (!_photoNivaPhotoShown) return canSpeak ? "Спросить бабая о снимке" : null;
            return canSpeak && CanOfferPhotoNivaLanguage ? "Выбрать язык разговора с бабаем" : null;
        }
    }

    private bool CanOfferPhotoNivaLanguage => _rideLook.X > 25f && _rideLook.Y > -18f
        && !_photoNivaVillageStarted && !_photoNivaLanguageActive && _bridge is { HasNivaLanguageResponse: false };

    private bool HandlePhotoNivaEvidenceInput(InputEvent inputEvent)
    {
        if (!_photoNivaDriving || _photoNivaSession is not { } session || !IsCurrentPhotoNivaArrival(session)
            || PhotoNivaDrivePaused || inputEvent is InputEventKey { Echo: true }
            || !inputEvent.IsActionPressed("interact") || PhotoNivaEvidenceAction is null) return false;
        _photoNivaGazeTaken = true;
        _photoNivaArrivalNotBefore = Mathf.Max(_photoNivaArrivalNotBefore, _photoNivaDriveElapsed + 15.2f);
        if (!_photoNivaPhotoInspected)
        {
            _photoNivaPhotoInspected = true;
            _rideLook = PhotoNivaLapGaze;
            ApplyPrologueRideLook();
            _photoNivaNextBarkReady = Math.Max(_photoNivaWakeVoiceEnds + .25, _photoNivaDriveElapsed + .6);
        }
        else if (!_photoNivaPhotoShown)
        {
            _photoNivaPhotoShown = true;
            _photoNivaEvidenceClock = 0f;
            _rideLook = new Vector2(32f, -8f);
            ApplyPrologueRideLook();
            StopRideRadio(.4);
            PrologueVoice.Stop();
            ShowPrologueCaption(_photoNivaHutKnocked
                ? "Айдар: «Ты велел ждать у сторожки. Я постучал. Кто там открыл?»"
                : _photoNivaSignalAnswered ? "Айдар: «Это ты мне мигал с дороги? Я тебе ответил»."
                : "Айдар: «Я этот лес видел, пока спал. Посмотри на снимок».", 3.8);
            _photoNivaNextBarkReady = _photoNivaDriveElapsed + 14.2f;
        }
        else if (CanOfferPhotoNivaLanguage)
        {
            _photoNivaLanguageActive = true;
            _photoNivaLanguageOpenedAt = _photoNivaDriveElapsed;
            _bridge!.OpenDialogueUi(PrologueRideDialogue);
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
            if (before < 4f && _photoNivaEvidenceClock >= 4f)
                ShowPrologueCaption(_photoNivaHutKnocked
                    ? _photoNivaMittenTaken
                        ? "Мансур бабай: «Не у сторожки я тебя взял. Ты один на дорогу вышел»."
                        : "Мансур бабай: «Я тебя у остановки забрал. Ты один был»."
                    : _photoNivaSignalAnswered ? "Мансур бабай: «Не я. Я тебе не мигал»."
                        : "Мансур бабай: «Ты с этой книгой и задремал».", 2.9);
            if (before < 7.2f && _photoNivaEvidenceClock >= 7.2f)
                ShowPrologueCaption("Айдар: «А вот эта берёза, с двумя стволами. Её ты узнаёшь?»", 3.0);
            if (before < 10.6f && _photoNivaEvidenceClock >= 10.6f)
            {
                ShowPrologueCaption("Мансур бабай: «Под ней жердь лежала. С дороги только конец видно. А на снимке?»", 4.0);
                if (_bridge is { } bridge && _photoNivaSession is { } session && IsCurrentPhotoNivaArrival(session))
                    _ = RememberMansurForestClue(bridge);
            }
            if (_photoNivaEvidenceClock >= 14f) _photoNivaEvidenceClock = -1f;
        }
        if (_photoNivaEvidenceCue is not { } cue || !IsInstanceValid(cue)) return;
        var action = PhotoNivaEvidenceAction;
        cue.Visible = _photoNivaWoke && !PhotoNivaDrivePaused && !PhotoNivaDialogueOpen
            && _photoNivaGateClock < 0f && _photoNivaJourneyCut < 0f;
        var gamepad = _photoNivaPlayer?.CurrentInputDevice == "gamepad";
        var main = action is null ? string.Empty : InputBindingService.ActionHint("interact", gamepad) + " — " + action + "\n";
        var stay = _photoNivaKeepRiding ? string.Empty : InputBindingService.ActionHint("move_forward", gamepad) + " — Остаться в поездке · ";
        cue.Text = (PhotoNivaWaitingAtVillage ? "Деревня ещё загружается.\n" : string.Empty)
            + main + stay + InputBindingService.ActionHint("skip_prologue", gamepad) + " — Доехать до дома";
    }

    private static async Task RememberMansurForestClue(RuntimeBridge bridge)
    {
        try
        {
            if (!YardMechanism.Flag(bridge.SelectWorldProps(), MansurForestClue, "heard"))
                await bridge.DispatchWorldPropsAsync(new JsonArray(new JsonObject { ["propId"] = MansurForestClue, ["heard"] = true }));
        }
        catch (Exception e) { GD.PushWarning($"Could not keep Mansur's recollection: {e.Message}"); }
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
