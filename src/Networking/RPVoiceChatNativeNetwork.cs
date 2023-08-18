
using Vintagestory.API.Common;

namespace rpvoicechat.Networking
{
    public class RPVoiceChatNativeNetwork
    {
        private ICoreAPI api;
        protected const string ChannelName = "RPAudioChannel";
        public RPVoiceChatNativeNetwork(ICoreAPI api)
        {
            api.Network.RegisterChannel(ChannelName).RegisterMessageType<AudioPacket>();
        }
    }
}
