using System.IO;
using System.Text.Json;

namespace CodexUsageCompanion.RateLimits;

public static class JsonLineRpcClient
{
    public static async Task<string> ReadResponseAsync(
        TextReader reader,
        long expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new EndOfStreamException($"Response {expectedId} was not received.");
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.Number ||
                id.GetInt64() != expectedId)
            {
                continue;
            }

            return line;
        }
    }
}

public sealed class JsonLineRpcConnection : IAsyncDisposable
{
    private readonly TextReader _reader;
    private readonly TextWriter _writer;
    private readonly object _sync = new();
    private readonly Dictionary<long, TaskCompletionSource<string>> _pending = new();
    private readonly Dictionary<long, string> _futureResponses = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _readerTask;
    private Task? _disposeTask;
    private Exception? _terminalError;
    private long _highestRequestId;
    private bool _disposed;

    public JsonLineRpcConnection(TextReader reader, TextWriter writer)
    {
        _reader = reader;
        _writer = writer;
    }

    public event Action<string>? NotificationReceived;

    public async Task<string> SendRequestAsync(
        long id,
        string request,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<string>? completion = null;
        string? futureResponse = null;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _highestRequestId = Math.Max(_highestRequestId, id);
            if (_futureResponses.Remove(id, out var existing))
            {
                futureResponse = existing;
            }
            else if (_terminalError is not null)
            {
                throw new InvalidOperationException("Codex app-server connection is closed.", _terminalError);
            }
            else
            {
                completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending.Add(id, completion);
            }

            _readerTask ??= ReadLoopAsync();
        }

        try
        {
            await SendNotificationAsync(request, cancellationToken);
            return futureResponse is not null
                ? ValidateResponse(futureResponse)
                : ValidateResponse(await completion!.Task.WaitAsync(cancellationToken));
        }
        catch
        {
            if (completion is not null)
            {
                lock (_sync)
                {
                    _pending.Remove(id);
                }
            }

            throw;
        }
    }

    public async Task SendNotificationAsync(string notification, CancellationToken cancellationToken)
    {
        CancellationToken connectionCancellationToken;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            connectionCancellationToken = _cancellation.Token;
        }

        using var writeCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            connectionCancellationToken);
        await _writeGate.WaitAsync(writeCancellation.Token);
        try
        {
            await _writer.WriteLineAsync(notification.AsMemory(), writeCancellation.Token);
            await _writer.FlushAsync(writeCancellation.Token);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            _disposed = true;
            _disposeTask = DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        // Canceling the reader alone leaves callers waiting on response tasks forever.
        FailConnection(new ObjectDisposedException(nameof(JsonLineRpcConnection)));
        _futureResponses.Clear();
        _cancellation.Cancel();
        if (_readerTask is not null)
        {
            try
            {
                await _readerTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _writeGate.WaitAsync();
        _writeGate.Release();
        _writeGate.Dispose();
        _cancellation.Dispose();
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(_cancellation.Token);
                if (line is null)
                {
                    throw new EndOfStreamException("Codex app-server output closed.");
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                {
                    RouteResponse(id.GetInt64(), line);
                    continue;
                }

                if (root.TryGetProperty("method", out var method) && method.ValueKind == JsonValueKind.String)
                {
                    NotificationReceived?.Invoke(method.GetString()!);
                }
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            FailConnection(exception);
        }
    }

    private void RouteResponse(long id, string line)
    {
        TaskCompletionSource<string>? completion;
        lock (_sync)
        {
            if (!_pending.Remove(id, out completion))
            {
                if (id > _highestRequestId && _futureResponses.Count < 16)
                {
                    _futureResponses[id] = line;
                }

                return;
            }
        }

        completion.SetResult(line);
    }

    private void FailConnection(Exception exception)
    {
        TaskCompletionSource<string>[] pending;
        lock (_sync)
        {
            _terminalError = exception;
            pending = _pending.Values.ToArray();
            _pending.Clear();
        }

        foreach (var completion in pending)
        {
            completion.TrySetException(exception);
        }
    }

    private static string ValidateResponse(string response)
    {
        using var document = JsonDocument.Parse(response);
        if (!document.RootElement.TryGetProperty("error", out var error) ||
            error.ValueKind != JsonValueKind.Object)
        {
            return response;
        }

        var message = error.TryGetProperty("message", out var messageElement) &&
                      messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : "Unknown JSON-RPC error";
        throw new InvalidOperationException($"Codex app-server error: {message}");
    }
}
