using System.Text.Json;
using JKToolKit.CodexSDK.StructuredOutputs;
using NJsonSchema;
using NJsonSchema.Annotations;
using NJsonSchema.Generation;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StructuredSchemaAnnotationTests
{
    [Fact]
    public async Task AnnotatedNullableProperties_AcceptNullAndRetainStrictObjectRules()
    {
        var element = CodexJsonSchemaGenerator.Generate<AnnotatedDto>();
        var schema = await JsonSchema.FromJsonAsync(element.GetRawText());
        Assert.Empty(schema.Validate("""{"text":null,"number":null,"child":null}"""));
        Assert.Empty(schema.Validate("""{"text":"value","number":4,"child":{"name":"child"}}"""));
        Assert.NotEmpty(schema.Validate("""{"text":"value","number":4,"child":{"name":"child","extra":1}}"""));
        Assert.NotEmpty(schema.Validate("""{"text":"value","number":4,"child":{}}"""));
        Assert.DoesNotContain("\"nullable\"", element.GetRawText());
        Assert.Single(element.GetProperty("properties").GetProperty("number").GetProperty("type").EnumerateArray(), x => x.GetString() == "null");
    }

    [Fact]
    public async Task CustomScalarUnion_NormalizesNullableWithoutLosingAcceptedTypes()
    {
        var element = CodexJsonSchemaGenerator.Generate<UnionDto>();
        var schema = await JsonSchema.FromJsonAsync(element.GetRawText());
        Assert.Empty(schema.Validate("""{"value":null}"""));
        Assert.Empty(schema.Validate("""{"value":42}"""));
        Assert.Empty(schema.Validate("""{"value":"text"}"""));
        Assert.NotEmpty(schema.Validate("""{"value":false}"""));
        Assert.NotEmpty(schema.Validate("""{}"""));
        Assert.DoesNotContain("\"nullable\"", element.GetRawText());
    }

    public sealed class UnionDto { public ScalarUnion Value { get; set; } = new(); }
    [JsonSchemaProcessor(typeof(ScalarUnionSchemaProcessor))]
    public sealed class ScalarUnion;
    public sealed class ScalarUnionSchemaProcessor : ISchemaProcessor
    {
        public void Process(SchemaProcessorContext context)
        {
            // A custom converter accepts either textual or numeric identifiers.
            context.Schema.Type = JsonObjectType.String | JsonObjectType.Integer;
            context.Schema.ExtensionData = new Dictionary<string, object?> { ["nullable"] = true };
        }
    }

    public sealed class AnnotatedDto
    {
        [JsonSchemaExtensionData("nullable", true)]
        public string Text { get; set; } = "";
        [JsonSchemaExtensionData("nullable", true)]
        public int? Number { get; set; }
        [JsonSchemaExtensionData("nullable", true)]
        public Child Child { get; set; } = new();
    }
    [JsonSchemaExtensionData("nullable", true)]
    public sealed class Child { public string Name { get; set; } = ""; }
}
