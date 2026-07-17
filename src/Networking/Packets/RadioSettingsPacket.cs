using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    public enum RadioSettingsOperation
    {
        SetFrequency = 0,
        SetDisplayName = 1,
        SetEmitterMode = 2,
        SetReceiverFrequency = 3,
        SetRepeaterFrequency = 4,
        SetMicrophoneTransmit = 5,
        SetMixingConsoleHlsUrl = 6,
        SetMixingConsoleOnAir = 7
    }

    public enum RadioEmitterOperatingMode
    {
        WiredSource = 0,
        Repeater = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioSettingsPacket
    {
        public BlockPos BlockPos { get; set; }
        public RadioSettingsOperation Operation { get; set; }
        public string Value { get; set; }
        public int IntValue { get; set; }
    }
}
