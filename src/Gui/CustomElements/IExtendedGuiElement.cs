using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public interface IExtendedGuiElement
    {
        public void Init(string elementKey, ElementBounds bounds, GuiComposer composer);
        public void OnAdd(GuiComposer composer) { }
    }
}
