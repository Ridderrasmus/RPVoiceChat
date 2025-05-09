﻿using RPVoiceChat.GameContent.BlockEntityBehaviors;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.BlockBehaviors
{
    class BehaviorRingable : BlockBehavior
    {
        public BehaviorRingable(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            BEBehaviorRingable? ringable = world.GetBlockAccessor(false, false, false).GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorRingable>();

            if (ringable == null)
                return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);


            // Check if the player is holding the bell part item
            if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Item is Item item && string.IsNullOrWhiteSpace(ringable.BellPartCode))
            {
                if (item.Code.Path.StartsWith("smallbellparts"))
                {
                    // Set the bell part item to the small bell parts item
                    ringable.BellPartCode = item.Code.Path;

                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        // Remove the bell part item from the player's inventory
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    }
                    
                    ringable.Blockentity.MarkDirty(true);
                }

                return true;
            }
            else
            {
                // If the bell part item is not "none" and the time since the last ring is greater than 5 seconds, play the bell sound
                if (!string.IsNullOrWhiteSpace(ringable.BellPartCode) && (ringable.LastRung == null || (ringable.LastRung < DateTime.Now - (TimeSpan.FromSeconds(5)))))
                {
                    ringable.LastRung = DateTime.Now;
                    int rand = new Random().Next(1, 3);
                    world.PlaySoundAt(new AssetLocation(RPVoiceChatMod.modID, $"sounds/block/callbell/callbell_{rand}.ogg"), blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer, false, 16);
                }
            }


            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {
            // Drop itemstack of bellpart on block broken by non creative player

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                BEBehaviorRingable? ringable = world.GetBlockAccessor(false, false, false).GetBlockEntity(pos).GetBehavior<BEBehaviorRingable>();

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

                ItemStack stack = new ItemStack(item);
                stack.StackSize = 1;

                world.SpawnItemEntity(stack, pos.ToVec3d());
            }

            base.OnBlockBroken(world, pos, byPlayer, ref handling);

            

        }

    }
}
