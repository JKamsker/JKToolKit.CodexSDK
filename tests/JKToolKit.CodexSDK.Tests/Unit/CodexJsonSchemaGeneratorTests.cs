using System.Text.Json;
using JKToolKit.CodexSDK.StructuredOutputs;
using FluentAssertions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexJsonSchemaGeneratorTests
{
    private sealed class SampleDto
    {
        public required string Name { get; init; }
        public int? Age { get; init; }
        public string? Note { get; init; }
    }

    [Fact]
    public void Generate_ProducesStrictObjectSchema()
    {
        var schema = CodexJsonSchemaGenerator.Generate<SampleDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        schema.ValueKind.Should().Be(JsonValueKind.Object);
        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();

        var properties = schema.GetProperty("properties");
        properties.ValueKind.Should().Be(JsonValueKind.Object);
        var propertyNames = properties.EnumerateObject().Select(p => p.Name).ToList();
        propertyNames.Should().Contain(new[] { "name", "age", "note" });

        var required = schema.GetProperty("required");
        required.ValueKind.Should().Be(JsonValueKind.Array);
        var requiredNames = required.EnumerateArray().Select(e => e.GetString()).ToList();
        requiredNames.Should().Contain(propertyNames);
    }

    [Fact]
    public void Generate_AllowsNullForNullableProperties()
    {
        var schema = CodexJsonSchemaGenerator.Generate<SampleDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var props = schema.GetProperty("properties");

        var note = props.GetProperty("note");
        AllowsNull(note).Should().BeTrue();

        var age = props.GetProperty("age");
        AllowsNull(age).Should().BeTrue();

        var name = props.GetProperty("name");
        AllowsNull(name).Should().BeFalse();
    }

    private static bool AllowsNull(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("type", out var t))
        {
            if (t.ValueKind == JsonValueKind.String)
            {
                return string.Equals(t.GetString(), "null", StringComparison.OrdinalIgnoreCase);
            }

            if (t.ValueKind == JsonValueKind.Array)
            {
                return t.EnumerateArray()
                    .Any(e => e.ValueKind == JsonValueKind.String &&
                              string.Equals(e.GetString(), "null", StringComparison.OrdinalIgnoreCase));
            }
        }

        if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
        {
            return anyOf.EnumerateArray().Any(AllowsNull);
        }

        if (schema.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array)
        {
            return oneOf.EnumerateArray().Any(AllowsNull);
        }

        return false;
    }
}

