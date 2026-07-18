using RPVoiceChat.GameContent.BlockEntity;
using RPVoiceChat.Util;
using Vintagestory.API.Client;

namespace RPVoiceChat.Gui
{
    public class RadioMixingConsoleDialog : GuiDialogBlockEntity
    {
        private readonly BlockEntityRadioMixingConsole mixingConsole;
        private GuiElementTextInput hlsUrlInput;
        private string pendingHlsUrl = "";
        private string actionButtonLangKey = "Radio.MixingConsole.Gui.TurnOn";

        public RadioMixingConsoleDialog(ICoreClientAPI capi, BlockEntityRadioMixingConsole mixingConsole)
            : base(UIUtils.I18n("Radio.MixingConsole.Gui.Title"), mixingConsole.Pos, capi)
        {
            this.mixingConsole = mixingConsole;
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

            pendingHlsUrl = mixingConsole.HlsStreamUrl;
            hlsUrlInput?.SetValue(pendingHlsUrl);
            BuildComposer();
        }

        /// <summary>
        /// Only situational notices — On/Off is already covered by the button + block On Air light.
        /// </summary>
        private string GetStatusText()
        {
            if (mixingConsole.IsOnAir && !mixingConsole.HasActiveBroadcastOutput())
            {
                return UIUtils.I18n("Radio.MixingConsole.Gui.Status.WaitingPower");
            }

            if (mixingConsole.IsBusyForOtherPlayer(capi.World.Player.PlayerUID))
            {
                return UIUtils.I18n("Radio.MixingConsole.Gui.Status.Busy");
            }

            return "";
        }

        private void BuildComposer()
        {
            actionButtonLangKey = mixingConsole.IsOnAir
                ? "Radio.MixingConsole.Gui.TurnOff"
                : "Radio.MixingConsole.Gui.TurnOn";

            string statusText = GetStatusText();
            bool showStatus = statusText.Length > 0;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds urlLabelBounds = ElementBounds.Fixed(0, 35, 520, 18);
            ElementBounds urlInputBounds = ElementBounds.Fixed(0, 55, 400, 26);
            ElementBounds urlSaveBounds = ElementBounds.Fixed(412, 55, 88, 26);
            ElementBounds statusBounds = ElementBounds.Fixed(0, 90, 520, 36);
            ElementBounds toggleBounds = ElementBounds.Fixed(0, showStatus ? 132 : 95, 180, 28);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            if (showStatus)
            {
                bgBounds.WithChildren(urlLabelBounds, urlInputBounds, urlSaveBounds, statusBounds, toggleBounds);
            }
            else
            {
                bgBounds.WithChildren(urlLabelBounds, urlInputBounds, urlSaveBounds, toggleBounds);
            }

            var composer = capi.Gui.CreateCompo("radiomixingconsole", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(UIUtils.I18n("Radio.MixingConsole.Gui.Title"), OnTitleBarCloseClicked)
                .AddStaticText(UIUtils.I18n("Radio.MixingConsole.Gui.HlsUrl"), CairoFont.WhiteSmallText(), urlLabelBounds)
                .AddTextInput(urlInputBounds, OnHlsUrlChanged, CairoFont.TextInput(), "radioMixingHlsUrlInput")
                .AddSmallButton(UIUtils.I18n("Radio.Gui.Save"), OnSaveHlsUrlClicked, urlSaveBounds);

            if (showStatus)
            {
                composer.AddDynamicText(statusText, CairoFont.WhiteSmallText(), statusBounds, "radioMixingStatusText");
            }

            SingleComposer = composer
                .AddSmallButton(UIUtils.I18n(actionButtonLangKey), OnToggleOnAirClicked, toggleBounds)
                .Compose();

            hlsUrlInput = SingleComposer.GetTextInput("radioMixingHlsUrlInput");
            hlsUrlInput?.SetValue(pendingHlsUrl);
        }

        private void OnHlsUrlChanged(string value) => pendingHlsUrl = value ?? "";

        private bool OnSaveHlsUrlClicked()
        {
            if (BlockEntityRadioMixingConsole.NormalizeHlsUrl(pendingHlsUrl) == null && !string.IsNullOrWhiteSpace(pendingHlsUrl))
            {
                capi.TriggerIngameError(this, "radio-hls-url-invalid", UIUtils.I18n("Radio.MixingConsole.Error.InvalidUrl"));
                return true;
            }

            mixingConsole.RequestSetHlsUrl(pendingHlsUrl);
            return true;
        }

        private bool OnToggleOnAirClicked()
        {
            string playerUid = capi.World.Player.PlayerUID;
            bool enable = !mixingConsole.IsOnAir;

            if (enable)
            {
                if (mixingConsole.IsBusyForOtherPlayer(playerUid))
                {
                    capi.TriggerIngameError(this, "radio-mixing-busy", UIUtils.I18n("Radio.MixingConsole.Error.Busy"));
                    return true;
                }

                if (mixingConsole.NetworkUID == 0)
                {
                    capi.TriggerIngameError(this, "radio-mixing-not-wired", UIUtils.I18n("Radio.MixingConsole.Error.NotWired"));
                    return true;
                }

                if (!mixingConsole.HasWiredBroadcastPath())
                {
                    capi.TriggerIngameError(this, "radio-mixing-no-path", UIUtils.I18n("Radio.MixingConsole.Error.NoBroadcastPath"));
                    return true;
                }
            }
            else if (!mixingConsole.IsOperator(playerUid))
            {
                capi.TriggerIngameError(this, "radio-mixing-not-operator", UIUtils.I18n("Radio.MixingConsole.Error.NotOperator"));
                return true;
            }

            mixingConsole.RequestSetOnAir(enable);
            return true;
        }

        private void OnTitleBarCloseClicked()
        {
            TryClose();
        }
    }
}
