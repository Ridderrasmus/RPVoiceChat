using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Systems;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class RadioMicrophoneDialog : GuiDialogBlockEntity
    {
        private readonly BlockEntityRadioMicrophone microphone;
        private string actionButtonLangKey = "Radio.Microphone.Gui.TurnOn";

        public RadioMicrophoneDialog(ICoreClientAPI capi, BlockEntityRadioMicrophone microphone)
            : base(UIUtils.I18n("Radio.Microphone.Gui.Title"), microphone.Pos, capi)
        {
            this.microphone = microphone;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            BuildComposer();
            RefreshData();
        }

        public void RefreshData()
        {
            if (SingleComposer == null)
            {
                return;
            }

            BuildComposer();
        }

        private string GetStatusText()
        {
            if (microphone.IsTransmitting)
            {
                if (RadioWireNetworkHelper.HasOnAirMixingConsole(microphone))
                {
                    return UIUtils.I18n("Radio.Microphone.Gui.Status.ViaMixingConsole");
                }

                return UIUtils.I18n("Radio.Microphone.Gui.Status.On");
            }

            if (microphone.IsBusyForOtherPlayer(capi.World.Player.PlayerUID))
            {
                return UIUtils.I18n("Radio.Microphone.Gui.Status.Busy");
            }

            if (RadioWireNetworkHelper.HasOnAirMixingConsole(microphone))
            {
                return UIUtils.I18n("Radio.Microphone.Gui.Status.MixingConsoleOnAir");
            }

            return UIUtils.I18n("Radio.Microphone.Gui.Status.Off");
        }

        private void BuildComposer()
        {
            actionButtonLangKey = microphone.IsTransmitting
                ? "Radio.Microphone.Gui.TurnOff"
                : "Radio.Microphone.Gui.TurnOn";

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 35, 420, 40);
            ElementBounds toggleBounds = ElementBounds.Fixed(0, 85, 160, 28);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(statusBounds, toggleBounds);

            SingleComposer = capi.Gui.CreateCompo("radiomicrophone", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.Microphone.Gui.Title"), OnTitleBarCloseClicked)
                .AddDynamicText(GetStatusText(), CairoFont.WhiteSmallText(), statusBounds, "radioMicStatusText")
                .AddSmallButton(UIUtils.I18n(actionButtonLangKey), OnToggleClicked, toggleBounds)
                .Compose();
        }

        private bool OnToggleClicked()
        {
            string playerUid = capi.World.Player.PlayerUID;
            bool enable = !microphone.IsTransmitting;

            if (enable && microphone.IsBusyForOtherPlayer(playerUid))
            {
                capi.TriggerIngameError(this, "radio-mic-busy", UIUtils.I18n("Radio.Microphone.Error.Busy"));
                return true;
            }

            if (!enable && !microphone.IsOperator(playerUid))
            {
                capi.TriggerIngameError(this, "radio-mic-not-operator", UIUtils.I18n("Radio.Microphone.Error.NotOperator"));
                return true;
            }

            microphone.RequestSetTransmitting(enable);
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
