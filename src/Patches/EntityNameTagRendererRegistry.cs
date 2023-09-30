using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using static Vintagestory.GameContent.EntityNameTagRendererRegistry;

namespace RPVoiceChat
{
    static class EntityNameTagRendererRegistryPatch
    {
        public static void Patch(Harmony harmony)
        {
            var OriginalField = AccessTools.Field(typeof(EntityNameTagRendererRegistry), nameof(EntityNameTagRendererRegistry.DefaultNameTagRenderer));
            var OriginalMethod1 = RuntimeReflectionExtensions.GetMethodInfo((NameTagRendererDelegate)OriginalField.GetValue(null));
            var PrefixMethod1 = AccessTools.Method(typeof(EntityNameTagRendererRegistryPatch), nameof(DefaultNameTagRenderer));
            harmony.Patch(OriginalMethod1, prefix: new HarmonyMethod(PrefixMethod1));

            var OriginalMethod2 = AccessTools.Method(typeof(DefaultEntitlementTagRenderer), nameof(DefaultEntitlementTagRenderer.renderTag));
            var PrefixMethod2 = AccessTools.Method(typeof(EntityNameTagRendererRegistryPatch), nameof(DefaultEntitlementTagRenderer_renderTag));
            harmony.Patch(OriginalMethod2, prefix: new HarmonyMethod(PrefixMethod2));
        }

        public static bool DefaultEntitlementTagRenderer_renderTag(ref LoadedTexture __result, double[] ___color, TextBackground ___background, Entity entity)
        {
            if (entity is not EntityPlayer) return true;

            __result = PlayerNameTagRenderer.GetRenderer((EntityPlayer)entity, ___color, ___background);
            return false;
        }

        public static bool DefaultNameTagRenderer(ref LoadedTexture __result, Entity entity)
        {
            if (entity is not EntityPlayer) return true;

            __result = PlayerNameTagRenderer.GetRenderer((EntityPlayer)entity);
            return false;
        }
    }
}
