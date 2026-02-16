using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerConfigClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<bool> _experimentalApiEnabled;

    public CodexAppServerConfigClient(
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<bool> experimentalApiEnabled)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _experimentalApiEnabled = experimentalApiEnabled ?? throw new ArgumentNullException(nameof(experimentalApiEnabled));
    }

    public async Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct = default)
    {
        var result = await _sendRequestAsync(
            "configRequirements/read",
            null,
            ct);

        return new ConfigRequirementsReadResult
        {
            Requirements = CodexAppServerClientConfigRequirementsParser.ParseConfigRequirementsReadRequirements(result, experimentalApiEnabled: _experimentalApiEnabled()),
            Raw = result
        };
    }

    public async Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct = default)
    {
        var result = await _sendRequestAsync(
            "skills/remote/read",
            null,
            ct);

        return new RemoteSkillsReadResult
        {
            Skills = CodexAppServerClientSkillsAppsParsers.ParseRemoteSkillsReadSkills(result),
            Raw = result
        };
    }

    public async Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hazelnutId))
            throw new ArgumentException("HazelnutId cannot be empty or whitespace.", nameof(hazelnutId));

        var result = await _sendRequestAsync(
            "skills/remote/write",
            new SkillsRemoteWriteParams
            {
                HazelnutId = hazelnutId,
                IsPreload = isPreload
            },
            ct);

        return new RemoteSkillWriteResult
        {
            Id = CodexAppServerClientJson.GetStringOrNull(result, "id"),
            Name = CodexAppServerClientJson.GetStringOrNull(result, "name"),
            Path = CodexAppServerClientJson.GetStringOrNull(result, "path"),
            Raw = result
        };
    }

    public async Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty or whitespace.", nameof(path));

        var result = await _sendRequestAsync(
            "skills/config/write",
            new SkillsConfigWriteParams
            {
                Enabled = enabled,
                Path = path
            },
            ct);

        return new SkillsConfigWriteResult
        {
            EffectiveEnabled = CodexAppServerClientJson.GetBoolOrNull(result, "effectiveEnabled"),
            Raw = result
        };
    }
}
