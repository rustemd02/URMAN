using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    internal const string MosqueQuranReadInteraction = "urman.chapter1:local/mosque/quran-read";

    // The mosque library owns blank shelf bindings and the imam's notebook;
    // no Quran copy existed anywhere in the interior (checked 2026-10-04),
    // while the author requires one to always be present. This partial adds
    // exactly one closed copy on a small wooden reading stand beside the
    // mihrab, from the same timber/paper/cloth families as the rest of the
    // mosque kit. No invented sacred text, no inscription, no save state.
    private Node3D? _mosqueQuranStand;
    private InteractionTarget? _mosqueQuranReadTarget;

    internal bool MosqueQuranPresent => _mosqueQuranStand is not null && IsInstanceValid(_mosqueQuranStand);
    internal InteractionTarget? MosqueQuranReadTarget => _mosqueQuranReadTarget;
    internal bool MosqueQuranReadTargetUsable => _mosqueQuranReadTarget is not null && IsInstanceValid(_mosqueQuranReadTarget);

    /// <summary>
    /// Called by BuildMosqueInterior after BuildMosquePrayerAndLibrary so the
    /// mihrab, minbar and carpet already exist. Idempotent: a second call does
    /// nothing. The stand sits on the west side of the mihrab, opposite the
    /// imam's minbar, clear of the prayer lane and of Timur's usual place.
    /// </summary>
    internal void BuildMosqueSanctuaryQuran()
    {
        if (_mosqueQuranStand is not null)
        {
            return;
        }

        var room = _mosqueRoom;
        var prayer = room?.GetNodeOrNull<Node3D>("MosqueQiblaWall");
        if (room is null || prayer is null)
        {
            GD.PushWarning("mosque-quran: BuildMosqueSanctuaryQuran ran outside the mosque build; no stand placed.");
            return;
        }

        const string timber = "7d6548";
        const string timberDark = "63523f";
        const string brass = "c9a860";
        var stand = new Node3D { Name = "MosqueQuranStand", Position = new(-1.15f, 0, .52f) };
        prayer.AddChild(stand);
        _mosqueQuranStand = stand;
        stand.SetMeta("runtimeStateOwner", "presentation-only; no world.props key");
        stand.SetMeta("presentationRole",
            "closed Quran copy on a small wooden reading stand beside the mihrab; no invented sacred text");
        stand.SetMeta("placement", "west of the mihrab, opposite the imam's minbar; clear of the prayer lane");
        stand.SetMeta("culturalReview", "open: cover ornament and placement require local review");

        // A compact X-fold reading stand (rahlé): two crossed side slats, a
        // lower crossbar, four floor pads and a tilted top board. The board
        // leans its high edge to the wall so a reader standing in the hall
        // (+Z local) sees the cover; the book's long axis runs down the slope.
        const float tiltRadians = 24f * Mathf.Pi / 180f;
        var highSlatRadians = -28.6f * Mathf.Pi / 180f;
        var lowSlatRadians = 39.7f * Mathf.Pi / 180f;
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * .19f;
            FacilitySolid(stand, $"MosqueQuranSlatHigh{side}", new(.035f, .60f, .035f), new(x, .278f, .013f),
                timber, "wood_furniture", new Vector3(highSlatRadians, 0, 0));
            FacilitySolid(stand, $"MosqueQuranSlatLow{side}", new(.035f, .51f, .035f), new(x, .209f, .001f),
                timber, "wood_furniture", new Vector3(lowSlatRadians, 0, 0));
            foreach (var z in new[] { -.16f, .16f })
                FacilitySolid(stand, $"MosqueQuranFoot{side}_{z}", new(.06f, .028f, .08f), new(x, .014f, z),
                    timberDark, "wood_furniture");
        }

        FacilitySolid(stand, "MosqueQuranCrossbar", new(.40f, .045f, .10f), new(0, .22f, .028f), timberDark, "wood_furniture");
        FacilitySolid(stand, "MosqueQuranTop", new(.46f, .022f, .34f), new(0, .50f, .02f), timber, "wood_furniture",
            new Vector3(tiltRadians, 0, 0));

        // The closed copy: the existing mosque book mesh (cloth boards, spine,
        // recessed paper block) plus two plain brass bands and a diamond. The
        // cover faces up the slope; nothing on it carries text or script.
        var bookPivot = new Node3D { Name = "MosqueQuranPivot", Position = new(0, .510f, .02f), Rotation = new(tiltRadians, 0, 0) };
        stand.AddChild(bookPivot);
        var book = new MeshInstance3D
        {
            Name = "MosqueQuranCopy",
            Mesh = BuildMosqueBookMesh(new Vector3(.055f, .28f, .20f), "33513f"),
            Position = new(0, .0395f, 0),
            RotationDegrees = new(0, 90, 90)
        };
        bookPivot.AddChild(book);
        book.SetMeta("geometryOwner", nameof(Act1ConnectedWorld));
        book.SetMeta("presentationRole", "closed Quran copy; plain cover bands only, no invented text");
        foreach (var y in new[] { -.10f, .10f })
            AddVisualBox(book, $"MosqueQuranBand{y}", new(.0025f, .018f, .16f), new(.0285f, y, 0f), brass, "metal");
        var diamond = AddVisualBox(book, "MosqueQuranMedallion", new(.0025f, .05f, .05f), new(.0285f, 0f, 0f), brass, "metal");
        diamond.RotationDegrees = new(45f, 0f, 0f);

        // A read-only local repeat, the same presentation-only contract as the
        // existing SquareLook targets: no RuntimeBridge dispatch, no journal,
        // no world.props write. Only the already-existing paper foley plays.
        var read = FacilityTarget("MosqueQuranRead", MosqueQuranReadInteraction, "Прочитать суру", stand,
            new(0, .62f, .02f), new(.55f, .5f, .55f));
        read.SetMeta("presentationOnly", true);
        read.SetMeta("repeatOnly", true);
        read.SetMeta("foley", "paper_open");
        read.SetMeta("presentationRole", "look-target on the mosque Quran; repeats freely, reads no invented text");
        read.PresentationRepeatAvailable = () => FacilityExteriorActive;
        read.PresentationRepeat = () =>
        {
            if (GetTree().GetFirstNodeInGroup("player_controller") is not FirstPersonController player)
            {
                return;
            }

            UiFoley.PlayWorld(read, read.GlobalPosition, "paper_open");
            player.NotifyTraversal("Айдар читает про себя. Спешить здесь некуда.");
        };
        _mosqueQuranReadTarget = read;
        room.SetMeta("mosqueQuranStand", stand.GetPath().ToString());
        room.SetMeta("mosqueQuranReadInteraction", MosqueQuranReadInteraction);
    }
}
