using RPVoiceChat.Networking;
using RPVoiceChat.Server;
using RPVoiceChat.Utils;
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

            var mainServer = new UDPNetworkServer(config.ServerPort, config.ServerIP);
            if (config.ManualPortForwarding) mainServer.TogglePortForwarding(false);
            var backupServer = new NativeNetworkServer(api);
            server = new GameServer(sapi, mainServer, backupServer);
            server.Launch();

            // Register/load world config
            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", sapi.World.Config.GetInt("rpvoicechat:distance-whisper", (int)VoiceLevel.Whispering));
            sapi.World.Config.SetInt("rpvoicechat:distance-talk", sapi.World.Config.GetInt("rpvoicechat:distance-talk", (int)VoiceLevel.Talking));
            sapi.World.Config.SetInt("rpvoicechat:distance-shout", sapi.World.Config.GetInt("rpvoicechat:distance-shout", (int)VoiceLevel.Shouting));

            // Register commands
            registerCommands();
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            api.World.Config.SetBool("rpvoicechat:extra-content", config.AdditionalContent);
        }

        public override double ExecuteOrder() => 1.02;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (config.AdditionalContent) return;

            RecipeHandler recipeHandler = new RecipeHandler(api, modID);
            recipeHandler.DisableRecipes();
        }

        private void registerCommands()
        {
            var parsers = sapi.ChatCommands.Parsers;

            sapi.ChatCommands
                .GetOrCreate("rpvc")
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
                    .WithDesc(UIUtils.I18n($"Command.Info.Desc"))
                    .HandleWith(DisplayInfoHandler)
                .EndSub()
                .BeginSub("reset")
                    .WithDesc(UIUtils.I18n("Command.Reset.Desc"))
                    .HandleWith(ResetDistanceHandler)
                .EndSub()
                .BeginSub("voiptoggle")
                    .WithDesc("Toggles voip functionality on the server")
                    .WithArgs(parsers.Bool("toggle"))
                    .HandleWith(ToggleVoip)
                .EndSub();
        }

        private TextCommandResult ToggleVoip(TextCommandCallingArgs args)
        {
            bool toggle = (bool)args[0];

            if (server.ServerToggle(toggle))
                return TextCommandResult.Success("Voip toggled " + (toggle ? "on" : "off"));
            else
                return TextCommandResult.Error("Voip is already " + (toggle ? "on" : "off") + "!");
        }
        private TextCommandResult ResetDistanceHandler(TextCommandCallingArgs args)
        {
            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", (int)VoiceLevel.Whispering);
            sapi.World.Config.SetInt("rpvoicechat:distance-talk", (int)VoiceLevel.Talking);
            sapi.World.Config.SetInt("rpvoicechat:distance-shout", (int)VoiceLevel.Shouting);

            return TextCommandResult.Success(UIUtils.I18n("Command.Reset.Success"));
        }

        private TextCommandResult DisplayInfoHandler(TextCommandCallingArgs args)
        {
            int whisper = sapi.World.Config.GetInt("rpvoicechat:distance-whisper", (int)VoiceLevel.Whispering);
            int talk = sapi.World.Config.GetInt("rpvoicechat:distance-talk", (int)VoiceLevel.Talking);
            int shout = sapi.World.Config.GetInt("rpvoicechat:distance-shout", (int)VoiceLevel.Shouting);

            return TextCommandResult.Success(UIUtils.I18n("Command.Info.Success", whisper, talk, shout));
        }

        private TextCommandResult SetWhisperHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", distance);

            return TextCommandResult.Success(UIUtils.I18n("Command.Whisper.Success", distance));
        }

        private TextCommandResult SetTalkHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            sapi.World.Config.SetInt("rpvoicechat:distance-talk", distance);

            return TextCommandResult.Success(UIUtils.I18n("Command.Talk.Success", distance));
        }

        private TextCommandResult SetShoutHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            sapi.World.Config.SetInt("rpvoicechat:distance-shout", distance);

            return TextCommandResult.Success(UIUtils.I18n($"Command.Shout.Success", distance));
        }

        public override void Dispose()
        {
            server?.Dispose();
        }
    }
}
