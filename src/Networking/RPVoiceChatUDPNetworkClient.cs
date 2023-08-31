using ProtoBuf;
using rpvoicechat;
using System;
using System.IO;
using System.Net;
using Vintagestory.API.Client;

namespace RPVoiceChat
{
    public class RPVoiceChatUDPNetworkClient : RPVoiceChatUDPNetwork, INetworkClient
    {
        private ICoreClientAPI capi;
        private IPEndPoint serverEndpoint;

        public event Action<AudioPacket> OnAudioReceived;

        public RPVoiceChatUDPNetworkClient(ICoreClientAPI capi) : base(capi)
        {
            this.capi = capi;

            capi.Network.GetChannel(ChannelName).SetMessageHandler<ConnectionInfo>(OnHandshakeReceived);

            OnMessageReceived += MessageReceived;
        }

        private void OnHandshakeReceived(ConnectionInfo info)
        {
            OpenUDPClient(info.Port);

            StartListening(info.Port);

            serverEndpoint = new IPEndPoint(IPAddress.Parse(info.Address), info.Port);
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (UdpClient == null || serverEndpoint == null) throw new Exception("Udp client or server endpoint has not been initialized.");

            var stream = new MemoryStream();
            Serializer.Serialize(stream, packet);
            UdpClient.Send(stream.ToArray(), (int)stream.Length, serverEndpoint);
        }

        private void MessageReceived(byte[] msg)
        {
            AudioPacket packet = Serializer.Deserialize<AudioPacket>(new MemoryStream(msg));
            OnAudioReceived?.Invoke(packet);
        }
    }
}
