using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class NamedSlider : GuiElementSlider
    {
        public string name { get; }

        public NamedSlider(ICoreClientAPI capi, string name, ActionConsumable<int, string> extendedCallback, ElementBounds bounds) : base(capi, WrapCallback(name, extendedCallback), bounds)
        {
            this.name = name;
        }

        private static ActionConsumable<int> WrapCallback(string name, ActionConsumable<int, string> extendedCallback)
        {
            ActionConsumable<int> baseCallback = val => extendedCallback(val, name);
            return baseCallback;
        }
    }
}
