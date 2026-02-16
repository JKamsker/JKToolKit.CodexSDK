using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema;
using NJsonSchema.Generation;

namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Generates strict JSON Schemas suitable for Codex structured outputs.
/// </summary>
public static class CodexJsonSchemaGenerator
{
    /// <summary>
    /// Generates a strict JSON schema for <typeparamref name="T"/>.
    /// </summary>
    public static JsonElement Generate<T>(JsonSerializerOptions? serializerOptions = null)
    {
        var schema = GenerateSchemaObject(typeof(T), serializerOptions);
        return ToStrictJsonElement(schema);
    }

    private static JsonSchema GenerateSchemaObject(Type type, JsonSerializerOptions? serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(type);

        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            SerializerOptions = serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web),
            FlattenInheritanceHierarchy = true,
            GenerateAbstractProperties = true
        };

        var generator = new JsonSchemaGenerator(settings);
        return generator.Generate(type);
    }

    private static JsonElement ToStrictJsonElement(JsonSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var json = schema.ToJson();
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Generated schema JSON was null.");

        NormalizeNullability(node);
        EnforceStrictObjects(node);

        using var doc = JsonDocument.Parse(node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return doc.RootElement.Clone();
    }

    private static void NormalizeNullability(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            // NJsonSchema can emit OpenAPI-style "nullable": true. Convert it to JSON Schema type union.
            if (obj.TryGetPropertyValue("nullable", out var nullableNode) &&
                nullableNode is JsonValue v &&
                v.TryGetValue<bool>(out var nullable) &&
                nullable)
            {
                obj.Remove("nullable");

                if (obj.TryGetPropertyValue("type", out var typeNode) && typeNode is not null)
                {
                    EnsureTypeAllowsNull(obj, typeNode);
                }
                else
                {
                    // If no explicit type, represent nullability via anyOf.
                    var anyOf = new JsonArray
                    {
                        new JsonObject { ["type"] = "null" },
                        obj.DeepClone()
                    };
                    obj.Clear();
                    obj["anyOf"] = anyOf;
                    return;
                }
            }
        }

        foreach (var child in EnumerateChildren(node))
        {
            NormalizeNullability(child);
        }
    }

    private static void EnsureTypeAllowsNull(JsonObject obj, JsonNode typeNode)
    {
        if (typeNode is JsonValue typeValue && typeValue.TryGetValue<string>(out var typeStr))
        {
            obj["type"] = new JsonArray(typeStr, "null");
            return;
        }

        if (typeNode is JsonArray arr)
        {
            if (!arr.Any(n => n is JsonValue tv && tv.TryGetValue<string>(out var s) && s == "null"))
            {
                arr.Add("null");
            }

            obj["type"] = arr;
        }
    }

    private static void EnforceStrictObjects(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (IsObjectSchema(obj))
            {
                if (obj.TryGetPropertyValue("additionalProperties", out var ap))
                {
                    if (ap is JsonObject or JsonArray)
                    {
                        throw new NotSupportedException(
                            "DTO type requires dictionary/additionalProperties schemas, which are not supported under strict structured outputs. " +
                            "Use a DTO without dictionaries/free-form objects, or provide a custom JSON schema.");
                    }
                }

                obj["additionalProperties"] = false;

                if (obj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject propsObj)
                {
                    var required = new JsonArray();
                    foreach (var name in propsObj.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
                    {
                        required.Add(name);
                    }

                    obj["required"] = required;
                }
            }
        }

        foreach (var child in EnumerateChildren(node))
        {
            EnforceStrictObjects(child);
        }
    }

    private static bool IsObjectSchema(JsonObject obj)
    {
        if (obj.TryGetPropertyValue("type", out var t))
        {
            if (t is JsonValue v && v.TryGetValue<string>(out var s))
            {
                return string.Equals(s, "object", StringComparison.OrdinalIgnoreCase);
            }

            if (t is JsonArray arr)
            {
                return arr.Any(n => n is JsonValue tv && tv.TryGetValue<string>(out var s) &&
                                    string.Equals(s, "object", StringComparison.OrdinalIgnoreCase));
            }
        }

        // If it declares properties, treat it as an object schema.
        return obj.ContainsKey("properties");
    }

    private static IEnumerable<JsonNode> EnumerateChildren(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj)
                {
                    if (kvp.Value is not null)
                    {
                        yield return kvp.Value;
                    }
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    if (item is not null)
                    {
                        yield return item;
                    }
                }
                break;
        }
    }
}
