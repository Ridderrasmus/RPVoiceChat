﻿using RPVoiceChat.Utils;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public abstract class RPVoiceChatMod : ModSystem
    {
        protected RPVoiceChatConfig config;
        public static readonly string modID = "rpvoicechat";

        public override void StartPre(ICoreAPI api)
        {
            ModConfig.ReadConfig(api);
            config = ModConfig.Config;
            WorldConfig.Init(api);
            new Logger(api);
        }

        public override void Start(ICoreAPI api)
        {
            ItemRegistry.RegisterItems(api);
            BlockRegistry.RegisterBlocks(api);
        }

        public override void Dispose()
        {
            WorldConfig.Dispose();
            ModConfig.Dispose();
        }
    }
}
