using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>Executable preflight and optional connected-server diagnostics.</summary>
public sealed record CodexRuntimeInfo
{
    /// <summary>Gets the resolved local executable, or null for a remote/custom launch.</summary>
    public string? ExecutablePath { get; init; }
    /// <summary>Gets the actual CLI version token from --version, if available.</summary>
    public string? ActualVersion { get; init; }
    /// <summary>Gets the upstream version used to generate this SDK.</summary>
    public required string ExpectedVersion { get; init; }
    /// <summary>Gets whether the CLI version exactly matches the SDK pin; null means unknown.</summary>
    public bool? IsVersionMatch => ActualVersion is null ? null : ActualVersion == ExpectedVersion;
    /// <summary>Gets the initialized server metadata, if requested and available.</summary>
    public AppServerInitializeResult? Initialize { get; init; }
    /// <summary>Gets server-advertised capabilities, if present. Null means not advertised, not unsupported.</summary>
    public JsonElement? Capabilities { get; init; }
    /// <summary>Gets account/authentication status when available; this does not initiate login or refresh tokens.</summary>
    public AccountReadResult? Account { get; init; }
    /// <summary>Gets diagnostic messages for mismatches or unavailable probes.</summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}
