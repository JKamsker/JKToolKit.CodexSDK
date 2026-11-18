using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerVersionedDisplayTests
{
    private static CodexThreadItem Parse(string display, bool moderated = true)
    {
        var payload = new JsonObject
        {
            ["id"] = "answer", ["type"] = "agentMessage", ["text"] = "answer",
            ["moderation"] = new JsonObject { ["moderated"] = moderated, ["display"] = JsonNode.Parse(display) }
        };
        using var document = JsonDocument.Parse(payload.ToJsonString());
        return CodexThreadItemParser.Parse(document.RootElement);
    }

    [Theory]
    [InlineData("check", CodexDisplayTrustedContacts.Check)]
    [InlineData("skip", CodexDisplayTrustedContacts.Skip)]
    public void Show_PreservesTrustedContactsAndOptionalAnnotation(string contacts, CodexDisplayTrustedContacts expected)
    {
        foreach (var subject in new[] { null, "answer", "prompt" })
        {
            var instruction = new JsonObject
            {
                ["version"] = 1, ["mode"] = "show", ["trustedContacts"] = contacts,
                ["additionalV1Field"] = true,
                ["annotation"] = subject is null ? null : new JsonObject { ["kind"] = "wellbeingHelpline", ["subject"] = subject }
            };
            var item = Parse(instruction.ToJsonString(), false).Should().BeOfType<CodexThreadItemAgentMessage>().Subject;
            item.Moderation!.Moderated.Should().BeFalse();
            item.Moderation.Display.Should().BeNull();
            var display = item.Moderation.VersionedDisplay.Should().BeOfType<CodexAgentMessageDisplayShow>().Subject;
            display.Version.Should().Be(1);
            display.TrustedContacts.Should().Be(expected);
            display.HelplineSubject.Should().Be(subject switch
            {
                "answer" => CodexDisplayHelplineSubject.Answer,
                "prompt" => CodexDisplayHelplineSubject.Prompt,
                _ => (CodexDisplayHelplineSubject?)null
            });
            item.Raw.GetProperty("moderation").GetProperty("display").GetProperty("additionalV1Field").GetBoolean().Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("""{"version":1,"mode":"show","trustedContacts":"skip"}""")]
    [InlineData("""{"version":1,"mode":"hide","replacement":{"kind":"wellbeingPromptHelpline"}}""")]
    [InlineData("""{"version":1,"mode":"block","reason":{"kind":"protection","protectionType":"omnimod"}}""")]
    [InlineData("""{"version":1,"mode":"block","reason":{"kind":"cyber"}}""")]
    public void VersionedDisplay_RequiresVersionAndMode(string json)
    {
        Parse(json).Should().BeOfType<CodexThreadItemAgentMessage>();
        var display = JsonNode.Parse(json)!.AsObject();
        foreach (var field in new[] { "version", "mode" })
        {
            var missing = display.DeepClone().AsObject();
            missing.Remove(field);
            Parse(missing.ToJsonString(), false).Should().BeOfType<CodexThreadItemUnknown>();
        }
    }

    [Fact]
    public void HideAndBlock_PreserveTypedInstructions()
    {
        var hide = Parse("""{"version":1,"mode":"hide","replacement":{"kind":"wellbeingPromptHelpline"}}""")
            .Should().BeOfType<CodexThreadItemAgentMessage>().Subject;
        hide.Moderation!.VersionedDisplay.Should().BeOfType<CodexAgentMessageDisplayHide>();
        hide.Moderation.Display.Should().BeNull();
        hide.Moderation.Moderated.Should().BeTrue();

        var protection = Parse("""{"version":1,"mode":"block","reason":{"kind":"protection","protectionType":"omnimod"}}""")
            .Should().BeOfType<CodexThreadItemAgentMessage>().Subject;
        protection.Moderation!.VersionedDisplay.Should().Be(new CodexAgentMessageDisplayProtection("omnimod"));

        foreach (var trustedAccess in new[] { "null", "false", "true" })
        {
            var cyber = Parse("""{"version":1,"mode":"block","reason":{"kind":"cyber","trustedAccess":ACCESS}}"""
                .Replace("ACCESS", trustedAccess, StringComparison.Ordinal))
                .Should().BeOfType<CodexThreadItemAgentMessage>().Subject;
            cyber.Moderation!.VersionedDisplay.Should().Be(new CodexAgentMessageDisplayCyber(
                trustedAccess == "null" ? null : bool.Parse(trustedAccess)));
        }
        var omitted = Parse("""{"version":1,"mode":"block","reason":{"kind":"cyber"}}""")
            .Should().BeOfType<CodexThreadItemAgentMessage>().Subject;
        omitted.Moderation!.VersionedDisplay.Should().Be(new CodexAgentMessageDisplayCyber(null));
    }

    [Theory]
    [InlineData("""{"version":2,"mode":"show","trustedContacts":"skip"}""")]
    [InlineData("""{"version":1,"mode":"unknown","action":"legacy"}""")]
    [InlineData("""{"version":"1","mode":"show","trustedContacts":"skip"}""")]
    [InlineData("""{"version":1,"mode":"show"}""")]
    [InlineData("""{"version":1,"mode":"show","trustedContacts":"unknown"}""")]
    [InlineData("""{"version":1,"mode":"show","trustedContacts":"skip","annotation":{"kind":"unknown"}}""")]
    [InlineData("""{"version":1,"mode":"show","trustedContacts":"skip","annotation":{"kind":"wellbeingHelpline","subject":"other"}}""")]
    [InlineData("""{"version":1,"mode":"hide"}""")]
    [InlineData("""{"version":1,"mode":"hide","replacement":{"kind":"unknown"}}""")]
    [InlineData("""{"version":1,"mode":"block"}""")]
    [InlineData("""{"version":1,"mode":"block","reason":{"kind":"unknown"}}""")]
    [InlineData("""{"version":1,"mode":"block","reason":{"kind":"protection"}}""")]
    [InlineData("""{"version":1,"mode":"block","reason":{"kind":"cyber","trustedAccess":0}}""")]
    public void UnsupportedOrMalformedDisplay_RetainsRawUnknownItem(string json)
    {
        var item = Parse(json, false).Should().BeOfType<CodexThreadItemUnknown>().Subject;
        item.Raw.GetProperty("moderation").GetProperty("display").GetRawText()
            .Should().Be(JsonNode.Parse(json)!.ToJsonString());
    }
}
