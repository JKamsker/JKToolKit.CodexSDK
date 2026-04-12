using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerThreadRealtimeStartWiring
{
    public static void ValidateOptions(ThreadRealtimeStartOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));

        if (options.SessionId is not null && string.IsNullOrWhiteSpace(options.SessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace when provided.", nameof(options));

        if (options.Voice is not null && string.IsNullOrWhiteSpace(options.Voice))
            throw new ArgumentException("Voice cannot be empty or whitespace when provided.", nameof(options));

        switch (options.PromptMode)
        {
            case ThreadRealtimePromptMode.Custom when options.Prompt is null:
                throw new ArgumentException(
                    "Prompt must be provided when PromptMode is Custom.",
                    nameof(options));

            case ThreadRealtimePromptMode.Default or ThreadRealtimePromptMode.None when options.Prompt is not null:
                throw new ArgumentException(
                    "Prompt must be null unless PromptMode is Custom.",
                    nameof(options));
        }
    }

    public static ThreadRealtimeStartParams BuildParams(ThreadRealtimeStartOptions options)
    {
        Dictionary<string, JsonElement>? extensionData = null;
        switch (options.PromptMode)
        {
            case ThreadRealtimePromptMode.Custom:
                extensionData = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["prompt"] = JsonSerializer.SerializeToElement(options.Prompt)
                };
                break;

            case ThreadRealtimePromptMode.None:
                extensionData = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["prompt"] = JsonSerializer.SerializeToElement<string?>(null)
                };
                break;
        }

        return new ThreadRealtimeStartParams
        {
            ThreadId = options.ThreadId,
            SessionId = options.SessionId,
            Transport = options.Transport?.ToJson(),
            Voice = options.Voice,
            ExtensionData = extensionData
        };
    }
}
