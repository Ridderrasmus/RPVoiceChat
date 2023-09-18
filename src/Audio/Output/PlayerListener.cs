using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;

namespace RPVoiceChat.Audio
{
    public static class PlayerListener
    {
        public static float gain = 1;
        private static ICoreClientAPI capi;

        public static void Init(ICoreClientAPI api)
        {
            capi = api;
            SetGain(ModConfig.Config.OutputGain);

            ModConfig.ConfigUpdated += OnConfigUpdate;
        }

        public static void SetGain(float newGain)
        {
            newGain /= 100f;
            if (newGain == gain) return;

            gain = newGain;
            if (gain < 1) return;

            //Force game to update volume of already playing sounds
            capi.Settings.Int["soundLevel"] = capi.Settings.Int["soundLevel"] + 1;
            capi.Settings.Int["soundLevel"] = capi.Settings.Int["soundLevel"] - 1;

            OALW.Listener(ALListenerf.Gain, gain);
        }

        private static void OnConfigUpdate()
        {
            SetGain(ModConfig.Config.OutputGain);
        }

        public static void Dispose()
        {
            capi = null;
            gain = 1;
            ModConfig.ConfigUpdated -= OnConfigUpdate;
        }
    }
}
