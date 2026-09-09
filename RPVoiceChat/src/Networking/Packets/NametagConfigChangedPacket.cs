using ProtoBuf;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NametagConfigChangedPacket : NetworkPacket
    {
        public bool ForceSpeakerNametag { get; set; }
        public bool PlayerNametagTargetedOnly { get; set; }
        public bool UseNametagDynamicRange { get; set; }

        protected override PacketType Code { get => PacketType.NametagConfigChanged; }

        public NametagConfigChangedPacket() { }

        public NametagConfigChangedPacket(bool forceSpeakerNametag, bool playerNametagTargetedOnly, bool useNametagDynamicRange)
        {
            ForceSpeakerNametag = forceSpeakerNametag;
            PlayerNametagTargetedOnly = playerNametagTargetedOnly;
            UseNametagDynamicRange = useNametagDynamicRange;
        }
    }
}
