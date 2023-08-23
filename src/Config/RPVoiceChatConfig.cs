namespace rpvoicechat
{
    public class RPVoiceChatConfig
    {

        // --- Server Config ---
        // These are meant to be set by server admins and
        // are used to configure the server
        public int ServerPort = 52525;
        public int MaximumConnections = 200;

        // --- Client Settings ---
        // These are meant to be set by the client, but are
        // stored here for persistence across sessions
        public bool PushToTalkEnabled = false;
        public bool IsLoopbackEnabled = false;
        public bool IsMuted = false;
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
            ServerPort = previousConfig.ServerPort;
            MaximumConnections = previousConfig.MaximumConnections;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsMuted = previousConfig.IsMuted;
            InputThreshold = previousConfig.InputThreshold;
            CurrentInputDevice = previousConfig.CurrentInputDevice;
            IsLoopbackEnabled = previousConfig.IsLoopbackEnabled;
        }

    }
}