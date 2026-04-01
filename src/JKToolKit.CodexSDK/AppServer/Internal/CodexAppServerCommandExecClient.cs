using System.Text.Json;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerCommandExecClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;

    public CodexAppServerCommandExecClient(Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
    }

    public async Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Command.Count == 0)
            throw new ArgumentException("Command cannot be empty.", nameof(options));

        ValidateExecutionOptions(options);
        ValidateStreamingProcessId(options.ProcessId, options.StreamStdin, options.StreamStdoutStderr, options.Tty);

        var result = await _sendRequestAsync(
            "command/exec",
            new UpstreamV2.CommandExecParams
            {
                Command = options.Command.ToArray(),
                Cwd = options.Cwd,
                DisableOutputCap = options.DisableOutputCap,
                DisableTimeout = options.DisableTimeout,
                Env = options.Env is null ? null : new Dictionary<string, string?>(options.Env, StringComparer.Ordinal),
                OutputBytesCap = options.OutputBytesCap,
                ProcessId = options.ProcessId,
                SandboxPolicy = BuildSandboxPolicy(options.SandboxPolicy),
                StreamStdin = options.StreamStdin,
                StreamStdoutStderr = options.StreamStdoutStderr,
                TimeoutMs = options.TimeoutMs,
                Tty = options.Tty,
                Size = options.Size is null
                    ? null
                    : new UpstreamV2.Size
                    {
                        AdditionalProperties =
                        {
                            ["cols"] = options.Size.Columns,
                            ["rows"] = options.Size.Rows
                        }
                    }
            },
            ct).ConfigureAwait(false);

        return new CommandExecResult
        {
            ExitCode = CodexAppServerClientJson.GetRequiredInt32(result, "exitCode", "command/exec response"),
            Stdout = CodexAppServerClientJson.GetRequiredString(result, "stdout", "command/exec response"),
            Stderr = CodexAppServerClientJson.GetRequiredString(result, "stderr", "command/exec response"),
            Raw = result
        };
    }

    public async Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));
        if (options.DeltaBase64 is null && options.CloseStdin != true)
            throw new ArgumentException("command/exec/write requires deltaBase64 or closeStdin.", nameof(options));

        var result = await _sendRequestAsync(
            "command/exec/write",
            new UpstreamV2.CommandExecWriteParams
            {
                ProcessId = options.ProcessId,
                DeltaBase64 = options.DeltaBase64,
                CloseStdin = options.CloseStdin == true
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "command/exec/write response");
        return new CommandExecWriteResult { Raw = result };
    }

    public async Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));
        ArgumentNullException.ThrowIfNull(options.Size);
        ValidateTerminalSize(options.Size);

        var result = await _sendRequestAsync(
            "command/exec/resize",
            new UpstreamV2.CommandExecResizeParams
            {
                ProcessId = options.ProcessId,
                Size = new UpstreamV2.CommandExecTerminalSize
                {
                    Cols = options.Size.Columns,
                    Rows = options.Size.Rows
                }
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "command/exec/resize response");
        return new CommandExecResizeResult { Raw = result };
    }

    public async Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "command/exec/terminate",
            new UpstreamV2.CommandExecTerminateParams
            {
                ProcessId = options.ProcessId
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "command/exec/terminate response");
        return new CommandExecTerminateResult { Raw = result };
    }

    private static void ValidateExecutionOptions(CommandExecOptions options)
    {
        if (options.DisableOutputCap == true && options.OutputBytesCap.HasValue)
        {
            throw new ArgumentException("DisableOutputCap cannot be combined with OutputBytesCap.", nameof(options));
        }

        if (options.DisableTimeout == true && options.TimeoutMs.HasValue)
        {
            throw new ArgumentException("DisableTimeout cannot be combined with TimeoutMs.", nameof(options));
        }

        if (options.Size is not null && options.Tty != true)
        {
            throw new ArgumentException("Size requires tty to be enabled.", nameof(options));
        }
        if (options.Size is not null)
        {
            ValidateTerminalSize(options.Size);
        }
        if (options.TimeoutMs.HasValue && options.TimeoutMs.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TimeoutMs), "TimeoutMs cannot be negative.");
        }

        if (options.OutputBytesCap.HasValue && options.OutputBytesCap.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.OutputBytesCap), "OutputBytesCap cannot be negative.");
        }
    }

    private static void ValidateTerminalSize(CommandExecTerminalSize size)
    {
        if (size.Rows == 0 || size.Columns == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "command/exec size rows and cols must be greater than 0.");
        }
    }

    private static void ValidateStreamingProcessId(
        string? processId,
        bool? streamStdin,
        bool? streamStdoutStderr,
        bool? tty)
    {
        var needsProcessId = streamStdin == true || streamStdoutStderr == true || tty == true;
        if (needsProcessId && string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException(
                "ProcessId is required when streamStdin, streamStdoutStderr, or tty is enabled.",
                nameof(processId));
        }
    }

    private static UpstreamV2.SandboxPolicy2? BuildSandboxPolicy(Protocol.SandboxPolicy.SandboxPolicy? sandboxPolicy)
    {
        if (sandboxPolicy is null)
        {
            return null;
        }

        var serializerOptions = CodexAppServerClient.CreateDefaultSerializerOptions();
        var payload = JsonSerializer.SerializeToElement(sandboxPolicy, serializerOptions);
        return payload.Deserialize<UpstreamV2.SandboxPolicy2>(serializerOptions);
    }

    private static void EnsureObjectResponse(JsonElement result, string context)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{context} must be a JSON object.");
        }
    }
}
