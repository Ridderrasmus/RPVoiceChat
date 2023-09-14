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
        public string[] DisabledRecipes = new string[] { "item-code" };

        // --- Client Settings ---
        // These are meant to be set by the client, but are
        // stored here for persistence across sessions
        public bool PushToTalkEnabled = false;
        public bool IsLoopbackEnabled = false;
        public bool IsHUDShown = true;
        public bool IsMuted = false;
        public int OutputGain = 200;
        public int InputGain = 100;
        public int InputThreshold = 20;
        public string CurrentInputDevice = OpenTK.Audio.AudioCapture.DefaultDevice;
        // These are client settings but they are not
        // accessible from the in-game settings menu and
        // are only meant to be modified by someone who
        // knows what they are doing
        public double MaxInputThreshold = 0.24;

        public RPVoiceChatConfig() 
        { 

        }

        public RPVoiceChatConfig(RPVoiceChatConfig previousConfig)
        {
            ManualPortForwarding = previousConfig.ManualPortForwarding;

            ServerPort = previousConfig.ServerPort;
            ServerIP = previousConfig.ServerIP;
            DisabledRecipes = previousConfig.DisabledRecipes;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsHUDShown = previousConfig.IsHUDShown;
            IsMuted = previousConfig.IsMuted;
            OutputGain = previousConfig.OutputGain;
            InputGain = previousConfig.InputGain;
            InputThreshold = previousConfig.InputThreshold;
            CurrentInputDevice = previousConfig.CurrentInputDevice;
            IsLoopbackEnabled = previousConfig.IsLoopbackEnabled;
        }

    }
}