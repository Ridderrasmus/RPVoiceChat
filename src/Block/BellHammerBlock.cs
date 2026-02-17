using RPVoiceChat;
using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat.GameContent.Block
{
    /// <summary>
    /// Block Bell Hammer: only placeable next to a carillon or church bell; orients to face the bell (variant "side" from blocktype JSON).
    /// Implements IMechanicalPowerBlock so the Survival mod's network propagation (spreadTo) does not hit a null when connecting an axle.
    /// </summary>
    public class BellHammerBlock : Vintagestory.API.Common.Block, IMechanicalPowerBlock
    {
        private static readonly BlockFacing[] HorizontalFaces = BlockFacing.HORIZONTALS;

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;

            // Only allow placement on floor (click top of block) or ceiling (click bottom of block).
            if (blockSel.Face != BlockFacing.UP && blockSel.Face != BlockFacing.DOWN)
            {
                failureCode = RPVoiceChatMod.modID + ":BellHammer.FloorOrCeilingOnly";
                return false;
            }

            // Position can be the target (air) after behavior, or the clicked block (solid). Target = where we place.
            var blockAtSel = world.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos placePos = blockAtSel.Replaceable >= 6000 ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);

            // Always check for bell at the same level (horizontal from the hammer position).
            foreach (BlockFacing face in HorizontalFaces)
            {
                if (IsBellAt(world, placePos, face))
                    return true;
            }

            failureCode = RPVoiceChatMod.modID + ":BellHammer.NeedBell";
            return false;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;
            return DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            var blockAtSel = world.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos placePos = blockAtSel.Replaceable >= 6000 ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
            bool onFloor = (blockAtSel.Replaceable >= 6000 && blockSel.Face == BlockFacing.DOWN) || (blockAtSel.Replaceable < 6000 && blockSel.Face == BlockFacing.UP);
            string vVariant = onFloor ? "down" : "up"; 

            // Same orientation logic: face toward bell if found at same level (horizontal), else player facing.
            string sideVariant = BlockFacing.HorizontalFromAngle(byPlayer.Entity.Pos.Yaw).Code;
            foreach (BlockFacing face in HorizontalFaces)
            {
                if (IsBellAt(world, placePos, face))
                {
                    sideVariant = face.Code;
                    break;
                }
            }

            // Build path: bellhammer-{wood}-{v}-{side} (e.g. bellhammer-aged-up-north).
            string path = Code.Path;
            int last = path.LastIndexOf('-');
            int prev = last > 0 ? path.LastIndexOf('-', last - 1) : -1;
            string basePath = prev > 0 ? path.Substring(0, prev) : (last > 0 ? path.Substring(0, last) : "bellhammer-aged");
            AssetLocation codeWithVariants = Code.WithPath(basePath + "-" + vVariant + "-" + sideVariant);
            Vintagestory.API.Common.Block blockToPlace = world.GetBlock(codeWithVariants) ?? world.GetBlock(Code);
            world.BlockAccessor.SetBlock(blockToPlace.BlockId, placePos);
            return true;
        }

        /// <summary>
        /// Checks if there is a bell in the given direction.
        /// At 1 block: carillonbell or churchbell. At 2 blocks: churchbell only (carillonbell is smaller).
        /// </summary>
        private static bool IsBellAt(IWorldAccessor world, BlockPos fromPos, BlockFacing face)
        {
            var ba = world.BlockAccessor;
            if (IsBellBlock(ba.GetBlock(fromPos.AddCopy(face))))
                return true;
            if (IsChurchBellBlock(ba.GetBlock(fromPos.AddCopy(face).Add(face))))
                return true;
            return false;
        }

        private static bool IsBellBlock(Vintagestory.API.Common.Block block)
        {
            if (block == null) return false;
            string path = block.Code?.Path ?? "";
            return path.StartsWith("carillonbell") || path.StartsWith("churchbell");
        }

        private static bool IsChurchBellBlock(Vintagestory.API.Common.Block block)
        {
            if (block == null) return false;
            string path = block.Code?.Path ?? "";
            return path.StartsWith("churchbell");
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);
            // When a neighbour (e.g. axle) is placed, trigger mechanical network discovery again
            if (world.Side == EnumAppSide.Server)
            {
                var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBellHammer;
                be?.TryDiscoverNetwork();
            }
        }

        /// <summary>
        /// Override to prevent vanilla HorizontalOrientable.OnPickBlock crash (ItemStack with null block)
        /// when the HUD looks up the block variant for display.
        /// </summary>
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var blockAt = world.BlockAccessor.GetBlock(pos) as Vintagestory.API.Common.Block;
            if (blockAt != null && blockAt.Variant != null)
                return new ItemStack(blockAt, 1);
            var block = world.GetBlock(CodeWithVariant("side", "north"))
                ?? world.GetBlock(Code);
            return new ItemStack(block, 1);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBellHammer;
            if (be != null)
                return be.OnPlayerRightClick(byPlayer, blockSel);
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        // IMechanicalPowerBlock: connector is always on the horizontal face opposite to "side" (bell in front, axle on the other side).
        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            Vintagestory.API.Common.Block blockAtPos = world.BlockAccessor.GetBlock(pos);
            if (blockAtPos?.Variant == null || !blockAtPos.Variant.TryGetValue("side", out string sideStr)) return false;
            BlockFacing frontFace = BlockFacing.FromCode(sideStr);
            if (frontFace == null) return false;
            return face == frontFace.Opposite;
        }

        public void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            // Simple consumer: no-op (no block exchange like gears).
        }

        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            return world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.Network;
        }
    }
}
