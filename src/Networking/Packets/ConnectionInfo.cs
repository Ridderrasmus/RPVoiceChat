using ProtoBuf;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionInfo : NetworkPacket
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string Transport { get; set; }
        protected override PacketType Code { get => PacketType.ConnectionInfo; }

        public ConnectionInfo() { }

        public ConnectionInfo(int port, string address = null)
        {
            Port = port;
            Address = address;
        }
    }
}
