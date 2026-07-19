using RPVoiceChat.Config;
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
        private GuiElementDynamicText rangeText;
        private GuiElementTextInput repeaterFrequencyInput;
        private string pendingRepeaterFrequency = "";
        private bool showingRepeaterControls;

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
            if (showingRepeaterControls != emitter.IsRepeaterMode)
            {
                BuildComposer();
            }

            statusText?.SetNewText(BuildStatusText());
            rangeText?.SetNewText(BuildRangeText());
            if (emitter.IsRepeaterMode)
            {
                pendingRepeaterFrequency = emitter.RepeaterFrequency;
                repeaterFrequencyInput?.SetValue(pendingRepeaterFrequency);
            }
        }

        private string BuildStatusText()
        {
            string mode = emitter.IsRepeaterMode
                ? UIUtils.I18n("Radio.Emitter.Mode.Repeater")
                : UIUtils.I18n("Radio.Emitter.Mode.WiredSource");
            int power = (int)System.Math.Round(emitter.PowerPercent * 100);
            int minPower = ServerConfigManager.RadioNetworkMinPowerPercent;
            string frequency = emitter.IsRepeaterMode
                ? (string.IsNullOrWhiteSpace(emitter.RepeaterFrequency) ? UIUtils.I18n("Radio.Emitter.Gui.NoConsole") : emitter.RepeaterFrequency)
                : (string.IsNullOrWhiteSpace(emitter.GetConsoleFrequency()) ? UIUtils.I18n("Radio.Emitter.Gui.NoConsole") : emitter.GetConsoleFrequency());
            string name = emitter.GetConsoleDisplayName();

            string status = UIUtils.I18n("Radio.Emitter.Gui.Status", mode, power, frequency, name);
            if (!emitter.HasSufficientTransmitPower())
            {
                status += "\n" + UIUtils.I18n("Radio.Emitter.Gui.InsufficientPower", minPower);
            }

            return status;
        }

        private string BuildRangeText()
        {
            return UIUtils.I18n("Radio.Emitter.Gui.Range", emitter.GetEffectiveTransmitRangeBlocks());
        }

        private void BuildComposer()
        {
            showingRepeaterControls = emitter.IsRepeaterMode;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds rangeBounds = ElementBounds.Fixed(0, 35, 420, 22);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 76, 420, 80);
            ElementBounds wiredButtonBounds = ElementBounds.Fixed(0, 164, 204, 28);
            ElementBounds repeaterButtonBounds = ElementBounds.Fixed(216, 164, 204, 28);
            ElementBounds repeaterLabelBounds = ElementBounds.Fixed(0, 200, 420, 18);
            ElementBounds repeaterInputBounds = ElementBounds.Fixed(0, 220, 320, 26);
            ElementBounds repeaterSaveBounds = ElementBounds.Fixed(332, 220, 88, 26);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            if (showingRepeaterControls)
            {
                bgBounds.WithChildren(rangeBounds, statusBounds, wiredButtonBounds, repeaterButtonBounds, repeaterLabelBounds, repeaterInputBounds, repeaterSaveBounds);
            }
            else
            {
                bgBounds.WithChildren(rangeBounds, statusBounds, wiredButtonBounds, repeaterButtonBounds);
            }

            pendingRepeaterFrequency = emitter.RepeaterFrequency;
            var composer = capi.Gui.CreateCompo("radioemitter", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Emitter.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(BuildRangeText(), CairoFont.WhiteSmallishText(), rangeBounds, "radioEmitterRange")
                .AddDynamicText(BuildStatusText(), CairoFont.WhiteSmallText(), statusBounds, "radioEmitterStatus")
                .AddSmallButton(UIUtils.I18n("Radio.Emitter.Mode.WiredSource"), OnWiredSourceClicked, wiredButtonBounds)
                .AddSmallButton(UIUtils.I18n("Radio.Emitter.Mode.Repeater"), OnRepeaterClicked, repeaterButtonBounds);

            if (showingRepeaterControls)
            {
                composer
                    .AddStaticText(UIUtils.I18n("Radio.Emitter.Gui.RepeaterFrequency"), CairoFont.WhiteSmallText(), repeaterLabelBounds)
                    .AddTextInput(repeaterInputBounds, value => pendingRepeaterFrequency = value ?? "", CairoFont.TextInput(), "radioEmitterRepeaterFrequency")
                    .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveRepeaterFrequencyClicked, repeaterSaveBounds);
            }

            SingleComposer = composer.Compose();

            rangeText = SingleComposer.GetDynamicText("radioEmitterRange");
            statusText = SingleComposer.GetDynamicText("radioEmitterStatus");
            repeaterFrequencyInput = showingRepeaterControls
                ? SingleComposer.GetTextInput("radioEmitterRepeaterFrequency")
                : null;
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
