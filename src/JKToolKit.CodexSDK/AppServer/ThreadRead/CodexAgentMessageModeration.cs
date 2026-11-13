namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>Moderation metadata associated with an agent message.</summary>
public sealed record class CodexAgentMessageModeration
{
    /// <summary>Gets whether the answer was moderated.</summary>
    public required bool Moderated { get; init; }

    /// <summary>Gets optional history-display instructions.</summary>
    public CodexAgentMessageDisplay? Display { get; init; }
}

/// <summary>A history-display instruction; unknown actions belong to the producing client.</summary>
public sealed record class CodexAgentMessageDisplay
{
    /// <summary>Gets the open-ended upstream action value.</summary>
    public required string Action { get; init; }

    /// <summary>Gets the optional block type.</summary>
    public string? BlockType { get; init; }

    /// <summary>Gets the optional trusted-access display hint.</summary>
    public bool? CyberTrustedAccess { get; init; }

    /// <summary>Gets the optional trusted-contact display hint.</summary>
    public bool? CheckTrustedContacts { get; init; }
}
