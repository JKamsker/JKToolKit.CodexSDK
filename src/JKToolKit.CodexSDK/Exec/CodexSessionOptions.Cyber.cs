using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Exec;

public partial class CodexSessionOptions
{
    /// <summary>
    /// Gets or sets the experimental Cyber access program requested for this turn.
    /// </summary>
    /// <remarks>
    /// When set, the SDK passes <c>--cyber-access-program &lt;program&gt;</c>. Upstream currently supports this
    /// option with the built-in OpenAI provider and does not treat it as a persistent thread setting.
    /// </remarks>
    public CodexCyberAccessProgram? CyberAccessProgram { get; set; }

    private void ValidateCyberAccessProgram()
    {
        if (CyberAccessProgram is { } program && string.IsNullOrWhiteSpace(program.Value))
        {
            throw new InvalidOperationException("CyberAccessProgram cannot be empty or whitespace.");
        }

        if (CyberAccessProgram is not null && _additionalOptions.Any(IsCyberAccessProgramArg))
        {
            throw new InvalidOperationException(
                "Do not specify '--cyber-access-program' in AdditionalOptions when CyberAccessProgram is set.");
        }
    }

    private static bool IsCyberAccessProgramArg(string? arg) =>
        !string.IsNullOrWhiteSpace(arg) &&
        (arg.Equals("--cyber-access-program", StringComparison.OrdinalIgnoreCase) ||
         arg.StartsWith("--cyber-access-program=", StringComparison.OrdinalIgnoreCase));
}
