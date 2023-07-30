namespace rpvoicechat
{
    public class RPVoiceChatConfig
    {

        public int ServerPort = 52525;

        public bool PushToTalkEnabled = false;
        public bool IsMuted = false;
        public int InputThreshold = 40;
        public string CurrentInputDevice;
        public int CurrentInputDeviceIndex = 0;
        public string CurrentOutputDevice;
        public int CurrentOutputDeviceIndex = 0;

        public RPVoiceChatConfig() 
        { 

        }

        public RPVoiceChatConfig(RPVoiceChatConfig previousConfig)
        {
            ServerPort = previousConfig.ServerPort;

            PushToTalkEnabled = previousConfig.PushToTalkEnabled;
            IsMuted = previousConfig.IsMuted;
            InputThreshold = previousConfig.InputThreshold;
            CurrentInputDevice = previousConfig.CurrentInputDevice;
            CurrentOutputDevice = previousConfig.CurrentOutputDevice;
        }

    }
}