using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpRegistrationScopeTests
{
    [Theory]
    [InlineData(null, "docs")]
    [InlineData("thr_1", null)]
    [InlineData(null, null)]
    [InlineData("", "docs")]
    [InlineData("thr_1", " ")]
    public async Task StatusList_RegistrationScopeRequiresThreadAndServer(string? threadId, string? serverName)
    {
        var client = new CodexAppServerMcpClient((_, _, _) =>
            throw new InvalidOperationException("Invalid requests must not be sent."));

        var act = () => client.ListMcpServerStatusAsync(new McpServerStatusListOptions
        {
            EnvironmentId = "env_docs",
            ThreadId = threadId,
            ServerName = serverName
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*EnvironmentId requires ServerName and ThreadId*");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"environmentId\":null}")]
    public void StatusList_OlderOrThreadWideRegistrationHasNoEnvironment(string fields)
    {
        using var doc = JsonDocument.Parse(fields);
        var server = new Dictionary<string, object?> { ["name"] = "docs" };
        foreach (var property in doc.RootElement.EnumerateObject())
            server[property.Name] = property.Value.Clone();

        var result = JsonSerializer.SerializeToElement(new { data = new[] { server } });
        var page = CodexAppServerClientMcpParsers.ParseMcpServerStatusListPage(result);

        page.Servers.Should().ContainSingle().Which.EnvironmentId.Should().BeNull();
    }
}
