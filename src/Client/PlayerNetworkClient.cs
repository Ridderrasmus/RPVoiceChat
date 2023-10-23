using Open.Nat;
using RPVoiceChat.Networking;
using RPVoiceChat.Utils;
using System;
using System.Linq;
using Vintagestory.API.Client;

namespace RPVoiceChat.Client
{
    public class PlayerNetworkClient : IDisposable
    {
        public event Action<AudioPacket> OnAudioReceived;

        private ICoreClientAPI api;
        private INetworkClient networkClient;
        private INetworkClient reserveClient;
        private bool isConnected = false;
        private IClientNetworkChannel handshakeChannel;

        public PlayerNetworkClient(ICoreClientAPI capi, INetworkClient client, INetworkClient _reserveClient = null)
        {
            api = capi;
            networkClient = client;
            reserveClient = _reserveClient;
            handshakeChannel = capi.Network
                .RegisterChannel("RPVCHandshake")
                .RegisterMessageType<ConnectionInfo>()
                .SetMessageHandler<ConnectionInfo>(OnHandshakeRequest);

            networkClient.OnAudioReceived += AudioPacketReceived;
            if (reserveClient == null) return;
            reserveClient.OnAudioReceived += AudioPacketReceived;

            if (reserveClient is IExtendedNetworkClient)
                throw new NotSupportedException("Reserve client requiring handshake is not supported");
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (!isConnected) return;
            networkClient.SendAudioToServer(packet);
        }

        private void OnHandshakeRequest(ConnectionInfo serverConnection)
        {
            var clientTransportID = networkClient.GetTransportID();
            try
            {
                if (!serverConnection.SupportedTransports.Contains(clientTransportID))
                    throw new Exception("Server doesn't support client's transport");

                var extendedClient = networkClient as IExtendedNetworkClient;
                ConnectionInfo clientConnection = extendedClient?.Connect(serverConnection) ?? new ConnectionInfo();
                clientConnection.SupportedTransports = new string[] { clientTransportID };
                handshakeChannel.SendPacket(clientConnection);
                isConnected = true;
                Logger.client.Notification($"Successfully connected with the {clientTransportID} client");
                return;
            }
            catch (NatDeviceNotFoundException)
            {
                Logger.client.Warning($"Failed to connect with the {clientTransportID} client: Unable to port forward with UPnP. " +
                    $"Make sure your IP is public and UPnP is enabled if you want to connect with {clientTransportID} client.");
            }
            catch (Exception e)
            {
                Logger.client.Warning($"Failed to connect with the {clientTransportID} client: {e.Message}");
            }

            if (reserveClient == null)
                throw new Exception($"Failed to connect to the server. Supported transports: {string.Join(", ", serverConnection.SupportedTransports)}");

            SwapActiveClient(reserveClient);
            reserveClient = null;
            OnHandshakeRequest(serverConnection);
        }

        private void SwapActiveClient(INetworkClient newClient)
        {
            Logger.client.Notification($"Using {newClient.GetTransportID()} client from now on");
            networkClient.Dispose();
            networkClient = newClient;
        }

        private void AudioPacketReceived(AudioPacket packet)
        {
            OnAudioReceived?.Invoke(packet);
        }

        public void Dispose()
        {
            var disposableClient = networkClient as IDisposable;
            var disposableReserveClient = reserveClient as IDisposable;
            disposableClient?.Dispose();
            disposableReserveClient?.Dispose();
        }
    }
}
