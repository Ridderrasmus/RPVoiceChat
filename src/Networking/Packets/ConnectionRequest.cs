using ProtoBuf;
using System.Collections.Generic;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ConnectionRequest : NetworkPacket
    {
        public ConnectionInfo[] SupportedTransports { get; }
        [ProtoMember(120)] public int VoiceTimingVersion { get; set; }
        protected override PacketType Code { get => PacketType.ConnectionRequest; }

        public ConnectionRequest() { }

        public ConnectionRequest(List<ConnectionInfo> serverConnectionInfos)
        {
            SupportedTransports = serverConnectionInfos.ToArray();
        }
    }
}
