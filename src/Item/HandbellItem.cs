﻿using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat
{
    public class HandbellItem : Item
    {

        public ILoadedSound handbellring;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            
        }

        // When an entity is attacked with this bell
        // Called twice it seems. Both clientside and serverside?
        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);
            if (!world.Side.IsServer()) return;

            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/item/handbell/handbell_hit_1.ogg"), byEntity, null, false, 32);
            world.Api.Logger.Debug("Bell sound played");
        }

        // When a block is broken with this item
        // Called twice it seems. Both clientside and serverside?
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (!world.Side.IsServer()) return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);

            world.PlaySoundFor(new AssetLocation("rpvoicechat", "sounds/item/handbell/handbell_blockbreak_1.ogg"), (IPlayer)byEntity);
            world.Api.Logger.Debug("Bell sound played");

            return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
        }

        // When item is used
        // Only happens once. Clientside only?
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IWorldAccessor world = byEntity.World;
            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/item/handbell/handbell_ring_1.ogg"), byEntity, null, false, 10);
            world.Api.Logger.Debug("Bell sound played");

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}