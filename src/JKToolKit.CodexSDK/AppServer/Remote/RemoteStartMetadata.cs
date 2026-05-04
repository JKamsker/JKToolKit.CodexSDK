namespace JKToolKit.CodexSDK.AppServer;

internal sealed record RemoteStartMetadata(
    string Id,
    int? ProcessId,
    Uri Uri,
    string StateDirectory,
    string PidFile,
    string LogFile)
{
    public static RemoteStartMetadata Parse(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = rawLine.IndexOf('=', StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            values[rawLine[..index]] = rawLine[(index + 1)..];
        }

        var id = Require(values, "CODEXSDK_ID");
        var uri = new Uri(Require(values, "CODEXSDK_URI"));
        return new RemoteStartMetadata(
            id,
            int.TryParse(values.GetValueOrDefault("CODEXSDK_PID"), out var pid) ? pid : null,
            uri,
            Require(values, "CODEXSDK_STATE_DIR"),
            Require(values, "CODEXSDK_PID_FILE"),
            Require(values, "CODEXSDK_LOG_FILE"));
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Remote app-server startup output did not contain {key}.");
}
