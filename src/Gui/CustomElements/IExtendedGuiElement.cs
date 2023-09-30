using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public interface IExtendedGuiElement
    {
        public void SetKey(string elementKey);
        public void SetBounds(ElementBounds bounds);
    }
}
