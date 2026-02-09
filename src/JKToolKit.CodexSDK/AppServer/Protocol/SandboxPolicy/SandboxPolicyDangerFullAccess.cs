namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Sandbox policy that allows full access (no sandbox restrictions).
    /// </summary>
    public sealed record class DangerFullAccess : SandboxPolicy
    {
        /// <inheritdoc />
        public override string Type => "dangerFullAccess";
    }
}
