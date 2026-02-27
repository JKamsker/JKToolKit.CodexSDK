using System.IO;

namespace JKToolKit.CodexSDK.StructuredOutputs.Internal;

internal static class StructuredOutputFileUtilities
{
    internal static long TryGetLogByteOffset(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
            return 0;

        try
        {
            var info = new FileInfo(logPath);
            return info.Exists ? info.Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}
