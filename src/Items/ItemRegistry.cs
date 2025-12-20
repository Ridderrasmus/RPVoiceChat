using RPVoiceChat.Config;
using Vintagestory.API.Common;

namespace RPVoiceChat.GameContent.Items
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            // Toujours enregistrer les classes, même si le contenu est désactivé
            // Les items seront désactivés dans AssetsFinalize si nécessaire
            api.RegisterItemClass("soundemitting", typeof(ItemSoundEmitting));
            api.RegisterItemClass("voiceamplifier", typeof(ItemVoiceAmplifier));
            api.RegisterItemClass("handheldradio", typeof(ItemRadio));
            api.RegisterItemClass("telegraphwire", typeof(ItemTelegraphWire));
            api.RegisterItemClass("telegram", typeof(ItemTelegram));
            api.RegisterItemClass("wirecutter", typeof(ItemWireCutter));
        }
    }
}
