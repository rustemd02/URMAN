using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urman.Core.Serialization;

public static class CanonicalJson
{
    private static readonly JsonSerializerOptions StringOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(JsonElement element) => Serialize(JsonNode.Parse(element.GetRawText()));

    public static string Serialize(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        return node switch
        {
            JsonObject jsonObject => SerializeObject(jsonObject),
            JsonArray jsonArray => $"[{string.Join(',', jsonArray.Select(Serialize))}]",
            JsonValue jsonValue => SerializeValue(jsonValue),
            _ => throw new InvalidOperationException($"Unsupported JSON node type {node.GetType().Name}.")
        };
    }

    public static string Sha256(JsonNode node)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(node)));
        return Convert.ToHexStringLower(bytes);
    }

    private static string SerializeObject(JsonObject jsonObject)
    {
        var properties = jsonObject
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => $"{JsonSerializer.Serialize(property.Key, StringOptions)}:{Serialize(property.Value)}");
        return $"{{{string.Join(',', properties)}}}";
    }

    private static string SerializeValue(JsonValue value)
    {
        if (value.TryGetValue<string>(out var text))
        {
            return JsonSerializer.Serialize(text, StringOptions);
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean ? "true" : "false";
        }

        if (value.TryGetValue<long>(out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue.ToString("G29", CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return JsonSerializer.Serialize(doubleValue);
        }

        return value.ToJsonString();
    }
}
