using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>environment/add</c>.
/// </summary>
public sealed class EnvironmentAddOptions
{
    /// <summary>
    /// Gets or sets the environment identifier.
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Gets or sets the execution server URL for the environment.
    /// </summary>
    public required string ExecServerUrl { get; set; }

    /// <summary>
    /// Gets or sets the optional bearer token used to authenticate with the execution server.
    /// </summary>
    /// <remarks>
    /// Upstream requires a secure transport or a loopback destination when this value is supplied.
    /// </remarks>
    public string? AuthBearerToken { get; set; }
}

/// <summary>
/// Result returned by <c>environment/add</c>.
/// </summary>
public sealed record class EnvironmentAddResult
{
    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
