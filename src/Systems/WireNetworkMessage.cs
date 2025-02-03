using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.GameContent.Systems
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireNetworkMessage
    {
        public long NetworkUID;
        public string Message;
        public BlockPos SenderPos;
    }
}
