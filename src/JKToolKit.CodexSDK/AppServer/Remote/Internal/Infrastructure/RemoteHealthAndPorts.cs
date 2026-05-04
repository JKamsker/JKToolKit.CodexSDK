using System.Net;
using System.Net.Sockets;

namespace JKToolKit.CodexSDK.AppServer.Remote.Internal;

internal interface IRemoteAppServerHealthProbe
{
    Task<bool> IsReadyAsync(Uri webSocketUri, TimeSpan timeout, CancellationToken ct);
}

internal sealed class RemoteAppServerHealthProbe : IRemoteAppServerHealthProbe
{
    private static readonly HttpClient Http = new();

    public async Task<bool> IsReadyAsync(Uri webSocketUri, TimeSpan timeout, CancellationToken ct)
    {
        var readyUri = ToReadyUri(webSocketUri);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCts.CancelAfter(timeout);
        }

        try
        {
            using var response = await Http.GetAsync(readyUri, timeoutCts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static Uri ToReadyUri(Uri webSocketUri)
    {
        var builder = new UriBuilder(webSocketUri)
        {
            Scheme = webSocketUri.Scheme == "wss" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
            Path = "readyz",
            Query = null,
            Fragment = null
        };
        return builder.Uri;
    }
}

internal static class LocalPortAllocator
{
    public static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
