using System.Text.RegularExpressions;
using NJsonSchema.CodeGeneration;

namespace JKToolKit.CodexSDK.UpstreamGen;

internal static partial class AppServerV2DtoGenerator
{
    private static void WriteMissingTypeFixups(string outDir, string @namespace, CodeArtifact[] artifacts)
    {
        var generatedTypeNames = new HashSet<string>(artifacts.Select(a => a.TypeName), StringComparer.Ordinal);

        var missingTypeKinds = FindMissingTypeKinds(artifacts, generatedTypeNames);
        foreach (var (typeName, kind) in missingTypeKinds.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var typeCode = kind == MissingTypeKind.Enum
                ? BuildEnumFixup(typeName)
                : BuildClassFixup(typeName);

            var code = BuildArtifactFile(@namespace, typeCode);
            var path = Path.Combine(outDir, MakeSafeFileName(typeName) + ".Fixup.g.cs");
            File.WriteAllText(path, code);
        }
    }

    private static string BuildEnumFixup(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return $$"""
[System.CodeDom.Compiler.GeneratedCode("JKToolKit.CodexSDK.UpstreamGen", "0.0.0")]
internal enum {{typeName}}
{
    [System.Runtime.Serialization.EnumMember(Value = @"unknown")]
    Unknown = 0,
}
""";
    }

    private static string BuildClassFixup(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return $$"""
[System.CodeDom.Compiler.GeneratedCode("JKToolKit.CodexSDK.UpstreamGen", "0.0.0")]
internal partial class {{typeName}}
{

    private System.Collections.Generic.IDictionary<string, object>? _additionalProperties;

    [System.Text.Json.Serialization.JsonExtensionData]
    public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
    {
        get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
        set { _additionalProperties = value; }
    }

}
""";
    }

    private enum MissingTypeKind
    {
        Class,
        Enum
    }

    private static IReadOnlyDictionary<string, MissingTypeKind> FindMissingTypeKinds(
        CodeArtifact[] artifacts,
        HashSet<string> generatedTypeNames)
    {
        var missing = new Dictionary<string, MissingTypeKind>(StringComparer.Ordinal);

        foreach (var artifact in artifacts)
        {
            var nextPropertyIsStringEnum = false;
            using var reader = new StringReader(artifact.Code);
            while (reader.ReadLine() is { } line)
            {
                if (line.Contains("JsonStringEnumConverter", StringComparison.Ordinal))
                {
                    nextPropertyIsStringEnum = true;
                    continue;
                }

                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                    !trimmed.Contains("{ get; set; }", StringComparison.Ordinal))
                {
                    continue;
                }

                var afterPublic = trimmed["public ".Length..];
                var spaceIndex = afterPublic.IndexOf(' ');
                if (spaceIndex <= 0)
                {
                    continue;
                }

                var typeExpression = afterPublic[..spaceIndex].TrimEnd('?');

                foreach (var candidate in ExtractUnqualifiedTypeTokens(typeExpression))
                {
                    if (ShouldIgnoreTypeToken(candidate))
                    {
                        continue;
                    }

                    if (generatedTypeNames.Contains(candidate))
                    {
                        continue;
                    }

                    if (!missing.TryGetValue(candidate, out var existing))
                    {
                        missing[candidate] = nextPropertyIsStringEnum ? MissingTypeKind.Enum : MissingTypeKind.Class;
                        continue;
                    }

                    if (existing != MissingTypeKind.Enum && nextPropertyIsStringEnum)
                    {
                        missing[candidate] = MissingTypeKind.Enum;
                    }
                }

                nextPropertyIsStringEnum = false;
            }
        }

        return missing;
    }

    private static IEnumerable<string> ExtractUnqualifiedTypeTokens(string typeExpression)
    {
        foreach (Match match in Regex.Matches(typeExpression, @"\b[A-Za-z_][A-Za-z0-9_]*\b"))
        {
            if (match.Index > 0 && typeExpression[match.Index - 1] == '.')
            {
                continue;
            }

            yield return match.Value;
        }
    }

    private static bool ShouldIgnoreTypeToken(string token)
    {
        return token is "System"
            or "String"
            or "Object"
            or "Boolean"
            or "Byte"
            or "SByte"
            or "Int16"
            or "UInt16"
            or "Int32"
            or "UInt32"
            or "Int64"
            or "UInt64"
            or "Single"
            or "Double"
            or "Decimal"
            or "Char"
            or "Guid"
            or "DateTime"
            or "DateTimeOffset"
            or "TimeSpan"
            or "Uri"
            or "JsonDocument"
            or "JsonElement"
            or "JsonNode"
            or "ICollection"
            or "IList"
            or "IEnumerable"
            or "IDictionary"
            or "Dictionary"
            or "Collection"
            or "Nullable"
            or "object"
            or "string"
            or "bool"
            or "byte"
            or "sbyte"
            or "short"
            or "ushort"
            or "int"
            or "uint"
            or "long"
            or "ulong"
            or "float"
            or "double"
            or "decimal"
            or "char";
    }
}

