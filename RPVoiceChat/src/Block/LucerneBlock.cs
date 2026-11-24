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
    /// Place only on stone ground. Sneak + RMB = show structure preview; Ctrl+Shift+RMB = hide preview; RMB = open UI when structure complete.
    /// </summary>
    public class LucerneBlock : Vintagestory.API.Common.Block
    {
        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;

            // Raw lucerne = plain clay block: place anywhere like other molds. Fired = beacon base: stone ground only.
            if (Variant?["materialtype"] as string != "fired")
                return true;

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
            // Only fired lucernes are the warning beacon (guide + fuel UI).
            if (Variant?["materialtype"] as string != "fired")
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);

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
                        return be != null && !be.StructureComplete && !be.ShowStructureGuide;
                    }
                },
                // Ctrl + Shift + RMB = hide preview (HotKeyCodes so both modifiers show in the HUD)
                new WorldInteraction
                {
                    ActionLangCode = RPVoiceChatMod.modID + ":WarningBeacon.Interaction.HideGuide",
                    HotKeyCodes = new[] { "ctrl", "shift" },
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) =>
                    {
                        var be = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityLucerne;
                        return be != null && !be.StructureComplete && be.ShowStructureGuide;
                    }
                }
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Raw lucerne must not open structure guide or beacon UI (no alarm fire).
            if (Variant?["materialtype"] as string != "fired")
                return base.OnBlockInteractStart(world, byPlayer, blockSel);

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityLucerne;
            if (be == null) return false;

            var ctrls = byPlayer.Entity.Controls;
            // Ctrl + Shift + RMB = hide preview only (ShiftKey/CtrlKey = mouse modifiers per API)
            if (be.ShowStructureGuide && ctrls.CtrlKey && ctrls.ShiftKey)
            {
                be.ToggleStructureGuide(byPlayer);
                return true;
            }
            // Sneak + RMB = show preview when hidden (do not toggle off with sneak alone)
            if (!be.ShowStructureGuide && ctrls.Sneak)
            {
                be.ToggleStructureGuide(byPlayer);
                return true;
            }
            // Guide visible but sneak without ctrl+shift: consume to avoid opening nothing
            if (be.ShowStructureGuide && ctrls.Sneak)
                return true;

            if (be.StructureComplete)
            {
                return be.OnPlayerRightClick(byPlayer, blockSel);
            }

            return true;
        }
    }
}
