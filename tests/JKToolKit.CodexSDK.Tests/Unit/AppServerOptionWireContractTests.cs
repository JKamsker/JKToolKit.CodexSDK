using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerOptionWireContractTests
{
    [Fact]
    public void TurnStartClone_PreservesAllOverridesAndAllowsIndependentReplacement()
    {
        var options = new TurnStartOptions
        {
            DisabledPluginIds = ["disabled"], Input = [TurnInputItem.Text("question")], ClientUserMessageId = "message", TurnTrigger = "user",
            ToolOutput = TurnToolOutput.Text("tool", "result", "ns"), ResponsesApiClientMetadata = new Dictionary<string,string> { ["key"] = "value" },
            AdditionalContext = new Dictionary<string,TurnAdditionalContextEntry> { ["context"] = new() { Value = "additional", Kind = TurnAdditionalContextKind.Application } },
            Cwd = XPaths.Abs("repo"), RuntimeWorkspaceRoots = [XPaths.Abs("workspace")], Environments = [new() { EnvironmentId = "env", Cwd = XPaths.Abs("env") }],
            ApprovalPolicy = CodexApprovalPolicy.OnRequest, AskForApproval = CodexAskForApproval.FromPolicy(CodexApprovalPolicy.Never), ApprovalsReviewer = CodexApprovalsReviewer.User,
            SandboxPolicy = new SandboxPolicy.ReadOnly(), PermissionProfileId = "profile", Model = CodexModel.Parse("model"), ServiceTier = CodexServiceTier.Parse("fast"), ClearServiceTier = true,
            ServiceTierForTurn = CodexServiceTier.Parse("flex"), Effort = CodexReasoningEffort.High, Summary = "concise", Personality = "friendly",
            OutputSchema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}"), CollaborationMode = JsonSerializer.Deserialize<JsonElement>("{\"mode\":\"plan\"}")
        };
        var clone = options.Clone();
        clone.Should().NotBeSameAs(options);
        clone.Should().BeEquivalentTo(options, config => config.ComparingByMembers<JsonElement>());
        clone.Model = CodexModel.Parse("other"); clone.Input = []; clone.Cwd = XPaths.Abs("other"); clone.ClearServiceTier = false;
        options.Model!.Value.Value.Should().Be("model"); options.Input.Should().ContainSingle(); options.Cwd.Should().Be(XPaths.Abs("repo")); options.ClearServiceTier.Should().BeTrue();
        clone.AdditionalContext!["context"].Value.Should().Be("additional"); clone.AdditionalContext["context"].Kind.Should().Be(TurnAdditionalContextKind.Application);
        new TurnAdditionalContextEntry { Value = "untrusted" }.Kind.Should().Be(TurnAdditionalContextKind.Untrusted);
    }

    [Theory]
    [InlineData("AGENTS_MD", ExternalAgentConfigMigrationItemType.AgentsMd)]
    [InlineData("CONFIG", ExternalAgentConfigMigrationItemType.Config)]
    [InlineData("SKILLS", ExternalAgentConfigMigrationItemType.Skills)]
    [InlineData("MCP_SERVER_CONFIG", ExternalAgentConfigMigrationItemType.McpServerConfig)]
    public void MigrationItems_RoundTripClosedTypeAndPreserveExplicitHomeScope(string wire, ExternalAgentConfigMigrationItemType type)
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var item = new ExternalAgentConfigMigrationItem { Cwd = null, Description = "Migrate", ItemType = type };
        var json = JsonSerializer.SerializeToElement(item, options);
        json.GetProperty("cwd").ValueKind.Should().Be(JsonValueKind.Null); json.GetProperty("description").GetString().Should().Be("Migrate"); json.GetProperty("itemType").GetString().Should().Be(wire);
        JsonSerializer.Deserialize<ExternalAgentConfigMigrationItem>(json).Should().Be(item);
        type.ToWireValue().Should().Be(wire);
        ExternalAgentConfigMigrationItemTypeExtensions.TryParseWireValue(wire, out var parsed).Should().BeTrue(); parsed.Should().Be(type);
    }

    [Theory]
    [InlineData("\"FUTURE\"")]
    [InlineData("null")]
    [InlineData("42")]
    public void MigrationItems_RejectUnknownTypes(string type)
    {
        Action deserialize = () => JsonSerializer.Deserialize<ExternalAgentConfigMigrationItem>("{\"description\":\"migrate\",\"itemType\":" + type + "}");
        deserialize.Should().Throw<JsonException>();
        Action format = () => ((ExternalAgentConfigMigrationItemType)42).ToWireValue();
        format.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("itemType");
        ExternalAgentConfigMigrationItemTypeExtensions.TryParseWireValue("future", out var parsed).Should().BeFalse(); parsed.Should().Be(default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UserInputQuestions_OmitFalseFlagsAndRoundTripOptions(bool enabled)
    {
        var question = new ToolRequestUserInputQuestion
        {
            Id = "id", Header = "Header", Question = "Prompt", IsOther = enabled, IsSecret = enabled,
            Options = [new ToolRequestUserInputOption { Label = "Choice", Description = "Description" }]
        };
        var json = JsonSerializer.SerializeToElement(question);
        json.GetProperty("id").GetString().Should().Be("id"); json.GetProperty("header").GetString().Should().Be("Header"); json.GetProperty("question").GetString().Should().Be("Prompt");
        json.TryGetProperty("isOther", out _).Should().Be(enabled); json.TryGetProperty("isSecret", out _).Should().Be(enabled);
        var parsed = JsonSerializer.Deserialize<ToolRequestUserInputQuestion>(json)!;
        parsed.Id.Should().Be("id"); parsed.Header.Should().Be("Header"); parsed.Question.Should().Be("Prompt"); parsed.IsOther.Should().Be(enabled); parsed.IsSecret.Should().Be(enabled);
        var option = parsed.Options.Should().ContainSingle().Subject; option.Label.Should().Be("Choice"); option.Description.Should().Be("Description");
    }
}
