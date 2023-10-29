using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace RPVoiceChat.Client
{
    public class PlayerNetworkClient : IDisposable
    {
        public event Action<AudioPacket> OnAudioReceived;

        private List<INetworkClient> _initialTransports;
        private List<INetworkClient> activeTransports;
        private IClientNetworkChannel handshakeChannel;
        private ConnectionInfo[] serverConnections;
        private bool isConnected = false;
        private bool isDisposed = false;

        public PlayerNetworkClient(ICoreClientAPI capi, List<INetworkClient> clientTransports)
        {
            _initialTransports = clientTransports;
            handshakeChannel = capi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionRequest>(OnHandshakeRequest);

            foreach (var transport in _initialTransports)
                transport.OnAudioReceived += AudioPacketReceived;
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (!isConnected) return;

            foreach (var transport in activeTransports)
            {
                try
                {
                    bool success = transport.SendAudioToServer(packet);
                    if (success) return;
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Couldn't use {transport.GetTransportID()} client to send audio packet to server: {e.Message}");
                }
            }
            Logger.client.Warning("Failed to send audio packet to server: All active network clients reported a failure");
        }

        private void OnHandshakeRequest(ConnectionRequest connectionRequest)
        {
            serverConnections = connectionRequest.SupportedTransports;
            Connect();
        }

        private void Connect()
        {
            activeTransports = new List<INetworkClient>();
            foreach (var transport in _initialTransports)
            {
                try
                {
                    ConnectWith(transport);
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Failed to connect with the {transport.GetTransportID()} client: {e.Message}");
                    transport.Dispose();
                }
            }
            isConnected = true;

            if (activeTransports.Count > 0) return;
            IEnumerable<string> serverTransportIDs = serverConnections.Select(e => e.Transport);
            throw new Exception($"Failed to connect to the server. Supported transports: {string.Join(", ", serverTransportIDs)}");
        }

        private void ConnectWith(INetworkClient transport)
        {
            var transportID = transport.GetTransportID();
            Logger.client.Notification($"Attempting to connect with {transportID} client");
            var serverConnection = serverConnections.FirstOrDefault(e => e.Transport == transportID);
            if (serverConnection == null)
                throw new Exception("Server doesn't support client's transport");

            ConnectionInfo clientConnection = new ConnectionInfo();
            if (transport is IExtendedNetworkClient extendedTransport)
            {
                clientConnection = extendedTransport?.Connect(serverConnection);
                extendedTransport.OnConnectionLost += ConnectionLost;
            }
            clientConnection.Transport = transportID;
            handshakeChannel.SendPacket(clientConnection);

            activeTransports.Add(transport);
            Logger.client.Notification($"Successfully connected with the {transportID} client");
        }

        private void AudioPacketReceived(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }

        private void ConnectionLost(bool canReconnect)
        {
            if (isConnected == false || activeTransport == null) return;

            Logger.client.Notification($"{activeTransport.GetTransportID()} transport reported connection loss");
            isConnected = false;
            if (activeTransport is IExtendedNetworkClient extendedTransport)
                extendedTransport.OnConnectionLost -= ConnectionLost;

            if (canReconnect && Reconnect()) return;
            if (isDisposed) return;
            activeTransport.Dispose();
            activeTransport = null;
            Connect();
        }

        private bool Reconnect()
        {
            Logger.client.Notification($"Reconnecting...");
            var transport = activeTransport;
            try
            {
                ConnectWith(transport);
                return true;
            }
            catch (Exception e)
            {
                if (isDisposed)
                {
                    Logger.client.Notification("Aborting due to mod unloading.");
                    return false;
                }
                Logger.client.Warning($"Unable to reconnect to {transport.GetTransportID()} server:\n{e}");
            }
            return false;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            foreach (var transport in activeTransports)
                transport.Dispose();
        }
    }
}
