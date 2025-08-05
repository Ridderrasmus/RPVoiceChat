using RPVoiceChat.Items;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            if (WorldConfig.GetBool("additional-content") == false) return;
            api.RegisterItemClass("soundemittingitem", typeof(SoundEmittingItem));
            api.RegisterItemClass("voiceamplifieritem", typeof(VoiceAmplifierItem));
            api.RegisterItemClass("handheldradio", typeof(RadioItem));
            api.RegisterItemClass("telegraphwire", typeof(TelegraphWireItem));
        }
    }
}
