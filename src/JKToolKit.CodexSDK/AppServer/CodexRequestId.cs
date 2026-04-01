using System.Globalization;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents an app-server request identifier, which upstream emits as either a string or an integer.
/// </summary>
public sealed class CodexRequestId
{
    /// <summary>
    /// Gets the raw JSON value for the request identifier.
    /// </summary>
    public JsonElement Raw { get; }

    /// <summary>
    /// Gets the string request identifier, when the upstream payload used a string id.
    /// </summary>
    public string? StringValue { get; }

    /// <summary>
    /// Gets the integer request identifier, when the upstream payload used a numeric id.
    /// </summary>
    public long? IntegerValue { get; }

    /// <summary>
    /// Gets the request identifier as text.
    /// </summary>
    public string ValueText =>
        StringValue ??
        IntegerValue?.ToString(CultureInfo.InvariantCulture) ??
        Raw.GetRawText();

    private CodexRequestId(JsonElement raw, string? stringValue, long? integerValue)
    {
        Raw = raw;
        StringValue = stringValue;
        IntegerValue = integerValue;
    }

    /// <summary>
    /// Returns the request identifier as text.
    /// </summary>
    public override string ToString() => ValueText;

    internal static bool TryParse(JsonElement element, out CodexRequestId? requestId)
    {
        requestId = null;

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                requestId = new CodexRequestId(element.Clone(), element.GetString(), integerValue: null);
                return true;

            case JsonValueKind.Number when element.TryGetInt64(out var integerValue):
                requestId = new CodexRequestId(element.Clone(), stringValue: null, integerValue);
                return true;

            default:
                return false;
        }
    }
}
