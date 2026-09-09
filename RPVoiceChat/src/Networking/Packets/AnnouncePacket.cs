using ProtoBuf;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class AnnouncePacket : NetworkPacket
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public double Duration { get; set; }
        public bool ShowBackground { get; set; }
        protected override PacketType Code { get => PacketType.Announce; }

        public AnnouncePacket() { }

        public AnnouncePacket(string title, string message, double duration = 5.0, bool showBackground = true)
        {
            Title = title;
            Message = message;
            Duration = duration;
            ShowBackground = showBackground;
        }
    }
}
