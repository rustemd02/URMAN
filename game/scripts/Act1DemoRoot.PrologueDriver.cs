using System;
using System.Linq;
using Godot;

namespace Urman.Godot;

// Babai at the wheel of the ride Niva. Author feedback 2026-09-29: no hand-built
// pose. The body is the ready-made Quaternius "Driving_Loop" clip (CC0, catalogue
// motion urman.anim:drive, same 65-bone skeleton as the character kit), the head
// turns to the passenger with Godot's built-in LookAtModifier3D, and the road feel
// is a few millimetres of FastNoiseLite. We only place him on the seat.
public partial class Act1DemoRoot
{
    // Where the hip joint of a man sitting on the (soft) driver's cushion is, Niva-local.
    private static readonly Vector3 DriverHip = new(-.40f, .80f, .19f);
    private const string DriverMotion = "urman.anim:drive";

    private Node3D? _driver;
    private Skeleton3D? _driverSkeleton;
    private Node3D? _driverGazeTarget;
    private Vector2 _rideShake;
    private Vector3 _driverSeatOffset;
    private readonly FastNoiseLite _rideRoadNoise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = 1.6f, Seed = 2121 };
    private double _rideClock;

    private void BuildPrologueDriver()
    {
        _driver = GeneratedCharacterKitDressing.Attach(_rideNiva!, "prologue-mansur", "Mansur", Vector3.Zero, sheltered: true);
        _driver.RotationDegrees = new(0, 180, 0);
        // The source body's crown is 1.82 m; Mansur is 1.72 m. He takes off his
        // ushanka in the heated cabin.
        _driver.Scale = Vector3.One * (1.72f / 1.82f);
        foreach (var hat in _driver.FindChildren("Mansur_Hat*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            hat.Visible = false;
        // The far-distance copies sit exactly on the near ones and z-fight at cabin range.
        foreach (var far in _driver.FindChildren("Mansur_*_LOD1", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            far.Visible = false;
        _driverSkeleton = _driver.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>()
            .First(item => item.Name.ToString().Contains("Mansur", StringComparison.Ordinal)
                || item.GetParent().Name.ToString().Contains("Mansur", StringComparison.Ordinal));

        var played = AnimationCatalog.Play(_driver, DriverMotion, 0);
        if (!played.Played) GD.PushWarning($"act1-prologue-ride: driving clip unavailable: {played.Problem}");

        AddDriverLookAt();
        AddDriverLegIk();
        AddDriverArmIk();
        _ = SeatDriverAsync();
    }

    // The clip decides where the pelvis is; once it has posed a frame, move the whole
    // body so the hip lands on the cushion.
    private async System.Threading.Tasks.Task SeatDriverAsync()
    {
        for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_driver is null || !IsInstanceValid(_driver) || _driverSkeleton is null || _rideNiva is null) return;
        var pelvis = _rideNiva.ToLocal(_driverSkeleton.ToGlobal(_driverSkeleton.GetBoneGlobalPose(_driverSkeleton.FindBone("pelvis")).Origin));
        _driverSeatOffset = DriverHip - pelvis;
        _driver.Position += _driverSeatOffset;
    }

    // Built-in look-at on the head bone, limited to an ordinary glance over the shoulder.
    private void AddDriverLookAt()
    {
        if (_driverSkeleton is not { } skeleton || _rideNiva is null || _rideCamera is null) return;
        // Look at the passenger's face, a touch below the lens so it is not a stare into the camera.
        _driverGazeTarget = new Node3D { Name = "DriverGazeTarget", Position = _rideCamera.Position + new Vector3(-.05f, -.10f, 0f) };
        _rideNiva.AddChild(_driverGazeTarget);
        var head = skeleton.FindBone("Head");
        if (head < 0) return;
        var lookAt = new LookAtModifier3D
        {
            Name = "DriverLookAt",
            BoneName = "Head",
            ForwardAxis = HeadForwardAxis(skeleton, head),
            UseAngleLimitation = true,
            SymmetryLimitation = true,
            PrimaryLimitAngle = Mathf.DegToRad(110f),
            SecondaryLimitAngle = Mathf.DegToRad(50f),
            Duration = .6f,
            TransitionType = Tween.TransitionType.Sine,
            EaseType = Tween.EaseType.InOut,
            Influence = .75f
        };
        skeleton.AddChild(lookAt);
        lookAt.TargetNode = lookAt.GetPathTo(_driverGazeTarget);
    }

    // The driving clip is authored for a chair-height seat: its shins hang straight
    // down through the Niva's floor. Godot's built-in TwoBoneIK3D puts the ankles on the
    // pedals and bends the knees up and forward; the clip keeps everything else.
    private void AddDriverLegIk()
    {
        if (_driverSkeleton is not { } skeleton || _rideNiva is null) return;
        var ik = new TwoBoneIK3D { Name = "DriverLegIk" };
        ik.SetSettingCount(2);
        var index = 0;
        foreach (var (side, x) in new[] { ("l", -.52f), ("r", -.30f) })
        {
            var pedal = new Node3D { Name = $"DriverPedal_{side}", Position = new Vector3(x, .58f, -.58f) };
            var knee = new Node3D { Name = $"DriverKneePole_{side}", Position = new Vector3(x, 1.4f, -.35f) };
            _rideNiva.AddChild(pedal);
            _rideNiva.AddChild(knee);
            ik.SetRootBoneName(index, "thigh_" + side);
            ik.SetMiddleBoneName(index, "calf_" + side);
            ik.SetEndBoneName(index, "foot_" + side);
            _driverPedals[index] = pedal;
            _driverKnees[index] = knee;
            index++;
        }
        skeleton.AddChild(ik);
        for (var i = 0; i < 2; i++)
        {
            ik.SetTargetNode(i, ik.GetPathTo(_driverPedals[i]!));
            ik.SetPoleNode(i, ik.GetPathTo(_driverKnees[i]!));
        }
    }

    // The clip's hands hold a smaller wheel close to the chest. The built-in
    // TwoBoneIK3D carries each wrist to the Niva's rim at ten and two; the clip keeps the
    // fingers' grip and the hand's attitude. Targets live on the steering-wheel node, so
    // the hands follow the wheel if it turns.
    private void AddDriverArmIk()
    {
        if (_driverSkeleton is not { } skeleton || _rideNiva?.GetNodeOrNull<Node3D>("SteeringWheel") is not { } wheel) return;
        var ik = new TwoBoneIK3D { Name = "DriverArmIk" };
        ik.SetSettingCount(2);
        var targets = new Node3D[2];
        var poles = new Node3D[2];
        var index = 0;
        foreach (var (side, sign) in new[] { ("l", -1f), ("r", 1f) })
        {
            // Rim is the wheel's local XY circle (radius 0.205), its face toward the driver (+Z).
            targets[index] = new Node3D { Name = $"DriverGrip_{side}", Position = new Vector3(sign * (side == "l" ? .20f : .18f), .085f, .075f) };
            wheel.AddChild(targets[index]);
            poles[index] = new Node3D { Name = $"DriverElbowPole_{side}", Position = new Vector3(-.40f + sign * .42f, .82f, .20f) };
            _rideNiva.AddChild(poles[index]);
            ik.SetRootBoneName(index, "upperarm_" + side);
            ik.SetMiddleBoneName(index, "lowerarm_" + side);
            ik.SetEndBoneName(index, "hand_" + side);
            index++;
        }
        skeleton.AddChild(ik);
        for (var i = 0; i < 2; i++)
        {
            ik.SetTargetNode(i, ik.GetPathTo(targets[i]));
            ik.SetPoleNode(i, ik.GetPathTo(poles[i]));
        }
    }

    private readonly Node3D?[] _driverPedals = new Node3D?[2];
    private readonly Node3D?[] _driverKnees = new Node3D?[2];

    // Which local axis of the head bone points where the character faces (-Z of the car).
    private SkeletonModifier3D.BoneAxis HeadForwardAxis(Skeleton3D skeleton, int head)
    {
        var basis = skeleton.GlobalTransform.Basis * skeleton.GetBoneGlobalRest(head).Basis;
        var facing = _rideNiva!.GlobalBasis * Vector3.Forward;
        var best = SkeletonModifier3D.BoneAxis.PlusZ;
        var score = float.MinValue;
        foreach (var (axis, dir) in new[]
                 {
                     (SkeletonModifier3D.BoneAxis.PlusX, basis.X), (SkeletonModifier3D.BoneAxis.MinusX, -basis.X),
                     (SkeletonModifier3D.BoneAxis.PlusY, basis.Y), (SkeletonModifier3D.BoneAxis.MinusY, -basis.Y),
                     (SkeletonModifier3D.BoneAxis.PlusZ, basis.Z), (SkeletonModifier3D.BoneAxis.MinusZ, -basis.Z)
                 })
        {
            var s = dir.Normalized().Dot(facing);
            if (s > score) { score = s; best = axis; }
        }
        return best;
    }

    // Road feel: a few millimetres of body movement under the camera, and the
    // driver riding the same bumps on his soft seat.
    private void TickRideRoad(float delta, float speed)
    {
        _rideClock += delta * Mathf.Clamp(speed / 8f, .2f, 1.2f);
        var t = (float)_rideClock * 10f;
        var bump = new Vector3(_rideRoadNoise.GetNoise1D(t) * .004f, _rideRoadNoise.GetNoise1D(t + 50f) * .007f, 0f);
        if (_rideCamera is not null) _rideCamera.Position = RideCameraRest + bump;
        if (_driver is not null && IsInstanceValid(_driver))
            _driver.Position = _driverSeatOffset + new Vector3(0, _rideRoadNoise.GetNoise1D(t + 13f) * .006f, 0);
    }

    // Jolted out of the nightmare: one soft sway of the camera, nothing violent.
    private async void DriverShakeAwake()
    {
        var elapsed = 0.0;
        const double total = 1.2;
        while (elapsed < total && IsInsideTree() && _rideCamera is not null && IsInstanceValid(_rideCamera) && !_prologueSkipRequested)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
            var envelope = (float)Mathf.Clamp(Mathf.Min(elapsed / .2, (total - elapsed) / .6), 0, 1);
            _rideShake = new Vector2(Mathf.Sin((float)elapsed * Mathf.Tau * 1.6f) * envelope * .6f, 0);
            ApplyPrologueRideLook();
        }
        _rideShake = Vector2.Zero;
        if (IsInsideTree() && _rideCamera is not null && IsInstanceValid(_rideCamera)) ApplyPrologueRideLook();
    }
}
