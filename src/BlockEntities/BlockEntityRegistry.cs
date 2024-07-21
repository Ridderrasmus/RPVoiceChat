﻿using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.BlockEntities
{
    public class BlockEntityRegistry
    {
        public static void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("ChurchBellPart", typeof(BlockEntityChurchBellPart));
            api.RegisterBlockEntityClass("ChurchBellLayer", typeof(BlockEntityChurchBellLayer));
        }
    }
}
