using System;
using System.Reflection;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Audio.Effects
{
    /// <summary>
    /// Optional integration with the "Sound Physics Adapted" mod (modid: soundphysicsadapted).
    /// When that mod is installed, RPVoiceChat replaces its own wall-thickness muffling with
    /// SPA's material-aware, multi-ray occlusion for more realistic voice muffling.
    ///
    /// Everything here goes through reflection, so RPVoiceChat keeps zero compile-time
    /// dependency on SPA. All members degrade gracefully (no-op / passthrough) when SPA
    /// is absent or unavailable.
    /// </summary>
    public static class SoundPhysicsCompat
    {
        private const string ModSystemName = "soundphysicsadapted.SoundPhysicsAdaptedModSystem";
        private const string ApiTypeName = "soundphysicsadapted.SoundPhysicsAPI";

        private static bool initialized;
        private static PropertyInfo isAvailableProp;
        private static MethodInfo getOcclusionGainHFMethod;

        /// <summary>True if the SPA mod is loaded and its API was resolved successfully.</summary>
        public static bool IsInstalled { get; private set; }

        /// <summary>
        /// Resolve the SPA API via reflection. Safe to call multiple times; only runs once.
        /// Call once on the client after mods have loaded.
        /// </summary>
        public static void Init(ICoreClientAPI capi)
        {
            if (initialized) return;
            initialized = true;

            try
            {
                var modSystem = capi?.ModLoader?.GetModSystem(ModSystemName);
                if (modSystem == null) return; // SPA not installed

                Type apiType = modSystem.GetType().Assembly.GetType(ApiTypeName);
                if (apiType == null) return;

                isAvailableProp = apiType.GetProperty("IsAvailable", BindingFlags.Public | BindingFlags.Static);
                getOcclusionGainHFMethod = apiType.GetMethod(
                    "GetOcclusionGainHF",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Vec3d), typeof(Vec3d) },
                    null);

                IsInstalled = isAvailableProp != null && getOcclusionGainHFMethod != null;
                if (IsInstalled)
                    Logger.client.Notification("Sound Physics Adapted detected - using its occlusion for voice muffling");
            }
            catch (Exception e)
            {
                IsInstalled = false;
                Logger.client?.Warning($"Failed to initialize Sound Physics Adapted integration: {e.Message}");
            }
        }

        /// <summary>
        /// Whether SPA is currently ready to serve occlusion queries (installed, enabled in its
        /// own config, EFX available). Re-checked each call since the user can toggle SPA at runtime.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                if (!IsInstalled) return false;
                try { return (bool)isAvailableProp.GetValue(null); }
                catch { return false; }
            }
        }

        /// <summary>
        /// Query SPA for the lowpass gainHF between a speaker and a listener.
        /// 1.0 = clear line of sight, lower = more muffled. Returns 1.0 when SPA is unavailable.
        /// </summary>
        public static float GetOcclusionGainHF(Vec3d speakerPos, Vec3d listenerPos)
        {
            if (!IsInstalled) return 1f;
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
