using System.Linq;
using Godot;

namespace Urman.Godot;

// Author request 2026-10-04: the square PA's world content, owned by this
// partial only. It builds the switch box beside the club entrance, the two
// horn speakers above the porch, the foyer radio point inside the hall, and
// their presentation interaction targets. It is invoked from
// VillagePaSystem.Initialize through the public world API and never touches
// the club/school interior partials or the households lane.
public partial class Act1ConnectedWorld
{
    // Club-local positions (HouseOfCulture: width 16, depth 12, front at +Z,
    // wall thickness .35, so the exterior wall face sits at z = 6.0).
    private static readonly Vector3 VillagePaHornAMouth = new(-3.4f, 3.35f, 6.42f);
    private static readonly Vector3 VillagePaHornBMouth = new(3.4f, 3.35f, 6.42f);
    private static readonly Vector3 VillagePaClubSpeakerAt = new(0f, 1.4f, -1.9f);
    private static readonly Vector3 VillagePaSwitchAt = new(1.62f, 1.45f, 6.05f);
    private static readonly Vector3 VillagePaRadioAt = new(2.9f, 2.35f, 2.65f);

    private bool _villagePaFixturesBuilt;

    internal void BuildVillagePaFixtures(VillagePaSystem pa)
    {
        if (_villagePaFixturesBuilt || !IsBuilt)
        {
            return;
        }

        if (FindChild("HouseOfCulture", true, false) is not Node3D club)
        {
            GD.PushWarning("village-pa: HouseOfCulture is missing; the switch and horns were not built.");
            return;
        }

        _villagePaFixturesBuilt = true;
        var boxMetal = RuralPropMaterials.Surface("metal", "6e7a72");
        var hornPaint = RuralPropMaterials.Surface("metal", "b3b8b1");
        var dark = RuralPropMaterials.Surface("plastic", "3a3a38");
        var wood = RuralPropMaterials.Surface("wood", "6b5a4a");
        var lamp = new StandardMaterial3D
        {
            ResourceName = "VillagePaRelayLamp",
            AlbedoColor = Color.FromHtml("5a1d14"),
            EmissionEnabled = true,
            Emission = Color.FromHtml("ff4b32"),
            EmissionEnergyMultiplier = 2.2f
        };

        // Switch box on the pier between the entrance door and the first
        // window, at hand height; the lamp shows that the PA is on.
        RuralPropGeometry.Block(club, "VillagePaSwitchBox", new(.22f, .30f, .075f), VillagePaSwitchAt, boxMetal, .008f);
        RuralPropGeometry.Block(club, "VillagePaSwitchLever", new(.05f, .12f, .05f),
            VillagePaSwitchAt + new Vector3(0, -.09f, .06f), RuralPropMaterials.Surface("plastic", "e8e4d8"), .006f);
        var switchLamp = RuralPropGeometry.Part(club, "VillagePaSwitchLamp",
            new SphereMesh { Radius = .026f, Height = .052f },
            VillagePaSwitchAt + new Vector3(.07f, .13f, .045f), lamp);
        switchLamp.Visible = false;
        club.AddChild(new Label3D
        {
            Name = "VillagePaSwitchLabel",
            Text = "ГРОМКОГОВОРИТЕЛЬ",
            Position = VillagePaSwitchAt + new Vector3(0, .23f, .05f),
            FontSize = 26,
            PixelSize = .0016f,
            OutlineSize = 0,
            Shaded = true,
            DoubleSided = false,
            Modulate = Color.FromHtml("2f2a24")
        });

        // Two street horns above the porch, mouths toward the square.
        foreach (var (tag, at) in new[] { ("A", VillagePaHornAMouth), ("B", VillagePaHornBMouth) })
        {
            RuralPropGeometry.Block(club, "VillagePaHornBracket" + tag, new(.07f, .30f, .07f),
                new(at.X, at.Y, 6.03f), boxMetal, .006f);
            RuralPropGeometry.Part(club, "VillagePaHornThroat" + tag,
                new CylinderMesh { TopRadius = .055f, BottomRadius = .075f, Height = .10f, RadialSegments = 12 },
                new(at.X, at.Y, 6.10f), dark, new Vector3(90f, 0f, 0f));
            RuralPropGeometry.Part(club, "VillagePaSquareHorn" + tag,
                new CylinderMesh { TopRadius = .17f, BottomRadius = .055f, Height = .34f, RadialSegments = 16, Rings = 1 },
                new(at.X, at.Y, 6.22f), hornPaint, new Vector3(90f, 0f, 0f));
        }

        // Foyer radio point on the hall side of the partition, feeding the
        // club's own stage speakers when the switch is in the ДК position.
        RuralPropGeometry.Block(club, "VillagePaRadioBody", new(.30f, .36f, .10f), VillagePaRadioAt, wood, .008f);
        RuralPropGeometry.Block(club, "VillagePaRadioGrille", new(.22f, .22f, .018f),
            VillagePaRadioAt + new Vector3(0, -.02f, -.055f), dark, .004f);
        RuralPropGeometry.Part(club, "VillagePaRadioKnob",
            new CylinderMesh { TopRadius = .022f, BottomRadius = .022f, Height = .03f, RadialSegments = 10 },
            VillagePaRadioAt + new Vector3(-.10f, -.13f, -.05f), boxMetal, new Vector3(90f, 0f, 0f));
        var radioLamp = RuralPropGeometry.Part(club, "VillagePaRadioLamp",
            new SphereMesh { Radius = .018f, Height = .036f },
            VillagePaRadioAt + new Vector3(.10f, -.13f, -.05f), lamp);
        radioLamp.Visible = false;

        var zone = GetZoneInstance("village_day");
        var switchTarget = PaTarget("VillagePaSwitch", "urman.chapter1:local/square/ClubPaSwitch",
            PaPrompt(VillagePaSystem.PaMode.Off), club, zone, new(1.62f, 1.42f, 6.14f), new(.55f, .62f, .38f));
        switchTarget.SetMeta("villagePaTarget", "switch");
        switchTarget.PresentationRepeatAvailable = () => true;
        switchTarget.PresentationRepeat = () =>
        {
            var mode = pa.CycleMode();
            switchTarget.Prompt = PaPrompt(mode);
            switchLamp.Visible = mode != VillagePaSystem.PaMode.Off;
            radioLamp.Visible = mode == VillagePaSystem.PaMode.Club;
            UiFoley.PlayWorld(this, switchTarget.GlobalPosition, "ui_click", -14f, 9f, 1.5f);
            if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            {
                player.NotifyTraversal(PaSwitchThought(mode));
            }
        };

        var radioTarget = PaTarget("VillagePaRadio", "urman.chapter1:local/square/ClubPaRadio",
            "Осмотреть радиоточку", club, zone, new(2.9f, 2.35f, 2.5f), new(.6f, .6f, .5f));
        radioTarget.SetMeta("villagePaTarget", "radio");
        radioTarget.PresentationRepeatAvailable = () => true;
        radioTarget.PresentationRepeat = () =>
        {
            if (GetTree().GetFirstNodeInGroup("player_controller") is FirstPersonController player)
            {
                player.NotifyTraversal(PaRadioThought(pa.Mode));
            }
        };

        pa.ConfigureSquareSpeakers(club.ToGlobal(VillagePaHornAMouth), club.ToGlobal(VillagePaHornBMouth));
        pa.ConfigureClubSpeaker(club.ToGlobal(VillagePaClubSpeakerAt));

        // The PA owner is created by the deferred household step, so this fixture
        // pass runs after Act1ConnectedWorld's one-time registration loop
        // (Act1ConnectedWorld.cs, the FindDescendants<InteractionTarget> pass).
        // Unregistered, these two targets stay invisible to the interaction ray:
        // InteractionTarget._Ready evaluates availability while
        // PresentationRepeatAvailable is still null (it is attached by PaTarget's
        // caller) and zeroes the live layer, and the routing pass never visits a
        // target it does not know. Joining the zone table here lets
        // ApplyInteractionRouting re-apply the authored ray layer exactly like
        // the square's FacilityTargets: layer 4 in village_day, 0 in interiors.
        if (zone is not null && _interactionsByZone.TryGetValue("village_day", out var bindings))
        {
            _interactionsByZone["village_day"] = bindings
                .Append(new InteractionBinding(switchTarget, switchTarget.ActiveCollisionLayer, switchTarget.CollisionMask))
                .Append(new InteractionBinding(radioTarget, radioTarget.ActiveCollisionLayer, radioTarget.CollisionMask))
                .ToArray();
            ApplyInteractionRouting();
        }
    }

    // The prompt always names the current mode, as requested; interacting
    // cycles it. Repeat-only presentation, so no journal or runtime state.
    private static string PaPrompt(VillagePaSystem.PaMode mode) => mode switch
    {
        VillagePaSystem.PaMode.Square => "Громкоговоритель: площадь",
        VillagePaSystem.PaMode.Club => "Громкоговоритель: ДК",
        _ => "Громкоговоритель: выключен"
    };

    private static string PaSwitchThought(VillagePaSystem.PaMode mode) => mode switch
    {
        VillagePaSystem.PaMode.Square =>
            "Тумблер щёлкнул. Над входом в клуб ожили рожки — над площадью поплыла запись, с хрипотцой и эхом от домов.",
        VillagePaSystem.PaMode.Club =>
            "Тумблер на «ДК». Площадь замолчала; запись теперь слышно только в клубе, из динамиков над сценой.",
        _ => "Тумблер вниз. Громкоговоритель выключен — площадь снова слышит только ветер и снег."
    };

    private static string PaRadioThought(VillagePaSystem.PaMode mode) => mode switch
    {
        VillagePaSystem.PaMode.Club =>
            "Радиоточка тёплая: провод уходит за стену, к динамикам над сценой. В зале негромко играет запись.",
        VillagePaSystem.PaMode.Square =>
            "Радиоточка молчит — звук сейчас идёт с площади, от рожков над входом в клуб.",
        _ => "Радиоточка выключена. Шкала пыльная, но провод и динамики над сценой в порядке."
    };

    // A local presentation target in the SquareLook style, but self-contained:
    // it only uses public world accessors and the public InteractionTarget API.
    private static InteractionTarget PaTarget(string name, string id, string prompt, Node3D owner,
        Node3D? parentSpace, Vector3 localAt, Vector3 size)
    {
        var target = new InteractionTarget
        {
            Name = name,
            InteractionId = id,
            Prompt = prompt,
            CollisionLayer = 4,
            CollisionMask = 0
        };
        target.AddChild(new CollisionShape3D { Name = "SurfaceRay", Shape = new BoxShape3D { Size = size } });
        (parentSpace ?? owner).AddChild(target);
        target.GlobalTransform = owner.GlobalTransform * new Transform3D(Basis.Identity, localAt);
        target.SetMeta("runtimeStateOwner", "presentation");
        return target;
    }
}
