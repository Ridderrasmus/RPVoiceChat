using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.src.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WeldingHitPacket
    {
        public BlockPos Pos;
        public Vec3d HitPosition;
    }
}
