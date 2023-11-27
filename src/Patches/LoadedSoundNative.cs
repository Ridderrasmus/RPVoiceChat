using HarmonyLib;
using RPVoiceChat.Audio;
using System;
using Vintagestory.Client;

namespace RPVoiceChat
{
    static class LoadedSoundNativePatch
    {
        public static event Action OnOutputDeviceChange;

        public static void Patch(Harmony harmony)
        {
            var OriginalMethod1 = AccessTools.Method(typeof(LoadedSoundNative), nameof(LoadedSoundNative.ChangeOutputDevice));
            var PostfixMethod1 = AccessTools.Method(typeof(LoadedSoundNativePatch), nameof(ChangeOutputDevice));
            harmony.Patch(OriginalMethod1, postfix: new HarmonyMethod(PostfixMethod1));

            var OriginalMethod2 = AccessTools.PropertyGetter(typeof(LoadedSoundNative), nameof(GlobalVolume));
            var PostfixMethod2 = AccessTools.Method(typeof(LoadedSoundNativePatch), nameof(GlobalVolume));
            harmony.Patch(OriginalMethod2, postfix: new HarmonyMethod(PostfixMethod2));
        }

        public static void ChangeOutputDevice()
        {
            OnOutputDeviceChange?.Invoke();
        }

        public static void GlobalVolume(ref float __result)
        {
            var globalGain = PlayerListener.gain;
            if (globalGain < 1) return;
            __result = __result / globalGain;
        }
    }
}
