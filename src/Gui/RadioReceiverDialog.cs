using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class RadioReceiverDialog : GuiDialogBlockEntity
    {
        private readonly BlockEntityRadioReceiver receiver;
        private GuiElementTextInput frequencyInput;
        private string pendingFrequency = "";

        public RadioReceiverDialog(ICoreClientAPI capi, BlockEntityRadioReceiver receiver)
            : base(UIUtils.I18n("Radio.Receiver.Gui.Title"), receiver.Pos, capi)
        {
            this.receiver = receiver;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            BuildComposer();
            pendingFrequency = receiver.TunedFrequency;
            frequencyInput?.SetValue(pendingFrequency);
            RefreshData();
        }

        public void RefreshData()
        {
            pendingFrequency = receiver.TunedFrequency;
            frequencyInput?.SetValue(pendingFrequency);
        }

        private void BuildComposer()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds frequencyLabelBounds = ElementBounds.Fixed(0, 35, 420, 18);
            ElementBounds frequencyInputBounds = ElementBounds.Fixed(0, 55, 320, 26);
            ElementBounds frequencySaveBounds = ElementBounds.Fixed(332, 55, 88, 26);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(frequencyLabelBounds, frequencyInputBounds, frequencySaveBounds);

            SingleComposer = capi.Gui.CreateCompo("radioreceiver", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Receiver.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(UIUtils.I18n("Radio.Receiver.Gui.Frequency"), CairoFont.WhiteSmallText(), frequencyLabelBounds)
                .AddTextInput(frequencyInputBounds, OnFrequencyChanged, CairoFont.TextInput(), "radioReceiverFrequencyInput")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveFrequencyClicked, frequencySaveBounds)
                .Compose();

            frequencyInput = SingleComposer.GetTextInput("radioReceiverFrequencyInput");
        }

        private void OnFrequencyChanged(string value) => pendingFrequency = value ?? "";

        private bool OnSaveFrequencyClicked()
        {
            receiver.RequestSetFrequency(pendingFrequency);
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
