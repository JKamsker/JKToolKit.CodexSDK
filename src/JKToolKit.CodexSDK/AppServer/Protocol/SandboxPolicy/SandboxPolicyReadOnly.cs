namespace JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

public abstract partial record class SandboxPolicy
{
    /// <summary>
    /// Sandbox policy that disallows writing (read-only).
    /// </summary>
    public sealed record class ReadOnly : SandboxPolicy
    {
        /// <inheritdoc />
        public override string Type => "readOnly";
    }
}
