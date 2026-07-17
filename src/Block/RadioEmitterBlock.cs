using RPVoiceChat.GameContent.BlockEntity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat.GameContent.Block
{
    public class RadioEmitterBlock : WireNodeBlock, IMechanicalPowerBlock
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "rpvoicechat:Radio.Emitter.Interaction.Use",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code.ToShortString() == "rpvoicechat:telegraphwire")
            {
                return false;
            }

            var emitter = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityRadioEmitter;
            emitter?.OnInteract(byPlayer);
            return true;
        }

        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            Vintagestory.API.Common.Block blockAtPos = world.BlockAccessor.GetBlock(pos);
            if (blockAtPos?.Variant == null || !blockAtPos.Variant.TryGetValue("side", out string sideStr))
            {
                return false;
            }

            BlockFacing frontFace = BlockFacing.FromCode(sideStr);
            return frontFace != null && face == frontFace;
        }

        public void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
        }

        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            return world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.Network;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);
            if (world.Side == EnumAppSide.Server)
            {
                (world.BlockAccessor.GetBlockEntity(pos) as BlockEntityRadioEmitter)?.TryDiscoverNetwork();
            }
        }
    }
}
