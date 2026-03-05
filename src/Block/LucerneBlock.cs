using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RPVoiceChat.GameContent.Block
{
    /// <summary>
    /// Lucerne block: base of the Warning Beacon multiblock.
    /// Place only on stone ground. Sneak + right-click = structure guide; right-click = open UI (when structure complete).
    /// </summary>
    public class LucerneBlock : Vintagestory.API.Common.Block
    {
        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;

            var blockAtSel = world.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos placePos = blockAtSel.Replaceable >= 6000 ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
            BlockPos below = placePos.DownCopy();
            var blockBelow = world.BlockAccessor.GetBlock(below);

            if (!WarningBeaconStructure.IsValidConstructionBlock(blockBelow))
            {
                failureCode = RPVoiceChatMod.modID + ":Lucerne.NeedStoneGround";
                return false;
            }
            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = RPVoiceChatMod.modID + ":WarningBeacon.Interaction.LoadFuel",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        var be = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityLucerne;
                        return be != null && be.StructureComplete;
                    }
                },
                new WorldInteraction
                {
                    ActionLangCode = RPVoiceChatMod.modID + ":WarningBeacon.Interaction.ShowGuide",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        var be = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityLucerne;
                        return be != null && !be.StructureComplete;
                    }
                }
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityLucerne;
            if (be == null) return false;

            // Sneak + right-click = structure guide (like vanilla kiln)
            bool showGuide = byPlayer.Entity.Controls.Sneak;
            if (showGuide)
            {
                be.ToggleStructureGuide(byPlayer);
                return true;
            }

            if (be.StructureComplete)
            {
                return be.OnPlayerRightClick(byPlayer, blockSel);
            }

            return true;
        }
    }
}
