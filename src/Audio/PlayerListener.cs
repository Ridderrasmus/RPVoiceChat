using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using RPVoiceChat.Utils;
using Vintagestory.API.Client;

namespace RPVoiceChat.Audio
{
    public class PlayerListener
    {
        private ICoreClientAPI capi;
        private float gain = 1;

        public PlayerListener(ICoreClientAPI api)
        {
            capi = api;
            SetGain(ModConfig.Config.OutputGain);

            ModConfig.ConfigUpdated += OnConfigUpdate;
        }

        public void SetGain(float newGain)
        {
            newGain /= 100f;
            if (newGain == gain) return;

            gain = newGain;
            OALW.Listener(ALListenerf.Gain, gain);
        }

        private void OnConfigUpdate()
        {
            SetGain(ModConfig.Config.OutputGain);
        }
    }
}
