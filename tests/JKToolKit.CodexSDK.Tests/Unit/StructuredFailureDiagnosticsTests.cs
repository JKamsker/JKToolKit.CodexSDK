using System.Text.Json;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;
using JKToolKit.CodexSDK.StructuredOutputs.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StructuredFailureDiagnosticsTests
{
    private sealed record Answer(string Value);

    [Fact]
    public void ExtractionFailure_PreservesOriginalJsonErrorAndIdentifiesRequestedType()
    {
        const string text = "This is not JSON";
        var error = Assert.Throws<CodexStructuredOutputParseException>(() => StructuredOutputDeserializer.DeserializeStructured<Answer>(text, new(), new(JsonSerializerDefaults.Web)));
        Assert.Equal(text, error.RawText);
        Assert.Null(error.ExtractedJson);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
        Assert.Contains("extract JSON", error.Message);
        Assert.Contains(typeof(Answer).FullName!, error.Message);
    }

    [Fact]
    public void DeserializationFailure_PreservesExtractedJsonAndDistinguishesTypeMismatch()
    {
        const string json = "{\"value\":42}";
        var error = Assert.Throws<CodexStructuredOutputParseException>(() => StructuredOutputDeserializer.DeserializeStructured<Answer>("prefix " + json, new(), new(JsonSerializerDefaults.Web)));
        Assert.Equal("prefix " + json, error.RawText);
        Assert.Equal(json, error.ExtractedJson);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
        Assert.Contains("deserialize", error.Message);
        Assert.Contains(typeof(Answer).FullName!, error.Message);
    }

    [Fact]
    public void NullResult_DiagnosesNullRatherThanInvalidJson()
    {
        var error = Assert.Throws<CodexStructuredOutputParseException>(() => StructuredOutputDeserializer.DeserializeStructured<Answer>("null", new(), new()));
        Assert.Equal("null", error.ExtractedJson);
        Assert.Contains("value was null", Assert.IsType<InvalidOperationException>(error.InnerException).Message);
    }

    [Fact]
    public void DefaultRetryPrompt_ExplainsPreviousFailureAndIncludesError()
    {
        var options = new CodexStructuredRetryOptions();
        var prompt = options.BuildRetryPrompt(new() { Attempt = 1, MaxAttempts = 2, RawText = "bad", Exception = new FormatException("invalid number") });
        Assert.Contains("previous response", prompt);
        Assert.Contains("invalid number", prompt);
    }

    [Fact]
    public void InvalidSchemaSources_ReportWhichInputMustBeCorrected()
    {
        Assert.Contains("undefined", Assert.Throws<ArgumentException>(() => CodexOutputSchema.FromJson(default)).Message);
        Assert.Contains("Schema file path", Assert.Throws<ArgumentException>(() => CodexOutputSchema.FromFile(" ")).Message);
        var unsupported = Assert.Throws<NotSupportedException>(() => CodexJsonSchemaGenerator.Generate<Dictionary<string, int>>());
        Assert.Contains("dictionary/additionalProperties", unsupported.Message);
        Assert.Contains("custom JSON schema", unsupported.Message);
        Assert.Contains("empty response", Assert.Throws<InvalidOperationException>(() => CodexStructuredJsonExtractor.ExtractJson(" ", true)).Message);
    }

    [Fact]
    public void InvalidBounds_ExplainConstraintsAndReportBothDates()
    {
        Assert.Contains("non-negative", Assert.Throws<ArgumentOutOfRangeException>(() => new EventStreamOptions(FromByteOffset: -1)).Message);
        Assert.Contains("non-negative", Assert.Throws<ArgumentOutOfRangeException>(() => EventStreamOptions.FromOffset(-1)).Message);
        var from = new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(-1);
        var constructor = Assert.Throws<ArgumentException>(() => new SessionFilter(FromDate: from, ToDate: to));
        var factory = Assert.Throws<ArgumentException>(() => SessionFilter.ForDateRange(from, to));
        foreach (var error in new[] { constructor, factory })
        {
            Assert.Contains(from.ToString("O"), error.Message);
            Assert.Contains(to.ToString("O"), error.Message);
        }
        Assert.Contains("not initialized", Assert.Throws<InvalidOperationException>(() => default(CodexAskForApproval).ToWireValue()).Message);
    }
}
