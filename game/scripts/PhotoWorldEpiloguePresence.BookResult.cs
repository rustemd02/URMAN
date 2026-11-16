using System;
using System.Linq;
using System.Text.Json;
using Godot;
using Urman.Core.Narrative;

namespace Urman.Godot;

public partial class PhotoWorldEpiloguePresence
{
    private const string BookResultKnowledgeId = "urman.fullgame:knowledge/pw-marat-death-question-remains-open";
    private static readonly JsonElement BookResultCondition = JsonSerializer.SerializeToElement(new
        { op = "knowledge.status", knowledgeId = BookResultKnowledgeId, status = "confirmed" });
    private Node3D? _resultBook;
    private MeshInstance3D? _resultPrint;
    private InteractionTarget? _resultBookTarget;
    private object? _bookResultSession;
    private bool _bookResultDirty = true;
    private bool _bookResultRecorded;

    internal static bool HasRecordedBookResult(RuntimeBridge bridge) => bridge.SessionIdentity is { } session
        && ContentRuleEngine.Evaluate(BookResultCondition, session.SelectState());

    private void MarkBookResultDirty() => _bookResultDirty = true;

    private bool BookResultContextCurrent(RuntimeBridge bridge) => _bookResultRecorded && IsInsideTree() && !IsQueuedForDeletion()
        && bridge.SessionIdentity is { } session && ReferenceEquals(_bookResultSession, session)
        && !bridge.IsPhotoBookProjectionDeferred && bridge.CurrentZoneId == HomeZoneId
        && bridge.ActiveSceneId is ("urman.fullgame:scene/pw-home" or EpilogueSceneId)
        && bridge.CurrentFullGamePhotoWorldRouteProjection() is { } route
        && ReferenceEquals(route.SessionIdentity, session) && route.ActiveSceneId == bridge.ActiveSceneId
        && HasNowPhoto(bridge);

    private void ProcessBookResult()
    {
        if (_bridge is not { } bridge) return;
        if (!ReferenceEquals(_bookResultSession, bridge.SessionIdentity))
        {
            _bookResultSession = bridge.SessionIdentity;
            _bookResultDirty = true;
        }
        if (_bookResultDirty && !bridge.IsPhotoBookProjectionDeferred)
        {
            _bookResultRecorded = HasRecordedBookResult(bridge);
            _bookResultDirty = false;
        }
        var current = BookResultContextCurrent(bridge);
        if (current && _resultBook is null) BuildBookResult();
        if (_resultPrint is { } print) print.Visible = current;
        _resultBookTarget?.SetPresentationEnabled(current && IsInteractionWindowOpen()
            && _resultBook is { } book && book.IsVisibleInTree());
    }

    private void BuildBookResult()
    {
        if (GetParent()?.GetNodeOrNull<Node3D>("ArrivalPersonalThings/FamilyTukayBookPresentation") is not { } book
            || !book.IsVisibleInTree()
            || !ResourceLoader.Exists(JournalUi.SNowFrontImagePath)
            || ResourceLoader.Load<Texture2D>(JournalUi.SNowFrontImagePath) is not { } photograph) return;

        // Measure the actual closed volume, including the authored GLB. The
        // new print stays inside its supported footprint; no furniture moves.
        var meshes = book.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Where(mesh => mesh.IsVisibleInTree() && mesh.Mesh is not null).ToArray();
        if (meshes.Length == 0) return;
        var top = meshes.Max(mesh => PublicBuildingShell.Bounds(book, mesh).End.Y);
        _resultBook = book;
        _resultPrint = new MeshInstance3D
        {
            Name = "TodayFamilyPrint",
            Position = new(0f, top + .0015f, 0f),
            RotationDegrees = new(-90f, 0f, 0f),
            Mesh = new QuadMesh { Size = new(.18f, .12f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = photograph, Roughness = 1f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _resultPrint.SetMeta("visualOnly", true);
        _resultPrint.SetMeta("assetSource", JournalUi.SNowFrontImagePath);
        book.AddChild(_resultPrint);

        // This thin ray box sits above the older forest-page target. Only the
        // earned final page owns the visible new print after the last talk.
        _resultBookTarget = new InteractionTarget
        {
            Name = "FamilyBookResultTarget", InteractionId = FamilyTalkInteractionId,
            Prompt = "Открыть итоговый разворот", Position = new(0f, top + .035f, 0f),
            CollisionLayer = 4u, CollisionMask = 0u
        };
        _resultBookTarget.ConfigureRayOnly();
        _resultBookTarget.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new(.20f, .06f, .15f) }
        });
        _resultBookTarget.PresentationBeforeDispatch = () => { OpenBookResult(); return false; };
        _resultBookTarget.PresentationRepeatAvailable = () => _bridge is { } current && BookResultContextCurrent(current);
        _resultBookTarget.PresentationRepeat = OpenBookResult;
        book.AddChild(_resultBookTarget);
    }

    private void OpenBookResult()
    {
        if (_bridge is not { } bridge || !BookResultContextCurrent(bridge)
            || !IsInteractionWindowOpen() || _resultBook is not { } book || !book.IsVisibleInTree()) return;
        // The existing album owns modal input, accessibility, Escape and load
        // teardown. Closing it returns to this same physical book and pose.
        if (GetTree().GetFirstNodeInGroup("journal_ui") is JournalUi journal)
            journal.OpenPhotoBook(bridge, "S-NOW");
    }
}
