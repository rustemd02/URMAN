using System.Text.Json.Nodes;

namespace Urman.Studio.Core.Editing;

/// <summary>
/// Safe reads of authored JSON. A cast such as (string?)node["field"] THROWS when the node holds an
/// object or an array instead of a value, and that crash reached my projections three times before
/// it was named as a pattern: a projection must REPORT bad content, not take Studio down with it.
///
/// Two shapes, because the callers want different things: Text answers "the field as a string, or
/// the empty string", and TextOrNull answers "the field as a string, or null when it is not one".
/// </summary>
public static class AuthoredJson
{
    /// <summary>The field as a string, or the empty string when it is missing or not a string.</summary>
    public static string Text(JsonNode? node, string field) =>
        node is JsonObject obj && obj[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text ?? "" : "";

    /// <summary>The field as a string, or null when it is missing or not a string.</summary>
    public static string? TextOrNull(JsonNode? node, string field) =>
        node is JsonObject obj && obj[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The field as an array, or null when it is missing or not an array.</summary>
    public static JsonArray? Array(JsonNode? node, string field) =>
        node is JsonObject obj && obj[field] is JsonArray array ? array : null;

    /// <summary>The field as an object, or null when it is missing or not an object.</summary>
    public static JsonObject? Object(JsonNode? node, string field) =>
        node is JsonObject obj && obj[field] is JsonObject value ? value : null;
}
