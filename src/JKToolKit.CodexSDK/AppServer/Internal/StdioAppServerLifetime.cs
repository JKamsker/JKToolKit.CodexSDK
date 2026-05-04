using JKToolKit.CodexSDK.Infrastructure.Stdio;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class StdioAppServerLifetime : IAppServerLifetime
{
    private readonly IStdioProcess _process;

    public StdioAppServerLifetime(IStdioProcess process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public Task Completion => _process.Completion;

    public int? ProcessId => _process.ProcessId;

    public int? ExitCode => _process.ExitCode;

    public IReadOnlyList<string> DiagnosticTail => _process.StderrTail;

    public ValueTask DisposeAsync() => _process.DisposeAsync();
}
