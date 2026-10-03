using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>Wraps turn startup for both collected and streaming high-level calls.</summary>
/// <remarks>Call next once to start the turn. Use the returned handle's Subscribe and Completion surfaces for observation.</remarks>
public interface ICodexTurnMiddleware
{
    /// <summary>Starts a turn through the remaining pipeline; exceptions propagate to the caller.</summary>
    Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct);
}

/// <summary>Context shared by middleware during turn startup.</summary>
/// <param name="ThreadId">The owning thread identifier.</param>
/// <param name="Options">The turn options that will be sent to the server.</param>
public sealed record CodexTurnContext(string ThreadId, TurnStartOptions Options);
