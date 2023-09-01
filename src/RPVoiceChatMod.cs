using Vintagestory.API.Common;

namespace rpvoicechat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatConfig config;
        protected const string modID = "rpvoicechat";

        public override void StartPre(ICoreAPI api)
        {
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
        }
    }
}
