using System;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Util
{
    /// <summary>
    /// Provides compatibility with Sound Physics Adapted for voice muffling.
    /// That mod calculates occlusion with multiple rays and the sound absorption of each
    /// block material, so RPVoiceChat can use it in place of its own wall thickness muffling.
    /// Every call goes through reflection, so RPVoiceChat keeps no dependency on that mod.
    /// </summary>
    public static class SoundPhysicsCompatibility
    {
        public const string ModId = "soundphysicsadapted";

        private const string ModSystemName = "soundphysicsadapted.SoundPhysicsAdaptedModSystem";
        private const string ApiTypeName = "soundphysicsadapted.SoundPhysicsAPI";

        private static bool initialized;
        private static bool installed;
        private static PropertyInfo isAvailableProperty;
        private static MethodInfo getOcclusionGainHFMethod;

        /// <summary>Resolves the API of the other mod. Only the first call does the work.</summary>
        public static void Init(ICoreClientAPI api)
        {
            if (initialized) return;
            initialized = true;

            try
            {
                var modSystem = api?.ModLoader?.GetModSystem(ModSystemName);
                if (modSystem == null) return;

                Type apiType = modSystem.GetType().Assembly.GetType(ApiTypeName);
                if (apiType == null) return;

                isAvailableProperty = apiType.GetProperty("IsAvailable", BindingFlags.Public | BindingFlags.Static);
                getOcclusionGainHFMethod = apiType.GetMethod(
                    "GetOcclusionGainHF",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Vec3d), typeof(Vec3d) },
                    null);

                installed = isAvailableProperty != null && getOcclusionGainHFMethod != null;
                if (installed) Logger.client?.Notification("Sound Physics Adapted detected. Voices use its occlusion");
            }
            catch (Exception e)
            {
                installed = false;
                Logger.client?.Warning($"Failed to integrate with Sound Physics Adapted: {e.Message}");
            }
        }

        /// <summary>True while the other mod can answer queries. The player can turn it off at any time.</summary>
        public static bool IsAvailable
        {
            get
            {
                if (!installed) return false;
                try { return (bool)isAvailableProperty.GetValue(null); }
                catch { return false; }
            }
        }

        /// <summary>
        /// Gets the lowpass gainHF between a speaker and a listener, where 1.0 is a clear line of sight.
        /// Returns 1.0 when the other mod is absent or the query fails.
        /// </summary>
        public static float GetOcclusionGainHF(Vec3d speakerPos, Vec3d listenerPos)
        {
            if (!installed) return 1f;

            try
            {
                return (float)getOcclusionGainHFMethod.Invoke(null, new object[] { speakerPos, listenerPos });
            }
            catch (Exception e)
            {
                Logger.client?.Debug($"Sound Physics Adapted occlusion query failed: {e.Message}");
                return 1f;
            }
        }
    }
}
