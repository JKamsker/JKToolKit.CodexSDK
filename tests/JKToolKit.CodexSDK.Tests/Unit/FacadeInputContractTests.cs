using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Facade;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class FacadeInputContractTests
{
    [Fact]
    public async Task UserText_IsSentUnchangedAsUserInput()
    {
        const string prompt = "  user text: Café\nsecond line  ";
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var handle = await thread.RunStreamedAsync(CodexInput.Text(prompt));
        var input = fixture.Rpc.LastTurn.GetProperty("input");
        Assert.Equal(1, input.GetArrayLength());
        Assert.Equal("text", input[0].GetProperty("type").GetString());
        Assert.Equal(prompt, input[0].GetProperty("text").GetString());
        Assert.False(fixture.Rpc.LastTurn.TryGetProperty("toolOutput", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ExternalMessage_RequiresItsSourceSoContentCannotBecomeUserInput(string? source) =>
        Assert.ThrowsAny<ArgumentException>(() => CodexInput.ExternalMessage(source!, "external content"));

    [Fact]
    public async Task ConfiguredThreadStart_PreservesCallerOptionsOnSharedConnection()
    {
        await using var fixture = new Fixture();
        var options = new ThreadStartOptions
        {
            Cwd = Directory.GetCurrentDirectory(), Model = "custom-model",
            DeveloperInstructions = "developer instructions", Ephemeral = true
        };
        var thread = await fixture.Threads.StartAsync(options);
        Assert.Equal("t", thread.Id);
        Assert.Equal(options.Cwd, fixture.Rpc.LastThread.GetProperty("cwd").GetString());
        Assert.Equal("custom-model", fixture.Rpc.LastThread.GetProperty("model").GetString());
        Assert.Equal(options.DeveloperInstructions, fixture.Rpc.LastThread.GetProperty("developerInstructions").GetString());
        Assert.True(fixture.Rpc.LastThread.GetProperty("ephemeral").GetBoolean());
    }

    [Theory]
    [InlineData("\"probe.cmd")]
    [InlineData("\rprobe.cmd")]
    [InlineData("\nprobe.cmd")]
    [InlineData("\0probe.cmd")]
    public void BatchPreflight_RejectsUnrepresentableLeadingPathCharacters(string path) =>
        Assert.Throws<ArgumentException>(() => CodexRuntime.CreateVersionStartInfo(path, true));
}
