using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Lidgren.Network;
using ProtoBuf;

namespace rpvoicechat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatSocketClient client;
        protected RPVoiceChatSocketServer server;

        protected ICoreClientAPI capi;
        protected ICoreServerAPI sapi;
        protected INetworkChannel networkChannel;

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        public class ConnectionInfo
        {
            public string Address { get; set; }
            public int Port { get; set; }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            networkChannel = api.Network.RegisterChannel("rpvoicechat")
                .RegisterMessageType(typeof(ConnectionInfo));
        }
    }
}
