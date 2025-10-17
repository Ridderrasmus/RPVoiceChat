using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace RPVoiceChat.Gui
{
    public class GuiDialogTelegramReader : GuiDialog
    {
        private string message;
        private new ICoreClientAPI capi;

        public GuiDialogTelegramReader(string title, string message, ICoreClientAPI capi) : base(capi)
        {
            this.message = message;
            this.capi = capi;
            SetupDialog(title);
        }

        private void SetupDialog(string title)
        {
            // Dialog background
            ElementBounds dialogBounds = ElementBounds.Fixed(0, 0, 600, 400).WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(dialogBounds);

            // Single dialog window
            SingleComposer = capi.Gui.CreateCompo("telegramreader-" + System.Guid.NewGuid(), dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddStaticText("Message:", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 40, 560, 30))
                    .AddTextArea(ElementBounds.Fixed(0, 70, 560, 280), OnTextAreaChanged, CairoFont.TextInput(), "message")
                .EndChildElements()
                .Compose();

            // Set the message text
            SingleComposer.GetTextArea("message").SetValue(message);
        }

        private void OnTextAreaChanged(string value)
        {
            // Text area is read-only, so no need to handle changes
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override string ToggleKeyCombinationCode => null;

        public override bool TryOpen()
        {
            if (IsOpened()) return false;
            return base.TryOpen();
        }
    }
}
