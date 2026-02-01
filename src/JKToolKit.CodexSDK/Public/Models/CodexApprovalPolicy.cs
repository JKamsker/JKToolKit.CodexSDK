namespace JKToolKit.CodexSDK.Public.Models;

/// <summary>
/// Represents a Codex approval policy identifier.
/// </summary>
/// <remarks>
/// This value object is forward-compatible: callers may use any string value supported by their Codex version.
/// </remarks>
public readonly record struct CodexApprovalPolicy
{
    public string Value { get; }

    private CodexApprovalPolicy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ApprovalPolicy cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    public static CodexApprovalPolicy Untrusted => new("untrusted");
    public static CodexApprovalPolicy OnRequest => new("on-request");
    public static CodexApprovalPolicy OnFailure => new("on-failure");
    public static CodexApprovalPolicy Never => new("never");

    public static CodexApprovalPolicy Parse(string value) => new(value);

    public static bool TryParse(string? value, out CodexApprovalPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            policy = default;
            return false;
        }

        policy = new CodexApprovalPolicy(value);
        return true;
    }

    public static implicit operator CodexApprovalPolicy(string value) => Parse(value);
    public static implicit operator string(CodexApprovalPolicy policy) => policy.Value;

    public override string ToString() => Value;
}

