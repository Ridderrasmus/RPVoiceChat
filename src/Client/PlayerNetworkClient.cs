using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Util;

namespace RPVoiceChat.Client
{
    public class PlayerNetworkClient : IDisposable
    {
        public event Action<AudioPacket> OnAudioReceived;

        private List<INetworkClient> reserveTransports;
        private INetworkClient activeTransport;
        private IClientNetworkChannel handshakeChannel;
        private bool isConnected = false;

        public PlayerNetworkClient(ICoreClientAPI capi, List<INetworkClient> clientTransports)
        {
            reserveTransports = clientTransports;
            handshakeChannel = capi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionRequest>()
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionRequest>(OnHandshakeRequest);

            foreach (var transport in reserveTransports)
                transport.OnAudioReceived += AudioPacketReceived;
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (!isConnected) return;
            activeTransport.SendAudioToServer(packet);
        }

        private void OnHandshakeRequest(ConnectionRequest connectionRequest)
        {
            while (reserveTransports.Count > 0)
            {
                var transport = reserveTransports.PopOne();
                try
                {
                    ConnectWith(transport, connectionRequest);
                    return;
                }
                catch (Exception e)
                {
                    Logger.client.Warning($"Failed to connect with the {transport.GetTransportID()} client: {e.Message}");
                    transport.Dispose();
                }
            }
            IEnumerable<string> serverTransports = connectionRequest.SupportedTransports.Select(e => e.Transport);
            throw new Exception($"Failed to connect to the server. Supported transports: {string.Join(", ", serverTransports)}");
        }

        private void ConnectWith(INetworkClient transport, ConnectionRequest connectionRequest)
        {
            var transportID = transport.GetTransportID();
            Logger.client.Notification($"Attempting to connect with {transportID} client");
            var serverConnectionInfo = connectionRequest.SupportedTransports.FirstOrDefault(e => e.Transport == transportID);
            if (serverConnectionInfo == null)
                throw new Exception("Server doesn't support client's transport");

            ConnectionInfo clientConnection = new ConnectionInfo();
            if (transport is IExtendedNetworkClient extendedTransport)
                clientConnection = extendedTransport?.Connect(serverConnectionInfo);
            clientConnection.Transport = transportID;
            handshakeChannel.SendPacket(clientConnection);

            activeTransport = transport;
            isConnected = true;
            Logger.client.Notification($"Successfully connected with the {transportID} client");
        }

        private void AudioPacketReceived(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            isConnected = false;
            activeTransport?.Dispose();
            foreach (var transport in reserveTransports)
                transport.Dispose();
        }
    }
}
