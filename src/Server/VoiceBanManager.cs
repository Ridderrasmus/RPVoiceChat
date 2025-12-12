using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Config;
using RPVoiceChat.Util;
using Vintagestory.API.Server;

namespace RPVoiceChat.Server
{
    public class VoiceBanManager
    {
        private ICoreServerAPI api;
        private HashSet<string> bannedPlayers = new HashSet<string>();
        private VoiceBanConfigManager banConfigManager;

        public VoiceBanManager(ICoreServerAPI sapi)
        {
            api = sapi;
            banConfigManager = new VoiceBanConfigManager(sapi);
            LoadBannedPlayers();
        }

        public bool IsPlayerBanned(string playerUID)
        {
            return bannedPlayers.Contains(playerUID);
        }

        public bool BanPlayer(string playerUID)
        {
            if (bannedPlayers.Add(playerUID))
            {
                SaveBannedPlayers();
                return true;
            }
            return false;
        }

        public bool UnbanPlayer(string playerUID)
        {
            if (bannedPlayers.Remove(playerUID))
            {
                SaveBannedPlayers();
                return true;
            }
            return false;
        }

        public List<string> GetBannedPlayers()
        {
            return bannedPlayers.ToList();
        }

        public string GetPlayerName(string playerUID)
        {
            var player = api.World.PlayerByUid(playerUID);
            if (player != null)
                return player.PlayerName;
            
            // Try to get from offline players if available
            var offlinePlayer = api.World.AllPlayers.FirstOrDefault(p => p.PlayerUID == playerUID);
            return offlinePlayer?.PlayerName ?? playerUID;
        }

        private void LoadBannedPlayers()
        {
            try
            {
                var bannedList = banConfigManager.LoadBannedPlayers();
                bannedPlayers = new HashSet<string>(bannedList);
            }
            catch (Exception e)
            {
                Logger.server.Warning($"Error loading voice ban blacklist: {e.Message}");
                bannedPlayers = new HashSet<string>();
            }
        }

        private void SaveBannedPlayers()
        {
            try
            {
                var bannedList = bannedPlayers.ToList();
                banConfigManager.SaveBannedPlayers(bannedList);
            }
            catch (Exception e)
            {
                Logger.server.Error($"Error saving voice ban blacklist: {e.Message}");
            }
        }
    }
}

