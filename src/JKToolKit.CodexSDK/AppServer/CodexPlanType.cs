namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a ChatGPT plan type reported by the app-server.
/// </summary>
public readonly record struct CodexPlanType
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CodexPlanType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plan type cannot be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the <c>free</c> plan.
    /// </summary>
    public static CodexPlanType Free => new("free");

    /// <summary>
    /// Gets the <c>go</c> plan.
    /// </summary>
    public static CodexPlanType Go => new("go");

    /// <summary>
    /// Gets the <c>plus</c> plan.
    /// </summary>
    public static CodexPlanType Plus => new("plus");

    /// <summary>
    /// Gets the <c>pro</c> plan.
    /// </summary>
    public static CodexPlanType Pro => new("pro");

    /// <summary>
    /// Gets the <c>team</c> plan.
    /// </summary>
    public static CodexPlanType Team => new("team");

    /// <summary>
    /// Gets the <c>self_serve_business_usage_based</c> plan.
    /// </summary>
    public static CodexPlanType SelfServeBusinessUsageBased => new("self_serve_business_usage_based");

    /// <summary>
    /// Gets the <c>business</c> plan.
    /// </summary>
    public static CodexPlanType Business => new("business");

    /// <summary>
    /// Gets the <c>enterprise_cbp_usage_based</c> plan.
    /// </summary>
    public static CodexPlanType EnterpriseCbpUsageBased => new("enterprise_cbp_usage_based");

    /// <summary>
    /// Gets the <c>enterprise</c> plan.
    /// </summary>
    public static CodexPlanType Enterprise => new("enterprise");

    /// <summary>
    /// Gets the <c>edu</c> plan.
    /// </summary>
    public static CodexPlanType Edu => new("edu");

    /// <summary>
    /// Gets the <c>unknown</c> plan.
    /// </summary>
    public static CodexPlanType Unknown => new("unknown");

    /// <summary>
    /// Parses a plan type from a wire value.
    /// </summary>
    public static CodexPlanType Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plan type from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CodexPlanType planType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            planType = default;
            return false;
        }

        planType = new CodexPlanType(value);
        return true;
    }

    /// <summary>
    /// Converts a wire string to a <see cref="CodexPlanType"/>.
    /// </summary>
    public static implicit operator CodexPlanType(string value) => Parse(value);

    /// <summary>
    /// Converts a plan type to its wire representation.
    /// </summary>
    public static implicit operator string(CodexPlanType planType) => planType.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
