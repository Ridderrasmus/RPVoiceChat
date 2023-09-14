using RPVoiceChat.BlockEntities;
using RPVoiceChat.Blocks;
using RPVoiceChat.Items;
using RPVoiceChat.Utils;
using Vintagestory.API.Common;
using static OpenTK.Graphics.OpenGL.GL;

namespace RPVoiceChat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatConfig config;
        protected const string modID = "rpvoicechat";
        protected RecipeHandler RecipeHandler;

        public override void StartPre(ICoreAPI api)
        {
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
            new Logger(api);
            RecipeHandler = new RecipeHandler(api);
        }

        public override void Start(ICoreAPI api)
        {
            ItemRegistry.RegisterItems(api);
            BlockRegistry.RegisterBlocks(api);
            BlockEntityRegistry.RegisterBlockEntities(api);
        }
    }
}
