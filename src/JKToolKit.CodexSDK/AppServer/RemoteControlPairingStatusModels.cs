using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>remoteControl/pairing/status</c>.
/// </summary>
public sealed class RemoteControlPairingStatusOptions
{
    /// <summary>
    /// Gets or sets the pairing code to check.
    /// </summary>
    public string? PairingCode { get; set; }

    /// <summary>
    /// Gets or sets the manual pairing code to check.
    /// </summary>
    public string? ManualPairingCode { get; set; }
}

/// <summary>
/// Result returned by <c>remoteControl/pairing/status</c>.
/// </summary>
public sealed record class RemoteControlPairingStatusResult
{
    /// <summary>
    /// Gets a value indicating whether the pairing code has been claimed.
    /// </summary>
    public bool Claimed { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
