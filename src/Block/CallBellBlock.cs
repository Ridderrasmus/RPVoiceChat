using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class CallBellBlock : Block
    {
        private Random Random = new Random();

        private List<AssetLocation> CallBellRings = new List<AssetLocation>();

        private int AudibleDistance = 16;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            for (int i = 1; i < 4; i++) 
            {
                CallBellRings.Add(new AssetLocation("rpvoicechat", "sounds/block/callbell/callbell_" + i + ".ogg"));
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var rand = Random.Next(CallBellRings.Count);
            world.PlaySoundAt(CallBellRings[rand], byPlayer, null, false, AudibleDistance);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

    }
}