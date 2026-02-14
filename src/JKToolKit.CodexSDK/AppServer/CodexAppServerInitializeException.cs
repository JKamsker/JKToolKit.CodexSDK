using System.Linq;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Thrown when the app-server initialization handshake fails.
/// </summary>
public sealed class CodexAppServerInitializeException : InvalidOperationException
{
    /// <summary>
    /// Gets the JSON-RPC error code.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// Gets the JSON-RPC error message.
    /// </summary>
    public string RemoteMessage { get; }

    /// <summary>
    /// Gets the JSON-RPC error <c>data</c> payload as raw JSON, when present.
    /// </summary>
    public string? DataJson { get; }

    /// <summary>
    /// Gets an optional help message suffix.
    /// </summary>
    public string? Help { get; }

    /// <summary>
    /// Gets a best-effort stderr tail from the app-server process, when available.
    /// </summary>
    public IReadOnlyList<string>? StderrTail { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexAppServerInitializeException"/> class.
    /// </summary>
    public CodexAppServerInitializeException(
        int code,
        string remoteMessage,
        string? dataJson,
        string? help,
        IReadOnlyList<string>? stderrTail,
        Exception innerException)
        : base(BuildMessage(code, remoteMessage, dataJson, help, stderrTail), innerException)
    {
        Code = code;
        RemoteMessage = remoteMessage;
        DataJson = dataJson;
        Help = help;
        StderrTail = stderrTail;
    }

    private static string BuildMessage(
        int code,
        string remoteMessage,
        string? dataJson,
        string? help,
        IReadOnlyList<string>? stderrTail)
    {
        var msg = $"{code}: {remoteMessage}";

        if (!string.IsNullOrWhiteSpace(dataJson))
        {
            msg += $" Data: {dataJson}";
        }

        if (!string.IsNullOrWhiteSpace(help))
        {
            msg += $" {help}";
        }

        if (stderrTail is { Count: > 0 })
        {
            var tail = string.Join(" | ", stderrTail.TakeLast(5));
            msg += $" (stderr tail: {tail})";
        }

        return msg;
    }
}
