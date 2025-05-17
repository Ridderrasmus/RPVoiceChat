using RPVoiceChat.Networking;
using RPVoiceChat.Server;
using RPVoiceChat.Systems;
using RPVoiceChat.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Server;

namespace RPVoiceChat
{
    public class RPVoiceChatServer : RPVoiceChatMod
    {
        protected ICoreServerAPI sapi;
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

            bool forwardPorts = !config.ManualPortForwarding;
            var networkTransports = new List<INetworkServer>()
            {
                new NativeNetworkServer(api)
            };
            if (config.UseCustomNetworkServers)
            {
                networkTransports.Insert(0, new UDPNetworkServer(config.ServerPort, config.ServerIP, forwardPorts));
                networkTransports.Insert(1, new TCPNetworkServer(config.ServerPort, config.ServerIP, forwardPorts));
            }

            server = new GameServer(sapi, networkTransports);
            server.Launch();
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            WorldConfig.Set("additional-content", config.AdditionalContent);
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
                .BeginSub("wtw")
                    .WithDesc(UIUtils.I18n("Command.WallThicknessWeighting.Desc"))
                    .WithAdditionalInformation(UIUtils.I18n("Command.WallThicknessWeighting.Help"))
                    .WithArgs(parsers.Float("weighting"))
                    .HandleWith(SetWallThicknessWeighting)
                .EndSub();
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
            float wallThicknessWeighting = WorldConfig.GetFloat("wall-thickness-weighting");

            return TextCommandResult.Success(UIUtils.I18n("Command.Info.Success", whisper, talk, shout, forceNameTags, encoding, wallThicknessWeighting));
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

        private TextCommandResult SetWallThicknessWeighting(TextCommandCallingArgs args)
        {
            float weighting = (float)args[0];

            WorldConfig.Set("wall-thickness-weighting", weighting);

            return TextCommandResult.Success(UIUtils.I18n("Command.WallThicknessWeighting.Success", weighting));
        }

        public override void Dispose()
        {
            server?.Dispose();
        }
    }
}
