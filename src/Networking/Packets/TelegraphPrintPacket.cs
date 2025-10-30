using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TelegraphPrintPacket
    {
        public string Message { get; set; }
        public BlockPos TelegraphPos { get; set; }
    }
}
