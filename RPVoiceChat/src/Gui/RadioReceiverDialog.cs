using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class RadioReceiverDialog : GuiDialogBlockEntity
    {
        private const string RangeSliderKey = "radioReceiverRangeSlider";
        private const string VolumeSliderKey = "radioReceiverVolumeSlider";

        private readonly BlockEntityRadioReceiver receiver;
        private GuiElementTextInput frequencyInput;
        private NamedSlider rangeSlider;
        private NamedSlider volumeSlider;
        private string pendingFrequency = "";
        private string actionButtonLangKey = "Radio.Receiver.Gui.TurnOn";

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
            if (SingleComposer == null)
            {
                return;
            }

            frequencyInput?.SetValue(pendingFrequency);
            rangeSlider?.SetValues(
                receiver.PlaybackRangeBlocks,
                BlockEntityRadioReceiver.MinPlaybackRangeBlocks,
                BlockEntityRadioReceiver.MaxPlaybackRangeBlocks,
                1,
                "");
            volumeSlider?.SetValues(
                receiver.PlaybackVolumePercent,
                BlockEntityRadioReceiver.MinPlaybackVolumePercent,
                BlockEntityRadioReceiver.MaxPlaybackVolumePercent,
                1,
                "%");
            SingleComposer.GetDynamicText("radioReceiverStationText")?.SetNewText(GetStationText());

            string newActionKey = receiver.IsEnabled
                ? "Radio.Receiver.Gui.TurnOff"
                : "Radio.Receiver.Gui.TurnOn";
            if (newActionKey != actionButtonLangKey)
            {
                BuildComposer();
            }
        }

        private string GetStationText()
        {
            string name = receiver.HeardStationName?.Trim() ?? "";
            if (name.Length == 0)
            {
                return UIUtils.I18n("Radio.Receiver.Gui.Station.None");
            }

            return UIUtils.I18n("Radio.Receiver.Gui.Station", name);
        }

        private void BuildComposer()
        {
            actionButtonLangKey = receiver.IsEnabled
                ? "Radio.Receiver.Gui.TurnOff"
                : "Radio.Receiver.Gui.TurnOn";

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds frequencyLabelBounds = ElementBounds.Fixed(0, 35, 420, 18);
            ElementBounds frequencyInputBounds = ElementBounds.Fixed(0, 55, 320, 26);
            ElementBounds frequencySaveBounds = ElementBounds.Fixed(332, 55, 88, 26);
            ElementBounds stationBounds = ElementBounds.Fixed(0, 90, 420, 22);
            ElementBounds toggleBounds = ElementBounds.Fixed(0, 120, 160, 28);
            ElementBounds rangeLabelBounds = ElementBounds.Fixed(0, 160, 420, 18);
            ElementBounds rangeSliderBounds = ElementBounds.Fixed(0, 180, 420, 26);
            ElementBounds volumeLabelBounds = ElementBounds.Fixed(0, 220, 420, 18);
            ElementBounds volumeSliderBounds = ElementBounds.Fixed(0, 240, 420, 26);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(
                frequencyLabelBounds,
                frequencyInputBounds,
                frequencySaveBounds,
                stationBounds,
                toggleBounds,
                rangeLabelBounds,
                rangeSliderBounds,
                volumeLabelBounds,
                volumeSliderBounds);

            rangeSlider = new NamedSlider(capi, RangeSliderKey, OnPlaybackRangeChanged, rangeSliderBounds);
            rangeSlider.SetValues(
                receiver.PlaybackRangeBlocks,
                BlockEntityRadioReceiver.MinPlaybackRangeBlocks,
                BlockEntityRadioReceiver.MaxPlaybackRangeBlocks,
                1,
                "");

            volumeSlider = new NamedSlider(capi, VolumeSliderKey, OnPlaybackVolumeChanged, volumeSliderBounds);
            volumeSlider.SetValues(
                receiver.PlaybackVolumePercent,
                BlockEntityRadioReceiver.MinPlaybackVolumePercent,
                BlockEntityRadioReceiver.MaxPlaybackVolumePercent,
                1,
                "%");

            SingleComposer = capi.Gui.CreateCompo("radioreceiver", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Receiver.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(UIUtils.I18n("Radio.Receiver.Gui.Frequency"), CairoFont.WhiteSmallText(), frequencyLabelBounds)
                .AddTextInput(frequencyInputBounds, OnFrequencyChanged, CairoFont.TextInput(), "radioReceiverFrequencyInput")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveFrequencyClicked, frequencySaveBounds)
                .AddDynamicText(GetStationText(), CairoFont.WhiteSmallText(), stationBounds, "radioReceiverStationText")
                .AddSmallButton(UIUtils.I18n(actionButtonLangKey), OnToggleEnabledClicked, toggleBounds)
                .AddStaticText(UIUtils.I18n("Radio.Receiver.Gui.PlaybackRange"), CairoFont.WhiteSmallText(), rangeLabelBounds)
                .AddInteractiveElement(rangeSlider, RangeSliderKey)
                .AddStaticText(UIUtils.I18n("Radio.Receiver.Gui.PlaybackVolume"), CairoFont.WhiteSmallText(), volumeLabelBounds)
                .AddInteractiveElement(volumeSlider, VolumeSliderKey)
                .Compose();

            frequencyInput = SingleComposer.GetTextInput("radioReceiverFrequencyInput");
            frequencyInput?.SetValue(pendingFrequency);
        }

        private void OnFrequencyChanged(string value) => pendingFrequency = value ?? "";

        private bool OnSaveFrequencyClicked()
        {
            receiver.RequestSetFrequency(pendingFrequency);
            return true;
        }

        private bool OnToggleEnabledClicked()
        {
            receiver.RequestSetEnabled(!receiver.IsEnabled);
            return true;
        }

        private bool OnPlaybackRangeChanged(int value, string _)
        {
            receiver.RequestSetPlaybackRange(value);
            return true;
        }

        private bool OnPlaybackVolumeChanged(int value, string _)
        {
            receiver.RequestSetPlaybackVolume(value);
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
