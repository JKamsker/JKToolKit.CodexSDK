namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Identifies the kind of operation being performed against the app-server connection.
/// </summary>
public enum CodexAppServerOperationKind
{
    /// <summary>
    /// A user-initiated JSON-RPC request via <c>CallAsync</c>.
    /// </summary>
    Call = 0,

    /// <summary>
    /// A user-initiated <c>thread/start</c> request.
    /// </summary>
    StartThread = 1,

    /// <summary>
    /// A user-initiated <c>thread/resume</c> request.
    /// </summary>
    ResumeThread = 2,

    /// <summary>
    /// A user-initiated <c>turn/start</c> request.
    /// </summary>
    StartTurn = 3,

    /// <summary>
    /// The internal notifications pump that bridges <c>Notifications()</c> across restarts.
    /// </summary>
    NotificationsPump = 4,

    /// <summary>
    /// A user-initiated thread management request other than thread start/resume.
    /// </summary>
    ThreadManagement = 5,

    /// <summary>
    /// A user-initiated configuration or account request.
    /// </summary>
    Configuration = 6,

    /// <summary>
    /// A user-initiated MCP request.
    /// </summary>
    Mcp = 7,

    /// <summary>
    /// A user-initiated skills or apps request.
    /// </summary>
    SkillsAndApps = 8,

    /// <summary>
    /// A user-initiated fuzzy file search request.
    /// </summary>
    FuzzyFileSearch = 9,

    /// <summary>
    /// A user-initiated turn control request other than turn start.
    /// </summary>
    TurnControl = 10,

    /// <summary>
    /// A user-initiated plugin request.
    /// </summary>
    Plugins = 11,

    /// <summary>
    /// A user-initiated standalone command execution request.
    /// </summary>
    CommandExecution = 12,

    /// <summary>
    /// A user-initiated filesystem watcher request.
    /// </summary>
    Filesystem = 13,

    /// <summary>
    /// A user-initiated collaboration mode request.
    /// </summary>
    CollaborationModes = 14,

    /// <summary>
    /// A user-initiated thread realtime request.
    /// </summary>
    ThreadRealtime = 15
}
