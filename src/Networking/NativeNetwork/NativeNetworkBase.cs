using Vintagestory.API.Common;

namespace RPVoiceChat.Networking
{
    public abstract class NativeNetworkBase
    {
        protected const string ChannelName = "RPAudioChannel";

        public NativeNetworkBase(ICoreAPI api)
        {
            api.Network.RegisterChannel(ChannelName).RegisterMessageType<AudioPacket>();
        }
    }
}
