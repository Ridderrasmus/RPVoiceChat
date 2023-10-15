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
        public event Action<TCPConnection> OnDisconnected;

        public IPEndPoint remoteEndpoint;
        public int port;
        private Logger logger;
        private Socket socket;
        private CancellationTokenSource _listeningCTS;

        public TCPConnection(Logger logger)
        {
            this.logger = logger;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public TCPConnection(Logger logger, Socket socket)
        {
            this.logger = logger;
            this.socket = socket;
            remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
            port = ((IPEndPoint)socket.LocalEndPoint)?.Port ?? 0;
        }

        public void Connect(IPEndPoint endPoint)
        {
            socket.Connect(endPoint);
            remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
            port = ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        public Task ConnectAsync(IPEndPoint endPoint)
        {
            return socket.ConnectAsync(endPoint).ContinueWith(_ =>
            {
                remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
                port = ((IPEndPoint)socket.LocalEndPoint).Port;
            });
        }

        public void Send(byte[] data)
        {
            var tcpMessage = PackMessage(data);
            socket.Send(tcpMessage);
        }

        public ValueTask<int> SendAsync(byte[] data, CancellationToken ct)
        {
            var tcpMessage = PackMessage(data);
            return socket.SendAsync(tcpMessage, ct);
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
                    var streamLength = await socket.ReceiveAsync(receiveBuffer);
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
                    string reason = e.Message;
                    if (ct.IsCancellationRequested) reason = "Gracefull exit";
                    else if (e is SocketException se)
                    {
                        reason = se.SocketErrorCode switch
                        {
                            SocketError.ConnectionReset => "Connection closed from the other side",
                            SocketError.Interrupted => "Socket was closed",
                            SocketError.OperationAborted => "Socket was closed",
                            _ => se.Message
                        };
                    }

                    logger.Debug($"Closing TCP connection, reason: {reason}");
                    Disconnect();
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
                while (stream.Position < stream.Length)
                {
                    int messageLength = reader.ReadInt32();
                    byte[] message = reader.ReadBytes(messageLength);
                    messages.Add(message);
                }
            }
            catch (Exception e)
            {
                logger.Error($"Couldn't parse TCP messages:\n{e}");
            }

            return messages;
        }

        public void Disconnect()
        {
            Dispose();
            OnDisconnected?.Invoke(this);
        }

        public void Dispose()
        {
            if (socket == null) return;
            _listeningCTS?.Cancel();
            _listeningCTS?.Dispose();
            socket.Close();
            socket = null;
        }
    }
}
