using RPVoiceChat.GameContent.BlockEntities;
using RPVoiceChat.GameContent.Blocks;
using RPVoiceChat.GameContent.Items;
using RPVoiceChat.GameContent.BlockBehaviors;
using RPVoiceChat.GameContent.BlockEntityBehaviors;
using RPVoiceChat.Utils;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        public static readonly string modID = "rpvoicechat";
        protected RPVoiceChatConfig config;
        private PatchManager patchManager;

        public override void StartPre(ICoreAPI api)
        {
            ClientSettings.Init(api);
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
            WorldConfig.Init(api);
            new Logger(api);
        }

        public override void Start(ICoreAPI api)
        {
            patchManager = new PatchManager(modID);
            patchManager.Patch(api);

            ItemRegistry.RegisterItems(api);
            BlockRegistry.RegisterBlocks(api);
            BlockEntityRegistry.RegisterBlockEntities(api);
            BlockBehaviorRegistry.RegisterBlockEntityBehaviors(api);
            BlockEntityBehaviorRegistry.RegisterBlockEntityBehaviors(api);

        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);

            if (api.Side == EnumAppSide.Server)
            {
                BlockBehaviorRegistry.AddBehaviors(api);
                BlockEntityBehaviorRegistry.AddBlockEntityBehaviors(api);
            }
        }

        public override void Dispose()
        {
            patchManager?.Dispose();
        }
    }
}
