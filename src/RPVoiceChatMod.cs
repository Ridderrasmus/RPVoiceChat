using RPVoiceChat.Utils;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatConfig config;
        protected const string modID = "rpvoicechat";

        public override void StartPre(ICoreAPI api)
        {
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
            new Logger(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            ItemRegistry.RegisterItems(api);
            BlockRegistry.RegisterBlocks(api);
        }
    }
}
