using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SwitchboardRenameNetworkPacket
    {
        public BlockPos SwitchboardPos { get; set; }
        public string NetworkName { get; set; }
    }
}
