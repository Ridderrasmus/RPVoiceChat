using Vintagestory.API.Common;

namespace RPVoiceChat.Config
{
    public class RPVoiceChatClientConfig : IModConfig
    {
        // Audio Output Settings
        public float OutputVoice { get; set; } = 1f;
        public float OutputBlock { get; set; } = 0.6f;
        public float OutputItem { get; set; } = 0.6f;

        // Audio Input Settings
        public float InputGain { get; set; } = 1f;
        public float InputThreshold { get; set; } = 0.36f;
        public string InputDevice { get; set; } = null;

        // Voice Processing
        public bool Denoising { get; set; } = false;
        public float DenoisingSensitivity { get; set; } = 0.5f;
        public float DenoisingStrength { get; set; } = 0.1f;

        // UI Settings
        public bool ShowHud { get; set; } = true;
        public int ActiveConfigTab { get; set; } = 0;

        // Behavior Settings
        public bool PushToTalkEnabled { get; set; } = false;
        public bool IsMuted { get; set; } = false;
        public bool Loopback { get; set; } = false;
        public bool Muffling { get; set; } = true;
        public bool ChannelGuessing { get; set; } = true;

        // System Settings
        public bool FirstTimeUse { get; set; } = true;

        public RPVoiceChatClientConfig() { }

        public RPVoiceChatClientConfig(ICoreAPI api, RPVoiceChatClientConfig previousConfig = null)
        {
            if (previousConfig == null) return;

            OutputVoice = previousConfig.OutputVoice;
            OutputBlock = previousConfig.OutputBlock;
            OutputItem = previousConfig.OutputItem;

            InputGain = previousConfig.InputGain;
            InputThreshold = previousConfig.InputThreshold;
            InputDevice = previousConfig.InputDevice;

            Denoising = previousConfig.Denoising;
            DenoisingSensitivity = previousConfig.DenoisingSensitivity;
            DenoisingStrength = previousConfig.DenoisingStrength;

            ShowHud = previousConfig.ShowHud;
            ActiveConfigTab = previousConfig.ActiveConfigTab;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsMuted = previousConfig.IsMuted;
            Loopback = previousConfig.Loopback;
            Muffling = previousConfig.Muffling;
            ChannelGuessing = previousConfig.ChannelGuessing;

            FirstTimeUse = previousConfig.FirstTimeUse;
        }
    }
}