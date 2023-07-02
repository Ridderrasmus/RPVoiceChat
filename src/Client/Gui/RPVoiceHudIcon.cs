using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace rpvoicechat.src.Client.Gui
{
    public class RPVoiceHudIcon : HudElement
    {

        public string currentVoice { get; set; } = "trumpet";

        public override bool ShouldReceiveMouseEvents() => false;

        public RPVoiceHudIcon(ICoreClientAPI capi) : base(capi)
        {
            // Get the voice name as string and select the enum value that fits that name
            //CurrentVoice = (CharacterVoice)Enum.Parse(typeof(CharacterVoice), capi.World.Player.Entity.talkUtil.soundName.GetName());
        }
    }
}
