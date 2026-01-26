using System;
using System.Collections.Generic;
using System.Linq;
using RPVoiceChat.Config;
using RPVoiceChat.Networking;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Server;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Server;

namespace RPVoiceChat
{
    public class RPVoiceChatServer : RPVoiceChatMod
    {
        private GameServer server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            // Register/load world config
            WorldConfig.Set(VoiceLevel.Whispering, WorldConfig.GetInt(VoiceLevel.Whispering));
            WorldConfig.Set(VoiceLevel.Talking, WorldConfig.GetInt(VoiceLevel.Talking));
            WorldConfig.Set(VoiceLevel.Shouting, WorldConfig.GetInt(VoiceLevel.Shouting));
            WorldConfig.Set("force-render-name-tags", WorldConfig.GetBool("force-render-name-tags", true));
            WorldConfig.Set("encode-audio", WorldConfig.GetBool("encode-audio", true));
            WorldConfig.Set("others-hear-spectators", WorldConfig.GetBool("others-hear-spectators", true));
            WorldConfig.Set("wall-thickness-weighting", WorldConfig.GetFloat("wall-thickness-weighting", 2));

            // Register commands
            registerCommands();


            WireNetworkHandler.RegisterServerside(api);

            bool forwardPorts = !ModConfig.ServerConfig.ManualPortForwarding;
            var networkTransports = new List<INetworkServer>()
            {
                new NativeNetworkServer(api)
            };
            if (ModConfig.ServerConfig.UseCustomNetworkServers)
            {
                networkTransports.Insert(0, new UDPNetworkServer(ModConfig.ServerConfig.ServerPort, ModConfig.ServerConfig.ServerIP, forwardPorts));
                networkTransports.Insert(1, new TCPNetworkServer(ModConfig.ServerConfig.ServerPort, ModConfig.ServerConfig.ServerIP, forwardPorts));
            }

            server = new GameServer(sapi, networkTransports);
            server.Launch();
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            WorldConfig.Set("additional-content", ModConfig.ServerConfig.AdditionalContent);
            WorldConfig.Set("telegraph-content", ModConfig.ServerConfig.TelegraphContent);
        }

        public override double ExecuteOrder() => 1.02;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        private void registerCommands()
        {
            var parsers = sapi.ChatCommands.Parsers;

            sapi.ChatCommands
                .GetOrCreate("rpvc")
                .WithAlias("rpvoice", "rpvoicechat")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSub("shout")
                    .WithDesc(UIUtils.I18n("Command.Shout.Desc"))
                    .WithArgs(parsers.Int("distance"))
                    .HandleWith(SetShoutHandler)
                .EndSub()
                .BeginSub("talk")
                    .WithDesc(UIUtils.I18n("Command.Talk.Desc"))
                    .WithArgs(parsers.Int("distance"))
                    .HandleWith(SetTalkHandler)
                .EndSub()
                .BeginSub("whisper")
                    .WithDesc(UIUtils.I18n("Command.Whisper.Desc"))
                    .WithArgs(parsers.Int("distance"))
                    .HandleWith(SetWhisperHandler)
                .EndSub()
                .BeginSub("info")
                    .WithDesc(UIUtils.I18n("Command.Info.Desc"))
                    .HandleWith(DisplayInfoHandler)
                .EndSub()
                .BeginSub("reset")
                    .WithDesc(UIUtils.I18n("Command.Reset.Desc"))
                    .HandleWith(ResetDistanceHandler)
                .EndSub()
                .BeginSub("forcenametags")
                    .WithDesc(UIUtils.I18n("Command.ForceNameTags.Desc"))
                    .WithAdditionalInformation(UIUtils.I18n("Command.ForceNameTags.Help"))
                    .WithArgs(parsers.Bool("state"))
                    .HandleWith(ToggleForceNameTags)
                .EndSub()
                .BeginSub("encodeaudio")
                    .WithDesc(UIUtils.I18n("Command.EncodeAudio.Desc"))
                    .WithAdditionalInformation(UIUtils.I18n("Command.EncodeAudio.Help"))
                    .WithArgs(parsers.Bool("state"))
                    .HandleWith(ToggleAudioEncoding)
                .EndSub()
                .BeginSub("hearspectators")
                    .WithDesc(UIUtils.I18n("Command.OthersHearSpectators.Desc"))
                    .WithAdditionalInformation(UIUtils.I18n("Command.OthersHearSpectators.Help"))
                    .WithArgs(parsers.Bool("state"))
                    .HandleWith(ToggleOthersHearSpectators)
                .EndSub()
                .BeginSub("voiceban")
                    .WithDesc(UIUtils.I18n("Command.VoiceBan.Desc"))
                    .WithArgs(parsers.Word("player"))
                    .HandleWith(VoiceBanHandler)
                .EndSub()
                .BeginSub("voiceunban")
                    .WithDesc(UIUtils.I18n("Command.VoiceUnban.Desc"))
                    .WithArgs(parsers.Word("player"))
                    .HandleWith(VoiceUnbanHandler)
                .EndSub()
                .BeginSub("voicebanlist")
                    .WithDesc(UIUtils.I18n("Command.VoiceBanList.Desc"))
                    .HandleWith(VoiceBanListHandler)
                .EndSub();
            
            // Announce command - format: /announce Title | Message | Duration | glass (optional)
            sapi.ChatCommands
                .GetOrCreate("announce")
                .RequiresPrivilege(Privilege.controlserver)
                .WithDesc(UIUtils.I18n("Command.Announce.Desc"))
                .WithArgs(parsers.All("title | message | duration | glass"))
                .HandleWith(AnnounceHandler);
        }

        private TextCommandResult ToggleOthersHearSpectators(TextCommandCallingArgs args)
        {
            const string i18nPrefix = "Command.OthersHearSpectators.Success";
            bool state = (bool)args[0];

            WorldConfig.Set("others-hear-spectators", state);
            string stateAsText = state ? "Enabled" : "Disabled";

            return TextCommandResult.Success(UIUtils.I18n($"{i18nPrefix}.{stateAsText}"));
        }

        private TextCommandResult ToggleAudioEncoding(TextCommandCallingArgs args)
        {
            const string i18nPrefix = "Command.EncodeAudio.Success";
            bool state = (bool)args[0];

            WorldConfig.Set("encode-audio", state);

            string stateAsText = state ? "Enabled" : "Disabled";
            return TextCommandResult.Success(UIUtils.I18n($"{i18nPrefix}.{stateAsText}"));
        }

        private TextCommandResult ToggleForceNameTags(TextCommandCallingArgs args)
        {
            const string i18nPrefix = "Command.ForceNameTags.Success";
            bool state = (bool)args[0];

            WorldConfig.Set("force-render-name-tags", state);

            string stateAsText = state ? "Enabled" : "Disabled";
            return TextCommandResult.Success(UIUtils.I18n($"{i18nPrefix}.{stateAsText}"));
        }

        private TextCommandResult ResetDistanceHandler(TextCommandCallingArgs args)
        {
            WorldConfig.Set(VoiceLevel.Whispering, (int)VoiceLevel.Whispering);
            WorldConfig.Set(VoiceLevel.Talking, (int)VoiceLevel.Talking);
            WorldConfig.Set(VoiceLevel.Shouting, (int)VoiceLevel.Shouting);

            return TextCommandResult.Success(UIUtils.I18n("Command.Reset.Success"));
        }

        private TextCommandResult DisplayInfoHandler(TextCommandCallingArgs args)
        {
            int whisper = WorldConfig.GetInt(VoiceLevel.Whispering);
            int talk = WorldConfig.GetInt(VoiceLevel.Talking);
            int shout = WorldConfig.GetInt(VoiceLevel.Shouting);
            bool forceNameTags = WorldConfig.GetBool("force-render-name-tags");
            bool encoding = WorldConfig.GetBool("encode-audio");

            return TextCommandResult.Success(UIUtils.I18n("Command.Info.Success", whisper, talk, shout, forceNameTags, encoding));
        }

        private TextCommandResult SetWhisperHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            WorldConfig.Set(VoiceLevel.Whispering, distance);

            return TextCommandResult.Success(UIUtils.I18n("Command.Whisper.Success", distance));
        }

        private TextCommandResult SetTalkHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            WorldConfig.Set(VoiceLevel.Talking, distance);

            return TextCommandResult.Success(UIUtils.I18n("Command.Talk.Success", distance));
        }

        private TextCommandResult SetShoutHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            WorldConfig.Set(VoiceLevel.Shouting, distance);

            return TextCommandResult.Success(UIUtils.I18n($"Command.Shout.Success", distance));
        }

        private TextCommandResult VoiceBanHandler(TextCommandCallingArgs args)
        {
            string playerIdentifier = (string)args[0];
            IPlayer targetPlayer = FindPlayer(playerIdentifier);
            
            if (targetPlayer == null)
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.VoiceBan.PlayerNotFound"));
            }

            var banManager = server.GetVoiceBanManager();
            if (banManager.IsPlayerBanned(targetPlayer.PlayerUID))
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.VoiceBan.AlreadyBanned", targetPlayer.PlayerName));
            }

            banManager.BanPlayer(targetPlayer.PlayerUID);
            server.NotifyAllPlayersBanStatus(targetPlayer.PlayerUID, true);

            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceBan.Success", targetPlayer.PlayerName));
        }

        private TextCommandResult VoiceUnbanHandler(TextCommandCallingArgs args)
        {
            string playerIdentifier = (string)args[0];
            IPlayer targetPlayer = FindPlayer(playerIdentifier);
            
            if (targetPlayer == null)
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.VoiceUnban.PlayerNotFound"));
            }

            var banManager = server.GetVoiceBanManager();
            if (!banManager.IsPlayerBanned(targetPlayer.PlayerUID))
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.VoiceUnban.NotBanned", targetPlayer.PlayerName));
            }

            banManager.UnbanPlayer(targetPlayer.PlayerUID);
            server.NotifyAllPlayersBanStatus(targetPlayer.PlayerUID, false);

            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceUnban.Success", targetPlayer.PlayerName));
        }

        private IPlayer FindPlayer(string identifier)
        {
            // Try to find by UID first
            IPlayer player = sapi.World.PlayerByUid(identifier);
            if (player != null) return player;

            // Try to find by name (case-insensitive) in online players first
            player = sapi.World.AllOnlinePlayers.FirstOrDefault(p => 
                p.PlayerName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
            if (player != null) return player;

            // Try to find in all players (including offline) by name
            player = sapi.World.AllPlayers.FirstOrDefault(p => 
                p.PlayerName.Equals(identifier, StringComparison.OrdinalIgnoreCase));
            
            return player;
        }

        private TextCommandResult VoiceBanListHandler(TextCommandCallingArgs args)
        {
            var banManager = server.GetVoiceBanManager();
            var bannedPlayers = banManager.GetBannedPlayers();

            if (bannedPlayers.Count == 0)
            {
                return TextCommandResult.Success(UIUtils.I18n("Command.VoiceBanList.Empty"));
            }

            var playerNames = new List<string>();
            foreach (var playerUID in bannedPlayers)
            {
                string playerName = banManager.GetPlayerName(playerUID);
                playerNames.Add($"{playerName} ({playerUID})");
            }

            return TextCommandResult.Success(UIUtils.I18n("Command.VoiceBanList.Success", string.Join(", ", playerNames)));
        }

        private TextCommandResult AnnounceHandler(TextCommandCallingArgs args)
        {
            string fullText = (string)args[0];

            if (string.IsNullOrWhiteSpace(fullText))
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.Announce.Usage"));
            }

            // Split by | separator
            string[] parts = fullText.Split('|');
            if (parts.Length < 2)
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.Announce.MissingSeparator"));
            }

            string title = parts[0].Trim();
            string message = parts[1].Trim();
            double duration = 5.0; // Default duration
            bool showBackground = true;

            // Parse optional duration
            if (parts.Length >= 3)
            {
                string durationStr = parts[2].Trim();
                if (!string.IsNullOrWhiteSpace(durationStr))
                {
                    if (!double.TryParse(durationStr, out duration) || duration <= 0)
                    {
                        return TextCommandResult.Error(UIUtils.I18n("Command.Announce.InvalidDuration", durationStr));
                    }
                }
            }

            // Parse optional glass flag (semi-transparent background)
            if (parts.Length >= 4)
            {
                string bgFlag = parts[3].Trim().ToLowerInvariant();
                if (bgFlag == "glass" || bgFlag == "transparent" || bgFlag == "1")
                {
                    showBackground = false;
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.Announce.EmptyTitle"));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return TextCommandResult.Error(UIUtils.I18n("Command.Announce.EmptyMessage"));
            }

            // Send announcement to all connected players
            var packet = new AnnouncePacket(title, message, duration, showBackground);
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState == EnumClientState.Playing)
                {
                    RPVoiceChatMod.AnnounceServerChannel.SendPacket(packet, player);
                }
            }

            int playerCount = sapi.World.AllOnlinePlayers.Count();
            if (showBackground)
            {
                return TextCommandResult.Success(UIUtils.I18n("Command.Announce.Success", playerCount, duration));
            }
            else
            {
                return TextCommandResult.Success(UIUtils.I18n("Command.Announce.SuccessGlass", playerCount, duration));
            }
        }

        public override void Dispose()
        {
            server?.Dispose();
        }

    }
}
