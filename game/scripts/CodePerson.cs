using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Urman.Godot;

/// <summary>
/// A person drawn in code. The imported kit keeps its skeleton, clips, seats
/// and anchors (every system that poses or places people still works), but its
/// meshes are no longer drawn: this node reads the joints each frame and builds
/// the body, winter clothes and headwear from simple solid shapes between them.
/// While the person is actually travelling, legs and arms are taken over by a
/// gait computed from the distance covered, so a foot on the ground never
/// slides and the stride is a human one whatever clip is playing.
/// Presentation only: no collision, no story or save state.
/// </summary>
public partial class CodePerson : Node3D
{
    // ---- Tuning ---------------------------------------------------------------------------
    // A walking step is .7-.8 m and lengthens with pace; the cycle is two steps.
    private const float StepAtRest = .6f, StepPerSpeed = .14f, StepMin = .68f, StepMax = .98f;
    private const float StanceShare = .6f;
    private const float FootLift = .1f;
    private const float ArmSwingDegrees = 23f;
    private const float GaitBlendSeconds = .22f;
    private const float FarUpdateDistance = 38f;
    private const float HideDistance = 110f;

    private sealed record Outfit(
        string Coat, string Trousers, string Boots, string Mittens, string Skin, string Hair,
        Headwear Head, string HeadColour, string Trim, float CoatLength, bool Skirt, bool Beard, bool Scarf, float Build);

    private enum Headwear { None, Ushanka, Knit, Kerchief, FurRound }

    private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();
    private static readonly Dictionary<(int, int), CylinderMesh> Tubes = new();
    private static SphereMesh? _ball;
    private static BoxMesh? _block;

    private Skeleton3D _skeleton = null!;
    private Outfit _outfit = null!;
    private bool _sheltered;
    private readonly Dictionary<string, int> _bones = new(StringComparer.Ordinal);
    private readonly List<MeshInstance3D> _parts = new();
    private int _next;
    private Vector3 _lastWorld;
    private bool _hasLast;
    private float _phase;
    private float _gait;
    private float _cycle = 1.5f;
    private Vector3 _travel = Vector3.Back;
    private float _groundY = float.NaN;
    private int _frame;
    private bool _ready;
    private Basis _headRestInverse = Basis.Identity;
    private Vector3 _restFront = Vector3.Back;
    private Vector3 _restUp = Vector3.Up;

    public static void Dress(Node3D instance, string prefix, string characterId, bool sheltered)
    {
        if (instance.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault() is not { } skeleton) return;
        var person = new CodePerson { Name = "CodePerson", _skeleton = skeleton, _sheltered = sheltered };
        person._outfit = OutfitFor(prefix, characterId);
        foreach (var name in new[] { "pelvis", "spine_03", "neck_01", "Head", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r",
                     "hand_l", "hand_r", "thigh_l", "thigh_r", "calf_l", "calf_r", "foot_l", "foot_r" })
        {
            var index = skeleton.FindBone(name);
            if (index < 0) index = skeleton.FindBone(name.ToLowerInvariant());
            if (index < 0) return; // Not the humanoid rig: leave the imported look alone.
            person._bones[name] = index;
        }
        // The imported surfaces stay loaded for the systems that inspect them, but are not drawn.
        foreach (var mesh in instance.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            mesh.Layers = 0;
        // The head follows its own bone (glances, look-at modifiers), measured against the rest pose.
        Vector3 Rest(string bone) => skeleton.GetBoneGlobalRest(person._bones[bone]).Origin;
        var restUp = (Rest("neck_01") - Rest("pelvis")).Normalized();
        var restAcross = (Rest("upperarm_l") - Rest("upperarm_r")).Normalized();
        if (restUp.IsFinite() && restAcross.IsFinite() && restUp != Vector3.Zero && restAcross != Vector3.Zero)
        {
            person._restUp = restUp;
            person._restFront = restAcross.Cross(restUp).Normalized();
            person._headRestInverse = skeleton.GetBoneGlobalRest(person._bones["Head"]).Basis.Orthonormalized().Inverse();
        }
        instance.SetMeta("codePerson", true);
        skeleton.AddChild(person);
    }

    // ---- Wardrobe -------------------------------------------------------------------------
    // Ordinary winter clothes of a Tatarstan village in 2026: quilted jackets and
    // down coats, felt boots, fur hats with ear flaps, knitted hats, warm shawls.
    // No lettering, emblems or ornament.
    private static Outfit OutfitFor(string prefix, string id)
    {
        const string skin = "d9a988", skinOld = "cf9f82";
        return prefix switch
        {
            "Mansur" => new("4c4a44", "2e2f33", "8a8780", "3a3632", skinOld, "b9b6ae", Headwear.Ushanka, "5a4a3c", "6b6a63", .16f, false, true, false, 1.06f),
            "Gulsina" => new("5a3a4a", "3a2c34", "8a8780", "6a4a52", skinOld, "c9c6be", Headwear.Kerchief, "e4dfd2", "8a6a74", .5f, true, false, false, .98f),
            "TimurHazrat" => new("2c3038", "25262b", "2a2a2c", "2a2c30", skinOld, "9c9a94", Headwear.FurRound, "3a3835", "44474f", .46f, false, true, true, 1.02f),
            "Naila" => new("2f6f73", "2c3340", "5a4a40", "c9b79a", skin, "3a2a22", Headwear.Knit, "d8cdb8", "e8e2d2", .3f, false, false, true, .95f),
            "Alsu" => new("e2d6c2", "3a4a66", "6a4c3a", "b5483c", skin, "2e211b", Headwear.Knit, "b5483c", "f0ebe0", .12f, false, false, true, .92f),
            "Rinat" => new("38424e", "2a2f38", "242426", "2a2c30", skin, "2a2320", Headwear.Ushanka, "4a4d52", "5a5d62", .14f, false, false, false, 1.05f),
            "Tamara" => new("6a2f37", "2f2a30", "8a8780", "4a3a40", skin, "5a4a42", Headwear.Kerchief, "9a9aa0", "7a4a50", .46f, true, false, false, 1f),
            "PhoneGuy" => new("1f2226", "2a3140", "2a2a2c", "1f2226", skin, "241c18", Headwear.Knit, "2f3a4a", "3a3d44", .1f, false, false, false, 1f),
            _ => Resident(id)
        };
    }

    private static Outfit Resident(string id)
    {
        var hash = 2166136261u;
        foreach (var c in id) hash = (hash ^ c) * 16777619u;
        var coats = new[] { "4a4f45", "3d4654", "5a4638", "474049", "2f4a52", "5b3d3a", "3a3f38", "545864" };
        var coat = coats[hash % (uint)coats.Length];
        var woman = (hash >> 5) % 2 == 0;
        var old = (hash >> 9) % 3 == 0;
        var skin = old ? "cf9f82" : "d9a988";
        var hair = old ? "b4b1a9" : new[] { "2a2019", "3a2a22", "4a3a2c" }[(hash >> 12) % 3];
        var build = .94f + ((hash >> 16) % 13) / 100f;
        if (woman)
        {
            var shawl = new[] { "d8d2c4", "8f8f96", "a35a4c", "5d7a86" }[(hash >> 20) % 4];
            return (hash >> 24) % 3 == 0
                ? new(coat, "2c3340", "5a4a40", "6a5a52", skin, hair, Headwear.Knit, shawl, "d9d3c6", .28f, false, false, true, build * .96f)
                : new(coat, "33292f", "8a8780", "5a4a4e", skin, hair, Headwear.Kerchief, shawl, "7a6a66", .48f, true, false, false, build * .97f);
        }
        return ((hash >> 24) % 3) switch
        {
            0u => new Outfit(coat, "2e2f33", "8a8780", "3a3632", skin, hair, Headwear.Ushanka, "4f4439", "66645e", .15f, false, old, false, build * 1.04f),
            1u => new Outfit(coat, "2a3140", "2a2a2c", "2c2c30", skin, hair, Headwear.Knit, "39424e", "50545c", .12f, false, false, true, build),
            _ => new Outfit(coat, "30302e", "6f6c66", "3a3632", skin, hair, Headwear.FurRound, "3d3a36", "5e5b55", .2f, false, old, false, build * 1.03f)
        };
    }

    // ---- Shapes ---------------------------------------------------------------------------

    private static StandardMaterial3D Cloth(string html)
    {
        var colour = new Color(html);
        if (Materials.TryGetValue(colour, out var material) && GodotObject.IsInstanceValid(material)) return material;
        return Materials[colour] = new StandardMaterial3D { AlbedoColor = colour, Roughness = .92f, MetallicSpecular = .15f };
    }

    private static CylinderMesh Tube(float from, float to)
    {
        var key = (Mathf.RoundToInt(from * 500f), Mathf.RoundToInt(to * 500f));
        if (Tubes.TryGetValue(key, out var mesh) && GodotObject.IsInstanceValid(mesh)) return mesh;
        return Tubes[key] = new CylinderMesh { BottomRadius = from, TopRadius = to, Height = 1f, RadialSegments = 10, Rings = 1 };
    }

    private MeshInstance3D Part(Mesh mesh, string colour)
    {
        MeshInstance3D part;
        if (_next < _parts.Count) part = _parts[_next];
        else
        {
            part = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.On, VisibilityRangeEnd = HideDistance };
            AddChild(part);
            _parts.Add(part);
        }
        _next++;
        if (part.Mesh != mesh) part.Mesh = mesh;
        var material = Cloth(colour);
        if (part.MaterialOverride != material) part.MaterialOverride = material;
        return part;
    }

    /// <summary>A tapered solid from a to b, optionally flattened front to back.</summary>
    private void Limb(Vector3 a, Vector3 b, float radiusA, float radiusB, string colour, Vector3 front, float depth = 1f)
    {
        var axis = b - a;
        var length = axis.Length();
        if (length < .002f) return;
        var y = axis / length;
        var z = Perpendicular(y, front);
        var x = y.Cross(z);
        Part(Tube(radiusA, radiusB), colour).Transform = new Transform3D(new Basis(x, y * length, z * depth), (a + b) * .5f);
    }

    private static Vector3 Perpendicular(Vector3 axis, Vector3 hint)
    {
        var z = hint - axis * hint.Dot(axis);
        if (z.LengthSquared() > .0004f) return z.Normalized();
        z = axis.Cross(Vector3.Right);
        return z.LengthSquared() > .0004f ? z.Normalized() : axis.Cross(Vector3.Up).Normalized();
    }

    private void Ball(Vector3 centre, Vector3 size, string colour, Vector3 up, Vector3 front)
    {
        _ball ??= new SphereMesh { Radius = .5f, Height = 1f, RadialSegments = 12, Rings = 6 };
        var y = up.LengthSquared() < .000001f ? Vector3.Up : up.Normalized();
        var z = Perpendicular(y, front);
        var x = y.Cross(z);
        Part(_ball, colour).Transform = new Transform3D(new Basis(x * size.X, y * size.Y, z * size.Z), centre);
    }

    private void Block(Vector3 centre, Vector3 size, string colour, Vector3 up, Vector3 front)
    {
        _block ??= new BoxMesh { Size = Vector3.One };
        var y = up.LengthSquared() < .000001f ? Vector3.Up : up.Normalized();
        var z = Perpendicular(y, front);
        var x = y.Cross(z);
        Part(_block, colour).Transform = new Transform3D(new Basis(x * size.X, y * size.Y, z * size.Z), centre);
    }

    // ---- Frame ----------------------------------------------------------------------------

    private Vector3 Joint(string bone) => _skeleton.GetBoneGlobalPose(_bones[bone]).Origin;

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_skeleton)) return;
        var world = _skeleton.GlobalPosition;
        var dt = Mathf.Max((float)delta, .0001f);
        var moved = _hasLast ? world - _lastWorld : Vector3.Zero;
        _lastWorld = world;
        _hasLast = true;
        moved.Y = 0f;
        var distance = moved.Length();
        if (distance > 1.5f) distance = 0f; // a placement, not a step
        var speed = distance / dt;
        var walking = speed > .28f;
        _gait = Mathf.MoveToward(_gait, walking ? 1f : 0f, dt / GaitBlendSeconds);
        if (walking)
        {
            var step = Mathf.Clamp(StepAtRest + StepPerSpeed * speed, StepMin, StepMax) * _outfit.Build;
            _cycle = Mathf.MoveToward(_cycle, step * 2f, dt * 1.2f);
            _phase = Mathf.PosMod(_phase + distance / _cycle, 1f);
            var local = _skeleton.GlobalBasis.Inverse() * (moved / distance);
            local.Y = 0f;
            if (local.LengthSquared() > .01f) _travel = local.Normalized();
        }
        else if (_gait <= 0f) _phase = 0f;

        // Far people are redrawn less often; standing people far away hardly at all.
        _frame++;
        var camera = GetViewport()?.GetCamera3D();
        var away = camera is null ? 0f : camera.GlobalPosition.DistanceTo(world);
        if (_ready && away > FarUpdateDistance && (_frame & (walking ? 1 : 7)) != 0) return;
        Draw();
        _ready = true;
    }

    private void Draw()
    {
        _next = 0;
        var o = _outfit;
        var b = o.Build;
        var pelvis = Joint("pelvis");
        var chest = Joint("spine_03");
        var neck = Joint("neck_01");
        var head = Joint("Head");
        var shoulderL = Joint("upperarm_l"); var shoulderR = Joint("upperarm_r");
        var elbowL = Joint("lowerarm_l"); var elbowR = Joint("lowerarm_r");
        var handL = Joint("hand_l"); var handR = Joint("hand_r");
        var hipL = Joint("thigh_l"); var hipR = Joint("thigh_r");
        var kneeL = Joint("calf_l"); var kneeR = Joint("calf_r");
        var ankleL = Joint("foot_l"); var ankleR = Joint("foot_r");

        var up = neck - pelvis;
        up = up.LengthSquared() < .0001f ? Vector3.Up : up.Normalized();
        var across = shoulderL - shoulderR;
        across = across.LengthSquared() < .0001f ? Vector3.Right : across.Normalized();
        var front = across.Cross(up).Normalized();
        if (front == Vector3.Zero) front = Vector3.Back;

        // The floor under a standing person, learned while they stand.
        var lowest = Mathf.Min(ankleL.Y, ankleR.Y);
        if (float.IsNaN(_groundY)) _groundY = lowest;
        else if (_gait <= 0f && Mathf.Abs(up.Y) > .9f) _groundY = Mathf.Lerp(_groundY, lowest, .05f);

        if (_gait > 0f)
        {
            var w = _gait * _gait * (3f - 2f * _gait);
            var travel = _travel;
            var bob = Vector3.Down * (.022f * (1f - Mathf.Cos(_phase * Mathf.Tau * 2f)) * .5f * w);
            pelvis += bob; chest += bob; neck += bob; head += bob;
            shoulderL += bob; shoulderR += bob; hipL += bob; hipR += bob;
            Stride(hipL, ref kneeL, ref ankleL, _phase, travel, w);
            Stride(hipR, ref kneeR, ref ankleR, _phase + .5f, travel, w);
            Swing(shoulderL, ref elbowL, ref handL, -Mathf.Cos(_phase * Mathf.Tau), travel, up, w);
            Swing(shoulderR, ref elbowR, ref handR, Mathf.Cos(_phase * Mathf.Tau), travel, up, w);
        }

        // Legs first: trousers, then felt boots over them.
        foreach (var (hip, knee, ankle) in new[] { (hipL, kneeL, ankleL), (hipR, kneeR, ankleR) })
        {
            Limb(hip, knee, .082f * b, .064f * b, o.Trousers, front);
            Limb(knee, ankle, .064f * b, .05f * b, o.Trousers, front);
            Ball(knee, Vector3.One * .13f * b, o.Trousers, up, front);
            var shin = ankle - knee;
            var shaft = knee + shin * .42f;
            Limb(shaft, ankle + shin.Normalized() * .03f, .07f * b, .066f * b, o.Boots, front);
            var toe = _gait > 0f ? _travel : front;
            toe = new Vector3(toe.X, 0f, toe.Z).Normalized();
            if (toe == Vector3.Zero) toe = Vector3.Back;
            // A felt boot: rounded heel, a low vamp running forward, a blunt toe.
            Ball(ankle - Vector3.Up * .035f, new Vector3(.118f, .105f, .125f) * b, o.Boots, Vector3.Up, toe);
            Limb(ankle - toe * .03f - Vector3.Up * .045f, ankle + toe * .15f - Vector3.Up * .052f, .054f * b, .047f * b, o.Boots, Vector3.Up, .78f);
            Ball(ankle + toe * .155f - Vector3.Up * .052f, new Vector3(.1f, .076f, .1f) * b, o.Boots, Vector3.Up, toe);
        }

        // Body: hips, chest, shoulders, and the coat hanging below the belt.
        var coat = _sheltered ? o.Trim : o.Coat;
        Limb(pelvis - up * .05f, chest, .175f * b, .19f * b, coat, front, .74f);
        Limb(chest, neck - up * .015f, .19f * b, .12f * b, coat, front, .72f);
        // Sloping shoulders and real hips instead of a tube with arms.
        Ball(neck - up * .055f, new Vector3(.34f, .13f, .2f) * b, coat, up, front);
        Ball(pelvis - up * .02f, new Vector3(.37f, .22f, .27f) * b, coat, up, front);
        if (!_sheltered) Limb(neck - up * .04f, neck + up * .03f, .09f * b, .072f * b, o.Trim, front, .92f);
        Ball(shoulderL, Vector3.One * .15f * b, coat, up, front);
        Ball(shoulderR, Vector3.One * .15f * b, coat, up, front);
        var hem = _sheltered ? Mathf.Min(o.CoatLength, .14f) : o.CoatLength;
        // Seated: the thighs lie forward, so nothing hangs straight down through the seat.
        var thighs = ((kneeL + kneeR) * .5f - pelvis);
        var seated = thighs.LengthSquared() > .01f && thighs.Normalized().Dot(-up) < .55f;
        if (seated)
        {
            if (o.Skirt || hem > .2f)
                Limb(pelvis - up * .02f, pelvis + thighs * .92f - up * .02f, .21f * b, .19f * b, o.Skirt && _sheltered ? o.Trousers : coat, up, .6f);
        }
        else if (o.Skirt)
            Limb(pelvis - up * (hem + .3f), pelvis + up * .02f, .285f * b, .19f * b, _sheltered ? o.Trousers : coat, front, .82f);
        else
            Limb(pelvis - up * hem, pelvis + up * .02f, (.2f + hem * .12f) * b, .185f * b, coat, front, .78f);
        if (!_sheltered && !o.Skirt)
            Limb(pelvis + up * .03f, pelvis + up * .075f, .19f * b, .19f * b, o.Trim, front, .76f); // belt line

        // Arms and mittens.
        foreach (var (shoulder, elbow, hand) in new[] { (shoulderL, elbowL, handL), (shoulderR, elbowR, handR) })
        {
            Limb(shoulder, elbow, .062f * b, .052f * b, coat, front);
            Limb(elbow, hand, .052f * b, .043f * b, coat, front);
            Ball(elbow, Vector3.One * .105f * b, coat, up, front);
            var reach = hand - elbow;
            reach = reach.LengthSquared() < .0001f ? Vector3.Down : reach.Normalized();
            if (!_sheltered)
            {
                // Mitten: cuff, the hand, and the thumb set apart.
                var palm = Perpendicular(reach, front);
                var thumbSide = reach.Cross(palm).Normalized() * (shoulder == shoulderL ? -1f : 1f);
                Limb(hand - reach * .035f, hand + reach * .01f, .05f * b, .046f * b, o.Trim, front);
                Ball(hand + reach * .06f, new Vector3(.082f, .125f, .052f) * b, o.Mittens, reach, palm);
                Ball(hand + reach * .04f + thumbSide * .042f, new Vector3(.03f, .065f, .03f) * b, o.Mittens, reach + thumbSide * .6f, palm);
                continue;
            }
            // A bare hand: wrist cuff, palm, the fingers as one curved pad, and a thumb.
            var grip = Perpendicular(reach, front);
            var thumb = reach.Cross(grip).Normalized() * (shoulder == shoulderL ? -1f : 1f);
            Limb(hand - reach * .03f, hand + reach * .012f, .046f * b, .04f * b, o.Trim, front);
            Ball(hand + reach * .045f, new Vector3(.082f, .095f, .034f) * b, o.Skin, reach, grip);
            Ball(hand + reach * .105f + grip * .012f, new Vector3(.074f, .075f, .028f) * b, o.Skin, reach + grip * .5f, grip);
            Ball(hand + reach * .04f + thumb * .045f + grip * .012f, new Vector3(.026f, .07f, .026f) * b, o.Skin, reach + thumb * .7f, grip);
        }

        // Neck, scarf, head.
        var turn = _skeleton.GetBoneGlobalPose(_bones["Head"]).Basis.Orthonormalized() * _headRestInverse;
        var headUp = (turn * _restUp).Normalized();
        var bodyFront = front;
        front = (turn * _restFront).Normalized();
        if (!headUp.IsFinite() || !front.IsFinite() || headUp == Vector3.Zero || front == Vector3.Zero)
        {
            headUp = up;
            front = bodyFront;
        }
        Limb(neck - up * .02f, head, .05f * b, .046f * b, o.Skin, bodyFront);
        if (o.Scarf && !_sheltered) Limb(neck - up * .03f, neck + up * .06f, .1f * b, .085f * b, o.HeadColour, bodyFront, .9f);
        var skull = head + headUp * .095f;
        Ball(skull, new Vector3(.2f, .235f, .215f), o.Skin, headUp, front);
        // A face that reads at a distance: brow shadow, nose, mouth line.
        var side = headUp.Cross(front).Normalized();
        Ball(skull + front * .1f - headUp * .012f, new Vector3(.032f, .05f, .04f), o.Skin, headUp, front);
        Ball(skull + front * .094f + headUp * .028f + side * .04f, new Vector3(.03f, .014f, .02f), "1c1714", headUp, front);
        Ball(skull + front * .094f + headUp * .028f - side * .04f, new Vector3(.03f, .014f, .02f), "1c1714", headUp, front);
        Ball(skull + front * .092f - headUp * .062f, new Vector3(.06f, .012f, .02f), "8a5a4e", headUp, front);
        if (o.Beard) Ball(skull + front * .06f - headUp * .1f, new Vector3(.15f, .13f, .12f), o.Hair, headUp, front);

        var headwear = _sheltered && o.Head != Headwear.Kerchief ? Headwear.None : o.Head;
        switch (headwear)
        {
            case Headwear.None:
                Ball(skull + headUp * .03f - front * .025f, new Vector3(.212f, .2f, .22f), o.Hair, headUp, front);
                break;
            case Headwear.Ushanka:
                // Crown, the turned-up front flap, and the ear flaps tied on top.
                Limb(skull + headUp * .02f, skull + headUp * .135f, .118f, .108f, o.HeadColour, front);
                Block(skull + headUp * .075f + front * .105f, new Vector3(.17f, .085f, .03f), o.Trim, headUp, front);
                Block(skull + headUp * .06f + side * .112f, new Vector3(.03f, .11f, .15f), o.Trim, headUp, front);
                Block(skull + headUp * .06f - side * .112f, new Vector3(.03f, .11f, .15f), o.Trim, headUp, front);
                break;
            case Headwear.Knit:
                Ball(skull + headUp * .045f - front * .008f, new Vector3(.222f, .19f, .232f), o.HeadColour, headUp, front);
                Limb(skull + headUp * .0f, skull + headUp * .04f, .113f, .113f, o.Trim, front);
                Ball(skull + headUp * .15f, Vector3.One * .055f, o.Trim, headUp, front);
                Ball(skull - headUp * .02f - front * .05f, new Vector3(.2f, .17f, .16f), o.Hair, headUp, front);
                break;
            case Headwear.FurRound:
                Limb(skull + headUp * .025f, skull + headUp * .125f, .122f, .118f, o.HeadColour, front);
                break;
            case Headwear.Kerchief:
                // A warm shawl: over the head, round the face, down onto the shoulders.
                Ball(skull + headUp * .012f - front * .03f, new Vector3(.25f, .262f, .25f), o.HeadColour, headUp, front);
                Limb(neck - up * .1f, skull - headUp * .06f - front * .02f, .17f * b, .1f, o.HeadColour, bodyFront, .8f);
                Ball(neck + bodyFront * .085f - up * .01f, new Vector3(.07f, .06f, .05f), o.HeadColour, up, bodyFront);
                break;
        }

        for (var index = _next; index < _parts.Count; index++) _parts[index].Visible = false;
        for (var index = 0; index < _next; index++) if (!_parts[index].Visible) _parts[index].Visible = true;
    }

    /// <summary>
    /// One leg of the walk. In stance the foot goes back under the body exactly
    /// as fast as the body goes forward, so it stays where it was put down; in
    /// swing it is carried forward in an arc. The knee comes from the two bone lengths.
    /// </summary>
    private void Stride(Vector3 hip, ref Vector3 knee, ref Vector3 ankle, float phase, Vector3 travel, float weight)
    {
        var thigh = Mathf.Max(.2f, (Joint("calf_l") - Joint("thigh_l")).Length());
        var shin = Mathf.Max(.2f, (Joint("foot_l") - Joint("calf_l")).Length());
        var cycle = _cycle;
        phase = Mathf.PosMod(phase, 1f);
        float along, lift;
        if (phase < StanceShare)
        {
            along = cycle * (StanceShare * .5f - phase);
            lift = 0f;
        }
        else
        {
            var u = (phase - StanceShare) / (1f - StanceShare);
            var eased = u * u * (3f - 2f * u);
            along = cycle * StanceShare * (eased - .5f);
            lift = Mathf.Sin(u * Mathf.Pi) * FootLift;
        }
        var target = new Vector3(hip.X, _groundY + lift, hip.Z) + travel * along;
        var reach = target - hip;
        var length = Mathf.Clamp(reach.Length(), .12f, thigh + shin - .004f);
        var down = reach.Normalized();
        target = hip + down * length;
        var cos = Mathf.Clamp((thigh * thigh + length * length - shin * shin) / (2f * thigh * length), -1f, 1f);
        var forward = travel - down * travel.Dot(down);
        forward = forward.LengthSquared() < .0001f ? Vector3.Back : forward.Normalized();
        var bent = hip + down * (cos * thigh) + forward * (Mathf.Sqrt(1f - cos * cos) * thigh);
        knee = knee.Lerp(bent, weight);
        ankle = ankle.Lerp(target, weight);
    }

    private void Swing(Vector3 shoulder, ref Vector3 elbow, ref Vector3 hand, float swing, Vector3 travel, Vector3 up, float weight)
    {
        var upper = Mathf.Max(.15f, (Joint("lowerarm_l") - Joint("upperarm_l")).Length());
        var fore = Mathf.Max(.15f, (Joint("hand_l") - Joint("lowerarm_l")).Length());
        var angle = Mathf.DegToRad(ArmSwingDegrees) * swing;
        var first = (-up * Mathf.Cos(angle) + travel * Mathf.Sin(angle)).Normalized();
        var second = angle + Mathf.DegToRad(16f) + Mathf.Max(0f, angle) * .5f;
        var next = (-up * Mathf.Cos(second) + travel * Mathf.Sin(second)).Normalized();
        var bentElbow = shoulder + first * upper;
        elbow = elbow.Lerp(bentElbow, weight);
        hand = hand.Lerp(bentElbow + next * fore, weight);
    }
}
