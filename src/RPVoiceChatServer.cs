using ProperVersion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace rpvoicechat.src
{
    public class RPVoiceChatServer : RPVoiceChatMod
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(sapi);
            sapi.Event.PlayerNowPlaying += OnPlayerPlaying;

            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", sapi.World.Config.GetInt("rpvoicechat:distance-whisper", 5));
            sapi.World.Config.SetInt("rpvoicechat:distance-talk", sapi.World.Config.GetInt("rpvoicechat:distance-talk", 15));
            sapi.World.Config.SetInt("rpvoicechat:distance-shout", sapi.World.Config.GetInt("rpvoicechat:distance-shout", 25));
        }

        private void OnPlayerPlaying(IServerPlayer byPlayer)
        {
            if (server == null)
            {
                server = new RPVoiceChatSocketServer(sapi);
            }

            string address = server.GetPublicIPAddress();
            int port = server.GetPort();
            sapi.Network.GetChannel("rpvoicechat").SendPacket(new ConnectionInfo()
            {
                Address = address,
                Port = port
            }, byPlayer);
        }
    }
}
