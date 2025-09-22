#nullable enable

using RPVoiceChat.GameContent.BlockEntityBehaviors;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockBehaviors
{
    class BehaviorRingable : BlockBehavior
    {
        const double BellRingCooldownSeconds = 1.5;

        public BehaviorRingable(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (blockEntity == null) return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);

            BEBehaviorRingable? ringable = blockEntity.GetBehavior<BEBehaviorRingable>();
            if (ringable == null) return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);

            // 1) Player attaches a bell part if they hold one and none is present
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Item is Item item &&
                string.IsNullOrWhiteSpace(ringable.BellPartCode))
            {
                if (item.Code.Path.StartsWith("smallbellparts"))
                {
                    ringable.BellPartCode = item.Code.Path;

                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    }

                    ringable.Blockentity.MarkDirty(true);
                    return true;
                }
            }
            else
            {
                // 2) Player rings the bell if a part is attached and cooldown elapsed
                if (!string.IsNullOrWhiteSpace(ringable.BellPartCode) &&
                    (ringable.LastRung == null || ringable.LastRung < DateTime.Now.AddSeconds(-BellRingCooldownSeconds)))
                {
                    ringable.LastRung = DateTime.Now;
                    int rand = new Random().Next(1, 3);
                    float volume = (world.Side == EnumAppSide.Client && ClientSettings.OutputBlock != 0) ? ClientSettings.OutputBlock : 0.6f;

                    world.PlaySoundAt(
                        new AssetLocation(RPVoiceChatMod.modID, $"sounds/block/callbell/callbell_{rand}.ogg"),
                        blockSel.Position.X + 0.5, blockSel.Position.Y + 0.5, blockSel.Position.Z + 0.5,
                        byPlayer,
                        false,
                        16f,
                        volume
                    );
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                var blockEntity = world.BlockAccessor.GetBlockEntity(pos);
                if (blockEntity == null) { base.OnBlockBroken(world, pos, byPlayer, ref handling); return; }

                BEBehaviorRingable? ringable = blockEntity.GetBehavior<BEBehaviorRingable>();
                if (ringable == null || string.IsNullOrWhiteSpace(ringable.BellPartCode))
                {
                    base.OnBlockBroken(world, pos, byPlayer, ref handling);
                    return;
                }

                Item item = world.GetItem(new AssetLocation(RPVoiceChatMod.modID, ringable.BellPartCode));
                if (item == null)
                {
                    base.OnBlockBroken(world, pos, byPlayer, ref handling);
                    return;
                }

                ItemStack stack = new ItemStack(item)
                {
                    StackSize = 1
                };
                world.SpawnItemEntity(stack, pos.ToVec3d());
            }

            base.OnBlockBroken(world, pos, byPlayer, ref handling);
        }
    }
}