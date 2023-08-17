using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatConfig config;

        protected INetworkChannel networkChannel;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
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
