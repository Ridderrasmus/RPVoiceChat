using Open.Nat;
using ProtoBuf;
using rpvoicechat;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace RPVoiceChat
{
    public class RPVoiceChatUDPNetworkClient : RPVoiceChatUDPNetwork, INetworkClient
    {
        private ICoreClientAPI capi;
        private IPEndPoint serverEndpoint;
        private int localPort;

        public event Action<AudioPacket> OnAudioReceived;

        public RPVoiceChatUDPNetworkClient(ICoreClientAPI capi) : base(capi)
        {
            this.capi = capi;

            capi.Network.GetChannel(ChannelName).SetMessageHandler<ConnectionInfo>(OnHandshakeReceived);

            OnMessageReceived += MessageReceived;
        }

        private void OnHandshakeReceived(ConnectionInfo info)
        {
            localPort = OpenUDPClient();

            // UPnP using Mono.Nat
            // (Mainly running this clientside just to be entirely certain that we have the port available)
            NatDiscoverer discoverer = new NatDiscoverer();
            NatDevice device = Task.Run(() => discoverer.DiscoverDeviceAsync()).Result;

            if (device != null)
            {
                device.CreatePortMapAsync(new Mapping(Protocol.Udp, localPort, localPort, "Vintage Story Voice Chat"));
            }

            StartListening(info.Port);

            serverEndpoint = new IPEndPoint(IPAddress.Parse(info.Address), info.Port);
        }

        public void SendAudioToServer(AudioPacket packet)
        {
            if (UdpClient == null || serverEndpoint == null) throw new Exception("Udp client or server endpoint has not been initialized.");
            packet.ClientPort = localPort;

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
