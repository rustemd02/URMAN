using Godot;
using System.Text.Json;

namespace Urman.Godot;

public enum VehicleKind { Niva, Motorcycle, HorseCart }
public enum HorseDisposition { Calm, Wary, Slowing, Refusing }

/// <summary>Authored metres and stable IDs, independent of address labels and input ordering.</summary>
public sealed record VehicleDefinition(
    string Id, VehicleKind Kind, string DisplayName, Vector3 Spawn, float YawDegrees,
    float MaxForwardSpeed, float ReverseSpeed, float Acceleration, float BrakeDeceleration,
    float WheelBase, float SteeringDegrees, Vector3 HullSize, Vector3 HullCenter,
    Vector3 Seat, float WheelRadius, bool HasRadio)
{
    /// <summary>
    /// Optional drivetrain/tire model for vehicles that use the vendored
    /// mechanics (currently the Niva). Null keeps the authored kinematic model.
    /// </summary>
    public VehicleMechanicsProfile? Mechanics { get; init; }

    public string StateId => "vehicle/" + Id;

    public static IReadOnlyList<VehicleDefinition> Load(string resourcePath)
    {
        using var file = global::Godot.FileAccess.Open(resourcePath, global::Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidOperationException("Vehicle definitions missing: " + resourcePath);
        using var doc = JsonDocument.Parse(file.GetAsText());
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<VehicleDefinition>();
        foreach (var row in doc.RootElement.GetProperty("vehicles").EnumerateArray())
        {
            var id = row.GetProperty("id").GetString()!;
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new InvalidDataException("Invalid or duplicate vehicle ID: " + id);
            var kind = Enum.Parse<VehicleKind>(row.GetProperty("kind").GetString()!, ignoreCase: false);
            var item = new VehicleDefinition(id, kind, row.GetProperty("displayName").GetString()!,
                ReadVector(row.GetProperty("spawn")), Number(row, "yawDegrees"),
                Number(row, "maxForwardSpeed"), Number(row, "reverseSpeed"), Number(row, "acceleration"),
                Number(row, "brakeDeceleration"), Number(row, "wheelBase"), Number(row, "steeringDegrees"),
                ReadVector(row.GetProperty("hullSize")), ReadVector(row.GetProperty("hullCenter")),
                ReadVector(row.GetProperty("seat")), Number(row, "wheelRadius"),
                row.GetProperty("hasRadio").GetBoolean());
            if (item.MaxForwardSpeed <= 0 || item.ReverseSpeed <= 0 || item.Acceleration <= 0
                || item.BrakeDeceleration <= 0 || item.WheelBase <= 0 || item.WheelRadius <= 0
                || item.HullSize.X <= 0 || item.HullSize.Y <= 0 || item.HullSize.Z <= 0)
                throw new InvalidDataException("Vehicle dimensions/dynamics must be positive: " + id);
            if (row.TryGetProperty("mechanics", out var mechanics) && mechanics.ValueKind == JsonValueKind.Object)
                item = item with { Mechanics = ReadMechanics(mechanics, id) };
            result.Add(item);
        }
        return result;
    }

    internal static Vector3 ReadVector(JsonElement row)
    {
        var xyz = row.EnumerateArray().Select(v => v.GetSingle()).ToArray();
        if (xyz.Length != 3 || xyz.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid vehicle vector.");
        return new(xyz[0], xyz[1], xyz[2]);
    }

    internal static float Number(JsonElement row, string name)
    {
        var value = row.GetProperty(name).GetSingle();
        if (!float.IsFinite(value)) throw new InvalidDataException("Non-finite vehicle field: " + name);
        return value;
    }

    /// <summary>
    /// Reads the optional drivetrain block. The authored values mirror the
    /// vendored GEVP tunables (game/addons/gevp, MIT) plus the snow friction
    /// table used by the Niva tyre model.
    /// </summary>
    private static VehicleMechanicsProfile ReadMechanics(JsonElement row, string vehicleId)
    {
        var defaults = new VehicleMechanicsProfile();
        float F(string name, float fallback)
        {
            if (!row.TryGetProperty(name, out var value)) return fallback;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out var result) || !float.IsFinite(result))
                throw new InvalidDataException("Invalid mechanics field '" + name + "': " + vehicleId);
            return result;
        }
        float[] A(string name, float[] fallback)
        {
            if (!row.TryGetProperty(name, out var value)) return fallback;
            var items = value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(item => item.GetSingle()).ToArray()
                : Array.Empty<float>();
            if (items.Length == 0 || items.Any(item => !float.IsFinite(item)))
                throw new InvalidDataException("Invalid mechanics array '" + name + "': " + vehicleId);
            return items;
        }

        var profile = new VehicleMechanicsProfile
        {
            Mass = F("mass", defaults.Mass),
            MaxTorque = F("maxTorque", defaults.MaxTorque),
            IdleRpm = F("idleRpm", defaults.IdleRpm),
            MaxRpm = F("maxRpm", defaults.MaxRpm),
            EngineInertia = F("engineInertia", defaults.EngineInertia),
            EngineBrakingTorque = F("engineBrakingTorque", defaults.EngineBrakingTorque),
            ForwardGears = A("forwardGears", defaults.ForwardGears),
            ReverseGear = F("reverseGear", defaults.ReverseGear),
            FinalDrive = F("finalDrive", defaults.FinalDrive),
            ShiftTime = F("shiftTime", defaults.ShiftTime),
            ShiftUpRpm = F("shiftUpRpm", defaults.ShiftUpRpm),
            ShiftDownRpm = F("shiftDownRpm", defaults.ShiftDownRpm),
            FrontTorqueSplit = F("frontTorqueSplit", defaults.FrontTorqueSplit),
            FrontWeightDistribution = F("frontWeightDistribution", defaults.FrontWeightDistribution),
            DragCoefficient = F("dragCoefficient", defaults.DragCoefficient),
            FrontalArea = F("frontalArea", defaults.FrontalArea),
            RollingResistance = F("rollingResistance", defaults.RollingResistance),
            PeakGrip = F("peakGrip", defaults.PeakGrip),
            SnowGrip = F("snowGrip", defaults.SnowGrip),
            IceGrip = F("iceGrip", defaults.IceGrip),
            BrakeBias = F("brakeBias", defaults.BrakeBias),
            TorqueCurve = A("torqueCurve", defaults.TorqueCurve),
        };
        if (profile.Mass <= 0 || profile.MaxTorque <= 0 || profile.MaxRpm <= profile.IdleRpm
            || profile.ReverseGear <= 0 || profile.FinalDrive <= 0
            || profile.ForwardGears.Any(ratio => ratio <= 0)
            || profile.TorqueCurve.Length < 4 || profile.TorqueCurve.Length % 2 != 0
            || profile.FrontTorqueSplit is < 0 or > 1
            || profile.FrontWeightDistribution is <= 0 or >= 1
            || profile.ShiftUpRpm <= profile.ShiftDownRpm)
            throw new InvalidDataException("Invalid mechanics profile: " + vehicleId);
        return profile;
    }
}

public readonly record struct VehicleTravelDecision(bool Allowed, string Reason, float SpeedFactor = 1f);
