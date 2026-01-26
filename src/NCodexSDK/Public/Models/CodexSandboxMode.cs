namespace NCodexSDK.Public.Models;

/// <summary>
/// Represents a Codex sandbox mode identifier.
/// </summary>
/// <remarks>
/// This value object is forward-compatible: callers may use any string value supported by their Codex version.
/// </remarks>
public readonly record struct CodexSandboxMode
{
    public string Value { get; }

    private CodexSandboxMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SandboxMode cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    public static CodexSandboxMode ReadOnly => new("read-only");
    public static CodexSandboxMode WorkspaceWrite => new("workspace-write");
    public static CodexSandboxMode DangerFullAccess => new("danger-full-access");

    public static CodexSandboxMode Parse(string value) => new(value);

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

    public static implicit operator CodexSandboxMode(string value) => Parse(value);
    public static implicit operator string(CodexSandboxMode mode) => mode.Value;

    public override string ToString() => Value;
}

