using HarmonyLib;
using Vintagestory.GameContent;

namespace RPVoiceChat
{
    /// <summary>
    /// Patch to allow items with helvehammerworkable attribute to be worked on the helve hammer.
    /// Inspired by DArkHekRoMaNT's HelveHammerExtensions mod.
    /// </summary>
    internal class HelveHammerPatch
    {
        internal static void Patch(Harmony harmony)
        {
            var originalMethod = AccessTools.Method(typeof(ItemWorkItem), nameof(ItemWorkItem.GetHelveWorkableMode));
            var prefixMethod = AccessTools.Method(typeof(HelveHammerPatch), nameof(GetHelveWorkableMode));
            harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
        }

        public static bool GetHelveWorkableMode(ref EnumHelveWorkableMode __result, ref BlockEntityAnvil beAnvil)
        {

            try
            {
                var jsonOutput = beAnvil.SelectedRecipe.Output;
                if (jsonOutput != null && jsonOutput.Code != null && beAnvil.Api?.World != null)
                {
                    // Get the Collectible from the item code using World
                    var collectible = beAnvil.Api.World.GetItem(jsonOutput.Code);
                    if (collectible != null)
                    {
                        var attr = collectible.Attributes;
                        if (attr != null && attr["helvehammerworkable"].AsBool())
                        {
                            // If the output has helvehammerworkable attribute set to true, allow it
                            __result = EnumHelveWorkableMode.TestSufficientVoxelsWorkable;
                            return false; // Skip original method
                        }
                    }
                }
            }
            catch
            {
                // If anything goes wrong, fall back to vanilla behavior
                return true;
            }

            return true; // Continue with vanilla behavior
        }
    }
}


