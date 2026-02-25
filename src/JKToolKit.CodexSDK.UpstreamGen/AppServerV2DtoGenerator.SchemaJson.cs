using System.Text.Json.Nodes;

namespace JKToolKit.CodexSDK.UpstreamGen;

internal static partial class AppServerV2DtoGenerator
{
    private static string BuildFlattenedV2SchemaJson(string schemaPath)
    {
        var rootNode = JsonNode.Parse(File.ReadAllText(schemaPath)) as JsonObject
                       ?? throw new InvalidOperationException("Schema JSON root was null or not an object.");

        if (!rootNode.TryGetPropertyValue("definitions", out var defsNode) || defsNode is not JsonObject defs)
        {
            throw new InvalidOperationException("Schema JSON does not contain a 'definitions' object.");
        }

        if (!defs.TryGetPropertyValue("v2", out var v2Node) || v2Node is not JsonObject v2Defs)
        {
            throw new InvalidOperationException("Schema JSON does not contain 'definitions.v2'.");
        }

        var flattened = new JsonObject();
        foreach (var kvp in v2Defs)
        {
            if (kvp.Value is null)
            {
                continue;
            }

            var clone = kvp.Value.DeepClone();
            EnsureHasTitle(clone, kvp.Key);
            RewriteRefs(clone);
            RewriteBooleanSchemas(clone);
            CollapseSingleRefAllOf(clone);
            flattened[kvp.Key] = clone;
        }

        var props = new JsonObject();
        foreach (var name in flattened.Select(k => k.Key).OrderBy(k => k, StringComparer.Ordinal))
        {
            props[name] = new JsonObject
            {
                ["$ref"] = $"#/definitions/{name}"
            };
        }

        var outRoot = new JsonObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = RootTypeName,
            ["type"] = "object",
            ["properties"] = props,
            ["definitions"] = flattened
        };

        return outRoot.ToJsonString();
    }

    private static void EnsureHasTitle(JsonNode node, string title)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        if (obj.TryGetPropertyValue("title", out var titleNode) &&
            titleNode is JsonValue v &&
            v.TryGetValue<string>(out var existing) &&
            !string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        obj["title"] = title;
    }

    private static void CollapseSingleRefAllOf(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            CollapseAllOfOnObject(obj);

            foreach (var kvp in obj.ToArray())
            {
                if (kvp.Value is not null)
                {
                    CollapseSingleRefAllOf(kvp.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                var item = arr[i];
                if (item is null)
                {
                    continue;
                }

                if (item is JsonObject itemObj)
                {
                    CollapseAllOfOnObject(itemObj);
                }

                CollapseSingleRefAllOf(item);
            }
        }
    }

    private static void CollapseAllOfOnObject(JsonObject obj)
    {
        if (!obj.TryGetPropertyValue("allOf", out var allOfNode) || allOfNode is not JsonArray allOf)
        {
            return;
        }

        if (allOf.Count != 1 || allOf[0] is not JsonObject allOfItem)
        {
            return;
        }

        if (!allOfItem.TryGetPropertyValue("$ref", out var refNode) ||
            refNode is not JsonValue refValue ||
            !refValue.TryGetValue<string>(out var s) ||
            string.IsNullOrWhiteSpace(s))
        {
            return;
        }

        obj.Clear();
        obj["$ref"] = s;
    }

    private static void RewriteRefs(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$ref", out var refNode) &&
                refNode is JsonValue v &&
                v.TryGetValue<string>(out var s) &&
                s.StartsWith("#/definitions/v2/", StringComparison.Ordinal))
            {
                obj["$ref"] = "#/definitions/" + s["#/definitions/v2/".Length..];
            }

            foreach (var kvp in obj.ToArray())
            {
                if (kvp.Value is not null)
                {
                    RewriteRefs(kvp.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    RewriteRefs(item);
                }
            }
        }
    }

    private static void RewriteBooleanSchemas(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToArray())
            {
                if (kvp.Value is null)
                {
                    continue;
                }

                if (kvp.Value is JsonValue v && v.TryGetValue<bool>(out var b))
                {
                    if (IsSchemaKeywordExpectingSchema(kvp.Key))
                    {
                        obj[kvp.Key] = b ? new JsonObject() : new JsonObject { ["not"] = new JsonObject() };
                    }

                    continue;
                }

                if (kvp.Key is "properties" or "patternProperties" or "definitions" or "$defs")
                {
                    if (kvp.Value is JsonObject map)
                    {
                        foreach (var entry in map.ToArray())
                        {
                            if (entry.Value is JsonValue mv && mv.TryGetValue<bool>(out var mb))
                            {
                                map[entry.Key] = mb ? new JsonObject() : new JsonObject { ["not"] = new JsonObject() };
                                continue;
                            }

                            if (entry.Value is not null)
                            {
                                RewriteBooleanSchemas(entry.Value);
                            }
                        }
                    }

                    continue;
                }

                if (kvp.Key is "anyOf" or "oneOf" or "allOf")
                {
                    if (kvp.Value is JsonArray arr)
                    {
                        for (var i = 0; i < arr.Count; i++)
                        {
                            var item = arr[i];
                            if (item is JsonValue iv && iv.TryGetValue<bool>(out var ib))
                            {
                                arr[i] = ib ? new JsonObject() : new JsonObject { ["not"] = new JsonObject() };
                                continue;
                            }

                            if (item is not null)
                            {
                                RewriteBooleanSchemas(item);
                            }
                        }
                    }

                    continue;
                }

                RewriteBooleanSchemas(kvp.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    RewriteBooleanSchemas(item);
                }
            }
        }
    }

    private static bool IsSchemaKeywordExpectingSchema(string keyword) =>
        keyword is "additionalProperties" or "items" or "additionalItems" or "contains" or "propertyNames" or "not" or "if" or "then" or "else";
}

