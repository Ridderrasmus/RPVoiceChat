using HarmonyLib;
using RPVoiceChat.GameContent.Block;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RPVoiceChat
{
    internal static class BellHammerTemporalGearPatch
    {
        internal static void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ItemTemporalGear), nameof(ItemTemporalGear.OnHeldInteractStart)),
                prefix: new HarmonyMethod(typeof(BellHammerTemporalGearPatch), nameof(OnHeldInteractStart)));
        }

        private static bool OnHeldInteractStart(EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is not BellHammerBlock)
                return true;

            // Let the standard block interaction handle the click (and its access checks).
            // Do not start the gear's held-use callbacks or respawn-setting action.
            handHandling = EnumHandHandling.NotHandled;
            return false;
        }
    }
}
