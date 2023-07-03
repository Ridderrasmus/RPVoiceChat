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

            server = new RPVoiceChatSocketServer(sapi);

        }

        private void OnPlayerPlaying(IServerPlayer byPlayer)
        {
            while (server == null) { }

            string address = server.GetPublicIPAddress();
            int port = server.GetLocalPort();
            sapi.Network.GetChannel("rpvoicechat").SendPacket(new ConnectionInfo()
            {
                Address = address,
                Port = port
            }, byPlayer);
        }
    }
}
