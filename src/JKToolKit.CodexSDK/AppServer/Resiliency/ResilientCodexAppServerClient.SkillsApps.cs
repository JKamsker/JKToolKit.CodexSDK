#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.ListSkillsAsync(options, token), ct);

    public Task<SkillsExtraRootsSetResult> SetSkillsExtraRootsAsync(SkillsExtraRootsSetOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.SetSkillsExtraRootsAsync(options, token), ct);

    public Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.ListAppsAsync(options, token), ct);

    public Task<AppsReadResult> ReadAppsAsync(AppsReadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.ReadAppsAsync(options, token), ct);

    public Task<AppsInstalledResult> ReadInstalledAppsAsync(AppsInstalledOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.ReadInstalledAppsAsync(options, token), ct);

    public Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.ReadConfigRequirementsAsync(token), ct);

    public Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.ReadRemoteSkillsAsync(token), ct);

    public Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.WriteRemoteSkillAsync(hazelnutId, isPreload, token), ct);

    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(SkillsConfigWriteOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.WriteSkillsConfigAsync(options, token), ct);

    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.SkillsAndApps, (c, token) => c.WriteSkillsConfigAsync(enabled, path, token), ct);
}

#pragma warning restore CS1591
