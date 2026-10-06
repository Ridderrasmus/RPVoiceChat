using System.Collections.Generic;
using ProtoBuf;

namespace RPVoiceChat.Networking
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VoiceGroupStateEntry
    {
        public string Name { get; set; }
        public string OwnerPlayerUid { get; set; }
        public bool InviteOnly { get; set; }
        public List<string> Members { get; set; } = new List<string>();

        public VoiceGroupStateEntry()
        {
        }

        public VoiceGroupStateEntry(string name, string ownerPlayerUid, bool inviteOnly, List<string> members)
        {
            Name = name;
            OwnerPlayerUid = ownerPlayerUid;
            InviteOnly = inviteOnly;
            Members = members ?? new List<string>();
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VoiceGroupStatePacket : NetworkPacket
    {
        public List<VoiceGroupStateEntry> Groups { get; set; } = new List<VoiceGroupStateEntry>();
        protected override PacketType Code => PacketType.VoiceGroupState;

        public VoiceGroupStatePacket()
        {
        }

        public VoiceGroupStatePacket(List<VoiceGroupStateEntry> groups)
        {
            Groups = groups ?? new List<VoiceGroupStateEntry>();
        }
    }
}
