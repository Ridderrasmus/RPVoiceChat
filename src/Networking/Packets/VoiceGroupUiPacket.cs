using System.Collections.Generic;
using ProtoBuf;

namespace RPVoiceChat.Networking
{
    public enum VoiceGroupAction
    {
        Refresh, Create, Join, Leave, Delete, Kick, Invite, SetInviteOnly, Accept, Decline, SetEnabled
    }

    // Separate, explicitly numbered contracts keep the original group-state wire format intact.
    [ProtoContract]
    public class VoiceGroupActionPacket
    {
        [ProtoMember(1)] public VoiceGroupAction Action { get; set; }
        [ProtoMember(2)] public string GroupName { get; set; }
        [ProtoMember(3)] public string TargetPlayerUid { get; set; }
        [ProtoMember(4)] public bool Value { get; set; }
    }

    [ProtoContract]
    public class VoiceGroupPlayerEntry
    {
        [ProtoMember(1)] public string Uid { get; set; }
        [ProtoMember(2)] public string Name { get; set; }
        [ProtoMember(3)] public bool Online { get; set; }
    }

    [ProtoContract]
    public class VoiceGroupUiStatePacket
    {
        [ProtoMember(1)] public bool Enabled { get; set; }
        [ProtoMember(2)] public bool CanManageServer { get; set; }
        [ProtoMember(3)] public List<VoiceGroupStateEntry> Groups { get; set; } = new();
        [ProtoMember(4)] public List<string> Invitations { get; set; } = new();
        [ProtoMember(5)] public List<VoiceGroupPlayerEntry> Players { get; set; } = new();
    }

    [ProtoContract]
    public class VoiceGroupActionResultPacket
    {
        [ProtoMember(1)] public bool Success { get; set; }
        [ProtoMember(2)] public string Message { get; set; }
    }
}
