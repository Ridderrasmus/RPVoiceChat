using ProtoBuf;
using Vintagestory.API.MathTools;

namespace RPVoiceChat.Networking.Packets
{
    public enum CommPayloadType
    {
        Text = 0,
        Voice = 1
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class CommDeliveryPacket
    {
        public BlockPos DevicePos { get; set; }
        public string SourceEndpointName { get; set; }
        public string TargetEndpointName { get; set; }
        public string NetworkName { get; set; }
        public string TextMessage { get; set; }
        public byte[] VoicePayload { get; set; }
        public CommPayloadType PayloadType { get; set; } = CommPayloadType.Text;
    }
}
