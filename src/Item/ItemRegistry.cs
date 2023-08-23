
using Vintagestory.API.Common;

namespace RPVoiceChat
{
    public class ItemRegistry
    {
        public static void RegisterItems(ICoreAPI api)
        {
            api.RegisterItemClass("BellItem", typeof(BellItem));
            //api.RegisterItemClass("ItemRadio", typeof(ItemRadio));
            //api.RegisterItemClass("ItemRadioHeadset", typeof(ItemRadioHeadset));
            //api.RegisterItemClass("ItemRadioHandheld", typeof(ItemRadioHandheld));
            //api.RegisterItemClass("ItemRadioStation", typeof(ItemRadioStation));
            //api.RegisterItemClass("ItemRadioStationHeadset", typeof(ItemRadioStationHeadset));
            //api.RegisterItemClass("ItemRadioStationHandheld", typeof(ItemRadioStationHandheld));
        }
    }
}
