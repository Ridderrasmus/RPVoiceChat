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
            var originalChangeDevice = AccessTools.Method(typeof(LoadedSoundNative), nameof(LoadedSoundNative.ChangeOutputDevice));
            var postfixChangeDevice = AccessTools.Method(typeof(LoadedSoundNativePatch), nameof(ChangeOutputDevicePostfix));
            harmony.Patch(originalChangeDevice, postfix: new HarmonyMethod(postfixChangeDevice));

            // Removed GlobalVolume patch - volume is now fully managed through OpenAL gain settings
        }

        public static void ChangeOutputDevicePostfix()
        {
            OnOutputDeviceChange?.Invoke();
        }
    }
}
