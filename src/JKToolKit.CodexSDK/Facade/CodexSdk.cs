using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.McpServer;

// ReSharper disable once CheckNamespace
namespace JKToolKit.CodexSDK;

/// <summary>
/// A single, discoverable entry point for the three supported Codex integration modes:
/// Exec, AppServer, and McpServer.
/// </summary>
/// <remarks>
/// This facade is additive only. Existing entry points (<c>CodexClient</c>,
/// <c>CodexAppServerClient</c>, <c>CodexMcpServerClient</c>) remain supported and unchanged.
/// </remarks>
public sealed class CodexSdk : IAsyncDisposable
{
    private readonly ICodexClient _exec;
    private readonly bool _ownsExec;

    /// <summary>
    /// Gets the facade for the <c>codex exec</c> mode.
    /// </summary>
    public CodexExecFacade Exec { get; }

    /// <summary>
    /// Gets the facade for the <c>codex app-server</c> mode.
    /// </summary>
    public CodexAppServerFacade AppServer { get; }

    /// <summary>
    /// Gets the facade for the <c>codex mcp-server</c> mode.
    /// </summary>
    public CodexMcpServerFacade McpServer { get; }

    /// <summary>
    /// Creates a new <see cref="CodexSdk"/> using dependencies provided by DI.
    /// </summary>
    public CodexSdk(
        ICodexClient exec,
        ICodexAppServerClientFactory appServer,
        ICodexMcpServerClientFactory mcpServer)
        : this(exec, appServer, mcpServer, ownsExec: false)
    {
    }

    private CodexSdk(
        ICodexClient exec,
        ICodexAppServerClientFactory appServer,
        ICodexMcpServerClientFactory mcpServer,
        bool ownsExec)
    {
        ArgumentNullException.ThrowIfNull(exec);
        ArgumentNullException.ThrowIfNull(appServer);
        ArgumentNullException.ThrowIfNull(mcpServer);

        _exec = exec;
        _ownsExec = ownsExec;

        Exec = new CodexExecFacade(exec);
        AppServer = new CodexAppServerFacade(appServer);
        McpServer = new CodexMcpServerFacade(mcpServer);
    }

    /// <summary>
    /// Creates a <see cref="CodexSdk"/> without using DI.
    /// </summary>
    /// <param name="configure">Optional configuration for the builder.</param>
    public static CodexSdk Create(Action<CodexSdkBuilder>? configure = null)
    {
        var builder = new CodexSdkBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Runs a non-interactive code review using exec-mode (<c>codex review</c>).
    /// </summary>
    public Task<CodexReviewResult> ReviewExecAsync(CodexReviewOptions options, CancellationToken ct = default) =>
        Exec.ReviewAsync(options, ct);

    /// <summary>
    /// Starts an app-server review (<c>review/start</c>) by creating a thread and returning a running turn.
    /// </summary>
    public async Task<CodexSdkAppServerReviewSession> ReviewAppServerAsync(CodexSdkAppServerReviewOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Thread);
        ArgumentNullException.ThrowIfNull(options.Target);

        CodexAppServerClient? client = null;
        try
        {
            client = await AppServer.StartAsync(ct);

            var thread = await client.StartThreadAsync(options.Thread, ct);
            var review = await client.StartReviewAsync(new ReviewStartOptions
            {
                ThreadId = thread.Id,
                Delivery = options.Delivery,
                Target = options.Target
            }, ct);

            return new CodexSdkAppServerReviewSession(client, thread, review);
        }
        catch
        {
            if (client is not null)
            {
                await client.DisposeAsync();
            }

            throw;
        }
    }

    /// <summary>
    /// Runs a review using either exec-mode or app-server mode based on <paramref name="options"/>.
    /// </summary>
    public async Task<CodexSdkReviewResult> ReviewAsync(CodexSdkReviewOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Mode switch
        {
            CodexSdkReviewMode.Exec => new CodexSdkReviewResult
            {
                Mode = CodexSdkReviewMode.Exec,
                Exec = await ReviewExecAsync(options.Exec ?? throw new ArgumentException("Exec options are required when Mode is Exec.", nameof(options)), ct)
            },
            CodexSdkReviewMode.AppServer => new CodexSdkReviewResult
            {
                Mode = CodexSdkReviewMode.AppServer,
                AppServer = await ReviewAppServerAsync(options.AppServer ?? throw new ArgumentException("AppServer options are required when Mode is AppServer.", nameof(options)), ct)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(options.Mode), options.Mode, "Unknown review mode.")
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_ownsExec)
        {
            return;
        }

        if (_exec is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        _exec.Dispose();
    }

    internal static CodexSdk CreateOwned(
        ICodexClient exec,
        ICodexAppServerClientFactory appServer,
        ICodexMcpServerClientFactory mcpServer) =>
        new(exec, appServer, mcpServer, ownsExec: true);
}
