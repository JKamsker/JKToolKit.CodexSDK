namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Provides built-in retry policies for resilient app-server clients.
/// </summary>
public static class CodexAppServerRetryPolicy
{
    /// <summary>
    /// Never retries user operations. (The subprocess may still be restarted for future operations.)
    /// </summary>
    public static CodexAppServerRetryPolicyDelegate NeverRetry { get; } = _ =>
        new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.NoRetry);
}

/// <summary>
/// Delegate used to decide whether an operation should be retried after a restart.
/// </summary>
public delegate ValueTask<CodexAppServerRetryDecision> CodexAppServerRetryPolicyDelegate(CodexAppServerRetryContext ctx);

