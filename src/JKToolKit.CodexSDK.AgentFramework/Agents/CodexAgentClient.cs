using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Creates Microsoft Agent Framework agents backed by Codex app-server.
/// </summary>
public sealed class CodexAgentClient
{
    private readonly Action<CodexSdkBuilder>? _configureSdk;

    /// <summary>
    /// Creates a Codex Agent Framework client.
    /// </summary>
    /// <param name="configureSdk">Optional Codex SDK configuration applied before each app-server run.</param>
    public CodexAgentClient(Action<CodexSdkBuilder>? configureSdk = null)
    {
        _configureSdk = configureSdk;
    }

    internal CodexSdk CreateSdk(IAppServerApprovalHandler? approvalHandler)
    {
        return CodexSdk.Create(builder =>
        {
            _configureSdk?.Invoke(builder);

            if (approvalHandler is not null)
            {
                builder.ConfigureAppServer(options =>
                {
                    options.ExperimentalApi = true;
                    options.ApprovalHandler = approvalHandler;
                });
            }
        });
    }
}
