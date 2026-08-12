using ProtoBuf;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioClientNotificationPacket
    {
        public string LangKey { get; set; }
    }
}
