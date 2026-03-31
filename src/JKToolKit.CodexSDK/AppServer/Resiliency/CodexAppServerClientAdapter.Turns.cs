namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct);

    Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct);

    Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct);

    Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct);

    Task<ReviewStartResult> ReviewAsync(ReviewStartOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct) => _inner.StartTurnAsync(threadId, options, ct);

    public Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct) => _inner.SteerTurnAsync(options, ct);

    public Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct) => _inner.SteerTurnRawAsync(options, ct);

    public Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct) => _inner.StartReviewAsync(options, ct);

    public Task<ReviewStartResult> ReviewAsync(ReviewStartOptions options, CancellationToken ct) => _inner.ReviewAsync(options, ct);
}
