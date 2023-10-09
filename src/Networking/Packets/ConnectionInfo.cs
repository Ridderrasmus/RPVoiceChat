using ProtoBuf;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionInfo : NetworkPacket
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string[] SupportedTransports { get; set; }
        protected override PacketType Code { get => PacketType.ConnectionInfo; }
    }
}
