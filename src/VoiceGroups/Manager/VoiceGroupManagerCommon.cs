using RPVoiceChat.VoiceGroups.Packets;
using Vintagestory.API.Common;

namespace RPVoiceChat.VoiceGroups.Manager
{
    public class VoiceGroupManagerCommon
    {
        protected const string _rpvcGroupNetworkChannelName = "rpvc-group";

        public VoiceGroupManagerCommon(ICoreAPI api)
        {
            api.Network.RegisterChannel(_rpvcGroupNetworkChannelName)
                .RegisterMessageType<VoiceGroupRequest>()
                .RegisterMessageType<VoiceGroup>();
        }
    }
}