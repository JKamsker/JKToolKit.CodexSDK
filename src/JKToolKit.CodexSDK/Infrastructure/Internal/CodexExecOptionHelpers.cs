namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexExecOptionHelpers
{
    internal static bool RequestsEphemeralSession(IReadOnlyList<string> additionalOptions) =>
        additionalOptions.Any(IsEphemeralOption);

    internal static bool IsEphemeralOption(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        if (arg.Equals("--ephemeral", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return arg.StartsWith("--ephemeral=", StringComparison.OrdinalIgnoreCase);
    }
}
