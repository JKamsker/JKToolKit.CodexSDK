using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;
using JKToolKit.CodexSDK.Exec.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Exec;

/// <summary>
/// Default implementation of the Codex client.
/// </summary>
public sealed class CodexClient : ICodexClient, IAsyncDisposable
{
    private readonly CodexClientOptions _clientOptions;
    private readonly ICodexProcessLauncher _processLauncher;
    private readonly ICodexSessionLocator _sessionLocator;
    private readonly IJsonlTailer _tailer;
    private readonly IJsonlEventParser _parser;
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILogger<CodexClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CodexSessionRunner _sessionRunner;
    private readonly CodexReviewRunner _reviewRunner;
    private readonly CodexRateLimitsReader _rateLimitsReader;

    /// <summary>
    /// Creates a CodexClient with default infrastructure implementations.
    /// </summary>
    public CodexClient()
        : this
        (
            Options.Create(new CodexClientOptions()),
            null,
            null,
            null,
            null,
            null,
            NullLoggerFactory.Instance.CreateLogger<CodexClient>(),
            NullLoggerFactory.Instance
        )
    {
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        return await _sessionRunner.ResumeSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken = default)
    {
        return await _sessionRunner.AttachToLogAsync(logFilePath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter = null, CancellationToken cancellationToken = default)
    {
        _clientOptions.Validate();
        var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);

        return _sessionLocator.ListSessionsAsync(sessionsRoot, filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default)
    {
        return await _reviewRunner.ReviewAsync(options, standardOutputWriter: null, standardErrorWriter: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a non-interactive code review and optionally mirrors Codex output as it is produced.
    /// </summary>
    /// <param name="options">Review configuration including scope and optional instructions.</param>
    /// <param name="standardOutputWriter">Optional writer that receives stdout as it is emitted.</param>
    /// <param name="standardErrorWriter">Optional writer that receives stderr as it is emitted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="CodexReviewResult"/> containing captured stdout/stderr and exit code.</returns>
    public async Task<CodexReviewResult> ReviewAsync(
        CodexReviewOptions options,
        TextWriter? standardOutputWriter,
        TextWriter? standardErrorWriter,
        CancellationToken cancellationToken = default)
    {
        return await _reviewRunner.ReviewAsync(options, standardOutputWriter, standardErrorWriter, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a CodexClient with explicit client options and default infrastructure implementations.
    /// </summary>
    public CodexClient
    (
        CodexClientOptions options,
        ICodexProcessLauncher? processLauncher = null,
        ICodexSessionLocator? sessionLocator = null,
        IJsonlTailer? tailer = null,
        IJsonlEventParser? parser = null,
        ICodexPathProvider? pathProvider = null,
        ILogger<CodexClient>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
        : this
        (
            options: Options.Create(options ?? throw new ArgumentNullException(nameof(options))),
            processLauncher,
            sessionLocator,
            tailer,
            parser,
            pathProvider,
            logger,
            loggerFactory
        )
    {
    }

    /// <summary>
    /// Primary constructor for dependency injection.
    /// </summary>
    public CodexClient
    (
        IOptions<CodexClientOptions> options,
        ICodexProcessLauncher? processLauncher = null,
        ICodexSessionLocator? sessionLocator = null,
        IJsonlTailer? tailer = null,
        IJsonlEventParser? parser = null,
        ICodexPathProvider? pathProvider = null,
        ILogger<CodexClient>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
    {
        _clientOptions = (options ?? throw new ArgumentNullException(nameof(options))).Value ?? throw new ArgumentNullException(nameof(options));

        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = logger ?? _loggerFactory.CreateLogger<CodexClient>();

        var fileSystem = new RealFileSystem();

        _pathProvider = pathProvider ?? new DefaultCodexPathProvider(fileSystem, _loggerFactory.CreateLogger<DefaultCodexPathProvider>());
        _processLauncher = processLauncher ?? new CodexProcessLauncher(_pathProvider, _loggerFactory.CreateLogger<CodexProcessLauncher>());
        _sessionLocator = sessionLocator ?? new CodexSessionLocator(fileSystem, _loggerFactory.CreateLogger<CodexSessionLocator>());
        _tailer = tailer ?? new JsonlTailer(fileSystem, _loggerFactory.CreateLogger<JsonlTailer>(), Options.Create(_clientOptions));
        _parser = parser ?? new JsonlEventParser(_loggerFactory.CreateLogger<JsonlEventParser>());
        _sessionRunner = new CodexSessionRunner(_clientOptions, _processLauncher, _sessionLocator, _tailer, _parser, _pathProvider, _loggerFactory, _logger);
        _reviewRunner = new CodexReviewRunner(_clientOptions, _processLauncher, _sessionLocator, _pathProvider, _logger);
        _rateLimitsReader = new CodexRateLimitsReader(_clientOptions, _sessionLocator, _tailer, _parser, _pathProvider, _logger);
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> StartSessionAsync
    (
        CodexSessionOptions options,
        CancellationToken cancellationToken = default
    )
    {
        return await _sessionRunner.StartSessionAsync(options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> ResumeSessionAsync
    (
        SessionId sessionId,
        CodexSessionOptions options,
        CancellationToken cancellationToken = default
    )
    {
        return await _sessionRunner.ResumeSessionAsync(sessionId, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to dispose; kept for compatibility with interface
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Retrieves the most recent rate limit snapshot emitted by Codex.
    /// </summary>
    /// <param name="noCache">When true, forces reading the latest session logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RateLimits if found; otherwise null.</returns>
    public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default) =>
        _rateLimitsReader.GetRateLimitsAsync(noCache, cancellationToken);
}
