using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JKToolKit.CodexSDK.Exec;

/// <summary>
/// Describes how to launch a long-running Codex subprocess over stdio (app-server / mcp-server).
/// </summary>
/// <remarks>
/// When <see cref="FileName"/> is null, the SDK resolves the Codex executable via <c>ICodexPathProvider</c>.
/// To launch via a prefix (e.g. <c>npx -y codex</c>), set <see cref="FileName"/> to <c>npx</c> and include
/// <c>-y</c>, <c>codex</c>, and subcommand args in <see cref="Arguments"/>.
/// </remarks>
public sealed record CodexLaunch
{
    /// <summary>
    /// Gets the process file name. When null, uses the resolved Codex executable path.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Gets the process arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the working directory for the process.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Gets the environment variables to set for the process.
    /// </summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    /// <summary>
    /// Creates a launch configuration that resolves the Codex executable from PATH/default locations.
    /// </summary>
    public static CodexLaunch CodexOnPath() => new();

    /// <summary>
    /// Creates a launch configuration with an explicit file name (e.g. "npx", "codex").
    /// </summary>
    public static CodexLaunch FromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName cannot be empty or whitespace.", nameof(fileName));

        return new CodexLaunch { FileName = fileName };
    }

    /// <summary>
    /// Returns a copy with the provided args appended to <see cref="Arguments"/>.
    /// </summary>
    public CodexLaunch WithArgs(params string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0) return this;

        return this with { Arguments = Arguments.Concat(args).ToArray() };
    }

    /// <summary>
    /// Returns a copy with the working directory set.
    /// </summary>
    public CodexLaunch WithWorkingDirectory(string? workingDirectory) =>
        this with { WorkingDirectory = workingDirectory };

    /// <summary>
    /// Returns a copy with an environment variable set.
    /// </summary>
    public CodexLaunch WithEnvironment(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Environment key cannot be empty or whitespace.", nameof(key));

        var env = new Dictionary<string, string>(Environment);
        env[key] = value;

        return this with { Environment = new ReadOnlyDictionary<string, string>(env) };
    }
}

