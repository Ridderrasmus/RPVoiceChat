using RPVoiceChat.GameContent.Items;
using RPVoiceChat.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace RPVoiceChat.Gui
{
    public class RadioTalkieDialog : GuiDialog
    {
        private readonly ItemSlot talkieSlot;
        private GuiElementTextInput frequencyInput;
        private string pendingFrequency = "";

        public RadioTalkieDialog(ICoreClientAPI capi, ItemSlot talkieSlot) : base(capi)
        {
            this.talkieSlot = talkieSlot;
        }

        public override string ToggleKeyCombinationCode => null;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            pendingFrequency = ItemRadio.GetTunedFrequency(talkieSlot?.Itemstack);
            BuildComposer();
            frequencyInput?.SetValue(pendingFrequency);
        }

        private void BuildComposer()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds labelBounds = ElementBounds.Fixed(0, 35, 320, 18);
            ElementBounds inputBounds = ElementBounds.Fixed(0, 55, 220, 26);
            ElementBounds saveBounds = ElementBounds.Fixed(232, 55, 88, 26);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(labelBounds, inputBounds, saveBounds);

            SingleComposer = capi.Gui.CreateCompo("radiotalkie", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Talkie.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(UIUtils.I18n("Radio.Talkie.Gui.Frequency"), CairoFont.WhiteSmallText(), labelBounds)
                .AddTextInput(inputBounds, value => pendingFrequency = value ?? "", CairoFont.TextInput(), "radioTalkieFrequencyInput")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveClicked, saveBounds)
                .Compose();

            frequencyInput = SingleComposer.GetTextInput("radioTalkieFrequencyInput");
        }

        private bool OnSaveClicked()
        {
            if (talkieSlot?.Itemstack != null)
            {
                ItemRadio.SetTunedFrequency(talkieSlot.Itemstack, pendingFrequency);
                capi.World.Player.InventoryManager.BroadcastHotbarSlot();
            }

            TryClose();
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
