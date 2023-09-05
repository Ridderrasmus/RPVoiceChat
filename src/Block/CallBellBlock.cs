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
        private float Volume = 0.6f;

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
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!world.Side.IsServer()) return;

            var rand = Random.Next(CallBellRings.Count);
            byPlayer.Entity.World.PlaySoundAt(CallBellRings[rand], byPlayer.Entity, null, false, AudibleDistance, Volume);

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

    }
}