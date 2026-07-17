using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Networking.Packets;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class RadioEmitterDialog : GuiDialogBlockEntity
    {
        private readonly BlockEntityRadioEmitter emitter;
        private GuiElementDynamicText statusText;
        private GuiElementTextInput repeaterFrequencyInput;
        private string pendingRepeaterFrequency = "";

        public RadioEmitterDialog(ICoreClientAPI capi, BlockEntityRadioEmitter emitter)
            : base(UIUtils.I18n("Radio.Emitter.Gui.Title"), emitter.Pos, capi)
        {
            this.emitter = emitter;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            BuildComposer();
            RefreshData();
        }

        public void RefreshData()
        {
            statusText?.SetNewText(BuildStatusText());
        }

        private string BuildStatusText()
        {
            string mode = emitter.IsRepeaterMode
                ? UIUtils.I18n("Radio.Emitter.Mode.Repeater")
                : UIUtils.I18n("Radio.Emitter.Mode.WiredSource");
            int power = (int)System.Math.Round(emitter.PowerPercent * 100);
            int range = emitter.GetEffectiveTransmitRangeBlocks();
            string frequency = emitter.IsRepeaterMode
                ? (string.IsNullOrWhiteSpace(emitter.RepeaterFrequency) ? UIUtils.I18n("Radio.Emitter.Gui.NoConsole") : emitter.RepeaterFrequency)
                : (string.IsNullOrWhiteSpace(emitter.GetConsoleFrequency()) ? UIUtils.I18n("Radio.Emitter.Gui.NoConsole") : emitter.GetConsoleFrequency());
            string name = emitter.GetConsoleDisplayName();
            return UIUtils.I18n("Radio.Emitter.Gui.Status", mode, power, range, frequency, name);
        }

        private void BuildComposer()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 35, 420, 72);
            ElementBounds wiredButtonBounds = ElementBounds.Fixed(0, 120, 204, 28);
            ElementBounds repeaterButtonBounds = ElementBounds.Fixed(216, 120, 204, 28);
            ElementBounds repeaterLabelBounds = ElementBounds.Fixed(0, 156, 420, 18);
            ElementBounds repeaterInputBounds = ElementBounds.Fixed(0, 176, 320, 26);
            ElementBounds repeaterSaveBounds = ElementBounds.Fixed(332, 176, 88, 26);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(statusBounds, wiredButtonBounds, repeaterButtonBounds, repeaterLabelBounds, repeaterInputBounds, repeaterSaveBounds);

            pendingRepeaterFrequency = emitter.RepeaterFrequency;
            SingleComposer = capi.Gui.CreateCompo("radioemitter", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Emitter.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(BuildStatusText(), CairoFont.WhiteSmallText(), statusBounds, "radioEmitterStatus")
                .AddSmallButton(UIUtils.I18n("Radio.Emitter.Mode.WiredSource"), OnWiredSourceClicked, wiredButtonBounds)
                .AddSmallButton(UIUtils.I18n("Radio.Emitter.Mode.Repeater"), OnRepeaterClicked, repeaterButtonBounds)
                .AddStaticText(UIUtils.I18n("Radio.Emitter.Gui.RepeaterFrequency"), CairoFont.WhiteSmallText(), repeaterLabelBounds)
                .AddTextInput(repeaterInputBounds, value => pendingRepeaterFrequency = value ?? "", CairoFont.TextInput(), "radioEmitterRepeaterFrequency")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveRepeaterFrequencyClicked, repeaterSaveBounds)
                .Compose();

            statusText = SingleComposer.GetDynamicText("radioEmitterStatus");
            repeaterFrequencyInput = SingleComposer.GetTextInput("radioEmitterRepeaterFrequency");
            repeaterFrequencyInput?.SetValue(pendingRepeaterFrequency);
        }

        private bool OnWiredSourceClicked()
        {
            emitter.RequestSetOperatingMode(RadioEmitterOperatingMode.WiredSource);
            return true;
        }

        private bool OnRepeaterClicked()
        {
            emitter.RequestSetOperatingMode(RadioEmitterOperatingMode.Repeater);
            return true;
        }

        private bool OnSaveRepeaterFrequencyClicked()
        {
            emitter.RequestSetRepeaterFrequency(pendingRepeaterFrequency);
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
