using ProtoBuf;
using System.Collections.Generic;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionRequest : NetworkPacket
    {
        public ConnectionInfo[] SupportedTransports { get; }
        protected override PacketType Code { get => PacketType.ConnectionRequest; }

        public ConnectionRequest() { }

        public ConnectionRequest(List<ConnectionInfo> serverConnectionInfos)
        {
            SupportedTransports = serverConnectionInfos.ToArray();
        }
    }
}
