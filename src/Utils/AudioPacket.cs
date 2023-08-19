using ProtoBuf;

namespace rpvoicechat
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AudioPacket
    {
        public string PlayerId { get; set; }
        public byte[] AudioData { get; set; }
        public int Length { get; set; }
        public VoiceLevel VoiceLevel { get; set; }
    }
}
