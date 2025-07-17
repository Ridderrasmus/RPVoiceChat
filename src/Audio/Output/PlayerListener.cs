using OpenTK.Audio.OpenAL;
using Vintagestory.API.Client;

namespace RPVoiceChat.Audio
{
    public static class PlayerListener
    {
        public static float VoiceGain = 1f;
        public static float BlockGain = 1f;
        public static float ItemGain = 1f;

        private static ICoreClientAPI capi;

        public static void Init(ICoreClientAPI api)
        {
            capi = api;

            VoiceGain = ClientSettings.OutputVoice;
            BlockGain = ClientSettings.OutputBlock;
            ItemGain = ClientSettings.OutputItem;
        }

        public static void SetVoiceGain(float gain)
        {
            if (gain == VoiceGain) return;

            VoiceGain = gain;
            ApplyVoiceGain();
        }

        public static void SetBlockGain(float gain)
        {
            if (gain == BlockGain) return;
            BlockGain = gain;
        }

        public static void SetItemGain(float gain)
        {
            if (gain == ItemGain) return;
            ItemGain = gain;
        }

        private static void ApplyVoiceGain()
        {
            capi.Settings.Int["soundLevel"] += 1;
            capi.Settings.Int["soundLevel"] -= 1;
        }

   
        public static float GetGainForSource(string sourceType)
        {
            return sourceType switch
            {
                "voice" => VoiceGain,
                "block" => BlockGain,
                "item" => ItemGain,
                _ => 1f
            };
        }

        public static void Dispose()
        {
            capi = null;
            VoiceGain = 1f;
            BlockGain = 1f;
            ItemGain = 1f;
        }
    }
}
