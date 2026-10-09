using Godot;

namespace Urman.Godot;

// The same home sources in connected Act I and the compact entrypoint.
// Knowledge, replies and save state remain owned by RuntimeBridge.
internal partial class ArrivalPersonalProps : Node3D
{
    internal static ArrivalPersonalProps Build(StyleBenchmarkZone house)
    {
        var table = house.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>().Single(mesh => mesh.Name == "HouseInterior_TableTop_LOD0");
        var support = PublicBuildingShell.Bounds(house, table);
        // The right-hand end is clear of the CRT, papers and tea invitation.
        var photoAt = new Vector3(support.End.X - .57f, support.End.Y + .006f, support.End.Z - .24f);
        var phoneAt = photoAt + new Vector3(.31f, .006f, 0);
        var things = new ArrivalPersonalProps { Name = "ArrivalPersonalThings" };
        house.AddChild(things);
        var arrivalPhoto = new Node3D
        {
            Name = "ArrivalPhotoAtHome",
            Position = photoAt,
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
        house.MakeInteractionBox("ArrivalPhotoTarget", new(.38f, .24f, .32f),
            photoAt + Vector3.Up * .06f, "665b49", "urman.chapter1:interaction/view-arrival-photo",
            "Посмотреть фотографию Марата",
            documentId: "urman.chapter1:document/arrival-photo-evidence",
            rayOnly: true);
        var phone = new Node3D { Name = "ArrivalPhone", Position = phoneAt };
        things.AddChild(phone);
        Box(phone, "Case", new(.080f, .016f, .158f), Vector3.Zero, "252b2b", "metal");
        Box(phone, "Screen", new(.070f, .002f, .139f), new(0f, .009f, 0f), "bac8c4");
        Box(phone, "Speaker", new(.018f, .002f, .003f), new(0f, .0105f, -.062f), "222928");
        Box(phone, "Message", new(.052f, .002f, .034f), new(-.004f, .011f, -.019f), "738f81");
        Box(phone, "MessageLine", new(.037f, .002f, .004f), new(-.003f, .0122f, -.019f), "d7e2d7");
        foreach (var reply in new[] { false, true })
            house.MakeInteractionBox(reply ? "ArrivalMotherReplyTarget" : "ArrivalPhoneTarget", new(.34f, .24f, .32f),
                phoneAt + Vector3.Up * .06f, "252b2b",
                reply ? "urman.chapter1:interaction/arrival-answer-mother" : "urman.chapter1:interaction/view-arrival-message",
                reply ? "Ответить маме" : "Сообщение от мамы",
                dialogueId: reply ? "urman.chapter1:dialogue/arrival_mother_reply" : "",
                documentId: reply ? "" : "urman.chapter1:document/arrival-mother-message", rayOnly: true);
        return things;
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
