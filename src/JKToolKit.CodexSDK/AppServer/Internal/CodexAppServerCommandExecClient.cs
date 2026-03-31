using System.Text.Json;

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

        ValidateStreamingProcessId(options.ProcessId, options.StreamStdin, options.StreamStdoutStderr, options.Tty);

        var result = await _sendRequestAsync(
            "command/exec",
            new
            {
                command = options.Command,
                cwd = options.Cwd,
                disableOutputCap = options.DisableOutputCap,
                disableTimeout = options.DisableTimeout,
                env = options.Env,
                outputBytesCap = options.OutputBytesCap,
                processId = options.ProcessId,
                sandboxPolicy = options.SandboxPolicy,
                size = options.Size is null ? null : new { cols = options.Size.Columns, rows = options.Size.Rows },
                streamStdin = options.StreamStdin,
                streamStdoutStderr = options.StreamStdoutStderr,
                timeoutMs = options.TimeoutMs,
                tty = options.Tty
            },
            ct);

        return new CommandExecResult
        {
            ExitCode = CodexAppServerClientJson.GetInt32OrNull(result, "exitCode") ?? 0,
            Stdout = CodexAppServerClientJson.GetStringOrNull(result, "stdout") ?? string.Empty,
            Stderr = CodexAppServerClientJson.GetStringOrNull(result, "stderr") ?? string.Empty,
            Raw = result
        };
    }

    public async Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "command/exec/write",
            new
            {
                processId = options.ProcessId,
                deltaBase64 = options.DeltaBase64,
                closeStdin = options.CloseStdin
            },
            ct);

        return new CommandExecWriteResult { Raw = result };
    }

    public async Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));
        ArgumentNullException.ThrowIfNull(options.Size);

        var result = await _sendRequestAsync(
            "command/exec/resize",
            new
            {
                processId = options.ProcessId,
                size = new { cols = options.Size.Columns, rows = options.Size.Rows }
            },
            ct);

        return new CommandExecResizeResult { Raw = result };
    }

    public async Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "command/exec/terminate",
            new
            {
                processId = options.ProcessId
            },
            ct);

        return new CommandExecTerminateResult { Raw = result };
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
}
