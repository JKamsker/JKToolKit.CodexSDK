namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct);

    Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct);

    Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct);

    Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct);

    Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct);

    Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(SkillsConfigWriteOptions options, CancellationToken ct);

    Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct) => _inner.ListSkillsAsync(options, ct);

    public Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct) => _inner.ListAppsAsync(options, ct);

    public Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct) => _inner.ReadConfigRequirementsAsync(ct);

    public Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct) => _inner.ReadRemoteSkillsAsync(ct);

    public Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct) => _inner.WriteRemoteSkillAsync(hazelnutId, isPreload, ct);

    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(SkillsConfigWriteOptions options, CancellationToken ct) => _inner.WriteSkillsConfigAsync(options, ct);

    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct) => _inner.WriteSkillsConfigAsync(enabled, path, ct);
}
