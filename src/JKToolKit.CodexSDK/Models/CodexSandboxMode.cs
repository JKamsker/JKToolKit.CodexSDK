namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a Codex sandbox mode identifier (the overall execution restriction level for model-generated commands).
/// </summary>
/// <remarks>
/// This value object is forward-compatible: callers may use any string value supported by their Codex version.
/// <para>
/// Known values in Codex include <c>read-only</c>, <c>workspace-write</c>, and <c>danger-full-access</c>.
/// In <c>workspace-write</c> mode, Codex generally allows writes to the working directory plus some temporary
/// directories, while keeping safety-sensitive subpaths such as <c>.git</c>, <c>.codex</c>, and <c>.agents</c>
/// read-only under writable roots.
/// </para>
/// </remarks>
public readonly record struct CodexSandboxMode
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CodexSandboxMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SandboxMode cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>read-only</c> sandbox mode.
    /// </summary>
    public static CodexSandboxMode ReadOnly => new("read-only");

    /// <summary>
    /// Gets the <c>workspace-write</c> sandbox mode.
    /// </summary>
    public static CodexSandboxMode WorkspaceWrite => new("workspace-write");

    /// <summary>
    /// Gets the <c>danger-full-access</c> sandbox mode.
    /// </summary>
    public static CodexSandboxMode DangerFullAccess => new("danger-full-access");

    /// <summary>
    /// Parses a sandbox mode from a wire value.
    /// </summary>
    public static CodexSandboxMode Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a sandbox mode from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CodexSandboxMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = default;
            return false;
        }

        mode = new CodexSandboxMode(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="CodexSandboxMode"/>.
    /// </summary>
    public static implicit operator CodexSandboxMode(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="CodexSandboxMode"/> to its wire value.
    /// </summary>
    public static implicit operator string(CodexSandboxMode mode) => mode.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

