using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class NamedSlider : GuiElementSlider
    {
        public string name;

        private NamedSlider(ICoreClientAPI capi, string name, ActionConsumable<int> callback, ElementBounds bounds) : base(capi, callback, bounds)
        {
            this.name = name;
        }

        public static NamedSlider Create(ICoreClientAPI capi, string name, ActionConsumable<int, string> extendedCallback, ElementBounds bounds)
        {
            ActionConsumable<int> baseCallback = val => extendedCallback(val, name);
            return new NamedSlider(capi, name, baseCallback, bounds);
        }
    }
}
