using System.Text.Json;

namespace JKToolKit.CodexSDK.StructuredOutputs.Internal;

internal static class StructuredOutputDeserializer
{
    internal static (T Value, string RawJson) DeserializeStructured<T>(
        string rawText,
        CodexStructuredOutputOptions structured,
        JsonSerializerOptions serializerOptions)
    {
        var extractedJson = (string?)null;
        try
        {
            extractedJson = CodexStructuredJsonExtractor.ExtractJson(rawText, structured.TolerantJsonExtraction);
        }
        catch (Exception ex)
        {
            throw new CodexStructuredOutputParseException(
                message: $"Failed to extract JSON for structured output type '{typeof(T).FullName}'.",
                rawText: rawText,
                extractedJson: null,
                innerException: ex);
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(extractedJson, serializerOptions);
            if (value is null)
            {
                throw new InvalidOperationException("Deserialized value was null.");
            }

            return (value, extractedJson);
        }
        catch (Exception ex)
        {
            throw new CodexStructuredOutputParseException(
                message: $"Failed to deserialize structured output into '{typeof(T).FullName}'.",
                rawText: rawText,
                extractedJson: extractedJson,
                innerException: ex);
        }
    }
}

