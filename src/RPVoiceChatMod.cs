using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace rpvoicechat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatSocketClient client;
        protected RPVoiceChatSocketServer server;
        protected RPVoiceChatConfig config;

        protected ICoreClientAPI capi;
        protected ICoreServerAPI sapi;
        protected INetworkChannel networkChannel;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            ModConfig.ReadConfig(api);
            config = ModConfig.config;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            
            // Register network channel
            networkChannel = api.Network.RegisterChannel("rpvoicechat")
                .RegisterMessageType(typeof(ConnectionInfo));

        }


    }
}
