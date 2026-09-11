using System.Collections.Generic;

namespace RPVoiceChat.Server
{
    public class VoiceGroup
    {
        public string Name { get; set; }
        public string OwnerPlayerUid { get; set; }
        public bool InviteOnly { get; set; }
        public HashSet<string> Members { get; set; } = new HashSet<string>();
        public HashSet<string> PendingInvites { get; set; } = new HashSet<string>();
        public Dictionary<string, long> PendingInviteTimestamps { get; set; } = new Dictionary<string, long>();

        public VoiceGroup(string name, string ownerPlayerUid)
        {
            Name = name;
            OwnerPlayerUid = ownerPlayerUid;
            InviteOnly = false;
            Members.Add(ownerPlayerUid);
        }

        public VoiceGroup()
        {
        }
    }
}
