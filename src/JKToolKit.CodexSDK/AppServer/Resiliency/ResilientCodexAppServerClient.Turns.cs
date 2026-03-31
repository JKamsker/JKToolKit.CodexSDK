#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.StartTurn, (c, token) => c.StartTurnAsync(threadId, options, token), ct);

    public Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.TurnControl, (c, token) => c.SteerTurnAsync(options, token), ct);

    public Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.TurnControl, (c, token) => c.SteerTurnRawAsync(options, token), ct);

    public Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.TurnControl, (c, token) => c.StartReviewAsync(options, token), ct);

    public Task<ReviewStartResult> ReviewAsync(ReviewStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.TurnControl, (c, token) => c.ReviewAsync(options, token), ct);
}

#pragma warning restore CS1591
