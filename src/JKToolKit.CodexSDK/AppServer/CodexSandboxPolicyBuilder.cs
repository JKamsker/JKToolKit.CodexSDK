using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Helper methods for building common app-server sandbox policy objects.
/// </summary>
public static class CodexSandboxPolicyBuilder
{
    /// <summary>
    /// Creates a <see cref="SandboxPolicy.ReadOnly"/> policy with no explicit read-only access override.
    /// </summary>
    public static SandboxPolicy.ReadOnly ReadOnly() => new();

    /// <summary>
    /// Creates a <see cref="SandboxPolicy.ReadOnly"/> policy with full read access override (upstream feature).
    /// </summary>
    public static SandboxPolicy.ReadOnly ReadOnlyFullAccess() =>
        new()
        {
            Access = new ReadOnlyAccess.FullAccess()
        };

    /// <summary>
    /// Creates a <see cref="SandboxPolicy.ReadOnly"/> policy that restricts readable roots (upstream feature).
    /// </summary>
    public static SandboxPolicy.ReadOnly ReadOnlyRestricted(
        IEnumerable<string> readableRoots,
        bool includePlatformDefaults = true) =>
        new()
        {
            Access = new ReadOnlyAccess.Restricted
            {
                IncludePlatformDefaults = includePlatformDefaults,
                ReadableRoots = readableRoots?.ToArray() ?? throw new ArgumentNullException(nameof(readableRoots))
            }
        };

    /// <summary>
    /// Creates a <see cref="SandboxPolicy.WorkspaceWrite"/> policy.
    /// </summary>
    public static SandboxPolicy.WorkspaceWrite WorkspaceWrite(
        IEnumerable<string>? writableRoots = null,
        bool networkAccess = false,
        bool excludeTmpdirEnvVar = false,
        bool excludeSlashTmp = false,
        ReadOnlyAccess? readOnlyAccess = null) =>
        new()
        {
            WritableRoots = writableRoots?.ToArray() ?? Array.Empty<string>(),
            NetworkAccess = networkAccess,
            ExcludeTmpdirEnvVar = excludeTmpdirEnvVar,
            ExcludeSlashTmp = excludeSlashTmp,
            ReadOnlyAccess = readOnlyAccess
        };
}

