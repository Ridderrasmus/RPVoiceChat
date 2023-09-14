using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RPVoiceChat.Networking.Packets
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class DisabledRecipesPacket
    {
        public List<string> DisabledRecipes { get; set; }

        public DisabledRecipesPacket() { }

        public DisabledRecipesPacket(List<string> disabledRecipes)
        {
            DisabledRecipes = disabledRecipes;
        }
    }
}
