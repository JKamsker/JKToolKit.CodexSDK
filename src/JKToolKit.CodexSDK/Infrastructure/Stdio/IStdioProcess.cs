namespace JKToolKit.CodexSDK.Infrastructure.Stdio;

internal interface IStdioProcess : IAsyncDisposable
{
    Task Completion { get; }

    int? ProcessId { get; }

    int? ExitCode { get; }

    IReadOnlyList<string> StderrTail { get; }
}

