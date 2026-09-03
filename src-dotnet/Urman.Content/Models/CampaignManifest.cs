using System.Text.Json.Serialization;
using Urman.Core.Contracts;

namespace Urman.Content.Models;

public sealed record ModuleDependency(
    [property: JsonPropertyName("moduleId")] string ModuleId,
    [property: JsonPropertyName("exactVersion")] string ExactVersion);

public sealed record CapabilityRequirement(
    [property: JsonPropertyName("protocolId")] string ProtocolId,
    [property: JsonPropertyName("exactVersion")] string ExactVersion);

public sealed record CampaignManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("exactVersion")] string ExactVersion,
    [property: JsonPropertyName("entrypoint")] ContentId Entrypoint,
    [property: JsonPropertyName("modules")] IReadOnlyList<ModuleDependency> Modules,
    [property: JsonPropertyName("roleBindings")] IReadOnlyDictionary<string, ContentId> RoleBindings,
    [property: JsonPropertyName("capabilityRequirements")] IReadOnlyList<CapabilityRequirement> CapabilityRequirements,
    [property: JsonPropertyName("narrativeOrder")] IReadOnlyList<ContentId> NarrativeOrder);

public sealed record CampaignFingerprint
{
    public const int Sha256Length = 64;

    public CampaignFingerprint(string value)
    {
        if (value.Length != Sha256Length || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Campaign fingerprint must be a SHA-256 hex string.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}

public sealed record CompiledCampaign(
    int SchemaVersion,
    string PackVersion,
    CampaignManifest Campaign,
    CampaignFingerprint Fingerprint);
