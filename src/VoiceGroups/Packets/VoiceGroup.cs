using ProtoBuf;

namespace RPVoiceChat.VoiceGroups.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VoiceGroup
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public string[] Members { get; set; } = new string[0];
        public bool Disbanded { get; set; } = false;

        public VoiceGroup() { }

        public VoiceGroup(string name, string owner)
        {
            Name = name;
            Owner = owner;
            Members = new string[] { owner };
        }
    }
}