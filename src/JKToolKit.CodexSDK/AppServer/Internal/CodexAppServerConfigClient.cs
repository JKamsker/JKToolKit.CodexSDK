using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

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

    public async Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "config/read",
            new ConfigReadParams
            {
                IncludeLayers = options.IncludeLayers,
                Cwd = options.Cwd
            },
            ct);

        return CodexAppServerClientConfigReadParsers.ParseConfigReadResult(result);
    }

    public async Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct = default)
    {
        JsonElement result;
        try
        {
            result = await _sendRequestAsync("skills/remote/list", null, ct);
        }
        catch (JsonRpcRemoteException ex) when (IsUnknownVariant(ex, "skills/remote/list"))
        {
            result = await _sendRequestAsync("skills/remote/read", null, ct);
        }

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

        JsonElement result;
        try
        {
            result = await _sendRequestAsync(
                "skills/remote/export",
                new SkillsRemoteWriteParams
                {
                    HazelnutId = hazelnutId,
                    IsPreload = isPreload
                },
                ct);
        }
        catch (JsonRpcRemoteException ex) when (IsUnknownVariant(ex, "skills/remote/export"))
        {
            result = await _sendRequestAsync(
                "skills/remote/write",
                new SkillsRemoteWriteParams
                {
                    HazelnutId = hazelnutId,
                    IsPreload = isPreload
                },
                ct);
        }

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

    private static bool IsUnknownVariant(JsonRpcRemoteException ex, string method)
    {
        if (ex.Error.Code != -32600)
        {
            return false;
        }

        var msg = ex.Error.Message;
        return msg is not null &&
               msg.Contains("unknown variant", StringComparison.OrdinalIgnoreCase) &&
               msg.Contains($"`{method}`", StringComparison.OrdinalIgnoreCase);
    }
}
