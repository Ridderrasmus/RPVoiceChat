using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat
{
    public class HandbellItem : Item
    {
        private Random Random = new Random();

        private List<AssetLocation> handbellring = new List<AssetLocation>();
        private List<AssetLocation> handbellattackring = new List<AssetLocation>();
        private List<AssetLocation> handbellblockbreakring = new List<AssetLocation>();

        private int AudibleDistance = 16;
        private float Volume = 0.6f;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            for (int i = 1; i < 4; i++)
            {
                handbellring.Add(new AssetLocation("rpvoicechat", "sounds/item/handbell/handbell_ring_" + i + ".ogg"));
                handbellattackring.Add(new AssetLocation("rpvoicechat", "sounds/item/handbell/handbell_hit_" + i + ".ogg"));
                handbellblockbreakring.Add(new AssetLocation("rpvoicechat", "sounds/item/handbell/handbell_blockbreak_" + i + ".ogg"));
            }
        }
        

        // When an entity is attacked with this bell
        // Called twice it seems. Both clientside and serverside?
        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);
            if (!world.Side.IsServer()) return;

            int rand = Random.Next(handbellattackring.Count);
            world.PlaySoundAt(handbellattackring[rand], byEntity, null, false, AudibleDistance, Volume);
        }

        // When a block is broken with this item
        // Called twice it seems. Both clientside and serverside?
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (!world.Side.IsServer()) return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);

            int rand = Random.Next(handbellblockbreakring.Count);

            world.PlaySoundAt(handbellblockbreakring[rand], byEntity, null, false, AudibleDistance, Volume);

            return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            IWorldAccessor world = byEntity.World;
            if (!world.Side.IsServer()) return;

            int rand = Random.Next(handbellring.Count);

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            byPlayer.Entity.World.PlaySoundAt(handbellring[rand], byEntity, null, false, AudibleDistance, Volume);
            world.Logger.Debug("Played sound");
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