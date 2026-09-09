using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    public enum TelephoneSettingsOperation
    {
        SetNumber = 0,
        SetTarget = 1,
        StartCall = 2,
        EndCall = 3
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class TelephoneSettingsPacket
    {
        public BlockPos TelephonePos { get; set; }
        public TelephoneSettingsOperation Operation { get; set; }
        public string Value { get; set; }
    }
}
