using HarmonyLib;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace RPVoiceChat
{
    /// <summary>
    /// When a block has both MPConsumer and Animatable, BEBehaviorMPBase.OnTesselation returns true,
    /// which skips the default JSON mesh. With no active animation, nothing draws → block invisible.
    /// This patch returns false when the block entity has Animatable, so the chunk tessellator
    /// adds the default mesh. The block stays visible (chunk mesh for base, Animatable for animations).
    /// </summary>
    internal static class BEBehaviorMPBasePatch
    {
        internal static void Patch(Harmony harmony)
        {
            var original = AccessTools.Method(typeof(BEBehaviorMPBase), nameof(BEBehaviorMPBase.OnTesselation));
            var prefix = AccessTools.Method(typeof(BEBehaviorMPBasePatch), nameof(OnTesselation_Prefix));
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));
        }

        /// <summary>
        /// If the block entity has Animatable, return false to allow default JSON mesh processing.
        /// </summary>
        public static bool OnTesselation_Prefix(BEBehaviorMPBase __instance, ref bool __result)
        {
            if (__instance?.Blockentity == null) return true;

            var animatable = __instance.Blockentity.GetBehavior<Vintagestory.GameContent.BEBehaviorAnimatable>();
            if (animatable != null)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}
