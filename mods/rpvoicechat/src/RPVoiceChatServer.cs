using System.Net.Sockets;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Server;

[assembly: ModInfo( "rpvoicechat",
Description = "",
Website     = "",
Authors     = new []{ "Ridderrasmus", "Purplep_", "blakdragan7" } )]

namespace rpvoicechat.src
{
    public class RPVoiceChatServer : RPVoiceChatMod
    {
        protected ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(sapi);

            server = new RPVoiceChatSocketServer(sapi, config.ServerPort);
            var ip = server.GetPublicIPAddress();
            
            // Register/load world config
            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", sapi.World.Config.GetInt("rpvoicechat:distance-whisper", 5));
            sapi.World.Config.SetInt("rpvoicechat:distance-talk", sapi.World.Config.GetInt("rpvoicechat:distance-talk", 15));
            sapi.World.Config.SetInt("rpvoicechat:distance-shout", sapi.World.Config.GetInt("rpvoicechat:distance-shout", 25));

            // Register commands
            registerCommands();

            // Register events
            sapi.Event.PlayerNowPlaying += OnPlayerPlaying;
        }

        private void registerCommands()
        {
            var parsers = sapi.ChatCommands.Parsers;



            sapi.ChatCommands
                .GetOrCreate("rpvc")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSub("shout")
                    .WithDesc("Sets the shout distance in blocks")
                    .WithArgs(parsers.Int("distance"))
                    .HandleWith(SetShoutHandler)
                .EndSub()
                .BeginSub("talk")
                    .WithDesc("Sets the talk distance in blocks")
                    .WithArgs(parsers.Int("distance"))
                    .HandleWith(SetTalkHandler)
                .EndSub()
                .BeginSub("whisper")
                    .WithDesc("Sets the whisper distance in blocks")
                    .WithArgs(parsers.Int("distance"))
                    .HandleWith(SetWhisperHandler)
                .EndSub()
                .BeginSub("info")
                    .WithDesc("Displays the current audio distances")
                    .HandleWith(DisplayInfoHandler)
                .EndSub()
                .BeginSub("reset")
                    .WithDesc("Resets the audio distances to their default settings")
                    .HandleWith(ResetDistanceHandler)
                .EndSub();
        }

        private TextCommandResult ResetDistanceHandler(TextCommandCallingArgs args)
        {
            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", 5);
            sapi.World.Config.SetInt("rpvoicechat:distance-talk", 15);
            sapi.World.Config.SetInt("rpvoicechat:distance-shout", 25);

            return TextCommandResult.Success("Audio distances reset to default");
        }
        
        private TextCommandResult DisplayInfoHandler(TextCommandCallingArgs args)
        {
            int whisper = sapi.World.Config.GetInt("rpvoicechat:distance-whisper", 5);
            int talk = sapi.World.Config.GetInt("rpvoicechat:distance-talk", 15);
            int shout = sapi.World.Config.GetInt("rpvoicechat:distance-shout", 25);

            return TextCommandResult.Success
                (
                    "Whisper distance: " + whisper + "\n" +
                    "Talk distance: " + talk + "\n" +
                    "Shout distance: " + shout
                );
        }

        private TextCommandResult SetWhisperHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            sapi.World.Config.SetInt("rpvoicechat:distance-whisper", distance);

            return TextCommandResult.Success("Whisper distance set to " + distance);
        }

        private TextCommandResult SetTalkHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            sapi.World.Config.SetInt("rpvoicechat:distance-talk", distance);

            return TextCommandResult.Success("Talking distance set to " + distance);
        }

        private TextCommandResult SetShoutHandler(TextCommandCallingArgs args)
        {
            int distance = (int)args[0];

            sapi.World.Config.SetInt("rpvoicechat:distance-shout", distance);

            return TextCommandResult.Success("Shout distance set to " + distance);
        }

        private void OnPlayerPlaying(IServerPlayer byPlayer)
        {
            sapi.Logger.Debug($"[RPVoiceChat - Server] Player start {byPlayer.PlayerName}");
            string address = server.GetPublicIPAddress();
            int port = server.GetPort();
            sapi.Network.GetChannel("rpvoicechat").SendPacket(new ConnectionInfo()
            {
                Address = address,
                Port = port
            }, byPlayer);
        }

        public override void Dispose()
        {
            base.Dispose();

            server?.Dispose();
            server = null;
        }
    }
}
