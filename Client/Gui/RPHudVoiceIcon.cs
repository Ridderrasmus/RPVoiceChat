using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;

namespace rpvoicechat.Client.Gui
{
    public class RPHudVoiceIcon : HudElement
    {

        public enum CharacterVoice
        {
            altoflute,
            harmonica,
            oboe,
            clarinet,
            accordion,
            trumpet,
            sax,
            tuba
        }

        public CharacterVoice characterVoice = CharacterVoice.sax;

        public RPHudVoiceIcon(ICoreClientAPI capi) : base(capi)
        {
            
        }

        public void ComposeGuis()
        {
            float sideLength = 32;
            ElementBounds dialogBounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.CenterFixed,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = sideLength,
                fixedHeight = sideLength,
                fixedY = sideLength
            }.WithFixedAlignmentOffset(0, 5);
        }
    }
}
