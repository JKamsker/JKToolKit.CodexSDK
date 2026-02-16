using System.Text.Json;
using JKToolKit.CodexSDK.StructuredOutputs;
using FluentAssertions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexOutputSchemaTests
{
    [Fact]
    public void FromJson_ClonesElement_SoItSurvivesJsonDocumentDisposal()
    {
        CodexOutputSchema schema;

        using (var doc = JsonDocument.Parse("{\"type\":\"object\",\"properties\":{}}"))
        {
            schema = CodexOutputSchema.FromJson(doc.RootElement);
        }

        schema.Kind.Should().Be(CodexOutputSchemaKind.Json);
        schema.Json.Should().NotBeNull();
        schema.Json!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        schema.Json.Value.GetRawText().Should().Contain("\"type\"");
    }
}

