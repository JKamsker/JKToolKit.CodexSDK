using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.StructuredOutputs;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StructuredOutputMutationBoundaryTests
{
    [Theory]
    [InlineData("```json\n{\"choice\":1}\n```\n```text\n{\"choice\":2}\n```", "{\"choice\":1}")]
    [InlineData("```JSON\n[1,2]\n```\nExplanation {\"choice\":2}", "[1,2]")]
    [InlineData("```json\n[1]\n```\n```json\n[2]\n```", "[2]")]
    [InlineData("```text\n{\"choice\":1}\n```\nExample {\"choice\":2}", "{\"choice\":1}")]
    [InlineData("```json\n{broken}\n```\n```json\n[1,2]\n```", "[1,2]")]
    [InlineData("```json\n\n```\n[1,2]", "[1,2]")]
    [InlineData("[1,2]\n```unfinished", "[1,2]")]
    [InlineData("[1,2]\n```json\nunfinished", "[1,2]")]
    [InlineData("[1,2]\n{broken}", "[1,2]")]
    [InlineData("[1,2]\n{unterminated", "[1,2]")]
    [InlineData("{\"a\":{\"b\":1}}", "{\"a\":{\"b\":1}}")]
    [InlineData("```text\n[1]\n```\n[2]\n```", "[1]")]
    [InlineData("```json\n42\n```\n[1]", "[1]")]
    [InlineData("```json\n{broken}\n```\n[1]", "[1]")]
    [InlineData("prefix {\"\":1} suffix", "{\"\":1}")]
    public void Extraction_RespectsFencePreferenceAndLastCompleteTopLevelValue(string raw, string expected) =>
        Assert.Equal(expected, CodexStructuredJsonExtractor.ExtractJson(raw, true));

    [Theory]
    [InlineData("{broken}")]
    [InlineData("[1,]")]
    [InlineData("prefix {}")]
    public void StrictExtraction_AlwaysValidatesCompleteDocument(string raw) =>
        Assert.ThrowsAny<JsonException>(() => CodexStructuredJsonExtractor.ExtractJson(raw, false));

    [Theory]
    [InlineData("{broken}")]
    [InlineData("[1,]")]
    [InlineData("words")]
    [InlineData("```\n```")]
    public void TolerantExtraction_DoesNotAcceptInvalidCandidates(string raw) =>
        Assert.ThrowsAny<JsonException>(() => CodexStructuredJsonExtractor.ExtractJson(raw, true));

    [Fact]
    public void NullText_IsRejectedAsAnArgument()
    {
        Assert.Equal("rawText", Assert.Throws<ArgumentNullException>(() => CodexStructuredJsonExtractor.ExtractJson(null!, true)).ParamName);
    }

    [Fact]
    public void Schema_RequiresNestedPropertiesAndFlattensInheritedProperties()
    {
        var schema = CodexJsonSchemaGenerator.Generate<NestedRoot>();
        Assert.True(schema.GetProperty("properties").TryGetProperty("baseName", out _));
        var nested = schema.GetProperty("definitions").GetProperty(nameof(NestedChild));
        Assert.False(nested.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "a", "z_value" }, nested.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "baseName", "children" }, schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Schema_ExposesAbstractContractProperties()
    {
        var schema = CodexJsonSchemaGenerator.Generate<AbstractContract>();
        Assert.True(schema.GetProperty("properties").TryGetProperty("name", out _));
        Assert.Equal(new[] { "name" }, schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
    }

    public class BaseRoot { public string BaseName { get; set; } = ""; }
    public sealed class NestedRoot : BaseRoot { public List<NestedChild?> Children { get; set; } = []; }
    public sealed class NestedChild
    {
        [JsonPropertyName("z_value")] public string? Z { get; set; }
        public int A { get; set; }
    }
    public abstract class AbstractContract { public abstract string Name { get; set; } }
}
