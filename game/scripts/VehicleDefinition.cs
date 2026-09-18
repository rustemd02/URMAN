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
}

public readonly record struct VehicleTravelDecision(bool Allowed, string Reason, float SpeedFactor = 1f);
