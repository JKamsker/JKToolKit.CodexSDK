using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Creates Microsoft Agent Framework agents backed by Codex app-server.
/// </summary>
public sealed class CodexAgentClient
{
    private readonly Action<CodexSdkBuilder>? _configureSdk;
    private readonly CodexSdk? _sdk;

    /// <summary>
    /// Creates a Codex Agent Framework client.
    /// </summary>
    /// <param name="configureSdk">Optional Codex SDK configuration applied before each app-server run.</param>
    public CodexAgentClient(Action<CodexSdkBuilder>? configureSdk = null)
    {
        _configureSdk = configureSdk;
    }

    /// <summary>
    /// Creates a Codex Agent Framework client over an existing SDK facade.
    /// </summary>
    /// <param name="sdk">The configured Codex SDK facade to use for app-server runs.</param>
    public CodexAgentClient(CodexSdk sdk)
    {
        ArgumentNullException.ThrowIfNull(sdk);
        _sdk = sdk;
    }

    internal async ValueTask<CodexAgentAppServerLease> StartAppServerAsync(
        IAppServerApprovalHandler? approvalHandler,
        CancellationToken cancellationToken)
    {
        if (_sdk is not null)
        {
            var client = approvalHandler is null
                ? await _sdk.AppServer.StartAsync(cancellationToken).ConfigureAwait(false)
                : await _sdk.AppServer.StartAsync(
                    options => ConfigureDynamicToolApproval(options, approvalHandler),
                    cancellationToken).ConfigureAwait(false);
            return new CodexAgentAppServerLease(client, sdk: null);
        }

        var sdk = CreateSdk(approvalHandler);
        try
        {
            var client = await sdk.AppServer.StartAsync(cancellationToken).ConfigureAwait(false);
            return new CodexAgentAppServerLease(client, sdk);
        }
        catch
        {
            await sdk.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private CodexSdk CreateSdk(IAppServerApprovalHandler? approvalHandler)
    {
        return CodexSdk.Create(builder =>
        {
            _configureSdk?.Invoke(builder);
            builder.ConfigureAppServer(options => ConfigureDynamicToolApproval(options, approvalHandler));
        });
    }

    private static void ConfigureDynamicToolApproval(
        CodexAppServerClientOptions options,
        IAppServerApprovalHandler? approvalHandler)
    {
        if (approvalHandler is null)
        {
            return;
        }

        options.ExperimentalApi = true;
        options.ApprovalHandler = approvalHandler;
    }
}
