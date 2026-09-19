using Godot;

namespace Urman.Godot;

// One owner for Aidar's arrival props in both the connected Act I and compact
// campaign entrypoint. Narrative knowledge, answers and saves stay in RuntimeBridge.
internal partial class ArrivalPersonalProps : Node3D
{
    private RuntimeBridge? _bridge;
    private bool _restsOnSnow;

    internal static ArrivalPersonalProps Build(StyleBenchmarkZone village, float benchGround)
    {
        // Aidar has put his phone and photograph on the bench while arriving.
        // Both lie on its snow cap; reading and replying use the same authored
        // runtime as every other document/dialogue. Only the reply puts them away.
        var things = new ArrivalPersonalProps { Name = "ArrivalPersonalThings", _restsOnSnow = village.GetParent() is Act1ConnectedWorld };
        village.AddChild(things);
        var arrivalPhoto = new Node3D
        {
            Name = "ArrivalPhotoInRoad",
            Position = new(5.35f, benchGround + .607f, 6.03f),
            RotationDegrees = new(-90f, 0f, -8f)
        };
        things.AddChild(arrivalPhoto);
        Box(arrivalPhoto, "PhotoPaper", new(.194f, .230f, .004f), Vector3.Zero, "d8cdb2", "paper");
        var photograph = ResourceLoader.Load<Texture2D>("res://assets/images/arrival_marat_childhood_existing_v1.png")
            ?? throw new InvalidOperationException("The authored arrival photograph has not been packaged.");
        // Keep the untouched archival scan in the reader. The world print maps
        // its paper rectangle, excluding the scanner's black surround.
        arrivalPhoto.AddChild(new MeshInstance3D
        {
            Name = "MaratChildhoodPhotograph", Position = new(0, 0, .0025f),
            Mesh = new QuadMesh { Size = new(.180f, .215f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = photograph, Roughness = 1f,
                Uv1Offset = new(142f / 1024f, 42f / 1024f, 0f),
                Uv1Scale = new(781f / 1024f, 933f / 1024f, 1f),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
            }
        });
        village.MakeInteractionBox("ArrivalPhotoTarget", new(.38f, .24f, .32f),
            new(5.35f, benchGround + .73f, 6.03f), "665b49", "urman.chapter1:interaction/view-arrival-photo",
            "Посмотреть фотографию Марата",
            documentId: "urman.chapter1:document/arrival-photo-evidence",
            rayOnly: true);
        var phone = new Node3D { Name = "ArrivalPhone", Position = new(4.42f, benchGround + .613f, 6.02f) };
        things.AddChild(phone);
        Box(phone, "Case", new(.080f, .016f, .158f), Vector3.Zero, "252b2b", "metal");
        Box(phone, "Screen", new(.070f, .002f, .139f), new(0f, .009f, 0f), "bac8c4");
        Box(phone, "Speaker", new(.018f, .002f, .003f), new(0f, .0105f, -.062f), "222928");
        Box(phone, "Message", new(.052f, .002f, .034f), new(-.004f, .011f, -.019f), "738f81");
        Box(phone, "MessageLine", new(.037f, .002f, .004f), new(-.003f, .0122f, -.019f), "d7e2d7");
        foreach (var reply in new[] { false, true })
            village.MakeInteractionBox(reply ? "ArrivalMotherReplyTarget" : "ArrivalPhoneTarget", new(.34f, .24f, .32f),
                new(4.42f, benchGround + .73f, 6.02f), "252b2b",
                reply ? "urman.chapter1:interaction/arrival-answer-mother" : "urman.chapter1:interaction/view-arrival-message",
                reply ? "Ответить маме" : "Сообщение от мамы",
                dialogueId: reply ? "urman.chapter1:dialogue/arrival_mother_reply" : "",
                documentId: reply ? "" : "urman.chapter1:document/arrival-mother-message", rayOnly: true);
        return things;
    }

    public override void _Ready()
    {
        _bridge = GetTree().GetFirstNodeInGroup("runtime_bridge") as RuntimeBridge;
        if (_bridge is not null) _bridge.RuntimeStateChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_bridge is not null && GodotObject.IsInstanceValid(_bridge))
            _bridge.RuntimeStateChanged -= Refresh;
        _bridge = null;
    }

    private void Refresh()
    {
        if (_bridge?.ActiveSceneId is null) return;
        var knowledge = _bridge.SelectRuntimeState().GetProperty("knowledge");
        bool Confirmed(string slug) => knowledge.TryGetProperty("urman.chapter1:knowledge/" + slug, out var entry)
            && entry.GetProperty("status").GetString() == "confirmed";
        Visible = !Confirmed("arrival_reply_help_babai") && !Confirmed("arrival_reply_kept_silent");
        // The optional brush-snow action exposes the seat .075 m lower. Props
        // follow that real support if it happens before the personal reply.
        Position = new(0, _restsOnSnow && Confirmed("discovery-arrival-bench-race-notches") ? -.075f : 0, 0);
    }

    private static void Box(Node3D parent, string name, Vector3 size, Vector3 at, string color, string surface = "")
    {
        var mesh = new MeshInstance3D
        {
            Name = name, Position = at, Mesh = new BoxMesh { Size = size },
            MaterialOverride = PainterlyMaterialLibrary.ForColor(color, surface)
        };
        mesh.SetMeta("visualOnly", true);
        parent.AddChild(mesh);
    }
}
