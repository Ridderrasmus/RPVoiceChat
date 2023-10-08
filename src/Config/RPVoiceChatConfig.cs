namespace RPVoiceChat
{
    public class RPVoiceChatConfig
    {

        // --- Shared Config ---
        // These are meant to be set by anyone and
        // are used to change mod behavior
        public bool ManualPortForwarding = false;

        // --- Server Config ---
        // These are meant to be set by server admins and
        // are used to configure the server
        public int ServerPort = 52525;
        public string ServerIP = null;
        public bool AdditionalContent = true;

        // --- Client Settings ---
        // These are meant to be set by the client, but are
        // stored here for persistence across sessions
        public bool PushToTalkEnabled = false;
        public bool IsLoopbackEnabled = false;
        public bool IsDenoisingEnabled = false;
        public bool IsHUDShown = true;
        public bool IsMuted = false;
        public int OutputGain = 100;
        public int InputGain = 100;
        public int InputThreshold = 20;
        public int BackgroungNoiseThreshold = 50;
        public int VoiceDenoisingStrength = 80;
        public string CurrentInputDevice;
        public double MaxInputThreshold = 0.24;

        public RPVoiceChatConfig()
        {

        }

        public RPVoiceChatConfig(RPVoiceChatConfig previousConfig)
        {
            ManualPortForwarding = previousConfig.ManualPortForwarding;

            ServerPort = previousConfig.ServerPort;
            ServerIP = previousConfig.ServerIP;
            AdditionalContent = previousConfig.AdditionalContent;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsLoopbackEnabled = previousConfig.IsLoopbackEnabled;
            IsDenoisingEnabled = previousConfig.IsDenoisingEnabled;
            IsHUDShown = previousConfig.IsHUDShown;
            IsMuted = previousConfig.IsMuted;
            OutputGain = previousConfig.OutputGain;
            InputGain = previousConfig.InputGain;
            InputThreshold = previousConfig.InputThreshold;
            CurrentInputDevice = previousConfig.CurrentInputDevice;
            BackgroungNoiseThreshold = previousConfig.BackgroungNoiseThreshold;
            VoiceDenoisingStrength = previousConfig.VoiceDenoisingStrength;
            MaxInputThreshold = previousConfig.MaxInputThreshold;
        }

    }
}