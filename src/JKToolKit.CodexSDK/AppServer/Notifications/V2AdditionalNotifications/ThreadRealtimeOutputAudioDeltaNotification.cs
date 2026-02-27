using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// EXPERIMENTAL - notification emitted when thread realtime streams additional output audio.
/// </summary>
public sealed record class ThreadRealtimeOutputAudioDeltaNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the raw audio chunk payload.
    /// </summary>
    public JsonElement Audio { get; }

    /// <summary>
    /// Gets the audio data chunk (typically base64-encoded), if present in <see cref="Audio"/>.
    /// </summary>
    public string? Data { get; }

    /// <summary>
    /// Gets the sample rate, if present in <see cref="Audio"/>.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of channels, if present in <see cref="Audio"/>.
    /// </summary>
    public int NumChannels { get; }

    /// <summary>
    /// Gets samples per channel, if present in <see cref="Audio"/>.
    /// </summary>
    public int? SamplesPerChannel { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadRealtimeOutputAudioDeltaNotification"/>.
    /// </summary>
    public ThreadRealtimeOutputAudioDeltaNotification(string ThreadId, JsonElement Audio, JsonElement Params)
        : base("thread/realtime/outputAudio/delta", Params)
    {
        this.ThreadId = ThreadId;
        this.Audio = Audio;
        Data = TryGetString(Audio, "data");
        SampleRate = TryGetInt32(Audio, "sampleRate");
        NumChannels = TryGetInt32(Audio, "numChannels");
        SamplesPerChannel = TryGetInt32OrNull(Audio, "samplesPerChannel");
    }

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int TryGetInt32(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var prop))
        {
            return default;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
        {
            return i;
        }

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out i))
        {
            return i;
        }

        return default;
    }

    private static int? TryGetInt32OrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
        {
            return i;
        }

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out i))
        {
            return i;
        }

        return null;
    }
}

