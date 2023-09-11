
using Vintagestory.API.Common;

namespace RPVoiceChat.Items
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            api.RegisterItemClass("soundemittingitem", typeof(SoundEmittingItem));
            api.RegisterItemClass("handheldradio", typeof(RadioItem));
            api.RegisterItemClass("telegraphwire", typeof(TelegraphWireItem));
        }
    }
}
