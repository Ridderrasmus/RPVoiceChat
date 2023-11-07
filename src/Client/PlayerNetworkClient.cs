using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace RPVoiceChat.Client
{
    public class PlayerNetworkClient : IDisposable
    {
        public event Action<AudioPacket> OnAudioReceived;

        private ICoreClientAPI api;
        private List<INetworkClient> _initialTransports;
        private List<INetworkClient> activeTransports = new List<INetworkClient>();
        private IClientNetworkChannel handshakeChannel;
        private ConnectionInfo[] serverConnections;
        private bool isConnected = false;
        private bool isDisposed = false;
        private bool isShutdown { get => isDisposed || (api as ClientCoreAPI)?.disposed == true; }

        public PlayerNetworkClient(ICoreClientAPI capi, List<INetworkClient> clientTransports)
        {
            api = capi;
            _initialTransports = clientTransports;
            handshakeChannel = capi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionRequest>(OnConnectionRequest);

            foreach (var transport in _initialTransports)
                transport.OnAudioReceived += AudioPacketReceived;
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (isShutdown || !isConnected) return;

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

        private void OnConnectionRequest(ConnectionRequest connectionRequest)
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

            if (activeTransports[0] is NativeNetworkClient) api.Event.EnqueueMainThreadTask(() => api.ShowChatMessage($"<strong>[RPVoiceChat]: Failed to connect with custom network clients, audio stability will be degraded</strong>"), "rpvoicechat:PlayerNetworkClient");
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
            {
                Logger.client.Notification($"Server doesn't support {transportID} transport, aborting");
                return;
            }

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

        private void ConnectionLost(bool canReconnect, IExtendedNetworkClient transport)
        {
            var transportID = transport.GetTransportID();
            Logger.client.Notification($"{transportID} transport reported connection loss");
            transport.OnConnectionLost -= ConnectionLost;

            if (isShutdown) return;
            if (canReconnect && Reconnect(transport)) return;
            if (isShutdown) return;
            activeTransports.Remove(transport);
            transport.Dispose();
            api.Event.EnqueueMainThreadTask(() => api.ShowChatMessage($"<strong>[RPVoiceChat]: Failed to reconnect with {transportID} client, audio stability will be degraded</strong>"), "rpvoicechat:PlayerNetworkClient");
        }

        private bool Reconnect(IExtendedNetworkClient transport)
        {
            Logger.client.Notification($"Reconnecting...");
            try
            {
                ConnectWith(transport);
                return true;
            }
            catch (Exception e)
            {
                if (isShutdown)
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
