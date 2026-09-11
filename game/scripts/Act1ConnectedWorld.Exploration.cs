using Godot;

namespace Urman.Godot;

public partial class Act1ConnectedWorld
{
    private Node3D? _familyPhoto;
    private Node3D? _sewingTinLid;
    private OmniLight3D? _repairedLampLight;
    private MeshInstance3D? _repairedLampBulb;
    private bool? _photoFound, _tinFound, _lampFound;
    private Tween? _photoTurn, _tinOpen;
    private AudioStreamPlayer? _discoveryFoley;

    private const string DiscoveryPrefix = "urman.chapter1:knowledge/discovery-";

    private void BuildAct1InteriorDiscoveries()
    {
        _discoveryFoley = UiFoley.Attach(this);
        var house = (StyleBenchmarkZone)_zoneInstances["house_old_pc"];
        foreach (var name in new[] { "FamilyPhoto", "FamilyPhotoInner" })
            if (house.GetNodeOrNull<Node3D>(name) is { } old) old.Visible = false;
        _familyPhoto = new Node3D { Name = "DiscoveryFamilyPhoto", Position = new(-2.1f, 1.45f, -4.60f) };
        house.AddChild(_familyPhoto);
        AddVisualBox(_familyPhoto, "WalnutFrame", new(.58f, .72f, .045f), Vector3.Zero, "72533d", "wood");
        AddVisualBox(_familyPhoto, "IvoryMount", new(.51f, .65f, .012f), new(0, 0, .029f), "e2d5b8");
        _familyPhoto.AddChild(new MeshInstance3D
        {
            Name = "FirstSnowPhotograph", Position = new(0, 0, .038f),
            Mesh = new QuadMesh { Size = new(.45f, .56f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = ResourceLoader.Load<Texture2D>("res://assets/textures/act1/photo_first_snow_v1.png"),
                Roughness = .85f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
            }
        });
        AddVisualBox(_familyPhoto, "CardBacking", new(.50f, .64f, .008f), new(0, 0, -.027f), "c8b493");
        var handwriting = DiscoveryLabel(_familyPhoto, "FirstSnowInscription", "Первый снег", new(0, .05f, -.034f), .00105f);
        handwriting.RotationDegrees = new(0, 180, -6);
        DiscoveryTarget(house, "house-interior-photo-back", new(.63f, .77f, .12f), _familyPhoto.Position, true);

        var tin = new Node3D { Name = "DiscoverySewingTin", Position = new(-3.97f, .858f, 3.70f), RotationDegrees = new(4f, 180f, 0) };
        house.AddChild(tin);
        DiscoveryCylinder(tin, "TinBottom", .25f, .25f, .025f, new(0, .015f, 0), "517c77");
        // A rim of individual shallow metal panels leaves the contents visible
        // when the hinged lid rises, without a solid cylinder filling the tin.
        for (var i = 0; i < 20; i++)
        {
            var angle = i * Mathf.Tau / 20;
            AddVisualBox(tin, $"TinWall{i}", new(.081f, .14f, .018f),
                new(Mathf.Sin(angle) * .24f, .075f, Mathf.Cos(angle) * .24f), "517c77", yawDegrees: Mathf.RadToDeg(angle));
        }
        for (var i = 0; i < 3; i++)
        {
            var p = new Vector3(-.12f + i * .105f, .045f, .06f);
            DiscoveryCylinder(tin, $"Spool{i}", .041f, .041f, .052f, p, i == 0 ? "a65143" : i == 1 ? "d6c89a" : "42677c");
            DiscoveryCylinder(tin, $"SpoolCap{i}", .049f, .049f, .009f, p + new Vector3(0, .03f, 0), "c7a675");
        }
        AddVisualBox(tin, "WordCard", new(.25f, .008f, .12f), new(0, .092f, -.06f), "e0d4b6", yawDegrees: -8);
        var word = DiscoveryLabel(tin, "HomeWord", "өй — дом", new(0, .099f, -.06f), .00058f);
        word.RotationDegrees = new(-90, -8, 0);
        _sewingTinLid = new Node3D { Name = "HingedLid", Position = new(0, .15f, -.24f) };
        tin.AddChild(_sewingTinLid);
        DiscoveryCylinder(_sewingTinLid, "EnamelLid", .26f, .26f, .018f, new(0, 0, .24f), "59867c");
        DiscoveryCylinder(_sewingTinLid, "IvoryMedallion", .13f, .13f, .004f, new(0, .012f, .24f), "c9ba8b");
        DiscoveryTarget(house, "house-interior-language-tin", new(.55f, .25f, .55f), tin.Position + new Vector3(0, .12f, 0), true);

        var fap = (StyleBenchmarkZone)_zoneInstances["fap_clinic"];
        var marks = new Node3D { Name = "DiscoveryHeightMarks", Position = new(.86f, 0, 5.51f) };
        fap.AddChild(marks);
        for (var i = 0; i < 7; i++)
        {
            AddVisualBox(marks, $"PencilNotch{i}", new(.082f + i % 3 * .013f, .007f, .006f),
                new(0, .85f + i * .098f, 0), "b9ac89", rollDegrees: i % 2 == 0 ? 2 : -3);
        }
        // The inside of the entrance faces -Z. These are family pencil marks,
        // not an invented medical growth chart or new plot evidence.
        DiscoveryTarget(fap, "fap-interior-height-marks", new(.24f, .82f, .08f), new(.86f, 1.16f, 5.47f), true);

        var lamp = new Node3D { Name = "DiscoveryRepairedLamp", Position = new(1.17f, 1.023f, -4.15f) };
        fap.AddChild(lamp);
        DiscoveryCylinder(lamp, "WeightedFoot", .16f, .18f, .045f, Vector3.Zero, "526f65");
        DiscoveryCylinder(lamp, "LowerArm", .014f, .014f, .26f, new(-.07f, .14f, 0), "697b76").RotationDegrees = new(0, 0, -24);
        DiscoveryCylinder(lamp, "UpperArm", .012f, .012f, .23f, new(-.07f, .354f, 0), "697b76").RotationDegrees = new(0, 0, 30);
        DiscoveryCylinder(lamp, "Shade", .055f, .13f, .12f, new(-.127f, .475f, 0), "52756b");
        _repairedLampBulb = DiscoveryCylinder(lamp, "Bulb", .038f, .038f, .025f, new(-.127f, .407f, 0), "d6c79f");
        AddVisualBox(lamp, "Switch", new(.043f, .025f, .065f), new(.10f, .035f, .04f), "2e443f");
        for (var i = 0; i < 5; i++)
            DiscoveryCylinder(lamp, $"PaperclipCoil{i}", .026f, .026f, .005f, new(-.02f, .246f + i * .008f, 0), "b4a883");
        _repairedLampLight = new OmniLight3D
        {
            Name = "RepairedDeskLight", Position = new(-.127f, .375f, .06f),
            LightColor = new Color("ffda9b"), LightEnergy = .7f, OmniRange = 1.8f,
            ShadowEnabled = false, Visible = false
        };
        lamp.AddChild(_repairedLampLight);
        DiscoveryTarget(fap, "fap-interior-repaired-desk-object", new(.40f, .54f, .35f), lamp.Position + new Vector3(0, .22f, 0), false);
    }

    private void DiscoveryTarget(StyleBenchmarkZone zone, string slug, Vector3 size, Vector3 position, bool journal)
    {
        var target = zone.MakeInteractionBox("Discovery_" + slug, size, position, "665b49",
            "urman.chapter1:interaction/discover-" + slug, "Осмотреть");
        // Localized prose stays in compiled authored content. Resolve after the
        // bridge exists; the target itself remains the usual raycast owner.
        target.SetMeta("discoverySlug", slug);
        if (journal) target.JournalEntryId = DiscoveryPrefix + slug;
    }

    private static MeshInstance3D DiscoveryCylinder(Node3D parent, string name, float top, float bottom,
        float height, Vector3 at, string color)
    {
        var mesh = new MeshInstance3D
        {
            Name = name, Position = at,
            Mesh = new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 20, Rings = 1 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(color), Roughness = .83f }
        };
        parent.AddChild(mesh);
        return mesh;
    }

    private static Label3D DiscoveryLabel(Node3D parent, string name, string text, Vector3 at, float pixelSize)
    {
        var label = new Label3D
        {
            Name = name, Text = text, Position = at, FontSize = 64, PixelSize = pixelSize,
            Modulate = new Color("564a3e"), OutlineSize = 0, Shaded = true, DoubleSided = false
        };
        parent.AddChild(label);
        return label;
    }

    private void UpdateAct1Discoveries()
    {
        if (_runtimeBridge?.ActiveSceneId is null) return;
        var state = _runtimeBridge.SelectRuntimeState();
        var knowledge = state.GetProperty("knowledge");
        bool Found(string slug) => knowledge.TryGetProperty(DiscoveryPrefix + slug, out var entry)
            && entry.GetProperty("status").GetString() is "confirmed" or "hypothesis";
        foreach (var bindings in _interactionsByZone.Values)
            foreach (var binding in bindings)
                if (binding.Node.HasMeta("discoverySlug"))
                    binding.Node.Prompt = _runtimeBridge.ResolveText("urman.chapter1:text/discover-" + binding.Node.GetMeta("discoverySlug").AsString());
        var photo = Found("house-interior-photo-back");
        if (_familyPhoto is not null && _photoFound != photo)
        {
            _photoTurn?.Kill();
            if (_photoFound == false && photo && ActiveZoneId == "house_old_pc")
                { _photoTurn = CreateTween(); _photoTurn.TweenProperty(_familyPhoto, "rotation:y", Mathf.Pi, .65); }
            else _familyPhoto.Rotation = new(0, photo ? Mathf.Pi : 0, 0);
            _photoFound = photo;
        }
        var tin = Found("house-interior-language-tin");
        if (_sewingTinLid is not null && _tinFound != tin)
        {
            _tinOpen?.Kill();
            if (_tinFound == false && tin && ActiveZoneId == "house_old_pc")
                { _tinOpen = CreateTween(); _tinOpen.TweenProperty(_sewingTinLid, "rotation:x", -1.85f, .55); }
            else _sewingTinLid.Rotation = new(tin ? -1.85f : 0, 0, 0);
            _tinFound = tin;
        }
        var lampOn = Found("fap-interior-repaired-desk-object");
        if (_repairedLampLight is not null) _repairedLampLight.Visible = lampOn && ActiveZoneId == "fap_clinic";
        if (_repairedLampBulb is not null && _lampFound != lampOn)
        {
            if (_lampFound == false && lampOn && ActiveZoneId == "fap_clinic") UiFoley.Play(_discoveryFoley, "ui_click");
            _repairedLampBulb.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(lampOn ? "ffe3a3" : "d6c79f"), Roughness = .5f,
                EmissionEnabled = lampOn, Emission = new Color("ffd994"), EmissionEnergyMultiplier = .8f
            };
            _lampFound = lampOn;
        }
    }
}
