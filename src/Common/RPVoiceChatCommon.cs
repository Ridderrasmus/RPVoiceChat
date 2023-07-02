using System.Net;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public class RPVoiceChatCommon : ModSystem
    {
        ICoreAPI api;
        public RPModSettings Config = new RPModSettings();

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            this.api = api;

            LoadConfig();

            // Register network channel
            api.Network.RegisterChannel("rpvoicechat")
                .RegisterMessageType(typeof(PlayerAudioPacket))
                .RegisterMessageType(typeof(ConnectionPacket));


            // Item registry (Not used yet)
            api.RegisterItemClass("ItemVoiceTransciever", typeof(ItemVoiceTransciever));

        }

        private void LoadConfig()
        {
            if (api.LoadModConfig("rpvoicechat.json") == null)
            {
                SaveConfig();
                return;
            }

            Config = api.LoadModConfig<RPModSettings>("rpvoicechat.json");
            SaveConfig();
        }

        private void SaveConfig() => api.StoreModConfig(Config, "rpvoicechat.json");

        private static void SetConfigDefaults()
        {
            
            if(RPModSettings.serverPort == 0) RPModSettings.serverPort = 52525;
            if(RPModSettings.InputThreshold == 0) RPModSettings.InputThreshold = 20;
        }

        public string GetPublicIp()
        {
            return new WebClient().DownloadString("https://ipv4.icanhazip.com/").Replace("\n", "");
        }
    }
}
