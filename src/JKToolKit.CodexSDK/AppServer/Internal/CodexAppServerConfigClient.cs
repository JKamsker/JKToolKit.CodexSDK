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

        var writeParams = new SkillsRemoteWriteParams
        {
            HazelnutId = hazelnutId,
            IsPreload = isPreload
        };

        JsonElement result;
        try
        {
            result = await _sendRequestAsync(
                "skills/remote/export",
                writeParams,
                ct);
        }
        catch (JsonRpcRemoteException ex) when (IsUnknownVariant(ex, "skills/remote/export"))
        {
            result = await _sendRequestAsync(
                "skills/remote/write",
                writeParams,
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

    public async Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "externalAgentConfig/detect",
            options,
            ct);

        var itemsArray = CodexAppServerClientJson.TryGetArray(result, "items");
        if (itemsArray is null)
        {
            return new ExternalAgentConfigDetectResult
            {
                Items = Array.Empty<ExternalAgentConfigMigrationItem>(),
                Raw = result
            };
        }

        var items = new List<ExternalAgentConfigMigrationItem>();
        foreach (var item in itemsArray.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var description = CodexAppServerClientJson.GetStringOrNull(item, "description");
            var itemType = CodexAppServerClientJson.GetStringOrNull(item, "itemType");
            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(itemType))
            {
                continue;
            }

            items.Add(new ExternalAgentConfigMigrationItem
            {
                Cwd = CodexAppServerClientJson.GetStringOrNull(item, "cwd"),
                Description = description,
                ItemType = itemType
            });
        }

        return new ExternalAgentConfigDetectResult
        {
            Items = items,
            Raw = result
        };
    }

    public async Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(migrationItems);

        _ = await _sendRequestAsync(
            "externalAgentConfig/import",
            new { migrationItems },
            ct);
    }

    public async Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(mode))
            throw new ArgumentException("Mode cannot be empty or whitespace.", nameof(mode));

        var result = await _sendRequestAsync(
            "windowsSandbox/setupStart",
            new { mode },
            ct);

        var started = CodexAppServerClientJson.GetBoolOrNull(result, "started");
        if (started is null)
        {
            throw new InvalidOperationException(
                $"windowsSandbox/setupStart returned no 'started' field. Raw result: {result}");
        }

        return started.Value;
    }

    private static bool IsUnknownVariant(JsonRpcRemoteException ex, string method)
    {
        var error = ex.Error;
        if (error is null)
        {
            return false;
        }

        if (error.Code == -32601)
        {
            return true;
        }

        if (error.Code != -32600)
        {
            return false;
        }

        var msg = error.Message;
        if (string.IsNullOrWhiteSpace(msg))
        {
            return false;
        }

        if (!msg.Contains("unknown variant", StringComparison.OrdinalIgnoreCase) &&
            !msg.Contains("unhandled server request", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(method) || msg.Contains(method, StringComparison.OrdinalIgnoreCase);
    }
}
