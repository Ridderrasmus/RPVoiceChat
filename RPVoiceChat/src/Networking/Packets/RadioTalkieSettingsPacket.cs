using ProtoBuf;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RadioTalkieSettingsPacket
    {
        public string Frequency { get; set; }
        public bool InventoryListen { get; set; }
        public int SlotNumber { get; set; }
        public int ListenVolumePercent { get; set; }
    }
}
