using Vintagestory.API.Common;
using RPVoiceChat.Utils;

namespace RPVoiceChat.Config
{
    /// <summary>
    /// Manager pour accéder facilement aux configurations serveur depuis le code
    /// Remplace les constantes hardcodées par des valeurs configurables
    /// </summary>
    public static class ServerConfigManager
    {
        public static RPVoiceChatServerConfig Config => ModConfig.ServerConfig;

        // Audio Performance Settings
        public static float MaxAudioGain => Config?.MaxAudioGain ?? 2f;
        public static float MaxVolumeLimit => Config?.MaxVolumeLimit ?? 0.8f;

        // Codec Settings
        public static int NormalBitrate => Config?.NormalBitrate ?? (40 * 1024);
        public static int BroadcastBitrate => Config?.BroadcastBitrate ?? (16 * 1024);

        // Network Settings
        public static int MaxConnectionAttempts => Config?.MaxConnectionAttempts ?? 5;

        // Communication Systems
        public static int TelegraphMaxConnectionDistance => Config?.TelegraphMaxConnectionDistance ?? 20;
        public static int TelegraphMaxConnectionsPerNode => Config?.TelegraphMaxConnectionsPerNode ?? 4;
        public static int TelegraphMaxMessageLength => Config?.TelegraphMaxMessageLength ?? 100;
        public static int TelegraphMinDelayBetweenKeysMs => Config?.TelegraphMinDelayBetweenKeysMs ?? 200;
        public static double BellRingCooldownSeconds => Config?.BellRingCooldownSeconds ?? 1.5;
        public static bool TelegraphGenuineMorseCharacters => Config?.TelegraphGenuineMorseCharacters ?? false;
        public static int PrinterAutoSaveDelaySeconds => Config?.PrinterAutoSaveDelaySeconds ?? 10;

        // Sound Emitting Objects Range Settings
        public static int HandbellAudibleDistance => Config?.HandbellAudibleDistance ?? 16;
        public static int CallbellAudibleDistance => Config?.CallbellAudibleDistance ?? 16;
        public static int RoyalhornAudibleDistance => Config?.RoyalhornAudibleDistance ?? 72;
        public static int WarhornAudibleDistance => Config?.WarhornAudibleDistance ?? 128;
        public static int CarillonbellAudibleDistance => Config?.CarillonbellAudibleDistance ?? 256;
        public static int ChurchbellAudibleDistance => Config?.ChurchbellAudibleDistance ?? 832;
        public static int MegaphoneAudibleDistance => Config?.MegaphoneAudibleDistance ?? 125;

        /// <summary>
        /// Validates configuration values to avoid invalid values
        /// </summary>
        public static void ValidateConfig()
        {
            if (Config == null) return;

            if (MaxAudioGain < 0.1f || MaxAudioGain > 10f)
            {
                Logger.server.Warning($"MaxAudioGain ({MaxAudioGain}) should be between 0.1 and 10. Using default (2.0).");
                Config.MaxAudioGain = 2f;
            }

            if (MaxVolumeLimit < 0.1f || MaxVolumeLimit > 1.0f)
            {
                Logger.server.Warning($"MaxVolumeLimit ({MaxVolumeLimit}) should be between 0.1 and 1.0. Using default (0.8).");
                Config.MaxVolumeLimit = 0.8f;
            }

            // Codec validation
            if (NormalBitrate < 8 * 1024 || NormalBitrate > 128 * 1024)
            {
                Logger.server.Warning($"NormalBitrate ({NormalBitrate}) should be between 8 and 128 kbps. Using default (40 kbps).");
                Config.NormalBitrate = 40 * 1024;
            }

            if (BroadcastBitrate < 4 * 1024 || BroadcastBitrate > 64 * 1024)
            {
                Logger.server.Warning($"BroadcastBitrate ({BroadcastBitrate}) should be between 4 and 64 kbps. Using default (16 kbps).");
                Config.BroadcastBitrate = 16 * 1024;
            }

            // Communication systems validation
            if (TelegraphMaxConnectionDistance < 5 || TelegraphMaxConnectionDistance > 100)
            {
                Logger.server.Warning($"TelegraphMaxConnectionDistance ({TelegraphMaxConnectionDistance}) should be between 5 and 100. Using default (20).");
                Config.TelegraphMaxConnectionDistance = 20;
            }

            if (TelegraphMaxConnectionsPerNode < 1 || TelegraphMaxConnectionsPerNode > 10)
            {
                Logger.server.Warning($"TelegraphMaxConnectionsPerNode ({TelegraphMaxConnectionsPerNode}) should be between 1 and 10. Using default (4).");
                Config.TelegraphMaxConnectionsPerNode = 4;
            }

            if (TelegraphMaxMessageLength < 10 || TelegraphMaxMessageLength > 1000)
            {
                Logger.server.Warning($"TelegraphMaxMessageLength ({TelegraphMaxMessageLength}) should be between 10 and 1000. Using default (100).");
                Config.TelegraphMaxMessageLength = 100;
            }

            if (BellRingCooldownSeconds < 0.1 || BellRingCooldownSeconds > 10.0)
            {
                Logger.server.Warning($"BellRingCooldownSeconds ({BellRingCooldownSeconds}) should be between 0.1 and 10.0. Using default (1.5).");
                Config.BellRingCooldownSeconds = 1.5;
            }

            if (PrinterAutoSaveDelaySeconds < 1 || PrinterAutoSaveDelaySeconds > 300)
            {
                Logger.server.Warning($"PrinterAutoSaveDelaySeconds ({PrinterAutoSaveDelaySeconds}) should be between 1 and 300. Using default (10).");
                Config.PrinterAutoSaveDelaySeconds = 10;
            }

            // Sound emitting objects range validation
            if (HandbellAudibleDistance < 1 || HandbellAudibleDistance > 1000)
            {
                Logger.server.Warning($"HandbellAudibleDistance ({HandbellAudibleDistance}) should be between 1 and 1000. Using default (16).");
                Config.HandbellAudibleDistance = 16;
            }

            if (CallbellAudibleDistance < 1 || CallbellAudibleDistance > 1000)
            {
                Logger.server.Warning($"CallbellAudibleDistance ({CallbellAudibleDistance}) should be between 1 and 1000. Using default (16).");
                Config.CallbellAudibleDistance = 16;
            }

            if (RoyalhornAudibleDistance < 1 || RoyalhornAudibleDistance > 1000)
            {
                Logger.server.Warning($"RoyalhornAudibleDistance ({RoyalhornAudibleDistance}) should be between 1 and 1000. Using default (72).");
                Config.RoyalhornAudibleDistance = 72;
            }

            if (WarhornAudibleDistance < 1 || WarhornAudibleDistance > 1000)
            {
                Logger.server.Warning($"WarhornAudibleDistance ({WarhornAudibleDistance}) should be between 1 and 1000. Using default (128).");
                Config.WarhornAudibleDistance = 128;
            }

            if (CarillonbellAudibleDistance < 1 || CarillonbellAudibleDistance > 1000)
            {
                Logger.server.Warning($"CarillonbellAudibleDistance ({CarillonbellAudibleDistance}) should be between 1 and 1000. Using default (256).");
                Config.CarillonbellAudibleDistance = 256;
            }

            if (ChurchbellAudibleDistance < 1 || ChurchbellAudibleDistance > 1000)
            {
                Logger.server.Warning($"ChurchbellAudibleDistance ({ChurchbellAudibleDistance}) should be between 1 and 1000. Using default (832).");
                Config.ChurchbellAudibleDistance = 832;
            }

            if (MegaphoneAudibleDistance < 1 || MegaphoneAudibleDistance > 1000)
            {
                Logger.server.Warning($"MegaphoneAudibleDistance ({MegaphoneAudibleDistance}) should be between 1 and 1000. Using default (125).");
                Config.MegaphoneAudibleDistance = 125;
            }
        }
    }
}
