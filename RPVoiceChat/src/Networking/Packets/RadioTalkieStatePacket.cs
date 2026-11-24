using ProtoBuf;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioTalkieStatePacket
    {
        public bool Transmitting { get; set; }
        public string Frequency { get; set; }
    }
}
