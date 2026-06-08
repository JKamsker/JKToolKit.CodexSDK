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

/// <summary>
/// Options for <c>remoteControl/pairing/start</c>.
/// </summary>
public sealed class RemoteControlPairingStartOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the server should return a manual pairing code.
    /// </summary>
    public bool ManualCode { get; set; }
}

/// <summary>
/// Result returned by <c>remoteControl/pairing/start</c>.
/// </summary>
public sealed record class RemoteControlPairingStartResult
{
    /// <summary>
    /// Gets the pairing code.
    /// </summary>
    public required string PairingCode { get; init; }

    /// <summary>
    /// Gets the optional manual pairing code.
    /// </summary>
    public string? ManualPairingCode { get; init; }

    /// <summary>
    /// Gets the environment id associated with the pairing.
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Gets the Unix timestamp, in seconds, when the pairing code expires.
    /// </summary>
    public long ExpiresAt { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents remote-control client list ordering.
/// </summary>
public readonly record struct RemoteControlClientsListOrder
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private RemoteControlClientsListOrder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Remote-control client list order cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets ascending order.
    /// </summary>
    public static RemoteControlClientsListOrder Asc => new("asc");

    /// <summary>
    /// Gets descending order.
    /// </summary>
    public static RemoteControlClientsListOrder Desc => new("desc");

    /// <summary>
    /// Parses an order from a wire value.
    /// </summary>
    public static RemoteControlClientsListOrder Parse(string value) => new(value);

    /// <summary>
    /// Converts a string to a <see cref="RemoteControlClientsListOrder"/>.
    /// </summary>
    public static implicit operator RemoteControlClientsListOrder(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="RemoteControlClientsListOrder"/> to its wire value.
    /// </summary>
    public static implicit operator string(RemoteControlClientsListOrder order) => order.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Options for <c>remoteControl/client/list</c>.
/// </summary>
public sealed class RemoteControlClientsListOptions
{
    /// <summary>
    /// Gets or sets the remote-control environment id.
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Gets or sets an optional pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets optional result ordering.
    /// </summary>
    public RemoteControlClientsListOrder? Order { get; set; }
}

/// <summary>
/// A remote-control client grant.
/// </summary>
public sealed record class RemoteControlClientInfo
{
    /// <summary>
    /// Gets the remote-control client id.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the optional display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the optional device type.
    /// </summary>
    public string? DeviceType { get; init; }

    /// <summary>
    /// Gets the optional platform.
    /// </summary>
    public string? Platform { get; init; }

    /// <summary>
    /// Gets the optional OS version.
    /// </summary>
    public string? OsVersion { get; init; }

    /// <summary>
    /// Gets the optional device model.
    /// </summary>
    public string? DeviceModel { get; init; }

    /// <summary>
    /// Gets the optional app version.
    /// </summary>
    public string? AppVersion { get; init; }

    /// <summary>
    /// Gets the optional last-seen Unix timestamp, in seconds.
    /// </summary>
    public long? LastSeenAt { get; init; }

    /// <summary>
    /// Gets the raw client payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>remoteControl/client/list</c>.
/// </summary>
public sealed record class RemoteControlClientsListResult
{
    /// <summary>
    /// Gets the returned remote-control clients.
    /// </summary>
    public required IReadOnlyList<RemoteControlClientInfo> Clients { get; init; }

    /// <summary>
    /// Gets the optional next-page cursor.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>remoteControl/client/revoke</c>.
/// </summary>
public sealed class RemoteControlClientsRevokeOptions
{
    /// <summary>
    /// Gets or sets the remote-control environment id.
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Gets or sets the remote-control client id to revoke.
    /// </summary>
    public required string ClientId { get; set; }
}

/// <summary>
/// Result returned by <c>remoteControl/client/revoke</c>.
/// </summary>
public sealed record class RemoteControlClientsRevokeResult
{
    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
