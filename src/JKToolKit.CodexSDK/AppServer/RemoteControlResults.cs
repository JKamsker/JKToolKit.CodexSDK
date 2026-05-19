using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a remote-control connection status wire value.
/// </summary>
public readonly record struct RemoteControlConnectionStatus
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private RemoteControlConnectionStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Remote-control connection status cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the disabled status.
    /// </summary>
    public static RemoteControlConnectionStatus Disabled => new("disabled");

    /// <summary>
    /// Gets the connecting status.
    /// </summary>
    public static RemoteControlConnectionStatus Connecting => new("connecting");

    /// <summary>
    /// Gets the connected status.
    /// </summary>
    public static RemoteControlConnectionStatus Connected => new("connected");

    /// <summary>
    /// Gets the errored status.
    /// </summary>
    public static RemoteControlConnectionStatus Errored => new("errored");

    /// <summary>
    /// Parses a remote-control connection status from a wire value.
    /// </summary>
    public static RemoteControlConnectionStatus Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a remote-control connection status from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out RemoteControlConnectionStatus status)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            status = default;
            return false;
        }

        status = new RemoteControlConnectionStatus(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="RemoteControlConnectionStatus"/>.
    /// </summary>
    public static implicit operator RemoteControlConnectionStatus(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="RemoteControlConnectionStatus"/> to its wire value.
    /// </summary>
    public static implicit operator string(RemoteControlConnectionStatus status) => status.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Result returned by remote-control status endpoints.
/// </summary>
public sealed record class RemoteControlStatusResult
{
    /// <summary>
    /// Gets the remote-control connection status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the typed remote-control connection status.
    /// </summary>
    public required RemoteControlConnectionStatus StatusValue { get; init; }

    /// <summary>
    /// Gets the remote-control server name.
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets the remote-control installation identifier.
    /// </summary>
    public required string InstallationId { get; init; }

    /// <summary>
    /// Gets the current remote-control environment id, when available.
    /// </summary>
    public string? EnvironmentId { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
