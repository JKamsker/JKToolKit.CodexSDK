namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Controls automatic retry behavior for structured outputs.
/// </summary>
public sealed record CodexStructuredRetryOptions
{
    /// <summary>
    /// Gets the maximum number of attempts to obtain a valid structured result.
    /// </summary>
    /// <remarks>
    /// This includes the initial attempt. The default is 3.
    /// </remarks>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Gets a value indicating whether the parse/deserialization error message should be included in the retry prompt.
    /// </summary>
    public bool IncludeErrorMessageInRetryPrompt { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of characters of the error message to include in the retry prompt.
    /// </summary>
    public int MaxErrorMessageChars { get; init; } = 400;

    /// <summary>
    /// Optional custom retry prompt factory.
    /// </summary>
    /// <remarks>
    /// If null, a conservative default prompt is used.
    /// </remarks>
    public Func<CodexStructuredRetryContext, string>? RetryPromptFactory { get; init; }

    internal string BuildRetryPrompt(CodexStructuredRetryContext context)
    {
        if (RetryPromptFactory is not null)
        {
            return RetryPromptFactory(context);
        }

        var err = context.Exception?.Message ?? "Unknown error.";
        if (err.Length > MaxErrorMessageChars)
        {
            err = err.Substring(0, MaxErrorMessageChars);
        }

        var suffix = IncludeErrorMessageInRetryPrompt ? $" Error: {err}" : string.Empty;

        return
            "Your previous response was not valid JSON matching the required schema. " +
            "Return ONLY valid JSON that conforms to the schema. " +
            "Do not include markdown, code fences, explanations, or any extra text." +
            suffix;
    }
}

