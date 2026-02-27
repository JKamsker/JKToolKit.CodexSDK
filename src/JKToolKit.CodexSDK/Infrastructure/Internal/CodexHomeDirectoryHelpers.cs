using System;
using System.IO;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexHomeDirectoryHelpers
{
    internal static void EnsureExists(string codexHomeDirectory)
    {
        if (string.IsNullOrWhiteSpace(codexHomeDirectory))
            return;

        try
        {
            Directory.CreateDirectory(codexHomeDirectory);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create or access CODEX_HOME directory '{codexHomeDirectory}'.",
                ex);
        }
    }
}
