using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urman.Studio.Core.Storage;

namespace Urman.Studio.Core.AI;

public sealed record RoadControlPoint(double X, double Z);

public sealed record RoadSourceRecord(
    string Id,
    string Street,
    double Width,
    IReadOnlyList<RoadControlPoint> Points);

/// <summary>Reads active authored road sources and smooths one selected point interval in place.</summary>
public static class RoadSegmentSmoothSource
{
    private const double JunctionEpsilon = 1e-4;
    public const string Operation = "road.segment.smooth";
    private const string TransactionOwnerPrefix = "urman.studio:" + Operation + "/";
    private const string OpenPartPath = "game/content/world/act1_open_part.world.v1.json";
    private const string FarBankPath = "game/content/world/act1_far_bank.world.v1.json";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static IReadOnlyList<RoadSourceRecord> Read(string sourcePath, string sourceText) =>
        ParseSource(sourcePath, sourceText).Records;

    /// <summary>
    /// Smooths an inclusive point interval. The interval endpoints and their adjacent points stay fixed;
    /// coincident points shared with another road are also fixed. Only the selected road's points span is edited.
    /// </summary>
    public static string Smooth(
        string sourcePath,
        string sourceText,
        string roadId,
        int startIndex,
        int endIndex,
        double strength,
        double maxDeviation,
        IReadOnlyList<RoadSourceRecord>? additionalRoads = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(roadId);
        ValidateSourcePath(sourcePath);
        if (string.IsNullOrWhiteSpace(roadId))
            throw new ArgumentException("Road ID must be non-empty.", nameof(roadId));
        if (!double.IsFinite(strength) || strength is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be finite and between 0 and 1.");
        if (!double.IsFinite(maxDeviation) || maxDeviation is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(maxDeviation), "Maximum deviation must be finite and between 0 and 1 metre.");

        var parsed = ParseSource(sourcePath, sourceText);
        var selectedIndex = parsed.Roads.FindIndex(road => string.Equals(road.Record.Id, roadId, StringComparison.Ordinal));
        if (selectedIndex < 0)
            throw new KeyNotFoundException($"Road source does not contain road '{roadId}'.");

        var selected = parsed.Roads[selectedIndex];
        var points = selected.Record.Points;
        if (startIndex < 0 || startIndex >= points.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (endIndex < startIndex || endIndex >= points.Count)
            throw new ArgumentOutOfRangeException(nameof(endIndex));
        if (endIndex - startIndex < 4)
            throw new ArgumentException("The selected interval must contain at least five points.", nameof(endIndex));
        var otherRoads = GetOtherRoads(parsed, selectedIndex, additionalRoads);
        if (strength == 0 || maxDeviation == 0)
            return sourceText;

        var locked = new HashSet<int> { startIndex, startIndex + 1, endIndex - 1, endIndex };
        for (var index = startIndex + 2; index <= endIndex - 2; index++)
        {
            if (CoincidesWithAnotherRoad(otherRoads, points[index]))
                locked.Add(index);
        }

        var pointIndex = FindUniqueProperty(selected.Document, "points", required: true);
        var pointDocument = JsonSpanDocument.Parse(selected.Document.RawAt(pointIndex));
        var updatedPoints = points.ToArray();
        var changed = false;
        for (var index = startIndex + 2; index <= endIndex - 2; index++)
        {
            if (locked.Contains(index))
                continue;

            var current = points[index];
            var previous = points[index - 1];
            var next = points[index + 1];
            if (!TrySmoothPoint(current, previous, next, strength, maxDeviation, out var updated)
                || updated == current)
                continue;

            updatedPoints[index] = updated;
            pointDocument.Replace(index, ToJsonPoint(updated));
            changed = true;
        }

        if (!changed)
            return sourceText;

        if (!PreservesOtherRoadIntersections(points, updatedPoints, otherRoads))
            throw new InvalidOperationException("Smoothing would move, create, or remove an intersection with another road.");

        selected.Document.ReplaceRaw(pointIndex, pointDocument.ToText());
        parsed.RoadsDocument.ReplaceRaw(selectedIndex, selected.Document.ToText());
        parsed.Root.ReplaceRaw(parsed.RoadsPropertyIndex, parsed.RoadsDocument.ToText());
        return parsed.Root.ToText();
    }

    private static ParsedSource ParseSource(string sourcePath, string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ValidateSourcePath(sourcePath);
        ValidateDocument(sourceText);

        try
        {
            var root = JsonSpanDocument.Parse(sourceText);
            if (root.RootIsArray)
                throw new InvalidDataException("Road source root must be an object.");

            var roadsPropertyIndex = FindUniqueProperty(root, "roads", required: true);
            var roadsDocument = JsonSpanDocument.Parse(root.RawAt(roadsPropertyIndex));
            if (!roadsDocument.RootIsArray)
                throw new InvalidDataException("Road source 'roads' must be an array.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var roads = new List<ParsedRoad>(roadsDocument.Count);
            for (var roadIndex = 0; roadIndex < roadsDocument.Count; roadIndex++)
            {
                if (roadsDocument.Get(roadIndex) is not JsonObject road)
                    throw new InvalidDataException($"Road source roads[{roadIndex}] must be an object.");

                var context = $"roads[{roadIndex}]";
                var id = RequiredString(road, "id", context);
                if (!ids.Add(id))
                    throw new InvalidDataException($"Road source contains duplicate road ID '{id}'.");
                var street = RequiredString(road, "street", context);
                var width = RequiredFiniteNumber(road, "width", context);
                if (width <= 0 || width > float.MaxValue)
                    throw new InvalidDataException($"Road source {context} width must be positive and finite in Godot's float range.");

                var roadDocument = JsonSpanDocument.Parse(roadsDocument.RawAt(roadIndex));
                if (roadDocument.RootIsArray)
                    throw new InvalidDataException($"Road source {context} must be an object.");
                var pointsPropertyIndex = FindUniqueProperty(roadDocument, "points", required: true);
                var pointsDocument = JsonSpanDocument.Parse(roadDocument.RawAt(pointsPropertyIndex));
                if (!pointsDocument.RootIsArray || pointsDocument.Count < 2)
                    throw new InvalidDataException($"Road source {context} points must contain at least two coordinate pairs.");

                var points = new RoadControlPoint[pointsDocument.Count];
                for (var pointIndex = 0; pointIndex < pointsDocument.Count; pointIndex++)
                {
                    if (pointsDocument.Get(pointIndex) is not JsonArray pair || pair.Count != 2)
                        throw new InvalidDataException($"Road source {context} points[{pointIndex}] must be an [x, z] pair.");
                    var x = RequiredFiniteNumber(pair[0], $"{context} points[{pointIndex}][0]");
                    var z = RequiredFiniteNumber(pair[1], $"{context} points[{pointIndex}][1]");
                    if (Math.Abs(x) > float.MaxValue || Math.Abs(z) > float.MaxValue)
                        throw new InvalidDataException($"Road source {context} points[{pointIndex}] exceed Godot's finite float coordinate range.");
                    points[pointIndex] = new RoadControlPoint(x, z);
                }

                roads.Add(new ParsedRoad(
                    new RoadSourceRecord(id, street, width, Array.AsReadOnly(points)),
                    roadDocument));
            }

            return new ParsedSource(root, roadsDocument, roadsPropertyIndex, roads);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Road source is not one strict JSON object with a roads array.", error);
        }
    }

    private static IReadOnlyList<IReadOnlyList<RoadControlPoint>> GetOtherRoads(
        ParsedSource parsed,
        int selectedRoadIndex,
        IReadOnlyList<RoadSourceRecord>? additionalRoads)
    {
        var roads = new List<IReadOnlyList<RoadControlPoint>>(parsed.Roads.Count + (additionalRoads?.Count ?? 0));
        for (var roadIndex = 0; roadIndex < parsed.Roads.Count; roadIndex++)
        {
            if (roadIndex == selectedRoadIndex)
                continue;
            roads.Add(parsed.Roads[roadIndex].Record.Points);
        }

        if (additionalRoads is null)
            return roads;

        for (var roadIndex = 0; roadIndex < additionalRoads.Count; roadIndex++)
        {
            var road = additionalRoads[roadIndex]
                ?? throw new ArgumentException($"Additional road {roadIndex} cannot be null.", nameof(additionalRoads));
            if (string.IsNullOrWhiteSpace(road.Id)
                || string.IsNullOrWhiteSpace(road.Street)
                || !double.IsFinite(road.Width)
                || road.Width <= 0
                || road.Width > float.MaxValue
                || road.Points is null
                || road.Points.Count < 2)
                throw new ArgumentException($"Additional road {roadIndex} is not a valid road record.", nameof(additionalRoads));
            foreach (var point in road.Points)
            {
                if (point is null
                    || !double.IsFinite(point.X)
                    || !double.IsFinite(point.Z)
                    || Math.Abs(point.X) > float.MaxValue
                    || Math.Abs(point.Z) > float.MaxValue)
                    throw new ArgumentException($"Additional road {roadIndex} contains a coordinate outside Godot's finite float range.", nameof(additionalRoads));
            }
            roads.Add(road.Points);
        }

        return roads;
    }

    private static bool CoincidesWithAnotherRoad(
        IReadOnlyList<IReadOnlyList<RoadControlPoint>> roads,
        RoadControlPoint point)
    {
        foreach (var road in roads)
        {
            foreach (var other in road)
            {
                var dx = point.X - other.X;
                var dz = point.Z - other.Z;
                if (Math.Abs(dx) <= JunctionEpsilon
                    && Math.Abs(dz) <= JunctionEpsilon
                    && dx * dx + dz * dz <= JunctionEpsilon * JunctionEpsilon)
                    return true;
            }
        }

        return false;
    }

    private static bool PreservesOtherRoadIntersections(
        IReadOnlyList<RoadControlPoint> before,
        IReadOnlyList<RoadControlPoint> after,
        IReadOnlyList<IReadOnlyList<RoadControlPoint>> otherRoads)
    {
        for (var segmentIndex = 0; segmentIndex < before.Count - 1; segmentIndex++)
        {
            foreach (var otherRoad in otherRoads)
            {
                for (var otherSegmentIndex = 0; otherSegmentIndex < otherRoad.Count - 1; otherSegmentIndex++)
                {
                    var oldContact = FindSegmentContact(
                        before[segmentIndex], before[segmentIndex + 1],
                        otherRoad[otherSegmentIndex], otherRoad[otherSegmentIndex + 1]);
                    var newContact = FindSegmentContact(
                        after[segmentIndex], after[segmentIndex + 1],
                        otherRoad[otherSegmentIndex], otherRoad[otherSegmentIndex + 1]);
                    if (!SameContact(oldContact, newContact))
                        return false;
                }
            }
        }

        return true;
    }

    private static SegmentContact FindSegmentContact(
        RoadControlPoint a0,
        RoadControlPoint a1,
        RoadControlPoint b0,
        RoadControlPoint b1)
    {
        var rx = a1.X - a0.X;
        var rz = a1.Z - a0.Z;
        var sx = b1.X - b0.X;
        var sz = b1.Z - b0.Z;
        var rLength = StableLength(rx, rz);
        var sLength = StableLength(sx, sz);
        if (rLength <= JunctionEpsilon)
            return ContactOfPointAndSegment(a0, b0, b1, sLength);
        if (sLength <= JunctionEpsilon)
            return ContactOfPointAndSegment(b0, a0, a1, rLength);

        var qx = b0.X - a0.X;
        var qz = b0.Z - a0.Z;
        var denominator = Cross(rx, rz, sx, sz);
        var parallelLimit = 1e-12 * rLength * sLength;
        if (Math.Abs(denominator) <= parallelLimit)
        {
            var distanceToLine = Math.Abs(Cross(qx, qz, rx, rz)) / rLength;
            if (distanceToLine > JunctionEpsilon)
                return SegmentContact.None;

            var t0 = (qx * rx + qz * rz) / (rLength * rLength);
            var q1x = b1.X - a0.X;
            var q1z = b1.Z - a0.Z;
            var t1 = (q1x * rx + q1z * rz) / (rLength * rLength);
            var low = Math.Max(0, Math.Min(t0, t1));
            var high = Math.Min(1, Math.Max(t0, t1));
            var parameterTolerance = JunctionEpsilon / rLength;
            if (high < low - parameterTolerance)
                return SegmentContact.None;

            var start = PointAlong(a0, rx, rz, Math.Clamp(low, 0, 1));
            var end = PointAlong(a0, rx, rz, Math.Clamp(high, 0, 1));
            return high - low <= parameterTolerance
                ? new SegmentContact(ContactKind.Point, Midpoint(start, end), null)
                : new SegmentContact(ContactKind.Overlap, start, end);
        }

        var t = Cross(qx, qz, sx, sz) / denominator;
        var u = Cross(qx, qz, rx, rz) / denominator;
        if (t < -JunctionEpsilon / rLength || t > 1 + JunctionEpsilon / rLength
            || u < -JunctionEpsilon / sLength || u > 1 + JunctionEpsilon / sLength)
            return SegmentContact.None;

        var intersection = PointAlong(a0, rx, rz, Math.Clamp(t, 0, 1));
        return new SegmentContact(ContactKind.Point, intersection, null);
    }

    private static SegmentContact ContactOfPointAndSegment(
        RoadControlPoint point,
        RoadControlPoint segmentStart,
        RoadControlPoint segmentEnd,
        double segmentLength)
    {
        if (segmentLength <= JunctionEpsilon)
            return Distance(point, segmentStart) <= JunctionEpsilon
                ? new SegmentContact(ContactKind.Point, Midpoint(point, segmentStart), null)
                : SegmentContact.None;

        var dx = segmentEnd.X - segmentStart.X;
        var dz = segmentEnd.Z - segmentStart.Z;
        var t = ((point.X - segmentStart.X) * dx + (point.Z - segmentStart.Z) * dz)
            / (segmentLength * segmentLength);
        if (t < -JunctionEpsilon / segmentLength || t > 1 + JunctionEpsilon / segmentLength)
            return SegmentContact.None;
        var nearest = PointAlong(segmentStart, dx, dz, Math.Clamp(t, 0, 1));
        return Distance(point, nearest) <= JunctionEpsilon
            ? new SegmentContact(ContactKind.Point, Midpoint(point, nearest), null)
            : SegmentContact.None;
    }

    private static bool SameContact(SegmentContact left, SegmentContact right)
    {
        if (left.Kind != right.Kind)
            return false;
        return left.Kind switch
        {
            ContactKind.None => true,
            ContactKind.Point => Distance(left.First!, right.First!) <= JunctionEpsilon,
            ContactKind.Overlap =>
                (Distance(left.First!, right.First!) <= JunctionEpsilon
                 && Distance(left.Second!, right.Second!) <= JunctionEpsilon)
                || (Distance(left.First!, right.Second!) <= JunctionEpsilon
                    && Distance(left.Second!, right.First!) <= JunctionEpsilon),
            _ => false
        };
    }

    private static double Cross(double ax, double az, double bx, double bz) => ax * bz - az * bx;

    private static RoadControlPoint PointAlong(RoadControlPoint start, double dx, double dz, double amount) =>
        new(start.X + dx * amount, start.Z + dz * amount);

    private static RoadControlPoint Midpoint(RoadControlPoint left, RoadControlPoint right) =>
        new(left.X * 0.5 + right.X * 0.5, left.Z * 0.5 + right.Z * 0.5);

    private static double Distance(RoadControlPoint left, RoadControlPoint right) =>
        StableLength(left.X - right.X, left.Z - right.Z);

    private enum ContactKind
    {
        None,
        Point,
        Overlap
    }

    private sealed record SegmentContact(ContactKind Kind, RoadControlPoint? First, RoadControlPoint? Second)
    {
        public static SegmentContact None { get; } = new(ContactKind.None, null, null);
    }

    public static bool IsTransactionOwner(string? owner)
    {
        if (owner is null || !owner.StartsWith(TransactionOwnerPrefix, StringComparison.Ordinal))
            return false;
        var requestId = owner.AsSpan(TransactionOwnerPrefix.Length);
        return requestId.Length == 32
            && Guid.TryParseExact(requestId, "N", out var parsed)
            && string.Equals(requestId.ToString(), parsed.ToString("N"), StringComparison.Ordinal);
    }

    public static bool IsTransactionPath(string? owner, string? path) =>
        IsTransactionOwner(owner) && path is OpenPartPath or FarBankPath;

    public static bool IsTransactionFileSet(IReadOnlyList<string> paths) =>
        paths is { Count: 1 } && paths[0] is OpenPartPath or FarBankPath;

    private static bool TrySmoothPoint(
        RoadControlPoint current,
        RoadControlPoint previous,
        RoadControlPoint next,
        double strength,
        double maxDeviation,
        out RoadControlPoint updated)
    {
        var scale = Math.Max(
            Math.Max(Math.Abs(current.X), Math.Abs(current.Z)),
            Math.Max(
                Math.Max(Math.Abs(previous.X), Math.Abs(previous.Z)),
                Math.Max(Math.Abs(next.X), Math.Abs(next.Z))));
        if (scale == 0)
        {
            updated = current;
            return false;
        }

        // Work in a normalized frame so finite but unusually large authored coordinates cannot overflow.
        var currentX = current.X / scale;
        var currentZ = current.Z / scale;
        var deltaX = ((previous.X / scale - currentX) * 0.5 + (next.X / scale - currentX) * 0.5) * strength;
        var deltaZ = ((previous.Z / scale - currentZ) * 0.5 + (next.Z / scale - currentZ) * 0.5) * strength;
        var normalizedLength = StableLength(deltaX, deltaZ);
        if (normalizedLength == 0)
        {
            updated = current;
            return false;
        }

        var normalizedLimit = maxDeviation / scale;
        double offsetX;
        double offsetZ;
        if (normalizedLength > normalizedLimit)
        {
            offsetX = deltaX / normalizedLength * maxDeviation;
            offsetZ = deltaZ / normalizedLength * maxDeviation;
        }
        else
        {
            offsetX = deltaX * scale;
            offsetZ = deltaZ * scale;
        }

        var x = current.X + offsetX;
        var z = current.Z + offsetZ;
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            updated = current;
            return false;
        }

        var appliedLength = StableLength(x - current.X, z - current.Z);
        if (appliedLength > maxDeviation)
        {
            // Adding a clamped offset to world coordinates can round its measured
            // length a few ULPs above the requested cap. Retreat inside the cap
            // before rejecting steps that are too coarse to represent safely.
            var coordinateMagnitude = Math.Max(
                Math.Max(Math.Abs(current.X), Math.Abs(current.Z)),
                Math.Max(Math.Abs(x), Math.Abs(z)));
            var coordinateUlp = Math.BitIncrement(coordinateMagnitude) - coordinateMagnitude;
            var safeLength = maxDeviation - 8 * coordinateUlp;
            if (safeLength <= 0)
            {
                updated = current;
                return false;
            }

            var correction = safeLength / appliedLength;
            x = current.X + (x - current.X) * correction;
            z = current.Z + (z - current.Z) * correction;
            appliedLength = StableLength(x - current.X, z - current.Z);
            if (appliedLength > maxDeviation)
            {
                updated = current;
                return false;
            }
        }

        updated = new RoadControlPoint(x, z);
        return updated != current;
    }

    private static double StableLength(double x, double z)
    {
        var scale = Math.Max(Math.Abs(x), Math.Abs(z));
        if (scale == 0)
            return 0;
        return scale * Math.Sqrt((x / scale) * (x / scale) + (z / scale) * (z / scale));
    }

    private static JsonArray ToJsonPoint(RoadControlPoint point) =>
        new(JsonValue.Create(point.X), JsonValue.Create(point.Z));

    private static void ValidateSourcePath(string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        if (sourcePath is not OpenPartPath and not FarBankPath)
            throw new ArgumentException("Road smoothing can edit only the two active Act 1 road sources.", nameof(sourcePath));
    }

    private static void ValidateDocument(string text)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(text);
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
            ValidateUniqueProperties(document.RootElement);
        }
        catch (EncoderFallbackException error)
        {
            throw new InvalidDataException("Road source is not valid UTF-8 text.", error);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Road source is not one strict JSON document.", error);
        }
    }

    private static void ValidateUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Road source contains duplicate JSON property '{property.Name}'.");
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateUniqueProperties(item);
        }
    }

    private static int FindUniqueProperty(JsonSpanDocument document, string key, bool required)
    {
        var found = -1;
        for (var index = 0; index < document.Count; index++)
        {
            if (document.KeyAt(index) != key)
                continue;
            if (found >= 0)
                throw new InvalidDataException($"Road source contains duplicate structural property '{key}'.");
            found = index;
        }
        if (required && found < 0)
            throw new InvalidDataException($"Road source is missing required property '{key}'.");
        return found;
    }

    private static string RequiredString(JsonObject value, string property, string context)
    {
        if (!value.TryGetPropertyValue(property, out var node)
            || node is not JsonValue jsonValue
            || !jsonValue.TryGetValue<string>(out var text)
            || string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException($"Road source {context} must have a non-empty string '{property}'.");
        return text;
    }

    private static double RequiredFiniteNumber(JsonObject value, string property, string context)
    {
        if (!value.TryGetPropertyValue(property, out var node))
            throw new InvalidDataException($"Road source {context} is missing '{property}'.");
        return RequiredFiniteNumber(node, $"{context} '{property}'");
    }

    private static double RequiredFiniteNumber(JsonNode? node, string context)
    {
        if (node is not JsonValue value
            || !value.TryGetValue<double>(out var number)
            || !double.IsFinite(number))
            throw new InvalidDataException($"Road source {context} must be a finite number.");
        return number;
    }

    private sealed record ParsedRoad(RoadSourceRecord Record, JsonSpanDocument Document);

    private sealed record ParsedSource(
        JsonSpanDocument Root,
        JsonSpanDocument RoadsDocument,
        int RoadsPropertyIndex,
        List<ParsedRoad> Roads)
    {
        public IReadOnlyList<RoadSourceRecord> Records =>
            Array.AsReadOnly(Roads.Select(road => road.Record).ToArray());
    }
}
