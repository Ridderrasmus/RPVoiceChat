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
        public int MaxConnectionAttempts { get; set; } = 5;

        // Features
        public bool AdditionalContent { get; set; } = true;
        public bool TelegraphContent { get; set; } = true;

        // Audio Performance Settings
        public float MaxAudioGain { get; set; } = 2f;
        public float MaxVolumeLimit { get; set; } = 0.8f;

        // Codec Settings
        public int NormalBitrate { get; set; } = 40 * 1024; // 40 kbps
        public int BroadcastBitrate { get; set; } = 16 * 1024; // 16 kbps

        // Communication Systems
        public int TelegraphMaxConnectionDistance { get; set; } = 20;
        public int TelegraphMaxConnectionsPerNode { get; set; } = 4;
        public int TelegraphMaxMessageLength { get; set; } = 100;
        public int TelegraphMinDelayBetweenKeysMs { get; set; } = 200;
        public double BellRingCooldownSeconds { get; set; } = 1.5;
        public bool TelegraphGenuineMorseCharacters { get; set; } = false;
        public int TelegraphMessageDeletionDelaySeconds { get; set; } = 10;

        // Sound Emitting Objects Range Settings
        public int HandbellAudibleDistance { get; set; } = 16;
        public int CallbellAudibleDistance { get; set; } = 16;
        public int RoyalhornAudibleDistance { get; set; } = 72;
        public int WarhornAudibleDistance { get; set; } = 128;
        public int CarillonbellAudibleDistance { get; set; } = 256;
        public int ChurchbellAudibleDistance { get; set; } = 832;
        public int MegaphoneAudibleDistance { get; set; } = 125;

        public RPVoiceChatServerConfig() { }

        public RPVoiceChatServerConfig(ICoreAPI api, RPVoiceChatServerConfig previousConfig = null)
        {
            if (previousConfig == null) return;

            // Network Settings
            ServerPort = previousConfig.ServerPort;
            ServerIP = previousConfig.ServerIP;
            UseCustomNetworkServers = previousConfig.UseCustomNetworkServers;
            ManualPortForwarding = previousConfig.ManualPortForwarding;
            MaxConnectionAttempts = previousConfig.MaxConnectionAttempts;

            // Features
            AdditionalContent = previousConfig.AdditionalContent;
            TelegraphContent = previousConfig.TelegraphContent;

            // Audio Performance Settings
            MaxAudioGain = previousConfig.MaxAudioGain;
            MaxVolumeLimit = previousConfig.MaxVolumeLimit;

            // Codec Settings
            NormalBitrate = previousConfig.NormalBitrate;
            BroadcastBitrate = previousConfig.BroadcastBitrate;

            // Communication Systems
            TelegraphMaxConnectionDistance = previousConfig.TelegraphMaxConnectionDistance;
            TelegraphMaxConnectionsPerNode = previousConfig.TelegraphMaxConnectionsPerNode;
            TelegraphMaxMessageLength = previousConfig.TelegraphMaxMessageLength;
            TelegraphMinDelayBetweenKeysMs = previousConfig.TelegraphMinDelayBetweenKeysMs;
            BellRingCooldownSeconds = previousConfig.BellRingCooldownSeconds;
            TelegraphGenuineMorseCharacters = previousConfig.TelegraphGenuineMorseCharacters;

            // Sound Emitting Objects Range Settings
            HandbellAudibleDistance = previousConfig.HandbellAudibleDistance;
            CallbellAudibleDistance = previousConfig.CallbellAudibleDistance;
            RoyalhornAudibleDistance = previousConfig.RoyalhornAudibleDistance;
            WarhornAudibleDistance = previousConfig.WarhornAudibleDistance;
            CarillonbellAudibleDistance = previousConfig.CarillonbellAudibleDistance;
            ChurchbellAudibleDistance = previousConfig.ChurchbellAudibleDistance;
            MegaphoneAudibleDistance = previousConfig.MegaphoneAudibleDistance;
        }
    }
}