namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Identifies an execution environment and its working directory for thread or turn startup.
/// </summary>
public sealed record class TurnEnvironmentOptions
{
    /// <summary>
    /// Gets the upstream environment identifier.
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Gets the environment-native working directory.
    /// </summary>
    public required string Cwd { get; init; }
}
