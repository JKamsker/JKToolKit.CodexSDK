using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a JSON-RPC error returned by the Codex app-server for a specific request.
/// </summary>
public sealed class CodexAppServerRequestFailedException : InvalidOperationException
{
    /// <summary>
    /// Gets the JSON-RPC method name (for example, <c>turn/steer</c>).
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// Gets the JSON-RPC error code.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Gets the JSON-RPC error message.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the JSON-RPC error data payload, when present.
    /// </summary>
    public JsonElement? ErrorData { get; }

    /// <summary>
    /// Gets the server user agent string observed at initialization time, when available.
    /// </summary>
    public string? UserAgent { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CodexAppServerRequestFailedException"/>.
    /// </summary>
    public CodexAppServerRequestFailedException(
        string method,
        int errorCode,
        string errorMessage,
        JsonElement? errorData,
        string? userAgent,
        Exception? innerException = null)
        : base($"App-server request '{method}' failed: {errorCode}: {errorMessage}", innerException)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        ErrorCode = errorCode;
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        UserAgent = userAgent;

        if (errorData is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } d)
        {
            ErrorData = d.Clone();
        }
    }
}

