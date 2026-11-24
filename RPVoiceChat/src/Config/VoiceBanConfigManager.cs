using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace RPVoiceChat.Config
{
    public class VoiceBanConfigManager
    {
        private const string ConfigSubfolder = "RPVoiceChat";
        private const string BannedFileName = "rpvoicechat-banned.json";
        private string configDirectory;
        private string bannedFilePath;
        private ICoreServerAPI api;

        public VoiceBanConfigManager(ICoreServerAPI sapi)
        {
            api = sapi;
            InitializePaths();
            EnsureConfigDirectoryExists();
        }

        private void InitializePaths()
        {
            // Get ModConfig directory path
            string configPath = GamePaths.ModConfig;
            configDirectory = Path.Combine(configPath, ConfigSubfolder);
            bannedFilePath = Path.Combine(configDirectory, BannedFileName);
        }

        private void EnsureConfigDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                    api.Logger.Notification($"[RPVoiceChat] Created config directory: {configDirectory}");
                }
            }
            catch (Exception e)
            {
                api.Logger.Error($"[RPVoiceChat] Failed to create config directory: {e.Message}");
            }
        }

        public List<string> LoadBannedPlayers()
        {
            if (!File.Exists(bannedFilePath))
            {
                return new List<string>();
            }

            try
            {
                string jsonContent = File.ReadAllText(bannedFilePath);
                var bannedConfig = JsonConvert.DeserializeObject<BannedConfig>(jsonContent);
                return bannedConfig?.BannedPlayers ?? new List<string>();
            }
            catch (Exception e)
            {
                api.Logger.Warning($"[RPVoiceChat] Error loading banned players from {bannedFilePath}: {e.Message}");
                return new List<string>();
            }
        }

        public void SaveBannedPlayers(List<string> bannedPlayers)
        {
            try
            {
                var bannedConfig = new BannedConfig
                {
                    BannedPlayers = bannedPlayers ?? new List<string>()
                };

                string jsonContent = JsonConvert.SerializeObject(bannedConfig, Formatting.Indented);
                File.WriteAllText(bannedFilePath, jsonContent);
            }
            catch (Exception e)
            {
                api.Logger.Error($"[RPVoiceChat] Error saving banned players to {bannedFilePath}: {e.Message}");
            }
        }

        private class BannedConfig
        {
            public List<string> BannedPlayers { get; set; } = new List<string>();
        }
    }
}

