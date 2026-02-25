using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerClientCore
{
    private AppServerNotification SafeMap(string method, JsonElement @params)
    {
        try
        {
            return AppServerNotificationMapper.Map(method, @params);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "App-server notification mapper failed (method={Method}).", method);
            return new UnknownNotification(method, @params);
        }
    }
}
