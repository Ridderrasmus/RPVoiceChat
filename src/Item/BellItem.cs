using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace RPVoiceChat
{
    public class BellItem : Item
    {

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.MaxStackSize = 1;
        }

        // When an entity is attacked with this bell
        public override void OnAttackingWith(IWorldAccessor world, Entity byEntity, Entity attackedEntity, ItemSlot itemslot)
        {
            base.OnAttackingWith(world, byEntity, attackedEntity, itemslot);

            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/item/bell.ogg"), byEntity, null, false, 32);
            world.Api.Logger.Debug("Bell sound played");
        }

        // When a block is broken with this item
        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/item/bell.ogg"), byEntity, null, false, 32);
            world.Api.Logger.Debug("Bell sound played");

            return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
        }

        // When item is used
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IWorldAccessor world = byEntity.World;
            world.PlaySoundAt(new AssetLocation("rpvoicechat", "sounds/item/bell.ogg"), byEntity, null, false, 32);
            world.Api.Logger.Debug("Bell sound played");

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}