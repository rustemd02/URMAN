using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    // Tatar terminology references for the generated, fictional stationery:
    // https://suzlek.antat.ru/text2.php?id=330216&kind=0&sourname=9
    // https://ifmk.kpfu.ru/content/uploads/2024/07/davajte-govorit-po-tatarski.pdf
    // https://huzurshop.ru/catalog/semya/nikakh_tanyklygy_dum_rt/
    // No real organisation's form, emblem, seal or completed record is copied.
    private void MosqueOfficeDocument(Node3D parent, string name, Vector3 at, float width,
        string file, int cell = -1, float yaw = 0, bool pinned = false)
    {
        var material = MosquePrint(file, cell, "office_documents_imagegen_v1", 2);
        var length = width / material.GetMeta("printAspect").AsSingle();
        var sheet = AddVisualBox(parent, name, new(width, .0012f, length), at, "d9cfb9", "paper");
        // A physical paper edge casts the small contact shadow; only its upper
        // surface carries writing. Never wrap the document around a box.
        sheet.Basis = pinned
            ? new Basis(Vector3.Forward, Vector3.Right, Vector3.Down)
            : new Basis(Vector3.Up, Mathf.DegToRad(yaw));
        sheet.AddChild(new MeshInstance3D {
            Name = "ImageGenDocument", Position = new(0, .0007f, 0), RotationDegrees = new(-90, 0, 0),
            Mesh = new QuadMesh { Size = new(width, length) }, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        sheet.SetMeta("presentationRole", "Tatar working papers; fictional unfilled stationery; image_gen lettering only");
    }
}
