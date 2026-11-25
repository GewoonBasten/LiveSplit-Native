using System.Net.WebSockets;
using System.Text;

namespace LiveSplitNative.Core.Racing;

/// <summary>The race's websocket (ClientWebSocket on racetime.gg; a fake in tests).</summary>
public interface IRacetimeSocket : IDisposable
{
    bool IsOpen { get; }

    /// <summary>Closed by a close handshake (not aborted): no reconnect.</summary>
    bool ClosedCleanly { get; }

    Task ConnectAsync(Uri uri, string accessToken, CancellationToken token);

    Task SendAsync(string text, CancellationToken token);

    /// <summary>The next whole message, or null when the socket closed.</summary>
    Task<string?> ReceiveAsync(CancellationToken token);
}

/// <summary>RacetimeChannel's ClientWebSocket with the Bearer header, messages up to 2 MiB.</summary>
public sealed class ClientRacetimeSocket : IRacetimeSocket
{
    public const int BufferSize = 20480;
    public const int MaxBufferSize = 2097152;

    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sending = new(1, 1);

    public bool IsOpen => _socket.State == WebSocketState.Open;

    public bool ClosedCleanly => _socket.State is WebSocketState.CloseSent or WebSocketState.CloseReceived or WebSocketState.Closed;

    public Task ConnectAsync(Uri uri, string accessToken, CancellationToken token)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
        return _socket.ConnectAsync(uri, token);
    }

    public async Task SendAsync(string text, CancellationToken token)
    {
        await _sending.WaitAsync(token);
        try
        {
            await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, token);
        }
        finally
        {
            _sending.Release();
        }
    }

    public async Task<string?> ReceiveAsync(CancellationToken token)
    {
        byte[] buffer = new byte[BufferSize];
        int read = 0;
        WebSocketReceiveResult result;
        do
        {
            if (read == buffer.Length)
            {
                if (buffer.Length + BufferSize > MaxBufferSize)
                {
                    // Too big: the rest of the message is read and dropped.
                    while (!(await _socket.ReceiveAsync(buffer, token)).EndOfMessage)
                    {
                    }

                    return "";
                }

                Array.Resize(ref buffer, buffer.Length + BufferSize);
            }

            result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer, read, buffer.Length - read), token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            read += result.Count;
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    public void Dispose()
    {
        _socket.Dispose();
        _sending.Dispose();
    }
}
