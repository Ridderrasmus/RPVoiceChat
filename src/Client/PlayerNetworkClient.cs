using RPVoiceChat.Networking;
using System;
using Vintagestory.API.Client;

namespace rpvoicechat.Client
{
    public class PlayerNetworkClient
    {
        public event Action<AudioPacket> OnAudioReceived;

        private INetworkClient networkClient;
        private bool handshakeSupported;
        private IClientNetworkChannel handshakeChannel;

        public PlayerNetworkClient(ICoreClientAPI capi, INetworkClient client)
        {
            networkClient = client;
            handshakeSupported = client is IExtendedNetworkClient;
            handshakeChannel = capi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(OnHandshakeRequest);

            networkClient.OnAudioReceived += OnAudioReceived;
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            networkClient.SendAudioToServer(packet);
        }

        private void OnHandshakeRequest(ConnectionInfo serverConnection)
        {
            if (!handshakeSupported)
                throw new Exception("Server requested handshake but current NetworkClient doesn't support it");

            var extendedClient = networkClient as IExtendedNetworkClient;
            ConnectionInfo clientConnection = extendedClient.OnHandshakeReceived(serverConnection);
            handshakeChannel.SendPacket(clientConnection);
        }
    }
}
