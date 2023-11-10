using Vintagestory.API.Common;

namespace RPVoiceChat.Networking
{
    public abstract class NativeNetworkBase
    {
        protected const string ChannelName = "RPAudioChannel";
        protected const string _transportID = "NativeTCP";

        public NativeNetworkBase(ICoreAPI api)
        {
            api.Network.RegisterChannel(ChannelName).RegisterMessageType<AudioPacket>();
        }

        public string GetTransportID()
        {
            return _transportID;
        }
    }
}
