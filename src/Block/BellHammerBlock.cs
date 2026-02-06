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

            BlockPos pos = blockSel.Position;
            foreach (BlockFacing face in HorizontalFaces)
            {
                if (IsBellBlock(world, pos.AddCopy(face)))
                {
                    return true;
                }
            }

            failureCode = RPVoiceChatMod.modID + ":BellHammer.NeedBell";
            return false;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            BlockPos pos = blockSel.Position;
            string sideVariant = null;

            foreach (BlockFacing face in HorizontalFaces)
            {
                if (IsBellBlock(world, pos.AddCopy(face)))
                {
                    sideVariant = FaceToSideVariant(face);
                    break;
                }
            }

            if (sideVariant == null)
                sideVariant = "north";

            int lastDash = Code.Path.LastIndexOf('-');
            string basePath = lastDash > 0 ? Code.Path.Substring(0, lastDash) : "bellhammer";
            AssetLocation codeWithSide = Code.WithPath(basePath + "-" + sideVariant);
            Vintagestory.API.Common.Block blockToPlace = world.GetBlock(codeWithSide) ?? world.GetBlock(Code);
            world.BlockAccessor.SetBlock(blockToPlace.BlockId, pos);
            return true;
        }

        private static bool IsBellBlock(IWorldAccessor world, BlockPos pos)
        {
            Vintagestory.API.Common.Block block = world.BlockAccessor.GetBlock(pos);
            if (block == null) return false;
            string path = block.Code?.Path ?? "";
            return path.StartsWith("carillonbell") || path.StartsWith("churchbell");
        }

        private static string FaceToSideVariant(BlockFacing face)
        {
            return face.Code;
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

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBellHammer;
            if (be != null)
                return be.OnPlayerRightClick(byPlayer, blockSel);
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        // IMechanicalPowerBlock: prevents NullReferenceException in BEBehaviorMPBase.spreadTo when an axle connects to the hammer.
        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            Vintagestory.API.Common.Block blockAtPos = world.BlockAccessor.GetBlock(pos);
            if (blockAtPos?.Variant == null || !blockAtPos.Variant.TryGetValue("side", out string sideStr))
                return false;
            BlockFacing frontFace = BlockFacing.FromCode(sideStr);
            if (frontFace == null) return false;
            // Mechanical connector on the face opposite to the bell (axle side).
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
