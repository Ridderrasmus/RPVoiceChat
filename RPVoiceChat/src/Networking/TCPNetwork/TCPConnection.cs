using RPVoiceChat.Util;
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
        public event Action<bool, bool, TCPConnection> OnDisconnected;

        public IPEndPoint remoteEndpoint;
        public int port;
        private Logger logger;
        private Socket socket;
        private CancellationTokenSource _listeningCTS;
        private bool isDisposed = false;
        private byte[] receiveBuffer;
        private byte[] fragmentBuffer = new byte[100 * 1024];
        private int fragmentedBytes = 0;

        public TCPConnection(Logger logger, Socket existingSocket = null)
        {
            this.logger = logger;
            socket = existingSocket;
            socket ??= new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveBufferSize = 400 * 1024;
            socket.NoDelay = true;
            remoteEndpoint = socket.RemoteEndPoint as IPEndPoint;
            port = (socket.LocalEndPoint as IPEndPoint)?.Port ?? 0;

            receiveBuffer = new byte[socket.ReceiveBufferSize];
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

        public ValueTask<int> SendAsync(byte[] data, CancellationToken ct = default)
        {
            if (socket == null) throw new Exception("Socket already disposed.");

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
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var streamLength = await socket.ReceiveAsync(receiveBuffer);
                    ct.ThrowIfCancellationRequested();
                    if (streamLength == 0) throw new SocketException((int)SocketError.ConnectionReset);

                    List<byte[]> messages = ParseMessages(receiveBuffer, streamLength);
                    if (streamLength == receiveBuffer.Length) ClearNetworkStream();

                    foreach (var msg in messages)
                        OnMessageReceived?.Invoke(msg, this);
                }
                catch (Exception e)
                {
                    string reason = e.ToString();
                    bool isGraceful = true;
                    bool isHalfClosed = false;
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
                        if (se.SocketErrorCode == SocketError.ConnectionReset) isHalfClosed = true;
                    }
                    else isGraceful = false;

                    logger.Debug($"Closing TCP connection with {remoteEndpoint}, reason: {reason}");
                    Disconnect(isGraceful, isHalfClosed);
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

        private List<byte[]> ParseMessages(byte[] data, int length)
        {
            var messages = new List<byte[]>();
            var stream = new MemoryStream(length + fragmentedBytes);
            stream.Write(fragmentBuffer, 0, fragmentedBytes);
            stream.Write(data, 0, length);
            fragmentedBytes = 0;
            stream.Position = 0;
            using var reader = new BinaryReader(stream);

            try
            {
                long bytesLeft = stream.Length;
                while (bytesLeft > 4)
                {
                    int messageLength = reader.ReadInt32();
                    if (messageLength > bytesLeft - 4 || messageLength < 1) break;
                    byte[] message = reader.ReadBytes(messageLength);
                    messages.Add(message);
                    bytesLeft = stream.Length - stream.Position;
                }
                if (bytesLeft != 0)
                {
                    var remainingData = stream.ToArray();
                    Buffer.BlockCopy(remainingData, (int)(remainingData.Length - bytesLeft), fragmentBuffer, fragmentedBytes, (int)bytesLeft);
                    fragmentedBytes += (int)bytesLeft;
                    if (fragmentedBytes > 30 * 1024)
                        logger.Warning($"Buffer of packet fragments from {remoteEndpoint} is abnormally full. (Message queue at {length}/{receiveBuffer.Length}, fragmented buffer at {fragmentedBytes}/{fragmentBuffer.Length})");
                    if (fragmentedBytes > 64 * 1024)
                    {
                        logger.Error($"Buffer of packet fragments from {remoteEndpoint} exceeded impossible value, discarding data. (Message queue at {length}/{receiveBuffer.Length}, fragmented buffer at {fragmentedBytes}/{fragmentBuffer.Length})");
                        ClearNetworkStream();
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error($"Couldn't parse TCP messages:\n{e}");
                ClearNetworkStream();
            }

            return messages;
        }

        private void ClearNetworkStream()
        {
            logger.Debug($"Unclogging message buffer for {remoteEndpoint}");
            int bytesRead;
            do
            {
                bytesRead = socket.Receive(receiveBuffer);
            } while (bytesRead == receiveBuffer.Length);
            fragmentedBytes = 0;
        }

        public void Disconnect(bool isGraceful = true, bool isHalfClosed = false)
        {
            try
            {
                socket?.Shutdown(SocketShutdown.Both);
                socket?.Disconnect(true);
            }
            catch { }
            OnDisconnected?.Invoke(isGraceful, isHalfClosed, this);
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
