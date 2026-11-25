using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using LiveSplitNative.Core.Options;

namespace LiveSplitNative.Core.Server;

/// <summary>
/// Port of LiveSplit.Server.CommandServer's servers. TCP (StartTcp): every line a client sends
/// is one command, each answer goes back as a line ("\n"). WebSocket (StartWs, websocket-sharp's
/// WebSocketServer with the "/livesplit" service): ws://&lt;this computer&gt;:&lt;Server Port&gt;/livesplit,
/// every text message one command, each answer a text message. One of them runs at a time.
/// </summary>
public sealed class CommandServer : IDisposable
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private readonly Func<string, Task<string?>> _process;
    private readonly object _lock = new();
    private readonly List<WebSocket> _clients = [];
    private readonly List<TcpClient> _tcpClients = [];
    private TcpListener? _listener;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _tcpCancel;
    private CancellationTokenSource? _cancel;

    /// <param name="process">Runs one command (on the timer's thread) and gives its answer.</param>
    public CommandServer(Func<string, Task<string?>> process) => _process = process;

    public ServerStateType ServerState { get; private set; } = ServerStateType.Off;

    /// <summary>The port the running server listens on (useful when it was started on port 0).</summary>
    public int Port => ((_listener ?? _tcpListener)?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    /// <summary>StartTcp: listens on every network interface at the port.</summary>
    public void StartTcp(int port)
    {
        StopTcp();
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _tcpListener = listener;
        _tcpCancel = new CancellationTokenSource();
        _ = AcceptTcpAsync(listener, _tcpCancel.Token);
        ServerState = ServerStateType.TCP;
    }

    public void StopTcp()
    {
        _tcpCancel?.Cancel();
        _tcpCancel = null;
        _tcpListener?.Stop();
        _tcpListener = null;
        lock (_lock)
        {
            foreach (TcpClient client in _tcpClients)
            {
                client.Dispose();
            }

            _tcpClients.Clear();
        }

        if (ServerState == ServerStateType.TCP)
        {
            ServerState = ServerStateType.Off;
        }
    }

    /// <summary>StopAll.</summary>
    public void StopAll()
    {
        StopTcp();
        StopWs();
    }

    private async Task AcceptTcpAsync(TcpListener listener, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancel);
            }
            catch (Exception)
            {
                return;
            }

            _ = ServeTcpAsync(client, cancel);
        }
    }

    // Connection.ReadCommands / SendMessage: UTF-8 lines in, "\n"-ended lines out.
    private async Task ServeTcpAsync(TcpClient client, CancellationToken cancel)
    {
        lock (_lock)
        {
            _tcpClients.Add(client);
        }

        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                using var reader = new StreamReader(stream, new UTF8Encoding(false), false);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" };
                while (!cancel.IsCancellationRequested)
                {
                    string? command = await reader.ReadLineAsync(cancel);
                    if (command is null)
                    {
                        break;
                    }

                    string? response = await _process(command);
                    if (!string.IsNullOrEmpty(response))
                    {
                        await writer.WriteLineAsync(response);
                        await writer.FlushAsync(cancel);
                    }
                }
            }
        }
        catch (Exception)
        {
            // The client went away or the server stopped.
        }
        finally
        {
            lock (_lock)
            {
                _tcpClients.Remove(client);
            }
        }
    }

    /// <summary>StartWs: listens on every network interface at the port.</summary>
    public void StartWs(int port)
    {
        StopWs();
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        _listener = listener;
        _cancel = new CancellationTokenSource();
        _ = AcceptAsync(listener, _cancel.Token);
        ServerState = ServerStateType.Websocket;
    }

    public void StopWs()
    {
        _cancel?.Cancel();
        _cancel = null;
        _listener?.Stop();
        _listener = null;
        lock (_lock)
        {
            foreach (WebSocket client in _clients)
            {
                client.Abort();
                client.Dispose();
            }

            _clients.Clear();
        }

        if (ServerState == ServerStateType.Websocket)
        {
            ServerState = ServerStateType.Off;
        }
    }

    public void Dispose() => StopAll();

    private async Task AcceptAsync(TcpListener listener, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancel);
            }
            catch (Exception)
            {
                return;
            }

            _ = ServeAsync(client, cancel);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancel)
    {
        using (client)
        {
            NetworkStream stream = client.GetStream();
            WebSocket? socket = null;
            try
            {
                socket = await HandshakeAsync(stream, cancel);
                if (socket is null)
                {
                    return;
                }

                lock (_lock)
                {
                    _clients.Add(socket);
                }

                byte[] buffer = new byte[8192];
                while (socket.State == WebSocketState.Open && !cancel.IsCancellationRequested)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, cancel);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                            return;
                        }

                        message.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        continue;
                    }

                    string? response = await _process(Encoding.UTF8.GetString(message.ToArray()));
                    if (!string.IsNullOrEmpty(response))
                    {
                        await socket.SendAsync(Encoding.UTF8.GetBytes(response), WebSocketMessageType.Text, true, cancel);
                    }
                }
            }
            catch (Exception)
            {
                // The client went away or the server stopped.
            }
            finally
            {
                if (socket is not null)
                {
                    lock (_lock)
                    {
                        _clients.Remove(socket);
                    }

                    socket.Dispose();
                }
            }
        }
    }

    /// <summary>The HTTP upgrade (RFC 6455) for /livesplit; anything else is answered and closed.</summary>
    private static async Task<WebSocket?> HandshakeAsync(NetworkStream stream, CancellationToken cancel)
    {
        string? request = await ReadHeaderAsync(stream, cancel);
        if (request is null)
        {
            return null;
        }

        string[] lines = request.Split("\r\n");
        string[] requestLine = lines[0].Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            int colon = line.IndexOf(':');
            if (colon > 0)
            {
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }

        string path = requestLine.Length > 1 ? requestLine[1].Split('?')[0].TrimEnd('/') : "";
        if (requestLine[0] != "GET" || path != "/livesplit")
        {
            await WriteAsync(stream, "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", cancel);
            return null;
        }

        if (!headers.TryGetValue("Sec-WebSocket-Key", out string? key)
            || !headers.TryGetValue("Upgrade", out string? upgrade)
            || !upgrade.Contains("websocket", StringComparison.OrdinalIgnoreCase))
        {
            await WriteAsync(stream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", cancel);
            return null;
        }

        await WriteAsync(stream, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + $"Sec-WebSocket-Accept: {AcceptKey(key)}\r\n\r\n", cancel);
        return WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.FromSeconds(30) });
    }

    public static string AcceptKey(string key) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key.Trim() + WebSocketGuid)));

    private static async Task<string?> ReadHeaderAsync(NetworkStream stream, CancellationToken cancel)
    {
        // Byte by byte, so nothing after the header is read by mistake.
        var header = new List<byte>(1024);
        byte[] one = new byte[1];
        while (header.Count < 16384)
        {
            int read = await stream.ReadAsync(one, cancel);
            if (read == 0)
            {
                return null;
            }

            header.Add(one[0]);
            int n = header.Count;
            if (n >= 4 && header[n - 4] == '\r' && header[n - 3] == '\n' && header[n - 2] == '\r' && header[n - 1] == '\n')
            {
                return Encoding.ASCII.GetString(header.ToArray(), 0, n - 4);
            }
        }

        return null;
    }

    private static Task WriteAsync(NetworkStream stream, string text, CancellationToken cancel) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(text), cancel).AsTask();
}
