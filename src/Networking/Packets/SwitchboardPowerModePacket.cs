using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SwitchboardPowerModePacket
    {
        public BlockPos SwitchboardPos { get; set; }
        public bool UsePowerRequirements { get; set; }
    }
}
