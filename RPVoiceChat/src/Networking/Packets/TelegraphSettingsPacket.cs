using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    public enum TelegraphSettingsOperation
    {
        SetCustomName = 0,
        SetTarget = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TelegraphSettingsPacket
    {
        public BlockPos TelegraphPos { get; set; }
        public TelegraphSettingsOperation Operation { get; set; }
        public string Value { get; set; }
    }
}
