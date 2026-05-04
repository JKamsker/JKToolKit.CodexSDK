namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class InternalShellQuoting
{
    public static string Quote(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
