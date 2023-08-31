using RPVoiceChat;
using System;
using System.Net;
using Vintagestory.API.Common;

namespace rpvoicechat.Networking
{
    public class RPVoiceChatNativeNetwork : INetworkCommon
    {
        public event Action<byte[]> OnMessageReceived;
        
        protected const string ChannelName = "RPAudioChannel";
        
        public RPVoiceChatNativeNetwork(ICoreAPI api)
        {
            api.Network.RegisterChannel(ChannelName).RegisterMessageType<AudioPacket>();
        }


        public IPEndPoint GetPublicIP()
        {
            throw new NotImplementedException();
        }
    }
}
