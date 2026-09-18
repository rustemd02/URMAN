using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Urman.Godot;

[Flags]
public enum SettlementTravelMode { None = 0, Foot = 1, Car = 2, Motorcycle = 4, HorseCart = 8, All = 15 }

public readonly record struct SettlementPoint(double X, double Y, double Z)
{
    public double Distance(SettlementPoint other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2) + Math.Pow(Z - other.Z, 2));
    public double DistanceXZ(SettlementPoint other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Z - other.Z, 2));
    public SettlementPoint Lerp(SettlementPoint other, double t) => new(X + (other.X-X)*t, Y + (other.Y-Y)*t, Z + (other.Z-Z)*t);
}
public sealed record SettlementStreet(string Id, string Tatar, string Russian, SettlementPoint Origin, string OddSide = "left", bool Protected = false);
public sealed record AddressAlias(string StreetId, string HouseNumber, string Revision);
public sealed record AddressRecord(string AddressId, string BuildingId, string ParcelId, string StreetId, string HouseNumber, string AccessId, IReadOnlyList<AddressAlias> History);
public sealed record SettlementBuilding(string BuildingId, string SourceKey, string ParcelId, string? AddressId, string Role, SettlementPoint Position, IReadOnlyList<SettlementPoint> Footprint);
public sealed record SettlementParcel(string ParcelId, string QuarterId, string GameCadastralId, string PrimaryBuildingId, string AccessId, IReadOnlyList<SettlementPoint> Polygon);
public sealed record SettlementAccess(string AccessId, string BuildingId, string Kind, SettlementPoint Position, string GraphNodeId, string State);
public sealed record SettlementConstraint(string Id, string Kind, IReadOnlyList<SettlementPoint> Boundary, SettlementTravelMode BlockedModes);
public sealed record SettlementIssue(string Code, string EntityId, string Detail);
public sealed record SettlementMap(IReadOnlyList<SettlementGraphNode> Nodes, IReadOnlyList<SettlementGraphEdge> Edges, IReadOnlyList<SettlementBuilding> Buildings, IReadOnlyList<SettlementParcel> Parcels, IReadOnlyList<SettlementAccess> AccessPoints, IReadOnlyList<SettlementConstraint> Constraints, IReadOnlyList<SettlementStreet> KnownStreets);

/// <summary>
/// Immutable entity references and mutable human addresses, imported by the existing
/// connected-world owner. It owns no player knowledge and writes no save files.
/// </summary>
public sealed class SettlementRegistry
{
    public const string VillageName = "КАРА-УРМАН";
    public const string RegistryVersion = "act1-addresses-1";
    public static readonly string[] AllowedSuffixes = ["А","Б","В","Г","Д","Е","Ж","И","К","Л","М","Н","О","П","Р","С","Т","У","Ф","Х","Ц","Ч","Ш","Щ","Э","Ю","Я"];
    private readonly Dictionary<string, SettlementStreet> _streets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementBuilding> _buildings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementParcel> _parcels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AddressRecord> _addresses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementAccess> _access = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _search = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _addressAliases = new(StringComparer.Ordinal);
    private readonly List<SettlementConstraint> _constraints = [];
    public SettlementRoadGraph Graph { get; } = new();
    public IReadOnlyDictionary<string, SettlementStreet> Streets => _streets;
    public IReadOnlyDictionary<string, SettlementBuilding> Buildings => _buildings;
    public IReadOnlyDictionary<string, SettlementParcel> Parcels => _parcels;
    public IReadOnlyDictionary<string, AddressRecord> Addresses => _addresses;
    public IReadOnlyDictionary<string, SettlementAccess> AccessPoints => _access;
    public IReadOnlyDictionary<string, string> AddressAliases => _addressAliases;
    public IReadOnlyList<SettlementConstraint> Constraints => _constraints;

    public static string StableId(string prefix, string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey)) throw new ArgumentException("A committed source key is required.");
        return prefix + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey)))[..16];
    }
    public static string NormalizeNumber(string number)
    {
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("House number cannot be empty.");
        var normalized = string.Concat(number.Normalize(NormalizationForm.FormKC).ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)));
        // Explicitly accept common keyboard lookalikes as aliases, never as different houses.
        foreach (var (latin, cyrillic) in new[] { ('A','А'),('B','В'),('E','Е'),('K','К'),('M','М'),('H','Н'),('O','О'),('P','Р'),('C','С'),('T','Т'),('X','Х') })
            normalized = normalized.Replace(latin, cyrillic);
        if (!Regex.IsMatch(normalized, @"^\d+[А-ЯЁ]?(?:[/.-]\d+)?(?:К\d+)?$"))
            throw new ArgumentException("Unsupported house number: " + number);
        return Regex.Replace(normalized, @"^0+(?=\d)", "");
    }
    private static string SearchKey(string streetId, string number) => streetId + ":" + NormalizeNumber(number);
    /// <summary>Reserve every committed authored number before importing visible
    /// sources. The same index serves assignment and lookup; a retired reservation
    /// has no address/building record and cannot reveal a notebook location.</summary>
    public void ReserveAuthoredAddress(string addressId,string streetId,string number)
    {
        if(string.IsNullOrWhiteSpace(addressId)||!_streets.ContainsKey(streetId))
            throw new InvalidOperationException("Authored reservation requires a stable address and known street.");
        var key=SearchKey(streetId,number);
        if(_search.TryGetValue(key,out var existing)&&existing!=addressId)
            throw new InvalidOperationException("Authored number is already reserved: "+key);
        _search[key]=addressId;
    }
    public void AddStreet(SettlementStreet street)
    {
        if (street.OddSide is not ("left" or "right")) throw new ArgumentException("Street odd side must be fixed.");
        if (_streets.TryGetValue(street.Id, out var existing) && existing != street)
            throw new InvalidOperationException("Street already registered: " + street.Id);
        _streets[street.Id] = street;
    }
    public void AddConstraint(SettlementConstraint constraint)
    {
        if (_constraints.Any(c => c.Id == constraint.Id)) throw new InvalidOperationException("Duplicate constraint " + constraint.Id);
        _constraints.Add(constraint);
    }
    public void Register(SettlementBuilding building, SettlementParcel parcel, AddressRecord? address, SettlementAccess access)
    {
        if (string.IsNullOrWhiteSpace(building.SourceKey) || string.IsNullOrWhiteSpace(building.BuildingId))
            throw new InvalidOperationException("A committed building source and identity are required.");
        if (building.ParcelId != parcel.ParcelId || access.BuildingId != building.BuildingId || parcel.AccessId != access.AccessId || parcel.PrimaryBuildingId != building.BuildingId)
            throw new InvalidOperationException("Building, parcel and access references disagree.");
        _buildings.TryGetValue(building.BuildingId, out var prior);
        if (prior is not null && (prior.SourceKey != building.SourceKey || prior.ParcelId != building.ParcelId || prior.AddressId != building.AddressId))
            throw new InvalidOperationException("Committed entity identity cannot change.");
        if (_buildings.Values.Any(b => b.SourceKey == building.SourceKey && b.BuildingId != building.BuildingId)) throw new InvalidOperationException("Duplicate source key.");
        if (!Regex.IsMatch(parcel.GameCadastralId, @"^URM-Q\d{2}-P\d{4}$")) throw new InvalidOperationException("Invalid fictional parcel id.");
        if (_parcels.Values.Any(p => p.GameCadastralId == parcel.GameCadastralId && p.ParcelId != parcel.ParcelId))
            throw new InvalidOperationException("Duplicate fictional parcel id.");
        if (_parcels.TryGetValue(parcel.ParcelId, out var existingParcel) && (existingParcel.PrimaryBuildingId != parcel.PrimaryBuildingId
            || existingParcel.AccessId != parcel.AccessId || existingParcel.QuarterId != parcel.QuarterId || existingParcel.GameCadastralId != parcel.GameCadastralId))
            throw new InvalidOperationException("Committed parcel and access identity cannot change implicitly.");
        if (_access.TryGetValue(access.AccessId, out var existingAccess) && existingAccess.BuildingId != building.BuildingId)
            throw new InvalidOperationException("Access identity is already owned.");
        if ((building.AddressId is null) != (address is null)) throw new InvalidOperationException("Addressable building must supply its complete address record.");
        string? key = null;
        AddressRecord? normalizedAddress = null;
        if (address is not null)
        {
            if (building.AddressId != address.AddressId || address.BuildingId != building.BuildingId || address.ParcelId != parcel.ParcelId || address.AccessId != access.AccessId)
                throw new InvalidOperationException("Address references disagree.");
            if (!_streets.ContainsKey(address.StreetId)) throw new InvalidOperationException("Unknown street " + address.StreetId);
            key = SearchKey(address.StreetId, address.HouseNumber);
            if (_search.TryGetValue(key, out var other) && other != address.AddressId) throw new InvalidOperationException("Duplicate address " + key);
            normalizedAddress = address with { HouseNumber = NormalizeNumber(address.HouseNumber) };
            if (_addressAliases.ContainsKey(address.AddressId)) throw new InvalidOperationException("Address ID is reserved as a semantic alias.");
            if (_addresses.TryGetValue(address.AddressId, out var old) && (old.BuildingId != address.BuildingId || old.ParcelId != address.ParcelId
                || old.AccessId != address.AccessId || old.StreetId != address.StreetId || old.HouseNumber != normalizedAddress.HouseNumber
                || !old.History.SequenceEqual(address.History)))
                throw new InvalidOperationException("Reimport cannot overwrite committed address history; use an explicit address migration.");
        }
        // Validate the complete import before any mutation, including repeat imports.
        _buildings[building.BuildingId] = building;
        _parcels[parcel.ParcelId] = parcel;
        _access[access.AccessId] = access;
        if (normalizedAddress is not null){_addresses[normalizedAddress.AddressId] = normalizedAddress;_search[key!] = normalizedAddress.AddressId;}
    }
    public void RegisterAuxiliary(SettlementBuilding building)
    {
        if (!_parcels.TryGetValue(building.ParcelId, out var parcel) || !_buildings.TryGetValue(parcel.PrimaryBuildingId, out var main))
            throw new InvalidOperationException("Auxiliary must inherit an existing parcel.");
        if (building.BuildingId == parcel.PrimaryBuildingId) throw new InvalidOperationException("An auxiliary cannot replace its parcel's primary building.");
        if (building.AddressId != main.AddressId) throw new InvalidOperationException("Auxiliary must inherit the parcel address.");
        if (_buildings.TryGetValue(building.BuildingId, out var old) && (old.SourceKey != building.SourceKey || old.ParcelId != building.ParcelId || old.AddressId != building.AddressId))
            throw new InvalidOperationException("Committed auxiliary ownership cannot change.");
        if (_buildings.Values.Any(b => b.SourceKey == building.SourceKey && b.BuildingId != building.BuildingId)) throw new InvalidOperationException("Duplicate auxiliary source key.");
        _buildings[building.BuildingId] = building;
    }
    public void AddAddressAlias(string aliasId, string canonicalAddressId)
    {
        if (string.IsNullOrWhiteSpace(aliasId) || !_addresses.ContainsKey(canonicalAddressId)) throw new InvalidOperationException("Alias must point directly to an imported stable address.");
        if (_addresses.ContainsKey(aliasId) || _buildings.ContainsKey(aliasId)) throw new InvalidOperationException("Alias cannot shadow a stable entity ID.");
        if (_addressAliases.TryGetValue(aliasId,out var old) && old != canonicalAddressId) throw new InvalidOperationException("Committed alias target cannot change.");
        _addressAliases[aliasId] = canonicalAddressId;
    }
    public string? CanonicalAddressId(string id) => TryResolve(id,out var address) ? address.AddressId : null;
    public bool TryResolve(string idOrAlias, out AddressRecord address)
    {
        if (_addressAliases.TryGetValue(idOrAlias,out var canonical)) return _addresses.TryGetValue(canonical,out address!);
        if (_addresses.TryGetValue(idOrAlias, out address!)) return true;
        if (_buildings.TryGetValue(idOrAlias, out var building) && building.AddressId is { } addressId)
            return _addresses.TryGetValue(addressId, out address!);
        address = null!;
        return false;
    }
    public bool TryFind(string streetId, string number, out AddressRecord address)
    {
        address = null!;
        if (_search.TryGetValue(SearchKey(streetId, number), out var id)) return _addresses.TryGetValue(id, out address!);
        var matches = _addresses.Values.Where(a => a.History.Any(h => h.StreetId == streetId && NormalizeNumber(h.HouseNumber) == NormalizeNumber(number))).ToArray();
        if (matches.Length != 1) return false;
        address = matches[0];
        return true;
    }
    public string FormatAddress(string id)
    {
        if (!TryResolve(id, out var record)) return "адрес пока не установлен";
        var street = _streets[record.StreetId];
        return $"{street.Tatar} / {street.Russian}, {record.HouseNumber}";
    }
    public string ResolveText(string text) => Regex.Replace(text, @"\{address:([^}]+)\}", match => FormatAddress(match.Groups[1].Value));
    public void RenameStreet(string streetId, string tatar, string russian)
    {
        var old = _streets[streetId];
        _streets[streetId] = old with { Tatar = tatar, Russian = russian };
    }
    public void ChangeAddress(string addressId, string streetId, string number, string revision)
    {
        var old = _addresses[addressId];
        var normalized = NormalizeNumber(number);
        if (!_streets.ContainsKey(streetId)) throw new ArgumentException("Unknown street.");
        var key = SearchKey(streetId, normalized);
        if (_search.TryGetValue(key, out var other) && other != addressId) throw new InvalidOperationException("Duplicate changed address.");
        _search.Remove(SearchKey(old.StreetId, old.HouseNumber));
        _search[key] = addressId;
        _addresses[addressId] = old with { StreetId = streetId, HouseNumber = normalized, History = old.History.Append(new(old.StreetId, old.HouseNumber, revision)).ToArray() };
    }
    public string InfillNumber(string streetId, string precedingNumber)
    {
        var number = NormalizeNumber(precedingNumber);
        var baseNumber = Regex.Match(number, @"^\d+").Value;
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        foreach(var old in _addresses.Values.SelectMany(a=>a.History).Where(h=>h.StreetId==streetId))
            reserved.Add(NormalizeNumber(old.HouseNumber));
        foreach (var suffix in AllowedSuffixes)
            if (!_search.ContainsKey(SearchKey(streetId, baseNumber + suffix)) && !reserved.Contains(baseNumber + suffix)) return baseNumber + suffix;
        throw new InvalidOperationException("No suffix remains; an explicit authored address is required.");
    }
    public void SetAccessState(string accessId, string state)
    {
        _access[accessId] = _access[accessId] with { State = state };
    }
    public void UpdateAccess(SettlementAccess access)
    {
        if (!_access.TryGetValue(access.AccessId, out var old) || old.BuildingId != access.BuildingId)
            throw new InvalidOperationException("Access identity cannot change.");
        _access[access.AccessId] = access;
    }
    public SettlementMap MapForKnownAddresses(IEnumerable<string> knownAddressIds)
    {
        var known = knownAddressIds.Select(CanonicalAddressId).Where(id => id is not null).ToHashSet(StringComparer.Ordinal);
        var buildings = _buildings.Values.Where(b => b.AddressId is not null && known.Contains(b.AddressId)).OrderBy(b => b.BuildingId).ToArray();
        var parcels = buildings.Select(b => b.ParcelId).ToHashSet();
        var streets = _addresses.Values.Where(a=>known.Contains(a.AddressId)).Select(a=>a.StreetId).ToHashSet(StringComparer.Ordinal);
        // Real streets and paths give a located house its geographical context.
        // A physics audit's computed access path is not the player's knowledge:
        // drawing it would reveal a solution and change the sketch after the audit.
        var edges = Graph.Edges.Values.Where(e=>known.Count>0
            && !e.RoadId.StartsWith("route/",StringComparison.Ordinal)
            && !e.RoadId.StartsWith("access/",StringComparison.Ordinal)).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
        var nodes = edges.SelectMany(e=>new[]{e.A,e.B}).ToHashSet(StringComparer.Ordinal);
        // Nodes and edges are the same objects used by routing, not a second map.
        // Only KnownStreets may supply text labels; context edges can carry the
        // internal identity of a street whose name the player has not learned.
        return new(Graph.Nodes.Values.Where(n=>nodes.Contains(n.Id)).OrderBy(n => n.Id).ToArray(), edges, buildings,
            _parcels.Values.Where(p => parcels.Contains(p.ParcelId)).ToArray(),
            _access.Values.Where(a => buildings.Any(b => b.BuildingId == a.BuildingId)).ToArray(), _constraints.ToArray(),
            _streets.Values.Where(s=>streets.Contains(s.Id)).OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray());
    }
    public IReadOnlyList<SettlementPoint> DiagnosticRoute(string fromNodeId, string addressId, SettlementTravelMode mode, bool winter = true)
    {
        if (!TryResolve(addressId, out var address) || !_access.TryGetValue(address.AccessId, out var access)) return [];
        if (access.State != "verified") return [];
        return Graph.Route(fromNodeId, access.GraphNodeId, mode, winter).Select(id => Graph.Nodes[id].Position).ToArray();
    }
    public IReadOnlyList<SettlementIssue> Validate()
    {
        var issues = new List<SettlementIssue>();
        foreach (var parcel in _parcels.Values)
        {
            var entityId = _buildings[parcel.PrimaryBuildingId].AddressId ?? parcel.ParcelId;
            if (!_access.TryGetValue(parcel.AccessId, out var access)) issues.Add(new("MISSING_ACCESS", entityId, "No entrance/gate record."));
            else if (access.State != "verified") issues.Add(new("ACCESS_NOT_VERIFIED", entityId, access.State));
            else if (!Graph.Nodes.ContainsKey(access.GraphNodeId)) issues.Add(new("ACCESS_GRAPH_MISSING", entityId, access.GraphNodeId));
        }
        var pair = _addresses.Values.Where(a => a.StreetId == "usal" && (a.HouseNumber == "15" || a.HouseNumber == "52")).ToArray();
        if (pair.Length != 2) issues.Add(new("USAL_PAIR_MISSING", "usal", "Both 15 and 52 must be present on an existing forest spur."));
        else if (_buildings[pair[0].BuildingId].Position.DistanceXZ(_buildings[pair[1].BuildingId].Position) > 40)
            issues.Add(new("USAL_PAIR_DISTANCE", "usal", "House 15 and 52 exceed 40 metres."));
        return issues;
    }
}
