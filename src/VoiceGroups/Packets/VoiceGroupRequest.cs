using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPVoiceChat.VoiceGroups.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VoiceGroupRequest
    {
        public string GroupName { get; set; }

        public VoiceGroupRequest() { }
    }
}
