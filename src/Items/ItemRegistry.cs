using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            if (WorldConfig.GetBool("extra-content") == false) return;
            api.RegisterItemClass("soundemittingitem", typeof(SoundEmittingItem));
            api.RegisterItemClass("handheldradio", typeof(RadioItem));
            api.RegisterItemClass("telegraphwire", typeof(TelegraphWireItem));
        }
    }
}
