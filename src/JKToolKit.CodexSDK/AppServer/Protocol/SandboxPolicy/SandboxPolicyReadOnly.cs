namespace JKToolKit.CodexSDK.AppServer.Protocol;

public abstract partial record class SandboxPolicy
{
    public sealed record class ReadOnly : SandboxPolicy
    {
        public override string Type => "readOnly";
    }
}

