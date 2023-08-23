
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            api.RegisterItemClass("handbell", typeof(HandbellItem));
            api.RegisterItemClass("handheldradio", typeof(RadioItem));
            api.RegisterItemClass("telegraphwire", typeof(TelegraphWireItem))
        }
    }
}
