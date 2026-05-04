using System.Runtime.CompilerServices;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed class LineJsonRpcMessageTransport : IJsonRpcMessageTransport
{
    private readonly TextReader _reader;
    private readonly TextWriter _writer;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LineJsonRpcMessageTransport(TextReader reader, TextWriter writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public Task Completion => _completion.Task;

    public async Task SendAsync(string message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _writer.WriteLineAsync(message.AsMemory(), ct).ConfigureAwait(false);
        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await _reader.ReadLineAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _completion.TrySetResult();
                yield break;
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
                throw;
            }

            if (line is null)
            {
                _completion.TrySetResult();
                yield break;
            }

            yield return line;
        }
    }

    public ValueTask DisposeAsync()
    {
        _completion.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
