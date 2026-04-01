using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

/// <summary>
/// Represents the outbound network access mode used by <see cref="SandboxPolicy.ExternalSandbox"/>.
/// </summary>
public readonly record struct SandboxNetworkAccess
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private SandboxNetworkAccess(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Sandbox network access value cannot be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the network mode that restricts outbound access.
    /// </summary>
    public static SandboxNetworkAccess Restricted => new("restricted");

    /// <summary>
    /// Gets the network mode that enables outbound access.
    /// </summary>
    public static SandboxNetworkAccess Enabled => new("enabled");

    /// <summary>
    /// Parses a sandbox network access mode from a wire value.
    /// </summary>
    public static SandboxNetworkAccess Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a sandbox network access mode from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out SandboxNetworkAccess networkAccess)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            networkAccess = default;
            return false;
        }

        networkAccess = new SandboxNetworkAccess(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="SandboxNetworkAccess"/>.
    /// </summary>
    public static implicit operator SandboxNetworkAccess(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="SandboxNetworkAccess"/> to its wire value.
    /// </summary>
    public static implicit operator string(SandboxNetworkAccess networkAccess) => networkAccess.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// JSON converter for <see cref="SandboxNetworkAccess"/> wire values.
/// </summary>
public sealed class SandboxNetworkAccessJsonConverter : JsonConverter<SandboxNetworkAccess>
{
    /// <inheritdoc />
    public override SandboxNetworkAccess Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("SandboxNetworkAccess must be a JSON string.");
        }

        var value = reader.GetString();
        if (!SandboxNetworkAccess.TryParse(value, out var networkAccess))
        {
            throw new JsonException("SandboxNetworkAccess value cannot be null or whitespace.");
        }

        return networkAccess;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SandboxNetworkAccess value, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(value.Value))
        {
            throw new JsonException("SandboxNetworkAccess value cannot be empty or whitespace.");
        }

        writer.WriteStringValue(value.Value);
    }
}
