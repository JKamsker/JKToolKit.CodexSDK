using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;

// ReSharper disable once CheckNamespace
namespace JKToolKit.CodexSDK;

/// <summary>
/// Specifies which Codex review mechanism to use when routing via <see cref="CodexSdk"/>.
/// </summary>
public enum CodexSdkReviewMode
{
    /// <summary>
    /// Uses exec-mode (<c>codex review</c>) and returns a <see cref="CodexReviewResult"/>.
    /// </summary>
    Exec,

    /// <summary>
    /// Uses app-server review (<c>review/start</c>) and returns a running turn (<see cref="ReviewStartResult"/>).
    /// </summary>
    AppServer
}

/// <summary>
/// Options for <see cref="CodexSdk.ReviewAsync(CodexSdkReviewOptions,System.Threading.CancellationToken)"/>.
/// </summary>
public sealed class CodexSdkReviewOptions
{
    /// <summary>
    /// Gets or sets which review mechanism to use.
    /// </summary>
    public CodexSdkReviewMode Mode { get; set; } = CodexSdkReviewMode.Exec;

    /// <summary>
    /// Gets or sets exec-mode review options (required when <see cref="Mode"/> is <see cref="CodexSdkReviewMode.Exec"/>).
    /// </summary>
    public CodexReviewOptions? Exec { get; set; }

    /// <summary>
    /// Gets or sets app-server review options (required when <see cref="Mode"/> is <see cref="CodexSdkReviewMode.AppServer"/>).
    /// </summary>
    public CodexSdkAppServerReviewOptions? AppServer { get; set; }
}

/// <summary>
/// Options for routing reviews through the app-server (<c>review/start</c>) via <see cref="CodexSdk"/>.
/// </summary>
public sealed class CodexSdkAppServerReviewOptions
{
    /// <summary>
    /// Gets or sets options for starting the thread that will host the review.
    /// </summary>
    public required ThreadStartOptions Thread { get; init; }

    /// <summary>
    /// Gets or sets the review delivery mode.
    /// </summary>
    public ReviewDelivery? Delivery { get; init; }

    /// <summary>
    /// Gets or sets the review target.
    /// </summary>
    public required ReviewTarget Target { get; init; }
}

/// <summary>
/// Represents a routed review result from <see cref="CodexSdk"/>.
/// </summary>
public sealed class CodexSdkReviewResult : IAsyncDisposable
{
    internal CodexSdkReviewResult() { }

    /// <summary>
    /// Gets which review mechanism was used.
    /// </summary>
    public required CodexSdkReviewMode Mode { get; init; }

    /// <summary>
    /// Gets the exec-mode review result when <see cref="Mode"/> is <see cref="CodexSdkReviewMode.Exec"/>.
    /// </summary>
    public CodexReviewResult? Exec { get; init; }

    /// <summary>
    /// Gets the app-server review session when <see cref="Mode"/> is <see cref="CodexSdkReviewMode.AppServer"/>.
    /// </summary>
    public CodexSdkAppServerReviewSession? AppServer { get; init; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() =>
        AppServer is { } session ? session.DisposeAsync() : ValueTask.CompletedTask;
}

/// <summary>
/// Represents an app-server review session created by <see cref="CodexSdk"/>.
/// </summary>
public sealed class CodexSdkAppServerReviewSession : IAsyncDisposable
{
    private int _disposed;

    internal CodexSdkAppServerReviewSession(CodexAppServerClient client, CodexThread thread, ReviewStartResult review)
    {
        Client = client;
        Thread = thread;
        Review = review;
    }

    /// <summary>
    /// Gets the underlying app-server client. Disposing this session disposes the client.
    /// </summary>
    public CodexAppServerClient Client { get; }

    /// <summary>
    /// Gets the thread created to host the review.
    /// </summary>
    public CodexThread Thread { get; }

    /// <summary>
    /// Gets the review start result, including the running review turn handle.
    /// </summary>
    public ReviewStartResult Review { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await Review.Turn.DisposeAsync();
        }
        finally
        {
            await Client.DisposeAsync();
        }
    }
}
