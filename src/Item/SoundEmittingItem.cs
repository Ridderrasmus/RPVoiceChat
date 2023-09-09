using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace RPVoiceChat
{
    public class SoundEmittingItem : Item
    {
        private Random Random = new Random();

        protected int AudibleDistance = 16;
        protected float Volume = 0.6f;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            AudibleDistance = (int)(Attributes?["soundAudibleDistance"].AsInt(0));
            Volume = (float)(Attributes?["soundVolume"].AsFloat(0f));
        }


        // When an entity is attacked with this item
        // Called twice it seems. Both clientside and serverside?
        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot slot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, slot);
            if (!world.Side.IsServer()) return;


            string[] attackSounds = slot.Itemstack.Item.Attributes?["attackSounds"].AsArray<string>(new string[0]);

            if (attackSounds == null || attackSounds.Length == 0) return;

            string sound = attackSounds[Random.Next(attackSounds.Length)];
            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), byEntity, null, false, AudibleDistance, Volume);
        }

        // When a block is broken with this item
        // Called twice it seems. Both clientside and serverside?
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot slot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (!world.Side.IsServer()) return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);

            string[] breakBlockSounds = slot.Itemstack.Item.Attributes?["breakBlockSounds"].AsArray<string>(new string[0]);

            if (breakBlockSounds == null || breakBlockSounds.Length == 0) return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);

            string sound = breakBlockSounds[Random.Next(breakBlockSounds.Length)];

            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), byEntity, null, false, AudibleDistance, Volume);

            return base.OnBlockBrokenWith(world, byEntity, slot, blockSel, dropQuantityMultiplier);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            IWorldAccessor world = byEntity.World;
            if (!world.Side.IsServer()) return;

            string[] rightClickSounds = slot.Itemstack.Item.Attributes?["rightClickSounds"].AsArray<string>(new string[0]);
            
            if (rightClickSounds == null || rightClickSounds.Length == 0) return;

            string sound = rightClickSounds[Random.Next(rightClickSounds.Length)];

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            byPlayer.Entity.World.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/" + sound + ".ogg"), byEntity, null, false, AudibleDistance, Volume);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.Handled;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return true;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return true;
        }
    }
}
