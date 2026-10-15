using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalTennis
{
    public readonly struct SocketNotice
    {
        public readonly string Kind, Data;
        public SocketNotice(string kind, string data = null) { Kind = kind; Data = data; }
    }

    /// <summary>Desktop transport. Background workers never touch Unity objects or deserialize game state.</summary>
    public sealed class MatchWebSocketClient : IDisposable
    {
        private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<SocketNotice> incoming = new ConcurrentQueue<SocketNotice>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private ClientWebSocket socket;
        private volatile bool connected;
        private int stopped;
        private volatile bool closing;
        public bool Connected => connected;
        public bool TryRead(out SocketNotice notice) => incoming.TryDequeue(out notice);

        public MatchWebSocketClient(Uri address)
        {
            if (address == null || (address.Scheme != "ws" && address.Scheme != "wss")) throw new ArgumentException("Use ws:// or wss://.");
            _ = Task.Run(() => Run(address));
        }
        public bool Send(string json)
        {
            if (!connected || outgoing.Count >= 128 || Encoding.UTF8.GetByteCount(json) > 16384) return false;
            outgoing.Enqueue(json); return true;
        }
        private async Task Run(Uri address)
        {
            try
            {
                using (socket = new ClientWebSocket())
                {
                    socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(10));
                        await socket.ConnectAsync(address, timeout.Token).ConfigureAwait(false);
                    }
                    connected = true; incoming.Enqueue(new SocketNotice("connected"));
                    var sender = SendLoop(); var receiver = ReceiveLoop();
                    try { await (await Task.WhenAny(sender, receiver).ConfigureAwait(false)).ConfigureAwait(false); }
                    finally
                    {
                        cancellation.Cancel();
                        try { await Task.WhenAll(sender, receiver).ConfigureAwait(false); } catch (OperationCanceledException) { }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { incoming.Enqueue(new SocketNotice("error", "Connection failed: " + e.GetType().Name)); }
            finally { connected = false; incoming.Enqueue(new SocketNotice("disconnected")); }
        }
        private async Task SendLoop()
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (outgoing.TryDequeue(out var json))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation.Token).ConfigureAwait(false);
                }
                else if (closing)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Leaving", cancellation.Token).ConfigureAwait(false);
                    return;
                }
                else await Task.Delay(5, cancellation.Token).ConfigureAwait(false);
            }
        }
        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            while (!cancellation.IsCancellationRequested)
            {
                using (var message = new MemoryStream())
                {
                    WebSocketReceiveResult part;
                    do
                    {
                        part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token).ConfigureAwait(false);
                        if (part.MessageType == WebSocketMessageType.Close) return;
                        if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > 16384)
                            throw new InvalidDataException("Invalid WebSocket message");
                        message.Write(buffer, 0, part.Count);
                    } while (!part.EndOfMessage);
                    if (incoming.Count >= 256) throw new InvalidDataException("Receive queue full");
                    incoming.Enqueue(new SocketNotice("message", Encoding.UTF8.GetString(message.ToArray())));
                }
            }
        }
        public void CloseGracefully()
        {
            if (!connected) { Dispose(); return; }
            closing = true; cancellation.CancelAfter(1000);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0) return;
            cancellation.Cancel();
            try { socket?.Abort(); } catch (ObjectDisposedException) { }
        }
    }
}
