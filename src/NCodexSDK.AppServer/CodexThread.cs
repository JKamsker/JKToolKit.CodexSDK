using System.Text.Json;

namespace NCodexSDK.AppServer;

public sealed record CodexThread(string Id, JsonElement Raw);

