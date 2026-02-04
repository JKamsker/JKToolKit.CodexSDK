namespace JKToolKit.CodexSDK.AppServer.Protocol;

public abstract partial record class SandboxPolicy
{
    public sealed record class DangerFullAccess : SandboxPolicy
    {
        public override string Type => "dangerFullAccess";
    }
}

