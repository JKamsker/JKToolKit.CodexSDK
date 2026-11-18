namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>A version 1 answer presentation instruction.</summary>
public abstract record CodexAgentMessageVersionedDisplay
{
    /// <summary>Gets the supported wire contract version.</summary>
    public int Version => 1;
}

/// <summary>Shows an answer, optionally annotated with a wellbeing helpline for the given subject.</summary>
public sealed record CodexAgentMessageDisplayShow(
    CodexDisplayTrustedContacts TrustedContacts,
    CodexDisplayHelplineSubject? HelplineSubject) : CodexAgentMessageVersionedDisplay;

/// <summary>Hides an answer and replaces it with the wellbeing prompt helpline.</summary>
public sealed record CodexAgentMessageDisplayHide : CodexAgentMessageVersionedDisplay;

/// <summary>Blocks an answer with a protection reason.</summary>
public sealed record CodexAgentMessageDisplayProtection(string ProtectionType) : CodexAgentMessageVersionedDisplay;

/// <summary>Blocks an answer with a cyber reason and optional trusted-access hint.</summary>
public sealed record CodexAgentMessageDisplayCyber(bool? TrustedAccess) : CodexAgentMessageVersionedDisplay;

/// <summary>The trusted-contact presentation hint.</summary>
public enum CodexDisplayTrustedContacts
{
    /// <summary>Check trusted contacts.</summary>
    Check,
    /// <summary>Skip checking trusted contacts.</summary>
    Skip
}

/// <summary>The subject of a wellbeing helpline annotation.</summary>
public enum CodexDisplayHelplineSubject
{
    /// <summary>The answer.</summary>
    Answer,
    /// <summary>The prompt.</summary>
    Prompt
}
