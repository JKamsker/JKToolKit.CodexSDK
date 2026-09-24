namespace JKToolKit.CodexSDK.Infrastructure.Stdio;

internal sealed record ProcessLaunchOptions
{
    public required string ResolvedFileName { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }

    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    public TimeSpan StartupTimeout { get; init; } = ProcessDefaults.StartupTimeout;
    public TimeSpan ShutdownTimeout { get; init; } = ProcessDefaults.ShutdownTimeout;
}

