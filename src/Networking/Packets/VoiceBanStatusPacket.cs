using ProtoBuf;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VoiceBanStatusPacket : NetworkPacket
    {
        public string PlayerId { get; set; }
        public bool IsBanned { get; set; }
        protected override PacketType Code { get => PacketType.VoiceBanStatus; }

        public VoiceBanStatusPacket() { }

        public VoiceBanStatusPacket(string playerId, bool isBanned)
        {
            PlayerId = playerId;
            IsBanned = isBanned;
        }
    }
}

