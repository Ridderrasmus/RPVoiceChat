using ProtoBuf;
using rpvoicechat;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
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

        private Dictionary<string, string> playerToAddress = new Dictionary<string, string>();

        public RPVoiceChatUDPNetworkServer(ICoreServerAPI sapi, int port) : base(sapi)
        {
            this.sapi = sapi;
            this.port = port;

            sapi.Event.PlayerNowPlaying += PlayerJoined;
            sapi.Event.PlayerDisconnect += PlayerLeft;
            OnMessageReceived += MessageReceived;


            UdpClient = new UdpClient(port);

            StartListening(port);

            
        }

        private void SendAudioToAllClientsInRange(AudioPacket packet)
        {
            var playersource = sapi.World.PlayerByUid(packet.PlayerId);
            string playersourceip;

            if (!playerToAddress.TryGetValue(packet.PlayerId, out playersourceip))
            {
                playersourceip = ((IServerPlayer)sapi.World.PlayerByUid(packet.PlayerId)).IpAddress;
                playerToAddress.Add(packet.PlayerId, playersourceip);
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
