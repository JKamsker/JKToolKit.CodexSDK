using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class PluginPolicyValueTests
{
    [Fact]
    public void PluginInstallPolicySource_DefaultAndFailedTryParse_AreSafe()
    {
        var defaultSource = default(PluginInstallPolicySource);

        defaultSource.Value.Should().BeEmpty();
        ((string)defaultSource).Should().BeEmpty();
        defaultSource.ToString().Should().BeEmpty();

        PluginInstallPolicySource.TryParse(null, out var parsed).Should().BeFalse();
        parsed.Should().Be(defaultSource);
        parsed.Value.Should().BeEmpty();
    }
}
