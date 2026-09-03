using System.Text.RegularExpressions;

namespace Urman.Core.Contracts;

public readonly record struct ContentId
{
    private static readonly Regex Pattern = new(
        "^[a-z][a-z0-9.-]*:[a-z][a-z0-9.-]*/[a-z0-9._-]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public ContentId(string value)
    {
        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException($"Invalid content ID: {value}", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorldLocationId
{
    public WorldLocationId(string value) => Value = IdentifierRules.RequireLogicalId(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct SpawnPointId
{
    public SpawnPointId(string value) => Value = IdentifierRules.RequireLogicalId(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct InteractionId
{
    public InteractionId(string value) => Value = IdentifierRules.RequireLogicalId(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct WorldStateVariantId
{
    public WorldStateVariantId(string value) => Value = IdentifierRules.RequireLogicalId(value, nameof(value));
    public string Value { get; }
    public override string ToString() => Value;
}

internal static class IdentifierRules
{
    private static readonly Regex LogicalPattern = new(
        "^[a-z][a-z0-9._-]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string RequireLogicalId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || !LogicalPattern.IsMatch(value))
        {
            throw new ArgumentException($"Invalid logical ID: {value}", parameterName);
        }

        return value;
    }
}

