using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking
{
    public class TCPConnection : IDisposable
    {
        public event Action<byte[], TCPConnection> OnMessageReceived;
        public event Action<bool, TCPConnection> OnDisconnected;

        public IPEndPoint remoteEndpoint;
        public int port;
        private Logger logger;
        private Socket socket;
        private CancellationTokenSource _listeningCTS;
        private bool isDisposed = false;

        public TCPConnection(Logger logger, Socket existingSocket = null)
        {
            this.logger = logger;
            socket = existingSocket;
            socket ??= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveBufferSize = 16384;
            remoteEndpoint = socket.RemoteEndPoint as IPEndPoint;
            port = (socket.LocalEndPoint as IPEndPoint)?.Port ?? 0;
        }

        public void Connect(IPEndPoint endPoint)
        {
            socket.Connect(endPoint);
            remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
            port = ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        public Task ConnectAsync(IPEndPoint endPoint)
        {
            return Task.Run(() => socket.Connect(endPoint)).ContinueWith(_ =>
            {
                remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
                port = ((IPEndPoint)socket.LocalEndPoint).Port;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public void Send(byte[] data)
        {
            if (socket == null) throw new Exception("Socket already disposed.");

            var tcpMessage = PackMessage(data);
            try
            {
                socket.Send(tcpMessage);
            }
            catch (Exception e)
            {
                if (e is SocketException se &&
                    (se.SocketErrorCode == SocketError.NotConnected ||
                    se.SocketErrorCode == SocketError.OperationAborted ||
                    se.SocketErrorCode == SocketError.Interrupted) ||
                    _listeningCTS.IsCancellationRequested) return;
                logger.Error($"Failed to send TCP packet to server:\n{e}");
                Disconnect(false);
            }
        }

        public void SendAsync(byte[] data, CancellationToken ct = default)
        {
            if (socket == null) throw new Exception("Socket already disposed.");

            var tcpMessage = PackMessage(data);
            Task.Run(() => socket.Send(tcpMessage));
        }

        public void StartListening()
        {
            if (socket == null) throw new Exception("Socket already disposed.");

            _listeningCTS = new CancellationTokenSource();
            Listen(_listeningCTS.Token);
        }

        private async void Listen(CancellationToken ct)
        {
            byte[] receiveBuffer = new byte[socket.ReceiveBufferSize];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var streamLength = await Task.Run(() => socket.Receive(receiveBuffer));
                    ct.ThrowIfCancellationRequested();
                    if (streamLength <= 0) throw new SocketException((int)SocketError.ConnectionReset);

                    byte[] data = new byte[streamLength];
                    Buffer.BlockCopy(receiveBuffer, 0, data, 0, streamLength);

                    List<byte[]> messages = ParseMessages(data);
                    foreach (var msg in messages)
                        OnMessageReceived?.Invoke(msg, this);
                }
                catch (Exception e)
                {
                    string reason = e.ToString();
                    bool isGraceful = true;
                    if (ct.IsCancellationRequested) reason = "Graceful exit";
                    else if (e is SocketException se)
                    {
                        reason = se.SocketErrorCode switch
                        {
                            SocketError.ConnectionReset => "Connection closed from the other side",
                            SocketError.Interrupted => "Socket was closed",
                            SocketError.OperationAborted => "Socket was closed",
                            _ => se.ToString()
                        };
                        if (reason == se.ToString()) isGraceful = false;
                    }
                    else isGraceful = false;

                    logger.Debug($"Closing TCP connection, reason: {reason}");
                    Disconnect(isGraceful);
                    return;
                }
            }
        }

        private byte[] PackMessage(byte[] data)
        {
            int messageLength = data.Length;
            byte[] prefix = BitConverter.GetBytes(messageLength);
            byte[] tcpMessage = new byte[messageLength + sizeof(int)];

            Buffer.BlockCopy(prefix, 0, tcpMessage, 0, sizeof(int));
            Buffer.BlockCopy(data, 0, tcpMessage, sizeof(int), data.Length);

            return tcpMessage;
        }

        private List<byte[]> ParseMessages(byte[] data)
        {
            var messages = new List<byte[]>();
            var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            try
            {
                long bytesLeft = stream.Length;
                while (bytesLeft >= 4)
                {
                    int messageLength = reader.ReadInt32();
                    bytesLeft -= 4;
                    if (bytesLeft < messageLength || messageLength < 0) break;
                    byte[] message = reader.ReadBytes(messageLength);
                    messages.Add(message);
                    bytesLeft = stream.Length - stream.Position;
                }
                if (bytesLeft != 0) logger.Warning("Found fragmented packet in message buffer. Proceeding to drop it");
            }
            catch (Exception e)
            {
                logger.Error($"Couldn't parse TCP messages:\n{e}");
            }

            return messages;
        }

        public void Disconnect(bool isGraceful = true)
        {
            try { socket?.Disconnect(true); } catch { }
            OnDisconnected?.Invoke(isGraceful, this);
        }

        public void Dispose()
        {
            if (isDisposed || socket == null) return;
            isDisposed = true;
            _listeningCTS?.Cancel();
            _listeningCTS?.Dispose();
            socket.Close();
            socket = null;
        }
    }
}
