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
        /// <summary>UID of the player who sent. Lets other clients play animation/sound on the same block.</summary>
        public string SenderPlayerUID;
    }
}
