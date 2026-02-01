using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

public sealed record CodexThread(string Id, JsonElement Raw);

