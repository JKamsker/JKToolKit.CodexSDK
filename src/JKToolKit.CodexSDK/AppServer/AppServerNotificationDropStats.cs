namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Snapshot counters describing how many app-server notifications were dropped from bounded buffers.
/// </summary>
/// <remarks>
/// Notification streams in this SDK use bounded, drop-oldest queues. If consumers are too slow (or do not read),
/// older notifications are discarded to avoid blocking the JSON-RPC read loop.
/// </remarks>
public sealed record class AppServerNotificationDropStats(
    long GlobalNotificationsDropped,
    long GlobalRawNotificationsDropped,
    long TurnNotificationsDropped,
    long TurnRawNotificationsDropped,
    long BufferedTurnNotificationsDroppedCapacity,
    long BufferedTurnNotificationsDroppedTtl);

