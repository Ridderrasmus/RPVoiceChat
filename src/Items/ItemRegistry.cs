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
            api.RegisterItemClass("soundemittingitem", typeof(SoundEmittingItem));
            api.RegisterItemClass("voiceamplifieritem", typeof(VoiceAmplifierItem));
            api.RegisterItemClass("handheldradio", typeof(RadioItem));
            api.RegisterItemClass("telegraphwire", typeof(TelegraphWireItem));
            api.RegisterItemClass("telegram", typeof(ItemTelegram));
        }
    }
}
