using Open.Nat;
using ProtoBuf;
using rpvoicechat;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace RPVoiceChat.src.Networking
{
    public class RPVoiceChatUDPNetworkServer : RPVoiceChatUDPNetwork, INetworkServer
    {
        private ICoreServerAPI sapi;
        private int port;
        private static Dictionary<VoiceLevel, string> configKeyByVoiceLevel = new Dictionary<VoiceLevel, string>
        {
            { VoiceLevel.Whispering, "rpvoicechat:distance-whisper" },
            { VoiceLevel.Talking, "rpvoicechat:distance-talk" },
            { VoiceLevel.Shouting, "rpvoicechat:distance-shout" },
        };
        private string defaultKey = configKeyByVoiceLevel[VoiceLevel.Talking];

        private Dictionary<string, ConnectionInfo> playerToAddress = new Dictionary<string, ConnectionInfo>();

        public RPVoiceChatUDPNetworkServer(ICoreServerAPI sapi, int port) : base(sapi)
        {
            this.sapi = sapi;
            this.port = port;

            sapi.Event.PlayerNowPlaying += PlayerJoined;
            sapi.Event.PlayerDisconnect += PlayerLeft;
            OnMessageReceived += MessageReceived;


            UdpClient = new UdpClient(port);
            
            // UPnP using Mono.Nat
            NatDiscoverer discoverer = new NatDiscoverer();
            NatDevice device = Task.Run(() => discoverer.DiscoverDeviceAsync()).Result;

            if (device != null)
            {
                device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "Vintage Story Voice Chat"));
            }

            StartListening(port);

            
        }

        private void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            var playersource = sapi.World.PlayerByUid(packet.PlayerId);
            ConnectionInfo playersourceip;

            if (!playerToAddress.TryGetValue(packet.PlayerId, out playersourceip))
            {
                playersourceip.Address = ((IServerPlayer)sapi.World.PlayerByUid(packet.PlayerId)).IpAddress;
                playersourceip.Port = packet.ClientPort;
                playerToAddress.Add(packet.PlayerId, playersourceip);
            }
            else if (playersourceip.Port == 0)
            {
                playersourceip.Port = packet.ClientPort;
            }

            var stream = new MemoryStream();
            Serializer.Serialize(stream, packet);

            configKeyByVoiceLevel.TryGetValue(packet.VoiceLevel, out string key);

            int distance = sapi.World.Config.GetInt(key ?? defaultKey);
            int squareDistance = distance * distance;

            foreach (var closePlayer in sapi.World.AllOnlinePlayers)
            {
                if (closePlayer == playersource ||
                    closePlayer.Entity == null ||
                    playersource.Entity.Pos.SquareDistanceTo(closePlayer.Entity.Pos.XYZ) > squareDistance)
                    continue;

                UdpClient.Send(stream.ToArray(), (int)stream.Length);
            }
        }

        private void MessageReceived(byte[] msg)
        {
            SendAudioToAllClientsInRange(Serializer.Deserialize<AudioPacket>(new MemoryStream(msg)));
        }

        private void PlayerJoined(IServerPlayer byPlayer)
        {
            sapi.Network.GetChannel(ChannelName).SendPacket(new ConnectionInfo() { Address = "", Port = port }, byPlayer);
        }

        private void PlayerLeft(IServerPlayer byPlayer)
        {
            playerToAddress.Remove(byPlayer.PlayerUID);
        }
    }
}
