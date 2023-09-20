using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            if (!api.World.Config.GetBool("rpvoicechat:extra-content")) return;
            api.RegisterItemClass("soundemittingitem", typeof(SoundEmittingItem));
            api.RegisterItemClass("handheldradio", typeof(RadioItem));
            api.RegisterItemClass("telegraphwire", typeof(TelegraphWireItem));
        }
    }
}
