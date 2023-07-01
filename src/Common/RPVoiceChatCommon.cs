using System.Net;
using Vintagestory.API.Common;

namespace rpvoicechat
{
    public class RPVoiceChatCommon : ModSystem
    {
        ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            this.api = api;

            // Register network channel
            api.Network.RegisterChannel("rpvoicechat")
                .RegisterMessageType(typeof(PlayerAudioPacket))
                .RegisterMessageType(typeof(ConnectionPacket));


            // Item registry (Not used yet)
            api.RegisterItemClass("ItemVoiceTransciever", typeof(ItemVoiceTransciever));

        }

        public string GetPublicIp()
        {
            return new WebClient().DownloadString("https://ipv4.icanhazip.com/").Replace("\n", "");
        }
    }
}
