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

    [Fact]
    public void InheritedInterface_ContainsPropertiesFromTheWholeContract()
    {
        var schema = CodexJsonSchemaGenerator.Generate<IDerivedContract>();
        var required = schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).Order().ToArray();
        Assert.Equal(new[] { "count", "name" }, required);
    }

    [Fact]
    public async Task NullableComposedSchema_NormalizesNestedNullabilityAndStrictObjects()
    {
        var element = CodexJsonSchemaGenerator.Generate<ChoiceDto>();
        var schema = await JsonSchema.FromJsonAsync(element.GetRawText());
        Assert.Empty(schema.Validate("""{"choice":null}"""));
        Assert.Empty(schema.Validate("""{"choice":{"name":null}}"""));
        Assert.Empty(schema.Validate("""{"choice":{"name":"text"}}"""));
        Assert.Empty(schema.Validate("""{"choice":{"count":4}}"""));
        Assert.NotEmpty(schema.Validate("""{"choice":{}}"""));
        Assert.NotEmpty(schema.Validate("""{"choice":{"count":4,"extra":1}}"""));
        Assert.DoesNotContain("\"nullable\"", element.GetRawText());
    }

    [Fact]
    public async Task PropertyOnlySchema_RetainsRequiredAndAdditionalPropertyRules()
    {
        var element = CodexJsonSchemaGenerator.Generate<PropertyOnlyDto>();
        var schema = await JsonSchema.FromJsonAsync(element.GetRawText());
        Assert.Empty(schema.Validate("""{"value":{"name":"text"}}"""));
        Assert.NotEmpty(schema.Validate("""{"value":{}}"""));
        Assert.NotEmpty(schema.Validate("""{"value":{"name":"text","extra":1}}"""));
    }

    public sealed class PropertyOnlyDto { public PropertyOnly Value { get; set; } = new(); }
    [JsonSchemaProcessor(typeof(PropertyOnlySchemaProcessor))]
    public sealed class PropertyOnly;
    public sealed class PropertyOnlySchemaProcessor : ISchemaProcessor
    {
        public void Process(SchemaProcessorContext context)
        {
            context.Schema.Type = JsonObjectType.None;
            context.Schema.AllowAdditionalProperties = true;
            context.Schema.Properties["name"] = new JsonSchemaProperty { Type = JsonObjectType.String };
        }
    }

    public interface IBaseContract { string Name { get; set; } }
    public interface IDerivedContract : IBaseContract { int Count { get; set; } }
    public sealed class ChoiceDto { public Choice Choice { get; set; } = new(); }
    [JsonSchemaProcessor(typeof(ChoiceSchemaProcessor))]
    public sealed class Choice;
    public sealed class ChoiceSchemaProcessor : ISchemaProcessor
    {
        public void Process(SchemaProcessorContext context)
        {
            context.Schema.Type = JsonObjectType.None;
            context.Schema.Properties.Clear();
            context.Schema.AllowAdditionalProperties = true;
            context.Schema.ExtensionData = new Dictionary<string, object?> { ["nullable"] = true };
            var named = new JsonSchema { Type = JsonObjectType.Object };
            named.Properties["name"] = new JsonSchemaProperty
            {
                Type = JsonObjectType.String,
                ExtensionData = new Dictionary<string, object?> { ["nullable"] = true }
            };
            var numbered = new JsonSchema { Type = JsonObjectType.Object };
            numbered.Properties["count"] = new JsonSchemaProperty { Type = JsonObjectType.Integer };
            context.Schema.OneOf.Add(named);
            context.Schema.OneOf.Add(numbered);
        }
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
