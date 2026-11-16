using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

public partial class W04Controller
{
    private sealed record CaptionSource(string PhotoId, string FactId, string Title, string Observation, string ImagePath);
    private static readonly CaptionSource[] CommonCaptionSources =
    [
        new("P-FOREST", "forest_encounter_resolved", "Лесной снимок",
            "За корнями я освободил ремень сумки.", "res://assets/textures/photobook/p-forest-front-v2.png"),
        new("P-WATER", "water_encounter_resolved", "Снимок у воды",
            "У воды я вернул дощечку на место.", "res://assets/textures/photobook/p-water-front-v1.png"),
        new("P-HOUSE", "house_two_views_seen", "Снимок двора",
            "Во дворе я увидел те же вещи с другой стороны.", "res://assets/textures/photobook/p-house-front-v1.png")
    ];
    private readonly List<(CaptionSource Source, MeshInstance3D Print)> _commonSpreadCards = [];
    private readonly List<Button> _commonSourceButtons = [];
    private Node3D? _commonSpreadDesk;
    private InteractionTarget? _commonSpreadTarget;
    private VBoxContainer _commonNoteSources = null!;
    private object? _commonSpreadSession;
    private bool _commonSpreadExpanded;
    private bool _commonSpreadMoving;
    private float _commonSpreadElapsed;
    private const float CommonSpreadDuration = .65f;

    private static Vector3 CommonStackPosition(int index) => new(.60f, .8865f + index * .0015f, .30f);
    private static Vector3 CommonSpreadPosition(int index) => new(-.48f + index * .48f, .8865f, -.36f);

    private static CaptionSource[] EarnedCommonCaptionSources(RuntimeBridge bridge)
    {
        var photos = bridge.PhotoBookPhotos();
        return CommonCaptionSources.Where(source => photos.Any(photo => photo.PhotoId == source.PhotoId)
            && bridge.PhotoWorldFactObserved(source.FactId) && ResourceLoader.Exists(source.ImagePath)).ToArray();
    }

    private void BuildCommonCaptionSpread(Node3D desk)
    {
        _commonSpreadDesk = desk;
        _commonSpreadTarget = new InteractionTarget
        {
            Name = "CommonPhotoSpreadTarget", InteractionId = NoteInteractionId,
            Prompt = "Разложить снимки рядом с листом", Position = CommonStackPosition(0) + Vector3.Up * .025f,
            CollisionLayer = 4u, CollisionMask = 0u
        };
        _commonSpreadTarget.ConfigureRayOnly();
        _commonSpreadTarget.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new(.30f, .07f, .22f) }
        });
        _commonSpreadTarget.PresentationBeforeDispatch = BeginCommonCaptionSpread;
        _commonSpreadTarget.SetPresentationEnabled(false);
        desk.AddChild(_commonSpreadTarget);
    }

    private void ProcessCommonCaptionSpread(double delta)
    {
        if (_bridge is not { } bridge || _commonSpreadDesk is not { } desk) return;
        var current = HasCurrentCommonRoute() && !bridge.IsPhotoBookProjectionDeferred;
        if (!current)
        {
            if (_commonSpreadSession is not null) ClearCommonCaptionSpread();
            _commonSpreadTarget?.SetPresentationEnabled(false);
            return;
        }
        if (!ReferenceEquals(_commonSpreadSession, bridge.SessionIdentity))
        {
            ClearCommonCaptionSpread();
            _commonSpreadSession = bridge.SessionIdentity;
            _commonSpreadExpanded = bridge.PhotoWorldFactObserved(NoteFactId);
            foreach (var source in EarnedCommonCaptionSources(bridge))
            {
                if (ResourceLoader.Load<Texture2D>(source.ImagePath) is not { } texture) continue;
                var print = new MeshInstance3D
                {
                    Name = source.PhotoId + "WritingPrint", RotationDegrees = new(-90f, 0f, 0f),
                    Mesh = new QuadMesh { Size = new(.26f, .1733f) },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoTexture = texture, Roughness = 1f,
                        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
                    },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                };
                print.SetMeta("visualOnly", true);
                print.SetMeta("assetSource", source.ImagePath);
                print.Position = _commonSpreadExpanded
                    ? CommonSpreadPosition(_commonSpreadCards.Count) : CommonStackPosition(_commonSpreadCards.Count);
                desk.AddChild(print);
                _commonSpreadCards.Add((source, print));
            }
        }

        if (_commonSpreadMoving)
        {
            if (CommonActionModalOpen())
            {
                _commonSpreadMoving = false;
                for (var index = 0; index < _commonSpreadCards.Count; index++)
                    _commonSpreadCards[index].Print.Position = CommonStackPosition(index);
            }
            else
            {
                _commonSpreadElapsed = Math.Min(CommonSpreadDuration, _commonSpreadElapsed + (float)Math.Max(0, delta));
                var progress = Mathf.SmoothStep(0f, 1f, _commonSpreadElapsed / CommonSpreadDuration);
                for (var index = 0; index < _commonSpreadCards.Count; index++)
                {
                    var at = CommonStackPosition(index).Lerp(CommonSpreadPosition(index), progress);
                    at.Y += .045f * Mathf.Sin(Mathf.Pi * progress);
                    _commonSpreadCards[index].Print.Position = at;
                }
                if (_commonSpreadElapsed >= CommonSpreadDuration)
                {
                    _commonSpreadMoving = false;
                    _commonSpreadExpanded = true;
                    if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
                        player.ShowRemark("Айдар", "Вот что я сделал в этих снимках. Теперь можно подобрать свою строку.");
                }
            }
        }
        // A confirmed line leaves its source prints beside it, including on
        // Continue. Arranging them earlier is an optional way to compose it.
        if (!_commonSpreadExpanded && !_commonSpreadMoving && bridge.PhotoWorldFactObserved(NoteFactId))
        {
            _commonSpreadExpanded = true;
            for (var index = 0; index < _commonSpreadCards.Count; index++)
                _commonSpreadCards[index].Print.Position = CommonSpreadPosition(index);
        }
        _commonSpreadTarget?.SetPresentationEnabled(current && !_commonSpreadMoving && _commonSpreadCards.Count > 0
            && !CommonActionModalOpen() && bridge.IsInteractionAvailable(NoteInteractionId));
        if (_commonSpreadTarget is { } target)
        {
            target.Position = _commonSpreadExpanded ? CommonSpreadPosition(1) + Vector3.Up * .025f
                : CommonStackPosition(0) + Vector3.Up * .025f;
            target.Prompt = _commonSpreadExpanded ? "Сверить снимки с моей строкой" : "Разложить снимки рядом с листом";
        }
    }

    private bool BeginCommonCaptionSpread()
    {
        if (_bridge is not { } bridge || !ReferenceEquals(_commonSpreadSession, bridge.SessionIdentity)) return false;
        if (_commonSpreadExpanded) return BeginCommonNote();
        if (_commonSpreadMoving || _commonSpreadCards.Count == 0
            || !CanBeginCommonAction(NoteInteractionId, out _, out _)) return false;
        _commonSpreadElapsed = 0f;
        _commonSpreadMoving = true;
        if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController { ReducedMotion: true })
            _commonSpreadElapsed = CommonSpreadDuration;
        return false;
    }

    private void RefreshCommonCaptionSources(RuntimeBridge bridge)
    {
        foreach (var child in _commonNoteSources.GetChildren()) { _commonNoteSources.RemoveChild(child); child.QueueFree(); }
        _commonSourceButtons.Clear();
        var current = _commonSpreadExpanded && ReferenceEquals(_commonSpreadSession, bridge.SessionIdentity);
        _commonNoteSources.Visible = current;
        if (!current) return;
        foreach (var source in EarnedCommonCaptionSources(bridge))
        {
            var row = new VBoxContainer();
            row.AddChild(new Label { Text = source.Title + ": " + source.Observation,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = Control.FocusModeEnum.All });
            var use = new Button { Text = "Взять за основу: " + source.Title.ToLowerInvariant() };
            use.Pressed += () =>
            {
                if (!CommonNoteContextCurrent() || _commonNoteSaving || _commonNoteReadOnly) return;
                var text = _commonNoteText.Text.Trim();
                var combined = string.IsNullOrEmpty(text) ? source.Observation : text + " " + source.Observation;
                if (!RuntimeBridge.TryNormalizeCommonNote(combined, out var normalized))
                {
                    _commonNoteStatus.Text = "В строке осталось мало места. Сократите её или напишите по-своему.";
                    return;
                }
                _commonNoteText.Text = normalized;
                _commonNoteText.GrabFocus();
                _commonNoteText.CaretColumn = normalized.Length;
                _commonNoteStatus.Text = "Это начало строки. Можно изменить его или написать своё.";
            };
            _commonSourceButtons.Add(use);
            row.AddChild(use);
            _commonNoteSources.AddChild(row);
        }
    }

    private void ClearCommonCaptionSpread()
    {
        foreach (var (_, print) in _commonSpreadCards)
            if (GodotObject.IsInstanceValid(print)) { print.Visible = false; print.QueueFree(); }
        _commonSpreadCards.Clear();
        _commonSpreadSession = null;
        _commonSpreadMoving = false;
        _commonSpreadExpanded = false;
    }
}
