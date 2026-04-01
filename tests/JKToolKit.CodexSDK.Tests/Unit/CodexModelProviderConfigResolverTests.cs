using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexModelProviderConfigResolverTests
{
    [Fact]
    public void ParseActiveModelProvider_UsesTopLevelModelProvider_WhenNoProfileIsActive()
    {
        var provider = CodexModelProviderConfigResolver.ParseActiveModelProvider(
        [
            "model_provider = \"openai\"",
            "model = \"gpt-5.3-codex\""
        ]);

        provider.Should().Be("openai");
    }

    [Fact]
    public void ParseActiveModelProvider_UsesActiveProfileOverride_WhenConfigured()
    {
        var provider = CodexModelProviderConfigResolver.ParseActiveModelProvider(
        [
            "profile = \"local\"",
            "model_provider = \"openai\"",
            "",
            "[profiles.local]",
            "model_provider = \"ollama\""
        ]);

        provider.Should().Be("ollama");
    }

    [Fact]
    public void ParseActiveModelProvider_IgnoresComments_OutsideQuotedStrings()
    {
        var provider = CodexModelProviderConfigResolver.ParseActiveModelProvider(
        [
            "model_provider = \"openai\" # trailing comment",
            "profile = \"default#literal\""
        ]);

        provider.Should().Be("openai");
    }
}
