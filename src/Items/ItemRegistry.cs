using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            // Always register all item classes - items will be disabled via JSON patches if needed
            // This prevents "no such class registered" errors when JSON files reference these classes
            api.RegisterItemClass("soundemitting", typeof(ItemSoundEmitting));
            api.RegisterItemClass("voiceamplifier", typeof(ItemVoiceAmplifier));
            api.RegisterItemClass("handheldradio", typeof(ItemRadio));
            api.RegisterItemClass("telegraphwire", typeof(ItemTelegraphWire));
            api.RegisterItemClass("telegram", typeof(ItemTelegram));
            api.RegisterItemClass("wirecutter", typeof(ItemWireCutter));
        }
    }
}
