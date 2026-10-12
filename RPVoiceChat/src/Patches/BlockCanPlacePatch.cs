using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using RPVoiceChat.Systems;

namespace RPVoiceChat
{
    /// <summary>
    /// Prevents placing any block in the reserved 3×3 zone at the center above a warning beacon.
    /// </summary>
    internal static class BlockCanPlacePatch
    {
        internal static void Patch(Harmony harmony)
        {
            var original = AccessTools.Method(typeof(Block), nameof(Block.CanPlaceBlock));
            var prefix = AccessTools.Method(typeof(BlockCanPlacePatch), nameof(CanPlaceBlock_Prefix));
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));
        }

        public static bool CanPlaceBlock_Prefix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode, ref bool __result)
        {
            if (world?.BlockAccessor == null || blockSel == null) return true;

            var blockAtSel = world.BlockAccessor.GetBlock(blockSel.Position);
            BlockPos placePos = blockAtSel.Replaceable >= 6000
                ? blockSel.Position
                : blockSel.Position.AddCopy(blockSel.Face);

            if (WarningBeaconStructure.IsInReservedZone(world.BlockAccessor, placePos))
            {
                failureCode = RPVoiceChatMod.modID + ":WarningBeacon.ReservedZone";
                __result = false;
                return false;
            }

            return true;
        }
    }
}
