using Vintagestory.API.Common;

namespace RPVoiceChat.Config
{
    public class RPVoiceChatServerConfig : IModConfig
    {
        // Network Settings
        public int ServerPort { get; set; } = 52525;
        public string ServerIP { get; set; } = null;
        public bool UseCustomNetworkServers { get; set; } = false;
        public bool ManualPortForwarding { get; set; } = false;

        // Features
        public bool AdditionalContent { get; set; } = true;

        public RPVoiceChatServerConfig() { }

        public RPVoiceChatServerConfig(ICoreAPI api, RPVoiceChatServerConfig previousConfig = null)
        {
            if (previousConfig == null) return;

            ServerPort = previousConfig.ServerPort;
            ServerIP = previousConfig.ServerIP;
            UseCustomNetworkServers = previousConfig.UseCustomNetworkServers;
            ManualPortForwarding = previousConfig.ManualPortForwarding;
            AdditionalContent = previousConfig.AdditionalContent;
        }
    }
}