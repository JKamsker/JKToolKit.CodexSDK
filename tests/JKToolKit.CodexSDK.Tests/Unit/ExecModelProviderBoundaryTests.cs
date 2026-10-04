using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecModelProviderBoundaryTests
{
    [Theory]
    [InlineData("model_provider = bare")]
    [InlineData("model_provider = \"")]
    [InlineData("model_provider = \"unterminated")]
    [InlineData("model_provider = unterminated\"")]
    [InlineData("= \"ignored\"")]
    [InlineData("no separator")]
    [InlineData("[not.a.profile]")]
    [InlineData("[profiles.nested.invalid]")]
    [InlineData("[profiles.incomplete")]
    public void InvalidOrUnrelatedLines_DoNotOverrideValidTopLevelProvider(string line)
    {
        Assert.Equal("original", CodexModelProviderConfigResolver.ParseActiveModelProvider(
            [null!, "", "# comment", "model_provider = \"original\"", line]));
    }

    [Theory]
    [InlineData("missing", "override", "base")]
    [InlineData("selected", "", "base")]
    [InlineData("selected", "  ", "base")]
    [InlineData("selected", "override", "override")]
    public void ProfileResolution_RequiresSelectedNonBlankProvider(string active, string profileProvider, string expected)
    {
        Assert.Equal(expected, CodexModelProviderConfigResolver.ParseActiveModelProvider(
        [
            "profile = \"" + active + "\"", "model_provider = \"base\"",
            "[profiles.selected]", "model = \"ignored-model\"", "model_provider = \"" + profileProvider + "\"",
            "[model_providers.other]", "model_provider = \"must-not-become-top-level\""
        ]));
    }

    [Fact]
    public void EscapedQuotesAndQuotedHashes_ArePreserved_AndCommentsAreRemoved()
    {
        Assert.Equal("local\"#server", CodexModelProviderConfigResolver.ParseActiveModelProvider(
            ["\"ignored\"", "model_provider = \"local\\\"#server\" # actual comment"]));
        Assert.Null(CodexModelProviderConfigResolver.ParseActiveModelProvider(["model_provider = \" \""]));
    }

    [Fact]
    public void ConfigResolution_PrefersExplicitHome_ThenInfersHomeFromSessionsDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "codex-provider-" + Guid.NewGuid().ToString("N"));
        var explicitHome = Directory.CreateDirectory(Path.Combine(root, "explicit")).FullName;
        var inferredHome = Directory.CreateDirectory(Path.Combine(root, "inferred")).FullName;
        var sessions = Directory.CreateDirectory(Path.Combine(inferredHome, "sessions")).FullName;
        try
        {
            File.WriteAllText(Path.Combine(explicitHome, "config.toml"), "model_provider = \"explicit\"");
            File.WriteAllText(Path.Combine(inferredHome, "config.toml"), "model_provider = \"inferred\"");
            Assert.Equal("explicit", CodexModelProviderConfigResolver.ResolveActiveModelProvider(new() { CodexHomeDirectory = explicitHome }, sessions));
            Assert.Equal("inferred", CodexModelProviderConfigResolver.ResolveActiveModelProvider(new(), sessions + Path.DirectorySeparatorChar));
            Assert.Equal("explicit", CodexModelProviderConfigResolver.ResolveActiveModelProvider(new() { CodexHomeDirectory = explicitHome }, " "));
            using var locked = new FileStream(Path.Combine(explicitHome, "config.toml"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Null(CodexModelProviderConfigResolver.ResolveActiveModelProvider(new() { CodexHomeDirectory = explicitHome }, sessions));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void MissingResolverArguments_ProduceArgumentSpecificFailures()
    {
        Assert.Equal("clientOptions", Assert.Throws<ArgumentNullException>(() => CodexModelProviderConfigResolver.ResolveActiveModelProvider(null!, "root")).ParamName);
        Assert.Equal("sessionsRoot", Assert.Throws<ArgumentNullException>(() => CodexModelProviderConfigResolver.ResolveActiveModelProvider(new(), null!)).ParamName);
        Assert.Equal("lines", Assert.Throws<ArgumentNullException>(() => CodexModelProviderConfigResolver.ParseActiveModelProvider(null!)).ParamName);
    }

    [Fact]
    public void EmptyAssignment_CanClearProvider_AndCommentedLaterAssignmentsStayIgnored()
    {
        Assert.Null(CodexModelProviderConfigResolver.ParseActiveModelProvider(["model_provider = \"old\"", "model_provider = \"\""]));
        Assert.Equal("first", CodexModelProviderConfigResolver.ParseActiveModelProvider(["model_provider = \"first\" # model_provider = \"wrong\""]));
        Assert.Equal("last", CodexModelProviderConfigResolver.ParseActiveModelProvider(["model_provider = \"first\"", "profiles.bad]", "model_provider = \"last\"", "[profiles.good]", "model_provider = \"nested\"", "profile = \"good\""]));
    }

}
